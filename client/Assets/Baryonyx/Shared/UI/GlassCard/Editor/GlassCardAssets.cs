using System;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Builds the frosted-glass card (<see cref="GlassCard"/>) that the formation's adventurers
    /// use, for any screen that lists things the player taps or holds: a translucent rounded
    /// plate, a rim lit from the top left, the quarter of a rose window fading out from the
    /// top-left corner, and a soft shadow. The chosen card adds a gold rim and glow. The images
    /// are drawn in code and saved as PNGs, so the showcase lists them and rebuilding gives the
    /// same pictures. Colours are white in the images and tinted where they are used.
    /// </summary>
    public static class GlassCardAssets
    {
        public const string Folder = "Assets/Baryonyx/Shared/UI/GlassCard/Art";
        public const string FillPath = Folder + "/GlassCardFill.png";
        public const string RimPath = Folder + "/GlassCardRim.png";
        public const string ShadowPath = Folder + "/GlassCardShadow.png";
        public const string GlowPath = Folder + "/GlassCardGlow.png";
        public const string SelectedRimPath = Folder + "/GlassCardSelectedRim.png";
        public const string DashedPath = Folder + "/GlassCardDashed.png";
        public const string RosePath = Folder + "/GlassCardRose.png";

        // 裏面：革の地と箔押しの飾り（生成画像。client/ArtSource/UI/GlassCard）、内側の金の線と左右の線の玉（コードで描く）。
        public const string BackLeatherPath = Folder + "/GlassCardBackLeather.png";
        public const string BackEmblemPath = Folder + "/GlassCardBackEmblem.png";
        public const string BackRingPath = Folder + "/GlassCardBackRing.png";
        public const string BackBeadPath = Folder + "/GlassCardBackBead.png";

        // 角丸の半径（1ピクセル = Canvasの1単位）。
        public const int Radius = 22;

        public static readonly Color FillColor = new(0.62f, 0.72f, 1f, 0.05f);
        public static readonly Color RoseColor = new(1f, 0.85f, 0.55f, 0.5f);
        public static readonly Color GoldColor = new(1f, 0.84f, 0.4f, 1f);
        public static readonly Color ShadowColor = new(0f, 0f, 0f, 0.55f);

        // 裏面の内側の線と、左右の線と玉の古金色。
        public static readonly Color BackRingColor = new(0.87f, 0.77f, 0.54f, 0.22f);
        public static readonly Color BackLineColor = new(0.77f, 0.66f, 0.44f, 1f);

        // 案内人の画面の透ける面を通さず、景色や絵の上に直に置くカードの板の色。面と同じ紺を濃く敷き、
        // 下の景色に負けずに文字が読めるようにする（探索の札・ダイアログの外の画面・デバッグルーム）。
        public static readonly Color SceneFillColor = new(0.035f, 0.05f, 0.12f, 0.82f);

        // 影は少し下へずらし、周りへ広げる。選んだときの光も周りへ広げる。
        private const float ShadowDrop = 10f;
        private const float ShadowSpread = 14f;
        private const float GlowSpread = 16f;

        // 左上のばら窓の模様。角から広がり、この大きさの中で消える。
        public const int RoseSize = 256;

        private const int PlateSize = 64;
        private const int PlateBorder = 26;

        // 裏面の内側の線の、カードの縁からの距離と、角丸を小さく線を細くする倍率（Image.pixelsPerUnitMultiplier）。
        private const float BackRingInset = 9f;
        private const float BackRingScale = 1.5f;
        private const int BackBeadSize = 16;

        // 飾りを置く枠（カードに対する割合）。左右の線は、この枠に収めた飾りを避けて引く。
        private static readonly Vector2 BackEmblemMin = new(
            (1f - GlassCardBackLine.EmblemWidth) / 2f,
            (1f - GlassCardBackLine.EmblemHeight) / 2f
        );
        private const int ShadowSize = 112;
        private const int ShadowBorder = 48;

        /// <summary>Writes the card's images if they are missing (or <paramref name="force"/>).</summary>
        public static void EnsureArt(bool force = false)
        {
            AssetFolders.Ensure(Folder);
            if (!force && AssetDatabase.LoadAssetAtPath<Sprite>(RosePath) != null)
                return;
            ArtAssets.WriteRounded(
                FillPath,
                PlateSize,
                Radius,
                0f,
                Vector4.one * PlateBorder,
                _ => Color.white
            );
            WriteRim();
            WriteRing(SelectedRimPath, 4f);
            WriteDashed();
            ArtAssets.ImportSprite(
                DashedPath,
                Vector4.one * PlateBorder,
                FilterMode.Bilinear,
                fullRect: true
            );
            WriteSoft(ShadowPath, 12f);
            WriteGlow();
            WriteRose();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>
        /// Turns <paramref name="rect"/> (already placed by the caller) into a card and returns it
        /// with the rect to put its contents in. The contents sink with the plate when pressed.
        /// A <paramref name="dashed"/> card (an empty place, or an action such as "remove") has a
        /// dashed rim and no pattern; small cards (equipment slots) leave the pattern out.
        /// A card straight over the scene passes <see cref="SceneFillColor"/> as <paramref name="fill"/>.
        /// </summary>
        public static (GlassCard Card, RectTransform Content) Build(
            RectTransform rect,
            bool dashed = false,
            bool pattern = true,
            Color? fill = null
        )
        {
            EnsureArt();
            var card = rect.gameObject.AddComponent<GlassCard>();
            // カード全体で押せるようにする（見えない板）。
            AddImage(rect, Color.clear, true);
            card.Group = rect.gameObject.AddComponent<CanvasGroup>();
            var (body, shadow, _, content, selection) = Visuals(rect, dashed, pattern, fill);
            card.Body = body;
            card.Shadow = shadow;
            card.Selection = selection;
            return (card, content);
        }

        /// <summary>
        /// A card that a <see cref="Button"/> drives, for the views that bind buttons and turn a
        /// "Selected" object on (the formation's page and change screens): the card's look and its
        /// press, with the button's click and the gold rim as <c>Selected</c> (hidden).
        /// </summary>
        public static (Button Button, GameObject Selected, RectTransform Content) Choice(
            RectTransform rect,
            bool dashed = false,
            bool pattern = true,
            Color? fill = null
        )
        {
            var (card, content) = Build(rect, dashed, pattern, fill);
            var plate = card.Body.Find("Fill").GetComponent<Image>();
            var button = ConfigureButton(rect.gameObject.AddComponent<Button>(), plate);
            var colors = button.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.7f);
            button.colors = colors;
            return (button, card.Selection, content);
        }

        /// <summary>
        /// The card's look without taking taps, for boxes that are read only (passives, locked
        /// slots). Returns the rect to put the contents in.
        /// </summary>
        public static RectTransform Plaque(
            RectTransform rect,
            bool dashed = false,
            bool pattern = true,
            Color? fill = null
        )
        {
            EnsureArt();
            return Visuals(rect, dashed, pattern, fill).Content;
        }

        /// <summary>
        /// The lit rim of a card built on <paramref name="rect"/>, for a card whose rim shows a
        /// colour of its own (the quality of what was found, the ranks).
        /// </summary>
        public static Image RimOf(RectTransform rect) =>
            rect.Find("Body/Rim").GetComponent<Image>();

        /// <summary>The plate of a card built on <paramref name="rect"/>, e.g. to mask a picture to its shape.</summary>
        public static Image FillOf(RectTransform rect) =>
            rect.Find("Body/Fill").GetComponent<Image>();

        /// <summary>
        /// Gives <paramref name="card"/> a back over its contents, face up (hidden) until a list
        /// covers it (<see cref="GlassCardCover"/>): the leather tiled to the card's shape, a thin
        /// gold line inside the edge, the gold-tooled ornament in the middle, and on a wide card
        /// gold lines threaded with beads to either side. Its parts follow the card's size, so the
        /// same back fits every list.
        /// </summary>
        public static GlassCardBack AddBack(GlassCard card)
        {
            EnsureBackArt();
            var face = Rect("Back", card.Body);
            Stretch(face);

            var leather = face.gameObject.AddComponent<Image>();
            leather.sprite = ArtAssets.LoadSprite(BackLeatherPath);
            leather.type = Image.Type.Tiled;
            leather.raycastTarget = false;

            var ring = Rect("Ring", face);
            Stretch(ring);
            ring.offsetMin = Vector2.one * BackRingInset;
            ring.offsetMax = -Vector2.one * BackRingInset;
            var line = Plate(ring, BackRingPath, BackRingColor);
            line.pixelsPerUnitMultiplier = BackRingScale;

            var emblemSprite = ArtAssets.LoadSprite(BackEmblemPath);
            var lines = Rect("Lines", face);
            Stretch(lines);
            var graphic = lines.gameObject.AddComponent<GlassCardBackLine>();
            graphic.color = BackLineColor;
            graphic.raycastTarget = false;
            graphic.BeadTexture = ArtAssets.LoadSprite(BackBeadPath).texture;
            graphic.EmblemAspect = emblemSprite.rect.width / emblemSprite.rect.height;

            var emblem = Rect("Emblem", face);
            emblem.anchorMin = BackEmblemMin;
            emblem.anchorMax = Vector2.one - BackEmblemMin;
            emblem.offsetMin = emblem.offsetMax = Vector2.zero;
            var picture = emblem.gameObject.AddComponent<Image>();
            picture.sprite = emblemSprite;
            picture.preserveAspect = true;
            picture.raycastTarget = false;

            // 革の上にも、すりガラスと同じ左上から照らした縁を重ねる。
            Plate(Stretched("Rim", face), RimPath, Color.white);
            face.gameObject.SetActive(false);

            var back = card.gameObject.AddComponent<GlassCardBack>();
            back.Card = card;
            back.Face = face.gameObject;
            return back;
        }

        // 裏面の画像：コードで描く線と玉がなければ描き、生成画像の革と飾りの読み込みを揃える。
        private static void EnsureBackArt()
        {
            EnsureArt();
            if (AssetDatabase.LoadAssetAtPath<Sprite>(BackBeadPath) == null)
            {
                WriteRing(BackRingPath, 1.5f);
                ArtAssets.WriteSmooth(
                    BackBeadPath,
                    BackBeadSize,
                    BackBeadSize,
                    Vector4.zero,
                    (u, v) =>
                    {
                        float r = new Vector2(u - 0.5f, v - 0.5f).magnitude * BackBeadSize;
                        return new Color(1f, 1f, 1f, Mathf.Clamp01(BackBeadSize / 2f - 0.5f - r));
                    }
                );
            }
            ImportBackPicture(BackLeatherPath, Vector4.one * PlateBorder, fullRect: true, false);
            ImportBackPicture(BackEmblemPath, Vector4.zero, fullRect: false, true);
        }

        // 生成画像をSpriteとして読み込む。並べる革は四角形全体を使い、縮めて出す飾りはミップマップを使う。
        private static void ImportBackPicture(
            string path,
            Vector4 border,
            bool fullRect,
            bool mipmaps
        )
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (
                importer.textureType == TextureImporterType.Sprite
                && importer.spriteBorder == border
                && importer.mipmapEnabled == mipmaps
                && importer.filterMode == FilterMode.Bilinear
            )
                return;
            ArtAssets.ImportSprite(path, border, FilterMode.Bilinear, fullRect, mipmaps);
        }

        private static (
            RectTransform Body,
            RectTransform Shadow,
            Image Fill,
            RectTransform Content,
            GameObject Selection
        ) Visuals(RectTransform rect, bool dashed, bool pattern, Color? plate)
        {
            RectTransform shadow = null;
            if (!dashed)
            {
                shadow = Rect("Shadow", rect);
                Inflate(shadow, ShadowSpread, new Vector2(0f, -ShadowDrop));
                Plate(shadow, ShadowPath, ShadowColor);
            }

            var body = Rect("Body", rect);
            Stretch(body);
            var fill = Plate(
                Stretched("Fill", body),
                FillPath,
                plate ?? (dashed ? new Color(1f, 1f, 1f, 0.04f) : FillColor)
            );

            var tint = Stretched("SelectedTint", body);
            Plate(tint, FillPath, new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.1f));
            tint.gameObject.SetActive(false);

            if (!dashed && pattern)
            {
                var rose = Rect("Pattern", body);
                Corner(rose, new Vector2(0, 1), Vector2.zero, Vector2.one * RoseSize);
                var image = rose.gameObject.AddComponent<Image>();
                image.sprite = ArtAssets.LoadSprite(RosePath);
                image.color = RoseColor;
                image.raycastTarget = false;
            }

            if (dashed)
            {
                var line = Plate(
                    Stretched("Rim", body),
                    DashedPath,
                    new Color(1f, 0.93f, 0.78f, 0.45f)
                );
                line.type = Image.Type.Tiled;
            }
            else
                Plate(Stretched("Rim", body), RimPath, Color.white);

            var content = Stretched("Content", body);

            // 選んだときの金の縁と、外へにじむ光。中身の上に重ねる。
            var selection = Rect("Selection", rect);
            Stretch(selection);
            var glow = Rect("Glow", selection);
            Inflate(glow, GlowSpread, Vector2.zero);
            Plate(glow, GlowPath, new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.85f));
            Plate(Stretched("Rim", selection), SelectedRimPath, GoldColor);
            selection.gameObject.AddComponent<GlassCardSelection>().Tint = tint.gameObject;
            selection.gameObject.SetActive(false);
            return (body, shadow, fill, content, selection.gameObject);
        }

        private static RectTransform Stretched(string name, RectTransform parent)
        {
            var rect = Rect(name, parent);
            Stretch(rect);
            return rect;
        }

        private static void Inflate(RectTransform rect, float spread, Vector2 offset)
        {
            Stretch(rect);
            rect.offsetMin = new Vector2(-spread, -spread) + offset;
            rect.offsetMax = new Vector2(spread, spread) + offset;
        }

        private static Image Plate(RectTransform rect, string path, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ArtAssets.LoadSprite(path);
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // --- Images --------------------------------------------------------------------

        // 角丸の外側からの距離（内側は負）。u・vは0〜1、sizeはピクセル。
        private static float Outside(float u, float v, int size, float radius)
        {
            float half = size / 2f;
            float px = Mathf.Abs(u * size - half) - (half - radius);
            float py = Mathf.Abs(v * size - half) - (half - radius);
            return new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude
                + Mathf.Min(Mathf.Max(px, py), 0f)
                - radius;
        }

        // 左上から光が当たったような縁：上と左は明るく、下と右は暗い。
        private static void WriteRim()
        {
            ArtAssets.WriteSmooth(
                RimPath,
                PlateSize,
                PlateSize,
                Vector4.one * PlateBorder,
                (u, v) =>
                {
                    float outside = Outside(u, v, PlateSize, Radius);
                    float alpha = Mathf.Clamp01(0.5f - outside) * Mathf.Clamp01(outside + 2.5f);
                    // 近い辺ほど重く混ぜる（v = 1が上端）。
                    float top = Mathf.Exp(-(1f - v) * 8f);
                    float bottom = Mathf.Exp(-v * 8f);
                    float left = Mathf.Exp(-u * 8f);
                    float right = Mathf.Exp(-(1f - u) * 8f);
                    float lit =
                        (top * 0.78f + left * 0.62f + right * 0.16f + bottom * 0.1f)
                        / (top + left + right + bottom);
                    return new Color(1f, 1f, 1f, alpha * lit);
                }
            );
        }

        private static void WriteRing(string path, float width)
        {
            ArtAssets.WriteRounded(
                path,
                PlateSize,
                Radius,
                width,
                Vector4.one * PlateBorder,
                _ => Color.white
            );
        }

        // 点線の縁。辺は繰り返して並べる（Image.Type.Tiled）ので、真ん中の12ピクセルに1つの点を描く。
        private static void WriteDashed()
        {
            const int size = PlateBorder * 2 + 12;
            ArtAssets.WriteSmooth(
                DashedPath,
                size,
                size,
                Vector4.one * PlateBorder,
                (u, v) =>
                {
                    float outside = Outside(u, v, size, Radius);
                    float alpha = Mathf.Clamp01(0.5f - outside) * Mathf.Clamp01(outside + 2.5f);
                    float x = u * size;
                    float y = v * size;
                    bool edgeX = x >= PlateBorder && x < size - PlateBorder;
                    bool edgeY = y >= PlateBorder && y < size - PlateBorder;
                    float along =
                        edgeX ? x - PlateBorder
                        : edgeY ? y - PlateBorder
                        : -1f;
                    if (along >= 0f && along >= 7f)
                        alpha = 0f;
                    return new Color(1f, 1f, 1f, alpha);
                }
            );
        }

        // 中が濃く、外へなめらかに消える角丸（影）。
        private static void WriteSoft(string path, float blur)
        {
            ArtAssets.WriteSmooth(
                path,
                ShadowSize,
                ShadowSize,
                Vector4.one * ShadowBorder,
                (u, v) =>
                {
                    // カードの縁からの距離（板の周りへ ShadowSpread だけ広げた絵の端で0になる）。
                    float outside = Outside(u, v, ShadowSize, Radius + ShadowSpread) + ShadowSpread;
                    float alpha =
                        1f
                        - Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(-blur * 0.5f, ShadowSpread, outside)
                        );
                    return new Color(1f, 1f, 1f, alpha);
                }
            );
        }

        // 選んだカードの縁から外へにじむ光。縁のすぐ外が一番明るい。
        private static void WriteGlow()
        {
            ArtAssets.WriteSmooth(
                GlowPath,
                ShadowSize,
                ShadowSize,
                Vector4.one * ShadowBorder,
                (u, v) =>
                {
                    float outside = Outside(u, v, ShadowSize, Radius + GlowSpread) + GlowSpread;
                    float alpha =
                        outside < 0f
                            ? Mathf.Clamp01(1f + outside / 4f)
                            : Mathf.Pow(Mathf.Clamp01(1f - outside / GlowSpread), 2f);
                    return new Color(1f, 1f, 1f, alpha * 0.8f);
                }
            );
        }

        // 左上の角を中心にした、ばら窓の4分の1：同心の弧、放射の線、弧の間の小さな円。
        // 角から離れるほど薄くし、カードの角丸の外は描かない。
        private static void WriteRose()
        {
            int[] rings = { 36, 72, 108, 150, 196, 244 };
            var petals = new (float Radius, float Size)[] { (90f, 12f), (172f, 16f) };
            ArtAssets.WriteSmooth(
                RosePath,
                RoseSize,
                RoseSize,
                Vector4.zero,
                (u, v) =>
                {
                    float x = u * RoseSize;
                    float y = (1f - v) * RoseSize;
                    float r = Mathf.Sqrt(x * x + y * y);
                    float d = float.MaxValue;
                    foreach (int ring in rings)
                        d = Mathf.Min(d, Mathf.Abs(r - ring));
                    if (r >= rings[0] && r <= rings[^1])
                        for (int k = 0; k <= 90; k += 15)
                        {
                            float t = k * Mathf.Deg2Rad;
                            // 放射の線までの距離（角の中心を通る直線）。
                            d = Mathf.Min(d, Mathf.Abs(x * Mathf.Sin(t) - y * Mathf.Cos(t)));
                        }
                    for (int k = 0; k < 90; k += 15)
                    {
                        float t = (k + 7.5f) * Mathf.Deg2Rad;
                        foreach (var (radius, size) in petals)
                        {
                            float cx = radius * Mathf.Cos(t);
                            float cy = radius * Mathf.Sin(t);
                            float pr = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                            d = Mathf.Min(d, Mathf.Abs(pr - size));
                        }
                    }
                    float line = Mathf.Clamp01(1f - (d - 0.8f));
                    float fade = Mathf.Pow(
                        Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y * 1.3f) / 250f),
                        1.3f
                    );
                    // カードの左上の角丸の外は描かない。
                    if (x < Radius && y < Radius)
                    {
                        float cx = Radius - x;
                        float cy = Radius - y;
                        if (Mathf.Sqrt(cx * cx + cy * cy) > Radius - 1f)
                            fade = 0f;
                    }
                    return new Color(1f, 1f, 1f, line * fade * 0.6f);
                }
            );
        }
    }
}
