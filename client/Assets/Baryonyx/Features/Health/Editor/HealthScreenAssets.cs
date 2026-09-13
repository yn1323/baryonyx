using System.IO;
using Baryonyx.App;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Baryonyx.Health.Editor
{
    public static class HealthScreenAssets
    {
        public const string PrefabPath = "Assets/Baryonyx/Features/Health/UI/HealthScreen.prefab";
        public const string SettingsPath =
            "Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset";
        private const string FontsPath = "Assets/Baryonyx/Features/Health/UI/Fonts/";
        private static readonly Color Ink = new(0.10f, 0.19f, 0.22f);
        private static readonly Color Muted = new(0.36f, 0.44f, 0.46f);
        private static readonly Color Green = new(0.13f, 0.42f, 0.35f);
        private static TMP_FontAsset font;

        // Explicit command: regenerate only this feature's prefab, preserving its GUID.
        [MenuItem("Baryonyx/Health/Create Screen Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play Mode first.");
            if (TMP_Settings.instance == null)
                throw new System.InvalidOperationException("Import TMP Essential Resources first.");
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            AssetDatabase.Refresh();
            font = Font("NotoSansCJKjp-Regular.otf", "Health Japanese");
            var mono = Font("NotoSansMono-Regular.ttf", "Health Mono");
            mono.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>
            {
                font,
            };
            EditorUtility.SetDirty(mono);
            if (AssetDatabase.LoadAssetAtPath<HealthConnectionSettings>(SettingsPath) == null)
                AssetDatabase.CreateAsset(
                    ScriptableObject.CreateInstance<HealthConnectionSettings>(),
                    SettingsPath
                );

            var root = Rect("HealthScreen", null);
            try
            {
                var canvas = root.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800, 1100);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                root.gameObject.AddComponent<GraphicRaycaster>();
                Image(root, new Color(0.94f, 0.96f, 0.94f), false);
                var view = root.gameObject.AddComponent<HealthScreenView>();
                var screenLayout = root.gameObject.AddComponent<HealthScreenLayout>();
                screenLayout.SafeArea = Rect("SafeArea", root);
                Stretch(screenLayout.SafeArea);
                screenLayout.MainPanel = Rect("Main", screenLayout.SafeArea);
                CenterPanel(screenLayout.MainPanel, HealthScreenLayout.MainMaximumWidth, 40);
                view.MainScroll = Scroll("MainScroll", screenLayout.MainPanel);
                Stretch((RectTransform)view.MainScroll.transform);
                var content = view.MainScroll.content;
                Vertical(content, 24, new RectOffset(0, 0, 12, 32));
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                    .FitMode
                    .PreferredSize;

                Label("Title", content, "1週間の歩数", 60, Ink, 0);
                Label("Brand", content, "BARYONYX / HEALTH", 30, Green, 0);
                Label("Subtitle", content, "Health Connectで、日ごとの歩数を確認。", 35, Muted, 0);
                var connection = Rect("Connection", content);
                Image(connection, Color.white, false);
                Vertical(connection, 24, new RectOffset(28, 28, 28, 28));
                view.Progress = Label("Progress", connection, "Health Connect接続", 40, Green, 0);
                view.Status = Label(
                    "Status",
                    connection,
                    "接続して、歩数の読み取りを許可してください。",
                    35,
                    Muted,
                    0
                );
                view.ConnectButton = Button(
                    "Connect",
                    connection,
                    "Health Connectに接続",
                    Ink,
                    Color.white
                );
                view.SettingsButton = Button(
                    "Settings",
                    connection,
                    "Health Connectの設定を開く",
                    new Color(0.86f, 0.91f, 0.88f),
                    Ink
                );
                view.SettingsButton.gameObject.SetActive(false);

                var googleConnection = Rect("GoogleConnection", content);
                Image(googleConnection, Color.white, false);
                Vertical(googleConnection, 24, new RectOffset(28, 28, 28, 28));
                Label("GoogleTitle", googleConnection, "Google接続（任意）", 40, Green, 0);
                view.GoogleStatus = Label(
                    "GoogleStatus",
                    googleConnection,
                    "未接続。歩数の表示には不要です。",
                    35,
                    Muted,
                    0
                );
                view.SignInButton = Button(
                    "SignIn",
                    googleConnection,
                    "Googleに接続",
                    Green,
                    Color.white
                );

                view.Period = Label("Period", content, "今日を含む直近7日間", 35, Ink, 0);
                view.RefreshButton = Button("Refresh", content, "更新", Green, Color.white);
                view.RefreshButton.gameObject.SetActive(false);
                var days = Rect("Days", content);
                Vertical(days, 20, new RectOffset());
                view.EmptyState = Label(
                    "EmptyState",
                    days,
                    "接続すると、ここに7日分の歩数が表示されます。",
                    35,
                    Muted,
                    0
                ).gameObject;
                view.DayButtons = new UnityEngine.UI.Button[7];
                view.DayLabels = new TMP_Text[7];
                view.DayDates = new TMP_Text[7];
                for (int i = 0; i < 7; i++)
                {
                    var row = Rect("Day" + i, days);
                    Image(row, Color.white, true);
                    view.DayButtons[i] = row.gameObject.AddComponent<UnityEngine.UI.Button>();
                    view.DayButtons[i].targetGraphic = row.GetComponent<UnityEngine.UI.Image>();
                    var group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                    group.padding = new RectOffset(24, 24, 24, 24);
                    group.spacing = 20;
                    group.childControlWidth = group.childControlHeight = true;
                    group.childForceExpandWidth = group.childForceExpandHeight = false;
                    group.childAlignment = TextAnchor.MiddleLeft;
                    row.gameObject.AddComponent<LayoutElement>().minHeight = 140;
                    view.DayDates[i] = Label("Date", row, "", 35, Ink, 0);
                    FlexibleWidth(view.DayDates[i].rectTransform, 1);
                    view.DayLabels[i] = Label("Steps", row, "", 40, Ink, 0);
                    view.DayLabels[i].alignment = TextAlignmentOptions.MidlineRight;
                    FlexibleWidth(view.DayLabels[i].rectTransform, 1.6f);
                    var arrow = Label("DetailsArrow", row, "›", 40, Green, 0);
                    var arrowSize = arrow.gameObject.AddComponent<LayoutElement>();
                    arrowSize.minWidth = arrowSize.preferredWidth = 32;
                    row.gameObject.SetActive(false);
                }
                view.Footnote = Label(
                    "Footnote",
                    content,
                    "歩数データは画面を開いている間だけ保持します。",
                    35,
                    Muted,
                    0
                );
                view.SignOutButton = Button(
                    "SignOut",
                    googleConnection,
                    "Google接続を解除",
                    new Color(0.86f, 0.91f, 0.88f),
                    Ink
                );
                view.SignOutButton.gameObject.SetActive(false);

                var overlay = Rect("DetailsOverlay", root);
                Stretch(overlay);
                Image(overlay, new Color(0.03f, 0.10f, 0.10f, 0.82f), true);
                view.DetailsOverlay = overlay.gameObject;
                screenLayout.DetailsSafeArea = Rect("DetailsSafeArea", overlay);
                Stretch(screenLayout.DetailsSafeArea);
                screenLayout.DetailsPanel = Rect("DetailsPanel", screenLayout.DetailsSafeArea);
                CenterPanel(screenLayout.DetailsPanel, HealthScreenLayout.DetailsMaximumWidth, 24);
                Image(screenLayout.DetailsPanel, new Color(0.98f, 0.99f, 0.98f), true);
                Vertical(screenLayout.DetailsPanel, 24, new RectOffset(28, 28, 24, 24));
                view.DetailsTitle = Label(
                    "DetailsTitle",
                    screenLayout.DetailsPanel,
                    "JSON",
                    40,
                    Ink,
                    0
                );
                Label(
                    "DetailsHint",
                    screenLayout.DetailsPanel,
                    "日別集計のJSON。上下・左右にスクロールできます。",
                    35,
                    Muted,
                    0
                );
                view.JsonScroll = Scroll("JsonScroll", screenLayout.DetailsPanel);
                Image(
                    (RectTransform)view.JsonScroll.transform,
                    new Color(0.09f, 0.15f, 0.17f),
                    true
                );
                view.JsonScroll.horizontal = true;
                var jsonLayout = view.JsonScroll.gameObject.AddComponent<LayoutElement>();
                jsonLayout.flexibleHeight = 1;
                jsonLayout.minHeight = 0;
                var jsonContent = view.JsonScroll.content;
                var fitter = jsonContent.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                jsonContent.anchorMax = new Vector2(0, 1);
                view.JsonText = jsonContent.gameObject.AddComponent<TextMeshProUGUI>();
                view.JsonText.font = mono;
                view.JsonText.fontSize = 36;
                view.JsonText.color = new Color(0.79f, 0.93f, 0.84f);
                view.JsonText.richText = false;
                view.JsonText.textWrappingMode = TextWrappingModes.NoWrap;
                view.JsonText.margin = new Vector4(20, 20, 24, 20);
                view.JsonText.raycastTarget = false;
                view.CloseButton = Button(
                    "CloseDetails",
                    screenLayout.DetailsPanel,
                    "閉じる",
                    Green,
                    Color.white
                );
                overlay.gameObject.SetActive(false);
                view.SignOutButton.interactable = view.RefreshButton.interactable = false;
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [MenuItem("Baryonyx/Health/Attach Screen To Current Scene")]
        public static void AttachToCurrentScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.isDirty || EditorApplication.isPlaying)
                throw new System.InvalidOperationException(
                    "Save the scene and stop Play Mode first."
                );
            if (Object.FindAnyObjectByType<HealthScreenBootstrap>() != null)
                return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var screen = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var app = new GameObject("HealthApp").AddComponent<HealthScreenBootstrap>();
            app.Screen = screen.GetComponent<HealthScreenView>();
            app.Settings = AssetDatabase.LoadAssetAtPath<HealthConnectionSettings>(SettingsPath);
            if (Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject(
                    "EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule)
                );
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static TMP_FontAsset Font(string source, string name)
        {
            string path = FontsPath + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
                return existing;
            var result = TMP_FontAsset.CreateFontAsset(
                AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(FontsPath + source),
                48,
                6,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true
            );
            result.name = name;
            AssetDatabase.CreateAsset(result, path);
            AssetDatabase.AddObjectToAsset(result.material, result);
            foreach (var texture in result.atlasTextures)
                AssetDatabase.AddObjectToAsset(texture, result);
            return result;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void CenterPanel(RectTransform rect, float width, float margin)
        {
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(width, -margin * 2);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void Vertical(RectTransform rect, float spacing, RectOffset padding)
        {
            var group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
        }

        private static void FlexibleWidth(RectTransform rect, float weight)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = 0;
            element.flexibleWidth = weight;
        }

        private static void Image(RectTransform rect, Color color, bool raycast)
        {
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (image == null)
                image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
        }

        private static TMP_Text Label(
            string name,
            Transform parent,
            string text,
            float size,
            Color color,
            float height
        )
        {
            var rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.raycastTarget = false;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            if (height > 0)
                rect.gameObject.AddComponent<LayoutElement>().minHeight = height;
            return label;
        }

        private static UnityEngine.UI.Button Button(
            string name,
            Transform parent,
            string text,
            Color background,
            Color color
        )
        {
            var rect = Rect(name, parent);
            Image(rect, background, true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors = button.colors;
            colors.disabledColor = new Color(1, 1, 1, 0.28f);
            colors.highlightedColor = new Color(0.88f, 0.96f, 0.91f);
            button.colors = colors;
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.minWidth = 120;
            Vertical(rect, 0, new RectOffset(24, 24, 20, 20));
            var label = Label("Label", rect, text, 40, color, 0);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static ScrollRect Scroll(string name, Transform parent)
        {
            var root = Rect(name, parent);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            var viewport = Rect("Viewport", root);
            Stretch(viewport);
            Image(viewport, Color.clear, true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0, 1);
            content.sizeDelta = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }
    }
}
