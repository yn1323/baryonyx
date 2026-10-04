using System;
using System.Collections.Generic;
using System.IO;
using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Home.Editor;
using Baryonyx.StepBonus;
using Baryonyx.StepBonus.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu.Editor;
using Baryonyx.Vfx.Hd2d.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Adventure.Editor
{
    /// <summary>
    /// Builds the adventure's screens: the exploration screen (the party and the doors on the
    /// battle's stage, the chest, the place, the prompt, the menu, the route and the dialog) and
    /// the overlay laid over the battle screen (the menu, the dialog and the notice band).
    /// Coordinates follow the 1920x1080 design; the party stands where it stands in battle and
    /// the doors where the enemies stand.
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

        public const string StageArtFolder = "Assets/Baryonyx/Shared/Art/Stages/Exploration";
        public const string DoorPath = StageArtFolder + "/RuinDoor.aseprite";
        public const string ChestClosedPath = StageArtFolder + "/ChestClosed.aseprite";
        public const string ChestOpenPath = StageArtFolder + "/ChestOpen.aseprite";

        // 3Dの舞台がない場所（展示室のPrefabの単体表示）で見せる、森の遺跡の描いた背景。
        public const string FlatBackgroundPath = "Assets/Baryonyx/Shared/Art/Stages/Forest.png";
        private const string CharacterArtFolder = "Assets/Baryonyx/Shared/Art/Characters";

        // 味方は戦闘と同じ1ドット4px、扉は敵と同じ3px、宝箱は4px。
        private const float PartyDot = 4f;
        private const float DoorDot = 3f;
        private const float ChestDot = 4f;

        // 後ろの者から描く。戦闘画面（BattleInspectAssets.Allies）と同じ足元。
        private static readonly (string Name, Vector2 Feet)[] Party =
        {
            ("Mina", new Vector2(-690, 170)),
            ("Toma", new Vector2(-420, 40)),
            ("Luka", new Vector2(-690, -100)),
            ("Aria", new Vector2(-420, -220)),
        };

        // 扉は戦闘で敵が立つ側に、奥と手前の2つ。入口が1つの部屋は奥の扉だけを使う。
        internal static readonly Vector2[] DoorFeet = { new(330, 150), new(720, -130) };
        internal static readonly Vector2 ChestFeet = new(120, -170);

        private static readonly Color TextMain = GuideMenuAssets.TextMain;
        private static readonly Color Plate = new(0.02f, 0.024f, 0.047f, 0.82f);
        private static readonly Color SpotShadow = new(0.012f, 0.02f, 0.04f, 0.78f);

        private const float MenuWidth = 128f;
        private const float MenuHeight = 168f;

        [MenuItem("Baryonyx/Adventure/Create Screen Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Path.GetDirectoryName(ExplorationPrefabPath));
            AssetDatabase.Refresh();
            UiArt.EnsureAll();
            GuideMenuAssets.CreateSharedArt();
            foreach (
                var path in new[] { IconBattlePath, IconElitePath, IconTreasurePath, IconBossPath }
            )
                ArtAssets.ImportDrawn(path);
            foreach (var path in new[] { DoorPath, ChestClosedPath, ChestOpenPath })
                ArtAssets.ImportTexture(path, FilterMode.Point);
            ArtAssets.ImportDrawn(HomeScreenArt.IconCompassPath);
            ArtAssets.ImportTexture(FlatBackgroundPath, FilterMode.Point);
            foreach (var member in Party)
                ArtAssets.ImportTexture(
                    $"{CharacterArtFolder}/Battle{member.Name}.aseprite",
                    FilterMode.Point
                );

            var font = GameFontAssets.GetOrCreate();
            var shadowText = UiArt.EnsureTextShadow(font);
            var bonuses = AssetDatabase.LoadAssetAtPath<StepBonusMockData>(
                StepBonusAssets.DataPath
            );
            using (Begin(font, shadowText))
            {
                BuildExploration(bonuses);
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

        // --- Exploration ---------------------------------------------------------------------

        private static void BuildExploration(StepBonusMockData bonuses)
        {
            var root = Rect("ExplorationScreen", null);
            Stretch(root);
            var view = root.gameObject.AddComponent<ExplorationView>();
            view.KindIcons = KindIcons();
            view.Bonuses = bonuses;

            // The stage canvas is drawn by the scene's camera over the 3D stage, like the battle's.
            var stageCanvas = CanvasRoot("StageCanvas");
            stageCanvas.SetParent(root, false);
            BattleSkillVfxAssets.MakeStageCanvas(stageCanvas);
            var screen = CanvasRoot("ScreenCanvas");
            screen.SetParent(root, false);
            screen.GetComponent<Canvas>().sortingOrder = 1;

            var drift = Rect("StageDrift", stageCanvas);
            Stretch(drift);
            drift.gameObject.AddComponent<BattleStageDrift>();
            var stage = Rect("Stage", drift);
            Stretch(stage);
            BuildFlatBackground(stage);
            var world = BattleSkillVfxAssets.Layer("World", stage);
            Hd2dStageKit.SortLayer(world, Hd2dStageKit.BoardOrder + 10);
            BuildParty(world, view);
            BuildDoors(world, view);
            BuildChest(world, view);

            // Taps land on the screen canvas, laid out like the world (the stage canvas takes none).
            var taps = BattleSkillVfxAssets.Layer("Taps", screen);
            BuildTaps(taps, view);

            var safe = SafeArea(screen);
            view.MenuButton = BuildMenuButton(safe);
            view.Location = Label(
                safe,
                "Location",
                "森の遺跡 B1F",
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

            view.Route = BuildRoute(screen);
            view.Notice = NoticeBandAssets.Build(screen);
            view.Dialog = GameDialogAssets.Build(screen);
            CollectTintGraphics(root);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, ExplorationPrefabPath);
        }

        private static void BuildFlatBackground(RectTransform stage)
        {
            var art = ArtAssets.LoadTexture(FlatBackgroundPath);
            var backdrop = BattleSkillVfxAssets.Overscan("Backdrop", stage);
            var image = Rect("Background", backdrop).gameObject.AddComponent<RawImage>();
            image.texture = art;
            image.raycastTarget = false;
            image.gameObject.AddComponent<ResponsiveBackground>().AspectRatio =
                art.width / (float)art.height;
            // The 3D stage takes the painted background's place when the scene has one.
            Hd2dStageKit.FlatOnly(image.gameObject);
            Shade(stage, "ShadeTop", top: true, 240f, 0.6f);
            Shade(stage, "ShadeBottom", top: false, 300f, 0.75f);
        }

        private static void BuildParty(RectTransform world, ExplorationView view)
        {
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                BattleInspectAssets.PixelArtMaterialPath
            );
            var bodies = new List<RectTransform>();
            foreach (var member in Party)
            {
                var texture = ArtAssets.LoadTexture(
                    $"{CharacterArtFolder}/Battle{member.Name}.aseprite"
                );
                var body = Rect("Ally" + member.Name, world);
                Place(body, member.Feet, Vector2.zero);
                var footShadow = Picture(
                    body,
                    "Shadow",
                    shadow,
                    new Vector2(0, 2),
                    new Vector2(150, 24),
                    0.5f
                );
                var sprite = PixelActor(body, "Sprite", texture, Vector2.zero, PartyDot);
                if (material != null)
                    sprite.material = material;
                Hd2dStageKit.Stand(body.gameObject, sprite, footShadow);
                bodies.Add(body);
            }
            view.Party = bodies.ToArray();
        }

        private static void BuildDoors(RectTransform world, ExplorationView view)
        {
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            var art = ArtAssets.LoadTexture(DoorPath);
            var doors = new List<ExplorationDoorWidget>();
            for (int i = 0; i < DoorFeet.Length; i++)
            {
                var body = Rect("Door" + i, world);
                Place(body, DoorFeet[i], Vector2.zero);
                var group = body.gameObject.AddComponent<CanvasGroup>();
                var footShadow = Picture(
                    body,
                    "Shadow",
                    shadow,
                    new Vector2(0, 2),
                    new Vector2(200, 30),
                    0.45f
                );
                var sprite = PixelActor(body, "Sprite", art, Vector2.zero, DoorDot);
                Hd2dStageKit.Stand(body.gameObject, sprite, footShadow);

                // The plate on the ground in front of the door: the room's kind and a short hint.
                // Over the door it stood apart from it, as the board stands shorter than its picture.
                var plate = Rect("Plate", body);
                Place(plate, new Vector2(0, -68), new Vector2(340, 88));
                AddImage(plate, Plate, false).sprite = ArtAssets.LoadSprite(UiArt.RoundedRectPath);
                plate.GetComponent<Image>().type = Image.Type.Sliced;
                var icon = Rect("Icon", plate);
                // The 24x24 icon at 3x, so each dot keeps a whole number of pixels.
                Place(icon, new Vector2(-124, 0), new Vector2(72, 72));
                var iconImage = icon.gameObject.AddComponent<Image>();
                iconImage.sprite = ArtAssets.LoadSprite(IconBattlePath);
                iconImage.raycastTarget = false;
                var hint = Label(
                    plate,
                    "Hint",
                    "魔物の気配",
                    36,
                    TextMain,
                    TextAlignmentOptions.Left
                );
                Place((RectTransform)hint.transform, new Vector2(40, 0), new Vector2(220, 64));
                GuideMenuAssets.Shrink(hint, 24);
                doors.Add(
                    new ExplorationDoorWidget
                    {
                        Body = body,
                        Group = group,
                        Icon = iconImage,
                        Hint = hint,
                    }
                );
            }
            view.Doors = doors.ToArray();
        }

        private static void BuildChest(RectTransform world, ExplorationView view)
        {
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            var closed = ArtAssets.LoadTexture(ChestClosedPath);
            var body = Rect("Chest", world);
            Place(body, ChestFeet, Vector2.zero);
            view.ChestGroup = body.gameObject.AddComponent<CanvasGroup>();
            var footShadow = Picture(
                body,
                "Shadow",
                shadow,
                new Vector2(0, 2),
                new Vector2(150, 26),
                0.5f
            );
            var sprite = PixelActor(body, "Sprite", closed, Vector2.zero, ChestDot);
            Hd2dStageKit.Stand(body.gameObject, sprite, footShadow);
            view.Chest = body;
            view.ChestSprite = sprite;
            view.ChestClosed = closed;
            view.ChestOpen = ArtAssets.LoadTexture(ChestOpenPath);
            view.ChestGroup.alpha = 0f;
            view.ChestGroup.blocksRaycasts = false;
        }

        // 扉と宝箱を押せる範囲。舞台の絵と同じ位置に、手前の画面で受け取る。
        private static void BuildTaps(RectTransform taps, ExplorationView view)
        {
            float doorHeight = 96 * DoorDot;
            for (int i = 0; i < view.Doors.Length; i++)
            {
                var tap = Rect("DoorTap" + i, taps);
                // From the plate in front of the door (110 under the feet) to the door's top.
                Place(
                    tap,
                    DoorFeet[i] + new Vector2(0, (doorHeight - 110) / 2f),
                    new Vector2(340, doorHeight + 110)
                );
                var hit = AddImage(tap, new Color(0, 0, 0, 0), true);
                view.Doors[i].Button = UiBuild.AddButton(tap, hit);
            }
            var chest = Rect("ChestTap", taps);
            Place(chest, ChestFeet + new Vector2(0, 64), new Vector2(200, 200));
            view.ChestButton = UiBuild.AddButton(
                chest,
                AddImage(chest, new Color(0, 0, 0, 0), true)
            );
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

        private static AdventureRouteView BuildRoute(RectTransform screen)
        {
            var root = Rect("Route", screen);
            Stretch(root);
            AddImage(root, new Color(0.012f, 0.02f, 0.04f, 0.82f), true);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var route = root.gameObject.AddComponent<AdventureRouteView>();
            route.Group = group;
            route.KindIcons = KindIcons();
            route.Frame = ArtAssets.LoadSprite(GuideMenuAssets.FramePath);
            route.FrameHere = ArtAssets.LoadSprite(GuideMenuAssets.FrameSelectedPath);

            var safe = SafeArea(root);
            var window = Rect("Window", safe);
            Place(window, new Vector2(0, 0), new Vector2(1640, 960));
            GuideMenuAssets.Frame(window, GuideMenuAssets.FramePath, Color.white);
            var title = Label(
                window,
                "Title",
                "ルート",
                52,
                GuideMenuAssets.Gold,
                TextAlignmentOptions.TopLeft
            );
            GuideMenuAssets.Fill(
                (RectTransform)title.transform,
                new Vector2(48, 0),
                new Vector2(-48, -28)
            );

            var area = Rect("Area", window);
            Stretch(area);
            area.offsetMin = new Vector2(48, 48);
            area.offsetMax = new Vector2(-260, -108);
            route.Area = area;

            var node = Rect("RoomTemplate", area);
            Place(node, Vector2.zero, new Vector2(200, 112));
            GuideMenuAssets.Frame(node, GuideMenuAssets.FramePath, Color.white).raycastTarget =
                false;
            var icon = Rect("Icon", node);
            Place(icon, new Vector2(0, 12), new Vector2(72, 72));
            icon.gameObject.AddComponent<Image>().raycastTarget = false;
            var name = Label(node, "Name", "", 26, TextMain, TextAlignmentOptions.Center);
            Place((RectTransform)name.transform, new Vector2(0, -36), new Vector2(190, 32));
            GuideMenuAssets.Shrink(name, 18);
            route.RoomTemplate = node;

            var line = Rect("LineTemplate", area);
            Place(line, Vector2.zero, new Vector2(100, 8));
            route.LineTemplate = AddImage(line, Color.white, false);

            var floor = Label(
                area,
                "FloorTemplate",
                "B1F",
                36,
                GuideMenuAssets.TextSub,
                TextAlignmentOptions.Center
            );
            Place((RectTransform)floor.transform, Vector2.zero, new Vector2(140, 48));
            route.FloorTemplate = floor;

            var close = Rect("Close", window);
            Corner(close, new Vector2(1, 0), new Vector2(-40, 40), new Vector2(200, 112));
            var frame = GuideMenuAssets.Frame(close, GuideMenuAssets.FramePath, Color.white);
            route.CloseButton = GuideMenuAssets.AddButton(close, frame);
            var closeLabel = Label(
                close,
                "Label",
                AdventureTexts.CloseChoice,
                44,
                TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)closeLabel.transform);

            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return route;
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
