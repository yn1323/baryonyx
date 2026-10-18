using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Baryonyx.Editor.Art
{
    // 縁取りのない白一色のアイコンを、64×64の設計座標（左上が原点、y は下向き）に置いた形で描く。
    // 部品は置いた順に重ね、gap を指定した部品はその周りを消してから塗り、前後の部品をすき間で分ける。
    // 細部は Cut・CutStroke でくり抜く。線の端と角は丸くなる。
    // 1ピクセルを4×4に分けて塗り、平均してなめらかな縁にする。
    public sealed class VectorIcon
    {
        public const float ViewSize = 64f;
        private const int Supersample = 4;

        private readonly List<Op> ops = new();

        public VectorIcon Fill(IconShape shape, float gap = 0f) =>
            Add(new Op(shape, true, 0f, gap, true));

        public VectorIcon Stroke(IconShape shape, float width, float gap = 0f) =>
            Add(new Op(shape, false, width, gap, true));

        public VectorIcon Cut(IconShape shape) => Add(new Op(shape, true, 0f, 0f, false));

        public VectorIcon CutStroke(IconShape shape, float width) =>
            Add(new Op(shape, false, width, 0f, false));

        private VectorIcon Add(Op op)
        {
            ops.Add(op);
            return this;
        }

        // 白で描き、使う側で色を付ける。色を付けられない場所に置くときは color を焼き込む。
        // 縮めて表示するため、ミップマップを使う。
        public void Save(string path, int size) => Save(path, size, Color.white);

        public void Save(string path, int size, Color color)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(Render(size, color));
                texture.Apply();
                ArtAssets.SavePng(path, texture, Vector4.zero, FilterMode.Bilinear, mipmaps: true);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // 下の行から並べた色（Texture2D.SetPixels の順）。
        public Color[] Render(int size) => Render(size, Color.white);

        public Color[] Render(int size, Color color)
        {
            int grid = size * Supersample;
            var covered = new bool[grid * grid];
            float unit = ViewSize / grid;
            foreach (var op in ops)
            {
                if (op.Paint && op.Gap > 0f)
                    Apply(op, op.Gap, false, covered, grid, unit);
                Apply(op, 0f, op.Paint, covered, grid, unit);
            }

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int count = 0;
                for (int sy = 0; sy < Supersample; sy++)
                for (int sx = 0; sx < Supersample; sx++)
                    if (covered[(y * Supersample + sy) * grid + x * Supersample + sx])
                        count++;
                // The grid runs top-down like the design space; textures run bottom-up.
                pixels[(size - 1 - y) * size + x] = new Color(
                    color.r,
                    color.g,
                    color.b,
                    color.a * count / (Supersample * Supersample)
                );
            }
            return pixels;
        }

        // 形（grow だけ太らせる）に入る細かい点を value にする。
        private static void Apply(
            Op op,
            float grow,
            bool value,
            bool[] covered,
            int grid,
            float unit
        )
        {
            float reach = (op.Filled ? 0f : op.Width / 2f) + grow;
            var bounds = op.Shape.Bounds();
            int x0 = Mathf.Max(0, Mathf.FloorToInt((bounds.xMin - reach) / unit));
            int x1 = Mathf.Min(grid - 1, Mathf.CeilToInt((bounds.xMax + reach) / unit));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((bounds.yMin - reach) / unit));
            int y1 = Mathf.Min(grid - 1, Mathf.CeilToInt((bounds.yMax + reach) / unit));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var point = new Vector2((x + 0.5f) * unit, (y + 0.5f) * unit);
                bool inside =
                    (op.Filled && op.Shape.Contains(point))
                    || (reach > 0f && op.Shape.DistanceToOutline(point) <= reach);
                if (inside)
                    covered[y * grid + x] = value;
            }
        }

        private readonly struct Op
        {
            public readonly IconShape Shape;
            public readonly bool Filled;
            public readonly float Width;
            public readonly float Gap;
            public readonly bool Paint;

            public Op(IconShape shape, bool filled, float width, float gap, bool paint)
            {
                Shape = shape;
                Filled = filled;
                Width = width;
                Gap = gap;
                Paint = paint;
            }
        }
    }

    // VectorIcon の部品の形。円・角丸の四角形・SVGのパス（M・L・Q・Z の絶対座標）を折れ線にして持つ。
    public sealed class IconShape
    {
        private const int CircleSegments = 96;
        private const int CornerSegments = 8;
        private const int CurveSegments = 16;

        private readonly List<Vector2[]> contours;
        private readonly List<bool> closed;

        private IconShape(List<Vector2[]> contours, List<bool> closed)
        {
            this.contours = contours;
            this.closed = closed;
        }

        public static IconShape Circle(float cx, float cy, float radius)
        {
            var points = new Vector2[CircleSegments];
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                points[i] = new Vector2(
                    cx + radius * Mathf.Cos(angle),
                    cy + radius * Mathf.Sin(angle)
                );
            }
            return new IconShape(new List<Vector2[]> { points }, new List<bool> { true });
        }

        public static IconShape Rect(
            float x,
            float y,
            float width,
            float height,
            float radius = 1.4f
        )
        {
            radius = Mathf.Min(radius, width / 2f, height / 2f);
            var points = new List<Vector2>();
            // Corners clockwise from the top left, each quarter arc around its own centre.
            var centres = new[]
            {
                new Vector2(x + radius, y + radius),
                new Vector2(x + width - radius, y + radius),
                new Vector2(x + width - radius, y + height - radius),
                new Vector2(x + radius, y + height - radius),
            };
            for (int corner = 0; corner < 4; corner++)
            {
                float start = Mathf.PI * (1f + corner * 0.5f);
                for (int i = 0; i <= CornerSegments; i++)
                {
                    float angle = start + i * Mathf.PI * 0.5f / CornerSegments;
                    points.Add(
                        centres[corner] + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                    );
                }
            }
            return new IconShape(new List<Vector2[]> { points.ToArray() }, new List<bool> { true });
        }

        public static IconShape Path(string data)
        {
            var tokens = Tokenize(data);
            var shapes = new List<Vector2[]>();
            var closedFlags = new List<bool>();
            var current = new List<Vector2>();
            int index = 0;
            char command = ' ';

            void Finish(bool isClosed)
            {
                if (current.Count > 1)
                {
                    shapes.Add(current.ToArray());
                    closedFlags.Add(isClosed);
                }
                current = new List<Vector2>();
            }

            Vector2 Next() =>
                new(
                    float.Parse(tokens[index++], CultureInfo.InvariantCulture),
                    float.Parse(tokens[index++], CultureInfo.InvariantCulture)
                );

            while (index < tokens.Count)
            {
                if (char.IsLetter(tokens[index][0]))
                    command = tokens[index++][0];
                switch (command)
                {
                    case 'M':
                        Finish(false);
                        current.Add(Next());
                        command = 'L';
                        break;
                    case 'L':
                        current.Add(Next());
                        break;
                    case 'Q':
                    {
                        var start = current[^1];
                        var control = Next();
                        var end = Next();
                        for (int i = 1; i <= CurveSegments; i++)
                        {
                            float t = i / (float)CurveSegments;
                            current.Add(
                                (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end
                            );
                        }
                        break;
                    }
                    case 'Z':
                        Finish(true);
                        break;
                    default:
                        throw new FormatException($"Unsupported path command '{command}': {data}");
                }
            }
            Finish(false);
            return new IconShape(shapes, closedFlags);
        }

        // SVG の rotate(degrees cx cy) と同じく、y が下向きの座標で時計回りに回す。
        public IconShape Rotated(float degrees, float cx, float cy)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return Map(p => new Vector2(
                cx + (p.x - cx) * cos - (p.y - cy) * sin,
                cy + (p.x - cx) * sin + (p.y - cy) * cos
            ));
        }

        public IconShape Translated(float dx, float dy) => Map(p => p + new Vector2(dx, dy));

        private IconShape Map(Func<Vector2, Vector2> map)
        {
            var mapped = new List<Vector2[]>();
            foreach (var contour in contours)
                mapped.Add(Array.ConvertAll(contour, p => map(p)));
            return new IconShape(mapped, new List<bool>(closed));
        }

        public UnityEngine.Rect Bounds()
        {
            float xMin = float.MaxValue;
            float yMin = float.MaxValue;
            float xMax = float.MinValue;
            float yMax = float.MinValue;
            foreach (var contour in contours)
            foreach (var p in contour)
            {
                xMin = Mathf.Min(xMin, p.x);
                yMin = Mathf.Min(yMin, p.y);
                xMax = Mathf.Max(xMax, p.x);
                yMax = Mathf.Max(yMax, p.y);
            }
            return UnityEngine.Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        // 閉じた輪郭の内側（重なりは nonzero で数える）。開いた輪郭も閉じたものとして扱う。
        public bool Contains(Vector2 point)
        {
            int winding = 0;
            foreach (var contour in contours)
            {
                for (int i = 0; i < contour.Length; i++)
                {
                    var a = contour[i];
                    var b = contour[(i + 1) % contour.Length];
                    float side = (b.x - a.x) * (point.y - a.y) - (point.x - a.x) * (b.y - a.y);
                    if (a.y <= point.y)
                    {
                        if (b.y > point.y && side > 0f)
                            winding++;
                    }
                    else if (b.y <= point.y && side < 0f)
                        winding--;
                }
            }
            return winding != 0;
        }

        public float DistanceToOutline(Vector2 point)
        {
            float best = float.MaxValue;
            for (int c = 0; c < contours.Count; c++)
            {
                var contour = contours[c];
                int segments = closed[c] ? contour.Length : contour.Length - 1;
                for (int i = 0; i < segments; i++)
                    best = Mathf.Min(
                        best,
                        DistanceToSegment(point, contour[i], contour[(i + 1) % contour.Length])
                    );
            }
            return best;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float length = ab.sqrMagnitude;
            float t = length > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / length) : 0f;
            return Vector2.Distance(p, a + t * ab);
        }

        private static List<string> Tokenize(string data)
        {
            var tokens = new List<string>();
            int i = 0;
            while (i < data.Length)
            {
                char c = data[i];
                if (char.IsWhiteSpace(c) || c == ',')
                {
                    i++;
                }
                else if (char.IsLetter(c))
                {
                    tokens.Add(c.ToString());
                    i++;
                }
                else
                {
                    int start = i++;
                    while (i < data.Length && (char.IsDigit(data[i]) || data[i] == '.'))
                        i++;
                    tokens.Add(data.Substring(start, i - start));
                }
            }
            return tokens;
        }
    }
}
