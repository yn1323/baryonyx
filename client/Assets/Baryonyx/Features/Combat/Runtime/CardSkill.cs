using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// A card's element (doc/features/combat.md): magic (fire, ice, thunder), physical (slash,
    /// blunt, pierce), or none. Same order as the battle mock's elements, the new two at the end.
    /// </summary>
    public enum CardElement
    {
        None,
        Slash,
        Fire,
        Ice,
        Thunder,
        Blunt,
        Pierce,
    }

    /// <summary>What a card is for, shown first on its kind line.</summary>
    public enum CardKind
    {
        Attack,
        Heal,
        Guard,
        Buff,
        Debuff,
        Support,
    }

    /// <summary>Who an action of a card reaches.</summary>
    public enum CardTarget
    {
        /// <summary>The enemy the card is played on.</summary>
        OneEnemy,
        AllEnemies,

        /// <summary>An enemy picked at random for each hit (the same one may be hit again).</summary>
        RandomEnemies,

        /// <summary>
        /// The enemy the card is played on, then a jump to another at random for each next hit,
        /// never the one just hit while another is left.
        /// </summary>
        ChainEnemies,

        /// <summary>The ally the card is played on.</summary>
        OneAlly,
        AllAllies,

        /// <summary>The card's user.</summary>
        Self,

        /// <summary>No character: the hand, the deck, the energy or the next card.</summary>
        None,
    }

    /// <summary>The stat a power is worked out from (provisional: the stats are not decided).</summary>
    public enum CardStat
    {
        None,

        /// <summary>ちから: physical blows.</summary>
        Strength,

        /// <summary>まりょく: spells and healing.</summary>
        Magic,

        /// <summary>まもり: block, and blows with the shield.</summary>
        Defense,
    }

    /// <summary>The battle mock's party member who uses a card (provisional, not a decided character).</summary>
    public enum CardUser
    {
        Aria,
        Toma,
        Luka,
        Mina,
    }

    /// <summary>
    /// A lasting state a card puts on a character or the battle (provisional rules:
    /// doc/features/combat.md, card statuses).
    /// </summary>
    public enum CardStatus
    {
        None,

        // On enemies.
        Bleed,
        Burn,
        Poison,
        Chill,
        Freeze,
        Paralysis,
        Vulnerable,

        /// <summary>A sigil that blows up at the start of the party's next turn, on every enemy.</summary>
        BlastSigil,

        // On the party.
        /// <summary>A thundercloud that strikes an enemy at random at the start of each party turn.</summary>
        Thundercloud,
        AttackUp,
        SpeedUp,
        Regen,
        Taunt,
        Reflect,

        /// <summary>The ally's blows take fire as an element too, for weaknesses.</summary>
        FireBlade,
        Protect,
        Invincible,
    }

    public enum CardActionKind
    {
        Damage,
        Heal,
        Block,
        Status,
        Energy,
        Draw,

        /// <summary>Lowers the cost of the cards in hand of <see cref="CardAction.Elements"/>.</summary>
        CostDown,

        /// <summary>The next card played costs nothing.</summary>
        NextCardFree,

        /// <summary>Reveals every weakness of the target.</summary>
        RevealWeakness,

        /// <summary>Moves the target's next action back in the turn order.</summary>
        Delay,

        /// <summary>Clears the bad statuses of the target.</summary>
        Cleanse,

        /// <summary>Clears one good status of the target.</summary>
        Dispel,

        /// <summary>Heals part of the maximum HP, and brings a fallen ally back.</summary>
        Revive,
    }

    /// <summary>When an action happens, or when its power grows.</summary>
    public enum CardCondition
    {
        Always,

        /// <summary>The blow hit a weakness.</summary>
        OnWeakness,

        /// <summary>The target is burning.</summary>
        OnBurning,

        /// <summary>The target has half of its HP or less left.</summary>
        OnWounded,
    }

    /// <summary>One thing a card does, in order.</summary>
    public sealed class CardAction
    {
        public CardActionKind Kind;
        public CardTarget Target;

        /// <summary>The stat the power is a part of, and the part in percent (damage, healing, block).</summary>
        public CardStat Stat;
        public int Percent;

        /// <summary>How many times the damage lands (for random and chained targets, a target each).</summary>
        public int Hits = 1;

        public CardStatus Status;
        public int Turns;

        /// <summary>Energy, cards drawn, cost lowered, poison stacks, or percent of the maximum HP revived.</summary>
        public int Amount;

        /// <summary>The cards whose cost <see cref="CardActionKind.CostDown"/> lowers.</summary>
        public CardElement[] Elements = Array.Empty<CardElement>();

        /// <summary>The action happens only when this holds.</summary>
        public CardCondition When;

        /// <summary>The power grows by <see cref="BonusPercent"/> when this holds.</summary>
        public CardCondition BonusWhen;
        public int BonusPercent;

        /// <summary>The power grows by this percent for every 1,000 UPT walked today, up to <see cref="UptCapPercent"/>.</summary>
        public int UptStepPercent;
        public int UptCapPercent;

        /// <summary>True when the action has a power worked out from a stat (shown as a number).</summary>
        public bool HasPower => Stat != CardStat.None && Percent > 0;

        public static CardAction Damage(
            CardTarget target,
            CardStat stat,
            int percent,
            int hits = 1
        ) =>
            new()
            {
                Kind = CardActionKind.Damage,
                Target = target,
                Stat = stat,
                Percent = percent,
                Hits = hits,
            };

        public static CardAction Heal(CardTarget target, CardStat stat, int percent) =>
            new()
            {
                Kind = CardActionKind.Heal,
                Target = target,
                Stat = stat,
                Percent = percent,
            };

        public static CardAction Block(CardTarget target, CardStat stat, int percent) =>
            new()
            {
                Kind = CardActionKind.Block,
                Target = target,
                Stat = stat,
                Percent = percent,
            };

        public static CardAction Apply(
            CardTarget target,
            CardStatus status,
            int turns,
            int amount = 0
        ) =>
            new()
            {
                Kind = CardActionKind.Status,
                Target = target,
                Status = status,
                Turns = turns,
                Amount = amount,
            };

        /// <summary>A status whose effect has a power of its own (a sigil's blast, a cloud's bolt, regen).</summary>
        public static CardAction Apply(
            CardTarget target,
            CardStatus status,
            int turns,
            CardStat stat,
            int percent
        ) =>
            new()
            {
                Kind = CardActionKind.Status,
                Target = target,
                Status = status,
                Turns = turns,
                Stat = stat,
                Percent = percent,
            };

        public static CardAction Simple(
            CardActionKind kind,
            CardTarget target = CardTarget.None,
            int amount = 0
        ) =>
            new()
            {
                Kind = kind,
                Target = target,
                Amount = amount,
            };

        public static CardAction CostDown(int amount, params CardElement[] elements) =>
            new()
            {
                Kind = CardActionKind.CostDown,
                Target = CardTarget.None,
                Amount = amount,
                Elements = elements,
            };

        /// <summary>The same action, growing by <paramref name="percent"/> when <paramref name="when"/> holds.</summary>
        public CardAction Bonus(CardCondition when, int percent)
        {
            BonusWhen = when;
            BonusPercent = percent;
            return this;
        }

        /// <summary>The same action, done only when <paramref name="when"/> holds.</summary>
        public CardAction Only(CardCondition when)
        {
            When = when;
            return this;
        }

        /// <summary>The same action, growing with today's UPT.</summary>
        public CardAction WithUpt(int stepPercent, int capPercent)
        {
            UptStepPercent = stepPercent;
            UptCapPercent = capPercent;
            return this;
        }
    }

    /// <summary>
    /// One card skill: its name, the mock's user, element, kind, cost and what it does. The
    /// description is <see cref="Text"/> with the powers of its actions filled in, in order
    /// ({0}, {1}).
    /// </summary>
    public sealed class CardSkill
    {
        /// <summary>A stable id in PascalCase; the art is "Card" + Id and the effect is played by it.</summary>
        public string Id;
        public string Name;
        public CardUser User;
        public CardElement Element;
        public CardKind Kind;

        /// <summary>Who the player picks, or the side it reaches: decides how the card is played.</summary>
        public CardTarget Target;
        public int Cost;
        public string Text;
        public IReadOnlyList<CardAction> Actions = Array.Empty<CardAction>();

        /// <summary>The art's name in the card art folder.</summary>
        public string Art => "Card" + Id;

        /// <summary>The actions that show a number in the description, in order.</summary>
        public IEnumerable<CardAction> Powered()
        {
            foreach (var action in Actions)
                if (action.HasPower)
                    yield return action;
        }
    }
}
