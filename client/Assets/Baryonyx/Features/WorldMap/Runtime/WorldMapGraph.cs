using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.WorldMap
{
    /// <summary>How far a square is from the party's reach.</summary>
    public enum WorldNodeState
    {
        // クリアした。上を自由に通れる。
        Cleared,

        // クリアしたマスの隣。次に挑める。
        Open,

        // まだ行けないが、関所の手前なので種類は見える。
        Seen,

        // 倒していない関所の先。種類を伏せる。
        Hidden,
    }

    /// <summary>
    /// The squares of the world map and the straight lines between them
    /// (doc/features/stage-progression.md#街道のマスと移動2026-10-24). Cleared squares stay cleared,
    /// a square next to one can be taken on next, and an unbeaten boss is a gate that hides what
    /// lies behind it. The party walks the shortest way over cleared squares.
    /// </summary>
    public sealed class WorldMapGraph
    {
        public readonly struct Node
        {
            public Node(string id, WorldNodeKind kind, Vector2 point, string name)
            {
                Id = id;
                Kind = kind;
                Point = point;
                Name = name;
            }

            public string Id { get; }
            public WorldNodeKind Kind { get; }
            public Vector2 Point { get; }
            public string Name { get; }
        }

        private readonly List<Node> nodes = new();
        private readonly Dictionary<string, int> indexOf = new();
        private readonly List<List<int>> links = new();
        private readonly List<(int From, int To)> lines = new();
        private readonly List<(int Level, Vector2 Point)> levels = new();

        public IReadOnlyList<Node> Nodes => nodes;
        public IReadOnlyList<(int From, int To)> Lines => lines;
        public IReadOnlyList<(int Level, Vector2 Point)> Levels => levels;

        public void Add(string id, WorldNodeKind kind, Vector2 point, string name)
        {
            indexOf.Add(id, nodes.Count);
            nodes.Add(new Node(id, kind, point, name));
            links.Add(new List<int>());
        }

        public void Link(string a, string b)
        {
            int from = indexOf[a];
            int to = indexOf[b];
            links[from].Add(to);
            links[to].Add(from);
            lines.Add((from, to));
        }

        public void AddLevel(int level, Vector2 point) => levels.Add((level, point));

        public int IndexOf(string id) => indexOf.TryGetValue(id, out int index) ? index : -1;

        public Vector2 PointOf(string id) => nodes[indexOf[id]].Point;

        public IReadOnlyList<int> LinksOf(int index) => links[index];

        /// <summary>Each square's state from the cleared squares.</summary>
        public WorldNodeState[] StatesOf(ICollection<string> cleared)
        {
            var states = new WorldNodeState[nodes.Count];
            for (int i = 0; i < states.Length; i++)
                states[i] = WorldNodeState.Hidden;
            var queue = new Queue<int>();
            for (int i = 0; i < nodes.Count; i++)
                if (cleared.Contains(nodes[i].Id))
                {
                    states[i] = WorldNodeState.Cleared;
                    queue.Enqueue(i);
                }
            // クリアしたマスの隣は挑める。そこから先は、倒していない関所で止まるまで見える。
            for (int i = 0; i < nodes.Count; i++)
                if (states[i] == WorldNodeState.Cleared)
                    foreach (int next in links[i])
                        if (states[next] == WorldNodeState.Hidden)
                            states[next] = WorldNodeState.Open;
            var seen = new HashSet<int>(queue);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                if (states[at] != WorldNodeState.Cleared && nodes[at].Kind == WorldNodeKind.Boss)
                    continue;
                foreach (int next in links[at])
                {
                    if (!seen.Add(next))
                        continue;
                    if (states[next] == WorldNodeState.Hidden)
                        states[next] = WorldNodeState.Seen;
                    queue.Enqueue(next);
                }
            }
            return states;
        }

        /// <summary>
        /// The squares from <paramref name="from"/> to <paramref name="to"/> over cleared squares,
        /// both ends included; the last one may be open. Empty when it cannot be reached.
        /// </summary>
        public List<int> PathTo(int from, int to, IReadOnlyList<WorldNodeState> states)
        {
            var path = new List<int>();
            if (
                from < 0
                || to < 0
                || states[to] is not (WorldNodeState.Cleared or WorldNodeState.Open)
            )
                return path;
            var previous = new int[nodes.Count];
            for (int i = 0; i < previous.Length; i++)
                previous[i] = -1;
            previous[from] = from;
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                if (at == to)
                    break;
                // 挑めるマスへは入れるが、その先へは抜けられない。
                if (at != from && states[at] != WorldNodeState.Cleared)
                    continue;
                foreach (int next in links[at])
                    if (
                        previous[next] < 0
                        && (next == to || states[next] == WorldNodeState.Cleared)
                    )
                    {
                        previous[next] = at;
                        queue.Enqueue(next);
                    }
            }
            if (previous[to] < 0)
                return path;
            for (int at = to; at != from; at = previous[at])
                path.Add(at);
            path.Add(from);
            path.Reverse();
            return path;
        }
    }
}
