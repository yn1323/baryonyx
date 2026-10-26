using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class BattleVfxNoiseTests
    {
        [Test]
        public void TheTableIsSetForTheShadersAsARepeatingSmoothLinearTexture()
        {
            BattleVfxNoise.Ensure();

            var table = Shader.GetGlobalTexture("_BattleVfxNoise") as Texture2D;

            Assert.That(table, Is.Not.Null);
            Assert.That(table.width, Is.EqualTo(BattleVfxNoise.Size));
            Assert.That(table.height, Is.EqualTo(BattleVfxNoise.Size));
            Assert.That(table.mipmapCount, Is.EqualTo(1));
            Assert.That(table.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(table.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(table.isDataSRGB, Is.False);
        }

        [Test]
        public void TheSecondChannelHoldsTheNextSliceAndTheTableRepeats()
        {
            BattleVfxNoise.Ensure();
            var table = (Texture2D)Shader.GetGlobalTexture("_BattleVfxNoise");
            var data = table.GetRawTextureData();
            int channels = data.Length / (BattleVfxNoise.Size * BattleVfxNoise.Size);
            var step = BattleVfxNoise.SliceStep;

            foreach (var (x, y) in new[] { (0, 0), (5, 200), (255, 255), (230, 250) })
            {
                int i = (y * BattleVfxNoise.Size + x) * channels;
                Assert.That(data[i], Is.EqualTo(BattleVfxNoise.Value(x, y)));
                Assert.That(data[i + 1], Is.EqualTo(BattleVfxNoise.Value(x + step.x, y + step.y)));
                Assert.That(
                    BattleVfxNoise.Value(x + BattleVfxNoise.Size, y - BattleVfxNoise.Size),
                    Is.EqualTo(BattleVfxNoise.Value(x, y))
                );
            }
        }

        [Test]
        public void TheValuesSpreadEvenlyOverTheirRange()
        {
            long sum = 0;
            int low = 0;
            int high = 0;
            for (int y = 0; y < BattleVfxNoise.Size; y++)
            for (int x = 0; x < BattleVfxNoise.Size; x++)
            {
                int v = BattleVfxNoise.Value(x, y);
                sum += v;
                if (v < 64)
                    low++;
                if (v >= 192)
                    high++;
            }
            int count = BattleVfxNoise.Size * BattleVfxNoise.Size;

            Assert.That(sum / (double)count, Is.EqualTo(127.5).Within(2.0));
            Assert.That(low / (double)count, Is.EqualTo(0.25).Within(0.01));
            Assert.That(high / (double)count, Is.EqualTo(0.25).Within(0.01));
        }
    }
}
