using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// Paints the exploration map's picture on the device (800x360 dots, one pixel a dot, rows
    /// from the bottom like a texture): the grass, the dirt roads between the rooms and their
    /// clearings, the forest of stamped trees, the ruin at the deepest room, and the fog, light
    /// and shade over it. The rooms' icons, the party and the words are laid over it by
    /// <see cref="ExplorationView"/>.
    /// The grass, the roads and the forest stand at fixed places of the route, so the camera can
    /// follow the party along a road and the picture moves with it; one painter keeps a route's
    /// forest between pictures. A tree stands where it hides no road and the forest around it is
    /// not yet as thick as its size and distance allow; a tree that starts or stops standing as
    /// the camera moves fades in or out through a dither instead of popping. The fog lies on the
    /// rows a fog camera sees far away, so it can lag behind the camera and then clear.
    /// </summary>
    public sealed class ExplorationMapPainter
    {
        // 木が現れ切る・消え切るまでの時間。
        public const float TreeFadeSeconds = 0.25f;

        private const int W = ExplorationMapProjection.Width;
        private const int H = ExplorationMapProjection.Height;

        // 霧の帯の高さ（ドット）。この行より上を霧で覆う。手前の3階ほどには掛けない。
        private const int FogRows = 150;

        // 木を立てる候補の格子（道の長さ・横幅の割合）。
        private const float StepT = 0.009f;
        private const float StepS = 0.022f;

        // 候補を置く道の範囲（t）。カメラが最奥の間に立っても、画面の上端まで森で埋まる長さにする。
        private const float SpotsFrom = -0.15f;
        private const float SpotsTo = 2f;

        // 森の茂り具合：木の幅の2乗のこの倍の広さに1本。小さいほど茂る。
        private const float Spacing = 0.24f;

        // 遠くの小さい木に替える行の縮尺。木ごとに少しずらし、一列にそろって替わらないようにする。
        private const float FarScale = 0.62f;
        private const float FarScaleSpread = 0.04f;

        // 草の模様の大きさ：手前の行で、横1ドット・縦1行がこの値の1/1.5になる。
        private const float GrassAcross = ExplorationMapProjection.NearWidth / 1.5f;
        private const float GrassAlong =
            (ExplorationMapProjection.NearY - ExplorationMapProjection.FarY)
            * (1f + ExplorationMapProjection.Depth)
            / ExplorationMapProjection.Span
            / 1.5f;

        // 木の影の縦の幅（横の幅に対する割合）。横に長い影を、地面と同じだけ縦に縮める。
        private const float ShadowSquash = 0.75f * ExplorationMapProjection.Squash;

        private static readonly Color32[] Grass =
        {
            new(0x1b, 0x33, 0x20, 255),
            new(0x24, 0x42, 0x2a, 255),
            new(0x2f, 0x52, 0x30, 255),
            new(0x3d, 0x62, 0x36, 255),
            new(0x4b, 0x72, 0x40, 255),
        };

        private static readonly Color32 RoadEdge = new(0x4a, 0x3a, 0x24, 255);
        private static readonly Color32 Road = new(0x95, 0x7a, 0x4e, 255);
        private static readonly Color32 RoadLight = new(0xa8, 0x8c, 0x5c, 255);
        private static readonly Color32 PassedRoad = new(0xb8, 0x9a, 0x66, 255);
        private static readonly Color32 PassedLight = new(0xd0, 0xb4, 0x7a, 255);
        private static readonly Color32 OldRoadEdge = new(0x1e, 0x32, 0x20, 255);
        private static readonly Color32 OldRoad = new(0x30, 0x48, 0x2c, 255);
        private static readonly Color32 GuideDot = new(0xff, 0xf3, 0xcf, 255);
        private static readonly Color32 PassedDot = new(0xf2, 0xc2, 0x4f, 255);
        private static readonly Color32 Clearing = new(0x8a, 0x70, 0x48, 255);
        private static readonly Color32 OldClearing = new(0x3a, 0x4a, 0x2c, 255);
        private static readonly Color32 TreeShadow = new(0x14, 0x28, 0x18, 255);
        private static readonly Color32 BossGlow = new(0x7a, 0x3a, 0xc0, 255);
        private static readonly Color32 Fog = new(176, 196, 196, 255);
        private static readonly Color32 Light = new(255, 240, 190, 255);
        private static readonly Color32 Shade = new(4, 8, 10, 255);
        private static readonly int[,] Bayer =
        {
            { 0, 8, 2, 10 },
            { 12, 4, 14, 6 },
            { 3, 11, 1, 9 },
            { 15, 7, 13, 5 },
        };

        // 画面に対して動かない光の筋と周りの影の濃さ（1/256単位、上から数えた行）。
        private static readonly byte[] LightAlpha = MakeLight();
        private static readonly byte[] ShadeAlpha = MakeShade();

        private enum RoadState
        {
            Old,
            Ahead,
            Passed,
            Next,
        }

        private sealed class RoadLine
        {
            public string From;
            public string To;
            public (float T, float S)[] Route;
            public RoadState State;
            public readonly List<Vector2> Dots = new();
        }

        private struct TreeSpot
        {
            public float T;
            public float S;
            public int Pick;
            public bool Low;
            public bool Flip;

            // 茂り具合で間引くときの順番（0〜1、小さいほど先に立つ）。
            public float Priority;

            // 遠くの小さい木に替える縮尺。
            public float FarBelow;
        }

        private sealed class Picture
        {
            public int Width;
            public int Height;

            // 下の行から数えた画素。
            public Color32[] Pixels;

            public static Picture[] From(PixelStamp[] stamps)
            {
                var pictures = new List<Picture>();
                foreach (var stamp in stamps ?? Array.Empty<PixelStamp>())
                    if (stamp != null && !stamp.IsEmpty)
                        pictures.Add(From(stamp));
                return pictures.ToArray();
            }

            public static Picture From(PixelStamp stamp)
            {
                var pixels = new Color32[stamp.Width * stamp.Height];
                for (int y = 0; y < stamp.Height; y++)
                for (int x = 0; x < stamp.Width; x++)
                    pixels[y * stamp.Width + x] = stamp.Pixel(x, y);
                return new Picture
                {
                    Width = stamp.Width,
                    Height = stamp.Height,
                    Pixels = pixels,
                };
            }
        }

        private readonly RoadLine[] roads;
        private readonly TreeSpot[] spots;
        private readonly float[] shown;
        private readonly float[] nearness;
        private readonly Vector2[] at;
        private readonly int[] order;
        private readonly float[] orderY;
        private readonly Picture[] trees;
        private readonly Picture[] treesFar;
        private readonly Picture[] undergrowth;
        private readonly Picture[] undergrowthFar;
        private readonly Picture ruin;
        private readonly float treeSize;

        // 木を立てない所（道・空き地・最奥の間の近く）。2ドット四方を1マスにする。
        private readonly bool[] mask = new bool[(W / 2) * (H / 2)];

        public ExplorationMapPainter(AdventureRouteMap map, ExplorationMapArt art)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            roads = MakeRoads(map);
            trees = Picture.From(art != null ? art.Trees : null);
            treesFar = Picture.From(art != null ? art.TreesFar : null);
            undergrowth = Picture.From(art != null ? art.Undergrowth : null);
            undergrowthFar = Picture.From(art != null ? art.UndergrowthFar : null);
            ruin =
                art != null && art.Ruin != null && !art.Ruin.IsEmpty
                    ? Picture.From(art.Ruin)
                    : null;
            treeSize = 0f;
            foreach (var tree in trees)
                treeSize += tree.Width / (float)trees.Length;
            spots = trees.Length > 0 ? MakeSpots(map.Seed) : Array.Empty<TreeSpot>();
            shown = new float[spots.Length];
            nearness = new float[spots.Length];
            at = new Vector2[spots.Length];
            order = new int[spots.Length];
            orderY = new float[spots.Length];
        }

        public AdventureRouteMap Map { get; }

        // 木が現れ切り、消え切っている。false のあいだは、描き直すと木の見え方が進む。
        public bool Settled { get; private set; } = true;

        /// <summary>
        /// Paints the map of the run from the camera <paramref name="projection"/>, with the fog
        /// lying where the <paramref name="fog"/> camera (the same camera when null) sees it,
        /// thinned by <paramref name="thinning"/> (0 to 1) while it clears.
        /// <paramref name="seconds"/> is the time since the last picture: the trees fade that far
        /// toward standing or not; 0 shows them at once.
        /// </summary>
        public void Paint(
            Color32[] pixels,
            AdventureRun run,
            ExplorationMapProjection projection,
            ExplorationMapSight sight,
            ExplorationMapProjection fog = null,
            float seconds = 0f,
            float thinning = 0f
        )
        {
            if (pixels == null || pixels.Length != W * H)
                throw new ArgumentException("The map needs 800x360 pixels.", nameof(pixels));
            if (run == null)
                throw new ArgumentNullException(nameof(run));
            if (projection == null)
                throw new ArgumentNullException(nameof(projection));
            if (sight == null)
                throw new ArgumentNullException(nameof(sight));
            // 透明な画素は、まだ何も描いていない所。最後に草で埋める。
            Array.Clear(pixels, 0, pixels.Length);
            Array.Clear(mask, 0, mask.Length);
            var canvas = new Canvas(pixels);

            PaintRoads(canvas, run, projection, sight);
            PaintClearings(canvas, run, projection, sight);
            var boss = projection.ToDots(Map.PointOf(AdventureRouteMap.BossId));
            MarkMask(boss + new Vector2(0, -14), 30);
            PaintForest(canvas, projection, seconds);
            PaintGrass(pixels, projection);

            // 最奥の間：紫の気配と、霧の上に立つ遺跡。
            canvas.Dither(
                (int)boss.x - 44,
                (int)boss.y - 78,
                88,
                92,
                BossGlow,
                (x, y) =>
                    Mathf.Max(
                        0f,
                        0.6f
                            - new Vector2(x - boss.x, (y - boss.y + 26) * 0.8f).magnitude
                                / 44f
                                * 0.7f
                    )
            );
            PaintFog(pixels, projection, fog ?? projection, Mathf.Clamp01(thinning));
            if (ruin != null)
                canvas.Stamp(
                    ruin,
                    Mathf.RoundToInt(boss.x),
                    Mathf.RoundToInt(boss.y),
                    false,
                    0f,
                    1f
                );
            PaintShade(pixels);
        }

        private static int Width(RoadState state, float y)
        {
            float scale = ExplorationMapProjection.ScaleAt(y);
            return Mathf.Max(1, Mathf.RoundToInt((state == RoadState.Old ? 3f : 6f) * scale));
        }

        private static bool OnMap(Vector2 dots, float margin) =>
            dots.x > -margin && dots.x < W + margin && dots.y > -margin && dots.y < H + margin;

        private static RoadLine[] MakeRoads(AdventureRouteMap map)
        {
            var roads = new List<RoadLine>();
            foreach (var room in map.Rooms)
            foreach (var next in room.Next)
            {
                var path = map.Path(room.Id, next, 64);
                var route = new (float T, float S)[path.Count];
                for (int i = 0; i < route.Length; i++)
                    route[i] = path[i];
                roads.Add(
                    new RoadLine
                    {
                        From = room.Id,
                        To = next,
                        Route = route,
                    }
                );
            }
            return roads.ToArray();
        }

        // 部屋をつなぐ道。通った道、今いる部屋から出る道、行ける先の道、行けなくなった道に分ける。
        private void PaintRoads(
            Canvas canvas,
            AdventureRun run,
            ExplorationMapProjection projection,
            ExplorationMapSight sight
        )
        {
            var passedPairs = new HashSet<(string, string)>();
            for (int i = 1; i < run.Route.Count; i++)
                passedPairs.Add((run.Route[i - 1], run.Route[i]));
            foreach (var road in roads)
            {
                road.State =
                    passedPairs.Contains((road.From, road.To)) ? RoadState.Passed
                    : road.From == run.RoomId ? (run.RoomCleared ? RoadState.Next : RoadState.Ahead)
                    : sight.Reachable.Contains(road.From) ? RoadState.Ahead
                    : RoadState.Old;
                road.Dots.Clear();
                foreach (var (t, s) in road.Route)
                {
                    var dot = projection.ToDots(t, s);
                    if (OnMap(dot, 40))
                        road.Dots.Add(dot);
                }
                if (road.Dots.Count < 2)
                    road.Dots.Clear();
                foreach (var dot in road.Dots)
                    MarkMask(dot, Width(road.State, dot.y) / 2 + 3);
            }
            // 縁、道、明るい中央、点線の順に、行けなくなった道から重ねる。
            for (int pass = 0; pass < 4; pass++)
            for (var state = RoadState.Old; state <= RoadState.Next; state++)
                foreach (var road in roads)
                {
                    if (road.State != state || road.Dots.Count == 0)
                        continue;
                    if (pass == 0)
                        canvas.Line(
                            road.Dots,
                            state,
                            2,
                            state == RoadState.Old ? OldRoadEdge : RoadEdge
                        );
                    else if (pass == 1)
                        canvas.Line(
                            road.Dots,
                            state,
                            0,
                            state == RoadState.Old ? OldRoad
                                : state == RoadState.Passed ? PassedRoad
                                : Road
                        );
                    else if (pass == 2 && state != RoadState.Old)
                        canvas.Line(
                            road.Dots,
                            state,
                            -3,
                            state == RoadState.Passed ? PassedLight : RoadLight
                        );
                    else if (pass == 3 && (state == RoadState.Next || state == RoadState.Passed))
                        canvas.Line(
                            road.Dots,
                            state,
                            int.MinValue,
                            state == RoadState.Next ? GuideDot : PassedDot,
                            dash: 2
                        );
                }
        }

        // 部屋の空き地。通った部屋と、これから行ける部屋は明るい土にする。
        private void PaintClearings(
            Canvas canvas,
            AdventureRun run,
            ExplorationMapProjection projection,
            ExplorationMapSight sight
        )
        {
            foreach (var room in Map.Rooms)
            {
                var dots = projection.ToDots(Map.PointOf(room.Id));
                if (!OnMap(dots, 24))
                    continue;
                float scale = ExplorationMapProjection.ScaleAt(dots.y);
                bool open =
                    room.Id == run.RoomId
                    || sight.Reachable.Contains(room.Id)
                    || sight.Passed.Contains(room.Id);
                int rx = Mathf.RoundToInt(16 * scale);
                int ry = Mathf.Max(
                    1,
                    Mathf.RoundToInt(16 * ExplorationMapProjection.Squash * scale)
                );
                canvas.Ellipse(dots.x, dots.y + 1, rx + 1, ry + 1, RoadEdge, 0f, 1f);
                canvas.Ellipse(dots.x, dots.y, rx, ry, open ? Clearing : OldClearing, 0f, 1f);
                MarkMask(dots + new Vector2(0, -6 * scale), Mathf.RoundToInt(18 * scale));
            }
        }

        // 木と下草を立てる場所の候補。道の上の決まった場所に、種から毎回同じに置く。
        private static TreeSpot[] MakeSpots(int seed)
        {
            var spots = new List<TreeSpot>();
            int rows = Mathf.CeilToInt((SpotsTo - SpotsFrom) / StepT);
            int columns = Mathf.CeilToInt(4.4f / StepS);
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                uint hash = Hash(row, column, seed);
                if ((hash & 0xff) < 64)
                    continue;
                uint more = Hash(column, row, seed + 7);
                spots.Add(
                    new TreeSpot
                    {
                        T = SpotsFrom + (row + Unit(hash >> 8)) * StepT,
                        S = -1.7f + (column + Unit(hash >> 16)) * StepS,
                        Pick = (int)((hash >> 4) & 0xfff),
                        Low = (hash >> 24) % 100 < 18,
                        Flip = (hash & 0x100) != 0,
                        Priority = (more & 0xffff) / 65536f,
                        FarBelow = FarScale + (Unit(more >> 16) - 0.5f) * 2f * FarScaleSpread,
                    }
                );
            }
            return spots.ToArray();
        }

        // 木と下草：近い物ほど上に重ね、光は左上から当たるため影は右下へ落とす。
        private void PaintForest(Canvas canvas, ExplorationMapProjection projection, float seconds)
        {
            bool settled = true;
            float step = seconds > 0f ? seconds / TreeFadeSeconds : 1f;
            // 候補の格子の、画面1平方ドットあたりの数は、道の t ごとに決まる。
            const float spotsPerArea = 0.75f / (StepT * StepS);
            // 画面に入る行の道の範囲（t）。この外の候補は、位置を求めずに飛ばす。
            float from = projection.TAtRow(H + 24);
            float to = projection.TAtRow(-24);
            int count = 0;
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                bool inView = spot.T >= from && spot.T <= to;
                var dots = inView ? projection.ToDots(spot.T, spot.S) : default;
                if (!inView || !OnMap(dots, 24))
                {
                    shown[i] = 0f;
                    continue;
                }
                float scale = ExplorationMapProjection.ScaleAt(dots.y);
                bool far = scale < spot.FarBelow;
                var picture = PictureOf(spot, far);
                bool stand = false;
                if (picture != null)
                {
                    float size = treeSize * Mathf.Clamp(scale, 0.45f, 1f);
                    float wanted = 1f / (Spacing * size * size);
                    float candidates = spotsPerArea / projection.DotsPerArea(spot.T);
                    int radius = Mathf.Max(2, Mathf.RoundToInt(picture.Width * 0.36f));
                    stand =
                        spot.Priority * candidates < wanted
                        && !Masked(
                            Mathf.RoundToInt(dots.x),
                            Mathf.RoundToInt(dots.y) - radius / 2,
                            radius
                        );
                }
                float target = stand ? 1f : 0f;
                float near = far ? 0f : 1f;
                shown[i] = Mathf.MoveTowards(shown[i], target, step);
                nearness[i] = seconds > 0f ? Mathf.MoveTowards(nearness[i], near, step) : near;
                if (shown[i] != target || nearness[i] != near)
                    settled = false;
                if (shown[i] <= 0f)
                    continue;
                at[i] = dots;
                order[count] = i;
                orderY[count] = dots.y;
                count++;
            }
            Settled = settled;
            Array.Sort(orderY, order, 0, count);
            for (int k = 0; k < count; k++)
            {
                int i = order[k];
                var spot = spots[i];
                // 近い木と遠い木を入れ替えている間は、2つの絵を市松模様で分け合う。
                float split = nearness[i] * shown[i];
                if (split > 0f)
                    DrawTree(canvas, PictureOf(spot, false), spot.Flip, at[i], 0f, split);
                if (split < shown[i])
                    DrawTree(canvas, PictureOf(spot, true), spot.Flip, at[i], split, shown[i]);
            }
        }

        private Picture PictureOf(TreeSpot spot, bool far)
        {
            var set =
                spot.Low ? (far ? undergrowthFar : undergrowth)
                : far ? treesFar
                : trees;
            return set.Length > 0 ? set[spot.Pick % set.Length] : null;
        }

        private static void DrawTree(
            Canvas canvas,
            Picture picture,
            bool flip,
            Vector2 dots,
            float from,
            float to
        )
        {
            if (picture == null)
                return;
            int bx = Mathf.RoundToInt(dots.x);
            int by = Mathf.RoundToInt(dots.y);
            int rx = Mathf.Max(1, Mathf.RoundToInt(picture.Width * 0.38f));
            int ry = Mathf.Max(1, Mathf.RoundToInt(rx * ShadowSquash));
            canvas.Ellipse(bx + rx / 3, by, rx, ry, TreeShadow, from, to);
            canvas.Stamp(picture, bx, by, flip, from, to);
        }

        private void MarkMask(Vector2 center, int radius)
        {
            int cx = Mathf.RoundToInt(center.x);
            int cy = Mathf.RoundToInt(center.y);
            int r2 = radius * radius;
            int top = Mathf.Max(0, (cy - radius) >> 1);
            int bottom = Mathf.Min(H / 2 - 1, (cy + radius) >> 1);
            int left = Mathf.Max(0, (cx - radius) >> 1);
            int right = Mathf.Min(W / 2 - 1, (cx + radius) >> 1);
            for (int my = top; my <= bottom; my++)
            {
                int dy = my * 2 + 1 - cy;
                for (int mx = left; mx <= right; mx++)
                {
                    int dx = mx * 2 + 1 - cx;
                    if (dx * dx + dy * dy <= r2)
                        mask[my * (W / 2) + mx] = true;
                }
            }
        }

        // 木の根元から奥へ、木の幅ほどの所に道や空き地があるか。
        private bool Masked(int x, int y, int radius)
        {
            int top = Mathf.Max(0, (y - radius) >> 1);
            int bottom = Mathf.Min(H / 2 - 1, (y + radius / 2) >> 1);
            int left = Mathf.Max(0, (x - radius) >> 1);
            int right = Mathf.Min(W / 2 - 1, (x + radius) >> 1);
            for (int my = top; my <= bottom; my++)
            for (int mx = left; mx <= right; mx++)
                if (mask[my * (W / 2) + mx])
                    return true;
            return false;
        }

        // 草地：道の上の場所ごとに決まった模様（ノイズで5段の緑）で、何も描いていない所を埋める。
        private void PaintGrass(Color32[] pixels, ExplorationMapProjection projection)
        {
            int seed = Map.Seed;
            for (int y = 0; y < H; y++)
            {
                float z = projection.DepthAtRow(y);
                float along = projection.TAtRow(y) * GrassAlong;
                float x0 =
                    (projection.AcrossAtRow(y) - W / 2f * z / ExplorationMapProjection.NearWidth)
                    * GrassAcross;
                float dx = z / ExplorationMapProjection.NearWidth * GrassAcross;
                var large = new NoiseRow(along / 12f, seed);
                var middle = new NoiseRow(along / 5f, seed + 1);
                var small = new NoiseRow(along / 2f, seed + 2);
                int row = (H - 1 - y) * W;
                for (int x = 0; x < W; x++)
                {
                    if (pixels[row + x].a != 0)
                        continue;
                    float across = x0 + dx * x;
                    float n =
                        0.55f * large.At(across / 18f)
                        + 0.3f * middle.At(across / 7f)
                        + 0.15f * small.At(across / 3f)
                        + (Bayer[y & 3, x & 3] / 16f - 0.5f) * 0.12f;
                    int k = Mathf.Clamp(Mathf.FloorToInt(n * 6f - 0.6f), 0, Grass.Length - 1);
                    pixels[row + x] = Grass[k];
                }
            }
        }

        /// <summary>Value noise along one row: the lattice of the row is read once per cell.</summary>
        private struct NoiseRow
        {
            private readonly int yi;
            private readonly float v;
            private readonly int seed;
            private int cell;
            private float left;
            private float right;

            public NoiseRow(float y, int seed)
            {
                yi = Mathf.FloorToInt(y);
                float yf = y - yi;
                v = yf * yf * (3f - 2f * yf);
                this.seed = seed;
                cell = int.MinValue;
                left = 0f;
                right = 0f;
            }

            public float At(float x)
            {
                int xi = Mathf.FloorToInt(x);
                if (xi != cell)
                {
                    left = xi == cell + 1 ? right : Lattice(xi);
                    right = Lattice(xi + 1);
                    cell = xi;
                }
                float u = x - xi;
                u = u * u * (3f - 2f * u);
                return left + (right - left) * u;
            }

            private float Lattice(int x)
            {
                float a = (Hash(x, yi, seed) & 0xffff) / 65536f;
                float b = (Hash(x, yi + 1, seed) & 0xffff) / 65536f;
                return a + (b - a) * v;
            }
        }

        // 霧の帯の濃さ。カメラが止まっているときの行で決める。
        private static float FogAt(float y)
        {
            if (y >= FogRows)
                return 0f;
            float k = Mathf.Max(0f, y) / FogRows;
            return k < 0.45f ? Mathf.Lerp(0.92f, 0.78f, k / 0.45f)
                : k < 0.72f ? Mathf.Lerp(0.78f, 0.4f, (k - 0.45f) / 0.27f)
                : Mathf.Lerp(0.4f, 0f, (k - 0.72f) / 0.28f);
        }

        // 奥の霧と、左上から差す光の筋（舞台のポストプロセスの代わり）。
        // 霧は、霧のカメラから見た行の濃さにする。カメラより遅れると、霧が近づいてから晴れる。
        private static void PaintFog(
            Color32[] pixels,
            ExplorationMapProjection projection,
            ExplorationMapProjection fog,
            float thinning
        )
        {
            for (int y = 0; y < H; y++)
            {
                float row = y;
                if (fog != projection)
                {
                    float t = projection.TAtRow(y);
                    if (!float.IsInfinity(t))
                        row = fog.RowOf(t);
                }
                int alpha = Mathf.RoundToInt(FogAt(row) * (1f - thinning) * 256f);
                int index = (H - 1 - y) * W;
                int light = y * W;
                for (int x = 0; x < W; x++)
                {
                    if (alpha > 0)
                        pixels[index + x] = Blend(pixels[index + x], Fog, alpha);
                    int shaft = LightAlpha[light + x];
                    if (shaft > 0)
                        pixels[index + x] = Blend(pixels[index + x], Light, shaft);
                }
            }
        }

        private static void PaintShade(Color32[] pixels)
        {
            for (int y = 0; y < H; y++)
            {
                int index = (H - 1 - y) * W;
                int shade = y * W;
                for (int x = 0; x < W; x++)
                {
                    int alpha = ShadeAlpha[shade + x];
                    if (alpha > 0)
                        pixels[index + x] = Blend(pixels[index + x], Shade, alpha);
                }
            }
        }

        private static Color32 Blend(Color32 under, Color32 over, int alpha) =>
            new(
                (byte)(under.r + (((over.r - under.r) * alpha) >> 8)),
                (byte)(under.g + (((over.g - under.g) * alpha) >> 8)),
                (byte)(under.b + (((over.b - under.b) * alpha) >> 8)),
                255
            );

        private static byte[] MakeLight()
        {
            var alpha = new byte[W * H];
            foreach (int start in new[] { 210, 380, 550 })
                for (int y = 0; y < H; y++)
                {
                    int value = Mathf.RoundToInt(0.16f * (1f - y / (float)H) * 256f);
                    int left = start - Mathf.RoundToInt(y * 0.55f);
                    for (int x = Mathf.Max(0, left); x < Mathf.Min(W, left + 26); x++)
                        alpha[y * W + x] = (byte)value;
                }
            return alpha;
        }

        private static byte[] MakeShade()
        {
            var alpha = new byte[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float d = new Vector2((x - W / 2f) * 0.85f, y - 210f).magnitude;
                alpha[y * W + x] = (byte)
                    Mathf.RoundToInt(Mathf.Clamp01((d - 150f) / 230f) * 0.7f * 256f);
            }
            return alpha;
        }

        private static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        private static float Unit(uint bits) => (bits & 0xff) / 256f;

        /// <summary>Drawing on the map's pixels in dots (y down).</summary>
        private readonly struct Canvas
        {
            private readonly Color32[] pixels;

            public Canvas(Color32[] pixels) => this.pixels = pixels;

            // 市松模様で、from から to までの割合の画素だけを描く（ドット絵の半透明）。
            private static bool Covers(int x, int y, float from, float to)
            {
                if (from <= 0f && to >= 1f)
                    return true;
                float threshold = (Bayer[y & 3, x & 3] + 0.5f) / 16f;
                return threshold >= from && threshold < to;
            }

            public void Set(int x, int y, Color32 color)
            {
                if (x >= 0 && y >= 0 && x < W && y < H)
                    pixels[(H - 1 - y) * W + x] = color;
            }

            public void Fill(int x, int y, int width, int height, Color32 color)
            {
                int left = Math.Max(0, x);
                int right = Math.Min(W, x + width);
                int top = Math.Max(0, y);
                int bottom = Math.Min(H, y + height);
                for (int py = top; py < bottom; py++)
                {
                    int row = (H - 1 - py) * W;
                    for (int px = left; px < right; px++)
                        pixels[row + px] = color;
                }
            }

            // 道の線。grow は道幅に足すドット数で、int.MinValue なら1ドットの点線にする。
            public void Line(
                List<Vector2> dots,
                RoadState state,
                int grow,
                Color32 color,
                int dash = 0
            )
            {
                float travelled = 0f;
                for (int k = 1; k < dots.Count; k++)
                {
                    var from = dots[k - 1];
                    var to = dots[k];
                    float length = Vector2.Distance(from, to);
                    int w = grow == int.MinValue ? 1 : Math.Max(1, Width(state, from.y) + grow);
                    // 2ドット以上の太さなら1ドットごと、細い線は隙間ができないよう半ドットごとに置く。
                    int steps = Mathf.Max(1, Mathf.CeilToInt(length * (w > 2 ? 1f : 2f)));
                    for (int j = 0; j <= steps; j++)
                    {
                        var p = Vector2.Lerp(from, to, j / (float)steps);
                        travelled += length / steps;
                        if (dash > 0 && Mathf.FloorToInt(travelled / dash) % 2 == 1)
                            continue;
                        if (grow != int.MinValue)
                            w = Math.Max(1, Width(state, p.y) + grow);
                        Fill(
                            Mathf.RoundToInt(p.x - w / 2f),
                            Mathf.RoundToInt(p.y - w / 2f),
                            w,
                            w,
                            color
                        );
                    }
                }
            }

            public void Ellipse(
                float cx,
                float cy,
                int rx,
                int ry,
                Color32 color,
                float from,
                float to
            )
            {
                bool all = from <= 0f && to >= 1f;
                for (int y = -ry; y <= ry; y++)
                {
                    int half = Mathf.FloorToInt(
                        rx * Mathf.Sqrt(Mathf.Max(0f, 1f - y * y / (float)(ry * ry))) + 0.35f
                    );
                    int py = Mathf.RoundToInt(cy) + y;
                    int left = Mathf.RoundToInt(cx) - half;
                    if (all)
                    {
                        Fill(left, py, half * 2 + 1, 1, color);
                        continue;
                    }
                    for (int px = left; px <= left + half * 2; px++)
                        if (Covers(px, py, from, to))
                            Set(px, py, color);
                }
            }

            // 2色の市松模様で色を重ねる（ドット絵の半透明）。
            public void Dither(
                int x0,
                int y0,
                int width,
                int height,
                Color32 color,
                Func<int, int, float> alpha
            )
            {
                for (int y = y0; y < y0 + height; y++)
                for (int x = x0; x < x0 + width; x++)
                {
                    float a = alpha(x, y);
                    if (a > 0f && a * 16f > Bayer[y & 3, x & 3] + 0.5f)
                        Set(x, y, color);
                }
            }

            // 絵の下辺の中央を (x, y) に合わせて置く。
            public void Stamp(Picture picture, int x, int y, bool flip, float from, float to)
            {
                bool all = from <= 0f && to >= 1f;
                int left = x - picture.Width / 2;
                int width = picture.Width;
                for (int sy = 0; sy < picture.Height; sy++)
                {
                    int py = y - sy;
                    if (py < 0 || py >= H)
                        continue;
                    int row = (H - 1 - py) * W;
                    int source = sy * width;
                    for (int sx = 0; sx < width; sx++)
                    {
                        int px = left + sx;
                        if (px < 0 || px >= W)
                            continue;
                        var color = picture.Pixels[source + (flip ? width - 1 - sx : sx)];
                        if (color.a == 0 || !(all || Covers(px, py, from, to)))
                            continue;
                        color.a = 255;
                        pixels[row + px] = color;
                    }
                }
            }
        }
    }
}
