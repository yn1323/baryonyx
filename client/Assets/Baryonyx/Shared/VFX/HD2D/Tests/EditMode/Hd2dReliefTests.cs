using Baryonyx.Vfx.Hd2d.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class Hd2dReliefTests
    {
        private static readonly Color32 Clear = new(0, 0, 0, 0);
        private static readonly Color32 Grey = new(128, 128, 128, 255);
        private static readonly Color32 White = new(255, 255, 255, 255);

        private static Color32[] Fill(int width, int height, Color32 colour)
        {
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = colour;
            return pixels;
        }

        [Test]
        public void AnEvenColourFacesStraightOut()
        {
            var normals = Hd2dRelief.Normals(Fill(4, 4, Grey), 4, 4, 3f, 1f, true);
            foreach (var normal in normals)
                Assert.That(normal, Is.EqualTo(new Color32(128, 128, 255, 255)));
        }

        [Test]
        public void ADarkJointSinksBelowTheStonesBesideIt()
        {
            // A dark column (a joint between stones) in the middle of a light stone.
            const int size = 5;
            var pixels = Fill(size, size, White);
            for (int y = 0; y < size; y++)
                pixels[y * size + 2] = Grey;
            var normals = Hd2dRelief.Normals(pixels, size, size, 3f, 0f, false);
            int row = 2 * size;
            Assert.That(
                normals[row + 1].r,
                Is.GreaterThan(128),
                "The stone left of the joint leans towards it."
            );
            Assert.That(
                normals[row + 3].r,
                Is.LessThan(128),
                "The stone right of the joint leans towards it."
            );
            Assert.That(normals[row + 2].r, Is.EqualTo(128), "The joint's own floor lies flat.");
        }

        [Test]
        public void ABrighterRowAboveTiltsTheDotsBelowItDown()
        {
            // Rows run from the bottom, as Unity keeps a texture's pixels.
            const int size = 4;
            var pixels = Fill(size, size, Grey);
            for (int x = 0; x < size; x++)
                pixels[2 * size + x] = White;
            var normals = Hd2dRelief.Normals(pixels, size, size, 3f, 0f, false);
            Assert.That(normals[1 * size + 1].g, Is.LessThan(128));
        }

        [Test]
        public void ACutOutRoundsOffTowardsItsEdges()
        {
            const int size = 9;
            var pixels = Fill(size, size, Clear);
            for (int y = 2; y < 7; y++)
            for (int x = 2; x < 7; x++)
                pixels[y * size + x] = Grey;
            var normals = Hd2dRelief.Normals(pixels, size, size, 0f, 1.5f, false);
            int middle = 4 * size;
            Assert.That(normals[middle + 2].r, Is.LessThan(128), "The left edge faces left.");
            Assert.That(normals[middle + 6].r, Is.GreaterThan(128), "The right edge faces right.");
            Assert.That(normals[6 * size + 4].g, Is.GreaterThan(128), "The top edge faces up.");
            Assert.That(normals[2 * size + 4].g, Is.LessThan(128), "The bottom edge faces down.");
            Assert.That(normals[middle + 4], Is.EqualTo(new Color32(128, 128, 255, 255)));
        }

        [Test]
        public void ATileReadsRoundItsEdges()
        {
            // A bright column at the left edge raises the right edge's neighbour across the seam.
            const int size = 4;
            var pixels = Fill(size, size, Grey);
            for (int y = 0; y < size; y++)
                pixels[y * size] = White;
            var tiled = Hd2dRelief.Normals(pixels, size, size, 3f, 0f, true);
            var clamped = Hd2dRelief.Normals(pixels, size, size, 3f, 0f, false);
            Assert.That(tiled[size - 1].r, Is.LessThan(128), "It leans towards the bright seam.");
            Assert.That(clamped[size - 1].r, Is.EqualTo(128), "A clamped board does not wrap.");
        }
    }
}
