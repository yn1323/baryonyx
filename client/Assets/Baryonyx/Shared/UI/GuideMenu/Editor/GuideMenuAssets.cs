using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.UI.GuideMenu.Editor
{
    /// <summary>
    /// Generates each feature's guide screen prefab from its definition. Every menu row, list
    /// row and destination is baked into the prefab, so the text can be read in the editor without
    /// Play Mode. Coordinates follow the 1920x1080 design: the art fills the screen and the
    /// controls sit in the Safe Area.
    /// </summary>
    public static class GuideMenuAssets
    {
        public const string Folder = "Assets/Baryonyx/Shared/UI/GuideMenu";
        public const string ArtFolder = Folder + "/Art";
        public const string FramePath = ArtFolder + "/GuideFrame.png";
        public const string FrameSelectedPath = ArtFolder + "/GuideFrameSelected.png";
        public const string ShadeHorizontalPath = ArtFolder + "/GuideShadeHorizontal.png";
        public const string SoftSpotPath = ArtFolder + "/GuideSoftSpot.png";
        public const string ArrowPath = ArtFolder + "/GuideArrow.png";
        public const string IconBackPath = ArtFolder + "/IconBack.aseprite";
        public const string IconClosePath = ArtFolder + "/IconClose.aseprite";
        public const string IconRunePath =
            "Assets/Baryonyx/Shared/Art/GameResources/IconRune.aseprite";
        public const string TextShadowPath = Folder + "/GuideTextShadow.mat";

        // The window frames and the 24x24 icons are drawn at 4 design pixels per dot, like the
        // game's sprites.
        public const float DotScale = 4f;
        public const float IconSize = 24f * DotScale;

        // A frame keeps its 4-dot corners; the 16-dot edges and fill repeat (Image.Type.Tiled).
        private const int FrameCorner = 4;
        private const int FrameTile = 16;

        // The guide stands in this box at the bottom left; the lists start right of it.
        private const float GuideLeft = 24f;
        private const float GuideMaxWidth = 800f;
        private const float GuideMaxHeight = 1048f;
        public const float RightLeft = 848f;

        // The back button: a tab of the frames' navy against the safe area's top-left corner,
        // with the arrow and the label side by side. The 24x24 icon keeps its 4x scale; the
        // arrow fills its middle, so the label starts where the arrow ends rather than where the
        // icon's clear margin does.
        private const float BackWidth = 220f;
        private const float BackHeight = IconSize;
        private const float BackLabelLeft = 88f;

        // How far the tab reaches past the safe area's top and left edges, so only its right and
        // bottom edges show even where a notch insets the safe area.
        private const float BackBleed = 240f;

        // The close button at the top right mirrors the back tab: the same navy, bleeding past the
        // top and right edges, with the 24x24 cross at 4x in the middle. 128 wide is the touch minimum.
        private const float CloseWidth = 128f;

        // The breadcrumb starts right of the back tab; the rune balance sits left of the close tab.
        private const float HeaderGap = 32f;
        private const float BreadcrumbSize = 44f;
        private const float BreadcrumbMinSize = 28f;
        private const float RuneIconSize = 24f * 2f;
        private const float RuneTextSize = 40f;

        // Room the rune balance keeps from the breadcrumb (its pill grows leftward with the digits).
        private const float RuneRoom = 360f;

        // 枠つきのリスト（商会・神殿の一覧、旅の案内所の行き先）：案内人の右に金の2本線を引き、
        // 右の透ける面に見出しとすりガラスのカードを並べる。設計座標、左上から。
        private const float ListSplit = RightLeft;
        private const float ListGap = 24f;
        private const float ListHeadingHeight = 76f;
        private const float EntryHeight = 128f;
        private const float EntryGap = 16f;
        private const float ConfirmWidth = 360f;
        private const float ConfirmHeight = 104f;
        private const float ConfirmBottom = 32f;

        // A locked row (an unexplored destination) is drawn at this opacity, with its name hidden.
        private const float LockedAlpha = 0.5f;
        private const string HiddenName = "？？？";

        private static TMP_FontAsset font;
        private static Material shadowText;

        // 組み立て中の画面の、枠つきのパネルを開いたときだけ出す部品と、透明にするタブ。
        private static readonly List<GameObject> frameParts = new();
        private static readonly List<Graphic> tabBackdrops = new();

        [MenuItem("Baryonyx/Guide Menu/Create Shared Art")]
        public static void CreateSharedArt()
        {
            Directory.CreateDirectory(ArtFolder);
            WriteArt();
            GuideFrameAssets.EnsureArt();
        }

        /// <summary>
        /// Writes a feature's definition and builds its screen prefab from it. The guide is
        /// scaled to the largest size that fits the guide box.
        /// </summary>
        public static GameObject CreateScreen(
            string definitionPath,
            string prefabPath,
            string guideArtPath,
            string backgroundPath,
            Action<GuideMenuDefinition> fill,
            Func<GuideMenuItem, RectTransform, GuideMenuView, GameObject> itemPanel = null
        )
        {
            EditorGuard.RequireEditMode();
            CreateSharedArt();
            font = GameFontAssets.GetOrCreate();
            shadowText = UiArt.EnsureTextShadow(
                font,
                TextShadowPath,
                new Vector2(0.4f, -0.6f),
                0.2f
            );

            Directory.CreateDirectory(Path.GetDirectoryName(definitionPath));
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            AssetDatabase.Refresh();

            var definition = AssetFolders.LoadOrCreate<GuideMenuDefinition>(definitionPath);
            fill(definition);
            definition.GuideArt = ArtAssets.ImportTexture(guideArtPath, FilterMode.Point);
            // The backgrounds are generated illustrations used as they are.
            definition.Background = ArtAssets.ImportTexture(backgroundPath, FilterMode.Bilinear);
            definition.GuideDotSize = FitDotSize(definition.GuideArt);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);

            var prefab = BuildPrefab(
                definition,
                Path.GetFileNameWithoutExtension(prefabPath),
                prefabPath,
                itemPanel
            );
            AssetDatabase.SaveAssetIfDirty(font);
            return prefab;
        }

        /// <summary>A 24x24 pixel-art icon drawn in Aseprite, used as a sprite for a menu row.</summary>
        public static Sprite Icon(string path)
        {
            ArtAssets.ImportDrawn(path);
            return AsepriteCanvasImport.LoadSprite(path);
        }

        // The largest quarter-pixel dot size that keeps the whole guide inside the guide box.
        private static float FitDotSize(Texture2D art)
        {
            float fit = Mathf.Min(GuideMaxWidth / art.width, GuideMaxHeight / art.height);
            return Mathf.Floor(fit * 4f) / 4f;
        }

        // --- Mock content ------------------------------------------------------------------

        public static GuideMenuItem Item(
            string label,
            string caption,
            string confirmLabel,
            Sprite icon,
            params GuideListEntry[] entries
        ) =>
            new()
            {
                Label = label,
                Caption = caption,
                ConfirmLabel = confirmLabel,
                Icon = icon,
                Entries = entries,
            };

        public static GuideListEntry Entry(string name, string badge, string detail) =>
            new()
            {
                Name = name,
                Badge = badge,
                Detail = detail,
            };

        public static GuideDestination Destination(
            string name,
            string detail,
            int recommendedLevel,
            bool locked = false,
            string id = ""
        ) =>
            new()
            {
                Id = id,
                Name = name,
                Detail = detail,
                RecommendedLevel = recommendedLevel,
                Locked = locked,
            };

        // --- Prefab --------------------------------------------------------------------------

        private static GameObject BuildPrefab(
            GuideMenuDefinition definition,
            string name,
            string prefabPath,
            Func<GuideMenuItem, RectTransform, GuideMenuView, GameObject> itemPanel
        )
        {
            using (UiBuild.Begin(font, shadowText))
            {
                var root = CanvasRoot(name);
                var view = root.gameObject.AddComponent<GuideMenuView>();
                view.Definition = definition;
                frameParts.Clear();
                tabBackdrops.Clear();

                BuildBackground(root, definition);
                // 枠つきのパネルを開いたとき：背景をぼかし、ヘッダーに帯を敷く。
                frameParts.Add(GuideFrameAssets.BuildBackdrop(root, definition.Background));
                var safe = SafeArea(root);
                frameParts.Add(GuideFrameAssets.BuildHeaderBand(safe));
                view.GuideArt = BuildGuide(safe, definition);
                BuildHeader(safe, view, definition);
                if (definition.Layout == GuideMenuLayout.Destinations)
                    BuildDestinations(safe, view, definition);
                else
                {
                    BuildMenu(safe, view, definition);
                    BuildList(safe, view, definition);
                    BuildItemPanels(safe, view, definition, itemPanel);
                }
                // ヘッダーの下の線は、パネルの上に重ねる。
                frameParts.Add(GuideFrameAssets.BuildHeaderLine(safe));
                view.FrameParts = frameParts.ToArray();
                view.TabBackdrops = tabBackdrops.ToArray();
                BuildToast(root, view);
                CollectTintGraphics(root);
                return PrefabUtility.SaveAsPrefabAsset(root.gameObject, prefabPath);
            }
        }

        private static void BuildBackground(RectTransform root, GuideMenuDefinition definition)
        {
            var background = Rect("Background", root);
            var image = background.gameObject.AddComponent<RawImage>();
            image.texture = definition.Background;
            image.raycastTarget = false;
            background.gameObject.AddComponent<ResponsiveBackground>().AspectRatio =
                definition.Background.width / (float)definition.Background.height;

            // Darkens the right side behind the menu and the lists.
            var shade = Rect("ShadeRight", root);
            shade.anchorMin = new Vector2(1, 0);
            shade.anchorMax = Vector2.one;
            shade.pivot = new Vector2(1, 0.5f);
            shade.sizeDelta = new Vector2(1400, 0);
            var shadeImage = shade.gameObject.AddComponent<RawImage>();
            shadeImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ShadeHorizontalPath);
            shadeImage.color = new Color(0.02f, 0.024f, 0.047f, 0.82f);
            shadeImage.raycastTarget = false;
        }

        private static GameObject BuildGuide(RectTransform safe, GuideMenuDefinition definition)
        {
            var guide = Rect("Guide", safe);
            guide.anchorMin = guide.anchorMax = Vector2.zero;
            guide.pivot = new Vector2(0.5f, 0f);
            guide.sizeDelta =
                new Vector2(definition.GuideArt.width, definition.GuideArt.height)
                * definition.GuideDotSize;
            // Centred in the guide box, with the waist on the bottom edge.
            guide.anchoredPosition = new Vector2(GuideLeft + GuideMaxWidth / 2f, 0f);
            var image = guide.gameObject.AddComponent<RawImage>();
            image.texture = definition.GuideArt;
            image.raycastTarget = false;
            return guide.gameObject;
        }

        private static void BuildHeader(
            RectTransform safe,
            GuideMenuView view,
            GuideMenuDefinition definition
        )
        {
            var back = Rect("Back", safe);
            Corner(back, new Vector2(0, 1), Vector2.zero, new Vector2(BackWidth, BackHeight));
            var tab = Rect("Tab", back);
            Stretch(tab);
            tab.offsetMin = new Vector2(-BackBleed, 0);
            tab.offsetMax = new Vector2(0, BackBleed);
            var backTab = Frame(tab, FramePath, Color.white);
            tabBackdrops.Add(backTab);
            view.Back = AddTintButton(back, backTab);
            // 枠つきの画面では、タブの代わりに「もどる」とパンくずの間に細い線を引く。
            frameParts.Add(GuideFrameAssets.Separator(back, 1f, HeaderGap / 2f));
            var icon = Rect("Icon", back);
            Corner(icon, new Vector2(0, 0.5f), Vector2.zero, new Vector2(IconSize, IconSize));
            SpriteImage(icon, IconBackPath, Color.white);
            var label = Label(
                back,
                "Label",
                "もどる",
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.Left
            );
            Corner(
                (RectTransform)label.transform,
                new Vector2(0, 0.5f),
                new Vector2(BackLabelLeft, 0),
                new Vector2(BackWidth - BackLabelLeft - FrameCorner * DotScale, 40)
            );

            BuildClose(safe, view);
            BuildRunes(safe, view);

            // 画面名から始めるパンくず。すべて同じ大きさ・色で、「もどる」と縦の中央をそろえる。
            var breadcrumb = Label(
                safe,
                "Breadcrumb",
                definition.Title,
                BreadcrumbSize,
                UiPalette.TextMain,
                TextAlignmentOptions.Left
            );
            var rect = (RectTransform)breadcrumb.transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0, 0.5f);
            rect.offsetMin = new Vector2(BackWidth + HeaderGap, -BackHeight);
            rect.offsetMax = new Vector2(-(CloseWidth + HeaderGap + RuneRoom), 0);
            breadcrumb.overflowMode = TextOverflowModes.Ellipsis;
            Shrink(breadcrumb, BreadcrumbMinSize);
            view.Breadcrumb = breadcrumb;
        }

        private static void BuildClose(RectTransform safe, GuideMenuView view)
        {
            var close = Rect("Home", safe);
            Corner(close, Vector2.one, Vector2.zero, new Vector2(CloseWidth, BackHeight));
            var tab = Rect("Tab", close);
            Stretch(tab);
            tab.offsetMin = new Vector2(0, 0);
            tab.offsetMax = new Vector2(BackBleed, BackBleed);
            var closeTab = Frame(tab, FramePath, Color.white);
            tabBackdrops.Add(closeTab);
            view.Home = AddTintButton(close, closeTab);
            frameParts.Add(GuideFrameAssets.Separator(close, 0f, -HeaderGap / 4f));
            var icon = Rect("Icon", close);
            Place(icon, Vector2.zero, new Vector2(IconSize, IconSize));
            SpriteImage(icon, IconClosePath, Color.white);
        }

        // ×の左の所持ルーン。Homeと同じく、四方へ薄くなる暗い下地に、2倍のアイコンと数を並べる。
        private static void BuildRunes(RectTransform safe, GuideMenuView view)
        {
            var pill = Rect("RunePill", safe);
            Corner(
                pill,
                Vector2.one,
                new Vector2(-(CloseWidth + HeaderGap / 2f), 0),
                new Vector2(0, BackHeight)
            );
            var plate = Rect("RuneShade", pill);
            Stretch(plate);
            plate.offsetMin = new Vector2(-16, 0);
            plate.offsetMax = new Vector2(16, 0);
            plate.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var shade = Sliced(plate, UiArt.FeatherPath, new Color(0.012f, 0.02f, 0.04f, 0.9f));
            shade.pixelsPerUnitMultiplier = 0.5f;
            shade.raycastTarget = false;
            tabBackdrops.Add(shade);
            frameParts.Add(GuideFrameAssets.Separator(pill, 0f, -HeaderGap / 4f));
            var row = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(36, 36, 0, 0);
            row.spacing = 12;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            pill.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            var icon = Rect("RuneIcon", pill);
            // 取り込み設定はHomeが持つ（HomeScreenArt）。ここでは読むだけにする。
            SpriteImage(icon, IconRunePath, Color.white);
            var size = icon.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = size.preferredHeight = RuneIconSize;
            view.Runes = Label(
                pill,
                "Runes",
                "--",
                RuneTextSize,
                UiPalette.TextMain,
                TextAlignmentOptions.Midline
            );
        }

        private static void BuildMenu(
            RectTransform safe,
            GuideMenuView view,
            GuideMenuDefinition definition
        )
        {
            var panel = Rect("Menu", safe);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 0.5f);
            panel.anchoredPosition = new Vector2(-56, -16);
            // Four rows still fit between the title and the bottom edge; five rows are a little
            // lower and closer together. Hidden items have no row.
            int rows = definition.Items.Count(item => !item.Hidden);
            float rowHeight = rows > 4 ? 140 : 156;
            float spacing = rows > 4 ? 20 : 28;
            panel.sizeDelta = new Vector2(
                820,
                Mathf.Max(700, rows * rowHeight + (rows - 1) * spacing)
            );
            var group = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            view.MenuPanel = panel.gameObject;

            // Hidden items keep their place in the array as null, so indices match the items.
            var buttons = new List<Button>();
            for (int i = 0; i < definition.Items.Length; i++)
            {
                var item = definition.Items[i];
                if (item.Hidden)
                {
                    buttons.Add(null);
                    continue;
                }
                // 冒険者の一覧と同じすりガラスのカード。中身はカードの板と一緒に沈む。
                // アイコンと重なるので、左上のばら窓の模様は付けない。
                var card = Rect("MenuItem" + i, panel);
                var size = card.gameObject.AddComponent<LayoutElement>();
                size.minHeight = size.preferredHeight = rowHeight;
                var (button, _, row) = GlassCardAssets.Choice(card, pattern: false);
                buttons.Add(button);

                // The 24x24 icon at 4x sits in a 96px column before the label.
                float textLeft = 56;
                if (item.Icon != null)
                {
                    var icon = Rect("Icon", row);
                    Corner(
                        icon,
                        new Vector2(0, 0.5f),
                        new Vector2(36, 0),
                        new Vector2(IconSize, IconSize)
                    );
                    var iconImage = icon.gameObject.AddComponent<Image>();
                    iconImage.sprite = item.Icon;
                    iconImage.raycastTarget = false;
                    textLeft = 36 + IconSize + 28;
                }
                var label = Label(
                    row,
                    "Label",
                    item.Label,
                    52,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Left
                );
                Band((RectTransform)label.transform, top: true, 20, 68, textLeft, 96);
                var caption = Label(
                    row,
                    "Caption",
                    item.Caption,
                    30,
                    UiPalette.TextSub,
                    TextAlignmentOptions.Left
                );
                Band((RectTransform)caption.transform, top: false, 22, 40, textLeft + 2, 96);
                Shrink(caption, 20);
                var arrow = Rect("Arrow", row);
                Corner(
                    arrow,
                    new Vector2(1, 0.5f),
                    new Vector2(-44, 0),
                    new Vector2(5, 9) * DotScale
                );
                SpriteImage(arrow, ArrowPath, UiPalette.Gold);
            }
            view.MenuItems = buttons.ToArray();
        }

        private static void BuildList(
            RectTransform safe,
            GuideMenuView view,
            GuideMenuDefinition definition
        )
        {
            var first = definition.Items.Length > 0 ? definition.Items[0] : new GuideMenuItem();
            var (panel, layer, viewport) = FramedList(safe, "List", ListHeadingHeight);
            view.ListPanel = panel.gameObject;
            view.ListTitle = GuideFrameAssets.Heading(
                layer,
                "ListTitle",
                first.Label,
                ListSplit + ListGap,
                GuideFrameAssets.HeaderHeight,
                600f,
                ListHeadingHeight
            );

            var lists = new List<RectTransform>();
            for (int i = 0; i < definition.Items.Length; i++)
            {
                var content = ListContent(viewport, "List" + i);
                var entries = definition.Items[i].Entries;
                for (int j = 0; j < entries.Length; j++)
                    BuildEntry(
                        content,
                        "Entry" + j,
                        entries[j].Name,
                        entries[j].Badge,
                        entries[j].Detail
                    );
                // Only the first list shows in the editor; the view switches them at runtime.
                content.gameObject.SetActive(i == 0);
                lists.Add(content);
            }
            view.Lists = lists.ToArray();
            view.ListScroll = ListScroll(viewport, lists.Count > 0 ? lists[0] : null);
            (view.Confirm, view.ConfirmLabel) = ConfirmButton(layer, first.ConfirmLabel);

            // The menu shows first; turn the list on in the editor to read its rows.
            panel.gameObject.SetActive(false);
        }

        // The travel office: the destinations fill the right side from the start, without a menu
        // or a title. A row turns gold when chosen, and the depart button below sets off for it.
        private static void BuildDestinations(
            RectTransform safe,
            GuideMenuView view,
            GuideMenuDefinition definition
        )
        {
            var (panel, layer, viewport) = FramedList(safe, "Destinations", 8f);
            view.DestinationPanel = panel.gameObject;

            var content = ListContent(viewport, "List");
            var rows = new Button[definition.Destinations.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var destination = definition.Destinations[i];
                bool locked = destination.Locked;
                rows[i] = BuildEntry(
                    content,
                    "Destination" + i,
                    locked ? HiddenName : destination.Name,
                    $"推奨Lv{destination.RecommendedLevel}",
                    locked ? "" : destination.Detail
                );
                // 未踏の地は地名と説明を伏せ、1行で縦の中央に置き、行ごと暗くして押せなくする。
                if (locked)
                {
                    var inside = rows[i].transform.Find("Body/Content");
                    CenterVertically(inside, "Name", "Badge");
                    rows[i].interactable = false;
                    rows[i].GetComponent<CanvasGroup>().alpha = LockedAlpha;
                }
            }
            view.DestinationRows = rows;
            ListScroll(viewport, content);
            (view.Depart, _) = ConfirmButton(layer, definition.DepartLabel);
        }

        // 枠つきのリストの土台：Safe Area全面のパネル、設計座標の層、案内人の右の透ける面と金の2本線、
        // 決めるボタンの上までの行の表示範囲。行の影と選んだときの光が切れないよう、表示範囲は行より広く取る。
        private static (
            RectTransform Panel,
            RectTransform Layer,
            RectTransform Viewport
        ) FramedList(RectTransform safe, string name, float headingHeight)
        {
            var panel = Rect(name, safe);
            Stretch(panel);
            var layer = Layout.Layer(panel, "Layout");
            GuideFrameAssets.Pane(
                layer,
                "Pane",
                ListSplit,
                1920f,
                GuideFrameAssets.HeaderHeight,
                GuideFrameAssets.PaneLight,
                false,
                true
            );
            GuideFrameAssets.Divider(layer, ListSplit, GuideFrameAssets.HeaderHeight);
            float top = GuideFrameAssets.HeaderHeight + headingHeight;
            float bottom = Layout.Design.y - ConfirmBottom - ConfirmHeight - 8f;
            var viewport = Layout.Box(
                layer,
                "Viewport",
                ListSplit,
                top,
                1920f - ListSplit,
                bottom - top
            );
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear, true);
            return (panel, layer, viewport);
        }

        // A column of rows that grows downward from the top of the viewport.
        private static RectTransform ListContent(RectTransform viewport, string name)
        {
            var content = Rect(name, viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var group = content.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = EntryGap;
            group.padding = new RectOffset(
                (int)ListGap,
                (int)GuideFrameAssets.Margin,
                (int)EntryGap,
                (int)(EntryGap * 2f)
            );
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            return content;
        }

        private static ScrollRect ListScroll(RectTransform viewport, RectTransform content)
        {
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            return scroll;
        }

        // The gold button at the bottom right that applies the chosen row; off until one is chosen.
        // 編成の「レベルアップ」と同じ、金の縁と光のすりガラスのカード。決定ボタンには模様を付けない。
        private static (Button Button, TMP_Text Label) ConfirmButton(
            RectTransform layer,
            string text
        )
        {
            var (button, rim, confirm) = GlassCardAssets.Choice(
                Layout.Box(
                    layer,
                    "Confirm",
                    1920f - GuideFrameAssets.Margin - ConfirmWidth,
                    Layout.Design.y - ConfirmBottom - ConfirmHeight,
                    ConfirmWidth,
                    ConfirmHeight
                ),
                pattern: false
            );
            rim.SetActive(true);
            var label = Label(
                confirm,
                "Label",
                text,
                44,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)label.transform);
            button.interactable = false;
            return (button, label);
        }

        // A feature's own panel in the list's place, for the items that have one.
        private static void BuildItemPanels(
            RectTransform safe,
            GuideMenuView view,
            GuideMenuDefinition definition,
            Func<GuideMenuItem, RectTransform, GuideMenuView, GameObject> itemPanel
        )
        {
            var panels = new GameObject[definition.Items.Length];
            for (int i = 0; i < panels.Length && itemPanel != null; i++)
            {
                panels[i] = itemPanel(definition.Items[i], safe, view);
                if (panels[i] != null)
                    panels[i].SetActive(false);
            }
            view.ItemPanels = panels;
        }

        private static Button BuildEntry(
            RectTransform content,
            string name,
            string title,
            string badge,
            string detail
        )
        {
            // 冒険者の一覧と同じすりガラスのカード。選んだ行は金の縁と光（「Selected」）を出す。
            // 文字が左上から始まるため、左上の模様は入れない。
            var card = Rect(name, content);
            var size = card.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = EntryHeight;
            var (button, selected, row) = GlassCardAssets.Choice(card, pattern: false);
            selected.name = "Selected";
            var label = Label(
                row,
                "Name",
                title,
                42,
                UiPalette.TextMain,
                TextAlignmentOptions.Left
            );
            Band((RectTransform)label.transform, top: true, 18, 54, 40, 260);
            var detailLabel = Label(
                row,
                "Detail",
                detail,
                28,
                UiPalette.TextSub,
                TextAlignmentOptions.Left
            );
            Band((RectTransform)detailLabel.transform, top: false, 14, 48, 42, 40);
            Shrink(detailLabel, 18);
            var badgeLabel = Label(
                row,
                "Badge",
                badge,
                34,
                UiPalette.Gold,
                TextAlignmentOptions.Right
            );
            var badgeRect = (RectTransform)badgeLabel.transform;
            Band(badgeRect, top: true, 20, 50, 40, 40);
            badgeRect.anchorMin = new Vector2(0.5f, 1);
            return button;
        }

        private static void BuildToast(RectTransform root, GuideMenuView view) =>
            view.Notice = NoticeBandAssets.Build(root);

        // --- Art -----------------------------------------------------------------------

        private static void WriteArt()
        {
            // A silver line with rounded corners on a navy fill; the selected one is gold on purple.
            WriteFrame(
                FramePath,
                outline: (Hex(0x465c85), Hex(0x0c1b36)),
                line: (Hex(0xdcdfe1), Hex(0x8496ae)),
                glint: (Hex(0xf4f4f1), Hex(0xa8b4c5)),
                fill: Hex(0x141f3f),
                shade: Hex(0x0a1229)
            );
            WriteFrame(
                FrameSelectedPath,
                outline: (Hex(0xa6500d), Hex(0x3c1903)),
                line: (Hex(0xfde45a), Hex(0xde8613)),
                glint: (Hex(0xfef6cb), Hex(0xf4b118)),
                fill: Hex(0x2a2350),
                shade: Hex(0x181432)
            );
            WriteArrow(ArrowPath);
            WriteGradient(ShadeHorizontalPath);
            WriteSoftSpot(SoftSpotPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ArtAssets.ImportSprite(
                FramePath,
                Vector4.one * FrameCorner,
                FilterMode.Point,
                fullRect: true
            );
            ArtAssets.ImportSprite(
                FrameSelectedPath,
                Vector4.one * FrameCorner,
                FilterMode.Point,
                fullRect: true
            );
            ArtAssets.ImportSprite(ArrowPath, Vector4.zero, FilterMode.Point);
            ArtAssets.ImportSprite(SoftSpotPath, Vector4.zero, FilterMode.Bilinear);
            Icon(IconBackPath);
            Icon(IconClosePath);
            ArtAssets.ImportTexture(ShadeHorizontalPath, FilterMode.Bilinear);
        }

        // The top-left corner, rows from the top edge and columns from the left edge, and the
        // strip across an edge from the outside in: '.' clear, 'o' outline, 'L' line, 'G' glint,
        // 's' the shadow the frame casts on the fill, 'f' fill. The other corners mirror it.
        private static readonly string[] FrameCornerDots = { "..oo", ".oGL", "oGss", "oLsf" };
        private const string FrameEdgeDots = "oLsf";

        // Lit from the top left: each colour pair is (top and left, bottom and right).
        private static void WriteFrame(
            string path,
            (Color32 Lit, Color32 Dark) outline,
            (Color32 Lit, Color32 Dark) line,
            (Color32 Lit, Color32 Dark) glint,
            Color32 fill,
            Color32 shade
        )
        {
            const int size = FrameCorner * 2 + FrameTile;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Texture rows run from the bottom.
                int row = size - 1 - y;
                int fromSide = Mathf.Min(x, size - 1 - x);
                int fromTop = Mathf.Min(row, size - 1 - row);
                bool litTop = row < FrameCorner;
                bool litLeft = x < FrameCorner;
                char dot;
                bool lit;
                if (fromSide < FrameCorner && fromTop < FrameCorner)
                {
                    dot = FrameCornerDots[fromTop][fromSide];
                    // A corner dot belongs to the nearer edge; the diagonal takes the lit one.
                    lit =
                        fromTop < fromSide ? litTop
                        : fromSide < fromTop ? litLeft
                        : litTop || litLeft;
                }
                else if (fromTop < FrameCorner)
                {
                    dot = FrameEdgeDots[fromTop];
                    lit = litTop;
                }
                else if (fromSide < FrameCorner)
                {
                    dot = FrameEdgeDots[fromSide];
                    lit = litLeft;
                }
                else
                {
                    dot = 'f';
                    lit = true;
                }
                Color32 color = dot switch
                {
                    '.' => new Color32(0, 0, 0, 0),
                    'o' => lit ? outline.Lit : outline.Dark,
                    'L' => lit ? line.Lit : line.Dark,
                    'G' => lit ? glint.Lit : glint.Dark,
                    's' => lit ? shade : fill,
                    _ => fill,
                };
                texture.SetPixel(x, y, color);
            }
            Save(texture, path);
        }

        private static Color32 Hex(int rgb) =>
            new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        // Clear on the left, dark on the right.
        private static void WriteGradient(string path)
        {
            const int length = 64;
            var texture = new Texture2D(length, 1, TextureFormat.RGBA32, false);
            for (int i = 0; i < length; i++)
                texture.SetPixel(
                    i,
                    0,
                    new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, i / (length - 1f)))
                );
            Save(texture, path);
        }

        // A round shadow that fades out toward the edge, like the Home buttons' backdrop.
        private static void WriteSoftSpot(string path)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = Vector2.one * size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / center.x;
                texture.SetPixel(
                    x,
                    y,
                    new Color(
                        1,
                        1,
                        1,
                        1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, d))
                    )
                );
            }
            Save(texture, path);
        }

        // A right-pointing 5x9 triangle, tinted in the UI.
        private static void WriteArrow(string path)
        {
            var texture = new Texture2D(5, 9, TextureFormat.RGBA32, false);
            for (int y = 0; y < 9; y++)
            for (int x = 0; x < 5; x++)
                texture.SetPixel(x, y, x <= 4 - Mathf.Abs(y - 4) ? Color.white : Color.clear);
            Save(texture, path);
        }

        private static void Save(Texture2D texture, string path)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        // --- Helpers -------------------------------------------------------------------

        public static Image Frame(RectTransform rect, string path, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            // Tiled keeps the dots of the edges instead of stretching them into a flat line.
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = 1f / DotScale;
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        public static Button AddButton(RectTransform rect, Graphic target) =>
            DimWhenDisabled(UiBuild.AddButton(rect, target));

        // For the back button's tab: the icon and label darken with it.
        private static Button AddTintButton(RectTransform rect, Graphic target) =>
            DimWhenDisabled(UiBuild.AddTintButton(rect, target));

        // The confirm and depart buttons stay disabled until a row is chosen; locked rows stay off.
        private static Button DimWhenDisabled(Button button)
        {
            var colors = button.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.7f);
            button.colors = colors;
            return button;
        }

        // On narrow screens the text shrinks to its box instead of running out of the frame.
        public static void Shrink(TMP_Text label, float minimum)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = minimum;
            label.fontSizeMax = label.fontSize;
        }

        // A full-width strip of the given height, inset from the top or bottom edge.
        public static void Band(
            RectTransform rect,
            bool top,
            float inset,
            float height,
            float left,
            float right
        )
        {
            float y = top ? 1 : 0;
            rect.anchorMin = new Vector2(0, y);
            rect.anchorMax = new Vector2(1, y);
            rect.pivot = new Vector2(0.5f, y);
            rect.offsetMin = new Vector2(left, top ? -inset - height : inset);
            rect.offsetMax = new Vector2(-right, top ? -inset : inset + height);
        }

        // Moves strips made by Band to the vertical middle of their parent, keeping their height.
        private static void CenterVertically(Transform parent, params string[] children)
        {
            foreach (var child in children)
            {
                var rect = (RectTransform)parent.Find(child);
                rect.anchorMin = new Vector2(rect.anchorMin.x, 0.5f);
                rect.anchorMax = new Vector2(rect.anchorMax.x, 0.5f);
                rect.pivot = new Vector2(rect.pivot.x, 0.5f);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 0f);
            }
        }

        public static void Fill(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            Stretch(rect);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
