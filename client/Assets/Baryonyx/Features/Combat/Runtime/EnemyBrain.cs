using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>What an enemy sees as it picks its next skill.</summary>
    public readonly struct EnemySituation
    {
        public EnemySituation(
            int turn,
            float hp,
            bool others,
            float othersLowest,
            float partyLowest,
            bool partyGuarded
        )
        {
            Turn = turn;
            Hp = hp;
            Others = others;
            OthersLowest = othersLowest;
            PartyLowest = partyLowest;
            PartyGuarded = partyGuarded;
        }

        /// <summary>The battle turn, 1 for the first.</summary>
        public int Turn { get; }

        /// <summary>The part of its own HP left, 0 to 1.</summary>
        public float Hp { get; }

        /// <summary>Another enemy stands, and the smallest part of HP left among them.</summary>
        public bool Others { get; }
        public float OthersLowest { get; }

        /// <summary>The smallest part of HP left among the allies standing, and whether any holds block.</summary>
        public float PartyLowest { get; }
        public bool PartyGuarded { get; }
    }

    /// <summary>One action of an enemy, as its brain chose it.</summary>
    public sealed class EnemyStep
    {
        /// <summary>The skill used, or being gathered when <see cref="Charging"/>.</summary>
        public EnemyMove Move;

        /// <summary>This action only gathers strength for <see cref="Move"/> (and does its ChargeWith).</summary>
        public bool Charging;

        /// <summary>While gathering: the actions before it lands, 1 for the next.</summary>
        public int ChargeLeft;

        /// <summary>The second part of the battle starts with this action.</summary>
        public bool Awakens;

        /// <summary>Its enrage starts with this action.</summary>
        public bool Enrages;

        /// <summary>The power in percent of the skill's (more while enraged), and the extra hits.</summary>
        public int Percent = 100;
        public int ExtraHits;
    }

    /// <summary>
    /// One enemy's choice of skills over a battle (doc/catalog/enemies, 行動パターン), as its
    /// behaviour lays down: the skill being gathered lands first, then the switch to the second
    /// part, a skill a rule asked for next, the opening, rules cutting in, and the usual choice.
    /// Skills the battle cannot do are passed over. <paramref name="random"/> gives a number from 0
    /// up to (not including) the one it is given.
    /// </summary>
    public sealed class EnemyBrain
    {
        /// <summary>
        /// The part of its weight a skill that lands no blow (guarding, gathering strength, a
        /// status) keeps in the usual choice, so enemies mostly attack (2026-10-24, provisional).
        /// </summary>
        public const int QuietWeightPercent = 40;

        private readonly Func<int, int> random;
        private readonly List<string> history = new();
        private readonly Dictionary<string, int> lastUsed = new();
        private readonly Dictionary<EnemyRule, int> uses = new();
        private EnemyPattern pattern;
        private int actions;
        private int order;
        private int cycle;
        private string then;
        private int chargeLeft;

        public EnemyBrain(EnemyBehavior behavior, Func<int, int> random)
        {
            Behavior = behavior;
            this.random = random;
            pattern = behavior.Early ?? new EnemyPattern();
        }

        public EnemyBehavior Behavior { get; }

        /// <summary>The part of the battle's pattern in use.</summary>
        public EnemyPattern Pattern => pattern;

        /// <summary>True once the second part has started.</summary>
        public bool Late { get; private set; }

        public bool Enraged { get; private set; }

        /// <summary>The skill being gathered, or null.</summary>
        public EnemyMove Charging { get; private set; }

        /// <summary>The skills used so far, oldest first (gathering left out).</summary>
        public IReadOnlyList<string> History => history;

        public EnemyStep Next(EnemySituation situation)
        {
            var step = new EnemyStep();
            if (!Enraged && Behavior.EnrageTurn > 0 && situation.Turn >= Behavior.EnrageTurn)
            {
                Enraged = true;
                step.Enrages = true;
            }

            if (Charging != null)
            {
                if (--chargeLeft > 0)
                {
                    step.Move = Charging;
                    step.Charging = true;
                    step.ChargeLeft = chargeLeft;
                    return step;
                }
                var landing = Charging;
                Charging = null;
                return Land(step, landing, situation);
            }

            if (
                Behavior.Late != null
                && !Late
                && (
                    (Behavior.LateTurn > 0 && situation.Turn >= Behavior.LateTurn)
                    || situation.Hp <= Behavior.LateBelow
                )
            )
            {
                Late = true;
                pattern = Behavior.Late;
                actions = order = cycle = 0;
                then = null;
                step.Awakens = true;
                if (Behavior.Usable(Behavior.LateMove))
                    return Use(step, Behavior.LateMove, situation);
            }

            if (then != null)
            {
                string next = then;
                then = null;
                if (Behavior.Usable(next))
                    return Use(step, next, situation);
            }

            if (actions == 0 && pattern.Opening != null)
            {
                string first =
                    !situation.Others && pattern.OpeningAlone != null
                        ? pattern.OpeningAlone
                        : pattern.Opening;
                if (Behavior.Usable(first))
                {
                    // An order that starts with the opening goes on from its second skill.
                    if (pattern.Order.Length > 0 && pattern.Order[0] == first)
                        order = 1;
                    return Use(step, first, situation);
                }
            }

            // In a cycle, rules cut in only where the cycle leaves the choice open.
            bool open =
                pattern.Cycle.Length == 0
                || pattern.Cycle[cycle % pattern.Cycle.Length] == EnemyPattern.Pick;
            if (open)
                foreach (var rule in pattern.Rules)
                    if (Ready(rule, situation))
                    {
                        uses[rule] = UsesOf(rule) + 1;
                        then = rule.Then;
                        return Use(step, rule.Move, situation);
                    }

            return Use(step, Usual() ?? Fallback(), situation);
        }

        // --- Choice ----------------------------------------------------------------------------

        private string Usual()
        {
            if (pattern.Cycle.Length > 0)
            {
                string entry = pattern.Cycle[cycle % pattern.Cycle.Length];
                cycle++;
                if (entry != EnemyPattern.Pick && Behavior.Usable(entry))
                    return entry;
                return Choose();
            }
            if (pattern.Order.Length > 0)
            {
                for (int k = 0; k < pattern.Order.Length; k++)
                {
                    string entry = pattern.Order[(order + k) % pattern.Order.Length];
                    if (!Behavior.Usable(entry))
                        continue;
                    order = (order + k + 1) % pattern.Order.Length;
                    return entry;
                }
                return null;
            }
            return Choose();
        }

        /// <summary>A weighted pick among the choices it may use now, the runs kept when it can.</summary>
        private string Choose()
        {
            var open = new List<EnemyChoice>();
            foreach (var choice in pattern.Choices)
                if (
                    choice.Weight > 0
                    && Behavior.Usable(choice.Move)
                    && Run(choice.Move) < pattern.RunOf(choice.Move)
                )
                    open.Add(choice);
            if (open.Count == 0)
                foreach (var choice in pattern.Choices)
                    if (choice.Weight > 0 && Behavior.Usable(choice.Move))
                        open.Add(choice);
            if (open.Count == 0)
                return null;
            int total = 0;
            foreach (var choice in open)
                total += WeightOf(choice);
            int roll = random(total);
            foreach (var choice in open)
            {
                int weight = WeightOf(choice);
                if (roll < weight)
                    return choice.Move;
                roll -= weight;
            }
            return open[^1].Move;
        }

        /// <summary>The weight of a choice, less for a skill that lands no blow.</summary>
        public int WeightOf(EnemyChoice choice) =>
            Behavior.Move(choice.Move) is { Strikes: true }
                ? choice.Weight
                : Math.Max(1, choice.Weight * QuietWeightPercent / 100);

        /// <summary>The first blow it has, when nothing else is left to choose.</summary>
        private string Fallback()
        {
            foreach (var move in Behavior.Moves)
                if (move.Supported && move.Strikes)
                    return move.Name;
            foreach (var move in Behavior.Moves)
                if (move.Supported)
                    return move.Name;
            return null;
        }

        private bool Ready(EnemyRule rule, EnemySituation situation)
        {
            if (!Behavior.Usable(rule.Move))
                return false;
            if (rule.Limit > 0 && UsesOf(rule) >= rule.Limit)
                return false;
            if (situation.Turn < rule.From)
                return false;
            // Its HP running low calls for the skill once at a time, not on actions in a row.
            if (rule.Cue == EnemyCue.Hurt && history.Count > 0 && history[^1] == rule.Move)
                return false;
            if (
                rule.Every > 0
                && lastUsed.TryGetValue(rule.Move, out int last)
                && situation.Turn - last < rule.Every
            )
                return false;
            return rule.Cue switch
            {
                EnemyCue.Always => true,
                EnemyCue.Hurt => situation.Hp <= rule.Below,
                EnemyCue.OtherHurt => situation.Others && situation.OthersLowest <= rule.Below,
                EnemyCue.Alone => !situation.Others,
                EnemyCue.Together => situation.Others,
                EnemyCue.PartyHurt => situation.PartyLowest <= rule.Below,
                EnemyCue.PartyGuarded => situation.PartyGuarded,
                _ => false,
            };
        }

        private int UsesOf(EnemyRule rule) => uses.TryGetValue(rule, out int used) ? used : 0;

        /// <summary>How many times in a row <paramref name="move"/> was used last.</summary>
        private int Run(string move)
        {
            int run = 0;
            for (int i = history.Count - 1; i >= 0 && history[i] == move; i--)
                run++;
            return run;
        }

        // --- Steps -----------------------------------------------------------------------------

        /// <summary>Uses the skill, or starts gathering for it when it is gathered first.</summary>
        private EnemyStep Use(EnemyStep step, string name, EnemySituation situation)
        {
            var move = name != null ? Behavior.Move(name) : null;
            actions++;
            if (move == null)
                return step;
            if (move.ChargeTurns > 0)
            {
                Charging = move;
                chargeLeft = move.ChargeTurns;
                step.Move = move;
                step.Charging = true;
                step.ChargeLeft = chargeLeft;
                return step;
            }
            return Land(step, move, situation);
        }

        private EnemyStep Land(EnemyStep step, EnemyMove move, EnemySituation situation)
        {
            history.Add(move.Name);
            lastUsed[move.Name] = situation.Turn;
            step.Move = move;
            if (Enraged && Behavior.EnrageOf(move.Name) is { } change)
            {
                step.Percent = change.Percent;
                step.ExtraHits = change.ExtraHits;
            }
            return step;
        }
    }
}
