using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The enemies a battle of the adventure is fought with: the kind of enemy in each place of
    /// the battle screen (by place in BattleInspectView.Enemies: 0 in front, 1 in the middle, 2 at
    /// the back, where the master stands; null for an empty place), the level they show up at
    /// and how much their HP and their attacks are scaled.
    /// </summary>
    public sealed class AdventureEncounter
    {
        public AdventureEncounter(
            string id,
            string[] kinds,
            int level,
            float hpScale,
            float powerScale = 1f
        )
        {
            Id = id;
            Kinds = kinds ?? Array.Empty<string>();
            Level = level;
            HpScale = hpScale;
            PowerScale = powerScale;
        }

        public string Id { get; }

        /// <summary>The kind of enemy in each place of the battle screen, null where none stands.</summary>
        public IReadOnlyList<string> Kinds { get; }

        public int Level { get; }
        public float HpScale { get; }
        public float PowerScale { get; }

        /// <summary>The places of the battle screen that have an enemy.</summary>
        public int[] Enemies =>
            Enumerable.Range(0, Kinds.Count).Where(i => !string.IsNullOrEmpty(Kinds[i])).ToArray();
    }

    /// <summary>
    /// How the enemies of a battle stand (doc/features/stage-progression.md の戦闘の編成): the
    /// size of the enemy in each place of the battle screen (null for an empty place), how much
    /// their HP and attacks are scaled against a small and a middle-sized enemy, and how often it
    /// comes on the way's first floor and on its last.
    /// </summary>
    public sealed class AdventureFormation
    {
        public AdventureFormation(
            EnemyBody?[] places,
            float hpScale,
            float powerScale,
            int shallowWeight,
            int deepWeight
        )
        {
            Places = places;
            HpScale = hpScale;
            PowerScale = powerScale;
            ShallowWeight = shallowWeight;
            DeepWeight = deepWeight;
        }

        public IReadOnlyList<EnemyBody?> Places { get; }
        public float HpScale { get; }
        public float PowerScale { get; }
        public int ShallowWeight { get; }
        public int DeepWeight { get; }

        /// <summary>How often it comes at <paramref name="depth"/> (0 the way's first floor, 1 its last).</summary>
        public float WeightAt(float depth) => ShallowWeight + (DeepWeight - ShallowWeight) * depth;

        /// <summary>True when <paramref name="kinds"/> stand as this formation, by place and size.</summary>
        public bool Fits(IReadOnlyList<string> kinds) =>
            kinds != null
            && kinds.Count == Places.Count
            && Places
                .Select(
                    (body, i) =>
                        string.IsNullOrEmpty(kinds[i])
                            ? body == null
                            : body == CombatEnemyStats.Find(kinds[i])?.Body
                )
                .All(fits => fits);
    }

    /// <summary>
    /// One destination of the adventure: its name, its battle background, the rooms between the
    /// entrance and the deepest room (the same as the server's), the level its enemies show up
    /// at and the enemies that live there, by size (doc/features/stage-progression.md).
    /// </summary>
    public sealed class AdventureArea
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public BattleStage Stage { get; set; }
        public int RoomCount { get; set; } = 8;

        /// <summary>The level of the enemies on the way; a strong enemy's room is higher.</summary>
        public int Level { get; set; } = 10;

        /// <summary>The small enemies (64 dots).</summary>
        public string[] Small { get; set; } = Array.Empty<string>();

        /// <summary>The middle-sized enemies (96 dots).</summary>
        public string[] Medium { get; set; } = Array.Empty<string>();

        /// <summary>The masters of the deepest room (128 dots); one of them waits there.</summary>
        public string[] Bosses { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// What the client shows of the server's adventure: the destinations' names, places and
    /// enemies, the floor names, the hints at the rooms and the enemies of each encounter, and
    /// the route made from the seed and the room count the server keeps
    /// (server/src/features/adventure/catalog.ts).
    /// </summary>
    public static class AdventureCatalog
    {
        public const string ForestRuins = AdventureLocalSource.ForestRuins;

        // 戦闘画面の敵の立ち位置（BattleInspectView.Enemies）。だれが立つかは編成が決め、最奥の主は奥に立つ。
        public const int FrontPlace = 0;
        public const int MiddlePlace = 1;
        public const int BackPlace = 2;

        // 倍率は、実際の戦闘画面での自動対戦で決めた仮の値（doc/features/stage-progression.md の
        // 戦闘の試し）。2026-10-22に敵が毎ターン動くようにしたため、決め直した。
        // 2026-10-24に、スキル・装備を育てる前提にするため、HPを2〜3割、攻撃を1割ほど上げた（試しは未実施）。
        // 魔物の気配は、行き先のLvのまま、HPと攻撃を少し増やす。
        public const float PackHpScale = 1.7f;
        public const float PackPowerScale = 2.0f;

        // 強敵の部屋は、同じ行き先の敵のLvを上げ、HPと攻撃を増やして出す。
        public const int EliteLevelUp = 5;
        public const float EliteHpScale = 4.6f;
        public const float ElitePowerScale = 2.7f;

        // 最奥の主は、道中より少し高いLvで、HPと攻撃を増やして待つ。
        public const int BossLevelUp = 2;

        // 怪しい宝箱のミミック（doc/catalog/enemies/enemy-chest-mimic.md）。
        public const string MimicId = "chest-mimic";

        // 道中の敵は、奥の階ほど少しずつ強くする（doc/features/stage-progression.md の行き先と敵）。
        // 道中の最初の階でHPと攻撃を0.85倍、最奥の直前の階で1.15倍にし、そのあいだは階ごとに等しく上げる。
        // 平均は1倍のままで、最奥の主には掛けない。
        public const int FirstDepthPercent = 85;
        public const int LastDepthPercent = 115;

        public const float BossHpScale = 2.3f;
        public const float BossPowerScale = 1.8f;

        private const EnemyBody Small = EnemyBody.Small;
        private const EnemyBody Medium = EnemyBody.Medium;

        // 魔物の気配の編成（doc/features/stage-progression.md の戦闘の編成）。小さな敵1体と中くらいの敵1体を
        // 基準に、敵の合計のHPと1ターンの攻撃がおおよそ同じになるよう倍率を掛ける（小さな敵の平均はHP約3100・
        // 攻撃約830、中くらいの敵はHP約6600・攻撃約1220）。浅い階は少ない敵、奥の階は多い敵がよく出る。
        public static readonly IReadOnlyList<AdventureFormation> PackFormations = new[]
        {
            // 中くらいの敵1体。
            new AdventureFormation(new EnemyBody?[] { null, Medium, null }, 1.4f, 1.4f, 3, 1),
            new AdventureFormation(new EnemyBody?[] { Small, Medium, null }, 1f, 1f, 4, 3),
            new AdventureFormation(new EnemyBody?[] { Small, Small, null }, 1.4f, 1.15f, 3, 1),
            new AdventureFormation(new EnemyBody?[] { Small, Small, Medium }, 0.8f, 0.75f, 1, 3),
        };

        // 強い魔物の気配の編成。中くらいの敵を2体にして、道中の編成と見分けられるようにする。
        // 倍率の基準は魔物の気配と同じ。
        public static readonly IReadOnlyList<AdventureFormation> EliteFormations = new[]
        {
            new AdventureFormation(new EnemyBody?[] { null, Medium, Medium }, 0.75f, 0.85f, 1, 1),
            new AdventureFormation(new EnemyBody?[] { Small, Medium, Medium }, 0.6f, 0.65f, 1, 1),
        };

        /// <summary>
        /// The destinations, from the nearest to the deepest (2026-10-20). Each has the enemies of
        /// its own land (doc/catalog/enemies, where each lives) and some of its neighbors', and
        /// every enemy kind shows up somewhere.
        /// </summary>
        public static readonly IReadOnlyList<AdventureArea> Areas = new[]
        {
            new AdventureArea
            {
                Id = ForestRuins,
                Name = "ミストラ遺跡",
                Stage = BattleStage.MistyWoods,
                RoomCount = 8,
                Level = 10,
                Small = new[]
                {
                    "moss-slime",
                    "dew-slime",
                    "acorn-soldier",
                    "crystal-hare",
                    "lost-signpost",
                },
                Medium = new[]
                {
                    "moss-wolf",
                    "spore-cap",
                    "thorn-serpent",
                    "stump-boar",
                    "nectar-bloom",
                },
                Bosses = new[] { "forest-guardian" },
            },
            new AdventureArea
            {
                Id = "port-portoria",
                Name = "港町ポルトリア",
                Stage = BattleStage.RockyShore,
                RoomCount = 9,
                Level = 14,
                Small = new[]
                {
                    "ghost-jelly",
                    "coin-pouch",
                    "lost-signpost",
                    "will-o-wisp",
                    "big-eared-bat",
                },
                Medium = new[]
                {
                    "barnacle-crab",
                    "skeleton-pirate",
                    "pumpkin-scarecrow",
                    "crystal-hermit",
                    "blind-fishman",
                },
                Bosses = new[] { "giant-octopus" },
            },
            new AdventureArea
            {
                Id = "glaciera-cave",
                Name = "グラシエラ氷窟",
                Stage = BattleStage.CrystalCavern,
                RoomCount = 9,
                Level = 18,
                Small = new[]
                {
                    "big-eared-bat",
                    "glow-worm",
                    "ice-penguin",
                    "snowball",
                    "crystal-hare",
                },
                Medium = new[] { "crystal-hermit", "ore-mole", "blind-fishman", "icicle-fox" },
                Bosses = new[] { "rock-troll" },
            },
            new AdventureArea
            {
                Id = "hush-mire",
                Name = "しじまの沼",
                Stage = BattleStage.MistySwamp,
                RoomCount = 10,
                Level = 22,
                Small = new[]
                {
                    "blood-leech",
                    "will-o-wisp",
                    "dew-slime",
                    "moss-slime",
                    "glow-worm",
                },
                Medium = new[]
                {
                    "frog-king",
                    "mud-golem",
                    "spore-cap",
                    "thorn-serpent",
                    "nectar-bloom",
                },
                Bosses = new[] { "bone-croc" },
            },
            new AdventureArea
            {
                Id = "almaja-tomb",
                Name = "アルマジャ王墓",
                Stage = BattleStage.CastleHall,
                RoomCount = 10,
                Level = 26,
                Small = new[]
                {
                    "cracked-urn",
                    "floating-grimoire",
                    "hourglass-sprite",
                    "cactus-warrior",
                    "cursed-candelabra",
                },
                Medium = new[]
                {
                    "hollow-armor",
                    "stone-gargoyle",
                    "chest-mimic",
                    "wrapped-jackal",
                    "sand-scorpion",
                },
                Bosses = new[] { "brass-warden", "sand-shark" },
            },
            new AdventureArea
            {
                Id = "moonshade-graveyard",
                Name = "月影の墓所",
                Stage = BattleStage.MoonlitGraveyard,
                RoomCount = 11,
                Level = 30,
                Small = new[]
                {
                    "cursed-candelabra",
                    "will-o-wisp",
                    "floating-grimoire",
                    "ghost-jelly",
                    "cracked-urn",
                },
                Medium = new[]
                {
                    "skeleton-soldier",
                    "grave-raven",
                    "hollow-armor",
                    "pumpkin-scarecrow",
                    "skeleton-pirate",
                },
                Bosses = new[] { "lich" },
            },
            new AdventureArea
            {
                Id = "volgan-volcano",
                Name = "ヴォルガン火山",
                Stage = BattleStage.VolcanoCrater,
                RoomCount = 11,
                Level = 35,
                Small = new[]
                {
                    "lava-slime",
                    "will-o-wisp",
                    "big-eared-bat",
                    "cactus-warrior",
                    "glow-worm",
                },
                Medium = new[]
                {
                    "fire-lizard",
                    "flame-tail-bird",
                    "ore-mole",
                    "stone-gargoyle",
                    "sand-scorpion",
                },
                Bosses = new[] { "obsidian-colossus" },
            },
            new AdventureArea
            {
                Id = "neve-peaks",
                Name = "白嶺ネーヴェ",
                Stage = BattleStage.SnowField,
                RoomCount = 12,
                Level = 40,
                Small = new[]
                {
                    "snowball",
                    "ice-penguin",
                    "crystal-hare",
                    "will-o-wisp",
                    "lost-signpost",
                },
                Medium = new[]
                {
                    "icicle-fox",
                    "crystal-hermit",
                    "stump-boar",
                    "moss-wolf",
                    "grave-raven",
                },
                Bosses = new[] { "frost-ape", "snow-mammoth", "mist-dragon" },
            },
        };

        public static AdventureArea Area(string destinationId) =>
            Areas.FirstOrDefault(area => area.Id == destinationId);

        public static string DestinationName(string destinationId) =>
            Area(destinationId)?.Name ?? "冒険先";

        // 探索と戦闘の背景。行き先ごとに戦闘の背景から選ぶ。
        public static BattleStage StageOf(string destinationId) =>
            Area(destinationId)?.Stage ?? BattleStage.MeadowRoad;

        public static string FloorText(int floor) => $"第{Math.Max(1, floor)}層";

        // 入口と最奥の間のあいだに通る部屋の数。サーバーの設定値と同じ（アプリの中だけの冒険で使う）。
        public static int RoomCount(string destinationId) => Area(destinationId)?.RoomCount ?? 8;

        /// <summary>
        /// The encounter of a room of <paramref name="kind"/>: its destination, what it holds and
        /// which of the destination's enemies, picked by <paramref name="variant"/>
        /// ("port-portoria/pack/17"). Empty for a room without a battle.
        /// </summary>
        public static string EncounterOf(
            string destinationId,
            AdventureRoomKind kind,
            int variant = 0,
            int depthPercent = 100
        )
        {
            string what = kind switch
            {
                AdventureRoomKind.Battle => "pack",
                // 試練の敵は、強い魔物と同じ強さにする（doc/features/stage-progression.md のマスの中身）。
                AdventureRoomKind.Elite or AdventureRoomKind.Trial => "elite",
                AdventureRoomKind.Boss => "boss",
                // 開けた怪しい宝箱がミミックだったときに戦う。
                AdventureRoomKind.ShadyChest => "mimic",
                _ => "",
            };
            if (what == "")
                return "";
            string id = $"{destinationId}/{what}/{Math.Max(0, variant)}";
            return what == "boss" || depthPercent == 100 ? id : $"{id}/{depthPercent}";
        }

        /// <summary>
        /// How strong the enemies of a room on the way are, in percent: <see cref="FirstDepthPercent"/>
        /// on the way's first floor up to <see cref="LastDepthPercent"/> on the last, rising evenly.
        /// 100 for a room that is not on the way.
        /// </summary>
        public static int DepthPercentOf(string roomId, int roomCount)
        {
            int floor = AdventureRouteMap.FloorOf(roomId);
            if (floor < 2 || floor > roomCount + 1)
                return 100;
            if (roomCount <= 1)
                return 100;
            float t = (floor - 2) / (float)(roomCount - 1);
            return (int)Math.Round(FirstDepthPercent + (LastDepthPercent - FirstDepthPercent) * t);
        }

        /// <summary>
        /// The adventure's route, made from the seed the server chose. Each room's enemies are
        /// picked from the seed and the room, so the same adventure meets the same enemies on
        /// every device, and another adventure meets others.
        /// </summary>
        public static AdventureRouteMap Route(string destinationId, int seed, int roomCount)
        {
            int count = roomCount > 0 ? roomCount : RoomCount(destinationId);
            return AdventureRouteMap.Generate(
                seed,
                count,
                (kind, roomId) =>
                    EncounterOf(
                        destinationId,
                        kind,
                        Variant(seed, roomId),
                        DepthPercentOf(roomId, count)
                    )
            );
        }

        // 種と部屋のIDから、0〜9999の組み合わせの番号を決める（どの端末でも同じになる計算）。
        private static int Variant(int seed, string roomId)
        {
            unchecked
            {
                uint hash = 2166136261u ^ (uint)seed;
                foreach (char c in roomId ?? "")
                    hash = (hash ^ c) * 16777619u;
                hash ^= hash >> 13;
                hash *= 0x5bd1e995u;
                hash ^= hash >> 15;
                return (int)(hash % 10000u);
            }
        }

        public static string Location(AdventureRun run) =>
            run == null ? "" : $"{DestinationName(run.DestinationId)} {FloorText(run.Floor)}";

        /// <summary>
        /// The enemies of an encounter ("port-portoria/pack/17", or "port-portoria/pack/17/92"
        /// with the room's depth in percent). The number picks, as the seed of a small random
        /// number generator, a formation (<see cref="PackFormations"/>, <see cref="EliteFormations"/>)
        /// and the destination's enemies standing in it, all of different kinds. The deeper the
        /// room, the more often a formation of many enemies comes, and the depth scales the HP
        /// and the attack. The shady chest's mimic stands with a small enemy, and the deepest room
        /// has a small and a middle-sized enemy before the destination's master. An unknown
        /// encounter is the first destination's pack.
        /// </summary>
        public static AdventureEncounter Encounter(string id)
        {
            var parts = (id ?? "").Split('/');
            var area = parts.Length is 3 or 4 ? Area(parts[0]) : null;
            if (area == null || !int.TryParse(parts[2], out int variant) || variant < 0)
                return Encounter(EncounterOf(ForestRuins, AdventureRoomKind.Battle));
            int percent =
                parts.Length == 4 && int.TryParse(parts[3], out int given) && given > 0
                    ? given
                    : 100;
            float depth = percent / 100f;
            var random = new RouteRandom(variant);
            var taken = new List<string>();
            string Take(string[] kinds) => TakeOne(kinds, taken, random);
            switch (parts[1])
            {
                case "elite":
                {
                    var formation = Choose(EliteFormations, DepthOf(percent), random);
                    return new AdventureEncounter(
                        id,
                        Fill(formation, area, Take),
                        area.Level + EliteLevelUp,
                        EliteHpScale * formation.HpScale * depth,
                        ElitePowerScale * formation.PowerScale * depth
                    );
                }
                // 宝箱ミミックに、行き先の小さな敵1体を加え、強い魔物と同じ強さにする。
                case "mimic":
                    return new AdventureEncounter(
                        id,
                        new[] { Take(area.Small), MimicId, null },
                        area.Level + EliteLevelUp,
                        EliteHpScale * depth,
                        ElitePowerScale * depth
                    );
                case "boss":
                    return new AdventureEncounter(
                        id,
                        new[] { Take(area.Small), Take(area.Medium), Take(area.Bosses) },
                        area.Level + BossLevelUp,
                        BossHpScale,
                        BossPowerScale
                    );
                default:
                {
                    var formation = Choose(PackFormations, DepthOf(percent), random);
                    return new AdventureEncounter(
                        id,
                        Fill(formation, area, Take),
                        area.Level,
                        PackHpScale * formation.HpScale * depth,
                        PackPowerScale * formation.PowerScale * depth
                    );
                }
            }
        }

        /// <summary>
        /// The pack formation <paramref name="kinds"/> stand as, by place and size; the formation
        /// of a small and a middle-sized enemy when none fits.
        /// </summary>
        public static AdventureFormation PackFormationOf(IReadOnlyList<string> kinds) =>
            PackFormations.FirstOrDefault(formation => formation.Fits(kinds)) ?? PackFormations[1];

        // 道中の深さ（%）を、最初の階の0から最奥の直前の階の1までにする。道中でない部屋は真ん中。
        private static float DepthOf(int percent) =>
            Math.Clamp(
                (percent - FirstDepthPercent) / (float)(LastDepthPercent - FirstDepthPercent),
                0f,
                1f
            );

        // 深さに応じた重みで、編成を1つ選ぶ。
        private static AdventureFormation Choose(
            IReadOnlyList<AdventureFormation> formations,
            float depth,
            RouteRandom random
        )
        {
            double roll = random.Next() * formations.Sum(formation => formation.WeightAt(depth));
            foreach (var formation in formations)
            {
                roll -= formation.WeightAt(depth);
                if (roll < 0)
                    return formation;
            }
            return formations[formations.Count - 1];
        }

        // 編成の立ち位置に、その大きさの行き先の敵を入れる。
        private static string[] Fill(
            AdventureFormation formation,
            AdventureArea area,
            Func<string[], string> take
        ) =>
            formation
                .Places.Select(body =>
                    body switch
                    {
                        Small => take(area.Small),
                        Medium => take(area.Medium),
                        _ => null,
                    }
                )
                .ToArray();

        // 敵の一覧から、同じ戦闘にまだいない種類を1体選ぶ。みな出ていれば、一覧のどれでもよい。
        private static string TakeOne(string[] kinds, List<string> taken, RouteRandom random)
        {
            if (kinds.Length == 0)
                return null;
            var left = kinds.Where(kind => !taken.Contains(kind)).ToArray();
            var from = left.Length > 0 ? left : kinds;
            string kind = from[random.Range(0, from.Length)];
            taken.Add(kind);
            return kind;
        }
    }
}
