using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// Reads a world map drawn as a grid of characters like a sugoroku board: an upper-case
    /// letter is a town (<see cref="WorldMapCatalog.Towns"/>), a lower-case one a square of a
    /// kind, and two squares in the same row or column are linked when they touch or when only
    /// <c>-</c> (across) or <c>|</c> (down) lies between them, so every line is straight.
    /// </summary>
    public sealed class WorldMapGrid
    {
        private readonly string[] rows;
        private readonly float cell;
        private readonly Vector2 origin;
        private readonly string prefix;
        private readonly IReadOnlyDictionary<char, WorldNodeKind> kinds;

        /// <param name="cell">The size of a grid cell on the picture (pixels).</param>
        /// <param name="origin">The centre of the top-left cell on the picture (pixels).</param>
        /// <param name="prefix">What a square's ID starts with, before its column and row.</param>
        public WorldMapGrid(
            string[] rows,
            float cell,
            Vector2 origin,
            string prefix,
            IReadOnlyDictionary<char, WorldNodeKind> kinds
        )
        {
            this.rows = rows;
            this.cell = cell;
            this.origin = origin;
            this.prefix = prefix;
            this.kinds = kinds;
        }

        public Vector2 PointOf(Vector2 at) => origin + at * cell;

        public string IdAt(int column, int row)
        {
            char c = rows[row][column];
            return WorldMapCatalog.Towns.TryGetValue(c, out var town)
                ? town.Id
                : $"{prefix}{column}-{row}";
        }

        public WorldMapGraph Build(IEnumerable<(int Level, Vector2 Cell)> levels)
        {
            var graph = new WorldMapGraph();
            for (int row = 0; row < rows.Length; row++)
            for (int column = 0; column < rows[row].Length; column++)
            {
                char c = rows[row][column];
                var point = PointOf(new Vector2(column, row));
                if (WorldMapCatalog.Towns.TryGetValue(c, out var town))
                    graph.Add(town.Id, WorldNodeKind.Town, point, town.Name);
                else if (kinds.TryGetValue(c, out var kind))
                    graph.Add(IdAt(column, row), kind, point, "街道");
            }
            for (int row = 0; row < rows.Length; row++)
            for (int column = 0; column < rows[row].Length; column++)
            {
                if (!IsSquare(column, row))
                    continue;
                LinkAlong(graph, column, row, 1, 0, '-');
                LinkAlong(graph, column, row, 0, 1, '|');
            }
            foreach (var (level, at) in levels)
                graph.AddLevel(level, PointOf(at));
            return graph;
        }

        private bool IsSquare(int column, int row) =>
            row >= 0
            && row < rows.Length
            && column >= 0
            && column < rows[row].Length
            && (
                WorldMapCatalog.Towns.ContainsKey(rows[row][column])
                || kinds.ContainsKey(rows[row][column])
            );

        // 右か下へ、道の文字をたどって次のマスまで結ぶ。
        private void LinkAlong(WorldMapGraph graph, int column, int row, int dx, int dy, char road)
        {
            int x = column + dx;
            int y = row + dy;
            while (y < rows.Length && x < rows[y].Length && rows[y][x] == road)
            {
                x += dx;
                y += dy;
            }
            if (IsSquare(x, y))
                graph.Link(IdAt(column, row), IdAt(x, y));
        }
    }
}
