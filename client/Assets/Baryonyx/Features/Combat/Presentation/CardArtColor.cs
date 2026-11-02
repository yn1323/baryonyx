using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The colour of a card's art, for the light of an effect that should match the picture (the
    /// evolution's circle, cracks, crystals and rays): read from the art itself the first time it
    /// is asked for and kept for that texture. The art is drawn small on the GPU and read back, so
    /// the texture need not be readable. Grey, white and dark pixels are left out and the most
    /// common vivid hue wins; it is given back bright, as a glow of that hue.
    /// </summary>
    public static class CardArtColor
    {
        // The art is read at this size: enough for its broad colours, cheap to read back.
        public const int SampleSize = 32;

        // Pixels paler or darker than these say nothing of the art's colour.
        public const float MinSaturation = 0.22f;
        public const float MinValue = 0.2f;

        // The hue is chosen among this many bins round the colour wheel.
        public const int HueBins = 24;

        // The colour given back: the hue found, kept between these saturations, at full brightness.
        public const float LowSaturation = 0.45f;
        public const float HighSaturation = 0.85f;

        private static readonly Dictionary<Texture, Color> found = new();
        private static Texture2D reader;

        /// <summary>
        /// The colour of <paramref name="art"/>, or <paramref name="fallback"/> when there is no
        /// art or nothing in it is vivid enough to tell.
        /// </summary>
        public static Color Of(Texture art, Color fallback)
        {
            if (art == null)
                return fallback;
            if (!found.TryGetValue(art, out var color))
            {
                color = Read(art) ?? new Color(0f, 0f, 0f, 0f);
                found[art] = color;
            }
            return color.a > 0f ? color : fallback;
        }

        /// <summary>Forgets the colours read so far (when the art may have been replaced).</summary>
        public static void Forget() => found.Clear();

        // Draws the art small, reads it back and picks its colour; null when none stands out.
        private static Color? Read(Texture art)
        {
            var target = RenderTexture.GetTemporary(
                SampleSize,
                SampleSize,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB
            );
            var active = RenderTexture.active;
            try
            {
                Graphics.Blit(art, target);
                RenderTexture.active = target;
                if (reader == null)
                    reader = new Texture2D(SampleSize, SampleSize, TextureFormat.RGBA32, false)
                    {
                        name = "CardArtColorReader",
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                reader.ReadPixels(new Rect(0, 0, SampleSize, SampleSize), 0, 0);
                reader.Apply();
                return Pick(reader.GetPixels32());
            }
            finally
            {
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>
        /// The colour of <paramref name="pixels"/>: the hue bin holding the most vivid weight (each
        /// pixel weighed by its alpha, saturation squared and brightness, neighbouring bins counting
        /// half), averaged within it and given back bright; null when no pixel is vivid enough.
        /// </summary>
        public static Color? Pick(IReadOnlyList<Color32> pixels)
        {
            var bins = new float[HueBins];
            var hues = new List<(float Hue, float Saturation, float Weight)>(pixels.Count);
            foreach (var pixel in pixels)
            {
                Color.RGBToHSV(pixel, out float h, out float s, out float v);
                if (s < MinSaturation || v < MinValue || pixel.a < 32)
                    continue;
                float weight = pixel.a / 255f * s * s * v;
                bins[Mathf.Min(HueBins - 1, (int)(h * HueBins))] += weight;
                hues.Add((h, s, weight));
            }
            if (hues.Count == 0)
                return null;

            int best = 0;
            float most = -1f;
            for (int i = 0; i < HueBins; i++)
            {
                float score =
                    bins[i]
                    + 0.5f * (bins[(i + HueBins - 1) % HueBins] + bins[(i + 1) % HueBins]);
                if (score > most)
                {
                    most = score;
                    best = i;
                }
            }

            // The mean hue (round the wheel) and saturation of the pixels in and beside the bin.
            float center = (best + 0.5f) / HueBins;
            float x = 0f;
            float y = 0f;
            float saturation = 0f;
            float total = 0f;
            foreach (var (hue, s, weight) in hues)
            {
                float apart = Mathf.Abs(Mathf.DeltaAngle(hue * 360f, center * 360f));
                if (apart > 1.5f * 360f / HueBins)
                    continue;
                float angle = hue * Mathf.PI * 2f;
                x += Mathf.Cos(angle) * weight;
                y += Mathf.Sin(angle) * weight;
                saturation += s * weight;
                total += weight;
            }
            if (total <= 0f)
                return null;
            float mean = Mathf.Atan2(y, x) / (Mathf.PI * 2f);
            if (mean < 0f)
                mean += 1f;
            return Color.HSVToRGB(
                mean,
                Mathf.Clamp(saturation / total, LowSaturation, HighSaturation),
                1f
            );
        }
    }
}
