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
    /// <see cref="ExplorationView"/>. The forest stands at the same places of the route every time
    /// the same route is painted, so it only turns as the party goes deeper.
    /// </summary>
    public static class ExplorationMapPainter
    {
        private const int W = ExplorationMapProjection.Width;
        private const int H = ExplorationMapProjection.Height;

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
        private static readonly int[,] Bayer =
        {
            { 0, 8, 2, 10 },
            { 12, 4, 14, 6 },
            { 3, 11, 1, 9 },
            { 15, 7, 13, 5 },
        };

        private enum RoadState
        {
            Old,
            Ahead,
            Passed,
            Next,
        }

        /// <summary>Paints the map of the run, seen from the room the party is in.</summary>
        public static void Paint(
            Color32[] pixels,
            AdventureRun run,
            ExplorationMapProjection projection,
            ExplorationMapSight sight,
            ExplorationMapArt art
        )
        {
            if (pixels == null || pixels.Length != W * H)
                throw new ArgumentException("The map needs 800x360 pixels.", nameof(pixels));
            var map = projection.Map;
            var canvas = new Canvas(pixels);
            PaintGrass(canvas, map.Seed);

            var mask = new bool[W * H];
            var roads = Roads(run, projection, sight);
            foreach (var road in roads)
            foreach (var dot in road.Dots)
                canvas.Mark(mask, dot, Width(road.State, dot.y) / 2 + 3);
            foreach (RoadState state in Enum.GetValues(typeof(RoadState)))
            foreach (var road in roads)
                if (road.State == state)
                    canvas.Line(
                        road.Dots,
                        y => Width(state, y) + 2,
                        state == RoadState.Old ? OldRoadEdge : RoadEdge
                    );
            foreach (RoadState state in Enum.GetValues(typeof(RoadState)))
            foreach (var road in roads)
                if (road.State == state)
                    canvas.Line(
                        road.Dots,
                        y => Width(state, y),
                        state == RoadState.Old ? OldRoad
                            : state == RoadState.Passed ? PassedRoad
                            : Road
                    );
            foreach (var road in roads)
                if (road.State != RoadState.Old)
                    canvas.Line(
                        road.Dots,
                        y => Math.Max(1, Width(road.State, y) - 3),
                        road.State == RoadState.Passed ? PassedLight : RoadLight
                    );
            foreach (var road in roads)
                if (road.State == RoadState.Next || road.State == RoadState.Passed)
                    canvas.Line(
                        road.Dots,
                        _ => 1,
                        road.State == RoadState.Next ? GuideDot : PassedDot,
                        dash: 2
                    );

            // 部屋の空き地。
            foreach (var room in map.Rooms)
            {
                var dots = projection.ToDots(map.PointOf(room.Id));
                if (!OnMap(dots, 24))
                    continue;
                float scale = ExplorationMapProjection.ScaleAt(dots.y);
                bool open = room.Id == run.RoomId || sight.Reachable.Contains(room.Id);
                int rx = Mathf.RoundToInt(16 * scale);
                int ry = Mathf.Max(1, Mathf.RoundToInt(7 * scale));
                canvas.Ellipse(dots.x, dots.y + 1, rx + 1, ry + 1, RoadEdge);
                canvas.Ellipse(dots.x, dots.y, rx, ry, open ? Clearing : OldClearing);
                canvas.Mark(mask, dots + new Vector2(0, -6 * scale), Mathf.RoundToInt(18 * scale));
            }

            var boss = projection.ToDots(map.PointOf(AdventureRouteMap.BossId));
            canvas.Mark(mask, boss + new Vector2(0, -14), 30);
            PaintForest(canvas, mask, projection, art);

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
            PaintFog(canvas);
            if (art != null && art.Ruin != null && !art.Ruin.IsEmpty)
                canvas.Stamp(art.Ruin, Mathf.RoundToInt(boss.x), Mathf.RoundToInt(boss.y), false);
            PaintShade(canvas);
        }

        private static int Width(RoadState state, float y)
        {
            float scale = ExplorationMapProjection.ScaleAt(y);
            return Mathf.Max(1, Mathf.RoundToInt((state == RoadState.Old ? 3f : 6f) * scale));
        }

        private static bool OnMap(Vector2 dots, float margin) =>
            dots.x > -margin && dots.x < W + margin && dots.y > -margin && dots.y < H + margin;

        private sealed class RoadLine
        {
            public RoadState State;
            public List<Vector2> Dots;
        }

        // 部屋をつなぐ道。通った道、今いる部屋から出る道、行ける先の道、行けなくなった道に分ける。
        private static List<RoadLine> Roads(
            AdventureRun run,
            ExplorationMapProjection projection,
            ExplorationMapSight sight
        )
        {
            var map = projection.Map;
            var passedPairs = new HashSet<(string, string)>();
            for (int i = 1; i < run.Route.Count; i++)
                passedPairs.Add((run.Route[i - 1], run.Route[i]));
            var roads = new List<RoadLine>();
            foreach (var room in map.Rooms)
            foreach (var next in room.Next)
            {
                RoadState state =
                    passedPairs.Contains((room.Id, next)) ? RoadState.Passed
                    : room.Id == run.RoomId ? (run.RoomCleared ? RoadState.Next : RoadState.Ahead)
                    : sight.Reachable.Contains(room.Id) ? RoadState.Ahead
                    : RoadState.Old;
                var dots = new List<Vector2>();
                foreach (var (t, s) in map.Path(room.Id, next, 64))
                {
                    var dot = projection.ToDots(t, s);
                    if (OnMap(dot, 40))
                        dots.Add(dot);
                }
                if (dots.Count > 1)
                    roads.Add(new RoadLine { State = state, Dots = dots });
            }
            return roads;
        }

        private static void PaintGrass(Canvas canvas, int seed)
        {
            for (int y = 0; y < H; y++)
            {
                float scale = ExplorationMapProjection.ScaleAt(
                    Mathf.Max(y, ExplorationMapProjection.FarY)
                );
                float stretch = 0.5f + scale;
                for (int x = 0; x < W; x++)
                {
                    float n =
                        Fbm(x / stretch, y / stretch, seed)
                        + (Bayer[y & 3, x & 3] / 16f - 0.5f) * 0.12f;
                    int k = Mathf.Clamp(Mathf.FloorToInt(n * 6f - 0.6f), 0, Grass.Length - 1);
                    canvas.Set(x, y, Grass[k]);
                }
            }
        }

        // 木と下草：道の上の決まった場所に立て、近い物から順に、重なりすぎない物だけを残す。
        private static void PaintForest(
            Canvas canvas,
            bool[] mask,
            ExplorationMapProjection projection,
            ExplorationMapArt art
        )
        {
            if (art == null || art.Trees.Length == 0)
                return;
            var map = projection.Map;
            var candidates = new List<(Vector2 Dots, PixelStamp Stamp, bool Flip)>();
            const float stepT = 0.009f;
            const float stepS = 0.022f;
            int rows = Mathf.CeilToInt(1.2f / stepT);
            int columns = Mathf.CeilToInt(4.4f / stepS);
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                uint hash = Hash(row, column, map.Seed);
                if ((hash & 0xff) < 64)
                    continue;
                float t = -0.15f + (row + Unit(hash >> 8)) * stepT;
                float s = -1.7f + (column + Unit(hash >> 16)) * stepS;
                var dots = projection.ToDots(t, s);
                if (!OnMap(dots, 24) || dots.y < ExplorationMapProjection.FarY - 30)
                    continue;
                float scale = ExplorationMapProjection.ScaleAt(dots.y);
                bool far = scale < 0.62f;
                bool low = (hash >> 24) % 100 < 18;
                var set =
                    low ? (far ? art.UndergrowthFar : art.Undergrowth)
                    : far ? art.TreesFar
                    : art.Trees;
                if (set.Length == 0)
                    continue;
                var stamp = set[(int)((hash >> 4) % (uint)set.Length)];
                if (stamp.IsEmpty)
                    continue;
                candidates.Add((dots, stamp, (hash & 0x100) != 0));
            }
            candidates.Sort((a, b) => b.Dots.y.CompareTo(a.Dots.y));

            const int cell = 4;
            var taken = new bool[(W / cell + 1) * (H / cell + 1)];
            var kept = new List<(Vector2 Dots, PixelStamp Stamp, bool Flip)>();
            foreach (var candidate in candidates)
            {
                int bx = Mathf.RoundToInt(candidate.Dots.x);
                int by = Mathf.RoundToInt(candidate.Dots.y);
                int radius = Mathf.Max(2, Mathf.RoundToInt(candidate.Stamp.Width * 0.36f));
                if (Masked(mask, bx, by - radius / 2, radius))
                    continue;
                int cx = Mathf.Clamp(bx / cell, 0, W / cell);
                int cy = Mathf.Clamp(by / cell, 0, H / cell);
                if (taken[cy * (W / cell + 1) + cx])
                    continue;
                int reach = Mathf.Max(1, radius / cell);
                for (int dy = -reach / 2; dy <= reach / 2; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (x >= 0 && y >= 0 && x <= W / cell && y <= H / cell)
                        taken[y * (W / cell + 1) + x] = true;
                }
                kept.Add(candidate);
            }
            kept.Sort((a, b) => a.Dots.y.CompareTo(b.Dots.y));
            foreach (var (dots, stamp, flip) in kept)
            {
                int bx = Mathf.RoundToInt(dots.x);
                int by = Mathf.RoundToInt(dots.y);
                int rx = Mathf.Max(1, Mathf.RoundToInt(stamp.Width * 0.38f));
                // 光は左上から当たるため、影は右下へ落とす。
                canvas.Ellipse(bx + rx / 3, by, rx, Mathf.Max(1, rx / 3), TreeShadow);
                canvas.Stamp(stamp, bx, by, flip);
            }
        }

        private static bool Masked(bool[] mask, int x, int y, int radius)
        {
            for (int dy = -radius; dy <= radius / 2; dy += 2)
            for (int dx = -radius; dx <= radius; dx += 2)
            {
                int px = x + dx;
                int py = y + dy;
                if (px >= 0 && py >= 0 && px < W && py < H && mask[py * W + px])
                    return true;
            }
            return false;
        }

        // 奥の霧と、左上から差す光の筋（舞台のポストプロセスの代わり）。
        private static void PaintFog(Canvas canvas)
        {
            var fog = new Color(176 / 255f, 196 / 255f, 196 / 255f);
            for (int y = 0; y < 200; y++)
            {
                float k = y / 200f;
                float alpha =
                    k < 0.45f ? Mathf.Lerp(0.92f, 0.78f, k / 0.45f)
                    : k < 0.72f ? Mathf.Lerp(0.78f, 0.4f, (k - 0.45f) / 0.27f)
                    : Mathf.Lerp(0.4f, 0f, (k - 0.72f) / 0.28f);
                for (int x = 0; x < W; x++)
                    canvas.Blend(x, y, fog, alpha);
            }
            var light = new Color(1f, 240 / 255f, 190 / 255f);
            foreach (int start in new[] { 210, 380, 550 })
                for (int y = 0; y < H; y++)
                {
                    float alpha = 0.16f * (1f - y / (float)H);
                    int left = start - Mathf.RoundToInt(y * 0.55f);
                    for (int x = left; x < left + 26; x++)
                        canvas.Blend(x, y, light, alpha);
                }
        }

        private static void PaintShade(Canvas canvas)
        {
            var shade = new Color(4 / 255f, 8 / 255f, 10 / 255f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float d = new Vector2((x - W / 2f) * 0.85f, y - 210f).magnitude;
                float alpha = Mathf.Clamp01((d - 150f) / 230f) * 0.7f;
                if (alpha > 0f)
                    canvas.Blend(x, y, shade, alpha);
            }
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

        private static float ValueNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;
            float u = xf * xf * (3f - 2f * xf);
            float v = yf * yf * (3f - 2f * yf);
            float a = (Hash(xi, yi, seed) & 0xffff) / 65536f;
            float b = (Hash(xi + 1, yi, seed) & 0xffff) / 65536f;
            float c = (Hash(xi, yi + 1, seed) & 0xffff) / 65536f;
            float d = (Hash(xi + 1, yi + 1, seed) & 0xffff) / 65536f;
            return (a * (1 - u) + b * u) * (1 - v) + (c * (1 - u) + d * u) * v;
        }

        private static float Fbm(float x, float y, int seed) =>
            0.55f * ValueNoise(x / 18f, y / 12f, seed)
            + 0.3f * ValueNoise(x / 7f, y / 5f, seed + 1)
            + 0.15f * ValueNoise(x / 3f, y / 2f, seed + 2);

        /// <summary>Drawing on the map's pixels in dots (y down).</summary>
        private readonly struct Canvas
        {
            private readonly Color32[] pixels;

            public Canvas(Color32[] pixels) => this.pixels = pixels;

            private static int Index(int x, int y) => (H - 1 - y) * W + x;

            public void Set(int x, int y, Color32 color)
            {
                if (x >= 0 && y >= 0 && x < W && y < H)
                    pixels[Index(x, y)] = color;
            }

            public void Blend(int x, int y, Color color, float alpha)
            {
                if (x < 0 || y < 0 || x >= W || y >= H || alpha <= 0f)
                    return;
                int i = Index(x, y);
                Color under = pixels[i];
                pixels[i] = Color.Lerp(under, color, Mathf.Clamp01(alpha));
            }

            public void Fill(int x, int y, int width, int height, Color32 color)
            {
                for (int py = y; py < y + height; py++)
                for (int px = x; px < x + width; px++)
                    Set(px, py, color);
            }

            public void Line(
                List<Vector2> dots,
                Func<float, int> width,
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
                    int steps = Mathf.Max(1, Mathf.CeilToInt(length * 2f));
                    for (int j = 0; j <= steps; j++)
                    {
                        var p = Vector2.Lerp(from, to, j / (float)steps);
                        travelled += length / steps;
                        if (dash > 0 && Mathf.FloorToInt(travelled / dash) % 2 == 1)
                            continue;
                        int w = width(p.y);
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

            public void Mark(bool[] mask, Vector2 center, int radius)
            {
                int cx = Mathf.RoundToInt(center.x);
                int cy = Mathf.RoundToInt(center.y);
                for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    int px = cx + x;
                    int py = cy + y;
                    if (x * x + y * y <= radius * radius && px >= 0 && py >= 0 && px < W && py < H)
                        mask[py * W + px] = true;
                }
            }

            public void Ellipse(float cx, float cy, int rx, int ry, Color32 color)
            {
                for (int y = -ry; y <= ry; y++)
                {
                    int half = Mathf.FloorToInt(
                        rx * Mathf.Sqrt(Mathf.Max(0f, 1f - y * y / (float)(ry * ry))) + 0.35f
                    );
                    Fill(
                        Mathf.RoundToInt(cx) - half,
                        Mathf.RoundToInt(cy) + y,
                        half * 2 + 1,
                        1,
                        color
                    );
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
            public void Stamp(PixelStamp stamp, int x, int y, bool flip)
            {
                int left = x - stamp.Width / 2;
                for (int sy = 0; sy < stamp.Height; sy++)
                for (int sx = 0; sx < stamp.Width; sx++)
                {
                    var color = stamp.Pixel(flip ? stamp.Width - 1 - sx : sx, sy);
                    if (color.a == 0)
                        continue;
                    Set(left + sx, y - sy, color);
                }
            }
        }
    }
}
