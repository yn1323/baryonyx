using System;
using System.Collections.Generic;
using System.IO;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Home.Editor;
using Baryonyx.StepBonus;
using Baryonyx.StepBonus.Editor;
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
    /// Builds the adventure's screens: the exploration screen (the map painted on the device with
    /// its room markers, the party and the chest, the floors, the place, the prompt, the menu and
    /// the dialog), the pictures the map is painted with, and the overlay laid over the battle
    /// screen (the menu, the dialog and the notice band). Coordinates follow the 1920x1080 design;
    /// the map is 2400x1080, three pixels a dot, centred so a wider screen shows more of it.
    /// </summary>
    public static class AdventureAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Adventure";
        public const string ExplorationPrefabPath = Folder + "/UI/ExplorationScreen.prefab";
        public const string OverlayPrefabPath = Folder + "/UI/AdventureOverlay.prefab";
        public const string MapArtPath = Folder + "/Data/ExplorationMapArt.asset";
        public const string ArtFolder = Folder + "/UI/Art";
        public const string IconBattlePath = ArtFolder + "/IconRoomBattle.aseprite";
        public const string IconElitePath = ArtFolder + "/IconRoomElite.aseprite";
        public const string IconTreasurePath = ArtFolder + "/IconRoomTreasure.aseprite";
        public const string IconBossPath = ArtFolder + "/IconRoomBoss.aseprite";

        public const string StageArtFolder = "Assets/Baryonyx/Shared/Art/Stages/Exploration";
        public const string ChestClosedPath = StageArtFolder + "/ChestClosed.aseprite";
        public const string ChestOpenPath = StageArtFolder + "/ChestOpen.aseprite";

        // 地図に立てる絵。木と下草は、近い行に使う原寸と、遠い行に使う半分の大きさがある。
        public const string MapTreesPath = StageArtFolder + "/MapTrees.aseprite";
        public const string MapUndergrowthPath = StageArtFolder + "/MapUndergrowth.aseprite";
        public const string MapTreesFarPath = StageArtFolder + "/MapTreesFar.aseprite";
        public const string MapUndergrowthFarPath = StageArtFolder + "/MapUndergrowthFar.aseprite";
        public const string MapRuinPath = StageArtFolder + "/MapRuin.aseprite";

        private const string CharacterArtFolder = "Assets/Baryonyx/Shared/Art/Characters";

        // 地図は1ドット3px。戦闘のキャラは地図の上では1ドット1pxの小さい姿にする。
        private const float MapDot = ExplorationMapProjection.Dot;
        private const float PartyDot = 1f;

        // 部屋の中心からの隊列（設計座標）。後ろの者から描く。
        private static readonly (string Name, Vector2 Offset)[] Party =
        {
            ("Mina", new Vector2(-12, 12)),
            ("Luka", new Vector2(24, 9)),
            ("Toma", new Vector2(-30, -6)),
            ("Aria", new Vector2(9, -12)),
        };

        private static readonly Color TextMain = GuideMenuAssets.TextMain;
        private static readonly Color Plate = new(0.02f, 0.024f, 0.047f, 0.82f);
        private static readonly Color MarkerPlate = new(0.11f, 0.165f, 0.19f, 0.96f);
        private static readonly Color SpotShadow = new(0.012f, 0.02f, 0.04f, 0.78f);

        private const float MenuWidth = 128f;
        private const float MenuHeight = 168f;

        [MenuItem("Baryonyx/Adventure/Create Screen Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Path.GetDirectoryName(ExplorationPrefabPath));
            Directory.CreateDirectory(Path.GetDirectoryName(MapArtPath));
            AssetDatabase.Refresh();
            UiArt.EnsureAll();
            GuideMenuAssets.CreateSharedArt();
            foreach (
                var path in new[] { IconBattlePath, IconElitePath, IconTreasurePath, IconBossPath }
            )
                ArtAssets.ImportDrawn(path);
            foreach (var path in new[] { ChestClosedPath, ChestOpenPath })
                ArtAssets.ImportTexture(path, FilterMode.Point);
            ArtAssets.ImportDrawn(HomeScreenArt.IconCompassPath);
            foreach (var member in Party)
                ArtAssets.ImportTexture(
                    $"{CharacterArtFolder}/Battle{member.Name}.aseprite",
                    FilterMode.Point
                );

            var art = CreateMapArt();
            var font = GameFontAssets.GetOrCreate();
            var shadowText = UiArt.EnsureTextShadow(font);
            var bonuses = AssetDatabase.LoadAssetAtPath<StepBonusMockData>(
                StepBonusAssets.DataPath
            );
            using (Begin(font, shadowText))
            {
                BuildExploration(bonuses, art);
                BuildOverlay(bonuses);
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
            };

        // --- Map art -------------------------------------------------------------------------

        // .aseprite の絵は実行中に画素を読めないため、地図に立てる絵の画素を Data に写しておく。
        public static ExplorationMapArt CreateMapArt()
        {
            var art = AssetDatabase.LoadAssetAtPath<ExplorationMapArt>(MapArtPath);
            bool created = art == null;
            if (created)
                art = ScriptableObject.CreateInstance<ExplorationMapArt>();
            art.Trees = Stamps(MapTreesPath);
            art.Undergrowth = Stamps(MapUndergrowthPath);
            art.TreesFar = Stamps(MapTreesFarPath);
            art.UndergrowthFar = Stamps(MapUndergrowthFarPath);
            var ruin = Stamps(MapRuinPath);
            art.Ruin = ruin.Length > 0 ? ruin[0] : new PixelStamp();
            if (created)
                AssetDatabase.CreateAsset(art, MapArtPath);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssetIfDirty(art);
            return art;
        }

        // 1フレームを1枚の絵とし、絵のある範囲だけに切り詰める。下辺が根元になる。
        private static PixelStamp[] Stamps(string path)
        {
            ArtAssets.ImportDrawn(path);
            var frames = AsepriteCanvasImport.LoadFrames(path);
            var stamps = new List<PixelStamp>();
            for (int frame = 0; frame < frames.Count; frame++)
            {
                var size = Vector2Int.RoundToInt(frames[frame].rect.size);
                var pixels = AsepriteCanvasImport.ReadFramePixels(path, frame);
                int minX = size.x,
                    minY = size.y,
                    maxX = -1,
                    maxY = -1;
                for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    if (pixels[y * size.x + x].a == 0)
                        continue;
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
                if (maxX < 0)
                    continue;
                int width = maxX - minX + 1;
                int height = maxY - minY + 1;
                var cropped = new Color32[width * height];
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    cropped[y * width + x] = pixels[(minY + y) * size.x + minX + x];
                stamps.Add(
                    PixelStamp.From(
                        $"{Path.GetFileNameWithoutExtension(path)}{frame}",
                        width,
                        height,
                        cropped
                    )
                );
            }
            return stamps.ToArray();
        }

        // --- Exploration ---------------------------------------------------------------------

        private static void BuildExploration(StepBonusMockData bonuses, ExplorationMapArt art)
        {
            var root = CanvasRoot("ExplorationScreen");
            var view = root.gameObject.AddComponent<ExplorationView>();
            view.KindIcons = KindIcons();
            view.Bonuses = bonuses;
            view.Art = art;
            // 展示室でPrefabだけを開いたときに描く見本の道。冒険の場面では使わない。
            view.SampleSeed = 20261004;

            var backdrop = Rect("Backdrop", root);
            Stretch(backdrop);
            AddImage(backdrop, ScreenScenes.CameraColor, false);

            var map = Rect("Map", root);
            Place(
                map,
                Vector2.zero,
                new Vector2(ExplorationMapProjection.Width, ExplorationMapProjection.Height)
                    * MapDot
            );
            view.Map = map;
            view.MapGroup = map.gameObject.AddComponent<CanvasGroup>();
            var picture = Rect("Picture", map);
            Stretch(picture);
            view.MapImage = picture.gameObject.AddComponent<RawImage>();
            view.MapImage.raycastTarget = false;
            // 地図の絵が届くまでは、背景と同じ暗い色にしておく。
            view.MapImage.color = ScreenScenes.CameraColor;

            var markers = Rect("Markers", map);
            Stretch(markers);
            view.Markers = markers;
            view.MarkerTemplate = BuildMarker(markers);
            BuildParty(map, view);
            BuildChest(map, view);

            var safe = SafeArea(root);
            BuildFloors(safe, view);
            view.MenuButton = BuildMenuButton(safe);
            view.Location = Label(
                safe,
                "Location",
                AdventureCatalog.DestinationName(AdventureCatalog.ForestRuins) + " B1F",
                64,
                TextMain,
                TextAlignmentOptions.Right
            );
            Corner(
                (RectTransform)view.Location.transform,
                Vector2.one,
                new Vector2(-56, -32),
                new Vector2(900, 96)
            );
            view.Prompt = BuildPrompt(safe);

            view.Notice = NoticeBandAssets.Build(root);
            view.Dialog = GameDialogAssets.Build(root);
            CollectTintGraphics(root);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, ExplorationPrefabPath);
        }

        // 部屋の印の型：光、丸い札、部屋の種類のアイコン、押せる範囲、横の手掛かり。
        private static ExplorationMapMarker BuildMarker(RectTransform markers)
        {
            var body = Rect("RoomTemplate", markers);
            Place(body, Vector2.zero, new Vector2(150, 150));
            var marker = body.gameObject.AddComponent<ExplorationMapMarker>();
            marker.Body = body;

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
            marker.Plate = SpriteImage(plate, UiArt.CirclePath, MarkerPlate);
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

            var hintBox = Rect("Hint", body);
            Place(hintBox, new Vector2(66, 0), new Vector2(300, 64));
            hintBox.pivot = new Vector2(0, 0.5f);
            var hintPlate = AddImage(hintBox, Plate, false);
            hintPlate.sprite = ArtAssets.LoadSprite(UiArt.RoundedRectPath);
            hintPlate.type = Image.Type.Sliced;
            var fitter = hintBox.gameObject.AddComponent<HorizontalLayoutGroup>();
            fitter.padding = new RectOffset(20, 20, 6, 6);
            fitter.childControlWidth = true;
            fitter.childControlHeight = true;
            fitter.childForceExpandWidth = false;
            hintBox.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            marker.HintBox = hintBox;
            marker.Hint = Label(
                hintBox,
                "Text",
                "魔物の気配",
                32,
                TextMain,
                TextAlignmentOptions.Center
            );
            return marker;
        }

        // 地図の上のパーティ。戦闘の姿を1ドット1pxで、部屋の中心のまわりに並べる。
        private static void BuildParty(RectTransform map, ExplorationView view)
        {
            var party = Rect("Party", map);
            Stretch(party);
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            var bodies = new List<RectTransform>();
            foreach (var member in Party)
            {
                var texture = ArtAssets.LoadTexture(
                    $"{CharacterArtFolder}/Battle{member.Name}.aseprite"
                );
                var body = Rect("Ally" + member.Name, party);
                Place(body, member.Offset, Vector2.zero);
                Picture(body, "Shadow", shadow, new Vector2(0, 1), new Vector2(40, 8), 0.5f);
                PixelActor(body, "Sprite", texture, Vector2.zero, PartyDot);
                bodies.Add(body);
            }
            view.Party = bodies.ToArray();
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

        // 左端の階の目安。地図の各階の高さに置く。
        private static void BuildFloors(RectTransform safe, ExplorationView view)
        {
            var floors = Rect("Floors", safe);
            floors.anchorMin = new Vector2(0, 0.5f);
            floors.anchorMax = new Vector2(0, 0.5f);
            floors.pivot = new Vector2(0, 0.5f);
            floors.sizeDelta = new Vector2(140, 1080);
            floors.anchoredPosition = new Vector2(24, 0);
            view.Floors = floors;
            var label = Label(
                floors,
                "FloorTemplate",
                "B1F",
                26,
                GuideMenuAssets.TextSub,
                TextAlignmentOptions.Left
            );
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.sizeDelta = new Vector2(140, 40);
            rect.anchoredPosition = Vector2.zero;
            view.FloorTemplate = label;
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
            SpriteImage(icon, HomeScreenArt.IconCompassPath, Color.white);
            var label = Label(menu, "Label", "メニュー", 32, TextMain, TextAlignmentOptions.Center);
            Place(
                (RectTransform)label.transform,
                new Vector2(0, -60),
                new Vector2(MenuWidth + 40, 40)
            );
            return button;
        }

        private static TMP_Text BuildPrompt(RectTransform safe)
        {
            var band = Rect("PromptBand", safe);
            band.anchorMin = band.anchorMax = band.pivot = new Vector2(0.5f, 0f);
            band.anchoredPosition = new Vector2(0, 40);
            band.sizeDelta = new Vector2(1100, 96);
            var image = AddImage(band, Plate, false);
            image.sprite = ArtAssets.LoadSprite(UiArt.RoundedRectPath);
            image.type = Image.Type.Sliced;
            var prompt = Label(band, "Prompt", "", 44, TextMain, TextAlignmentOptions.Center);
            Stretch((RectTransform)prompt.transform);
            return prompt;
        }

        // --- Battle overlay ------------------------------------------------------------------

        private static void BuildOverlay(StepBonusMockData bonuses)
        {
            var root = CanvasRoot("AdventureOverlay");
            // Over the battle's screen canvas (order 1).
            root.GetComponent<Canvas>().sortingOrder = 5;
            var overlay = root.gameObject.AddComponent<AdventureOverlay>();
            overlay.Bonuses = bonuses;
            var safe = SafeArea(root);
            overlay.MenuButton = BuildMenuButton(safe, right: true);
            overlay.Notice = NoticeBandAssets.Build(root);
            overlay.Dialog = GameDialogAssets.Build(root);
            CollectTintGraphics(root);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, OverlayPrefabPath);
        }
    }
}
