using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 覚醒先を選ぶ画面の後光の靄が読むノイズの絵（BattleCardAuraNoise）。継ぎ目なく並び、どの層も
    // 濃淡の幅を持つ。
    public sealed class BattleCardAuraNoiseTests
    {
        [Test]
        public void TheNoiseTilesAndEveryLayerVaries()
        {
            var texture = BattleCardAuraNoise.Texture;
            Assert.That(texture.width, Is.EqualTo(BattleCardAuraNoise.Size));
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(BattleCardAuraNoise.Texture, Is.SameAs(texture), "It is made once.");

            var pixels = texture.GetPixels32();
            int size = BattleCardAuraNoise.Size;
            for (int channel = 0; channel < 4; channel++)
            {
                int low = 255;
                int high = 0;
                int seam = 0;
                for (int i = 0; i < size; i++)
                {
                    // The last column runs on into the first, and the last row into the first.
                    seam = Mathf.Max(
                        seam,
                        Mathf.Abs(At(pixels[i * size + size - 1], channel) - At(pixels[i * size], channel)),
                        Mathf.Abs(At(pixels[(size - 1) * size + i], channel) - At(pixels[i], channel))
                    );
                    for (int j = 0; j < size; j++)
                    {
                        int value = At(pixels[i * size + j], channel);
                        low = Mathf.Min(low, value);
                        high = Mathf.Max(high, value);
                    }
                }
                Assert.That(high - low, Is.GreaterThan(150), "Layer " + channel + " is flat.");
                Assert.That(seam, Is.LessThan(40), "Layer " + channel + " has a seam.");
            }
        }

        private static int At(Color32 color, int channel) =>
            channel switch
            {
                0 => color.r,
                1 => color.g,
                2 => color.b,
                _ => color.a,
            };
    }
}
