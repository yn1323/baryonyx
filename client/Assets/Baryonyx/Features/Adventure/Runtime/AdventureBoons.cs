using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The step of a boon after a won battle (doc/features/stage-progression.md, 戦闘後の強化の中身):
    /// the card's frame shows it whitish, gold or rainbow, with no name on the screen.
    /// </summary>
    public enum AdventureBoonRank
    {
        White,
        Gold,
        Rainbow,
    }

    /// <summary>How hard the battle won was, which sets how often each step comes up.</summary>
    public enum AdventureBoonBattle
    {
        // 最奥の間（勝つと冒険が終わる）と、戦闘のないマス。強化は出ない。
        None,

        // 魔物（真ん中の宝箱の階より前・後）。条件を満たさずに勝った試練も同じ。
        Early,
        Late,

        // 強い魔物とミミック。
        Strong,

        // 条件を満たして勝った試練。
        TrialMet,
    }

    /// <summary>How a boon grows the party (the boon's 系統).</summary>
    public enum AdventureBoonFamily
    {
        Single,
        Party,
        Pair,
        Situational,
        Growth,
        Cost,
        Training,
    }

    /// <summary>
    /// The stats an effect grows. <see cref="Attack"/> is the member's own attack: physical or
    /// magic, whichever its cards use (<see cref="AdventureBoons.AttackOf"/>).
    /// </summary>
    public enum AdventureBoonStats
    {
        Attack,
        Attacks,
        Hp,
        PhysicalDefense,
        MagicDefense,
        Defenses,
        Speed,
    }

    /// <summary>When an effect works.</summary>
    public enum AdventureBoonWhen
    {
        Always,

        // 強い魔物・試練・ミミック・最奥の間の戦闘でだけ。
        StrongBattle,

        // 選んだあと戦闘に勝つたびに、上限まで。
        PerWin,

        // 選んでから TrainingBattles 回の戦闘のあいだ。
        WhileTraining,

        // その後。
        AfterTraining,

        // 戦闘の途中で、HPが半分以下の間・最初のターンの間・倒れた仲間1人につき（BattleStatBoost）。
        HalfHp,
        FirstTurns,
        PerFallenAlly,
    }

    /// <summary>One change a boon makes: the stats, by how many percent at each step, and when.</summary>
    public sealed class AdventureBoonEffect
    {
        public AdventureBoonEffect(
            AdventureBoonStats stats,
            int[] percents,
            AdventureBoonWhen when = AdventureBoonWhen.Always,
            int[] caps = null
        )
        {
            Stats = stats;
            Percents = percents;
            When = when;
            Caps = caps ?? Array.Empty<int>();
        }

        public AdventureBoonStats Stats { get; }

        // 段階（白・金・虹）ごとの幅。PerWin では1勝ごとの幅。
        public int[] Percents { get; }
        public AdventureBoonWhen When { get; }

        // PerWin の段階ごとの上限。
        public int[] Caps { get; }

        public int PercentAt(AdventureBoonRank rank) => Percents[(int)rank];

        public int CapAt(AdventureBoonRank rank) =>
            (int)rank < Caps.Length ? Caps[(int)rank] : int.MaxValue;
    }

    /// <summary>One kind of boon: who it grows, its name (empty for the plain ones) and its effects.</summary>
    public sealed class AdventureBoonTemplate
    {
        public string Id = "";
        public string Name = "";
        public AdventureBoonFamily Family;
        public bool Everyone;
        public AdventureBoonEffect[] Effects = Array.Empty<AdventureBoonEffect>();
    }

    /// <summary>
    /// A boon offered or chosen: its kind and step, the party slot it grows (-1 for everyone)
    /// and the attack that slot uses.
    /// </summary>
    public readonly struct AdventureBoon : IEquatable<AdventureBoon>
    {
        public AdventureBoon(string id, AdventureBoonRank rank, int slot, CardStat attack)
        {
            Id = id ?? "";
            Rank = rank;
            Slot = slot;
            Attack =
                attack == CardStat.MagicAttack ? CardStat.MagicAttack : CardStat.PhysicalAttack;
        }

        public string Id { get; }
        public AdventureBoonRank Rank { get; }
        public int Slot { get; }
        public CardStat Attack { get; }

        public AdventureBoonTemplate Template => AdventureBoons.Find(Id);
        public bool Everyone => Slot < 0;

        /// <summary>The boon as it is kept (id/rank/slot/p or m).</summary>
        public string Encode() =>
            $"{Id}/{(int)Rank}/{Slot}/{(Attack == CardStat.MagicAttack ? "m" : "p")}";

        /// <summary>A boon kept with <see cref="Encode"/>, or null for one not understood.</summary>
        public static AdventureBoon? Decode(string text)
        {
            var parts = (text ?? "").Split('/');
            if (
                parts.Length != 4
                || AdventureBoons.Find(parts[0]) == null
                || !int.TryParse(parts[1], out int rank)
                || rank < 0
                || rank > (int)AdventureBoonRank.Rainbow
                || !int.TryParse(parts[2], out int slot)
            )
                return null;
            return new AdventureBoon(
                parts[0],
                (AdventureBoonRank)rank,
                slot,
                parts[3] == "m" ? CardStat.MagicAttack : CardStat.PhysicalAttack
            );
        }

        public bool Equals(AdventureBoon other) =>
            Id == other.Id && Rank == other.Rank && Slot == other.Slot && Attack == other.Attack;

        public override bool Equals(object obj) => obj is AdventureBoon other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Id, Rank, Slot, Attack);
    }

    /// <summary>
    /// The boons after a won battle (doc/features/stage-progression.md, 戦闘後の強化): two for each
    /// member are offered and one is chosen; they last for the adventure only. They are made from the
    /// adventure's seed and the room, so the same win offers the same ones on any device and
    /// after resuming. A harder battle offers higher steps more often, and a run of offers with
    /// no rainbow raises its chance. The stats grow before each battle, added up with the rest's
    /// training (休憩の鍛える) and multiplied once.
    /// </summary>
    public static class AdventureBoons
    {
        // 仲間1人に出す候補の数。
        public const int PerMember = 2;

        // 段階を引く回数（候補が3つだったころと同じ。残りの候補はその戦闘の一番下の段階）。
        public const int RankDraws = 3;

        // 修行で下がる戦闘の数。
        public const int TrainingBattles = 2;

        // 虹が出なかった候補ごとに、次の虹の割合へ足す%。
        public const int PityStep = 3;

        // どの項目も、元の値のこの割合より下げない。
        public const float Floor = BattleStatBoosts.Floor;

        // 先手が効く、戦闘の最初のターンの数。
        public const int VanguardTurns = 2;

        // 段階ごとの重み（白・金・虹、%）。
        private static readonly int[] EarlyWeights = { 70, 25, 5 };
        private static readonly int[] LateWeights = { 60, 32, 8 };
        private static readonly int[] StrongWeights = { 20, 55, 25 };
        private static readonly int[] TrialWeights = { 0, 50, 50 };

        // 系統の重みと、出る段階（白でも出るか）。
        private static readonly (AdventureBoonFamily Family, int Weight, bool White)[] Families =
        {
            (AdventureBoonFamily.Single, 30, true),
            (AdventureBoonFamily.Party, 25, true),
            (AdventureBoonFamily.Pair, 20, true),
            (AdventureBoonFamily.Situational, 10, false),
            (AdventureBoonFamily.Growth, 8, false),
            (AdventureBoonFamily.Cost, 5, false),
            (AdventureBoonFamily.Training, 2, false),
        };

        private static readonly int[] SinglePercents = { 12, 20, 30 };
        private static readonly int[] PartyPercents = { 5, 8, 12 };
        private static readonly int[] PairPercents = { 7, 12, 18 };

        /// <summary>
        /// Every kind of boon. 背水・先手・弔い work while their condition holds during the battle,
        /// so they do not grow the stats before it: the battle works them out
        /// (<see cref="BattleStatBoost"/>).
        /// </summary>
        public static readonly IReadOnlyList<AdventureBoonTemplate> Templates = new[]
        {
            Single("attack", AdventureBoonStats.Attack),
            Single("hp", AdventureBoonStats.Hp),
            Single("physical-defense", AdventureBoonStats.PhysicalDefense),
            Single("magic-defense", AdventureBoonStats.MagicDefense),
            Party("party-attack", AdventureBoonStats.Attacks),
            Party("party-hp", AdventureBoonStats.Hp),
            Party("party-guard", AdventureBoonStats.Defenses),
            Party("party-speed", AdventureBoonStats.Speed),
            Pair("valor", "勇猛", AdventureBoonStats.Attack, AdventureBoonStats.Hp),
            Pair("bulwark", "堅守", AdventureBoonStats.Defenses),
            Pair("nimble", "身軽", AdventureBoonStats.Attack, AdventureBoonStats.Speed),
            new AdventureBoonTemplate
            {
                Id = "giant-slayer",
                Name = "強敵狩り",
                Family = AdventureBoonFamily.Situational,
                Effects = new[]
                {
                    new AdventureBoonEffect(
                        AdventureBoonStats.Attacks,
                        new[] { 0, 25, 40 },
                        AdventureBoonWhen.StrongBattle
                    ),
                },
            },
            Situational(
                "backwater",
                "背水",
                false,
                AdventureBoonStats.Attacks,
                new[] { 0, 30, 50 },
                AdventureBoonWhen.HalfHp
            ),
            Situational(
                "vanguard",
                "先手",
                true,
                AdventureBoonStats.Speed,
                new[] { 0, 25, 40 },
                AdventureBoonWhen.FirstTurns
            ),
            Situational(
                "mourning",
                "弔い",
                true,
                AdventureBoonStats.Attacks,
                new[] { 0, 10, 15 },
                AdventureBoonWhen.PerFallenAlly
            ),
            new AdventureBoonTemplate
            {
                Id = "seasoned",
                Name = "場数",
                Family = AdventureBoonFamily.Growth,
                Effects = new[]
                {
                    new AdventureBoonEffect(
                        AdventureBoonStats.Attack,
                        new[] { 0, 4, 6 },
                        AdventureBoonWhen.PerWin,
                        new[] { 0, 20, 30 }
                    ),
                },
            },
            Cost(
                "reckless",
                "捨て身",
                false,
                (AdventureBoonStats.Attack, new[] { 0, 30, 45 }),
                (AdventureBoonStats.Defenses, new[] { 0, -20, -25 })
            ),
            Cost(
                "glass-blade",
                "ガラスの刃",
                false,
                (AdventureBoonStats.Attack, new[] { 0, 40, 60 }),
                (AdventureBoonStats.Hp, new[] { 0, -25, -30 })
            ),
            Cost(
                "heavy-armor",
                "重装",
                false,
                (AdventureBoonStats.Defenses, new[] { 0, 30, 45 }),
                (AdventureBoonStats.Speed, new[] { 0, -20, -25 })
            ),
            Cost(
                "fleet",
                "早駆け",
                true,
                (AdventureBoonStats.Speed, new[] { 0, 12, 18 }),
                (AdventureBoonStats.Hp, new[] { 0, -8, -10 })
            ),
            new AdventureBoonTemplate
            {
                Id = "training",
                Name = "修行",
                Family = AdventureBoonFamily.Training,
                Effects = new[]
                {
                    new AdventureBoonEffect(
                        AdventureBoonStats.Defenses,
                        new[] { 0, -20, -20 },
                        AdventureBoonWhen.WhileTraining
                    ),
                    new AdventureBoonEffect(
                        AdventureBoonStats.Attack,
                        new[] { 0, 30, 45 },
                        AdventureBoonWhen.AfterTraining
                    ),
                },
            },
        };

        private static AdventureBoonTemplate Single(string id, AdventureBoonStats stats) =>
            new()
            {
                Id = id,
                Family = AdventureBoonFamily.Single,
                Effects = new[] { new AdventureBoonEffect(stats, SinglePercents) },
            };

        private static AdventureBoonTemplate Party(string id, AdventureBoonStats stats) =>
            new()
            {
                Id = id,
                Family = AdventureBoonFamily.Party,
                Everyone = true,
                Effects = new[] { new AdventureBoonEffect(stats, PartyPercents) },
            };

        // 組み合わせ：2つの項目をそれぞれ上げる。物防と属防は Defenses の1つで2つの項目になる。
        private static AdventureBoonTemplate Pair(
            string id,
            string name,
            params AdventureBoonStats[] stats
        ) =>
            new()
            {
                Id = id,
                Name = name,
                Family = AdventureBoonFamily.Pair,
                Effects = stats
                    .Select(stat => new AdventureBoonEffect(stat, PairPercents))
                    .ToArray(),
            };

        private static AdventureBoonTemplate Situational(
            string id,
            string name,
            bool everyone,
            AdventureBoonStats stats,
            int[] percents,
            AdventureBoonWhen when
        ) =>
            new()
            {
                Id = id,
                Name = name,
                Family = AdventureBoonFamily.Situational,
                Everyone = everyone,
                Effects = new[] { new AdventureBoonEffect(stats, percents, when) },
            };

        private static AdventureBoonTemplate Cost(
            string id,
            string name,
            bool everyone,
            params (AdventureBoonStats Stats, int[] Percents)[] effects
        ) =>
            new()
            {
                Id = id,
                Name = name,
                Family = AdventureBoonFamily.Cost,
                Everyone = everyone,
                Effects = effects
                    .Select(effect => new AdventureBoonEffect(effect.Stats, effect.Percents))
                    .ToArray(),
            };

        public static AdventureBoonTemplate Find(string id) =>
            Templates.FirstOrDefault(template => template.Id == id);

        // --- Offers --------------------------------------------------------------------------

        /// <summary>
        /// How hard the battle of <paramref name="run"/>'s room was: monsters early or late in the
        /// way (after the middle chest's floor), strong monsters and the mimic, or a trial won with
        /// its condition (one won without it counts as monsters). None for the deepest room.
        /// </summary>
        public static AdventureBoonBattle BattleOf(AdventureRun run, bool trialMet)
        {
            var room = run?.Room;
            if (room == null)
                return AdventureBoonBattle.None;
            switch (room.Kind)
            {
                case AdventureRoomKind.Elite:
                case AdventureRoomKind.ShadyChest:
                    return AdventureBoonBattle.Strong;
                case AdventureRoomKind.Trial:
                    return trialMet ? AdventureBoonBattle.TrialMet : MonstersAt(run, room.Floor);
                case AdventureRoomKind.Battle:
                    return MonstersAt(run, room.Floor);
                default:
                    return AdventureBoonBattle.None;
            }
        }

        /// <summary>
        /// Whether the battle of <paramref name="run"/>'s room is a strong one, where 強敵狩り
        /// works: strong monsters, a trial, the mimic or the deepest room.
        /// </summary>
        public static bool IsStrongBattle(AdventureRun run) =>
            run?.Room?.Kind
                is AdventureRoomKind.Elite
                    or AdventureRoomKind.Trial
                    or AdventureRoomKind.ShadyChest
                    or AdventureRoomKind.Boss;

        private static AdventureBoonBattle MonstersAt(AdventureRun run, int floor)
        {
            int roomCount = run.Map?.RoomCount ?? 0;
            // 真ん中の宝箱の階（AdventureRouteMap.FixedKindOf）より奥なら後半。
            int middle = roomCount / 2 + 2;
            return floor > middle ? AdventureBoonBattle.Late : AdventureBoonBattle.Early;
        }

        /// <summary>
        /// The attack a member's cards use: physical or magic, whichever more of its cards' powers
        /// are worked out from, and physical on a tie. Only the cards decide, so the stats the
        /// boons grew do not change the offer.
        /// </summary>
        public static CardStat AttackOf(BattlePartyMember member)
        {
            if (member == null)
                return CardStat.PhysicalAttack;
            int physical = 0;
            int magic = 0;
            foreach (var card in member.Cards ?? Array.Empty<BattlePartyCard>())
            foreach (var action in card?.Skill?.Powered() ?? Enumerable.Empty<CardAction>())
                if (action.Stat == CardStat.PhysicalAttack)
                    physical++;
                else if (action.Stat == CardStat.MagicAttack)
                    magic++;
            return magic > physical ? CardStat.MagicAttack : CardStat.PhysicalAttack;
        }

        /// <summary>
        /// The boons offered after winning <paramref name="roomId"/>'s battle, made from the
        /// adventure's <paramref name="seed"/>: <see cref="PerMember"/> for each member there, in
        /// slot order (the member's own first, then the next member's). <paramref name="pity"/>
        /// is how many offers in a row came with no rainbow. <paramref name="members"/> are the
        /// party in slot order (null for an empty slot). The steps are drawn
        /// <see cref="RankDraws"/> times as before and the rest are the battle's lowest step, so
        /// the more choices do not make better boons come more often. A member's two are of
        /// different kinds, a boon for everyone comes once at most, and a cost or training boon
        /// comes once at most.
        /// </summary>
        public static IReadOnlyList<AdventureBoon> Offer(
            int seed,
            string roomId,
            AdventureBoonBattle battle,
            int pity,
            IReadOnlyList<BattlePartyMember> members
        )
        {
            var present = Present(members);
            if (battle == AdventureBoonBattle.None || present.Length == 0)
                return Array.Empty<AdventureBoon>();
            var random = new RouteRandom(Seed(seed, roomId));
            var ranks = Spread(random, Ranks(random, battle, pity), present.Length * PerMember, battle);
            var offers = new List<AdventureBoon>();
            for (int i = 0; i < ranks.Length; i++)
            {
                int slot = present[i / PerMember];
                var mine = offers.Skip(i / PerMember * PerMember).ToArray();
                AdventureBoon? boon = null;
                for (int attempt = 0; attempt < 64 && boon == null; attempt++)
                    boon = Pick(random, ranks[i], slot, offers, mine, members);
                // 決まりで選べなかったら、その仲間のまだ出ていない1人の強化にする。
                offers.Add(boon ?? Fallback(ranks[i], slot, mine, members));
            }
            return offers;
        }

        /// <summary>The slots of the members there, in slot order: one column of the offers each.</summary>
        public static int[] Present(IReadOnlyList<BattlePartyMember> members) =>
            Enumerable.Range(0, members?.Count ?? 0).Where(slot => members[slot] != null).ToArray();

        // 引いた段階を count 個の候補のどこかへ散らし、残りをその戦闘の一番下の段階で埋める。
        private static AdventureBoonRank[] Spread(
            RouteRandom random,
            AdventureBoonRank[] drawn,
            int count,
            AdventureBoonBattle battle
        )
        {
            var lowest = (AdventureBoonRank)Array.FindIndex(WeightsOf(battle), weight => weight > 0);
            var ranks = Enumerable.Repeat(lowest, count).ToArray();
            var places = Enumerable.Range(0, count).ToList();
            foreach (var rank in drawn)
            {
                if (places.Count == 0)
                    break;
                int at = random.Range(0, places.Count);
                ranks[places[at]] = rank;
                places.RemoveAt(at);
            }
            return ranks;
        }

        /// <summary>The <see cref="RankDraws"/> steps drawn, each on its own, then the battle's floor.</summary>
        public static AdventureBoonRank[] Ranks(
            RouteRandom random,
            AdventureBoonBattle battle,
            int pity
        )
        {
            var weights = (int[])WeightsOf(battle).Clone();
            // 虹が出ない候補が続くほど、虹の割合を足す（白から、白が足りなければ金から引く）。
            int extra = Math.Max(0, pity) * PityStep;
            int fromWhite = Math.Min(extra, weights[0]);
            weights[0] -= fromWhite;
            int fromGold = Math.Min(extra - fromWhite, weights[1]);
            weights[1] -= fromGold;
            weights[2] += fromWhite + fromGold;
            var ranks = new AdventureBoonRank[RankDraws];
            int total = weights.Sum();
            for (int i = 0; i < ranks.Length; i++)
            {
                double roll = random.Next() * total;
                int rank = 0;
                while (rank < weights.Length - 1 && roll >= weights[rank])
                    roll -= weights[rank++];
                ranks[i] = (AdventureBoonRank)rank;
            }
            if (
                battle == AdventureBoonBattle.Strong
                && ranks.All(r => r == AdventureBoonRank.White)
            )
                ranks[0] = AdventureBoonRank.Gold;
            if (
                battle == AdventureBoonBattle.TrialMet
                && ranks.All(r => r != AdventureBoonRank.Rainbow)
            )
                ranks[0] = AdventureBoonRank.Rainbow;
            return ranks;
        }

        private static int[] WeightsOf(AdventureBoonBattle battle) =>
            battle switch
            {
                AdventureBoonBattle.Late => LateWeights,
                AdventureBoonBattle.Strong => StrongWeights,
                AdventureBoonBattle.TrialMet => TrialWeights,
                _ => EarlyWeights,
            };

        private static AdventureBoon? Pick(
            RouteRandom random,
            AdventureBoonRank rank,
            int slot,
            IReadOnlyList<AdventureBoon> offers,
            IReadOnlyList<AdventureBoon> mine,
            IReadOnlyList<BattlePartyMember> members
        )
        {
            bool costly = offers.Any(offer => IsCostly(offer.Template));
            var families = Families
                .Where(entry => rank != AdventureBoonRank.White || entry.White)
                .Where(entry =>
                    !costly
                    || (
                        entry.Family != AdventureBoonFamily.Cost
                        && entry.Family != AdventureBoonFamily.Training
                    )
                )
                .ToArray();
            int total = families.Sum(entry => entry.Weight);
            double roll = random.Next() * total;
            var family = families[^1].Family;
            foreach (var entry in families)
            {
                if (roll < entry.Weight)
                {
                    family = entry.Family;
                    break;
                }
                roll -= entry.Weight;
            }
            bool everyone = offers.Any(offer => offer.Everyone);
            var templates = Templates
                .Where(template =>
                    template.Family == family
                    && mine.All(offer => offer.Id != template.Id)
                    && !(template.Everyone && everyone)
                )
                .ToArray();
            if (templates.Length == 0)
                return null;
            var picked = templates[random.Range(0, templates.Length)];
            return picked.Everyone
                ? new AdventureBoon(picked.Id, rank, -1, CardStat.PhysicalAttack)
                : new AdventureBoon(picked.Id, rank, slot, AttackOf(members[slot]));
        }

        private static AdventureBoon Fallback(
            AdventureBoonRank rank,
            int slot,
            IReadOnlyList<AdventureBoon> mine,
            IReadOnlyList<BattlePartyMember> members
        )
        {
            var template =
                Templates.FirstOrDefault(template =>
                    template.Family == AdventureBoonFamily.Single
                    && mine.All(offer => offer.Id != template.Id)
                ) ?? Templates[0];
            return new AdventureBoon(template.Id, rank, slot, AttackOf(members[slot]));
        }

        private static bool IsCostly(AdventureBoonTemplate template) =>
            template != null
            && (
                template.Family == AdventureBoonFamily.Cost
                || template.Family == AdventureBoonFamily.Training
            );

        // 冒険の種とマスから、候補を作る乱数の種を決める（AdventureCatalog の敵の組み合わせと同じ作り方）。
        private static int Seed(int seed, string roomId)
        {
            unchecked
            {
                uint hash = 2166136261u ^ (uint)seed;
                foreach (char c in "boon:" + (roomId ?? ""))
                    hash = (hash ^ c) * 16777619u;
                hash ^= hash >> 13;
                hash *= 0x5bd1e995u;
                hash ^= hash >> 15;
                return (int)hash;
            }
        }

        // --- Growing the party ---------------------------------------------------------------

        /// <summary>
        /// Grows the members' stats by the boons chosen in the adventure (in the order chosen) and
        /// by <paramref name="honed"/>, how many times each slot was trained at a rest. Every
        /// stat's percents are added up and multiplied once, and no stat falls under
        /// <see cref="Floor"/> of its own. <paramref name="strongBattle"/> is whether the battle
        /// about to be fought is a strong one (強敵狩り). The boons that work during the battle
        /// (背水・先手・弔い) go to the members as boosts, with the stats before any boon and the
        /// percents, which the boosts add to.
        /// </summary>
        public static void Apply(
            IReadOnlyList<BattlePartyMember> members,
            IReadOnlyList<AdventureBoon> boons,
            IReadOnlyList<int> honed,
            float honeStat,
            bool strongBattle
        )
        {
            if (members == null)
                return;
            var percents = PercentsOf(members.Count, boons, honed, honeStat, strongBattle);
            for (int slot = 0; slot < members.Count; slot++)
            {
                var member = members[slot];
                if (member == null)
                    continue;
                var stats = member.Stats;
                var p = percents[slot];
                member.Unboosted = stats;
                member.Percents = p;
                member.Boosts = BoostsOf(slot, boons);
                stats.Hp = Grow(stats.Hp, p[(int)Stat.Hp]);
                stats.PhysicalAttack = Grow(stats.PhysicalAttack, p[(int)Stat.PhysicalAttack]);
                stats.PhysicalDefense = Grow(stats.PhysicalDefense, p[(int)Stat.PhysicalDefense]);
                stats.MagicAttack = Grow(stats.MagicAttack, p[(int)Stat.MagicAttack]);
                stats.MagicDefense = Grow(stats.MagicDefense, p[(int)Stat.MagicDefense]);
                stats.Speed = Grow(stats.Speed, p[(int)Stat.Speed]);
                member.Stats = stats;
            }
        }

        // 足し合わせる項目（CharacterStats のうち、強化で上げる6つ）。
        private enum Stat
        {
            Hp,
            PhysicalAttack,
            PhysicalDefense,
            MagicAttack,
            MagicDefense,
            Speed,
        }

        /// <summary>
        /// Each slot's percent by stat (HP, physical attack, physical defense, magic attack, magic
        /// defense, speed) the boons and the training add up to.
        /// </summary>
        public static float[][] PercentsOf(
            int slots,
            IReadOnlyList<AdventureBoon> boons,
            IReadOnlyList<int> honed,
            float honeStat,
            bool strongBattle
        )
        {
            var percents = Enumerable.Range(0, slots).Select(_ => new float[6]).ToArray();
            for (int slot = 0; slot < slots && honed != null && slot < honed.Count; slot++)
            {
                percents[slot][(int)Stat.PhysicalAttack] += honed[slot] * honeStat * 100f;
                percents[slot][(int)Stat.MagicAttack] += honed[slot] * honeStat * 100f;
            }
            boons ??= Array.Empty<AdventureBoon>();
            for (int k = 0; k < boons.Count; k++)
            {
                var boon = boons[k];
                var template = boon.Template;
                if (template == null)
                    continue;
                // 強化を選ぶのは戦闘に勝ったときだけなので、あとに選んだ数が、選んでから勝った数になる。
                int winsSince = boons.Count - k - 1;
                foreach (var effect in template.Effects)
                {
                    int percent = PercentNow(effect, boon.Rank, winsSince, strongBattle);
                    if (percent == 0)
                        continue;
                    for (int slot = 0; slot < slots; slot++)
                        if (boon.Everyone || boon.Slot == slot)
                            foreach (var stat in StatsOf(effect.Stats, boon.Attack))
                                percents[slot][(int)stat] += percent;
                }
            }
            return percents;
        }

        /// <summary>The percent an effect works with now.</summary>
        public static int PercentNow(
            AdventureBoonEffect effect,
            AdventureBoonRank rank,
            int winsSince,
            bool strongBattle
        )
        {
            int percent = effect.PercentAt(rank);
            return effect.When switch
            {
                AdventureBoonWhen.StrongBattle => strongBattle ? percent : 0,
                AdventureBoonWhen.PerWin => Math.Min(percent * winsSince, effect.CapAt(rank)),
                AdventureBoonWhen.WhileTraining => winsSince < TrainingBattles ? percent : 0,
                AdventureBoonWhen.AfterTraining => winsSince >= TrainingBattles ? percent : 0,
                // 戦闘の途中で効く強化は、戦闘の前には掛けない（BoostsOf）。
                AdventureBoonWhen.HalfHp
                or AdventureBoonWhen.FirstTurns
                or AdventureBoonWhen.PerFallenAlly => 0,
                _ => percent,
            };
        }

        private static IEnumerable<Stat> StatsOf(AdventureBoonStats stats, CardStat attack)
        {
            switch (stats)
            {
                case AdventureBoonStats.Attack:
                    yield return attack == CardStat.MagicAttack
                        ? Stat.MagicAttack
                        : Stat.PhysicalAttack;
                    break;
                case AdventureBoonStats.Attacks:
                    yield return Stat.PhysicalAttack;
                    yield return Stat.MagicAttack;
                    break;
                case AdventureBoonStats.Hp:
                    yield return Stat.Hp;
                    break;
                case AdventureBoonStats.PhysicalDefense:
                    yield return Stat.PhysicalDefense;
                    break;
                case AdventureBoonStats.MagicDefense:
                    yield return Stat.MagicDefense;
                    break;
                case AdventureBoonStats.Defenses:
                    yield return Stat.PhysicalDefense;
                    yield return Stat.MagicDefense;
                    break;
                case AdventureBoonStats.Speed:
                    yield return Stat.Speed;
                    break;
            }
        }

        private static int Grow(int value, float percent) => BattleStatBoosts.Grow(value, percent);

        /// <summary>The boosts the boons give the member at <paramref name="slot"/> during a battle.</summary>
        public static IReadOnlyList<BattleStatBoost> BoostsOf(
            int slot,
            IReadOnlyList<AdventureBoon> boons
        )
        {
            var boosts = new List<BattleStatBoost>();
            foreach (var boon in boons ?? Array.Empty<AdventureBoon>())
            {
                var template = boon.Template;
                if (template == null || !(boon.Everyone || boon.Slot == slot))
                    continue;
                foreach (var effect in template.Effects)
                    if (ConditionOf(effect.When) is { } condition)
                        boosts.Add(
                            new BattleStatBoost
                            {
                                Name = template.Name,
                                Condition = condition,
                                Stats =
                                    effect.Stats == AdventureBoonStats.Speed
                                        ? BattleBoostStats.Speed
                                        : BattleBoostStats.Attacks,
                                Percent = effect.PercentAt(boon.Rank),
                                Turns = VanguardTurns,
                            }
                        );
            }
            return boosts;
        }

        private static BattleBoostCondition? ConditionOf(AdventureBoonWhen when) =>
            when switch
            {
                AdventureBoonWhen.HalfHp => BattleBoostCondition.HalfHpOrLess,
                AdventureBoonWhen.FirstTurns => BattleBoostCondition.FirstTurns,
                AdventureBoonWhen.PerFallenAlly => BattleBoostCondition.PerFallenAlly,
                _ => null,
            };

        // --- Words ---------------------------------------------------------------------------

        // 上がる幅と下がる幅の色（カードの説明の欄）。
        public const string UpColor = "#8CF28C";
        public const string DownColor = "#FF7A70";

        /// <summary>The name of the stats an effect grows, as the card shows it.</summary>
        public static string StatsName(AdventureBoonStats stats, CardStat attack) =>
            stats switch
            {
                AdventureBoonStats.Attack => attack == CardStat.MagicAttack ? "属攻" : "物攻",
                AdventureBoonStats.Attacks => "物攻・属攻",
                AdventureBoonStats.Hp => "最大HP",
                AdventureBoonStats.PhysicalDefense => "物防",
                AdventureBoonStats.MagicDefense => "属防",
                AdventureBoonStats.Defenses => "物防・属防",
                _ => "速度",
            };

        /// <summary>
        /// What the boon does, one line an effect, as the card's description shows it: the stats
        /// and the percent (green up, red down), with when it works for the ones that work at a
        /// time only.
        /// </summary>
        public static string Description(AdventureBoon boon)
        {
            var template = boon.Template;
            if (template == null)
                return "";
            var lines = new List<string>();
            foreach (var effect in template.Effects)
            {
                string stats = StatsName(effect.Stats, boon.Attack);
                int percent = effect.PercentAt(boon.Rank);
                string amount = Amount(percent);
                lines.Add(
                    effect.When switch
                    {
                        AdventureBoonWhen.StrongBattle => $"強敵との戦いで\n{stats} {amount}",
                        AdventureBoonWhen.PerWin =>
                            $"勝つたびに {stats} {amount}\n（最大 {Amount(effect.CapAt(boon.Rank))}）",
                        AdventureBoonWhen.WhileTraining =>
                            $"{TrainingBattles}戦のあいだ {stats} {amount}",
                        AdventureBoonWhen.AfterTraining => $"そのあと {stats} {amount}",
                        AdventureBoonWhen.HalfHp => $"HPが半分以下の間\n{stats} {amount}",
                        AdventureBoonWhen.FirstTurns =>
                            $"最初の{VanguardTurns}ターン\n{stats} {amount}",
                        AdventureBoonWhen.PerFallenAlly => $"倒れた仲間1人につき\n{stats} {amount}",
                        _ => $"{stats} {amount}",
                    }
                );
            }
            return string.Join("\n", lines);
        }

        private static string Amount(int percent) =>
            percent >= 0
                ? $"<color={UpColor}>+{percent}%</color>"
                : $"<color={DownColor}>{percent}%</color>";
    }
}
