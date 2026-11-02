using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// Soft shapes of light made in code (no image is used; doc/art/direction.md#エフェクトの描き方),
    /// white so a <see cref="UnityEngine.UI.RawImage"/> tints them: a thin line with a bright
    /// middle that fades out to its sides and its ends, and a round glow that fades out from its
    /// middle. They are made the first time they are asked for and kept while the app runs.
    /// </summary>
    public static class SoftLight
    {
        private const int LineWidth = 16;
        private const int LineHeight = 64;
        private const int GlowSize = 64;

        private static Texture2D line;
        private static Texture2D glow;

        /// <summary>An upright line: bright down its middle, soft at its sides, tapering at both ends.</summary>
        public static Texture2D Line => line != null ? line : line = MakeLine();

        /// <summary>A round glow, brightest in the middle and gone at its edge.</summary>
        public static Texture2D Glow => glow != null ? glow : glow = MakeGlow();

        private static Texture2D MakeLine()
        {
            var texture = New(LineWidth, LineHeight);
            float half = (LineWidth - 1) / 2f;
            for (int y = 0; y < LineHeight; y++)
            {
                float v = y / (LineHeight - 1f);
                // 両端は細くなって消える。
                float along =
                    Mathf.SmoothStep(0f, 1f, v / 0.25f)
                    * Mathf.SmoothStep(0f, 1f, (1f - v) / 0.25f);
                for (int x = 0; x < LineWidth; x++)
                {
                    float d = (x - half) / half;
                    float across = Mathf.Exp(-d * d * 6f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, across * along));
                }
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D MakeGlow()
        {
            var texture = New(GlowSize, GlowSize);
            float half = (GlowSize - 1) / 2f;
            for (int y = 0; y < GlowSize; y++)
            for (int x = 0; x < GlowSize; x++)
            {
                float r = Mathf.Clamp01(new Vector2(x - half, y - half).magnitude / half);
                float alpha = Mathf.Exp(-r * r * 3f) * (1f - Mathf.SmoothStep(0.75f, 1f, r));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D New(int width, int height) =>
            new(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
    }
}
