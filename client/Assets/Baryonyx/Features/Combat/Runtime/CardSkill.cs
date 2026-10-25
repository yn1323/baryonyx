using System;
using System.Collections.Generic;
using Baryonyx.Inventory;

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

    /// <summary>The stat a power is worked out from (doc/features/party.md, the stats).</summary>
    public enum CardStat
    {
        None,

        /// <summary>物攻: physical blows.</summary>
        PhysicalAttack,

        /// <summary>属攻: spells and healing.</summary>
        MagicAttack,

        /// <summary>物防: block, and blows with the shield.</summary>
        PhysicalDefense,
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

        /// <summary>
        /// Discards <see cref="CardAction.Amount"/> cards of the hand the player picks (捨てる),
        /// or the whole hand for -1.
        /// </summary>
        Discard,

        /// <summary>Puts <see cref="CardAction.Amount"/> cards the player picks from the discard pile into the hand.</summary>
        TakeFromDiscard,

        /// <summary>
        /// The next card played of <see cref="CardAction.Elements"/> (any card when none) grows
        /// by <see cref="CardAction.Amount"/> percent.
        /// </summary>
        NextCardBoost,
    }

    /// <summary>When an action of a card happens (doc/features/combat.md, カードのキーワード).</summary>
    public enum CardActionPhase
    {
        /// <summary>When the card is played.</summary>
        Play,

        /// <summary>
        /// When the card is played after the card of <see cref="CardSkill.ComboWith"/> in the
        /// same turn (連携), before its other actions.
        /// </summary>
        Combo,

        /// <summary>When a card's effect discards it from the hand (捨てたとき).</summary>
        Discarded,
    }

    /// <summary>How a card moves between the piles, and how it shows (doc/features/combat.md, カードのキーワード).</summary>
    [Flags]
    public enum CardTrait
    {
        None = 0,

        /// <summary>消滅: once played, it is out of the battle.</summary>
        Exhaust = 1,

        /// <summary>保持: it stays in the hand at the end of the turn.</summary>
        Retain = 2,

        /// <summary>霧散: left in the hand at the end of the turn, it is out of the battle.</summary>
        Ethereal = 4,

        /// <summary>初手: it is always in the battle's opening hand.</summary>
        Innate = 8,

        /// <summary>眠り札: each time it flows away or is discarded it wakes a stage and grows.</summary>
        Sleeper = 16,

        /// <summary>A big move: its effect opens with the cut-in.</summary>
        CutIn = 32,
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

        /// <summary>The target is chilled.</summary>
        OnChilled,

        /// <summary>The target has poison on it.</summary>
        OnPoisoned,

        /// <summary>The target is bleeding.</summary>
        OnBleeding,

        /// <summary>The target's defense is down.</summary>
        OnVulnerable,

        /// <summary>The target is paralysed.</summary>
        OnParalyzed,

        /// <summary>The card's user has a block.</summary>
        OnGuarded,
    }

    /// <summary>One thing a card does, in order.</summary>
    public sealed class CardAction
    {
        public CardActionKind Kind;
        public CardTarget Target;

        /// <summary>When it happens: as the card is played (most), on a combo, or when discarded.</summary>
        public CardActionPhase Phase;

        /// <summary>The stat the power is a part of, and the part in percent (damage, healing, block).</summary>
        public CardStat Stat;
        public int Percent;

        /// <summary>How many times the damage lands (for random and chained targets, a target each).</summary>
        public int Hits = 1;

        public CardStatus Status;
        public int Turns;

        /// <summary>
        /// The base chance in percent that a bad status lands on an enemy, before luck
        /// (<see cref="CombatFormula.StatusChance"/>); 0 for a status that always lands.
        /// </summary>
        public int Chance;

        /// <summary>Energy, cards drawn, cost lowered, poison stacks, or percent of the maximum HP revived.</summary>
        public int Amount;

        /// <summary>The cards whose cost <see cref="CardActionKind.CostDown"/> lowers.</summary>
        public CardElement[] Elements = Array.Empty<CardElement>();

        /// <summary>The action happens only when this holds.</summary>
        public CardCondition When;

        /// <summary>The power grows by <see cref="BonusPercent"/> when this holds.</summary>
        public CardCondition BonusWhen;
        public int BonusPercent;

        /// <summary>The power grows by this percent for every 1,000 ACT walked today, up to <see cref="ActCapPercent"/>.</summary>
        public int ActStepPercent;
        public int ActCapPercent;

        /// <summary>A damage that adds this percent of its user's block (シールドバッシュ).</summary>
        public int BlockPercent;

        /// <summary>
        /// The hits of a damage, or the cards drawn, grow by one for each card this card
        /// discarded (千刃乱舞).
        /// </summary>
        public bool PerDiscarded;

        /// <summary>The cards taken from the discard pile cost nothing this turn.</summary>
        public bool MakesFree;

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

        /// <summary>Discards <paramref name="amount"/> cards the player picks, or the whole hand for -1.</summary>
        public static CardAction Discard(int amount) =>
            new()
            {
                Kind = CardActionKind.Discard,
                Target = CardTarget.None,
                Amount = amount,
            };

        /// <summary>Puts <paramref name="amount"/> cards picked from the discard pile into the hand.</summary>
        public static CardAction TakeFromDiscard(int amount, bool free = false) =>
            new()
            {
                Kind = CardActionKind.TakeFromDiscard,
                Target = CardTarget.None,
                Amount = amount,
                MakesFree = free,
            };

        /// <summary>The next card of <paramref name="elements"/> (any when none) grows by <paramref name="percent"/>.</summary>
        public static CardAction NextCardBoost(int percent, params CardElement[] elements) =>
            new()
            {
                Kind = CardActionKind.NextCardBoost,
                Target = CardTarget.None,
                Amount = percent,
                Elements = elements,
            };

        /// <summary>The next card of <paramref name="elements"/> (any when none) costs nothing.</summary>
        public static CardAction NextCardFree(params CardElement[] elements) =>
            new()
            {
                Kind = CardActionKind.NextCardFree,
                Target = CardTarget.None,
                Elements = elements,
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

        /// <summary>The same status, landing with <paramref name="percent"/>% before luck.</summary>
        public CardAction WithChance(int percent)
        {
            Chance = percent;
            return this;
        }

        /// <summary>The same action, done only when <paramref name="when"/> holds.</summary>
        public CardAction Only(CardCondition when)
        {
            When = when;
            return this;
        }

        /// <summary>The same action, done when the card follows its combo partner (連携).</summary>
        public CardAction OnCombo()
        {
            Phase = CardActionPhase.Combo;
            return this;
        }

        /// <summary>The same action, done when an effect discards the card (捨てたとき).</summary>
        public CardAction WhenDiscarded()
        {
            Phase = CardActionPhase.Discarded;
            return this;
        }

        /// <summary>The same damage, adding <paramref name="percent"/>% of the user's block.</summary>
        public CardAction WithBlock(int percent)
        {
            BlockPercent = percent;
            return this;
        }

        /// <summary>The same damage or draw, one more hit or card for each card this card discarded.</summary>
        public CardAction Discarded()
        {
            PerDiscarded = true;
            return this;
        }

        /// <summary>The same action, growing with today's ACT.</summary>
        public CardAction WithAct(int stepPercent, int capPercent)
        {
            ActStepPercent = stepPercent;
            ActCapPercent = capPercent;
            return this;
        }

        /// <summary>A copy to change without changing this one (its elements are shared).</summary>
        public CardAction Copy() => (CardAction)MemberwiseClone();
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

        /// <summary>
        /// What the card does, in the order of its text: the actions as it is played, those of a
        /// combo and those when discarded (<see cref="CardAction.Phase"/>).
        /// </summary>
        public IReadOnlyList<CardAction> Actions = Array.Empty<CardAction>();

        /// <summary>How the card moves between the piles (消滅・保持・霧散・初手・眠り札) and whether it cuts in.</summary>
        public CardTrait Traits;

        /// <summary>The card (its root id) this one fuses with when both are in hand (合体), or null.</summary>
        public string FuseWith;

        /// <summary>The fused card (its id) the two become, or null.</summary>
        public string FusesInto;

        /// <summary>The card (its root id) that, played before this one in the same turn, sets off its combo (連携), or null.</summary>
        public string ComboWith;

        /// <summary>The id without an awakening's or evolution's stage ("Slash" of "Slash.1").</summary>
        public string RootId => Id == null ? null : Id.Split('.')[0];

        public bool Has(CardTrait trait) => (Traits & trait) == trait;

        /// <summary>
        /// The art of another card this one borrows (a prototype without art of its own), by
        /// that card's id; null for its own.
        /// </summary>
        public string BorrowedArt;

        /// <summary>The art's name in the card art folder.</summary>
        public string Art => "Card" + (BorrowedArt ?? Id);

        /// <summary>
        /// The upper limit of this kind's multiplier (doc/features/inventory.md, quality), set by
        /// each kind (<see cref="CardSkillQuality.LimitOf"/>).
        /// </summary>
        public float MaxMultiplier;

        /// <summary>
        /// This one card's multiplier, ×1.00 up to the limit. The numbers of <see cref="Actions"/>,
        /// <see cref="Cost"/> and <see cref="Text"/> already have it applied
        /// (<see cref="CardSkillQuality.WithMultiplier"/>); the definitions are at ×1.00.
        /// </summary>
        public float Multiplier = Inventory.Quality.MinMultiplier;

        /// <summary>
        /// How this kind's card sparkles (doc/art/direction.md, card sparkle), one style for every
        /// card of the kind and every stage of a unique skill: the card skills take the first
        /// styles in their order, the unique skills and the prototypes the ones after them.
        /// </summary>
        public int FoilStyle;

        /// <summary>The card at ×1.00 this one was grown from; null for a definition itself.</summary>
        public CardSkill Base;

        /// <summary>The quality of this one card, from its multiplier (S, A, B or C).</summary>
        public ItemQuality Quality =>
            Inventory.Quality.Of(Multiplier, CardSkillQuality.LimitOf(this));

        /// <summary>A copy to change without changing this one (its actions are shared).</summary>
        public CardSkill Copy() => (CardSkill)MemberwiseClone();

        /// <summary>The actions done in <paramref name="phase"/>, in order.</summary>
        public IEnumerable<CardAction> In(CardActionPhase phase)
        {
            foreach (var action in Actions)
                if (action.Phase == phase)
                    yield return action;
        }

        /// <summary>The actions that show a number in the description, in order.</summary>
        public IEnumerable<CardAction> Powered()
        {
            foreach (var action in Actions)
                if (action.HasPower)
                    yield return action;
        }
    }
}
