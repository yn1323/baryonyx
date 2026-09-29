using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Baryonyx.Editor.Art
{
    // Assets/Baryonyx/ の .aseprite は、Aseprite Importer（com.unity.2d.aseprite）で直接読み込む。
    // Importerは絵の周りの透明な部分を切り取り、余白をつけて1枚のテクスチャへ詰めるため、
    // そのままではPNGのときと大きさや位置が変わる。読み込みの最後に、切り取られた絵をキャンバスの位置へ戻した
    // テクスチャと、フレームごとのSpriteを追加する。画面や展示室は、この追加したSpriteとテクスチャを使う。
    // Importerが作るSpriteは一覧に出ないよう非表示にする。
    public sealed class AsepriteCanvasImport : AssetPostprocessor
    {
        public const string ArtRoot = "Assets/Baryonyx";
        public const string Extension = ".aseprite";
        private const string TextureId = "BaryonyxCanvasTexture";
        private const string SpriteId = "BaryonyxCanvasFrame";
        private const int MaxTextureSize = 16384;

        // 処理を変えたら上げる。全ての .aseprite が読み込み直される。
        public override uint GetVersion() => 1;

        public static bool IsAseprite(string path) =>
            path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

        public static IReadOnlyList<string> FindSources() =>
            AssetDatabase
                .FindAssets("", new[] { ArtRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsAseprite)
                .Distinct()
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

        // 追加したSpriteをフレーム順に返す。
        public static IReadOnlyList<Sprite> LoadFrames(string path) =>
            AssetDatabase
                .LoadAllAssetRepresentationsAtPath(path)
                .OfType<Sprite>()
                .Where(sprite => (sprite.hideFlags & HideFlags.HideInHierarchy) == 0)
                .OrderByDescending(sprite => sprite.rect.y)
                .ThenBy(sprite => sprite.rect.x)
                .ToList();

        public static Sprite LoadSprite(string path)
        {
            var frames = LoadFrames(path);
            if (frames.Count == 0)
                throw new InvalidOperationException("Asepriteの画像を読み込めません: " + path);
            return frames[0];
        }

        public static Texture2D LoadTexture(string path) => LoadSprite(path).texture;

        // 追加したテクスチャは画素を読めないため、生成スクリプトで絵の範囲などを調べるときは、
        // Importerが作った読めるテクスチャから、1フレームの画素をキャンバスの大きさで組み立て直す。
        public static Color32[] ReadFramePixels(string path, int frame = 0)
        {
            if (AssetImporter.GetAtPath(path) is not AsepriteImporter importer)
                throw new InvalidOperationException("Asepriteのファイルがありません: " + path);
            var assetName = Path.GetFileNameWithoutExtension(path);
            var frameCount = LoadFrames(path).Count;
            var canvas = Vector2Int.RoundToInt(importer.canvasSize);
            var pixels = new Color32[canvas.x * canvas.y];
            var drawnSprites = AssetDatabase
                .LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .Where(sprite => (sprite.hideFlags & HideFlags.HideInHierarchy) != 0)
                .Where(sprite => FrameIndex(sprite.name, assetName, frameCount) == frame);
            foreach (var sprite in drawnSprites)
                CopyToCanvas(
                    sprite.texture.GetPixels32(),
                    sprite.texture.width,
                    DrawnRect(sprite),
                    Vector2Int.zero - Vector2Int.RoundToInt(sprite.pivot),
                    pixels,
                    canvas.x,
                    new RectInt(Vector2Int.zero, canvas)
                );
            return pixels;
        }

        // 生成スクリプトから、フィルターとミップマップを指定して読み込み直す。
        public static void ApplyTextureSettings(string path, FilterMode filter, bool mipmaps)
        {
            if (AssetImporter.GetAtPath(path) is not AsepriteImporter importer)
                throw new InvalidOperationException("Asepriteのファイルがありません: " + path);
            if (importer.filterMode == filter && importer.mipmapEnabled == mipmaps)
                return;
            importer.filterMode = filter;
            importer.mipmapEnabled = mipmaps;
            importer.SaveAndReimport();
        }

        // Importerが付けるSprite名（1フレームならファイル名、複数なら Frame_<番号>）からフレーム番号を返す。
        public static int FrameIndex(string spriteName, string assetName, int frameCount)
        {
            if (frameCount == 1)
                return spriteName == assetName ? 0 : -1;
            var match = Regex.Match(spriteName, @"^Frame_(\d+)$");
            if (!match.Success)
                return -1;
            var frame = int.Parse(match.Groups[1].Value);
            return frame < frameCount ? frame : -1;
        }

        public static string FrameName(string assetName, int frame, int frameCount) =>
            frameCount == 1 ? assetName : $"{assetName}_{frame}";

        // フレームを左上から右へ並べ、テクスチャの最大サイズに収まらなければ次の行へ折り返す。
        public static RectInt FrameRect(int frame, int columns, int rows, Vector2Int canvas) =>
            new(
                frame % columns * canvas.x,
                (rows - 1 - frame / columns) * canvas.y,
                canvas.x,
                canvas.y
            );

        // 切り取られた絵（sourceのdrawn）を、キャンバス上のoffsetの位置に置いてtargetのframeへ写す。
        // Asepriteではキャンバスの外にも描けるため、はみ出た部分は捨てる。
        public static void CopyToCanvas(
            Color32[] source,
            int sourceWidth,
            RectInt drawn,
            Vector2Int offset,
            Color32[] target,
            int targetWidth,
            RectInt frame
        )
        {
            for (var y = 0; y < drawn.height; y++)
            {
                var canvasY = offset.y + y;
                if (canvasY < 0 || canvasY >= frame.height)
                    continue;
                for (var x = 0; x < drawn.width; x++)
                {
                    var canvasX = offset.x + x;
                    if (canvasX < 0 || canvasX >= frame.width)
                        continue;
                    target[(frame.y + canvasY) * targetWidth + frame.x + canvasX] = source[
                        (drawn.y + y) * sourceWidth + drawn.x + x
                    ];
                }
            }
        }

        // ミップマップで縮めたとき、透明な画素の色（黒）が絵の縁に混ざらないよう、
        // 隣の不透明な画素の色で埋める。PNGの読み込み設定 Alpha Is Transparency と同じ目的。
        public static void BleedColor(Color32[] pixels, int width, int height)
        {
            var filled = pixels.Select(pixel => pixel.a > 0).ToArray();
            var changed = true;
            while (changed)
            {
                changed = false;
                var next = (bool[])filled.Clone();
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var i = y * width + x;
                    if (filled[i])
                        continue;
                    var from =
                        x > 0 && filled[i - 1] ? i - 1
                        : x < width - 1 && filled[i + 1] ? i + 1
                        : y > 0 && filled[i - width] ? i - width
                        : y < height - 1 && filled[i + width] ? i + width
                        : -1;
                    if (from < 0)
                        continue;
                    pixels[i] = new Color32(pixels[from].r, pixels[from].g, pixels[from].b, 0);
                    next[i] = true;
                    changed = true;
                }
                filled = next;
            }
        }

        private static RectInt DrawnRect(Sprite sprite) =>
            new(
                Mathf.RoundToInt(sprite.rect.x),
                Mathf.RoundToInt(sprite.rect.y),
                Mathf.RoundToInt(sprite.rect.width),
                Mathf.RoundToInt(sprite.rect.height)
            );

        private void OnPreprocessAsset()
        {
            if (
                assetImporter is not AsepriteImporter importer
                || !assetPath.StartsWith(ArtRoot + "/", StringComparison.Ordinal)
            )
                return;
            Configure(importer);
            importer.OnPostAsepriteImport -= AddCanvasFrames;
            importer.OnPostAsepriteImport += AddCanvasFrames;
        }

        // キャンバスの画像を作るのに必要な設定。Inspectorで変えても、読み込むたびに戻す。
        private static void Configure(AsepriteImporter importer)
        {
            // Importerのテクスチャから画素を読むため。このテクスチャは画面から参照しないのでビルドに入らない。
            var settings = new SerializedObject(importer);
            settings.FindProperty("m_TextureImporterSettings.m_IsReadable").intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            importer.importMode = FileImportModes.AnimatedSprite;
            importer.layerImportMode = LayerImportModes.MergeFrame;
            // 左下を基準にすると、Spriteのpivotが絵からキャンバスの原点までの距離になる。
            importer.pivotSpace = PivotSpaces.Canvas;
            importer.pivotAlignment = SpriteAlignment.BottomLeft;
            importer.spritePadding = 0;
            // 画面はuGUIで表示するため、SpriteRenderer向けのPrefabとアニメーションは作らない。
            importer.generateModelPrefab = false;
            importer.generateAnimationClips = false;
        }

        private static void AddCanvasFrames(AsepriteImporter.ImportEventArgs args)
        {
            var context = args.context;
            var importer = args.importer;
            var assetName = Path.GetFileNameWithoutExtension(context.assetPath);
            var frameCount = importer.asepriteFile.noOfFrames;
            var canvas = Vector2Int.RoundToInt(importer.canvasSize);
            var columns = Math.Max(1, Math.Min(frameCount, MaxTextureSize / canvas.x));
            var rows = (frameCount + columns - 1) / columns;
            var size = new Vector2Int(columns * canvas.x, rows * canvas.y);
            if (size.y > MaxTextureSize)
            {
                context.LogImportError(
                    $"フレームが多すぎて{MaxTextureSize}ピクセルのテクスチャに収まりません: {context.assetPath}"
                );
                return;
            }

            var objects = new List<Object>();
            context.GetObjects(objects);
            var drawnSprites = objects.OfType<Sprite>().ToList();
            var pixels = new Color32[size.x * size.y];
            var source = drawnSprites.Count > 0 ? drawnSprites[0].texture.GetPixels32() : null;
            foreach (var sprite in drawnSprites)
            {
                sprite.hideFlags |= HideFlags.HideInHierarchy;
                var frame = FrameIndex(sprite.name, assetName, frameCount);
                if (frame < 0)
                {
                    context.LogImportWarning(
                        $"フレーム番号がわからない絵 {sprite.name} を除きました。絵のあるフレームが1つだけの場合は、ほかのフレームにも絵を置いてください: {context.assetPath}"
                    );
                    continue;
                }
                CopyToCanvas(
                    source,
                    sprite.texture.width,
                    DrawnRect(sprite),
                    Vector2Int.zero - Vector2Int.RoundToInt(sprite.pivot),
                    pixels,
                    size.x,
                    FrameRect(frame, columns, rows, canvas)
                );
            }

            if (importer.mipmapEnabled)
                BleedColor(pixels, size.x, size.y);
            var texture = new Texture2D(
                size.x,
                size.y,
                TextureFormat.RGBA32,
                importer.mipmapEnabled,
                !importer.sRGBTexture
            )
            {
                name = assetName,
                filterMode = importer.filterMode,
                wrapMode = importer.wrapMode,
                anisoLevel = importer.aniso,
            };
            texture.SetPixels32(pixels);
            texture.Apply(importer.mipmapEnabled, true);
            context.AddObjectToAsset(TextureId, texture);
            for (var frame = 0; frame < frameCount; frame++)
            {
                var rect = FrameRect(frame, columns, rows, canvas);
                var sprite = Sprite.Create(
                    texture,
                    new Rect(rect.x, rect.y, rect.width, rect.height),
                    new Vector2(0.5f, 0.5f),
                    importer.spritePixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect
                );
                sprite.name = FrameName(assetName, frame, frameCount);
                context.AddObjectToAsset(SpriteId + frame, sprite);
            }
        }
    }
}
