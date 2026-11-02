using Baryonyx.Adventure;
using Baryonyx.Combat.Presentation;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// The battle of the nested world map waiting for the battle scene
    /// (doc/features/world-atlas/square-events.md#戦闘の設定): the map and square it was fought from, and the
    /// enemies drawn from the square's settings when "たたかう" was pressed, so fighting again
    /// meets the same ones. It is kept only while the app runs.
    /// </summary>
    public sealed class WorldAtlasBattle
    {
        private WorldAtlasBattle(string map, string square, WorldMapEncounter encounter)
        {
            Map = map;
            Square = square;
            Encounter = encounter;
        }

        public string Map { get; }
        public string Square { get; }
        public WorldMapEncounter Encounter { get; }

        /// <summary>The battle the battle scene fights next, or null.</summary>
        public static WorldAtlasBattle Pending { get; set; }

        /// <summary>
        /// Draws the battle of <paramref name="node"/> on a map of <paramref name="mapLevel"/>,
        /// or null when the square's words cannot be read.
        /// </summary>
        public static WorldAtlasBattle Of(
            string map,
            WorldAtlasLayout.Node node,
            int mapLevel,
            int seed
        )
        {
            int level = node.enemyLevel > 0 ? node.enemyLevel : mapLevel;
            AdventureEncounter enemies = WorldAtlasEncounters.Draw(
                $"atlas/{map}/{node.id}",
                node.enemies,
                level,
                WorldAtlasEncounters.StrengthOf(node.strength),
                seed
            );
            if (enemies == null)
                return null;
            BattleStage stage = WorldAtlasEncounters.StageNear(level);
            return new WorldAtlasBattle(map, node.id, new WorldMapEncounter(enemies, stage));
        }
    }
}
