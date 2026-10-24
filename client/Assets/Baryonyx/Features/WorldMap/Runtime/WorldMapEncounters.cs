using System.Collections.Generic;
using Baryonyx.Adventure;
using Baryonyx.Combat.Presentation;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// The enemies of a battle on the highway: always the same, set by hand for each square
    /// (doc/features/stage-progression.md#街道のマスと移動2026-10-24), with the battle background.
    /// </summary>
    public sealed class WorldMapEncounter
    {
        public WorldMapEncounter(AdventureEncounter enemies, BattleStage stage)
        {
            Enemies = enemies;
            Stage = stage;
        }

        public AdventureEncounter Enemies { get; }
        public BattleStage Stage { get; }
    }

    /// <summary>
    /// The highway's battles and gate bosses, square by square (WorldMapCatalog.Rows). The
    /// enemies are those of the dungeons near each town (AdventureCatalog.Areas), stronger the
    /// farther the square is from the first town, up to the section's recommended level. All
    /// enemies and levels are provisional.
    /// </summary>
    public static class WorldMapEncounters
    {
        private static readonly Dictionary<string, WorldMapEncounter> BySquare = new()
        {
            // ポルタのまわり（推奨Lv10〜12）。ミストラ遺跡の森の魔物。
            ["n10-10"] = Pack("n10-10", "moss-slime", "moss-wolf", 10, BattleStage.MeadowRoad),
            ["n14-10"] = Pack("n14-10", "dew-slime", "spore-cap", 10, BattleStage.MeadowRoad),
            ["n12-8"] = Pack("n12-8", "acorn-soldier", "stump-boar", 11, BattleStage.MistyWoods),
            ["n12-12"] = Pack("n12-12", "moss-slime", "thorn-serpent", 11, BattleStage.MeadowRoad),
            ["n9-11"] = Pack("n9-11", "crystal-hare", "nectar-bloom", 11, BattleStage.MeadowRoad),
            ["n15-9"] = Pack("n15-9", "dew-slime", "moss-wolf", 11, BattleStage.MistyWoods),
            ["n13-13"] = Pack("n13-13", "acorn-soldier", "spore-cap", 12, BattleStage.MistyWoods),
            ["n17-10"] = Pack("n17-10", "crystal-hare", "stump-boar", 12, BattleStage.MeadowRoad),
            // 外の輪の西（ブリーザ、推奨Lv12〜14）。
            ["n6-6"] = Pack("n6-6", "acorn-soldier", "thorn-serpent", 13, BattleStage.DuskHighland),
            ["n6-12"] = Pack("n6-12", "moss-slime", "nectar-bloom", 13, BattleStage.DuskHighland),
            // 外の輪の東（推奨Lv14）。港町ポルトリアの海の魔物。
            ["n18-6"] = Pack("n18-6", "ghost-jelly", "barnacle-crab", 14, BattleStage.RockyShore),
            ["n18-14"] = Pack(
                "n18-14",
                "lost-signpost",
                "pumpkin-scarecrow",
                14,
                BattleStage.MeadowRoad
            ),
            // 北の輪（ピーノ、推奨Lv16）。グラシエラ氷窟の洞窟の魔物。
            ["n10-4"] = Pack("n10-4", "big-eared-bat", "ore-mole", 16, BattleStage.MineTunnel),
            ["n16-4"] = Pack("n16-4", "glow-worm", "crystal-hermit", 16, BattleStage.MineTunnel),
            // 南の道（サッビア、推奨Lv20〜24）。アルマジャ王墓の砂の魔物。
            ["n8-16"] = Pack(
                "n8-16",
                "cactus-warrior",
                "sand-scorpion",
                20,
                BattleStage.DuskHighland
            ),
            ["n16-16"] = Pack(
                "n16-16",
                "hourglass-sprite",
                "wrapped-jackal",
                22,
                BattleStage.DuskHighland
            ),
            // カンパネラの先（推奨Lv30）。月影の墓所の魔物。
            ["n24-4"] = Pack(
                "n24-4",
                "cursed-candelabra",
                "grave-raven",
                30,
                BattleStage.MoonlitGraveyard
            ),
            // フォルジャへの道（推奨Lv35）。ヴォルガン火山の魔物。
            ["n24-8"] = Pack("n24-8", "lava-slime", "fire-lizard", 35, BattleStage.VolcanoCrater),
            // フレッダからアルバへ（推奨Lv38）。白嶺ネーヴェの雪の魔物。
            ["n11-1"] = Pack("n11-1", "snowball", "icicle-fox", 38, BattleStage.SnowField),
            // 関所のボス。倒すと、その先の街への道が開く。
            ["n4-8"] = Boss(
                "n4-8",
                "ghost-jelly",
                "skeleton-pirate",
                "giant-octopus",
                14,
                BattleStage.RockyShore
            ),
            ["n13-3"] = Boss(
                "n13-3",
                "ice-penguin",
                "icicle-fox",
                "rock-troll",
                18,
                BattleStage.CrystalCavern
            ),
            ["n5-14"] = Boss(
                "n5-14",
                "blood-leech",
                "frog-king",
                "bone-croc",
                22,
                BattleStage.MistySwamp
            ),
            ["n22-16"] = Boss(
                "n22-16",
                "cracked-urn",
                "sand-scorpion",
                "sand-shark",
                26,
                BattleStage.CastleHall
            ),
            ["n20-4"] = Boss(
                "n20-4",
                "will-o-wisp",
                "skeleton-soldier",
                "lich",
                30,
                BattleStage.MoonlitGraveyard
            ),
            ["n27-6"] = Boss(
                "n27-6",
                "lava-slime",
                "flame-tail-bird",
                "obsidian-colossus",
                35,
                BattleStage.VolcanoCrater
            ),
            ["n26-8"] = Boss(
                "n26-8",
                "big-eared-bat",
                "fire-lizard",
                "obsidian-colossus",
                35,
                BattleStage.VolcanoCrater
            ),
            ["n7-1"] = Boss(
                "n7-1",
                "ice-penguin",
                "crystal-hermit",
                "frost-ape",
                40,
                BattleStage.SnowField
            ),
        };

        /// <summary>The battle of the square, or null when the square has none.</summary>
        public static WorldMapEncounter Of(string squareId) =>
            squareId != null && BySquare.TryGetValue(squareId, out var encounter)
                ? encounter
                : null;

        public static IEnumerable<string> Squares => BySquare.Keys;

        // 小さな敵と中くらいの敵の2体。強さはダンジョンの魔物と同じ倍率にする。
        private static WorldMapEncounter Pack(
            string square,
            string small,
            string medium,
            int level,
            BattleStage stage
        ) =>
            new(
                new AdventureEncounter(
                    "world/" + square,
                    new[] { small, medium, null },
                    level,
                    AdventureCatalog.PackHpScale,
                    AdventureCatalog.PackPowerScale
                ),
                stage
            );

        // 小さな敵と中くらいの敵を連れた主。強さはダンジョンの最奥の主と同じ倍率にする。
        private static WorldMapEncounter Boss(
            string square,
            string small,
            string medium,
            string master,
            int level,
            BattleStage stage
        ) =>
            new(
                new AdventureEncounter(
                    "world/" + square,
                    new[] { small, medium, master },
                    level,
                    AdventureCatalog.BossHpScale,
                    AdventureCatalog.BossPowerScale
                ),
                stage
            );
    }
}
