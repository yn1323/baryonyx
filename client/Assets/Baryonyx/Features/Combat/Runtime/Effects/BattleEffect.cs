using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>How often a rule may work.</summary>
    public enum BattleLimitSpan
    {
        /// <summary>Counted anew as each party turn begins.</summary>
        Turn,

        /// <summary>Counted anew as the battle begins.</summary>
        Battle,
    }

    /// <summary>How long an effect put on for a while (<see cref="BattleAct.Hold"/>) lasts.</summary>
    public enum BattleHoldSpan
    {
        /// <summary>Until its holder next acts (次の自分の行動まで).</summary>
        UntilOwnerActs,

        /// <summary>For a number of turns, counted as each party turn begins.</summary>
        Turns,

        /// <summary>Until the battle ends.</summary>
        Battle,
    }

    /// <summary>Whether a rule shows the effect's name when it works.</summary>
    public enum BattleShow
    {
        Quiet,

        /// <summary>Each time it works.</summary>
        Always,

        /// <summary>Once in a card's play however often it works there (each time outside one).</summary>
        OncePerCard,

        /// <summary>
        /// For a value read all through the battle (a stat): each time what it adds starts or
        /// grows (背水・弔い), as <see cref="BattleEffects.ShowGrown"/> watches it.
        /// </summary>
        WhenGrows,
    }

    /// <summary>
    /// One rule of an effect: either it changes a number (<see cref="IsValue"/>: a
    /// <see cref="BattleValue"/> grown by fixed amounts and percents), or it acts when something
    /// happens (a <see cref="BattleTrigger"/> and its <see cref="Acts"/>). Either works only
    /// while its condition holds, as often as its limit lets it.
    /// </summary>
    public sealed class BattleEffectRule
    {
        internal BattleEffectRule(int index) => Index = index;

        /// <summary>Its place among its effect's rules.</summary>
        public int Index { get; }

        public bool IsValue { get; internal set; }
        public BattleTrigger Trigger { get; internal set; }
        public BattleValue Value { get; internal set; }
        public BattleCondition Condition { get; internal set; } = BattleCondition.Always;
        public IReadOnlyList<BattleAct> Acts { get; internal set; } = Array.Empty<BattleAct>();

        /// <summary>For a value: the amount added, and the percent it grows by.</summary>
        public BattleAmount Flat { get; internal set; }
        public BattleAmount Percent { get; internal set; }

        /// <summary>
        /// For a value: the percent it is multiplied by after the sums, on its own and rounded
        /// (攻撃力アップ・防御ダウン・プロテクト), in <see cref="Order"/>.
        /// </summary>
        public BattleAmount Multiplier { get; internal set; }

        /// <summary>Where a multiplier comes among the others (smaller first).</summary>
        public int Order { get; internal set; }

        /// <summary>How many times it may work in its span (0: as often as it holds).</summary>
        public int Limit { get; internal set; }
        public BattleLimitSpan LimitSpan { get; internal set; }

        /// <summary>True when chance decides what it does: a forecast leaves it out.</summary>
        public bool Random { get; internal set; }

        public BattleShow Show { get; internal set; }

        /// <summary>Over whom the name shows (the owner unless set).</summary>
        public BattleWho ShowOver { get; internal set; } = BattleWho.Owner;
    }

    /// <summary>
    /// An effect of the battle with a name: a passive skill, an item's effect, a card's own rule,
    /// a status (doc/plans/2026-11-03-battle-effect-system.md). Written once as rules that join
    /// a trigger or a value with a condition and acts, it works wherever its holder stands. Built
    /// rule by rule: <see cref="On"/>, <see cref="Add"/> and <see cref="AddPercent"/> begin a
    /// rule, and the calls after one (<see cref="When"/>, <see cref="OncePerTurn"/>,
    /// <see cref="Shows"/>...) set that rule.
    /// </summary>
    public sealed class BattleEffect
    {
        private readonly List<BattleEffectRule> rules = new();

        public BattleEffect(string id, string name)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? "";
        }

        public string Id { get; }

        /// <summary>The name shown as it works.</summary>
        public string Name { get; }

        /// <summary>The most stacks it holds (0: it holds none).</summary>
        public int MaxStacks { get; private set; }

        /// <summary>The status this effect is the working of, or None (<see cref="BattleStatuses"/>).</summary>
        public CardStatus Status { get; private set; }

        /// <summary>Among the statuses on one character, the effects of smaller priority are asked first (無敵 before 反射).</summary>
        public int Priority { get; private set; }

        public IReadOnlyList<BattleEffectRule> Rules => rules;

        /// <summary>A rule that does <paramref name="acts"/> when <paramref name="trigger"/> happens; it shows the name.</summary>
        public BattleEffect On(BattleTrigger trigger, params BattleAct[] acts)
        {
            var rule = Begin();
            rule.Trigger = trigger;
            rule.Acts = acts ?? Array.Empty<BattleAct>();
            rule.Show = BattleShow.Always;
            return this;
        }

        /// <summary>A rule that adds <paramref name="amount"/> to <paramref name="value"/>; it shows nothing.</summary>
        public BattleEffect Add(BattleValue value, BattleAmount amount)
        {
            var rule = Begin();
            rule.IsValue = true;
            rule.Value = value;
            rule.Flat = amount;
            return this;
        }

        /// <summary>A rule that grows <paramref name="value"/> by <paramref name="percent"/>%; it shows nothing.</summary>
        public BattleEffect AddPercent(BattleValue value, BattleAmount percent)
        {
            var rule = Begin();
            rule.IsValue = true;
            rule.Value = value;
            rule.Percent = percent;
            return this;
        }

        /// <summary>
        /// A rule that multiplies <paramref name="value"/> by (1 + <paramref name="percent"/>/100)
        /// after the sums, rounded on its own; the multipliers go in <paramref name="order"/>.
        /// </summary>
        public BattleEffect Multiply(BattleValue value, BattleAmount percent, int order = 0)
        {
            var rule = Begin();
            rule.IsValue = true;
            rule.Value = value;
            rule.Multiplier = percent;
            rule.Order = order;
            return this;
        }

        /// <summary>The last rule works only while <paramref name="condition"/> holds too.</summary>
        public BattleEffect When(BattleCondition condition)
        {
            var rule = Last();
            rule.Condition =
                rule.Condition == BattleCondition.Always ? condition : rule.Condition & condition;
            return this;
        }

        public BattleEffect OncePerTurn() => Limit(1, BattleLimitSpan.Turn);

        public BattleEffect OncePerBattle() => Limit(1, BattleLimitSpan.Battle);

        /// <summary>The last rule works at most <paramref name="times"/> in each <paramref name="span"/>.</summary>
        public BattleEffect Limit(int times, BattleLimitSpan span)
        {
            var rule = Last();
            rule.Limit = Math.Max(0, times);
            rule.LimitSpan = span;
            return this;
        }

        /// <summary>Chance decides what the last rule does, so a forecast leaves it out.</summary>
        public BattleEffect AtRandom()
        {
            Last().Random = true;
            return this;
        }

        /// <summary>How the last rule shows the name.</summary>
        public BattleEffect Shows(BattleShow show)
        {
            Last().Show = show;
            return this;
        }

        /// <summary>The last rule shows the name over <paramref name="who"/> rather than the owner.</summary>
        public BattleEffect ShowsOver(BattleWho who)
        {
            var rule = Last();
            rule.ShowOver = who ?? BattleWho.Owner;
            if (rule.Show == BattleShow.Quiet)
                rule.Show = BattleShow.Always;
            return this;
        }

        /// <summary>The effect is the working of <paramref name="status"/>, held while the status is on (<see cref="BattleStatuses"/>).</summary>
        public BattleEffect OfStatus(CardStatus status, int priority = 0)
        {
            Status = status;
            Priority = priority;
            return this;
        }

        /// <summary>The effect holds up to <paramref name="max"/> stacks, from none as the battle begins.</summary>
        public BattleEffect Stacks(int max)
        {
            MaxStacks = Math.Max(0, max);
            return this;
        }

        private BattleEffectRule Begin()
        {
            var rule = new BattleEffectRule(rules.Count);
            rules.Add(rule);
            return rule;
        }

        private BattleEffectRule Last() =>
            rules.Count > 0
                ? rules[^1]
                : throw new InvalidOperationException(
                    $"{Id}: begin a rule with On, Add or AddPercent first."
                );
    }
}
