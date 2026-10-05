using System;
using System.Collections.Generic;
using System.Linq;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The adventure's branching route, made again the same from the seed the server chose when
    /// the adventure started (doc/plans/2026-10-04-exploration-route-map.md). Like an amidakuji,
    /// three or four roads run side by side from the entrance to the deepest room, winding as
    /// they go, and the cross paths between neighbouring roads let the party change road. Each
    /// floor has one room on each road; the party passes one room a floor.
    /// Positions are in route space: <see cref="AdventureRoutePoint.T"/> is how far along (0 at
    /// the entrance, 1 at the deepest room) and <see cref="AdventureRoutePoint.S"/> is across (0..1).
    /// </summary>
    public sealed class AdventureRouteMap
    {
        public const string EntranceId = "entrance";
        public const string BossId = "boss";

        // 道同士の横道を置く確率。隣の道へ移れる所の多さを決める。
        private const double CrossChance = 0.62;

        private readonly Dictionary<string, AdventureRoom> rooms = new();
        private readonly Dictionary<string, AdventureRoutePoint> points = new();
        private readonly Lane[] lanes;
        private readonly float minGap;

        private AdventureRouteMap(int seed, int roomCount, Lane[] lanes, float minGap)
        {
            Seed = seed;
            RoomCount = roomCount;
            this.lanes = lanes;
            this.minGap = minGap;
        }

        public int Seed { get; }

        // 入口と最奥の間のあいだに通る部屋の数（＝道中の階の数）。
        public int RoomCount { get; }
        public int LaneCount => lanes.Length;
        public int BossFloor => RoomCount + 2;

        // 入口、道中の部屋（階の順、道の順）、最奥の間の順。
        public IReadOnlyList<AdventureRoom> Rooms { get; private set; } =
            Array.Empty<AdventureRoom>();

        public AdventureRoom Find(string id) =>
            id != null && rooms.TryGetValue(id, out var room) ? room : null;

        public AdventureRoutePoint PointOf(string id) =>
            id != null && points.TryGetValue(id, out var point) ? point : null;

        /// <summary>The room id of a floor's road, as the server is told it.</summary>
        public static string RoomId(int floor, int lane) => $"f{floor}-{lane}";

        /// <summary>
        /// Where a road runs across the map at <paramref name="t"/>. The roads wind, keep their
        /// order, stay at least a gap apart so the rooms' plates never overlap, and stay inside
        /// the map.
        /// </summary>
        public float LaneS(int lane, float t)
        {
            Span<float> s = stackalloc float[lanes.Length];
            float margin = 0.5f / lanes.Length;
            // 入口と最奥の近くでは蛇行を弱め、扇形に集まる道をきれいに見せる。
            float envelope = Math.Clamp(Math.Min(t / 0.12f, (1f - t) / 0.12f), 0f, 1f);
            for (int i = 0; i < lanes.Length; i++)
            {
                float baseS = margin + i * (1f - 2f * margin) / (lanes.Length - 1);
                s[i] = baseS + envelope * lanes[i].Wind(t);
            }
            for (int pass = 0; pass < 2 * lanes.Length; pass++)
            {
                for (int i = 1; i < lanes.Length; i++)
                {
                    if (s[i] - s[i - 1] >= minGap)
                        continue;
                    float middle = (s[i] + s[i - 1]) / 2f;
                    s[i - 1] = middle - minGap / 2f;
                    s[i] = middle + minGap / 2f;
                }
            }
            // 地図の端（0.03〜0.97）に収め、間隔を保ったまま端から押し戻す。
            int last = lanes.Length - 1;
            for (int i = 0; i <= last; i++)
                s[i] = Math.Clamp(s[i], 0.03f + i * minGap, 0.97f - (last - i) * minGap);
            for (int i = 1; i <= last; i++)
                s[i] = Math.Max(s[i], s[i - 1] + minGap);
            for (int i = last - 1; i >= 0; i--)
                s[i] = Math.Min(s[i], s[i + 1] - minGap);
            return s[lane];
        }

        /// <summary>
        /// Points along the road from one room to the next, in route space. A road that changes
        /// lane eases across between the two roads' windings.
        /// </summary>
        public IReadOnlyList<(float T, float S)> Path(string fromId, string toId, int steps = 48)
        {
            var from = PointOf(fromId);
            var to = PointOf(toId);
            if (from == null || to == null)
                return Array.Empty<(float, float)>();
            var path = new (float T, float S)[steps + 1];
            for (int k = 0; k <= steps; k++)
            {
                float u = k / (float)steps;
                float t = from.T + (to.T - from.T) * u;
                float ease = u * u * (3f - 2f * u);
                float a = from.Lane < 0 ? from.S : LaneS(from.Lane, t);
                float b = to.Lane < 0 ? to.S : LaneS(to.Lane, t);
                path[k] = (t, from.Lane == to.Lane ? a : a * (1f - ease) + b * ease);
            }
            return path;
        }

        /// <summary>The rooms reachable from the given rooms, themselves included.</summary>
        public HashSet<string> Reachable(IEnumerable<string> from)
        {
            var seen = new HashSet<string>();
            var stack = new Stack<string>(from);
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (!seen.Add(id))
                    continue;
                var room = Find(id);
                if (room == null)
                    continue;
                foreach (var next in room.Next)
                    stack.Push(next);
            }
            return seen;
        }

        /// <summary>
        /// Makes the route of a destination from the adventure's seed. The same seed and room
        /// count always make the same route, on every device.
        /// </summary>
        public static AdventureRouteMap Generate(
            int seed,
            int roomCount,
            Func<AdventureRoomKind, string> encounter = null
        )
        {
            if (roomCount < 1)
                throw new ArgumentOutOfRangeException(nameof(roomCount));
            encounter ??= _ => "";
            var random = new RouteRandom(seed);
            int laneCount = random.Next() < 0.3 ? 3 : 4;
            float amp = laneCount == 3 ? 0.13f : 0.11f;
            var lanes = new Lane[laneCount];
            for (int i = 0; i < laneCount; i++)
                lanes[i] = new Lane(
                    amp * (0.5f + random.NextFloat()),
                    6f + random.NextFloat() * 6f,
                    random.NextFloat() * 6.2832f,
                    amp * 0.45f * random.NextFloat(),
                    13f + random.NextFloat() * 8f,
                    random.NextFloat() * 6.2832f
                );
            var map = new AdventureRouteMap(seed, roomCount, lanes, laneCount == 3 ? 0.26f : 0.2f);

            // 道中の部屋の位置。階ごとに少しずらし、毎回違う並びに見せる。
            var t = new float[roomCount, laneCount];
            for (int c = 0; c < roomCount; c++)
            for (int lane = 0; lane < laneCount; lane++)
                t[c, lane] =
                    (c + 1) / (float)(roomCount + 1)
                    + (random.NextFloat() - 0.5f) * 0.35f / (roomCount + 1);

            // 横道：隣の道の次の階へ斜めに移る。同じ階のあいだで交わる横道は置かない。
            var crosses = new List<(int Column, int From, int To)>();
            for (int c = 0; c < roomCount - 1; c++)
            {
                var used = new HashSet<int>();
                for (int lane = 0; lane < laneCount - 1; lane++)
                {
                    if (random.Next() > CrossChance)
                        continue;
                    bool down = random.Next() < 0.5;
                    if (used.Contains(lane) || used.Contains(lane + 1))
                        continue;
                    crosses.Add((c, down ? lane : lane + 1, down ? lane + 1 : lane));
                    used.Add(lane);
                    used.Add(lane + 1);
                }
            }

            var kinds = Kinds(random, roomCount, laneCount);

            var all = new List<AdventureRoom>();
            map.Add(
                all,
                new AdventureRoom(
                    EntranceId,
                    1,
                    AdventureRoomKind.Start,
                    "",
                    Enumerable.Range(0, laneCount).Select(lane => RoomId(2, lane)).ToArray()
                ),
                new AdventureRoutePoint(0f, 0.5f, -1)
            );
            for (int c = 0; c < roomCount; c++)
            {
                int floor = c + 2;
                for (int lane = 0; lane < laneCount; lane++)
                {
                    var next = new List<string>();
                    if (c == roomCount - 1)
                        next.Add(BossId);
                    else
                    {
                        next.Add(RoomId(floor + 1, lane));
                        foreach (var cross in crosses)
                            if (cross.Column == c && cross.From == lane)
                                next.Add(RoomId(floor + 1, cross.To));
                    }
                    var kind = kinds[c, lane];
                    float at = t[c, lane];
                    map.Add(
                        all,
                        new AdventureRoom(
                            RoomId(floor, lane),
                            floor,
                            kind,
                            encounter(kind),
                            next.ToArray()
                        ),
                        new AdventureRoutePoint(at, map.LaneS(lane, at), lane)
                    );
                }
            }
            map.Add(
                all,
                new AdventureRoom(
                    BossId,
                    map.BossFloor,
                    AdventureRoomKind.Boss,
                    encounter(AdventureRoomKind.Boss),
                    Array.Empty<string>()
                ),
                new AdventureRoutePoint(1f, 0.5f, -1)
            );
            map.Rooms = all;
            return map;
        }

        private void Add(List<AdventureRoom> all, AdventureRoom room, AdventureRoutePoint point)
        {
            all.Add(room);
            rooms[room.Id] = room;
            points[room.Id] = point;
        }

        // 部屋の種類。最初の階は弱い魔物、強い魔物は3つ目の階（第4層）から、道中の中ほどの階は宝箱、
        // 最奥の直前は強い魔物を置かない。同じ道で強い魔物や宝箱を続けない。
        private static AdventureRoomKind[,] Kinds(RouteRandom random, int roomCount, int laneCount)
        {
            var kinds = new AdventureRoomKind[roomCount, laneCount];
            int middle = roomCount >= 5 ? roomCount / 2 : -1;
            for (int c = 0; c < roomCount; c++)
            for (int lane = 0; lane < laneCount; lane++)
            {
                var previous = c > 0 ? kinds[c - 1, lane] : AdventureRoomKind.Start;
                AdventureRoomKind kind;
                if (c == 0)
                    kind = AdventureRoomKind.Battle;
                else if (c == middle)
                    kind = AdventureRoomKind.Treasure;
                else if (c == roomCount - 1)
                    kind =
                        random.Next() < 0.7 ? AdventureRoomKind.Battle : AdventureRoomKind.Treasure;
                else if (c < 2)
                    kind =
                        random.Next() < 0.75
                            ? AdventureRoomKind.Battle
                            : AdventureRoomKind.Treasure;
                else
                {
                    double roll = random.Next() * 8.8;
                    kind =
                        roll < 5.0 ? AdventureRoomKind.Battle
                        : roll < 7.2 ? AdventureRoomKind.Elite
                        : AdventureRoomKind.Treasure;
                }
                if (kind == previous && kind != AdventureRoomKind.Battle)
                    kind = AdventureRoomKind.Battle;
                // 宝箱の階の前後に宝箱を続けない。
                if (kind == AdventureRoomKind.Treasure && middle >= 0 && Math.Abs(c - middle) == 1)
                    kind = AdventureRoomKind.Battle;
                kinds[c, lane] = kind;
            }

            // 強い魔物が少なすぎる道にしない（道中が6部屋以上なら2つ以上）。
            if (roomCount >= 6)
            {
                int elites = 0;
                foreach (var kind in kinds)
                    if (kind == AdventureRoomKind.Elite)
                        elites++;
                // 1巡目は半分の確率で選び、それでも足りなければ2巡目で前から選ぶ。
                for (int round = 0; round < 2 && elites < 2; round++)
                for (int c = 2; c < roomCount - 1 && elites < 2; c++)
                for (int lane = 0; lane < laneCount && elites < 2; lane++)
                {
                    bool nearElite =
                        kinds[c - 1, lane] == AdventureRoomKind.Elite
                        || kinds[c + 1, lane] == AdventureRoomKind.Elite;
                    if (
                        c != middle
                        && kinds[c, lane] == AdventureRoomKind.Battle
                        && !nearElite
                        && (round > 0 || random.Next() < 0.5)
                    )
                    {
                        kinds[c, lane] = AdventureRoomKind.Elite;
                        elites++;
                    }
                }
            }
            return kinds;
        }

        private readonly struct Lane
        {
            private readonly float a1;
            private readonly float f1;
            private readonly float p1;
            private readonly float a2;
            private readonly float f2;
            private readonly float p2;

            public Lane(float a1, float f1, float p1, float a2, float f2, float p2)
            {
                this.a1 = a1;
                this.f1 = f1;
                this.p1 = p1;
                this.a2 = a2;
                this.f2 = f2;
                this.p2 = p2;
            }

            public float Wind(float t) =>
                a1 * (float)Math.Sin(t * f1 + p1) + a2 * (float)Math.Sin(t * f2 + p2);
        }
    }

    /// <summary>Where a room lies on the route: how far along, how far across, and its road.</summary>
    public sealed class AdventureRoutePoint
    {
        public AdventureRoutePoint(float t, float s, int lane)
        {
            T = t;
            S = s;
            Lane = lane;
        }

        public float T { get; }
        public float S { get; }

        // 道の番号。入口と最奥の間は、どの道にも属さないため -1。
        public int Lane { get; }
    }

    /// <summary>
    /// A small random number generator (mulberry32) that gives the same numbers for a seed on
    /// every device and runtime, so the route and the forest are made again the same.
    /// </summary>
    public sealed class RouteRandom
    {
        private uint state;

        public RouteRandom(int seed) => state = unchecked((uint)seed);

        // 0以上1未満。
        public double Next()
        {
            unchecked
            {
                state += 0x6D2B79F5;
                uint t = state;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public float NextFloat() => (float)Next();

        public int Range(int min, int maxExclusive) =>
            min + (int)Math.Floor(Next() * (maxExclusive - min));
    }
}
