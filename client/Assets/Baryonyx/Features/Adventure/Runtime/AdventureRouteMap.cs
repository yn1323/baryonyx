using System;
using System.Collections.Generic;
using System.Linq;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The adventure's branching route, made again the same from the seed the server chose when
    /// the adventure started (doc/features/stage-progression.md). From the entrance the road
    /// splits into two or three, and keeps branching and joining like a net: each floor has two
    /// to six rooms, every room leads on to the next floor and is reached from the one before, and
    /// no two roads cross. The party passes one room a floor.
    /// Positions are in route space: <see cref="AdventureRoutePoint.T"/> is how far along (0 at
    /// the entrance, 1 at the deepest room) and <see cref="AdventureRoutePoint.S"/> is across (0..1).
    /// </summary>
    public sealed class AdventureRouteMap
    {
        public const string EntranceId = "entrance";
        public const string BossId = "boss";

        // 1つの階のマスの数の幅（doc/features/stage-progression.md の冒険の道の作り方）。
        public const int MinRoomsOnFloor = 2;
        public const int MaxRoomsOnFloor = 6;

        // 同じ階で隣り合うマスの、地図を横切る向きの間隔の下限。マスの札が重ならないようにする。
        public const float MinGap = 0.13f;

        // 冒険を始めたときに、進化の祭壇を1つ置く確率。置くときは、決まった階以外のマスを1つ置き換える。
        public const double AltarChance = 0.05;

        // 決まった階以外のマスを選ぶ重みと、置ける最初の階（道中の最初の階を1つ目と数える）。
        private static readonly (AdventureRoomKind Kind, double Weight, int Earliest)[] Weights =
        {
            (AdventureRoomKind.Battle, 45, 2),
            (AdventureRoomKind.Elite, 15, 3),
            (AdventureRoomKind.Treasure, 8, 2),
            (AdventureRoomKind.ShadyChest, 8, 3),
            (AdventureRoomKind.Trial, 7, 4),
            (AdventureRoomKind.Rest, 6, 4),
            (AdventureRoomKind.Merchant, 6, 3),
            (AdventureRoomKind.StepDoor, 5, 3),
        };

        private readonly Dictionary<string, AdventureRoom> rooms = new();
        private readonly Dictionary<string, AdventureRoutePoint> points = new();

        private AdventureRouteMap(int seed, int roomCount)
        {
            Seed = seed;
            RoomCount = roomCount;
        }

        public int Seed { get; }

        // 入口と最奥の間のあいだに通るマスの数（＝道中の階の数）。
        public int RoomCount { get; }
        public int BossFloor => RoomCount + 2;

        // 入口、道中のマス（階の順、地図を横切る向きの順）、最奥の間の順。
        public IReadOnlyList<AdventureRoom> Rooms { get; private set; } =
            Array.Empty<AdventureRoom>();

        public AdventureRoom Find(string id) =>
            id != null && rooms.TryGetValue(id, out var room) ? room : null;

        public AdventureRoutePoint PointOf(string id) =>
            id != null && points.TryGetValue(id, out var point) ? point : null;

        /// <summary>The room id of a floor's room, as the server is told it, counted across from 0.</summary>
        public static string RoomId(int floor, int index) => $"f{floor}-{index}";

        /// <summary>
        /// The kind a floor always has, or null for a floor whose rooms are chosen: the first floor
        /// is monsters, the middle one a chest when the way has five rooms or more, and the last
        /// before the deepest room a rest when it has three or more, as the server's fixedKindOf.
        /// </summary>
        public static AdventureRoomKind? FixedKindOf(int roomCount, int floor)
        {
            int index = floor - 1;
            if (floor == roomCount + 2)
                return AdventureRoomKind.Boss;
            if (index == 1)
                return AdventureRoomKind.Battle;
            if (roomCount >= 5 && index == roomCount / 2 + 1)
                return AdventureRoomKind.Treasure;
            if (roomCount >= 3 && index == roomCount)
                return AdventureRoomKind.Rest;
            return null;
        }

        /// <summary>The first floor (counted from the way's first) a kind may be on, off the fixed floors.</summary>
        public static int EarliestOf(AdventureRoomKind kind)
        {
            if (kind == AdventureRoomKind.Altar)
                return 2;
            foreach (var entry in Weights)
                if (entry.Kind == kind)
                    return entry.Earliest;
            return int.MaxValue;
        }

        /// <summary>
        /// Points along the road from one room to the next, in route space. The road eases across
        /// from the one room to the other.
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
                float ease = u * u * (3f - 2f * u);
                path[k] = (from.T + (to.T - from.T) * u, from.S + (to.S - from.S) * ease);
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
        ) =>
            Generate(
                seed,
                roomCount,
                encounter == null
                    ? (Func<AdventureRoomKind, string, string>)null
                    : (kind, _) => encounter(kind)
            );

        /// <summary>
        /// The same, with each room's encounter chosen from its kind and its id, so rooms of one
        /// kind can hold different enemies.
        /// </summary>
        public static AdventureRouteMap Generate(
            int seed,
            int roomCount,
            Func<AdventureRoomKind, string, string> encounter
        )
        {
            if (roomCount < 1)
                throw new ArgumentOutOfRangeException(nameof(roomCount));
            encounter ??= (_, _) => "";
            var random = new RouteRandom(seed);
            var map = new AdventureRouteMap(seed, roomCount);
            var counts = Counts(random, roomCount);

            // 道中のマスの位置。階ごとに横へ少しずらし、毎回違う並びに見せる。
            var at = new (float T, float S)[roomCount][];
            for (int c = 0; c < roomCount; c++)
                at[c] = Places(random, counts[c], (c + 1) / (float)(roomCount + 1), roomCount);

            // 隣の階のマスどうしをつなぐ道。どのマスも次の階へ続き、前の階から来られる。
            var next = new List<int>[roomCount][];
            for (int c = 0; c < roomCount; c++)
            {
                next[c] = new List<int>[counts[c]];
                for (int i = 0; i < counts[c]; i++)
                    next[c][i] = new List<int>();
            }
            for (int c = 0; c < roomCount - 1; c++)
                foreach (var (from, to) in Links(random, counts[c], counts[c + 1]))
                    next[c][from].Add(to);

            var kinds = Kinds(random, roomCount, counts, next);

            var all = new List<AdventureRoom>();
            map.Add(
                all,
                new AdventureRoom(
                    EntranceId,
                    1,
                    AdventureRoomKind.Start,
                    "",
                    Enumerable.Range(0, counts[0]).Select(i => RoomId(2, i)).ToArray()
                ),
                new AdventureRoutePoint(0f, 0.5f)
            );
            for (int c = 0; c < roomCount; c++)
            {
                int floor = c + 2;
                for (int i = 0; i < counts[c]; i++)
                {
                    string id = RoomId(floor, i);
                    var kind = kinds[c][i];
                    var exits =
                        c == roomCount - 1
                            ? new[] { BossId }
                            : next[c][i].Select(to => RoomId(floor + 1, to)).ToArray();
                    map.Add(
                        all,
                        new AdventureRoom(id, floor, kind, encounter(kind, id), exits),
                        new AdventureRoutePoint(at[c][i].T, at[c][i].S)
                    );
                }
            }
            map.Add(
                all,
                new AdventureRoom(
                    BossId,
                    map.BossFloor,
                    AdventureRoomKind.Boss,
                    encounter(AdventureRoomKind.Boss, BossId),
                    Array.Empty<string>()
                ),
                new AdventureRoutePoint(1f, 0.5f)
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

        // 階ごとのマスの数。入口から2〜3つに分かれ、道中の前半で増え、後半は増減する。
        // 道中が6マス以上なら、5つ以上並ぶ階を1つは作る。
        private static int[] Counts(RouteRandom random, int roomCount)
        {
            var counts = new int[roomCount];
            counts[0] = random.Next() < 0.5 ? 2 : 3;
            for (int c = 1; c < roomCount; c++)
            {
                int[] steps = c <= roomCount / 2 ? new[] { 0, 1, 1, 2 } : new[] { -1, 0, 0, 1 };
                counts[c] = Math.Clamp(
                    counts[c - 1] + steps[random.Range(0, steps.Length)],
                    MinRoomsOnFloor,
                    MaxRoomsOnFloor
                );
            }
            if (roomCount >= 6 && counts.Max() < 5)
            {
                int widest = Array.IndexOf(counts, counts.Max());
                counts[widest] = 5;
            }
            return counts;
        }

        // 1つの階のマスの位置。横は中央に寄せて等間隔に並べ、少しずつずらす。
        private static (float T, float S)[] Places(
            RouteRandom random,
            int count,
            float t,
            int roomCount
        )
        {
            float gap = Math.Min(0.24f, 0.84f / Math.Max(1, count - 1));
            float span = gap * (count - 1);
            float shift = (random.NextFloat() - 0.5f) * Math.Max(0f, 0.86f - span);
            var places = new (float T, float S)[count];
            for (int i = 0; i < count; i++)
            {
                float s = 0.5f + shift + (i - (count - 1) / 2f) * gap;
                s += (random.NextFloat() - 0.5f) * 0.3f * gap;
                float along = t + (random.NextFloat() - 0.5f) * 0.25f / (roomCount + 1);
                places[i] = (along, s);
            }
            // 間隔の下限を保ち、全体をずらして地図の端（0.03〜0.97）に収める。
            for (int i = 1; i < count; i++)
                places[i].S = Math.Max(places[i].S, places[i - 1].S + MinGap);
            float over = Math.Max(0f, places[count - 1].S - 0.97f);
            float under = Math.Max(0f, 0.03f - (places[0].S - over));
            for (int i = 0; i < count; i++)
                places[i].S += under - over;
            return places;
        }

        // 2つの階のマスを、交わらない道でつなぐ。上から順に階段のように進み、どのマスにも道を通す。
        private static List<(int From, int To)> Links(RouteRandom random, int from, int to)
        {
            var links = new List<(int, int)> { (0, 0) };
            int i = 0;
            int j = 0;
            while (i < from - 1 || j < to - 1)
            {
                if (i == from - 1)
                    j++;
                else if (j == to - 1)
                    i++;
                else if (random.Next() < 0.35)
                {
                    i++;
                    j++;
                }
                else if ((i + 1) / (float)from <= (j + 1) / (float)to)
                    i++;
                else
                    j++;
                links.Add((i, j));
            }
            return links;
        }

        // マスの種類（doc/features/stage-progression.md の道の決まり）。決まった階はその種類にし、
        // ほかの階は置ける階と重みで選ぶ。道でつながった2つのマスに、魔物以外の同じ種類や、強い魔物と
        // 試練を続けない。真ん中の宝箱の階と最後の休憩の階の隣の階には、宝箱と休憩を置かない。
        private static AdventureRoomKind[][] Kinds(
            RouteRandom random,
            int roomCount,
            int[] counts,
            List<int>[][] next
        )
        {
            var kinds = new AdventureRoomKind[roomCount][];
            var parents = new List<int>[roomCount][];
            for (int c = 0; c < roomCount; c++)
            {
                kinds[c] = new AdventureRoomKind[counts[c]];
                parents[c] = new List<int>[counts[c]];
                for (int i = 0; i < counts[c]; i++)
                    parents[c][i] = new List<int>();
            }
            for (int c = 0; c < roomCount - 1; c++)
            for (int i = 0; i < counts[c]; i++)
                foreach (int to in next[c][i])
                    parents[c + 1][to].Add(i);

            AdventureRoomKind? Fixed(int c) => FixedKindOf(roomCount, c + 2);
            bool NearFixed(int c, AdventureRoomKind kind) =>
                (c > 0 && Fixed(c - 1) == kind) || (c < roomCount - 1 && Fixed(c + 1) == kind);
            bool Heavy(AdventureRoomKind kind) =>
                kind == AdventureRoomKind.Elite || kind == AdventureRoomKind.Trial;
            IEnumerable<AdventureRoomKind> Around(int c, int i)
            {
                if (c > 0)
                    foreach (int p in parents[c][i])
                        yield return kinds[c - 1][p];
                if (c < roomCount - 1)
                    foreach (int n in next[c][i])
                        yield return kinds[c + 1][n];
            }
            // 道でつながったマスと並べてよいか。
            bool Fits(int c, int i, AdventureRoomKind kind)
            {
                if (
                    (kind == AdventureRoomKind.Treasure || kind == AdventureRoomKind.Rest)
                    && (
                        NearFixed(c, AdventureRoomKind.Treasure)
                        || NearFixed(c, AdventureRoomKind.Rest)
                    )
                )
                    return false;
                foreach (var other in Around(c, i))
                {
                    if (other == kind && kind != AdventureRoomKind.Battle)
                        return false;
                    if (Heavy(other) && Heavy(kind))
                        return false;
                }
                return true;
            }

            for (int c = 0; c < roomCount; c++)
            for (int i = 0; i < counts[c]; i++)
            {
                if (Fixed(c) is { } fixedKind)
                {
                    kinds[c][i] = fixedKind;
                    continue;
                }
                // 次の階はまだ決めていないため、ここでは前の階とだけ比べる（次の階は Battle の既定値）。
                var choices = Weights
                    .Where(entry => c + 1 >= entry.Earliest && Fits(c, i, entry.Kind))
                    .ToArray();
                double roll = random.Next() * choices.Sum(entry => entry.Weight);
                var kind = AdventureRoomKind.Battle;
                foreach (var entry in choices)
                {
                    if (roll < entry.Weight)
                    {
                        kind = entry.Kind;
                        break;
                    }
                    roll -= entry.Weight;
                }
                kinds[c][i] = kind;
            }

            int Count(AdventureRoomKind kind) => kinds.Sum(floor => floor.Count(k => k == kind));
            // 置き換えてよい魔物のマス：決まった階でなく、その種類を置ける階にあり、並びの決まりを守る。
            List<(int C, int I)> Battles(AdventureRoomKind into) =>
                Enumerable
                    .Range(0, roomCount)
                    .Where(c => Fixed(c) == null && c + 1 >= EarliestOf(into))
                    .SelectMany(c => Enumerable.Range(0, counts[c]).Select(i => (c, i)))
                    .Where(place =>
                        kinds[place.c][place.i] == AdventureRoomKind.Battle
                        && Fits(place.c, place.i, into)
                    )
                    .ToList();

            // 行商人と歩数の扉は、地図全体で1〜2つにする。多ければ魔物に戻し、なければ魔物を置き換える。
            foreach (var kind in new[] { AdventureRoomKind.Merchant, AdventureRoomKind.StepDoor })
            {
                int seen = 0;
                for (int c = 0; c < roomCount; c++)
                for (int i = 0; i < counts[c]; i++)
                    if (kinds[c][i] == kind && ++seen > 2)
                        kinds[c][i] = AdventureRoomKind.Battle;
                if (seen == 0 && Battles(kind) is { Count: > 0 } open)
                {
                    var (c, i) = open[random.Range(0, open.Count)];
                    kinds[c][i] = kind;
                }
            }

            // 強い魔物が少なすぎる地図にしない（道中が6マス以上なら2つ以上）。
            if (roomCount >= 6)
            {
                while (Count(AdventureRoomKind.Elite) < 2)
                {
                    var open = Battles(AdventureRoomKind.Elite);
                    if (open.Count == 0)
                        break;
                    var (c, i) = open[random.Range(0, open.Count)];
                    kinds[c][i] = AdventureRoomKind.Elite;
                }
            }

            // 進化の祭壇は、冒険を始めたときに5%で、決まった階以外のマスを1つだけ置き換える。
            // 強い魔物・行商人・歩数の扉が足りなくなる置き換えはしない。
            if (random.Next() < AltarChance)
            {
                var open = new List<(int C, int I)>();
                for (int c = 0; c < roomCount; c++)
                for (int i = 0; i < counts[c]; i++)
                {
                    var kind = kinds[c][i];
                    bool needed =
                        (kind == AdventureRoomKind.Elite && Count(kind) <= 2 && roomCount >= 6)
                        || (
                            (
                                kind == AdventureRoomKind.Merchant
                                || kind == AdventureRoomKind.StepDoor
                            )
                            && Count(kind) <= 1
                        );
                    if (Fixed(c) == null && !needed)
                        open.Add((c, i));
                }
                if (open.Count > 0)
                {
                    var (c, i) = open[random.Range(0, open.Count)];
                    kinds[c][i] = AdventureRoomKind.Altar;
                }
            }
            return kinds;
        }
    }

    /// <summary>Where a room lies on the route: how far along and how far across.</summary>
    public sealed class AdventureRoutePoint
    {
        public AdventureRoutePoint(float t, float s)
        {
            T = t;
            S = s;
        }

        public float T { get; }
        public float S { get; }
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
