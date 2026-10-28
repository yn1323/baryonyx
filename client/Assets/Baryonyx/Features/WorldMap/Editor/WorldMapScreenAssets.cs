using System.Collections.Generic;
using System.IO;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.WorldMap.Editor
{
    /// <summary>
    /// Builds the world map screen (the highway prototype, <see cref="WorldMapView"/>): the
    /// generated picture of the continent, the sugoroku squares and straight lines over it from
    /// <see cref="WorldMapCatalog"/>, the party, the place's name, the buttons back to Home and
    /// to the list of dungeons, and the dialog showing what a chest held. The picture and the
    /// squares are generated illustrations used as they are (doc/art/direction.md#例外).
    /// </summary>
    public static class WorldMapScreenAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/WorldMap";
        public const string PrefabPath = Folder + "/UI/WorldMapScreen.prefab";
        public const string ArtFolder = Folder + "/UI/Art";
        public const string BackgroundPath = ArtFolder + "/WorldMapBackground.png";
        public const string HiddenNodePath = ArtFolder + "/WorldNodeLocked.png";
        public const string IconDungeonsPath = ArtFolder + "/IconDungeons.png";

        // 背景の1画素を3pxで出す。地図は横に約2.4画面、縦に約2.8画面ぶんになる。
        private const float Scale = 3f;

        /// <summary>The sizes of one map's screen, all in design pixels but the scale.</summary>
        public sealed class Look
        {
            // 背景の1画素を、設計座標で何pxに出すか。
            public float Scale = WorldMapScreenAssets.Scale;

            // マスの台の幅。街と関所は大きくする。
            public float Node = 104f;
            public float Town = 140f;
            public float Boss = 128f;

            // マスの押せる範囲の幅。台より小さければ台の大きさのまま押す。
            public float Hit;

            // 道の太さ。縁の上に地の色を重ねる。
            public float LineEdge = 32f;
            public float LineFill = 20f;

            // 駒の大きさ（跳ねる高さも同じ割合で縮む）と、地図を開いたときの拡大率。
            public float Pawn = 1f;
            public float StartZoom = 1f;
        }

        // 作っている画面の大きさ。
        private static Look look = new();

        private const float ButtonSize = 128f;
        private const float Margin = 24f;

        // パーティを表す木の駒の高さと、マスの中心からの足元の位置（設計座標、y が上）。
        private const float PawnHeight = 140f;
        private static readonly Vector2 PawnFeet = new(0, -12);

        // WorldNodeKind の順。
        private static readonly string[] KindArt =
        {
            "WorldNodeEmpty",
            "WorldNodeBattle",
            "WorldNodeTreasure",
            "WorldNodeStory",
            "WorldNodeBoss",
            "WorldNodeTown",
            // 入れ子の地図の地域と入口。
            "WorldNodeTown",
            "WorldNodeGate",
        };

        // ダンジョン一覧のボタンの城門（白一色の形。ホームのボタンのアイコンと同じ描き方）。
        private static readonly string[] DungeonsIcon =
        {
            "................",
            "#.#..........#.#",
            "###..........###",
            "###.#.#..#.#.###",
            "################",
            "################",
            "################",
            "######....######",
            "#####......#####",
            "#####......#####",
            "#####......#####",
            "#####......#####",
            "#####......#####",
            "#####......#####",
            "#####......#####",
            "................",
        };

        [MenuItem("Baryonyx/World Map/Create Screen Assets")]
        public static GameObject CreateAssets()
        {
            EditorGuard.RequireEditMode();
            var background = PrepareArt();
            var pictures = new[]
            {
                (background, new Rect(Vector2.zero, WorldMapCatalog.PictureSize)),
            };
            return Build(
                "WorldMapScreen",
                PrefabPath,
                new Look(),
                WorldMapCatalog.Build(),
                WorldMapCatalog.PictureSize,
                pictures,
                layout: null,
                dungeons: true
            );
        }

        /// <summary>
        /// Builds one map's screen. <paramref name="pictures"/> are the background's pieces with
        /// the pixels of the whole picture each covers. A <paramref name="layout"/> makes it a
        /// map of the nested world map. <paramref name="dungeons"/> adds the button at the bottom
        /// right that opens the list of dungeons.
        /// </summary>
        public static GameObject Build(
            string rootName,
            string prefabPath,
            Look screenLook,
            WorldMapGraph graph,
            Vector2 pictureSize,
            IReadOnlyList<(Texture2D Texture, Rect Pixels)> pictures,
            TextAsset layout,
            bool dungeons
        )
        {
            look = screenLook;
            var kinds = new Sprite[KindArt.Length];
            for (int i = 0; i < KindArt.Length; i++)
                kinds[i] = ImportNode($"{ArtFolder}/{KindArt[i]}.png");
            var hidden = ImportNode(HiddenNodePath);

            var font = GameFontAssets.GetOrCreate();
            using (Begin(font, UiArt.EnsureTextShadow(font)))
            {
                var root = CanvasRoot(rootName);
                var view = root.gameObject.AddComponent<WorldMapView>();
                view.Scale = look.Scale;
                view.StartZoom = look.StartZoom;
                view.Layout = layout;
                view.KindSprites = kinds;
                view.HiddenSprite = hidden;
                BuildMap(root, view, graph, pictureSize, pictures);
                BuildControls(root, view, dungeons);
                view.ItemArt = Baryonyx.Inventory.Editor.ItemArtAssets.Load();
                view.Dialog = GameDialogAssets.Build(root);
                // 入れ子の地図では、マスの出来事をマスから開く窓で見せる。
                if (layout != null)
                    view.EventWindow = WorldMapEventWindowAssets.Build(root);
                CollectTintGraphics(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, prefabPath);
                look = new Look();
                return prefab;
            }
        }

        // 街道の画面の絵（ダンジョン一覧のボタン、背景）を用意する。
        private static Texture2D PrepareArt()
        {
            Directory.CreateDirectory(ArtFolder);
            ArtAssets.WritePattern(
                IconDungeonsPath,
                DungeonsIcon,
                c => c == '#' ? Color.white : Color.clear
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ArtAssets.ImportSprite(IconDungeonsPath, Vector4.zero, FilterMode.Point);
            return ArtAssets.ImportTexture(BackgroundPath, FilterMode.Point);
        }

        private static Sprite ImportNode(string path)
        {
            ArtAssets.ImportSprite(path, Vector4.zero, FilterMode.Bilinear, mipmaps: true);
            return ArtAssets.LoadSprite(path);
        }

        private static void BuildMap(
            RectTransform root,
            WorldMapView view,
            WorldMapGraph graph,
            Vector2 pictureSize,
            IReadOnlyList<(Texture2D Texture, Rect Pixels)> pictures
        )
        {
            var viewport = Rect("Viewport", root);
            Stretch(viewport);
            view.Viewport = viewport;
            view.Drag = viewport.gameObject.AddComponent<WorldMapDrag>();

            var map = Rect("Map", viewport);
            map.anchorMin = map.anchorMax = map.pivot = new Vector2(0f, 1f);
            map.sizeDelta = pictureSize * look.Scale;
            view.Map = map;

            var background = Layer("Background", map);
            for (int i = 0; i < pictures.Count; i++)
            {
                var (texture, pixels) = pictures[i];
                var picture = Rect("Picture" + i, background);
                picture.anchorMin = picture.anchorMax = picture.pivot = new Vector2(0f, 1f);
                picture.sizeDelta = pixels.size * look.Scale;
                picture.anchoredPosition = new Vector2(pixels.x, -pixels.y) * look.Scale;
                var image = picture.gameObject.AddComponent<RawImage>();
                image.texture = texture;
                // 地図のどこからでも動かせるよう、絵で指を受ける。
                image.raycastTarget = true;
            }

            BuildLines(Layer("Lines", map), graph, view);
            BuildNodes(Layer("Nodes", map), graph, view);
            BuildLevels(Layer("Levels", map), graph);
            view.Party = BuildParty(Layer("Party", map));
            view.Party.localScale = new Vector3(look.Pawn, look.Pawn, 1f);
            view.PawnHop = view.Party.GetComponentInChildren<MapPawnHop>();
            // 世界地図は、ゲームの時間の速さによらず歩く。
            view.PawnHop.UnscaledTime = true;
        }

        private static RectTransform Layer(string name, RectTransform map)
        {
            var layer = Rect(name, map);
            Stretch(layer);
            return layer;
        }

        // 地図の上の位置（左上から、y は下向きの画素）を、左上を基準にした置き場所にする。
        private static void PlaceOnMap(RectTransform rect, Vector2 point, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(point.x * look.Scale, -point.y * look.Scale);
        }

        private static void BuildLines(RectTransform layer, WorldMapGraph graph, WorldMapView view)
        {
            var edges = new List<Image>();
            var fills = new List<Image>();
            // 縁を先にすべて描き、地の色をその上に重ねて、つなぎ目に縁が出ないようにする。
            var edgeLayer = Layer("Edges", layer);
            var fillLayer = Layer("Fills", layer);
            foreach (var (from, to) in graph.Lines)
            {
                var a = graph.Nodes[from].Point;
                var b = graph.Nodes[to].Point;
                string name = graph.Nodes[from].Id + "_" + graph.Nodes[to].Id;
                edges.Add(Line(edgeLayer, name, a, b, look.LineEdge, WorldMapView.EdgeColor));
                fills.Add(Line(fillLayer, name, a, b, look.LineFill, WorldMapView.ClearedLine));
            }
            view.LineEdges = edges.ToArray();
            view.LineFills = fills.ToArray();
        }

        private static Image Line(
            RectTransform layer,
            string name,
            Vector2 a,
            Vector2 b,
            float width,
            Color color
        )
        {
            var rect = Rect(name, layer);
            float length = Vector2.Distance(a, b) * look.Scale;
            PlaceOnMap(rect, (a + b) * 0.5f, new Vector2(length + width, width));
            float angle = Mathf.Atan2(-(b.y - a.y), b.x - a.x) * Mathf.Rad2Deg;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            return AddImage(rect, color, false);
        }

        // 区間の推奨Lvの札。道の脇に置き、マスの上に重ねて隠れないようにする。
        private static void BuildLevels(RectTransform layer, WorldMapGraph graph)
        {
            foreach (var (level, point) in graph.Levels)
            {
                var rect = Rect("Lv" + level, layer);
                PlaceOnMap(rect, point, new Vector2(132f, 60f));
                var inside = GlassCardAssets.Plaque(
                    rect,
                    pattern: false,
                    fill: GlassCardAssets.SceneFillColor
                );
                var label = Label(
                    inside,
                    "Level",
                    "Lv" + level,
                    32,
                    UiPalette.Gold,
                    TextAlignmentOptions.Center
                );
                Stretch((RectTransform)label.transform);
            }
        }

        private static void BuildNodes(RectTransform layer, WorldMapGraph graph, WorldMapView view)
        {
            var rects = new List<RectTransform>();
            var images = new List<Image>();
            var buttons = new List<Button>();
            foreach (var node in graph.Nodes)
            {
                float width = node.Kind switch
                {
                    WorldNodeKind.Town or WorldNodeKind.Region => look.Town,
                    WorldNodeKind.Boss or WorldNodeKind.Gate => look.Boss,
                    _ => look.Node,
                };
                var rect = Rect("Node_" + node.Id, layer);
                PlaceOnMap(rect, node.Point, Vector2.one * width);
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = view.KindSprites[(int)node.Kind];
                image.preserveAspect = true;
                image.raycastTarget = true;
                rects.Add(rect);
                images.Add(image);
                buttons.Add(AddButton(rect, image));
                if (look.Hit > width)
                    HitArea(rect, (look.Hit - width) / 2f);
            }
            view.Nodes = rects.ToArray();
            view.NodeImages = images.ToArray();
            view.NodeButtons = buttons.ToArray();
        }

        // 台の絵の大きさを変えずに、押せる範囲だけを広げる透明な子。
        private static void HitArea(RectTransform node, float margin)
        {
            var hit = Rect("HitArea", node);
            Stretch(hit);
            hit.offsetMin = -Vector2.one * margin;
            hit.offsetMax = Vector2.one * margin;
            AddImage(hit, Color.clear, true);
        }

        private static RectTransform BuildParty(RectTransform layer)
        {
            var party = Rect("Party", layer);
            party.anchorMin = party.anchorMax = new Vector2(0f, 1f);
            party.pivot = new Vector2(0.5f, 0.5f);
            party.sizeDelta = Vector2.zero;
            // パーティは、4人の代わりに木の駒1つで歩く。
            MapPawnAssets.Build(party, "Pawn", PawnHeight).anchoredPosition = PawnFeet;
            return party;
        }

        // 画面の端の部品：左上のもどる、右上の今いる場所、右下のダンジョン一覧。
        private static void BuildControls(RectTransform root, WorldMapView view, bool dungeons)
        {
            var safe = SafeArea(root);
            view.Back = RoundButton(
                safe,
                "Back",
                new Vector2(0f, 1f),
                new Vector2(Margin, -Margin),
                GuideMenuAssets.IconBackPath
            );
            if (dungeons)
                view.Dungeons = RoundButton(
                    safe,
                    "Dungeons",
                    new Vector2(1f, 0f),
                    new Vector2(-Margin, Margin),
                    IconDungeonsPath
                );

            var place = Rect("Place", safe);
            Corner(place, Vector2.one, new Vector2(-Margin, -Margin), new Vector2(480f, 88f));
            var inside = GlassCardAssets.Plaque(
                place,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            var label = Label(
                inside,
                "Name",
                "",
                40,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)label.transform);
            view.Place = label;

            // 保存に失敗したときの知らせ。下の中央に、透明な状態で置く。
            var notice = Rect("Notice", safe);
            Corner(notice, new Vector2(0.5f, 0f), new Vector2(0f, Margin), new Vector2(960f, 88f));
            view.NoticeGroup = HiddenGroup(notice);
            var noticeInside = GlassCardAssets.Plaque(
                notice,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            var noticeLabel = Label(
                noticeInside,
                "Message",
                "",
                36,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)noticeLabel.transform);
            view.Notice = noticeLabel;
        }

        private static Button RoundButton(
            RectTransform safe,
            string name,
            Vector2 corner,
            Vector2 offset,
            string iconPath
        )
        {
            var rect = Rect(name, safe);
            Corner(rect, corner, offset, Vector2.one * ButtonSize);
            var (button, _, content) = GlassCardAssets.Choice(
                rect,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            var icon = Rect("Icon", content);
            Place(icon, Vector2.zero, Vector2.one * 64f);
            SpriteImage(icon, iconPath, Color.white);
            return button;
        }
    }
}
