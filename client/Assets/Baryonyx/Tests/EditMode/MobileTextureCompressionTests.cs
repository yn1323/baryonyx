using System.Linq;
using Baryonyx.Editor.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // Assets/Baryonyx/ の大きな画像は、AndroidとiOSで必ずASTCに圧縮して入る
    // （doc/rules/frontend-design.md#画像の圧縮）。
    public sealed class MobileTextureCompressionTests
    {
        [Test]
        public void EveryLargePictureIsCompressedForPhones()
        {
            var checkedCount = 0;
            foreach (
                var path in AssetDatabase
                    .FindAssets("t:Texture2D", new[] { AsepriteCanvasImport.ArtRoot })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Distinct()
            )
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                if (Mathf.Max(width, height) < MobileTextureCompression.SmallestSide)
                    continue;
                checkedCount++;
                foreach (var platform in new[] { "Android", "iPhone" })
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    Assert.That(settings.overridden, Is.True, platform + " " + path);
                    Assert.That(
                        settings.format,
                        Is.EqualTo(MobileTextureCompression.FormatOf(path)),
                        platform + " " + path
                    );
                }
            }
            Assert.That(checkedCount, Is.GreaterThan(0));
        }
    }
}
