using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Editor.Art
{
    // 生成スクリプトが画像を読み込み、コードで描いた画像を保存するときの共通処理。
    // .aseprite は、読み込み時に追加したキャンバスと同じ大きさのSpriteとテクスチャを返す。
    public static class ArtAssets
    {
        public static Sprite LoadSprite(string path)
        {
            if (AsepriteCanvasImport.IsAseprite(path))
                return AsepriteCanvasImport.LoadSprite(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException("画像がありません: " + path);
            return sprite;
        }

        public static Texture2D LoadTexture(string path)
        {
            if (AsepriteCanvasImport.IsAseprite(path))
                return AsepriteCanvasImport.LoadTexture(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                throw new InvalidOperationException("画像がありません: " + path);
            return texture;
        }

        // 画面にそのまま貼る画像を、元の大きさのまま取り込む。ドット絵はPoint、生成したイラストはBilinearにする。
        public static Texture2D ImportTexture(string path, FilterMode filter)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AsepriteCanvasImport.IsAseprite(path))
            {
                AsepriteCanvasImport.ApplyTextureSettings(path, filter, mipmaps: false);
                return AsepriteCanvasImport.LoadTexture(path);
            }
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("画像がありません: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = filter;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            // 2の累乗でない画像（100x100など）を引き伸ばすと、ドットがにじむ。
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return LoadTexture(path);
        }

        // Aseprite で描いた画像の取り込み設定を揃える。縮めて表示する画像はミップマップを使う。
        public static void ImportDrawn(string path, bool mipmaps = false)
        {
            if (!File.Exists(path))
                throw new InvalidOperationException("画像がありません: " + path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AsepriteCanvasImport.ApplyTextureSettings(path, FilterMode.Point, mipmaps);
        }

        // コードで描いた画像のSprite設定。fullRect は Image.Type.Tiled で並べる画像に使う。
        public static void ImportSprite(
            string path,
            Vector4 border,
            FilterMode filter,
            bool fullRect = false
        )
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            if (fullRect)
            {
                // 並べる画像は、Spriteの四角形全体を使うため、メッシュで切り詰めない。
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
            importer.spriteBorder = border;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = filter;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        // 1文字1ドットの模様をドット絵のSpriteとして保存する。
        public static void WritePattern(string path, string[] rows, Func<char, Color> palette)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    if (rows[y].Length != width)
                        throw new InvalidOperationException(
                            $"Pattern row {y} of {path} is ragged."
                        );
                    for (int x = 0; x < width; x++)
                        texture.SetPixel(x, height - 1 - y, palette(rows[y][x]));
                }
                texture.Apply();
                SavePng(path, texture, Vector4.zero, FilterMode.Point);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // 画素の中心で color(u, v) を求めて保存する。u・v は0〜1（v = 0 が下端）。
        public static void WriteSmooth(
            string path,
            int width,
            int height,
            Vector4 border,
            Func<float, float, Color> color
        )
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, color((x + 0.5f) / width, (y + 0.5f) / height));
                texture.Apply();
                SavePng(path, texture, border, FilterMode.Bilinear);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // 縁を1ピクセルでなめらかにした正方形の角丸（ringWidth > 0 なら輪）。
        // fill(v) は縦の位置 v（0 = 下端、1 = 上端）で色を決める。
        public static void WriteRounded(
            string path,
            int size,
            float radius,
            float ringWidth,
            Vector4 border,
            Func<float, Color> fill
        )
        {
            float half = size / 2f;
            WriteSmooth(
                path,
                size,
                size,
                border,
                (u, v) =>
                {
                    float px = Mathf.Abs(u * size - half) - (half - radius);
                    float py = Mathf.Abs(v * size - half) - (half - radius);
                    float outside =
                        new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude
                        + Mathf.Min(Mathf.Max(px, py), 0f)
                        - radius;
                    float alpha = Mathf.Clamp01(0.5f - outside);
                    if (ringWidth > 0f)
                        alpha *= Mathf.Clamp01(outside + ringWidth + 0.5f);
                    var color = fill(v);
                    color.a *= alpha;
                    return color;
                }
            );
        }

        public static void SavePng(
            string path,
            Texture2D texture,
            Vector4 border,
            FilterMode filter
        )
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ImportSprite(path, border, filter);
        }
    }
}
