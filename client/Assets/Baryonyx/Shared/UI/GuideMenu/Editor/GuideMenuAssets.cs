using System;
using System.Collections.Generic;
using System.IO;
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

        // The back button matches the Home buttons: an icon and a label over a soft shadow.
        private const float BackWidth = 128f;
        private const float BackHeight = 168f;

        // Feature panels on a guide screen (e.g. the tavern's formation) use the same colours.
        private static readonly Color Shadow = new(0.012f, 0.02f, 0.04f, 0.9f);

        // A locked row (an unexplored destination) is drawn at this opacity, with its name hidden.
        private const float LockedAlpha = 0.5f;
        private const string HiddenName = "？？？";

        private static TMP_FontAsset font;
        private static Material shadowText;

        [MenuItem("Baryonyx/Guide Menu/Create Shared Art")]
        public static void CreateSharedArt()
        {
            Directory.CreateDirectory(ArtFolder);
            WriteArt();
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

                BuildBackground(root, definition);
                var safe = SafeArea(root);
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
            Corner(
                back,
                new Vector2(0, 1),
                new Vector2(40, -24),
                new Vector2(BackWidth, BackHeight)
            );
            // The shadow reaches past the button so the label reads over busy guide art.
            var shadow = Rect("Shadow", back);
            Stretch(shadow);
            shadow.offsetMin = new Vector2(-56, -56);
            shadow.offsetMax = new Vector2(56, 32);
            var spot = SpriteImage(shadow, SoftSpotPath, Shadow);
            spot.raycastTarget = true;
            view.Back = AddTintButton(back, spot);
            var icon = Rect("Icon", back);
            Place(icon, new Vector2(0, 24), new Vector2(IconSize, IconSize));
            SpriteImage(icon, IconBackPath, Color.white);
            var label = Label(
                back,
                "Label",
                "もどる",
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Place((RectTransform)label.transform, new Vector2(0, -60), new Vector2(BackWidth, 40));

            var title = Label(
                safe,
                "Title",
                definition.Title,
                72,
                UiPalette.TextMain,
                TextAlignmentOptions.Right
            );
            Corner(
                (RectTransform)title.transform,
                Vector2.one,
                new Vector2(-56, -28),
                new Vector2(900, 100)
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
            // Four rows still fit between the title and the bottom edge; five rows (the formation)
            // are a little lower and closer together.
            int rows = definition.Items.Length;
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

            var buttons = new List<Button>();
            for (int i = 0; i < definition.Items.Length; i++)
            {
                var item = definition.Items[i];
                var row = Rect("MenuItem" + i, panel);
                var size = row.gameObject.AddComponent<LayoutElement>();
                size.minHeight = size.preferredHeight = rowHeight;
                buttons.Add(AddButton(row, Frame(row, FramePath, Color.white)));

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
            var panel = RightPanel(safe, "List");
            Frame(panel, FramePath, Color.white);
            view.ListPanel = panel.gameObject;

            var first = definition.Items.Length > 0 ? definition.Items[0] : new GuideMenuItem();
            view.ListTitle = ListTitle(panel, first.Label);
            var viewport = ListViewport(panel, top: 104);

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
            view.ListScroll = ListScroll(panel, viewport, lists.Count > 0 ? lists[0] : null);
            (view.Confirm, view.ConfirmLabel) = ConfirmButton(panel, first.ConfirmLabel);

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
            var panel = RightPanel(safe, "Destinations");
            Frame(panel, FramePath, Color.white);
            view.DestinationPanel = panel.gameObject;

            var viewport = ListViewport(panel, top: 28);
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
                    CenterVertically(rows[i].transform, "Name", "Badge");
                    rows[i].interactable = false;
                    rows[i].gameObject.AddComponent<CanvasGroup>().alpha = LockedAlpha;
                }
            }
            view.DestinationRows = rows;
            ListScroll(panel, viewport, content);
            (view.Depart, _) = ConfirmButton(panel, definition.DepartLabel);
        }

        // The gold title at the top left of a full list.
        private static TMP_Text ListTitle(RectTransform panel, string text)
        {
            var title = Label(
                panel,
                "ListTitle",
                text,
                48,
                UiPalette.Gold,
                TextAlignmentOptions.TopLeft
            );
            Fill((RectTransform)title.transform, new Vector2(48, 0), new Vector2(-48, -32));
            return title;
        }

        // The clipped area above the confirm button where the rows scroll; top leaves room for a
        // title.
        private static RectTransform ListViewport(RectTransform panel, float top)
        {
            var viewport = Rect("Viewport", panel);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(28, 148);
            viewport.offsetMax = new Vector2(-28, -top);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear, true);
            return viewport;
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
            group.spacing = 12;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            return content;
        }

        private static ScrollRect ListScroll(
            RectTransform panel,
            RectTransform viewport,
            RectTransform content
        )
        {
            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            return scroll;
        }

        // The gold button at the bottom right that applies the chosen row; off until one is chosen.
        private static (Button Button, TMP_Text Label) ConfirmButton(
            RectTransform panel,
            string text
        )
        {
            var confirm = Rect("Confirm", panel);
            Corner(confirm, new Vector2(1, 0), new Vector2(-32, 28), new Vector2(360, 104));
            var button = AddButton(confirm, Frame(confirm, FrameSelectedPath, Color.white));
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

        /// <summary>The rect of the right side where a list or a feature panel sits.</summary>
        public static RectTransform RightPanel(RectTransform safe, string name)
        {
            var panel = Rect(name, safe);
            Stretch(panel);
            panel.offsetMin = new Vector2(RightLeft, 24);
            panel.offsetMax = new Vector2(-32, -148);
            return panel;
        }

        private static Button BuildEntry(
            RectTransform content,
            string name,
            string title,
            string badge,
            string detail
        )
        {
            var row = Rect(name, content);
            var size = row.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 128;
            var button = AddButton(row, Frame(row, FramePath, new Color(1f, 1f, 1f, 0.9f)));
            var selected = Rect("Selected", row);
            Stretch(selected);
            Frame(selected, FrameSelectedPath, Color.white).raycastTarget = false;
            selected.gameObject.SetActive(false);
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

        // For the back button over a dark shadow: the icon and label darken with it.
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
