using System;

namespace Baryonyx.Combat
{
    /// <summary>
    /// Where a number of an effect comes from: a fixed amount, a part of the owner's stat, a part
    /// of what happened, the effect's stacks or today's ACT. A whole number stands for itself;
    /// <see cref="Of"/> takes any other.
    /// </summary>
    public sealed class BattleAmount
    {
        private readonly Func<BattleEvent, int> of;

        private BattleAmount(Func<BattleEvent, int> of) => this.of = of;

        public int For(BattleEvent moment) => of(moment);

        public static implicit operator BattleAmount(int amount) => Fixed(amount);

        public static BattleAmount Fixed(int amount) => new(_ => amount);

        /// <summary>An amount of its own, for what the parts cannot say.</summary>
        public static BattleAmount Of(Func<BattleEvent, int> of) =>
            new(of ?? throw new ArgumentNullException(nameof(of)));

        /// <summary><paramref name="percent"/>% of the owner's <paramref name="stat"/>.</summary>
        public static BattleAmount OwnerStat(CardStat stat, int percent) =>
            new(m => BattleMath.Percent(m.World.Stat(m.Owner, stat), percent));

        /// <summary>The amount of what happened (the damage, the healing, the block, the energy left).</summary>
        public static readonly BattleAmount Happened = new(m => m.Amount);

        /// <summary>
        /// <paramref name="percent"/>% of the amount of what happened, at least
        /// <paramref name="least"/> when there was any.
        /// </summary>
        public static BattleAmount PartOfHappened(int percent, int least = 0) =>
            new(m => m.Amount <= 0 ? 0 : Math.Max(least, BattleMath.Percent(m.Amount, percent)));

        /// <summary>The healing that went past the max HP.</summary>
        public static readonly BattleAmount Overflow = new(m => m.Overflow);

        /// <summary><paramref name="each"/> for each stack of the effect.</summary>
        public static BattleAmount PerStack(int each) => new(m => m.Effect.Stacks * each);

        /// <summary><paramref name="step"/> for every 1,000 ACT walked today, up to <paramref name="cap"/>.</summary>
        public static BattleAmount ByAct(int step, int cap) =>
            new(m =>
                m.World.TodayAct <= 0
                    ? 0
                    : Math.Min(cap, m.World.TodayAct / CardRules.ActPerStep * step)
            );

        /// <summary>The amount of <paramref name="status"/> on the owner (攻撃力アップの%、毒の重ね).</summary>
        public static BattleAmount OwnerStatusAmount(CardStatus status) =>
            new(m => m.World.StatusAmount(m.Owner, status));

        /// <summary><paramref name="each"/> for each ally of the party who has fallen (an empty place is not one).</summary>
        public static BattleAmount PerFallenAlly(int each) =>
            new(m => FallenAllies(m.World) * each);

        /// <summary>
        /// <paramref name="percent"/>% of the actor's block, rounded half to even
        /// (シールドバッシュ, as it has always been worked out).
        /// </summary>
        public static BattleAmount ActorBlock(int percent) =>
            new(m => (int)Math.Round(m.World.Block(m.Actor) * percent / 100f));

        /// <summary>How many allies of the party have fallen (an empty place is not one).</summary>
        public static int FallenAllies(IBattleWorld world)
        {
            int fallen = 0;
            int count = world.Count(true);
            for (int i = 0; i < count; i++)
            {
                var ally = BattleUnit.Ally(i);
                if (world.InBattle(ally) && !world.Stands(ally))
                    fallen++;
            }
            return fallen;
        }

        /// <summary>The damage or healing <paramref name="status"/> on the target does each time.</summary>
        public static BattleAmount TargetStatusPower(CardStatus status) =>
            new(m => m.World.StatusPower(m.Target, status));
    }
}
