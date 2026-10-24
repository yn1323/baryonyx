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

        // マスの台の幅（設計座標）。街と関所は大きくする。
        private const float NodeSize = 104f;
        private const float TownSize = 140f;
        private const float BossSize = 128f;

        // 道の太さ。縁の上に地の色を重ねる。
        private const float LineEdge = 32f;
        private const float LineFill = 20f;

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
            Directory.CreateDirectory(ArtFolder);
            ArtAssets.WritePattern(
                IconDungeonsPath,
                DungeonsIcon,
                c => c == '#' ? Color.white : Color.clear
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ArtAssets.ImportSprite(IconDungeonsPath, Vector4.zero, FilterMode.Point);
            var background = ArtAssets.ImportTexture(BackgroundPath, FilterMode.Point);
            var kinds = new Sprite[KindArt.Length];
            for (int i = 0; i < KindArt.Length; i++)
                kinds[i] = ImportNode($"{ArtFolder}/{KindArt[i]}.png");
            var hidden = ImportNode(HiddenNodePath);

            var font = GameFontAssets.GetOrCreate();
            using (Begin(font, UiArt.EnsureTextShadow(font)))
            {
                var root = CanvasRoot("WorldMapScreen");
                var view = root.gameObject.AddComponent<WorldMapView>();
                view.Scale = Scale;
                view.KindSprites = kinds;
                view.HiddenSprite = hidden;
                BuildMap(root, view, background);
                BuildControls(root, view);
                view.ItemArt = Baryonyx.Inventory.Editor.ItemArtAssets.Load();
                view.Dialog = GameDialogAssets.Build(root);
                CollectTintGraphics(root);
                return PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            }
        }

        private static Sprite ImportNode(string path)
        {
            ArtAssets.ImportSprite(path, Vector4.zero, FilterMode.Bilinear, mipmaps: true);
            return ArtAssets.LoadSprite(path);
        }

        private static void BuildMap(RectTransform root, WorldMapView view, Texture2D background)
        {
            var graph = WorldMapCatalog.Build();
            var viewport = Rect("Viewport", root);
            Stretch(viewport);
            view.Viewport = viewport;
            view.Drag = viewport.gameObject.AddComponent<WorldMapDrag>();

            var map = Rect("Map", viewport);
            map.anchorMin = map.anchorMax = map.pivot = new Vector2(0f, 1f);
            map.sizeDelta = WorldMapCatalog.PictureSize * Scale;
            view.Map = map;

            var picture = Rect("Background", map);
            Stretch(picture);
            var image = picture.gameObject.AddComponent<RawImage>();
            image.texture = background;
            // 地図のどこからでも動かせるよう、絵で指を受ける。
            image.raycastTarget = true;

            BuildLines(Layer("Lines", map), graph, view);
            BuildNodes(Layer("Nodes", map), graph, view);
            BuildLevels(Layer("Levels", map), graph);
            view.Party = BuildParty(Layer("Party", map));
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
            rect.anchoredPosition = new Vector2(point.x * Scale, -point.y * Scale);
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
                edges.Add(Line(edgeLayer, name, a, b, LineEdge, WorldMapView.EdgeColor));
                fills.Add(Line(fillLayer, name, a, b, LineFill, WorldMapView.ClearedLine));
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
            float length = Vector2.Distance(a, b) * Scale;
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
                    WorldNodeKind.Town => TownSize,
                    WorldNodeKind.Boss => BossSize,
                    _ => NodeSize,
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
            }
            view.Nodes = rects.ToArray();
            view.NodeImages = images.ToArray();
            view.NodeButtons = buttons.ToArray();
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
        private static void BuildControls(RectTransform root, WorldMapView view)
        {
            var safe = SafeArea(root);
            view.Back = RoundButton(
                safe,
                "Back",
                new Vector2(0f, 1f),
                new Vector2(Margin, -Margin),
                GuideMenuAssets.IconBackPath
            );
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
