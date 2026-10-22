using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>Who an enemy's blows land on, among the allies still standing.</summary>
    public enum EnemyAim
    {
        /// <summary>One ally at random.</summary>
        One,

        /// <summary>The ally with the smallest part of its HP left.</summary>
        Weakest,

        /// <summary>The ally holding the most block (one at random when none holds any).</summary>
        MostBlock,

        /// <summary>Every ally.</summary>
        All,

        /// <summary>An ally at random for each hit; the same one may be hit again.</summary>
        Random,
    }

    /// <summary>Who an enemy's block, healing and boosts reach, among the enemies still standing.</summary>
    public enum EnemyCare
    {
        Self,

        /// <summary>Every other enemy; itself when none is left.</summary>
        Others,

        /// <summary>Itself and every other enemy.</summary>
        Everyone,

        /// <summary>The other enemy with the smallest part of its HP left; itself when none is left.</summary>
        WeakestOther,
    }

    /// <summary>
    /// One skill an enemy uses (doc/catalog/enemy-skills), as the battle can do it: a blow on the
    /// party, and block, healing and boosts for the enemies, in that order. Powers are percents of
    /// the enemy's stats at the level it shows up at. A skill whose effect the battle does not
    /// have yet (a status on the party, a call for help, a stolen card) is <see cref="Unsupported"/>
    /// and never chosen; one that only adds such a status to a blow keeps the blow.
    /// </summary>
    public sealed class EnemyMove
    {
        /// <summary>A boost that lasts until the end of the battle.</summary>
        public const int Forever = 99;

        public string Name;

        /// <summary>The blow: the stat its power is a part of (属攻 makes it a spell), the part, the hits.</summary>
        public CardStat Stat;
        public int Percent;
        public int Hits = 1;
        public EnemyAim Aim;

        /// <summary>The part of the stat the blow lands with on the allies not aimed at (0 for none).</summary>
        public int SplashPercent;

        /// <summary>The ally's defense does not soften the blow.</summary>
        public bool IgnoreDefense;

        /// <summary>The blow goes past the ally's block, leaving it.</summary>
        public bool IgnoreBlock;

        /// <summary>The ally's block is taken away before the blow.</summary>
        public bool BreakBlock;

        /// <summary>The part of the damage dealt the enemy heals itself by.</summary>
        public int DrainPercent;

        /// <summary>The part of its own max HP the enemy loses after it.</summary>
        public int SelfCostPercent;

        /// <summary>Who the block, healing and boosts reach.</summary>
        public EnemyCare Care;

        public CardStat BlockStat = CardStat.PhysicalDefense;
        public int BlockPercent;

        /// <summary>Healing as a part of a stat; with <see cref="CardStat.None"/>, a part of the max HP.</summary>
        public CardStat HealStat = CardStat.MagicAttack;
        public int HealPercent;

        /// <summary>Regen for this many turns, healing a part of 属攻 at each party turn.</summary>
        public int RegenTurns;
        public int RegenPercent;

        /// <summary>Attack up (the cards' 25%) and protection for this many turns (or <see cref="Forever"/>).</summary>
        public int AttackUpTurns;
        public int ProtectTurns;

        /// <summary>The next attacking skill of this enemy is this much stronger (力を溜める).</summary>
        public int PowerUpPercent;

        /// <summary>
        /// The turns it gathers its strength before it lands (前のターンに溜めを予告する), shown as it
        /// starts; 0 lands at once. <see cref="ChargeWith"/> is done on each turn of gathering too.
        /// </summary>
        public int ChargeTurns;
        public string ChargeWith;

        /// <summary>Why the battle cannot do it yet; null when it can.</summary>
        public string Unsupported;

        public bool Strikes => Percent > 0;
        public bool Supported => Unsupported == null;

        /// <summary>A blow on the party: <paramref name="percent"/> of <paramref name="stat"/>.</summary>
        public static EnemyMove Attack(
            string name,
            CardStat stat,
            int percent,
            EnemyAim aim = EnemyAim.One,
            int hits = 1
        ) =>
            new()
            {
                Name = name,
                Stat = stat,
                Percent = percent,
                Aim = aim,
                Hits = hits,
            };

        /// <summary>Block of <paramref name="percent"/> of 物防 (or <paramref name="stat"/>).</summary>
        public static EnemyMove Guard(
            string name,
            int percent,
            EnemyCare care = EnemyCare.Self,
            CardStat stat = CardStat.PhysicalDefense
        ) => new EnemyMove { Name = name, Care = care }.WithGuard(percent, stat);

        /// <summary>Healing of <paramref name="percent"/> of 属攻 (or of the max HP, with <see cref="CardStat.None"/>).</summary>
        public static EnemyMove Mend(
            string name,
            int percent,
            EnemyCare care = EnemyCare.Self,
            CardStat stat = CardStat.MagicAttack
        ) => new EnemyMove { Name = name, Care = care }.WithMend(percent, stat);

        /// <summary>Attack up for <paramref name="turns"/> turns.</summary>
        public static EnemyMove Boost(
            string name,
            int turns,
            EnemyCare care = EnemyCare.Everyone
        ) => new EnemyMove { Name = name, Care = care }.WithBoost(turns);

        /// <summary>Protection (damage taken -25%) for <paramref name="turns"/> turns.</summary>
        public static EnemyMove Shield(
            string name,
            int turns,
            EnemyCare care = EnemyCare.Everyone
        ) =>
            new()
            {
                Name = name,
                Care = care,
                ProtectTurns = turns,
            };

        /// <summary>The next attacking skill is <paramref name="percent"/> stronger.</summary>
        public static EnemyMove Focus(string name, int percent = 50) =>
            new() { Name = name, PowerUpPercent = percent };

        /// <summary>
        /// A skill that only starts the second part of the battle, when what it does besides is
        /// not in the battle yet (its pattern's changes still come).
        /// </summary>
        public static EnemyMove Awaken(string name) => new() { Name = name };

        /// <summary>A skill the battle cannot do yet (<paramref name="why"/>): it is never chosen.</summary>
        public static EnemyMove Other(string name, string why) =>
            new() { Name = name, Unsupported = why };

        /// <summary>The same skill, gathering its strength for <paramref name="turns"/> turns first.</summary>
        public EnemyMove Charged(int turns = 1, string with = null)
        {
            ChargeTurns = turns;
            ChargeWith = with;
            return this;
        }

        /// <summary>The same blow, also landing on the other allies with <paramref name="percent"/>.</summary>
        public EnemyMove Splash(int percent)
        {
            SplashPercent = percent;
            return this;
        }

        public EnemyMove Piercing()
        {
            IgnoreDefense = true;
            return this;
        }

        public EnemyMove PastBlock()
        {
            IgnoreBlock = true;
            return this;
        }

        public EnemyMove Breaking()
        {
            BreakBlock = true;
            return this;
        }

        public EnemyMove Draining(int percent)
        {
            DrainPercent = percent;
            return this;
        }

        public EnemyMove Costing(int percent)
        {
            SelfCostPercent = percent;
            return this;
        }

        public EnemyMove WithGuard(int percent, CardStat stat = CardStat.PhysicalDefense)
        {
            BlockPercent = percent;
            BlockStat = stat;
            return this;
        }

        public EnemyMove WithMend(int percent, CardStat stat = CardStat.MagicAttack)
        {
            HealPercent = percent;
            HealStat = stat;
            return this;
        }

        public EnemyMove WithRegen(int turns, int percent)
        {
            RegenTurns = turns;
            RegenPercent = percent;
            return this;
        }

        public EnemyMove WithBoost(int turns)
        {
            AttackUpTurns = turns;
            return this;
        }

        public EnemyMove WithProtect(int turns)
        {
            ProtectTurns = turns;
            return this;
        }

        public EnemyMove WithPowerUp(int percent = 50)
        {
            PowerUpPercent = percent;
            return this;
        }

        /// <summary>The same skill for <see cref="EnemyCare"/> other than itself.</summary>
        public EnemyMove For(EnemyCare care)
        {
            Care = care;
            return this;
        }
    }

    /// <summary>What a rule of an enemy's pattern looks at (doc/catalog/enemies, the patterns).</summary>
    public enum EnemyCue
    {
        /// <summary>Always: a skill used every few turns, or from a turn on.</summary>
        Always,

        /// <summary>Its own HP is at most <see cref="EnemyRule.Below"/> of its max.</summary>
        Hurt,

        /// <summary>Another enemy standing has at most <see cref="EnemyRule.Below"/> of its HP.</summary>
        OtherHurt,

        /// <summary>No other enemy stands.</summary>
        Alone,

        /// <summary>Another enemy stands.</summary>
        Together,

        /// <summary>An ally has at most <see cref="EnemyRule.Below"/> of its HP.</summary>
        PartyHurt,

        /// <summary>An ally holds block.</summary>
        PartyGuarded,
    }

    /// <summary>
    /// A skill used instead of the usual choice when <see cref="Cue"/> holds (条件で割り込む技),
    /// at most <see cref="Limit"/> times a battle, <see cref="Every"/> turns apart, from turn
    /// <see cref="From"/>; the next action is <see cref="Then"/>, when given.
    /// </summary>
    public sealed class EnemyRule
    {
        public EnemyCue Cue;
        public float Below = 0.5f;
        public string Move;
        public string Then;

        /// <summary>Times a battle at most; 0 for no limit.</summary>
        public int Limit;

        /// <summary>Battle turns at least since it was last used by this rule or the pattern; 0 for any.</summary>
        public int Every;

        /// <summary>Not before this battle turn.</summary>
        public int From;

        public static EnemyRule When(EnemyCue cue, string move, float below = 0.5f) =>
            new()
            {
                Cue = cue,
                Move = move,
                Below = below,
            };

        public EnemyRule Times(int limit)
        {
            Limit = limit;
            return this;
        }

        public EnemyRule Apart(int turns)
        {
            Every = turns;
            return this;
        }

        public EnemyRule After(int turn)
        {
            From = turn;
            return this;
        }

        public EnemyRule Next(string move)
        {
            Then = move;
            return this;
        }
    }

    /// <summary>One weighted skill of a pattern's choice.</summary>
    public readonly struct EnemyChoice
    {
        public EnemyChoice(string move, int weight)
        {
            Move = move;
            Weight = weight;
        }

        public string Move { get; }
        public int Weight { get; }
    }

    /// <summary>
    /// How an enemy picks its skills in one part of the battle: an opening skill, then rules that
    /// cut in, then the usual choice, which is either an order gone round and round, a weighted
    /// choice, or a cycle of named skills and choices (<see cref="Pick"/>).
    /// </summary>
    public sealed class EnemyPattern
    {
        /// <summary>A cycle's place for one of <see cref="Choices"/>.</summary>
        public const string Pick = "*";

        /// <summary>The first action's skill, and the one used instead when no other enemy stands.</summary>
        public string Opening;
        public string OpeningAlone;

        public string[] Order = Array.Empty<string>();
        public EnemyChoice[] Choices = Array.Empty<EnemyChoice>();
        public string[] Cycle = Array.Empty<string>();

        /// <summary>The same skill at most this many times in a row (同じ技を3回続けず: 2).</summary>
        public int MaxRun = 2;

        /// <summary>Skills never used twice in a row (2回続けない).</summary>
        public string[] Once = Array.Empty<string>();

        public EnemyRule[] Rules = Array.Empty<EnemyRule>();

        /// <summary>Blows on one ally land on every ally with this part of their power (0 for none).</summary>
        public int SpreadPercent;

        /// <summary>Skills used one after another on each of its actions.</summary>
        public int ActionsPerTurn = 1;

        public static EnemyChoice Weight(string move, int weight) => new(move, weight);

        /// <summary>The longest run of <paramref name="move"/> allowed.</summary>
        public int RunOf(string move) => Array.IndexOf(Once, move) >= 0 ? 1 : MaxRun;
    }

    /// <summary>How much stronger a skill is while the enemy is enraged, and its extra hits.</summary>
    public readonly struct EnemyEnrage
    {
        public EnemyEnrage(string move, int percent, int extraHits = 0)
        {
            Move = move;
            Percent = percent;
            ExtraHits = extraHits;
        }

        public string Move { get; }

        /// <summary>The power in percent of the usual (130 for 1.3 times).</summary>
        public int Percent { get; }
        public int ExtraHits { get; }
    }

    /// <summary>
    /// One kind of enemy's skills and patterns (doc/catalog/enemies, 技 and 行動パターン): the
    /// first part of the battle, its enrage from turn <see cref="EnrageTurn"/>, and for those that
    /// change their whole way of fighting, the second part (後半) after <see cref="LateMove"/>,
    /// used on turn <see cref="LateTurn"/> or once its HP is down to <see cref="LateBelow"/>,
    /// whichever comes first.
    /// </summary>
    public sealed class EnemyBehavior
    {
        public string Id;
        public EnemyMove[] Moves = Array.Empty<EnemyMove>();
        public EnemyPattern Early = new();

        /// <summary>The battle turn its enrage starts on; 0 for none.</summary>
        public int EnrageTurn;
        public EnemyEnrage[] Enrage = Array.Empty<EnemyEnrage>();

        public EnemyPattern Late;
        public int LateTurn;
        public float LateBelow = 0.5f;
        public string LateMove;

        public EnemyMove Move(string name)
        {
            foreach (var move in Moves)
                if (move.Name == name)
                    return move;
            return null;
        }

        /// <summary>True when the skill is one the battle can do.</summary>
        public bool Usable(string name) => Move(name) is { Supported: true };

        /// <summary>The enraged change of a skill, or none.</summary>
        public EnemyEnrage? EnrageOf(string name)
        {
            foreach (var change in Enrage)
                if (change.Move == name)
                    return change;
            return null;
        }

        /// <summary>The names of every skill a pattern or rule names, for checking the data.</summary>
        public IEnumerable<string> Named()
        {
            foreach (var pattern in new[] { Early, Late })
            {
                if (pattern == null)
                    continue;
                if (pattern.Opening != null)
                    yield return pattern.Opening;
                if (pattern.OpeningAlone != null)
                    yield return pattern.OpeningAlone;
                foreach (string move in pattern.Order)
                    yield return move;
                foreach (var choice in pattern.Choices)
                    yield return choice.Move;
                foreach (string move in pattern.Cycle)
                    if (move != EnemyPattern.Pick)
                        yield return move;
                foreach (string move in pattern.Once)
                    yield return move;
                foreach (var rule in pattern.Rules)
                {
                    yield return rule.Move;
                    if (rule.Then != null)
                        yield return rule.Then;
                }
            }
            if (LateMove != null)
                yield return LateMove;
            foreach (var change in Enrage)
                yield return change.Move;
            foreach (var move in Moves)
                if (move.ChargeWith != null)
                    yield return move.ChargeWith;
        }
    }
}
