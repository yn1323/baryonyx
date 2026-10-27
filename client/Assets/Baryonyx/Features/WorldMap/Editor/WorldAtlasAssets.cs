using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.WorldMap.Editor
{
    /// <summary>
    /// Builds the nested world map prototype (<see cref="WorldAtlasNavigator"/>): a screen for
    /// the world map and for each region's area map from its layout (Atlas/Layouts/&lt;key&gt;.json)
    /// and the pieces of its background (Atlas/Art/&lt;key&gt;_&lt;row&gt;&lt;column&gt;.jpg), the
    /// navigator prefab that loads them from Resources, and a scene to try them in, opened from
    /// Home's list of debugging rooms.
    /// The backgrounds are generated pictures used as they are (doc/art/direction.md#例外),
    /// compressed to ASTC 6x6 on phones.
    /// </summary>
    public static class WorldAtlasAssets
    {
        public const string Folder = WorldMapScreenAssets.Folder + "/Atlas";
        public const string LayoutFolder = Folder + "/Layouts";
        public const string ArtFolder = Folder + "/Art";

        // 地図のPrefabは、見ている間だけ読むよう Resources に置く（WorldAtlasNavigator.ResourceFolder）。
        public const string PrefabFolder = Folder + "/Resources/WorldAtlas";
        public const string NavigatorPath = Folder + "/WorldAtlas.prefab";
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/WorldAtlas.unity";

        // 世界地図と、地域のエリアマップ（WorldAtlasLayout のキー）。
        public static readonly string[] Keys =
        {
            WorldAtlasLayout.WorldKey,
            "verde",
            "marina",
            "gelo",
            "bosco",
            "fiamma",
            "nebbia",
            "oro",
        };

        // 背景を分けた1枚の大きさの上限。古い端末でも読めるよう、1辺4096までにする。
        public const int MaxPiece = 4096;

        // 世界地図は5倍の背景（7680×5120）を半分の大きさで出し、縮めて見るのでミップマップを使う。
        // 網の目の道にマスが多いので、マスは出来事のアイコンが描ける程度にとどめ、地域のマスだけ大きくする。
        // 駒はマスより少し大きい程度に縮め、開いたときは少し近づけて、マスと駒が見分けやすいようにする。
        // エリアマップは背景を等倍で出し、マスと道は背景に描いた道に合わせて細くする。
        private static WorldMapScreenAssets.Look LookOf(string key) =>
            key == WorldAtlasLayout.WorldKey
                ? new WorldMapScreenAssets.Look
                {
                    Scale = 0.5f,
                    Node = 50f,
                    Town = 88f,
                    Boss = 68f,
                    LineEdge = 12f,
                    LineFill = 6f,
                    Pawn = 0.5f,
                    StartZoom = 1.3f,
                }
                : new WorldMapScreenAssets.Look
                {
                    Scale = 1f,
                    Node = 56f,
                    Town = 92f,
                    Boss = 76f,
                    LineEdge = 10f,
                    LineFill = 5f,
                    Pawn = 0.55f,
                    StartZoom = 1.15f,
                };

        [MenuItem("Baryonyx/World Map/Create Atlas Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var key in Keys)
                BuildMap(key);
            var root = new GameObject("WorldAtlas");
            root.AddComponent<WorldAtlasNavigator>();
            PrefabUtility.SaveAsPrefabAsset(root, NavigatorPath);
            Object.DestroyImmediate(root);
            CreateScene();
        }

        // 地図を1枚作る。配置か背景がまだなければ作らない。
        private static WorldMapView BuildMap(string key)
        {
            string layoutPath = $"{LayoutFolder}/{key}.json";
            var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(layoutPath);
            var pieces = Pieces(key);
            if (layoutAsset == null || pieces.Count == 0)
            {
                Debug.LogWarning($"入れ子の地図の配置か背景がありません: {key}");
                return null;
            }
            var layout = WorldAtlasLayout.Parse(layoutAsset.text);
            bool world = key == WorldAtlasLayout.WorldKey;
            var pictures = new List<(Texture2D, Rect)>();
            // 行ごとに左から並べ、1行の高さはその行の最初の1枚の高さにする。
            float y = 0f;
            foreach (var row in pieces.GroupBy(p => p.Row).OrderBy(g => g.Key))
            {
                float x = 0f;
                float height = 0f;
                foreach (var piece in row.OrderBy(p => p.Column))
                {
                    var texture = ImportPicture(piece.Path, mipmaps: world);
                    pictures.Add((texture, new Rect(x, y, texture.width, texture.height)));
                    x += texture.width;
                    height = texture.height;
                }
                y += height;
            }
            var prefab = WorldMapScreenAssets.Build(
                world ? "WorldAtlasWorld" : "WorldAtlasArea_" + key,
                $"{PrefabFolder}/{key}.prefab",
                LookOf(key),
                layout.Build(),
                layout.PictureSize,
                pictures,
                layoutAsset
            );
            return prefab.GetComponent<WorldMapView>();
        }

        private readonly struct Piece
        {
            public Piece(string path, int row, int column)
            {
                Path = path;
                Row = row;
                Column = column;
            }

            public string Path { get; }
            public int Row { get; }
            public int Column { get; }
        }

        private static List<Piece> Pieces(string key)
        {
            var pieces = new List<Piece>();
            if (!Directory.Exists(ArtFolder))
                return pieces;
            var pattern = new Regex("^" + Regex.Escape(key) + @"_(\d)(\d)\.jpg$");
            foreach (var file in Directory.GetFiles(ArtFolder, key + "_*.jpg"))
            {
                var match = pattern.Match(Path.GetFileName(file));
                if (match.Success)
                    pieces.Add(
                        new Piece(
                            ArtFolder + "/" + Path.GetFileName(file),
                            int.Parse(match.Groups[1].Value),
                            int.Parse(match.Groups[2].Value)
                        )
                    );
            }
            return pieces;
        }

        // 背景の1枚を、スマホではASTC 6×6に圧縮して取り込む（2026-10-27、ユーザー決定）。
        private static Texture2D ImportPicture(string path, bool mipmaps)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = mipmaps;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = MaxPiece;
            importer.textureCompression = TextureImporterCompression.Compressed;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = MaxPiece;
                settings.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void CreateScene()
        {
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    ScreenScenes.AddCamera(scene, "WorldAtlasCamera", ScreenScenes.CameraColor);
                    ScreenScenes.AddScreen(scene, NavigatorPath, "WorldAtlas");
                    ScreenScenes.AddEventSystem(scene);
                }
            );
            // Homeのデバッグルームの一覧から開くため、ビルドの対象に入れる。
            ScreenScenes.AddToBuildSettings(ScenePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
