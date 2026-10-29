using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The noise the card aura (Battle Card Aura) reads, made once in code instead of being
    /// computed for every pixel each frame, which was too heavy for a phone (2026-10-29): a
    /// small square of smooth value noise that tiles, four layers in its four channels.
    /// R is the large puffs, G and B two more of the same size (the warp of the puffs and the
    /// ribbons' waves and breaks), A finer wisps at twice the size. Nothing is saved as a file.
    /// </summary>
    public static class BattleCardAuraNoise
    {
        public const int Size = 128;

        // How many cells of the puffs fit across the square (the wisps have twice as many).
        public const int Cells = 4;

        private const int Octaves = 4;
        private const int Seed = 2910;

        private static Texture2D texture;

        public static Texture2D Texture => texture != null ? texture : texture = Make();

        private static Texture2D Make()
        {
            var made = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
            {
                name = "BattleCardAuraNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var lattices = new float[4][];
            var random = new System.Random(Seed);
            int largest = Cells * 2 << (Octaves - 1);
            for (int channel = 0; channel < 4; channel++)
            {
                lattices[channel] = new float[largest * largest * Octaves];
                for (int i = 0; i < lattices[channel].Length; i++)
                    lattices[channel][i] = (float)random.NextDouble();
            }
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (float)x / Size;
                float v = (float)y / Size;
                pixels[y * Size + x] = new Color32(
                    Byte(Fbm(lattices[0], largest, u, v, Cells)),
                    Byte(Fbm(lattices[1], largest, u, v, Cells)),
                    Byte(Fbm(lattices[2], largest, u, v, Cells)),
                    Byte(Fbm(lattices[3], largest, u, v, Cells * 2))
                );
            }
            made.SetPixels32(pixels);
            made.Apply(false, false);
            return made;
        }

        // Smooth value noise summed over octaves, each twice as fine and half as strong, every
        // octave wrapping at the square's edge so the square tiles. Spread back to 0..1.
        private static float Fbm(float[] lattice, int stride, float u, float v, int cells)
        {
            float sum = 0f;
            float total = 0f;
            float amplitude = 0.5f;
            for (int octave = 0; octave < Octaves; octave++)
            {
                int period = cells << octave;
                sum += amplitude * Noise(lattice, stride, octave, u * period, v * period, period);
                total += amplitude;
                amplitude *= 0.5f;
            }
            return Mathf.Clamp01((sum / total - 0.5f) * 1.8f + 0.5f);
        }

        private static float Noise(float[] lattice, int stride, int octave, float x, float y, int period)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float At(int cx, int cy)
            {
                cx = ((cx % period) + period) % period;
                cy = ((cy % period) + period) % period;
                return lattice[octave * stride * stride + cy * stride + cx];
            }
            float a = At(x0, y0);
            float b = At(x0 + 1, y0);
            float c = At(x0, y0 + 1);
            float d = At(x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static byte Byte(float value) => (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
    }
}
