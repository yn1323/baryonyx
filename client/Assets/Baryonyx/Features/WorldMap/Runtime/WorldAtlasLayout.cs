using System;
using UnityEngine;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// One map of the nested world map prototype
    /// (doc/plans/2026-10-27-world-map-rune-squares.md#2026-10-27地域の名前と細かく描いたエリアマップ):
    /// the world map with a square for each region, or a region's area map with its squares,
    /// towns and the gates to the next regions. Squares and lines are generated with the
    /// background and kept as JSON; positions are pixels of the background, from its top left.
    /// </summary>
    [Serializable]
    public sealed class WorldAtlasLayout
    {
        // 世界地図のキー。地域のキーは verde・oro など。
        public const string WorldKey = "world";

        // 世界地図の地域のマスのIDは、この接頭辞に地域のキーを付ける。
        public const string RegionPrefix = "region-";

        // エリアマップの入口のマスのIDは、この接頭辞に地域のキーを付ける。地域に1つだけ置く。
        public const string GatePrefix = "gate-";

        // 世界地図で冒険を始めるマス（マリーナ海岸の入口から北へ5マス）。
        public const string StartSquare = "world-start";

        [Serializable]
        public sealed class Node
        {
            public string id;
            public float x;
            public float y;
            public string kind;
        }

        [Serializable]
        public sealed class Line
        {
            public string a;
            public string b;
        }

        public string key;
        public string name;
        public int width;
        public int height;
        public Node[] nodes = Array.Empty<Node>();
        public Line[] lines = Array.Empty<Line>();

        public Vector2 PictureSize => new(width, height);

        public static WorldAtlasLayout Parse(string json) =>
            JsonUtility.FromJson<WorldAtlasLayout>(json);

        public WorldMapGraph Build()
        {
            var graph = new WorldMapGraph();
            foreach (var node in nodes)
                graph.Add(node.id, KindOf(node.kind), new Vector2(node.x, node.y), name);
            foreach (var line in lines)
                graph.Link(line.a, line.b);
            return graph;
        }

        public static WorldNodeKind KindOf(string kind) =>
            kind switch
            {
                "town" => WorldNodeKind.Town,
                "region" => WorldNodeKind.Region,
                "gate" => WorldNodeKind.Gate,
                _ => WorldNodeKind.Empty,
            };
    }
}
