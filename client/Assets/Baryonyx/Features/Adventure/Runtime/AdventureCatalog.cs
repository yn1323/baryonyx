using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat.Presentation;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The enemies a battle of the adventure is fought with: the kind of enemy in each place of
    /// the battle screen (by place in BattleInspectView.Enemies: 0 the small one in front, 1 the
    /// middle-sized one, 2 the large one at the back; null for an empty place), the level they
    /// show up at and how much their HP and their attacks are scaled.
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

        /// <summary>The small enemies (64 dots), standing in front.</summary>
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

        // 戦闘画面の敵の並び（BattleInspectView.Enemies）。小さな敵が手前、大きな敵が奥に立つ。
        public const int SmallPlace = 0;
        public const int MediumPlace = 1;
        public const int LargePlace = 2;

        // 倍率は、実際の戦闘画面での自動対戦で決めた仮の値（doc/features/stage-progression.md の
        // 戦闘の試し）。2026-10-22に敵が毎ターン動くようにしたため、決め直した。
        // 2026-10-24に、スキル・装備を育てる前提にするため、HPを2〜3割、攻撃を1割ほど上げた（試しは未実施）。
        // 魔物の気配は、行き先のLvのまま、HPと攻撃を少し増やす。
        public const float PackHpScale = 1.7f;
        public const float PackPowerScale = 2.0f;

        // 強敵の部屋は、同じ行き先の敵のLvを上げ、HPと攻撃を増やして出す。
        public const int EliteLevelUp = 8;
        public const float EliteHpScale = 4.0f;
        public const float ElitePowerScale = 3.3f;

        // 最奥の主は、道中より少し高いLvで、HPと攻撃を増やして待つ。
        public const int BossLevelUp = 2;

        // 怪しい宝箱のミミック（doc/catalog/enemies/enemy-chest-mimic.md）。
        public const string MimicId = "chest-mimic";
        public const float BossHpScale = 2.3f;
        public const float BossPowerScale = 1.8f;

        /// <summary>
        /// The destinations, from the nearest to the deepest (2026-10-20). Each has the enemies of
        /// its own land (doc/catalog/enemies, where each lives), and every enemy kind shows up
        /// somewhere.
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
                Small = new[] { "moss-slime", "dew-slime", "acorn-soldier", "crystal-hare" },
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
                Small = new[] { "ghost-jelly", "coin-pouch", "lost-signpost" },
                Medium = new[] { "barnacle-crab", "skeleton-pirate", "pumpkin-scarecrow" },
                Bosses = new[] { "giant-octopus" },
            },
            new AdventureArea
            {
                Id = "glaciera-cave",
                Name = "グラシエラ氷窟",
                Stage = BattleStage.CrystalCavern,
                RoomCount = 9,
                Level = 18,
                Small = new[] { "big-eared-bat", "glow-worm", "ice-penguin" },
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
                Small = new[] { "blood-leech", "will-o-wisp", "dew-slime" },
                Medium = new[] { "frog-king", "mud-golem", "spore-cap" },
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
                Small = new[] { "cursed-candelabra", "will-o-wisp", "floating-grimoire" },
                Medium = new[]
                {
                    "skeleton-soldier",
                    "grave-raven",
                    "hollow-armor",
                    "pumpkin-scarecrow",
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
                Small = new[] { "lava-slime", "will-o-wisp", "big-eared-bat" },
                Medium = new[] { "fire-lizard", "flame-tail-bird", "ore-mole" },
                Bosses = new[] { "obsidian-colossus" },
            },
            new AdventureArea
            {
                Id = "neve-peaks",
                Name = "白嶺ネーヴェ",
                Stage = BattleStage.SnowField,
                RoomCount = 12,
                Level = 40,
                Small = new[] { "snowball", "ice-penguin", "crystal-hare" },
                Medium = new[] { "icicle-fox", "crystal-hermit", "stump-boar" },
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
            int variant = 0
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
            return what == "" ? "" : $"{destinationId}/{what}/{Math.Max(0, variant)}";
        }

        /// <summary>
        /// The adventure's route, made from the seed the server chose. Each room's enemies are
        /// picked from the seed and the room, so the same adventure meets the same enemies on
        /// every device, and another adventure meets others.
        /// </summary>
        public static AdventureRouteMap Route(string destinationId, int seed, int roomCount) =>
            AdventureRouteMap.Generate(
                seed,
                roomCount > 0 ? roomCount : RoomCount(destinationId),
                (kind, roomId) => EncounterOf(destinationId, kind, Variant(seed, roomId))
            );

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
        /// The enemies of an encounter ("port-portoria/pack/17"): a pack has a small and a
        /// middle-sized enemy of the destination, a strong enemy's room the same at a higher level
        /// and with more HP, and the deepest room adds the destination's master behind them. An
        /// unknown encounter is the first destination's pack.
        /// </summary>
        public static AdventureEncounter Encounter(string id)
        {
            var parts = (id ?? "").Split('/');
            var area = parts.Length == 3 ? Area(parts[0]) : null;
            if (area == null || !int.TryParse(parts[2], out int variant) || variant < 0)
                return Encounter(EncounterOf(ForestRuins, AdventureRoomKind.Battle));
            string small = Pick(area.Small, variant, 1);
            string medium = Pick(area.Medium, variant, area.Small.Length);
            return parts[1] switch
            {
                "elite" => new AdventureEncounter(
                    id,
                    new[] { small, medium, null },
                    area.Level + EliteLevelUp,
                    EliteHpScale,
                    ElitePowerScale
                ),
                // 宝箱ミミックに、行き先の小さな敵1体を加え、強い魔物と同じ強さにする。
                "mimic" => new AdventureEncounter(
                    id,
                    new[] { small, MimicId, null },
                    area.Level + EliteLevelUp,
                    EliteHpScale,
                    ElitePowerScale
                ),
                "boss" => new AdventureEncounter(
                    id,
                    new[]
                    {
                        small,
                        medium,
                        Pick(area.Bosses, variant, area.Small.Length * area.Medium.Length),
                    },
                    area.Level + BossLevelUp,
                    BossHpScale,
                    BossPowerScale
                ),
                _ => new AdventureEncounter(
                    id,
                    new[] { small, medium, null },
                    area.Level,
                    PackHpScale,
                    PackPowerScale
                ),
            };
        }

        // 組み合わせの番号から、敵の一覧の1体を選ぶ。小・中・主の順に、番号の桁をずらして使う。
        private static string Pick(string[] kinds, int variant, int stride) =>
            kinds.Length == 0 ? null : kinds[variant / Math.Max(1, stride) % kinds.Length];

        // 手に入れた物の下に添える一言。落ちた物は、いつも持ち物に加わる。
        public const string AddedNote = "持ち物に加えた";

        public static string StatusTitle(AdventureEndStatus status) =>
            status switch
            {
                AdventureEndStatus.Cleared => "踏破！",
                AdventureEndStatus.Defeated => "冒険の終わり",
                _ => "帰還",
            };
    }
}
