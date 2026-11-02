using System;
using System.Linq;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.UI;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Combat.Editor
{
    // 属性の印とカードのコストの数字（doc/rules/ui-design.md#属性とコストの印）。
    // 生成り色の単純な記号の後ろに、属性の色をほんのり混ぜたやわらかい暗がりを敷く。
    // 板や縁を置かず、ドット絵の挿絵や背景の上でも浮かずに読めるようにする。
    // 記号は VectorIcon の64×64の設計座標で描き、大きく描いて縮めて表示する（Bilinear・ミップマップあり）。
    public static class AttributeIconArt
    {
        public const string Folder = "Assets/Baryonyx/Shared/Art/Attributes";
        public const string UnknownPath = Folder + "/ElementUnknown.png";
        public const string CostDigitsPath =
            "Assets/Baryonyx/Features/Combat/UI/Art/CardCostDigits.png";

        // 数字を文字で出す所（編成のスキルの一覧）の、数字の後ろに敷く暗がりだけの画像。
        public const string CostGlowPath =
            "Assets/Baryonyx/Features/Combat/UI/Art/CardCostGlow.png";

        // コストの数字のシートは0〜9を左から並べ、残りのマスを空けて幅を2のべき乗にする
        // （スマホ向けに圧縮してもミップマップを保つ）。BattleInspectCardView.CostUv が1つを切り出す。
        private const int CostDigits = Presentation.BattleInspectCardView.CostDigits;
        private const int CostSheetCells = Presentation.BattleInspectCardView.CostSheetCells;

        private const int IconSize = 128;
        private const int DigitCellSize = 128;

        // 記号の色。文字と同じ生成り色にして、画面の文字と並べたときにそろえる。
        private static readonly Color Ink = UiPalette.TextMain;

        // 記号のすぐ外側の、形に沿った暗い影。暗がりより濃くして縁を読ませる。
        // 光らせるときは、光を濁らせないよう影を薄くする。
        private static readonly Color Edge = new(0.04f, 0.03f, 0.05f);
        private const float EdgeAlpha = 0.75f;
        private const float EdgeAlphaShining = 0.45f;
        private const float EdgeBlur = 3f;

        // 記号のまわりの、属性の明るい色のにじみ（発光）。0で光らせない。
        public const float Shine = 0.3f;
        private const float ShineBlur = 4.5f;

        // 暗がりの濃さ。中央から外へ薄くなり、画像の縁で消える。
        private const float GlowCenter = 0.92f;
        private const float GlowMiddle = 0.85f;
        private const float GlowMiddleAt = 0.62f;

        private const float LinearBlendGamma = 2.2f;

        private static readonly Color CostTint = new(0.063f, 0.082f, 0.169f);
        private static readonly Color CostLight = new(0.663f, 0.761f, 1f);

        // 無属性。どの属性の色とも重ならない、中立の焦げ茶にする。
        private static readonly Color NoneTint = new(0.2f, 0.176f, 0.137f);
        private static readonly Color NoneLight = new(0.95f, 0.9f, 0.78f);
        private static readonly Color UnknownTint = new(0.165f, 0.14f, 0.192f);
        private static readonly Color UnknownLight = new(0.816f, 0.816f, 0.863f);

        public static string IconPath(CardElement element) => $"{Folder}/Element{element}.png";

        // 暗がりの色。属性の色を深く暗くしたもの（2026-11-02、ユーザーが選んだ案Vの色に合わせた）。
        // どれも同じくらい暗くし、背景の夕焼けの茶色と見分けられる濃さにする。
        public static Color Tint(CardElement element) =>
            element switch
            {
                CardElement.Fire => new Color(0.376f, 0.055f, 0.07f),
                CardElement.Ice => new Color(0.02f, 0.141f, 0.322f),
                CardElement.Thunder => new Color(0.341f, 0.216f, 0.02f),
                CardElement.Slash => new Color(0.169f, 0.161f, 0.22f),
                CardElement.Blunt => new Color(0.275f, 0.122f, 0.051f),
                CardElement.Pierce => new Color(0.086f, 0.192f, 0.114f),
                _ => NoneTint,
            };

        // 発光の色。属性の色を明るく淡くしたもの。
        public static Color Light(CardElement element) =>
            element switch
            {
                CardElement.Fire => new Color(1f, 0.604f, 0.353f),
                CardElement.Ice => new Color(0.561f, 0.816f, 1f),
                CardElement.Thunder => new Color(1f, 0.878f, 0.478f),
                CardElement.Slash => new Color(0.875f, 0.902f, 0.941f),
                CardElement.Blunt => new Color(0.941f, 0.69f, 0.478f),
                CardElement.Pierce => new Color(0.498f, 0.91f, 0.784f),
                _ => NoneLight,
            };

        // 弱点になる属性。無属性は弱点にならないため含めない。
        public static readonly CardElement[] Elements =
        {
            CardElement.Fire,
            CardElement.Ice,
            CardElement.Thunder,
            CardElement.Slash,
            CardElement.Blunt,
            CardElement.Pierce,
        };

        [MenuItem("Baryonyx/Art/Create Attribute Icons")]
        public static void CreateMenu()
        {
            EditorGuard.RequireEditMode();
            EnsureAll();
        }

        public static void EnsureAll()
        {
            AssetFolders.Ensure(Folder);
            foreach (var element in Elements.Append(CardElement.None))
                Save(IconPath(element), RenderMark(element, Shine), IconSize, IconSize);
            Save(UnknownPath, RenderUnknown(Shine), IconSize, IconSize);
            SaveCostDigits();
            Save(
                CostGlowPath,
                Render(new VectorIcon(), CostTint, CostLight, 0f, IconSize),
                IconSize,
                IconSize
            );
        }

        // ドット絵をPointで読み込み直す処理から、この画像を外すために使う。
        public static bool Owns(string path) =>
            path == UnknownPath
            || path == CostDigitsPath
            || path == CostGlowPath
            || path == IconPath(CardElement.None)
            || Array.Exists(Elements, element => IconPath(element) == path);

        public static Texture2D Load(CardElement element) =>
            ArtAssets.LoadTexture(IconPath(element));

        public static Texture2D LoadUnknown() => ArtAssets.LoadTexture(UnknownPath);

        public static Texture2D LoadCostDigits() => ArtAssets.LoadTexture(CostDigitsPath);

        private static void SaveCostDigits()
        {
            int width = DigitCellSize * CostSheetCells;
            var pixels = new Color[width * DigitCellSize];
            for (int digit = 0; digit < CostDigits; digit++)
            {
                var cell = Render(Digit(digit), CostTint, CostLight, Shine, DigitCellSize);
                for (int y = 0; y < DigitCellSize; y++)
                    Array.Copy(
                        cell,
                        y * DigitCellSize,
                        pixels,
                        y * width + digit * DigitCellSize,
                        DigitCellSize
                    );
            }
            Save(CostDigitsPath, pixels, width, DigitCellSize);
        }

        private static void Save(string path, Color[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(pixels);
                texture.Apply();
                ArtAssets.SavePng(path, texture, Vector4.zero, FilterMode.Bilinear, mipmaps: true);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // 下の行から並べた色（Texture2D.SetPixels の順）。見比べの画像を作るときにも使う。
        public static Color[] RenderMark(CardElement element, float shine) =>
            Render(Symbol(element), Tint(element), Light(element), shine, IconSize);

        public static Color[] RenderUnknown(float shine) =>
            Render(Unknown(), UnknownTint, UnknownLight, shine, IconSize);

        public static Color[] RenderDigit(int digit, float shine) =>
            Render(Digit(digit), CostTint, CostLight, shine, DigitCellSize);

        // 暗がり → 形に沿った影 → 発光 → 記号の順に重ねる（色はまっすぐなアルファで合成する）。
        private static Color[] Render(
            VectorIcon symbol,
            Color tint,
            Color light,
            float shine,
            int size
        )
        {
            var ink = symbol.Render(size);
            var coverage = new float[ink.Length];
            for (int i = 0; i < ink.Length; i++)
                coverage[i] = ink[i].a;
            float blur = EdgeBlur * size / VectorIcon.ViewSize;
            var edge = Blur(Blur(coverage, size, blur), size, blur);
            float spread = ShineBlur * size / VectorIcon.ViewSize;
            var halo = Blur(Blur(coverage, size, spread), size, spread);
            float edgeAlpha = shine > 0f ? EdgeAlphaShining : EdgeAlpha;

            var pixels = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                var color = new Color(tint.r, tint.g, tint.b, Glow(Mathf.Sqrt(dx * dx + dy * dy)));
                color = Over(
                    new Color(Edge.r, Edge.g, Edge.b, Mathf.Clamp01(edge[i] * 2.4f) * edgeAlpha),
                    color
                );
                color = Over(
                    new Color(light.r, light.g, light.b, Mathf.Clamp01(halo[i] * 1.8f) * shine),
                    color
                );
                color = Over(new Color(Ink.r, Ink.g, Ink.b, coverage[i]), color);
                // プロジェクトはリニア空間で重ねるため、半透明の暗がりから背景がsRGBで重ねたときより
                // ずっと多く透け、選んだ見本より薄く見える。不透明度を 1 - (1 - a)^2.2 に上げ、
                // sRGBで重ねたときと同じくらい背景を暗くする。
                color.a = 1f - Mathf.Pow(1f - color.a, LinearBlendGamma);
                pixels[i] = color;
            }
            return pixels;
        }

        private static float Glow(float distance)
        {
            if (distance >= 1f)
                return 0f;
            if (distance <= GlowMiddleAt)
                return Mathf.Lerp(GlowCenter, GlowMiddle, distance / GlowMiddleAt);
            float t = (distance - GlowMiddleAt) / (1f - GlowMiddleAt);
            return GlowMiddle * (1f - Mathf.SmoothStep(0f, 1f, t));
        }

        private static Color Over(Color top, Color bottom)
        {
            float alpha = top.a + bottom.a * (1f - top.a);
            if (alpha <= 0f)
                return Color.clear;
            var rgb = ((Vector4)top * top.a + (Vector4)bottom * bottom.a * (1f - top.a)) / alpha;
            return new Color(rgb.x, rgb.y, rgb.z, alpha);
        }

        // 横と縦の箱ぼかし。
        private static float[] Blur(float[] source, int size, float radius)
        {
            int r = Mathf.Max(1, Mathf.RoundToInt(radius));
            var across = new float[source.Length];
            var result = new float[source.Length];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float sum = 0f;
                for (int k = -r; k <= r; k++)
                    sum += source[y * size + Mathf.Clamp(x + k, 0, size - 1)];
                across[y * size + x] = sum / (2 * r + 1);
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float sum = 0f;
                for (int k = -r; k <= r; k++)
                    sum += across[Mathf.Clamp(y + k, 0, size - 1) * size + x];
                result[y * size + x] = sum / (2 * r + 1);
            }
            return result;
        }

        // 1目で分かる最小限の形にする。細い線を使わず、太さは小さく表示しても潰れない5以上にする。
        internal static VectorIcon Symbol(CardElement element) =>
            element switch
            {
                CardElement.Fire => new VectorIcon()
                    .Fill(
                        IconShape.Path(
                            "M32 12 Q46 26 46 39 Q46 52 32 52 Q18 52 18 39 Q18 31 24 25 Q25 32 29 33 Q27 22 32 12 Z"
                        )
                    )
                    .Cut(
                        IconShape.Path(
                            "M32 35 Q37 40 37 44.5 Q37 49 32 49 Q27 49 27 44.5 Q27 40 32 35 Z"
                        )
                    ),
                CardElement.Ice => Snowflake(),
                CardElement.Thunder => new VectorIcon().Fill(
                    IconShape.Path("M37 11 L19 35 L30 35 L26 53 L45 27 L34 27 L39 11 Z")
                ),
                CardElement.Slash => Sword(),
                CardElement.Blunt => new VectorIcon()
                    .Fill(IconShape.Rect(17f, 13f, 30f, 13f, 2.5f).Rotated(-40f, 32f, 32f))
                    .Fill(IconShape.Rect(29f, 26f, 6f, 26f, 2f).Rotated(-40f, 32f, 32f)),
                CardElement.Pierce => new VectorIcon()
                    .Stroke(IconShape.Path("M17 47 L40 24"), 5f)
                    .Fill(IconShape.Path("M48 16 L45 33 L31 19 Z")),
                // 無属性は、何も宿らないことを表す太い輪（2026-11-03、ユーザーが4つの候補から選んだ）。
                _ => new VectorIcon().Stroke(IconShape.Circle(32f, 32f, 13f), 6f),
            };

        private static VectorIcon Snowflake()
        {
            var icon = new VectorIcon();
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f;
                icon.Stroke(IconShape.Path("M32 32 L32 13").Rotated(angle, 32f, 32f), 5f)
                    .Stroke(IconShape.Path("M26 17 L32 22 L38 17").Rotated(angle, 32f, 32f), 4f);
            }
            return icon;
        }

        // 刃先を右上へ向けた剣。
        private static VectorIcon Sword()
        {
            const float angle = 45f;
            return new VectorIcon()
                .Fill(
                    IconShape
                        .Path("M32 9 L36.5 15 L36.5 38 L27.5 38 L27.5 15 Z")
                        .Rotated(angle, 32f, 32f)
                )
                .Fill(IconShape.Rect(21f, 38f, 22f, 5f).Rotated(angle, 32f, 32f))
                .Fill(IconShape.Rect(29.5f, 43f, 5f, 8f).Rotated(angle, 32f, 32f))
                .Fill(IconShape.Circle(32f, 53.5f, 3.3f).Rotated(angle, 32f, 32f));
        }

        private static VectorIcon Unknown() =>
            new VectorIcon()
                .Stroke(
                    IconShape.Path("M24 24 Q24 14 32 14 Q40 14 40 22 Q40 28 34 31 Q32 32.5 32 37"),
                    6f
                )
                .Fill(IconShape.Circle(32f, 47f, 3.8f));

        // 丸みのある太い数字。Kiwi Maru の丸さに合わせ、線の端を丸くする。
        private static readonly string[] DigitPaths =
        {
            "M32 16 Q42 16 42 32 Q42 48 32 48 Q22 48 22 32 Q22 16 32 16 Z",
            "M26 21 L33 16 L33 48",
            "M23 23 Q24 16 32 16 Q41 16 41 24 Q41 31 33 37 L23 47 L42 47",
            "M23 19 Q27 16 32 16 Q41 16 41 23.5 Q41 31 32 31 Q42 31 42 39.5 Q42 48 32 48 Q26 48 22 45",
            "M37 48 L37 16 L21 38 L43 38",
            "M41 16 L25 16 L23.5 30 Q27 28 32 28 Q42 28 42 38 Q42 48 32 48 Q26 48 22 45",
            "M39 17 Q36 16 33 16 Q22 16 22 33 Q22 48 32 48 Q42 48 42 39 Q42 30 32 30 Q24 30 22.5 37",
            "M22 16 L42 16 L29 48",
            "M32 31 Q23 31 23 23.5 Q23 16 32 16 Q41 16 41 23.5 Q41 31 32 31 Z M32 31 Q42 31 42 39.5 Q42 48 32 48 Q22 48 22 39.5 Q22 31 32 31 Z",
            "M25 47 Q28 48 31 48 Q42 48 42 31 Q42 16 32 16 Q22 16 22 25 Q22 34 32 34 Q40 34 41.5 27",
        };

        private const float DigitStroke = 7f;

        internal static VectorIcon Digit(int digit) =>
            new VectorIcon().Stroke(IconShape.Path(DigitPaths[digit]), DigitStroke);
    }
}
