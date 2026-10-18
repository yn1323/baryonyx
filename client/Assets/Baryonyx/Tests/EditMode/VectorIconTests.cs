using Baryonyx.Editor.Art;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class VectorIconTests
    {
        // 64×64の設計座標を64pxで描くと、1単位が1px になる。
        private const int Size = 64;

        [Test]
        public void FillPaintsInsideTheShapeOnly()
        {
            var pixels = new VectorIcon().Fill(IconShape.Rect(16f, 16f, 32f, 32f, 0f)).Render(Size);

            Assert.That(Alpha(pixels, 32, 32), Is.EqualTo(1f));
            Assert.That(Alpha(pixels, 8, 8), Is.EqualTo(0f));
            Assert.That(Alpha(pixels, 32, 52), Is.EqualTo(0f));
        }

        [Test]
        public void EdgesAreSmoothedAcrossAPixel()
        {
            // The square's left edge runs through the middle of column 16.
            var pixels = new VectorIcon()
                .Fill(IconShape.Rect(16.5f, 16f, 32f, 32f, 0f))
                .Render(Size);

            Assert.That(Alpha(pixels, 16, 32), Is.EqualTo(0.5f));
        }

        [Test]
        public void GapClearsAMarginAroundTheFrontPart()
        {
            var pixels = new VectorIcon()
                .Fill(IconShape.Rect(0f, 0f, 64f, 64f, 0f))
                .Fill(IconShape.Rect(24f, 24f, 16f, 16f, 0f), gap: 4f)
                .Render(Size);

            Assert.That(Alpha(pixels, 32, 32), Is.EqualTo(1f), "the front part");
            Assert.That(Alpha(pixels, 21, 32), Is.EqualTo(0f), "the gap");
            Assert.That(Alpha(pixels, 10, 32), Is.EqualTo(1f), "the part behind");
        }

        [Test]
        public void CutsPunchHolesWithRoundEnds()
        {
            var pixels = new VectorIcon()
                .Fill(IconShape.Rect(0f, 0f, 64f, 64f, 0f))
                .Cut(IconShape.Circle(16f, 16f, 6f))
                .CutStroke(IconShape.Path("M32 40 L56 40"), 4f)
                .Render(Size);

            Assert.That(Alpha(pixels, 16, 16), Is.EqualTo(0f), "the cut circle");
            Assert.That(Alpha(pixels, 44, 40), Is.EqualTo(0f), "the cut line");
            Assert.That(Alpha(pixels, 31, 40), Is.EqualTo(0f), "the round end of the line");
            Assert.That(Alpha(pixels, 44, 46), Is.EqualTo(1f), "beside the line");
        }

        [Test]
        public void ColourIsBakedIntoThePaintedPixels()
        {
            var colour = new Color(0.9f, 0.8f, 0.7f);
            var pixels = new VectorIcon()
                .Fill(IconShape.Rect(16f, 16f, 32f, 32f, 0f))
                .Render(Size, colour);

            Assert.That(pixels[(Size - 1 - 32) * Size + 32], Is.EqualTo(colour));
            Assert.That(Alpha(pixels, 8, 8), Is.EqualTo(0f));
        }

        [Test]
        public void RotationTurnsClockwiseLikeSvg()
        {
            // A bar to the right of the centre, turned 90 degrees, points down (y grows downward).
            var pixels = new VectorIcon()
                .Fill(IconShape.Rect(40f, 30f, 16f, 4f, 0f).Rotated(90f, 32f, 32f))
                .Render(Size);

            Assert.That(Alpha(pixels, 32, 48), Is.EqualTo(1f));
            Assert.That(Alpha(pixels, 48, 32), Is.EqualTo(0f));
        }

        // x, y in design units from the top left; pixels run bottom-up like Texture2D.
        private static float Alpha(Color[] pixels, int x, int y) =>
            pixels[(Size - 1 - y) * Size + x].a;
    }
}
