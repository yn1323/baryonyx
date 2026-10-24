using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Home.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Adventure.Editor
{
    /// <summary>
    /// Builds the adventure's screens: the exploration screen (the route laid out from left to
    /// right on the destination's ground, its roads of light and room pedestals, the party and the
    /// chest, the place, floor and prompt, the menu, the bar of where the map is, the legend and
    /// the dialog), and the overlay laid over the battle screen (the menu, the plate of a drop, the
    /// dialog and the notice band). Coordinates follow the 1920x1080 design; the map is as wide as
    /// the route and slides across the screen.
    /// </summary>
    public static class AdventureAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Adventure";
        public const string ExplorationPrefabPath = Folder + "/UI/ExplorationScreen.prefab";
        public const string OverlayPrefabPath = Folder + "/UI/AdventureOverlay.prefab";
        public const string ArtFolder = Folder + "/UI/Art";
        public const string IconBattlePath = ArtFolder + "/IconRoomBattle.aseprite";
        public const string IconElitePath = ArtFolder + "/IconRoomElite.aseprite";
        public const string IconTreasurePath = ArtFolder + "/IconRoomTreasure.aseprite";
        public const string IconBossPath = ArtFolder + "/IconRoomBoss.aseprite";
        public const string IconRestPath = ArtFolder + "/IconRoomRest.aseprite";
        public const string IconMerchantPath = ArtFolder + "/IconRoomMerchant.aseprite";
        public const string IconShadyChestPath = ArtFolder + "/IconRoomShadyChest.aseprite";
        public const string IconTrialPath = ArtFolder + "/IconRoomTrial.aseprite";
        public const string IconStepDoorPath = ArtFolder + "/IconRoomStepDoor.aseprite";
        public const string IconAltarPath = ArtFolder + "/IconRoomAltar.aseprite";
        public const string IconMysteryPath = ArtFolder + "/IconRoomMystery.aseprite";

        public const string StageArtFolder = "Assets/Baryonyx/Shared/Art/Stages/Exploration";
        public const string ChestClosedPath = StageArtFolder + "/ChestClosed.aseprite";
        public const string ChestOpenPath = StageArtFolder + "/ChestOpen.aseprite";

        // 行き先ごとの地図の地面（Codexの生成画像を清書せずに使う。doc/art/direction.md の例外）。
        public static string GroundPath(string destinationId) =>
            $"{StageArtFolder}/Ground{Pascal(destinationId)}.png";

        // 宝箱は1ドット3px。戦闘のキャラは地図の上では1ドット1pxの小さい姿にする。
        private const float MapDot = ExplorationView.MapDot;

        // パーティを表す木の駒の高さと、マスの中心からの足元の位置（設計座標）。
        private const float PawnHeight = 132f;
        private static readonly Vector2 PawnFeet = new(0, -10);

        private static readonly Color TextMain = UiPalette.TextMain;

        // 凡例に並べるマスの種類。名前は AdventureTexts.KindName。
        private static readonly AdventureRoomKind[] LegendKinds =
        {
            AdventureRoomKind.Battle,
            AdventureRoomKind.Elite,
            AdventureRoomKind.Treasure,
            AdventureRoomKind.ShadyChest,
            AdventureRoomKind.Trial,
            AdventureRoomKind.Rest,
            AdventureRoomKind.Merchant,
            AdventureRoomKind.StepDoor,
            AdventureRoomKind.Altar,
            AdventureRoomKind.Mystery,
            AdventureRoomKind.Boss,
        };
        private static readonly Color SpotShadow = new(0.012f, 0.02f, 0.04f, 0.78f);

        // 勝った戦闘のドロップの札の中心。戦闘の演出デバッグルームと同じく、敵と味方の上に出す。
        private static readonly Vector2 DropPlateCenter = new(40f, 250f);

        private const float MenuWidth = 128f;
        private const float MenuHeight = 168f;

        [MenuItem("Baryonyx/Adventure/Create Screen Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();
            Directory.CreateDirectory(Path.GetDirectoryName(ExplorationPrefabPath));
            AssetDatabase.Refresh();
            UiArt.EnsureAll();
            GuideMenuAssets.CreateSharedArt();
            foreach (
                var path in new[]
                {
                    IconBattlePath,
                    IconElitePath,
                    IconTreasurePath,
                    IconBossPath,
                    IconRestPath,
                    IconMerchantPath,
                    IconShadyChestPath,
                    IconTrialPath,
                    IconStepDoorPath,
                    IconAltarPath,
                }
            )
                ArtAssets.ImportDrawn(path);
            foreach (var path in new[] { ChestClosedPath, ChestOpenPath })
                ArtAssets.ImportTexture(path, FilterMode.Point);
            ArtAssets.ImportDrawn(UiArt.IconCompassPath);

            var grounds = ImportGrounds();
            var font = GameFontAssets.GetOrCreate();
            var shadowText = UiArt.EnsureTextShadow(font);
            using (Begin(font, shadowText))
            {
                BuildExploration(grounds);
                BuildOverlay();
            }
            AssetDatabase.SaveAssetIfDirty(font);
            AssetDatabase.SaveAssets();
        }

        private static Sprite[] KindIcons() =>
            new[]
            {
                null,
                ArtAssets.LoadSprite(IconBattlePath),
                ArtAssets.LoadSprite(IconElitePath),
                ArtAssets.LoadSprite(IconTreasurePath),
                ArtAssets.LoadSprite(IconBossPath),
                ArtAssets.LoadSprite(IconRestPath),
                ArtAssets.LoadSprite(IconMerchantPath),
                ArtAssets.LoadSprite(IconShadyChestPath),
                ArtAssets.LoadSprite(IconTrialPath),
                ArtAssets.LoadSprite(IconStepDoorPath),
                ArtAssets.LoadSprite(IconAltarPath),
                ArtAssets.LoadSprite(IconMysteryPath),
            };

        // --- Ground --------------------------------------------------------------------------

        // 行き先ごとの地面を、並べて敷けるよう繰り返しの設定で取り込む。絵のない行き先は森の地面を使う。
        private static ExplorationGround[] ImportGrounds()
        {
            var grounds = new List<ExplorationGround>();
            foreach (var area in AdventureCatalog.Areas)
            {
                string path = GroundPath(area.Id);
                if (!File.Exists(path))
                    continue;
                ArtAssets.ImportTexture(path, FilterMode.Bilinear);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
                grounds.Add(
                    new ExplorationGround
                    {
                        DestinationId = area.Id,
                        Texture = ArtAssets.LoadTexture(path),
                    }
                );
            }
            return grounds.ToArray();
        }

        // "forest-ruins" を "ForestRuins" にする。
        private static string Pascal(string id) =>
            string.Concat(
                (id ?? "")
                    .Split('-', StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1))
            );

        // --- Exploration ---------------------------------------------------------------------

        private static void BuildExploration(ExplorationGround[] grounds)
        {
            var root = CanvasRoot("ExplorationScreen");
            var view = root.gameObject.AddComponent<ExplorationView>();
            view.KindIcons = KindIcons();
            view.ItemArt = Baryonyx.Inventory.Editor.ItemArtAssets.Load();
            view.Grounds = grounds;
            // 展示室でPrefabだけを開いたときに描く見本の道。冒険の場面では使わない。
            view.SampleSeed = 20261004;

            var backdrop = Rect("Backdrop", root);
            Stretch(backdrop);
            AddImage(backdrop, ScreenScenes.CameraColor, false);

            // 地図の窓は画面いっぱい。中の板（Content）を左右に動かす。
            var map = Rect("Map", root);
            Stretch(map);
            view.Map = map;
            view.Drag = map.gameObject.AddComponent<ExplorationMapDrag>();
            var content = Rect("Content", map);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = new Vector2(1920f, 0f);
            content.anchoredPosition = Vector2.zero;
            view.Content = content;
            var ground = Rect("Ground", content);
            Stretch(ground);
            view.Ground = ground.gameObject.AddComponent<RawImage>();
            view.Ground.color = ScreenScenes.CameraColor;
            // 地面は指を受け、地図の上のどこを1本指で動かしても道の先を見られる。
            view.Ground.raycastTarget = true;
            if (grounds.Length > 0)
            {
                view.Ground.texture = grounds[0].Texture;
                view.Ground.color = Color.white;
            }
            // 地面を少し暗くして、光の道と台座を浮かせる。
            var shade = Rect("Shade", content);
            Stretch(shade);
            AddImage(shade, new Color(0.02f, 0.03f, 0.06f, 0.28f), false);

            view.Roads = Layer("Roads", content);
            view.RoadTemplate = BuildRoad(view.Roads);
            var markers = Layer("Markers", content);
            view.Markers = markers;
            view.MarkerTemplate = BuildMarker(markers);
            BuildParty(Layer("Party", content), view);
            BuildChest(Layer("ChestLayer", content), view);

            var safe = SafeArea(root);
            view.MenuButton = BuildMenuButton(safe);
            view.Location = Label(
                safe,
                "Location",
                AdventureCatalog.DestinationName(AdventureCatalog.ForestRuins) + " 第1層",
                56,
                TextMain,
                TextAlignmentOptions.Right
            );
            Corner(
                (RectTransform)view.Location.transform,
                Vector2.one,
                new Vector2(-56, -28),
                new Vector2(900, 80)
            );
            view.Prompt = BuildPrompt(safe);
            BuildScrollBar(safe, view);
            view.LegendButton = BuildLegendButton(safe);
            view.Legend = BuildLegend(safe);
            // 魔物の出現と宝箱の演出は、地図と文字の上、通知とダイアログの下に重ねる。
            view.Moments = ExplorationMomentsAssets.Build(root, view);
            // 休憩の焚き火は、地図と演出の上、通知とダイアログの下に重ねる。
            view.Campfire = ExplorationCampfireAssets.Build(root);
            // 戦闘後の強化を選ぶ前にアプリが終わったときは、探索で開き直す。
            view.Boons = AdventureBoonPickerAssets.Build(root);

            view.Notice = NoticeBandAssets.Build(root);
            // 行商人の品物5つと「立ち去る」を並べるため、選択肢を6つにして行を少し低くする。
            view.Dialog = GameDialogAssets.Build(root, choiceCount: 6, rowHeight: 96f);
            CollectTintGraphics(root);
            DrawSample(view);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, ExplorationPrefabPath);
        }

        // 地図の板の左端・上下の中央を原点にする層。中の物は、そこから道に沿って置く。
        private static RectTransform Layer(string name, RectTransform content)
        {
            var layer = Rect(name, content);
            layer.anchorMin = layer.anchorMax = layer.pivot = new Vector2(0f, 0.5f);
            layer.sizeDelta = Vector2.zero;
            layer.anchoredPosition = Vector2.zero;
            return layer;
        }

        // 見本の道を入口から見た形で一度並べ、印・道・パーティをPrefabに作り込む。
        private static void DrawSample(ExplorationView view)
        {
            view.MarkerTemplate.gameObject.SetActive(false);
            view.RoadTemplate.gameObject.SetActive(false);
            view.Legend.gameObject.SetActive(false);
            view.RenderSample(view.SampleSeed);
        }

        // 道の型：淡い光の帯と、光の芯。長さと向きと色はビューが道ごとに決める。
        private static RectTransform BuildRoad(RectTransform roads)
        {
            var body = Rect("RoadTemplate", roads);
            Place(body, Vector2.zero, Vector2.zero);
            var glow = Rect("Glow", body);
            Place(glow, Vector2.zero, new Vector2(100, 22));
            SpriteImage(glow, UiArt.SoftSpotPath, Color.white).raycastTarget = false;
            var core = Rect("Core", body);
            Place(core, Vector2.zero, new Vector2(100, 6));
            AddImage(core, Color.white, false);
            return body;
        }

        // 下の細いバー：地図の幅に対して、画面に入っている所を示す。
        private static void BuildScrollBar(RectTransform safe, ExplorationView view)
        {
            var track = Rect("ScrollTrack", safe);
            track.anchorMin = track.anchorMax = track.pivot = new Vector2(0.5f, 0f);
            track.anchoredPosition = new Vector2(-60, 44);
            track.sizeDelta = new Vector2(1200, 10);
            AddImage(track, new Color(0.04f, 0.06f, 0.1f, 0.6f), false);
            var thumb = Rect("Thumb", track);
            thumb.anchorMin = thumb.anchorMax = thumb.pivot = new Vector2(0f, 0.5f);
            thumb.anchoredPosition = Vector2.zero;
            thumb.sizeDelta = new Vector2(240, 10);
            AddImage(thumb, new Color(0.62f, 0.82f, 1f, 0.9f), false);
            view.ScrollTrack = track;
            view.ScrollThumb = thumb;
        }

        // 右下の「？」。押すと凡例を開く。
        private static Button BuildLegendButton(RectTransform safe)
        {
            var body = Rect("LegendButton", safe);
            Corner(body, Vector2.right, new Vector2(-40, 32), new Vector2(104, 104));
            var ring = Rect("Ring", body);
            Stretch(ring);
            var image = SpriteImage(ring, UiArt.CirclePath, new Color(0.04f, 0.06f, 0.1f, 0.82f));
            image.raycastTarget = true;
            var rim = Rect("Rim", body);
            Stretch(rim);
            rim.offsetMin = new Vector2(4, 4);
            rim.offsetMax = new Vector2(-4, -4);
            SpriteImage(rim, UiArt.CirclePath, new Color(1f, 1f, 1f, 0.1f)).raycastTarget = false;
            var label = Label(body, "Label", "？", 56, TextMain, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform);
            return AddTintButton(body, image);
        }

        // 部屋の印の型：光、丸い札、部屋の種類のアイコン、押せる範囲。札の色はビューが部屋の種類で決める。
        private static ExplorationMapMarker BuildMarker(RectTransform markers)
        {
            var body = Rect("RoomTemplate", markers);
            Place(body, Vector2.zero, new Vector2(150, 150));
            var marker = body.gameObject.AddComponent<ExplorationMapMarker>();
            marker.Body = body;
            // 霧が晴れて現れる・見えなくなって消えるときに、印をまとめて薄くする。
            marker.Group = body.gameObject.AddComponent<CanvasGroup>();

            var glow = Rect("Glow", body);
            Place(glow, Vector2.zero, new Vector2(120, 120));
            marker.Glow = SpriteImage(glow, UiArt.SoftSpotPath, Color.white);
            marker.Glow.raycastTarget = false;

            var ring = Rect("Ring", body);
            Place(ring, Vector2.zero, new Vector2(96, 96));
            marker.Ring = SpriteImage(ring, UiArt.CirclePath, Color.white);
            marker.Ring.raycastTarget = false;
            var plate = Rect("Plate", body);
            Place(plate, Vector2.zero, new Vector2(84, 84));
            marker.Plate = SpriteImage(
                plate,
                UiArt.CirclePath,
                ExplorationView.PlateOf(AdventureRoomKind.Battle)
            );
            marker.Plate.raycastTarget = false;

            var icon = Rect("Icon", body);
            // 24x24のアイコンを3倍にして、1ドットを整数のピクセルにする。
            Place(icon, Vector2.zero, new Vector2(72, 72));
            marker.Icon = icon.gameObject.AddComponent<Image>();
            marker.Icon.sprite = ArtAssets.LoadSprite(IconBattlePath);
            marker.Icon.raycastTarget = false;

            // 押せる範囲は、指で押しやすい150px四方にする。
            var hit = AddImage(body, new Color(0, 0, 0, 0), true);
            marker.Button = UiBuild.AddButton(body, hit);
            return marker;
        }

        // 「？」で開く凡例：マスの札と同じ色の小さい札とアイコンに、マスの種類の名前を添えて2列に並べる。
        private static RectTransform BuildLegend(RectTransform safe)
        {
            const float Cell = 224f;
            const float Row = 60f;
            const float Padding = 16f;
            var legend = Rect("Legend", safe);
            Corner(
                legend,
                Vector2.right,
                new Vector2(-40, 152),
                new Vector2(
                    Cell * 2 + Padding * 2,
                    Row * ((LegendKinds.Length + 1) / 2) + Padding * 2
                )
            );
            ScenePlaque(legend);
            // 凡例を押しても閉じる。
            UiBuild.AddButton(legend, AddImage(legend, new Color(0, 0, 0, 0), true));
            for (int i = 0; i < LegendKinds.Length; i++)
            {
                var kind = LegendKinds[i];
                string name = AdventureTexts.KindName(kind);
                var entry = Rect(kind.ToString(), legend);
                entry.anchorMin = entry.anchorMax = entry.pivot = new Vector2(0, 1);
                entry.anchoredPosition = new Vector2(
                    Padding + (i % 2) * Cell,
                    -Padding - (i / 2) * Row
                );
                entry.sizeDelta = new Vector2(Cell, Row);
                var ring = Rect("Ring", entry);
                Place(ring, new Vector2(-Cell / 2 + 28, 0), new Vector2(52, 52));
                SpriteImage(ring, UiArt.CirclePath, ExplorationView.AccentOf(kind));
                var plate = Rect("Plate", ring);
                Place(plate, Vector2.zero, new Vector2(46, 46));
                SpriteImage(plate, UiArt.CirclePath, ExplorationView.PlateOf(kind));
                var icon = Rect("Icon", ring);
                // 24x24のアイコンを2倍にする（地図の小さい札と同じ）。
                Place(icon, Vector2.zero, new Vector2(48, 48));
                var image = icon.gameObject.AddComponent<Image>();
                image.sprite = KindIcons()[(int)kind];
                image.raycastTarget = false;
                var label = Label(entry, "Name", name, 26, TextMain, TextAlignmentOptions.Left);
                var rect = (RectTransform)label.transform;
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(1, 1);
                rect.offsetMin = new Vector2(64, 0);
                rect.offsetMax = Vector2.zero;
            }
            return legend;
        }

        // 地図の上のパーティ。戦闘の姿を1ドット1pxで、部屋の中心のまわりに並べる。
        // パーティは、4人の代わりに木の駒1つで地図の上を歩く。
        private static void BuildParty(RectTransform party, ExplorationView view)
        {
            var pawn = MapPawnAssets.Build(party, "Pawn", PawnHeight);
            pawn.anchoredPosition = PawnFeet;
            view.Party = new[] { pawn };
            view.Formation = new[] { PawnFeet };
        }

        private static void BuildChest(RectTransform map, ExplorationView view)
        {
            var closed = ArtAssets.LoadTexture(ChestClosedPath);
            var body = Rect("Chest", map);
            Place(body, Vector2.zero, Vector2.zero);
            view.ChestGroup = body.gameObject.AddComponent<CanvasGroup>();
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            Picture(body, "Shadow", shadow, new Vector2(0, 2), new Vector2(96, 18), 0.5f);
            // 宝箱は地図と同じ1ドット3pxで置く。
            var sprite = PixelActor(body, "Sprite", closed, Vector2.zero, MapDot);
            var tap = Rect("Tap", body);
            Place(tap, new Vector2(0, 48), new Vector2(150, 150));
            view.ChestButton = UiBuild.AddButton(tap, AddImage(tap, new Color(0, 0, 0, 0), true));
            view.Chest = body;
            view.ChestSprite = sprite;
            view.ChestClosed = closed;
            view.ChestOpen = ArtAssets.LoadTexture(ChestOpenPath);
            view.ChestGroup.alpha = 0f;
            view.ChestGroup.blocksRaycasts = false;
        }

        // The menu at the top left like the guide screens' back button, or at the top right over
        // the battle, where the turn order takes the top left.
        private static Button BuildMenuButton(RectTransform safe, bool right = false)
        {
            var menu = Rect("Menu", safe);
            Corner(
                menu,
                right ? Vector2.one : new Vector2(0, 1),
                right ? new Vector2(-40, -24) : new Vector2(40, -24),
                new Vector2(MenuWidth, MenuHeight)
            );
            var shadow = Rect("Shadow", menu);
            Stretch(shadow);
            shadow.offsetMin = new Vector2(-56, -56);
            shadow.offsetMax = new Vector2(56, 32);
            var spot = SpriteImage(shadow, UiArt.SoftSpotPath, SpotShadow);
            spot.raycastTarget = true;
            var button = AddTintButton(menu, spot);
            var icon = Rect("Icon", menu);
            Place(
                icon,
                new Vector2(0, 24),
                new Vector2(GuideMenuAssets.IconSize, GuideMenuAssets.IconSize)
            );
            SpriteImage(icon, UiArt.IconCompassPath, Color.white);
            var label = Label(menu, "Label", "メニュー", 32, TextMain, TextAlignmentOptions.Center);
            Place(
                (RectTransform)label.transform,
                new Vector2(0, -60),
                new Vector2(MenuWidth + 40, 40)
            );
            return button;
        }

        // 地図の上の札と帯：景色の上に直に置く、模様のないすりガラスの板。文字に合わせて伸び縮みする札でも
        // 並べ方に入らないよう、板は並べ方の外の子に作る。
        private static void ScenePlaque(RectTransform parent)
        {
            var plate = Rect("Plate", parent);
            Stretch(plate);
            plate.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            GlassCardAssets.Plaque(plate, pattern: false, fill: GlassCardAssets.SceneFillColor);
        }

        // 案内は、右上の行き先と層の下に小さく添える。
        private static TMP_Text BuildPrompt(RectTransform safe)
        {
            var prompt = Label(
                safe,
                "Prompt",
                "",
                32,
                UiPalette.TextSub,
                TextAlignmentOptions.Right
            );
            Corner(
                (RectTransform)prompt.transform,
                Vector2.one,
                new Vector2(-56, -104),
                new Vector2(900, 48)
            );
            return prompt;
        }

        // --- Battle overlay ------------------------------------------------------------------

        private static void BuildOverlay()
        {
            var root = CanvasRoot("AdventureOverlay");
            // Over the battle's screen canvas (order 1).
            root.GetComponent<Canvas>().sortingOrder = 5;
            var overlay = root.gameObject.AddComponent<AdventureOverlay>();
            overlay.ItemArt = Baryonyx.Inventory.Editor.ItemArtAssets.Load();
            var safe = SafeArea(root);
            overlay.MenuButton = BuildMenuButton(safe, right: true);
            overlay.Reveal = RewardRevealAssets.Build(safe, DropPlateCenter);
            // 戦闘後の強化は、ドロップの札の上、通知とダイアログの下に重ねる。
            overlay.Boons = AdventureBoonPickerAssets.Build(root);
            overlay.Notice = NoticeBandAssets.Build(root);
            overlay.Dialog = GameDialogAssets.Build(root);
            CollectTintGraphics(root);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, OverlayPrefabPath);
        }
    }
}
