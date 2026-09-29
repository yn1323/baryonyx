using System.IO;
using System.Linq;
using Baryonyx.Editor.Art;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class AsepriteCanvasImportTests
    {
        private static readonly Color32 Clear = new(0, 0, 0, 0);
        private static readonly Color32 Red = new(255, 0, 0, 255);
        private static readonly Color32 Blue = new(0, 0, 255, 255);

        [Test]
        public void EverySourceHasOneCanvasSizedSpritePerFrame()
        {
            var sources = AsepriteCanvasImport.FindSources();
            Assert.That(sources, Is.Not.Empty);
            foreach (var source in sources)
            {
                var (frames, width, height) = ReadHeader(source);
                var sprites = AsepriteCanvasImport.LoadFrames(source);
                var name = Path.GetFileNameWithoutExtension(source);
                Assert.That(
                    sprites.Select(sprite => sprite.name),
                    Is.EqualTo(
                        Enumerable
                            .Range(0, frames)
                            .Select(frame => AsepriteCanvasImport.FrameName(name, frame, frames))
                    ),
                    source
                );
                foreach (var sprite in sprites)
                    Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(width, height)), source);
                var pixels = AsepriteCanvasImport.ReadFramePixels(source);
                Assert.That(pixels.Length, Is.EqualTo(width * height), source);
                Assert.That(pixels.Any(pixel => pixel.a > 0), Is.True, source);
            }
        }

        [Test]
        public void PutsTheDrawnPixelsBackAtTheirCanvasPosition()
        {
            // 3x2の絵が、4x4のテクスチャの (1, 1) にある。
            var source = new Color32[16];
            source[1 * 4 + 1] = Red;
            source[2 * 4 + 3] = Blue;
            var target = new Color32[3 * 3];

            AsepriteCanvasImport.CopyToCanvas(
                source,
                4,
                new RectInt(1, 1, 3, 2),
                new Vector2Int(-1, 1),
                target,
                3,
                new RectInt(0, 0, 3, 3)
            );

            // 左へ1はみ出た列は捨て、上へ1ずらして置く。
            Assert.That(target[1 * 3 + 0], Is.EqualTo(Clear));
            Assert.That(target[2 * 3 + 1], Is.EqualTo(Blue));
            Assert.That(target.Count(pixel => pixel.a > 0), Is.EqualTo(1));
        }

        [Test]
        public void LaysOutFramesFromTheTopLeft()
        {
            var canvas = new Vector2Int(8, 5);

            Assert.That(
                AsepriteCanvasImport.FrameRect(0, 2, 2, canvas),
                Is.EqualTo(new RectInt(0, 5, 8, 5))
            );
            Assert.That(
                AsepriteCanvasImport.FrameRect(1, 2, 2, canvas),
                Is.EqualTo(new RectInt(8, 5, 8, 5))
            );
            Assert.That(
                AsepriteCanvasImport.FrameRect(2, 2, 2, canvas),
                Is.EqualTo(new RectInt(0, 0, 8, 5))
            );
        }

        [Test]
        public void ReadsTheFrameFromTheImporterSpriteName()
        {
            Assert.That(AsepriteCanvasImport.FrameIndex("IconRune", "IconRune", 1), Is.Zero);
            Assert.That(AsepriteCanvasImport.FrameIndex("Frame_3", "Walk", 4), Is.EqualTo(3));
            Assert.That(AsepriteCanvasImport.FrameIndex("Frame_4", "Walk", 4), Is.EqualTo(-1));
            Assert.That(AsepriteCanvasImport.FrameIndex("Walk", "Walk", 4), Is.EqualTo(-1));
            Assert.That(AsepriteCanvasImport.FrameName("Walk", 2, 4), Is.EqualTo("Walk_2"));
            Assert.That(AsepriteCanvasImport.FrameName("IconRune", 0, 1), Is.EqualTo("IconRune"));
        }

        [Test]
        public void FillsTransparentPixelsWithTheNearestColour()
        {
            var pixels = new[] { Clear, Clear, Red };

            AsepriteCanvasImport.BleedColor(pixels, 3, 1);

            Assert.That(
                pixels,
                Is.EqualTo(new[] { new Color32(255, 0, 0, 0), new Color32(255, 0, 0, 0), Red })
            );
        }

        // .asepriteのヘッダー：6バイト目からフレーム数、幅、高さ（各2バイト、リトルエンディアン）。
        private static (int frames, int width, int height) ReadHeader(string path)
        {
            var header = new byte[12];
            using (var stream = File.OpenRead(path))
                Assert.That(stream.Read(header, 0, header.Length), Is.EqualTo(header.Length), path);
            Assert.That(header[4] == 0xE0 && header[5] == 0xA5, Is.True, path);
            return (
                header[6] | header[7] << 8,
                header[8] | header[9] << 8,
                header[10] | header[11] << 8
            );
        }
    }
}
