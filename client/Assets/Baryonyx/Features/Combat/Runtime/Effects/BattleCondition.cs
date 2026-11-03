using System;

namespace Baryonyx.Combat
{
    /// <summary>
    /// Whether a rule of an effect works for a <see cref="BattleEvent"/>. Conditions join with
    /// <c>&amp;</c>, <c>|</c> and <c>!</c>; <see cref="Of"/> takes any test the parts here cannot
    /// say. "Owner" is the effect's holder, "actor" who acts and "target" whom it lands on.
    /// </summary>
    public sealed class BattleCondition
    {
        private readonly Func<BattleEvent, bool> test;

        private BattleCondition(Func<BattleEvent, bool> test) => this.test = test;

        public static readonly BattleCondition Always = new(_ => true);

        public bool Holds(BattleEvent moment) => test(moment);

        /// <summary>A condition of its own, for what the parts cannot say.</summary>
        public static BattleCondition Of(Func<BattleEvent, bool> test) =>
            new(test ?? throw new ArgumentNullException(nameof(test)));

        public static BattleCondition operator &(BattleCondition a, BattleCondition b) =>
            new(moment => a.Holds(moment) && b.Holds(moment));

        public static BattleCondition operator |(BattleCondition a, BattleCondition b) =>
            new(moment => a.Holds(moment) || b.Holds(moment));

        public static BattleCondition operator !(BattleCondition a) =>
            new(moment => !a.Holds(moment));

        // --- Who ------------------------------------------------------------------------------

        public static readonly BattleCondition ActorIsOwner = new(m => m.Actor == m.Owner);
        public static readonly BattleCondition ActorIsAlly = new(m =>
            m.Actor.IsAlly && !m.Actor.IsNone
        );

        /// <summary>An ally other than the owner acts.</summary>
        public static readonly BattleCondition ActorIsOtherAlly = new(m =>
            m.Actor.IsAlly && !m.Actor.IsNone && m.Actor != m.Owner
        );

        public static readonly BattleCondition ActorIsEnemy = new(m => m.Actor.IsEnemy);
        public static readonly BattleCondition ActorStands = new(m => m.World.Stands(m.Actor));

        public static readonly BattleCondition TargetIsOwner = new(m => m.Target == m.Owner);
        public static readonly BattleCondition TargetIsAlly = new(m =>
            m.Target.IsAlly && !m.Target.IsNone
        );
        public static readonly BattleCondition TargetIsEnemy = new(m => m.Target.IsEnemy);
        public static readonly BattleCondition TargetStands = new(m => m.World.Stands(m.Target));

        // --- HP and statuses --------------------------------------------------------------

        /// <summary>The target stands with at most <paramref name="percent"/>% of its max HP.</summary>
        public static BattleCondition TargetHpAtMost(int percent) =>
            new(m => HpAtMost(m, m.Target, percent));

        /// <summary>The owner stands with at most <paramref name="percent"/>% of its max HP.</summary>
        public static BattleCondition OwnerHpAtMost(int percent) =>
            new(m => HpAtMost(m, m.Owner, percent));

        /// <summary>The actor stands with at most <paramref name="percent"/>% of its max HP.</summary>
        public static BattleCondition ActorHpAtMost(int percent) =>
            new(m => HpAtMost(m, m.Actor, percent));

        public static BattleCondition TargetHas(CardStatus status) =>
            new(m => !m.Target.IsNone && m.World.HasStatus(m.Target, status));

        public static BattleCondition OwnerHas(CardStatus status) =>
            new(m => m.World.HasStatus(m.Owner, status));

        public static BattleCondition ActorHas(CardStatus status) =>
            new(m => !m.Actor.IsNone && m.World.HasStatus(m.Actor, status));

        /// <summary>The owner has a block.</summary>
        public static readonly BattleCondition OwnerGuarded = new(m => m.World.Block(m.Owner) > 0);

        /// <summary>The actor has a block (a card's 使い手にブロックがあるとき).</summary>
        public static readonly BattleCondition ActorGuarded = new(m =>
            !m.Actor.IsNone && m.World.Block(m.Actor) > 0
        );

        /// <summary>The blow hit a weakness.</summary>
        public static readonly BattleCondition HitWeakness = new(m => m.Weak);

        /// <summary>A <see cref="BattleValue.StatPercent"/> is asked for one of <paramref name="stats"/> (<see cref="BattleStatBoosts"/> order).</summary>
        public static BattleCondition StatIs(params int[] stats) =>
            new(m => Array.IndexOf(stats, m.StatIndex) >= 0);

        /// <summary>At least <paramref name="least"/> allies of the party have fallen.</summary>
        public static BattleCondition FallenAtLeast(int least) =>
            new(m => BattleAmount.FallenAllies(m.World) >= least);

        /// <summary>The status being put on is a bad one (<see cref="CardRules.IsBad"/>).</summary>
        public static readonly BattleCondition StatusIsBad = new(m => CardRules.IsBad(m.Status));

        public static BattleCondition StatusIs(params CardStatus[] statuses) =>
            new(m => Array.IndexOf(statuses, m.Status) >= 0);

        // --- Cards and blows --------------------------------------------------------------

        /// <summary>The element of the blow or the card is one of <paramref name="elements"/>.</summary>
        public static BattleCondition ElementIs(params CardElement[] elements) =>
            new(m => Array.IndexOf(elements, m.Element) >= 0);

        /// <summary>The card being played is of one of <paramref name="elements"/>.</summary>
        public static BattleCondition CardElementIs(params CardElement[] elements) =>
            new(m => m.Card != null && Array.IndexOf(elements, m.Card.Element) >= 0);

        public static BattleCondition CardKindIs(params CardKind[] kinds) =>
            new(m => m.Card != null && Array.IndexOf(kinds, m.Card.Kind) >= 0);

        /// <summary>The card being played costs at least <paramref name="least"/> (as written).</summary>
        public static BattleCondition CardCostAtLeast(int least) =>
            new(m => m.Card != null && m.Card.Cost >= least);

        /// <summary>The card being played aims at one enemy (単体攻撃).</summary>
        public static readonly BattleCondition CardOnOneEnemy = new(m =>
            m.Card != null && m.Card.Target == CardTarget.OneEnemy
        );

        /// <summary>The blow is a physical one (斬・打・貫).</summary>
        public static readonly BattleCondition Physical = new(m =>
            m.Element is CardElement.Slash or CardElement.Blunt or CardElement.Pierce
        );

        /// <summary>The card being played deals damage.</summary>
        public static readonly BattleCondition CardDeals = new(m => Deals(m.Card));

        /// <summary>The card's action being worked out lands more than once ("×回数").</summary>
        public static readonly BattleCondition HitsMoreThanOnce = new(m =>
            m.Action != null && m.Action.Kind == CardActionKind.Damage && m.Action.Hits > 1
        );

        public static BattleCondition KindIs(params BattleDamageKind[] kinds) =>
            new(m => Array.IndexOf(kinds, m.Kind) >= 0);

        /// <summary>The amount of what happened is more than <paramref name="least"/>.</summary>
        public static BattleCondition AmountAbove(int least) => new(m => m.Amount > least);

        /// <summary>Some healing went past the max HP.</summary>
        public static readonly BattleCondition Overflowed = new(m => m.Overflow > 0);

        // --- The battle and the effect ----------------------------------------------------

        public static BattleCondition TurnAtMost(int turn) => new(m => m.World.Turn <= turn);

        /// <summary>The owner's effect has not reached its most stacks.</summary>
        public static readonly BattleCondition StacksBelowMax = new(m => !m.Effect.Full);

        /// <summary>The owner's effect has at least <paramref name="least"/> stacks.</summary>
        public static BattleCondition StacksAtLeast(int least) =>
            new(m => m.Effect.Stacks >= least);

        /// <summary>The owner's effect <paramref name="id"/> (another of its effects) is at its most stacks.</summary>
        public static BattleCondition OwnerEffectFull(string id) =>
            new(m => m.Effects.Find(m.Owner, id) is { Full: true });

        private static bool HpAtMost(BattleEvent m, BattleUnit unit, int percent) =>
            !unit.IsNone && BattleMath.HpAtMost(m.World.Hp(unit), m.World.MaxHp(unit), percent);

        private static bool Deals(CardSkill card)
        {
            if (card == null)
                return false;
            foreach (var action in card.Actions)
                if (action.Kind == CardActionKind.Damage)
                    return true;
            return false;
        }
    }
}
