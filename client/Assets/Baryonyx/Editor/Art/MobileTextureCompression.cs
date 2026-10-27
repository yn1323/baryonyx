using System;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Editor.Art
{
    // Assets/Baryonyx/ の画像（PNG・JPEG）を、AndroidとiOSでは必ずASTCに圧縮して取り込む
    // （doc/rules/frontend-design.md#画像の圧縮、2026-10-28、ユーザー決定）。
    // プロジェクトに置く画像は元の画質のまま残し、圧縮はUnityの取り込みでだけ行う。
    // 生成メニューが画像の設定を書き換えて読み込み直しても、取り込みのたびにここで圧縮を掛け直す。
    public sealed class MobileTextureCompression : AssetPostprocessor
    {
        // 入れ子の地図の背景。大きく枚数も多いため、ほかの画像より粗く圧縮する。
        public const string AtlasArtFolder = "Assets/Baryonyx/Features/WorldMap/Atlas/Art/";

        // 長い辺がこれより小さい画像は圧縮しない。容量はほとんど減らず、細い線や小さな絵がにじむ。
        public const int SmallestSide = 512;

        private static readonly string[] Platforms = { "Android", "iPhone" };

        // 処理を変えたら上げる。対象の画像がすべて読み込み直される。
        public override uint GetVersion() => 1;

        public static TextureImporterFormat FormatOf(string path) =>
            path.StartsWith(AtlasArtFolder, StringComparison.Ordinal)
                ? TextureImporterFormat.ASTC_8x8
                : TextureImporterFormat.ASTC_6x6;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AsepriteCanvasImport.ArtRoot + "/", StringComparison.Ordinal))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (Mathf.Max(width, height) < SmallestSide)
                return;
            // 2のべき乗でない画像にミップマップを付けると、ASTCに圧縮されずRGBA32のまま入る
            // （2026-10-27、3840×2560の世界地図で1枚50MBになった）。画面に出す絵なので外す。
            if (importer.mipmapEnabled && !(IsPowerOfTwo(width) && IsPowerOfTwo(height)))
                importer.mipmapEnabled = false;
            var format = FormatOf(assetPath);
            foreach (var platform in Platforms)
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = Mathf.Max(settings.maxTextureSize, importer.maxTextureSize);
                settings.format = format;
                importer.SetPlatformTextureSettings(settings);
            }
        }

        private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
    }
}
