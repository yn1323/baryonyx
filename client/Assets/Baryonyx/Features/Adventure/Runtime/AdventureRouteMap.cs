using System;
using System.Collections.Generic;
using System.Linq;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The adventure's branching route, made again the same from the seed the server chose when
    /// the adventure started (doc/features/stage-progression.md). The rooms stand on five lanes
    /// running from the entrance to the deepest room, like a board: from the entrance the road
    /// splits into two or three, and each road goes straight on along its lane or slants to the
    /// next lane, so roads split and join without crossing. Each floor has two to four rooms,
    /// every room leads on to the next floor and is reached from the one before. The party passes
    /// one room a floor.
    /// Positions are in route space: <see cref="AdventureRoutePoint.T"/> is how far along (0 at
    /// the entrance, 1 at the deepest room) and <see cref="AdventureRoutePoint.S"/> is across (0..1).
    /// </summary>
    public sealed class AdventureRouteMap
    {
        public const string EntranceId = "entrance";
        public const string BossId = "boss";

        // 1つの階のマスの数の幅（doc/features/stage-progression.md の冒険の道の作り方）。
        public const int MinRoomsOnFloor = 2;
        public const int MaxRoomsOnFloor = 4;

        // マスが並ぶ列の数と、隣り合う列の間隔（地図を横切る向き）。真ん中の列に入口と最奥の間を置く。
        public const int LaneCount = 5;
        public const float LaneGap = 0.235f;

        // 道が次の階で、同じ列を真っすぐ進む・隣の列へ斜めに移る・2つに分かれる重み。
        private const double StraightWeight = 0.5;
        private const double SlantWeight = 0.32;

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
            // 「？」：入るまで中身が分からない。中身はサーバーが入ったときに決める。
            (AdventureRoomKind.Mystery, 10, 3),
        };

        private readonly Dictionary<string, AdventureRoom> rooms = new();
        private readonly Dictionary<string, AdventureRoutePoint> points = new();
        private readonly Func<AdventureRoomKind, string, string> encounter;

        private AdventureRouteMap(
            int seed,
            int roomCount,
            Func<AdventureRoomKind, string, string> encounter
        )
        {
            Seed = seed;
            RoomCount = roomCount;
            this.encounter = encounter;
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

        /// <summary>
        /// A "?" room as what it turned out to be: the same place and roads, the kind the server
        /// chose, and that kind's encounter. Any other room stays as it is.
        /// </summary>
        public AdventureRoom Reveal(string id, AdventureRoomKind kind)
        {
            var room = Find(id);
            if (room == null || room.Kind != AdventureRoomKind.Mystery)
                return room;
            return new AdventureRoom(
                room.Id,
                room.Floor,
                kind,
                encounter(kind, room.Id),
                room.Next
            );
        }

        /// <summary>The room id of a floor's room, as the server is told it, counted across from 0.</summary>
        public static string RoomId(int floor, int index) => $"f{floor}-{index}";

        /// <summary>The floor of a way's room id ("f5-2" is 5), or 0 for the entrance, the deepest room and others.</summary>
        public static int FloorOf(string id)
        {
            if (id == null || !id.StartsWith("f", StringComparison.Ordinal))
                return 0;
            int dash = id.IndexOf('-');
            return dash > 1 && int.TryParse(id.Substring(1, dash - 1), out int floor) ? floor : 0;
        }

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

        /// <summary>The lane (0 to <see cref="LaneCount"/> - 1) a room stands on, or -1 for an unknown room.</summary>
        public int LaneOf(string id) =>
            PointOf(id) is { } point ? (int)Math.Round((point.S - LaneS(0)) / LaneGap) : -1;

        // 列の、地図を横切る向きの位置。真ん中の列が 0.5。
        private static float LaneS(int lane) => 0.5f + (lane - (LaneCount - 1) / 2) * LaneGap;

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
            var map = new AdventureRouteMap(seed, roomCount, encounter);
            // 階ごとのマスの列と、隣の階のマスどうしをつなぐ道。どのマスも次の階へ続き、前の階から来られる。
            var (lanes, next) = Lanes(random, roomCount);
            var counts = lanes.Select(floor => floor.Length).ToArray();

            // 道中のマスの位置。同じ階のマスは縦に揃え、列の上に置く。
            var at = new (float T, float S)[roomCount][];
            for (int c = 0; c < roomCount; c++)
                at[c] = lanes[c]
                    .Select(lane => ((c + 1) / (float)(roomCount + 1), LaneS(lane)))
                    .ToArray();

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

        // 階ごとのマスの列（小さい順）と、次の階のどのマスへ道が続くか（次の階の中の順番）。
        // 入口の次の階は、真ん中の両隣の2列か、真ん中と両隣の3列。そこから先は、道ごとに同じ列を
        // 真っすぐ進むか、隣の列へ斜めに移るか、両隣の列へ2つに分かれる。同じ列へ着いた道は合流する。
        // 上の道より下へは行かないため、道は交差しない。1つの階のマスが2〜4にならなければ選び直す。
        private static (int[][] Lanes, List<int>[][] Next) Lanes(RouteRandom random, int roomCount)
        {
            int middle = (LaneCount - 1) / 2;
            var lanes = new int[roomCount][];
            lanes[0] =
                random.Next() < 0.5
                    ? new[] { middle - 1, middle + 1 }
                    : new[] { middle - 1, middle, middle + 1 };
            var next = new List<int>[roomCount][];
            for (int c = 0; c < roomCount; c++)
            {
                var here = lanes[c];
                next[c] = here.Select(_ => new List<int>()).ToArray();
                if (c == roomCount - 1)
                    break;
                int[][] targets = null;
                for (int attempt = 0; attempt < 20 && targets == null; attempt++)
                {
                    var tried = Branches(random, here);
                    int count = tried.SelectMany(to => to).Distinct().Count();
                    if (count >= MinRoomsOnFloor && count <= MaxRoomsOnFloor)
                        targets = tried;
                }
                // 選び直しても収まらなければ、どの道も真っすぐ進める（マスの数は今の階と同じ）。
                targets ??= here.Select(lane => new[] { lane }).ToArray();
                lanes[c + 1] = targets
                    .SelectMany(to => to)
                    .Distinct()
                    .OrderBy(lane => lane)
                    .ToArray();
                for (int i = 0; i < here.Length; i++)
                    foreach (int lane in targets[i])
                        next[c][i].Add(Array.IndexOf(lanes[c + 1], lane));
            }
            return (lanes, next);
        }

        // 1つの階の各マスから、次の階のどの列へ道を延ばすか。上のマスの道が着いた列より下へは行かない。
        private static int[][] Branches(RouteRandom random, int[] here)
        {
            var targets = new int[here.Length][];
            int floor = 0;
            for (int i = 0; i < here.Length; i++)
            {
                int lane = here[i];
                bool Open(int to) => to >= floor && to >= 0 && to < LaneCount;
                double roll = random.Next();
                int side = random.Next() < 0.5 ? -1 : 1;
                int[] to;
                if (roll < StraightWeight)
                    to = new[] { lane };
                else if (roll < StraightWeight + SlantWeight)
                    to = new[] { Open(lane + side) ? lane + side : lane };
                else
                    to = new[] { lane - 1, lane + 1 }.Where(Open).DefaultIfEmpty(lane).ToArray();
                targets[i] = to;
                floor = to.Max();
            }
            return targets;
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
            // 置き換えてよいマス：決まった階でなく、その種類を置ける階にあり、並びの決まりを守る。
            List<(int C, int I)> Replaceable(
                AdventureRoomKind into,
                Func<AdventureRoomKind, bool> from
            ) =>
                Enumerable
                    .Range(0, roomCount)
                    .Where(c => Fixed(c) == null && c + 1 >= EarliestOf(into))
                    .SelectMany(c => Enumerable.Range(0, counts[c]).Select(i => (c, i)))
                    .Where(place => from(kinds[place.c][place.i]) && Fits(place.c, place.i, into))
                    .ToList();
            // 魔物のマスを置き換え、置き換えられる魔物のマスがなければ、宝箱・怪しい宝箱・休憩・試練・「？」の
            // マスを置き換える。
            List<(int C, int I)> Open(AdventureRoomKind into)
            {
                var open = Replaceable(into, kind => kind == AdventureRoomKind.Battle);
                return open.Count > 0
                    ? open
                    : Replaceable(
                        into,
                        kind =>
                            kind
                                is AdventureRoomKind.Treasure
                                    or AdventureRoomKind.ShadyChest
                                    or AdventureRoomKind.Rest
                                    or AdventureRoomKind.Trial
                                    or AdventureRoomKind.Mystery
                    );
            }

            // 行商人と歩数の扉は、地図全体で1〜2つにする。多ければ魔物に戻し、なければ魔物を置き換える。
            foreach (var kind in new[] { AdventureRoomKind.Merchant, AdventureRoomKind.StepDoor })
            {
                int seen = 0;
                for (int c = 0; c < roomCount; c++)
                for (int i = 0; i < counts[c]; i++)
                    if (kinds[c][i] == kind && ++seen > 2)
                        kinds[c][i] = AdventureRoomKind.Battle;
                if (seen == 0 && Open(kind) is { Count: > 0 } open)
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
                    var open = Open(AdventureRoomKind.Elite);
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
