using System.Linq;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 覚醒の演出の光の色を、カードの挿絵から取る（白・灰・暗い所は数えず、いちばん多い鮮やかな色相を明るく返す）。
    public sealed class CardArtColorTests
    {
        private static Color32[] Pixels(params (Color32 Color, int Count)[] parts) =>
            parts.SelectMany(part => Enumerable.Repeat(part.Color, part.Count)).ToArray();

        private static float HueOf(Color color)
        {
            Color.RGBToHSV(color, out float h, out _, out _);
            return h;
        }

        [Test]
        public void TheMostCommonVividHueWinsOverWhiteDarkAndAFewOtherColours()
        {
            var purple = new Color32(150, 50, 230, 255);
            var picked = CardArtColor.Pick(
                Pixels(
                    (purple, 500),
                    (new Color32(255, 255, 255, 255), 300),
                    (new Color32(10, 8, 20, 255), 150),
                    (new Color32(240, 200, 40, 255), 74)
                )
            );

            Assert.That(picked.HasValue, Is.True);
            Assert.That(HueOf(picked.Value), Is.EqualTo(HueOf(purple)).Within(0.02f));
            Color.RGBToHSV(picked.Value, out _, out float s, out float v);
            Assert.That(v, Is.EqualTo(1f).Within(0.01f), "It is given back bright, as a glow.");
            Assert.That(s, Is.InRange(CardArtColor.LowSaturation, CardArtColor.HighSaturation));
        }

        [Test]
        public void ARedArtIsFoundAcrossTheEndOfTheColourWheel()
        {
            var picked = CardArtColor.Pick(
                Pixels((new Color32(230, 20, 40, 255), 300), (new Color32(230, 40, 20, 255), 300))
            );

            Assert.That(picked.HasValue, Is.True);
            Assert.That(picked.Value.r, Is.GreaterThan(0.95f));
            Assert.That(picked.Value.g, Is.LessThan(0.4f));
            Assert.That(picked.Value.b, Is.LessThan(0.4f));
        }

        [Test]
        public void ArtWithNoVividColourOrNoArtGivesTheFallback()
        {
            var grey = Pixels((new Color32(128, 128, 128, 255), 600), (new Color32(250, 250, 250, 255), 400));
            Assert.That(CardArtColor.Pick(grey).HasValue, Is.False);
            Assert.That(CardArtColor.Of(null, Color.cyan), Is.EqualTo(Color.cyan));
        }

        [Test]
        public void TheArtIsReadOnTheGpuEvenWhenItIsNotReadableAndKept()
        {
            var art = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            try
            {
                art.SetPixels32(Enumerable.Repeat(new Color32(30, 200, 60, 255), 64).ToArray());
                art.Apply(false, makeNoLongerReadable: true);
                CardArtColor.Forget();

                var color = CardArtColor.Of(art, Color.magenta);

                Assert.That(HueOf(color), Is.EqualTo(HueOf(new Color32(30, 200, 60, 255))).Within(0.03f));
                Assert.That(CardArtColor.Of(art, Color.magenta), Is.EqualTo(color));
            }
            finally
            {
                CardArtColor.Forget();
                Object.DestroyImmediate(art);
            }
        }
    }
}
