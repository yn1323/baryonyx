using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>Whether an act did something, and the words to show with the effect's name.</summary>
    public readonly struct BattleActResult
    {
        private BattleActResult(bool done, string detail)
        {
            Done = done;
            Detail = detail;
        }

        public static readonly BattleActResult Nothing = new(false, null);

        public bool Done { get; }

        /// <summary>The words after the effect's name ("+1", "ブロック +12"), or null for the name alone.</summary>
        public string Detail { get; }

        public static BattleActResult Did(string detail = null) => new(true, detail);
    }

    /// <summary>
    /// Whom an act of an effect reaches, among those standing: the owner, the actor or the target
    /// of what happened, or the enemies or allies. <see cref="Of"/> takes any other choice.
    /// </summary>
    public sealed class BattleWho
    {
        private readonly Func<BattleEvent, IReadOnlyList<BattleUnit>> pick;

        private BattleWho(Func<BattleEvent, IReadOnlyList<BattleUnit>> pick) => this.pick = pick;

        public IReadOnlyList<BattleUnit> In(BattleEvent moment) => pick(moment);

        public static BattleWho Of(Func<BattleEvent, IReadOnlyList<BattleUnit>> pick) =>
            new(pick ?? throw new ArgumentNullException(nameof(pick)));

        public static readonly BattleWho Owner = new(m => new[] { m.Owner });
        public static readonly BattleWho Actor = new(m => new[] { m.Actor });
        public static readonly BattleWho Target = new(m => new[] { m.Target });

        /// <summary>One enemy still standing, picked at random (a rule with it is left out of forecasts).</summary>
        public static readonly BattleWho RandomEnemy = new(m => OneOf(m, Standing(m, false)));

        public static readonly BattleWho RandomAlly = new(m => OneOf(m, Standing(m, true)));
        public static readonly BattleWho AllEnemies = new(m => Standing(m, false));
        public static readonly BattleWho AllAllies = new(m => Standing(m, true));

        /// <summary>The owner's side standing: the allies for an ally, the enemies for an enemy.</summary>
        public static readonly BattleWho OwnSide = new(m => Standing(m, m.Owner.IsAlly));

        private static List<BattleUnit> Standing(BattleEvent m, bool allies)
        {
            var found = new List<BattleUnit>();
            int count = m.World.Count(allies);
            for (int i = 0; i < count; i++)
            {
                var unit = allies ? BattleUnit.Ally(i) : BattleUnit.Enemy(i);
                if (m.World.Stands(unit))
                    found.Add(unit);
            }
            return found;
        }

        private static IReadOnlyList<BattleUnit> OneOf(BattleEvent m, List<BattleUnit> units) =>
            units.Count == 0 ? units : new[] { units[m.World.Pick(units.Count)] };
    }

    /// <summary>
    /// What a rule of an effect does when it works. The acts here change the battle through
    /// <see cref="IBattleWorld"/>, so the same act works in the battle and in its forecast;
    /// <see cref="Custom"/> takes any act the parts cannot say.
    /// </summary>
    public sealed class BattleAct
    {
        private readonly Func<BattleEvent, BattleActResult> run;

        private BattleAct(Func<BattleEvent, BattleActResult> run) => this.run = run;

        public BattleActResult Run(BattleEvent moment) => run(moment);

        /// <summary>An act of its own, for what the parts cannot say.</summary>
        public static BattleAct Custom(Func<BattleEvent, BattleActResult> run) =>
            new(run ?? throw new ArgumentNullException(nameof(run)));

        /// <summary>Does nothing but show the effect's name.</summary>
        public static readonly BattleAct Show = new(_ => BattleActResult.Did());

        // --- Blows ---------------------------------------------------------------------------

        /// <summary>
        /// A blow of the owner on each of <paramref name="to"/>: <paramref name="percent"/>% of the
        /// owner's <paramref name="stat"/>, grown by <see cref="BattleValue.Damage"/> as an
        /// effect's blow, softened by the target's defense against that stat.
        /// </summary>
        public static BattleAct Strike(
            BattleWho to,
            CardStat stat,
            int percent,
            CardElement element,
            BattleStrikeLook look = BattleStrikeLook.Plain
        ) =>
            new(m =>
            {
                bool any = false;
                foreach (var target in to.In(m))
                {
                    if (!m.World.Stands(target))
                        continue;
                    var blow = new BattleEvent
                    {
                        Actor = m.Owner,
                        Target = target,
                        Element = element,
                        Kind = BattleDamageKind.Effect,
                        Play = m.Play,
                    };
                    int power = BattleMath.Percent(m.World.Stat(m.Owner, stat), percent);
                    power = m.Effects.Modify(m.World, BattleValue.Damage, power, blow);
                    power = CombatFormula.Defend(
                        power,
                        m.World.Defense(target, stat),
                        m.World.Level(m.Owner)
                    );
                    m.World.Strike(blow, power, look);
                    any = true;
                }
                return any ? BattleActResult.Did() : BattleActResult.Nothing;
            });

        /// <summary>
        /// <paramref name="status"/> on the target does its damage once more (傷口を開く), as a
        /// lasting blow of the owner.
        /// </summary>
        public static BattleAct StrikeWith(CardStatus status) =>
            new(m =>
            {
                int power = m.World.StatusPower(m.Target, status);
                if (power <= 0 || !m.World.Stands(m.Target))
                    return BattleActResult.Nothing;
                m.World.Strike(
                    new BattleEvent
                    {
                        Actor = m.Owner,
                        Target = m.Target,
                        Kind = BattleDamageKind.Status,
                        Status = status,
                        Play = m.Play,
                    },
                    power,
                    BattleStrikeLook.Plain
                );
                return BattleActResult.Did();
            });

        // --- Block, healing and statuses ------------------------------------------------------

        /// <summary>Each of <paramref name="who"/> gains <paramref name="amount"/> block; shows how much.</summary>
        public static BattleAct GainBlock(BattleWho who, BattleAmount amount) =>
            new(m =>
            {
                int total = 0;
                int each = amount.For(m);
                if (each <= 0)
                    return BattleActResult.Nothing;
                foreach (var unit in who.In(m))
                    if (m.World.Stands(unit))
                        total += m.World.AddBlock(unit, each);
                return total > 0
                    ? BattleActResult.Did($"ブロック +{total}")
                    : BattleActResult.Nothing;
            });

        /// <summary>The owner heals each of <paramref name="who"/> by <paramref name="amount"/>.</summary>
        public static BattleAct Heal(BattleWho who, BattleAmount amount) =>
            new(m =>
            {
                int each = amount.For(m);
                if (each <= 0)
                    return BattleActResult.Nothing;
                bool any = false;
                foreach (var unit in who.In(m))
                    if (m.World.Stands(unit))
                    {
                        m.World.Heal(m.Owner, unit, each);
                        any = true;
                    }
                return any ? BattleActResult.Did() : BattleActResult.Nothing;
            });

        /// <summary>
        /// The owner puts <paramref name="status"/> for <paramref name="turns"/> (and
        /// <paramref name="amount"/>: poison's stacks, a buff's percent) on each of
        /// <paramref name="to"/>; a bad one lands at <paramref name="chance"/>% moved by luck
        /// (0: always). Shows the effect's name only when it landed.
        /// </summary>
        public static BattleAct ApplyStatus(
            BattleWho to,
            CardStatus status,
            int turns,
            int chance = 0,
            int amount = 0
        ) =>
            new(m =>
            {
                bool any = false;
                foreach (var unit in to.In(m))
                    if (m.World.Stands(unit))
                        any |= m.World.ApplyStatus(m.Owner, unit, status, turns, chance, amount);
                return any ? BattleActResult.Did() : BattleActResult.Nothing;
            });

        // --- Energy and cards -----------------------------------------------------------------

        /// <summary>The party gains <paramref name="amount"/> energy, up to the battle's cap.</summary>
        public static BattleAct GainEnergy(BattleAmount amount) =>
            new(m =>
            {
                int energy = amount.For(m);
                if (energy <= 0)
                    return BattleActResult.Nothing;
                m.World.GainEnergy(energy);
                return BattleActResult.Did($"エネルギー +{energy}");
            });

        /// <summary>Up to <paramref name="most"/> of the energy left is carried into the next turn.</summary>
        public static BattleAct CarryEnergy(int most) =>
            new(m =>
            {
                int carried = m.World.CarryEnergy(most);
                return carried > 0 ? BattleActResult.Did($"+{carried}") : BattleActResult.Nothing;
            });

        public static BattleAct Draw(int count) =>
            new(m =>
                m.World.Draw(count) > 0
                    ? BattleActResult.Did($"{count}枚引く")
                    : BattleActResult.Nothing
            );

        // --- The happening itself -------------------------------------------------------------

        /// <summary>The card being played deals <paramref name="percent"/>% more damage (居合の型).</summary>
        public static BattleAct BoostCard(int percent) =>
            new(m =>
            {
                if (m.Play == null)
                    return BattleActResult.Nothing;
                m.Play.BonusPercent += percent;
                return BattleActResult.Did();
            });

        /// <summary>
        /// The effect gains <paramref name="count"/> stacks, up to its most; <paramref name="detail"/>
        /// tells the stacks it has then. A forecast leaves the stacks as they are.
        /// </summary>
        public static BattleAct AddStacks(int count, Func<int, string> detail = null) =>
            new(m =>
            {
                var effect = m.Effect;
                if (effect.Full)
                    return BattleActResult.Nothing;
                if (!m.Preview)
                    effect.Stacks =
                        effect.Effect.MaxStacks > 0
                            ? Math.Min(effect.Effect.MaxStacks, effect.Stacks + count)
                            : effect.Stacks + count;
                return BattleActResult.Did(detail?.Invoke(effect.Stacks));
            });

        /// <summary>
        /// Each of <paramref name="who"/> (the owner unless given) holds <paramref name="effect"/>
        /// for a while (<paramref name="span"/>, <paramref name="turns"/>): すり抜け・霧隠れ. A
        /// forecast holds nothing.
        /// </summary>
        public static BattleAct Hold(
            BattleEffect effect,
            BattleHoldSpan span,
            int turns = 0,
            BattleWho who = null
        ) =>
            new(m =>
            {
                if (m.Preview)
                    return BattleActResult.Nothing;
                foreach (var unit in (who ?? BattleWho.Owner).In(m))
                    if (m.World.Stands(unit))
                        m.Effects.Hold(unit, effect, span, turns);
                return BattleActResult.Did();
            });

        /// <summary>Stops what was about to happen (a status being put on).</summary>
        public static readonly BattleAct Cancel = new(m =>
        {
            m.Cancelled = true;
            return BattleActResult.Did();
        });

        /// <summary>The owner takes the blow instead of its target.</summary>
        public static readonly BattleAct TakeInstead = new(m =>
        {
            m.Redirect = m.Owner;
            return BattleActResult.Did();
        });
    }
}
