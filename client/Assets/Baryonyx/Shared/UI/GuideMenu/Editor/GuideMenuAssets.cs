using System;
using System.Collections.Generic;
using System.IO;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baryonyx.UI.GuideMenu.Editor
{
    /// <summary>
    /// Generates each feature's guide screen prefab from its definition. Every menu row, list
    /// row and map pin is baked into the prefab, so the text can be read in the editor without
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
        public const string MarkerPath = ArtFolder + "/GuideMapMarker.png";
        public const string MarkerSelectedPath = ArtFolder + "/GuideMapMarkerSelected.png";
        public const string ArrowPath = ArtFolder + "/GuideArrow.png";
        public const string IconBackPath = ArtFolder + "/IconBack.aseprite";
        public const string TextShadowPath = Folder + "/GuideTextShadow.mat";

        // The window frames and the 24x24 icons are drawn at 4 design pixels per dot, like the
        // game's sprites.
        private const float DotScale = 4f;
        private const float IconSize = 24f * DotScale;

        // A frame keeps its 4-dot corners; the 16-dot edges and fill repeat (Image.Type.Tiled).
        private const int FrameCorner = 4;
        private const int FrameTile = 16;

        // The guide stands in this box at the bottom left; the list and map start right of it.
        private const float GuideLeft = 24f;
        private const float GuideMaxWidth = 800f;
        private const float GuideMaxHeight = 1048f;
        private const float RightLeft = 848f;

        // The back button matches the Home buttons: an icon and a label over a soft shadow.
        private const float BackWidth = 128f;
        private const float BackHeight = 168f;

        private static readonly Color TextMain = new(0.953f, 0.914f, 0.824f);
        private static readonly Color TextSub = new(0.788f, 0.749f, 0.659f);
        private static readonly Color Gold = new(1f, 0.843f, 0.4f);
        private static readonly Color Shadow = new(0.012f, 0.02f, 0.04f, 0.9f);

        private static Scene generationScene;
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
            string mapArtPath = null
        )
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            CreateSharedArt();
            font = GameFontAssets.GetOrCreate();
            shadowText = EnsureTextShadow(font);

            Directory.CreateDirectory(Path.GetDirectoryName(definitionPath));
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            AssetDatabase.Refresh();

            var definition = AssetDatabase.LoadAssetAtPath<GuideMenuDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<GuideMenuDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }
            fill(definition);
            definition.GuideArt = ImportTexture(guideArtPath, FilterMode.Point);
            // The backgrounds and the map are generated illustrations used as they are.
            definition.Background = ImportTexture(backgroundPath, FilterMode.Bilinear);
            definition.MapArt =
                mapArtPath != null ? ImportTexture(mapArtPath, FilterMode.Bilinear) : null;
            definition.GuideDotSize = FitDotSize(definition.GuideArt);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);

            var prefab = BuildPrefab(
                definition,
                Path.GetFileNameWithoutExtension(prefabPath),
                prefabPath
            );
            AssetDatabase.SaveAssetIfDirty(font);
            return prefab;
        }

        /// <summary>A 24x24 pixel-art icon drawn in Aseprite, used as a sprite for a menu row.</summary>
        public static Sprite Icon(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AsepriteCanvasImport.ApplyTextureSettings(path, FilterMode.Point, mipmaps: false);
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

        public static GuideMapPoint Point(
            string name,
            float x,
            float y,
            string detail,
            bool locked = false
        ) =>
            new()
            {
                Name = name,
                Position = new Vector2(x, y),
                Detail = detail,
                Locked = locked,
            };

        // --- Prefab --------------------------------------------------------------------------

        private static GameObject BuildPrefab(
            GuideMenuDefinition definition,
            string name,
            string prefabPath
        )
        {
            generationScene = EditorSceneManager.NewPreviewScene();
            RectTransform root = null;
            try
            {
                root = Rect(name, null);
                var canvas = root.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1;
                root.gameObject.AddComponent<GraphicRaycaster>();
                var view = root.gameObject.AddComponent<GuideMenuView>();
                view.Definition = definition;

                BuildBackground(root, definition);
                var safe = Rect("SafeArea", root);
                Stretch(safe);
                safe.gameObject.AddComponent<SafeAreaFollower>();
                BuildGuide(safe, definition);
                BuildHeader(safe, view, definition);
                if (definition.Layout == GuideMenuLayout.Map)
                    BuildMap(safe, view, definition);
                else
                {
                    BuildMenu(safe, view, definition);
                    BuildList(safe, view, definition);
                }
                BuildToast(root, view);
                CollectTintGraphics(root);
                return PrefabUtility.SaveAsPrefabAsset(root.gameObject, prefabPath);
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
                EditorSceneManager.ClosePreviewScene(generationScene);
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

            // Darkens the right side behind the menu, list and map.
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

        private static void BuildGuide(RectTransform safe, GuideMenuDefinition definition)
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
            var label = Label(back, "Label", "もどる", 32, TextMain, TextAlignmentOptions.Center);
            Place((RectTransform)label.transform, new Vector2(0, -60), new Vector2(BackWidth, 40));

            var title = Label(
                safe,
                "Title",
                definition.Title,
                72,
                TextMain,
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
            panel.sizeDelta = new Vector2(820, 700);
            var group = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 28;
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
                size.minHeight = size.preferredHeight = 156;
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
                    TextMain,
                    TextAlignmentOptions.Left
                );
                Band((RectTransform)label.transform, top: true, 20, 68, textLeft, 96);
                var caption = Label(
                    row,
                    "Caption",
                    item.Caption,
                    30,
                    TextSub,
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
                SpriteImage(arrow, ArrowPath, Gold);
            }
            view.MenuItems = buttons.ToArray();
        }

        private static void BuildList(
            RectTransform safe,
            GuideMenuView view,
            GuideMenuDefinition definition
        )
        {
            var panel = Rect("List", safe);
            Stretch(panel);
            panel.offsetMin = new Vector2(RightLeft, 24);
            panel.offsetMax = new Vector2(-32, -148);
            Frame(panel, FramePath, Color.white);
            view.ListPanel = panel.gameObject;

            var first = definition.Items.Length > 0 ? definition.Items[0] : new GuideMenuItem();
            view.ListTitle = Label(
                panel,
                "ListTitle",
                first.Label,
                48,
                Gold,
                TextAlignmentOptions.TopLeft
            );
            Fill(
                (RectTransform)view.ListTitle.transform,
                new Vector2(48, 0),
                new Vector2(-48, -32)
            );

            var viewport = Rect("Viewport", panel);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(28, 148);
            viewport.offsetMax = new Vector2(-28, -104);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear, true);

            var lists = new List<RectTransform>();
            for (int i = 0; i < definition.Items.Length; i++)
            {
                var content = Rect("List" + i, viewport);
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
                var entries = definition.Items[i].Entries;
                for (int j = 0; j < entries.Length; j++)
                    BuildEntry(content, "Entry" + j, entries[j]);
                // Only the first list shows in the editor; the view switches them at runtime.
                content.gameObject.SetActive(i == 0);
                lists.Add(content);
            }
            view.Lists = lists.ToArray();

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = lists.Count > 0 ? lists[0] : null;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            view.ListScroll = scroll;

            var confirm = Rect("Confirm", panel);
            Corner(confirm, new Vector2(1, 0), new Vector2(-32, 28), new Vector2(360, 104));
            view.Confirm = AddButton(confirm, Frame(confirm, FrameSelectedPath, Color.white));
            view.ConfirmLabel = Label(
                confirm,
                "Label",
                first.ConfirmLabel,
                44,
                TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)view.ConfirmLabel.transform);
            view.Confirm.interactable = false;

            // The menu shows first; turn the list on in the editor to read its rows.
            panel.gameObject.SetActive(false);
        }

        private static void BuildEntry(RectTransform content, string name, GuideListEntry entry)
        {
            var row = Rect(name, content);
            var size = row.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 128;
            AddButton(row, Frame(row, FramePath, new Color(1f, 1f, 1f, 0.9f)));
            var selected = Rect("Selected", row);
            Stretch(selected);
            Frame(selected, FrameSelectedPath, Color.white).raycastTarget = false;
            selected.gameObject.SetActive(false);
            var label = Label(row, "Name", entry.Name, 42, TextMain, TextAlignmentOptions.Left);
            Band((RectTransform)label.transform, top: true, 18, 54, 40, 260);
            var detail = Label(row, "Detail", entry.Detail, 28, TextSub, TextAlignmentOptions.Left);
            Band((RectTransform)detail.transform, top: false, 14, 48, 42, 40);
            Shrink(detail, 18);
            var badge = Label(row, "Badge", entry.Badge, 34, Gold, TextAlignmentOptions.Right);
            var badgeRect = (RectTransform)badge.transform;
            Band(badgeRect, top: true, 20, 50, 40, 40);
            badgeRect.anchorMin = new Vector2(0.5f, 1);
        }

        private static void BuildMap(
            RectTransform safe,
            GuideMenuView view,
            GuideMenuDefinition definition
        )
        {
            var panel = Rect("MapPanel", safe);
            Stretch(panel);
            panel.offsetMin = new Vector2(RightLeft, 24);
            panel.offsetMax = new Vector2(-32, -148);
            view.MapPanel = panel.gameObject;

            var holder = Rect("MapHolder", panel);
            Stretch(holder);
            holder.offsetMin = new Vector2(0, 148);
            Frame(holder, FramePath, Color.white).raycastTarget = false;

            // The fitter overrides the map's own offsets, so the frame padding is a parent rect.
            var area = Rect("MapArea", holder);
            Stretch(area);
            area.offsetMin = new Vector2(20, 20);
            area.offsetMax = new Vector2(-20, -20);
            var map = Rect("Map", area);
            Stretch(map);
            var mapImage = map.gameObject.AddComponent<RawImage>();
            mapImage.texture = definition.MapArt;
            mapImage.raycastTarget = false;
            var fitter = map.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = definition.MapArt.width / (float)definition.MapArt.height;

            var pins = new List<Button>();
            for (int i = 0; i < definition.MapPoints.Length; i++)
                pins.Add(BuildPin(map, "Pin" + i, definition.MapPoints[i]));
            view.Pins = pins.ToArray();

            var footer = Rect("Footer", panel);
            footer.anchorMin = Vector2.zero;
            footer.anchorMax = new Vector2(1, 0);
            footer.pivot = new Vector2(0.5f, 0);
            footer.sizeDelta = new Vector2(0, 124);
            Frame(footer, FramePath, Color.white).raycastTarget = false;
            view.MapDetail = Label(
                footer,
                "Detail",
                "行き先を選んでください",
                36,
                TextMain,
                TextAlignmentOptions.Left
            );
            view.MapDetail.textWrappingMode = TextWrappingModes.Normal;
            Shrink(view.MapDetail, 22);
            Fill(
                (RectTransform)view.MapDetail.transform,
                new Vector2(40, 16),
                new Vector2(-340, -16)
            );

            var depart = Rect("Depart", footer);
            Corner(depart, new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(300, 100));
            view.Depart = AddButton(depart, Frame(depart, FrameSelectedPath, Color.white));
            var departLabel = Label(
                depart,
                "Label",
                definition.DepartLabel,
                44,
                TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)departLabel.transform);
            view.Depart.interactable = false;
        }

        private static Button BuildPin(RectTransform map, string name, GuideMapPoint point)
        {
            var pin = Rect(name, map);
            pin.anchorMin = pin.anchorMax = point.Position;
            pin.pivot = new Vector2(0.5f, 0.5f);
            pin.anchoredPosition = Vector2.zero;
            pin.sizeDelta = new Vector2(120, 120);
            var button = AddButton(pin, AddImage(pin, Color.clear, true));
            var ring = Rect("Selected", pin);
            Place(ring, Vector2.zero, new Vector2(13, 13) * DotScale);
            SpriteImage(ring, MarkerSelectedPath, Color.white);
            ring.gameObject.SetActive(false);
            var marker = Rect("Marker", pin);
            Place(marker, Vector2.zero, new Vector2(9, 9) * DotScale);
            // Locked destinations are greyed out.
            SpriteImage(
                marker,
                MarkerPath,
                point.Locked ? new Color(0.45f, 0.45f, 0.5f, 1f) : Color.white
            );
            var label = Label(pin, "Name", point.Name, 30, TextMain, TextAlignmentOptions.Center);
            Place((RectTransform)label.transform, new Vector2(0, -52), new Vector2(320, 44));
            return button;
        }

        private static void BuildToast(RectTransform root, GuideMenuView view)
        {
            var toast = Rect("Toast", root);
            Place(toast, new Vector2(0, 300), new Vector2(800, 104));
            Frame(toast, FramePath, Color.white).raycastTarget = false;
            var group = toast.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;
            view.Toast = group;
            view.ToastLabel = Label(toast, "Label", "", 44, TextMain, TextAlignmentOptions.Center);
            Stretch((RectTransform)view.ToastLabel.transform);
        }

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
            WriteMarker(
                MarkerPath,
                new Color32(226, 66, 58, 255),
                new Color32(255, 170, 140, 255),
                9
            );
            WriteMarker(
                MarkerSelectedPath,
                new Color32(255, 215, 102, 255),
                new Color32(255, 250, 210, 255),
                13
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ImportSprite(FramePath, Vector4.one * FrameCorner, FilterMode.Point, fullRect: true);
            ImportSprite(
                FrameSelectedPath,
                Vector4.one * FrameCorner,
                FilterMode.Point,
                fullRect: true
            );
            ImportSprite(MarkerPath, Vector4.zero, FilterMode.Point);
            ImportSprite(MarkerSelectedPath, Vector4.zero, FilterMode.Point);
            ImportSprite(ArrowPath, Vector4.zero, FilterMode.Point);
            ImportSprite(SoftSpotPath, Vector4.zero, FilterMode.Bilinear);
            Icon(IconBackPath);
            ImportTexture(ShadeHorizontalPath, FilterMode.Bilinear);
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

        // A diamond with a dark rim and a highlight on the upper left.
        private static void WriteMarker(string path, Color32 body, Color32 light, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            int c = size / 2;
            var rim = new Color32(20, 16, 28, 255);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int d = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                Color32 color =
                    d > c ? new Color32(0, 0, 0, 0)
                    : d == c ? rim
                    : x < c && y > c && d >= c - 2 ? light
                    : body;
                texture.SetPixel(x, y, color);
            }
            Save(texture, path);
        }

        private static void Save(Texture2D texture, string path)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static Texture2D ImportTexture(string path, FilterMode filter)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AsepriteCanvasImport.IsAseprite(path))
            {
                AsepriteCanvasImport.ApplyTextureSettings(path, filter, mipmaps: false);
                return AsepriteCanvasImport.LoadTexture(path);
            }
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Required artwork is missing: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = filter;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void ImportSprite(
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
                // Tiled images repeat the sprite's whole rectangle, so the mesh must not trim it.
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

        private static Material EnsureTextShadow(TMP_FontAsset fontAsset)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(TextShadowPath);
            if (material == null)
            {
                material = new Material(fontAsset.material) { name = "GuideTextShadow" };
                AssetDatabase.CreateAsset(material, TextShadowPath);
            }
            material.shader = fontAsset.material.shader;
            material.CopyPropertiesFromMaterial(fontAsset.material);
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.85f));
            material.SetFloat("_UnderlayOffsetX", 0.4f);
            material.SetFloat("_UnderlayOffsetY", -0.6f);
            material.SetFloat("_UnderlayDilate", 0.25f);
            material.SetFloat("_UnderlaySoftness", 0.2f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        // --- Helpers -------------------------------------------------------------------

        private static Image Frame(RectTransform rect, string path, Color color)
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

        private static Image SpriteImage(RectTransform rect, string path, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AsepriteCanvasImport.IsAseprite(path)
                ? AsepriteCanvasImport.LoadSprite(path)
                : AssetDatabase.LoadAssetAtPath<Sprite>(path);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image AddImage(RectTransform rect, Color color, bool raycast)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static Button AddButton(RectTransform rect, Graphic target) =>
            Configure(rect.gameObject.AddComponent<Button>(), target);

        // For the back button over a dark shadow: the icon and label darken with it.
        private static Button AddTintButton(RectTransform rect, Graphic target) =>
            Configure(rect.gameObject.AddComponent<TintGroupButton>(), target);

        private static Button Configure(Button button, Graphic target)
        {
            button.targetGraphic = target;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.06f, 1f);
            colors.pressedColor = new Color(0.72f, 0.70f, 0.66f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.7f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            return button;
        }

        // Runs after every button's children exist, so each one tints all of its own graphics.
        private static void CollectTintGraphics(RectTransform root)
        {
            foreach (var button in root.GetComponentsInChildren<TintGroupButton>(true))
            {
                var graphics = new List<Graphic>();
                foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
                    if (graphic != button.targetGraphic)
                        graphics.Add(graphic);
                button.SetTintGraphics(graphics.ToArray());
            }
        }

        private static TMP_Text Label(
            RectTransform parent,
            string name,
            string text,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment
        )
        {
            var rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = color;
            label.text = text ?? "";
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = alignment;
            label.raycastTarget = false;
            if (shadowText != null)
                label.fontSharedMaterial = shadowText;
            return label;
        }

        // On narrow screens the text shrinks to its box instead of running out of the frame.
        private static void Shrink(TMP_Text label, float minimum)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = minimum;
            label.fontSizeMax = label.fontSize;
        }

        private static void Corner(RectTransform rect, Vector2 corner, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void Place(RectTransform rect, Vector2 center, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // A full-width strip of the given height, inset from the top or bottom edge.
        private static void Band(
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

        private static void Fill(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            Stretch(rect);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var obj = EditorUtility.CreateGameObjectWithHideFlags(
                name,
                HideFlags.HideAndDontSave,
                typeof(RectTransform)
            );
            SceneManager.MoveGameObjectToScene(obj, generationScene);
            obj.hideFlags = HideFlags.None;
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
