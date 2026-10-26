using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// On a wide card's back (<see cref="GlassCardBack"/>): a thin gold line from either side of
    /// the ornament to near the edge, threaded with gold beads and fading outwards, like the
    /// chains beside a unique skill's seal. Laid over the whole back, it draws only when the card
    /// is wider than <see cref="WideFrom"/> times its height, and leaves room for the ornament as
    /// the back's <see cref="Image.preserveAspect"/> picture fits it.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GlassCardBackLine : MaskableGraphic
    {
        // 横長とみなす幅と高さの比。
        public const float WideFrom = 1.6f;

        // 飾りを置く枠（カードに対する割合）。GlassCardAssets.AddBack の飾りと同じ。
        public const float EmblemWidth = 0.62f;
        public const float EmblemHeight = 0.72f;

        // 線の太さ、玉の間隔と直径、飾りから線までの隙間、カードの端から線の終わりまで（設計座標）。
        private const float Thickness = 2f;
        private const float Step = 24f;
        private const float Bead = 6.4f;
        private const float Gap = 8f;
        private const float EndInset = 27f;

        // 線と玉の、飾りの側での濃さ。外へ薄くなる。
        private const float LineAlpha = 0.69f;
        private const float BeadAlpha = 0.92f;

        [Tooltip("玉の形（白い円）。線は真ん中の色で塗る。")]
        public Texture BeadTexture;

        [Tooltip("飾りの絵の幅÷高さ。")]
        public float EmblemAspect = 1f;

        public override Texture mainTexture => BeadTexture != null ? BeadTexture : s_WhiteTexture;

        /// <summary>Whether a card of <paramref name="size"/> is wide enough for the lines.</summary>
        public static bool IsWide(Vector2 size) => size.y > 0f && size.x > size.y * WideFrom;

        /// <summary>The width the ornament takes on a card of <paramref name="size"/>.</summary>
        public static float EmblemDrawnWidth(Vector2 size, float aspect)
        {
            float boxWidth = size.x * EmblemWidth;
            float boxHeight = size.y * EmblemHeight;
            return Mathf.Min(boxWidth, boxHeight * aspect);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            if (!IsWide(rect.size))
                return;
            float inner = EmblemDrawnWidth(rect.size, EmblemAspect) / 2f + Gap;
            float outer = rect.width / 2f - EndInset;
            if (outer <= inner + Step)
                return;
            var center = rect.center;
            foreach (float side in new[] { -1f, 1f })
            {
                float length = outer - inner;
                // 線：飾りの側から外へ、なめらかに薄くする。
                const int pieces = 8;
                for (int i = 0; i < pieces; i++)
                {
                    float a = (float)i / pieces;
                    float b = (float)(i + 1) / pieces;
                    Quad(
                        vh,
                        center + new Vector2(side * (inner + length * a), 0f),
                        center + new Vector2(side * (inner + length * b), 0f),
                        Thickness / 2f,
                        LineAlpha * Fade(a),
                        LineAlpha * Fade(b),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f)
                    );
                }
                for (float along = Step / 2f; along < length - Step / 3f; along += Step)
                {
                    float x = side * (inner + along);
                    float alpha = BeadAlpha * Fade(along / length);
                    Quad(
                        vh,
                        center + new Vector2(x - Bead / 2f, 0f),
                        center + new Vector2(x + Bead / 2f, 0f),
                        Bead / 2f,
                        alpha,
                        alpha,
                        Vector2.zero,
                        Vector2.one
                    );
                }
            }
        }

        // 外へ向かって薄くなる割合（0で飾りの側、1で端）。
        private static float Fade(float t) => Mathf.Pow(1f - Mathf.Clamp01(t), 0.8f);

        // from から to への横長の四角形。uvMin・uvMax は玉の絵の範囲（線は真ん中の1点）。
        private void Quad(
            VertexHelper vh,
            Vector2 from,
            Vector2 to,
            float halfHeight,
            float alphaFrom,
            float alphaTo,
            Vector2 uvMin,
            Vector2 uvMax
        )
        {
            float left = Mathf.Min(from.x, to.x);
            float right = Mathf.Max(from.x, to.x);
            bool flipped = from.x > to.x;
            var c = color;
            Color32 colorLeft = new Color(c.r, c.g, c.b, c.a * (flipped ? alphaTo : alphaFrom));
            Color32 colorRight = new Color(c.r, c.g, c.b, c.a * (flipped ? alphaFrom : alphaTo));
            int start = vh.currentVertCount;
            vh.AddVert(
                new Vector3(left, from.y - halfHeight),
                colorLeft,
                new Vector2(uvMin.x, uvMin.y)
            );
            vh.AddVert(
                new Vector3(left, from.y + halfHeight),
                colorLeft,
                new Vector2(uvMin.x, uvMax.y)
            );
            vh.AddVert(
                new Vector3(right, from.y + halfHeight),
                colorRight,
                new Vector2(uvMax.x, uvMax.y)
            );
            vh.AddVert(
                new Vector3(right, from.y - halfHeight),
                colorRight,
                new Vector2(uvMax.x, uvMin.y)
            );
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
