using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// One card being played, from just before its actions to just after them: the card, its
    /// user, the extra damage its play has been given and the damage its blows have dealt.
    /// </summary>
    public sealed class BattleCardPlay
    {
        public BattleCardPlay(CardSkill card, BattleUnit user)
        {
            Card = card;
            User = user;
        }

        public CardSkill Card { get; }
        public BattleUnit User { get; }

        /// <summary>The percent this card's damage grows by (<see cref="BattleAct.BoostCard"/>, 居合の型).</summary>
        public int BonusPercent { get; set; }

        /// <summary>The damage this card's own blows have dealt so far.</summary>
        public int Dealt { get; set; }

        // この1枚のあいだに名前を出した効果（1枚で1回だけ出すもの）。
        internal readonly HashSet<BattleEffectInstance> Shown = new();
    }

    /// <summary>
    /// What happened, as the effects are asked about it: who acted on whom, with which card,
    /// element and amount. The battle fills in what it knows; an effect reads it through its
    /// conditions and amounts, and may change the outcome (<see cref="Cancelled"/>,
    /// <see cref="Redirect"/>). While an effect is asked, <see cref="World"/>,
    /// <see cref="Effects"/> and <see cref="Effect"/> are set.
    /// </summary>
    public sealed class BattleEvent
    {
        /// <summary>Who acts: a card's user, the one who heals or gains block, the attacker.</summary>
        public BattleUnit Actor = BattleUnit.None;

        /// <summary>Who it lands on.</summary>
        public BattleUnit Target = BattleUnit.None;

        /// <summary>The card being played, or null.</summary>
        public CardSkill Card;

        /// <summary>The card's action being worked out (its hits), or null.</summary>
        public CardAction Action;

        /// <summary>The element of the blow or the card.</summary>
        public CardElement Element;

        public BattleDamageKind Kind;

        /// <summary>The status being put on, for the status triggers.</summary>
        public CardStatus Status;

        /// <summary>The damage dealt, the healing, the block gained or the energy left.</summary>
        public int Amount;

        /// <summary>The healing that went past the max HP.</summary>
        public int Overflow;

        /// <summary>The stat a <see cref="BattleValue.StatPercent"/> is asked for (<see cref="BattleStatBoosts"/> order), or -1.</summary>
        public int StatIndex = -1;

        /// <summary>True when the blow hit a weakness (a card's 弱点を突いたとき).</summary>
        public bool Weak;

        /// <summary>For a blow: it goes past protection and block (丸のみ).</summary>
        public bool Pierce;

        /// <summary>The card play this happens in, or null outside one.</summary>
        public BattleCardPlay Play;

        /// <summary>True once an effect stopped what was about to happen.</summary>
        public bool Cancelled;

        /// <summary>The one who takes the blow instead (<see cref="BattleAct.TakeInstead"/>), or none.</summary>
        public BattleUnit Redirect = BattleUnit.None;

        public IBattleWorld World { get; internal set; }
        public BattleEffects Effects { get; internal set; }

        /// <summary>The effect being asked now.</summary>
        public BattleEffectInstance Effect { get; internal set; }

        public BattleUnit Owner => Effect?.Owner ?? BattleUnit.None;

        /// <summary>True while a forecast works the battle out (nothing shows, nothing is used up).</summary>
        public bool Preview => World != null && World.Preview;

        /// <summary>What happens as the card of <paramref name="play"/> is played by its user.</summary>
        public static BattleEvent OfCard(BattleCardPlay play) =>
            new()
            {
                Actor = play.User,
                Card = play.Card,
                Element = play.Card?.Element ?? CardElement.None,
                Kind = BattleDamageKind.Card,
                Play = play,
            };
    }
}
