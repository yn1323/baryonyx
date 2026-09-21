using System;
using System.Collections.Generic;
using System.IO;
using Baryonyx.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.Wireframe.Editor
{
    public static partial class WireframeScreenAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/Wireframe/UI/WireframeScreen.prefab";
        public const string DataPath =
            "Assets/Baryonyx/Features/Wireframe/Data/WireframeSamples.asset";
        private static readonly Color Ink = new(.16f, .23f, .20f);
        private static readonly Color Paper = new(.96f, .95f, .90f);
        private static readonly Color Accent = new(.19f, .36f, .29f);
        private static TMP_FontAsset font;
        private static Scene generationScene;

        [MenuItem("Baryonyx/Wireframe/Create Screen Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            AssetDatabase.Refresh();
            PrepareArt();
            font = GameFontAssets.GetOrCreate();
            GameFontAssets.SetAsDefault(font);
            if (AssetDatabase.LoadAssetAtPath<WireframeData>(DataPath) == null)
                AssetDatabase.CreateAsset(
                    ScriptableObject.CreateInstance<WireframeData>(),
                    DataPath
                );
            generationScene = EditorSceneManager.NewPreviewScene();
            RectTransform root = null;
            try
            {
                root = Rect("WireframeScreen", null);
                var canvas = root.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.pixelPerfect = false;
                var scaler = root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                // Landscape UI is authored at 1920x1080. Matching the height keeps the
                // logical UI size stable on 19.5:9 and 20:9 screens; only the margins grow.
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 1;
                root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Image(root, Paper, false);
                var view = root.gameObject.AddComponent<WireframeView>();
                var health = root.gameObject.AddComponent<WireframeHealthView>();
                var skin = root.gameObject.AddComponent<WireframeArt>();
                skin.Actors = actors;
                skin.Forest = forest;
                skin.Mine = mine;
                var layout = root.gameObject.AddComponent<WireframeLayout>();
                layout.CoreArea = Rect("CoreArea", root);
                layout.CoreArea.anchorMin = layout.CoreArea.anchorMax = Vector2.one * .5f;
                layout.CoreArea.pivot = Vector2.one * .5f;
                layout.CoreArea.sizeDelta = WireframeLayout.ReferenceResolution;
                Image(layout.CoreArea, new Color(1, 1, 1, 0), false);
                var battlefieldAmbient = Rect("BattlefieldAmbient", root);
                Stretch(battlefieldAmbient);
                var ambientImage =
                    battlefieldAmbient.gameObject.AddComponent<UnityEngine.UI.RawImage>();
                ambientImage.texture = forest;
                ambientImage.color = new Color(1, 1, 1, .18f);
                ambientImage.raycastTarget = false;
                battlefieldAmbient.SetAsFirstSibling();
                layout.BattlefieldAmbient = battlefieldAmbient.gameObject;
                layout.BattlefieldAmbient.SetActive(false);
                layout.SafeArea = Rect("SafeArea", root);
                Stretch(layout.SafeArea);
                layout.Panel = Rect("Panel", layout.CoreArea);
                Center(layout.Panel, 344, 0);
                var header = Rect("Header", layout.Panel);
                AnchorTop(header, 54);
                Horizontal(header);
                header.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth =
                    false;
                Button("Back", header, "戻る", 54, 13, false, 54);
                var title = Label("Title", header, "ホーム", 21, 54);
                title.alignment = TextAlignmentOptions.MidlineLeft;
                title.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
                Button("Preview", header, "確認", 54, 12, false, 48);
                Button("HeaderSettings", header, "設定", 54, 12, false, 48);
                var scrollRoot = Rect("PageScroll", layout.Panel);
                Stretch(scrollRoot);
                layout.Viewport = scrollRoot;
                view.PageScroll = Scroll(scrollRoot, out var content);
                var pageObjects = new List<GameObject>();
                var pageSizes = new List<UnityEngine.UI.LayoutElement>();
                foreach (WireScreen screen in Enum.GetValues(typeof(WireScreen)))
                {
                    var page = Rect(screen + "Page", content);
                    Vertical(page);
                    var size = page.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                    size.minHeight = 480;
                    pageObjects.Add(page.gameObject);
                    pageSizes.Add(size);
                    BuildFinishedPage(screen, page, layout);
                    page.gameObject.SetActive(screen == WireScreen.Home);
                }
                view.Pages = pageObjects.ToArray();
                layout.PageSizes = pageSizes.ToArray();
                // Navigation is edge UI: it follows the device Safe Area instead of the
                // fixed-width content column used by the three game screens.
                var nav = Rect("Navigation", layout.SafeArea);
                nav.anchorMin = new Vector2(0, 0);
                nav.anchorMax = new Vector2(1, 0);
                nav.pivot = new Vector2(.5f, 0);
                nav.sizeDelta = new Vector2(0, 54);
                nav.anchoredPosition = new Vector2(0, 24);
                Horizontal(nav);
                nav.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth =
                    false;
                nav.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childAlignment =
                    TextAnchor.MiddleCenter;
                Button("NavHome", nav, "ホーム", 54, 14, false, 192);
                Button("NavParty", nav, "仲間", 54, 14, false, 192);
                Button("NavGoals", nav, "歩み・目標", 54, 14, false, 192);
                view.Navigation = layout.Navigation = nav.gameObject;
                var sample = Label(
                    "SampleFooter",
                    layout.Panel,
                    "てくてくダンジョン  /  試作版",
                    10,
                    18
                );
                var sampleRect = (RectTransform)sample.transform;
                sampleRect.anchorMin = Vector2.zero;
                sampleRect.anchorMax = new Vector2(1, 0);
                sampleRect.pivot = new Vector2(.5f, 0);
                sampleRect.sizeDelta = new Vector2(0, 18);
                view.PopupOverlay = Overlay(
                    "Popup",
                    root,
                    layout.SafeArea,
                    out var popupPanel,
                    out var popupContent
                );
                layout.PopupPanel = popupPanel;
                PopupArtwork(popupContent);
                Label("PopupTitle", popupContent, "", 22, 44);
                Label("PopupText", popupContent, "", 15, 0);
                for (int i = 0; i < 3; i++)
                    Button("GoalOption" + i, popupContent, "条件");
                Button("PopupPrimary", popupContent, "進む", primary: true);
                Button("PopupSecondary", popupContent, "戻る");
                Close("PopupClose", popupPanel);
                view.DebugOverlay = Overlay(
                    "Debug",
                    root,
                    layout.SafeArea,
                    out var debugPanel,
                    out var debugContent
                );
                layout.DebugPanel = debugPanel;
                Label("DebugTitle", debugContent, "表示サンプルの切り替え", 22, 44);
                Label(
                    "DebugNote",
                    debugContent,
                    "確認用パネルを閉じて、通常画面の密度を評価します。",
                    15,
                    0
                );
                var winRow = Row(debugContent, "DebugResults");
                Button("DebugWin", winRow, "仮：勝利", primary: true);
                Button("DebugLose", winRow, "仮：敗北");
                string[] states = { "通常", "詠唱中", "クールダウン", "弱った濃淡", "ダウン中" };
                for (int i = 0; i < states.Length; i++)
                    Button("DebugCombat" + i, debugContent, "戦闘：" + states[i]);
                for (int i = 0; i < 3; i++)
                    Button("DebugOption" + i, debugContent, "表示条件");
                string[] goals = { "未設定", "進行中", "達成済み", "データ未確認", "変更予約" };
                for (int i = 0; i < goals.Length; i++)
                    Button("DebugGoal" + i, debugContent, "目標：" + goals[i]);
                Button("DebugReset", debugContent, "初回状態へリセット");
                Close("DebugClose", debugPanel);
                BuildHealthDetails(root, layout.SafeArea, health);
                view.PopupOverlay.SetActive(false);
                view.DebugOverlay.SetActive(false);
                DecorateNavigation(nav);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
                EditorSceneManager.ClosePreviewScene(generationScene);
                generationScene = default;
            }
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rect, float width, float margin)
        {
            rect.anchorMin = new Vector2(.5f, 0);
            rect.anchorMax = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(width, -margin * 2);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void CenterModal(RectTransform rect, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void AnchorTop(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(0, height);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void Vertical(RectTransform rect)
        {
            var group = rect.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            group.spacing = 8;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
        }

        private static void Horizontal(RectTransform rect)
        {
            var group = rect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            group.spacing = 8;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
        }

        private static RectTransform Row(RectTransform parent, string name)
        {
            var row = Rect(name, parent);
            Horizontal(row);
            return row;
        }

        private static void Image(RectTransform rect, Color color, bool raycast)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
        }

        private static TMP_Text Label(
            string name,
            RectTransform parent,
            string text,
            float fontSize,
            float minimum
        )
        {
            var rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = Ink;
            label.text = text;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            var size = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            size.minHeight = minimum;
            size.minWidth = 0;
            return label;
        }

        private static void Card(
            string name,
            RectTransform parent,
            string text,
            float height,
            float fontSize = 17
        )
        {
            var card = Rect(name + "Card", parent);
            Image(card, Color.white, false);
            Frame(card);
            Vertical(card);
            var label = Label(name, card, text, fontSize, height);
            label.margin = new Vector4(12, 8, 12, 8);
        }

        private static UnityEngine.UI.Button Button(
            string name,
            RectTransform parent,
            string text,
            float height = 54,
            float fontSize = 16,
            bool primary = false,
            float width = 0,
            Sprite frameOverride = null
        )
        {
            var rect = Rect(name, parent);
            Image(rect, Color.white, true);
            var buttonFrame =
                frameOverride != null ? frameOverride
                : primary ? frame
                : panelFrame;
            Frame(rect, buttonFrame);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.08f, .94f);
            colors.pressedColor = new Color(.70f, .76f, .65f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.52f, .57f, .57f, .65f);
            colors.fadeDuration = .08f;
            button.colors = colors;
            var size = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            size.minHeight = height;
            size.preferredHeight = height;
            size.minWidth = width;
            size.flexibleWidth = width > 0 ? 0 : 1;
            if (width > 0)
                size.preferredWidth = width;
            var label = Label(name + "Label", rect, text, fontSize, 0);
            Stretch((RectTransform)label.transform);
            label.margin = new Vector4(6, 3, 6, 3);
            label.alignment = TextAlignmentOptions.Center;
            label.color =
                frameOverride == equipmentFrame ? new Color(.96f, .94f, .86f)
                : primary ? new Color(.98f, .97f, .91f)
                : Ink;
            if (primary)
                label.fontStyle = FontStyles.Bold;
            return button;
        }

        private static UnityEngine.UI.ScrollRect Scroll(
            RectTransform root,
            out RectTransform content
        )
        {
            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var viewport = Rect(root.name + "Viewport", root);
            Stretch(viewport);
            Image(viewport, Color.clear, true);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            content = Rect(root.name + "Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            Vertical(content);
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        private static GameObject Overlay(
            string name,
            RectTransform root,
            RectTransform safeArea,
            out RectTransform panel,
            out RectTransform content
        )
        {
            // Keep the raycast shield at the root so system-bar margins are also blocked;
            // the modal controls live under the SafeArea follower and therefore remain
            // inside the usable rectangle on notched or gesture-navigation devices.
            var shield = Rect(name + "Overlay", root);
            Stretch(shield);
            Image(shield, new Color(0, 0, 0, .65f), true);
            var safe = Rect(name + "Safe", shield);
            Stretch(safe);
            var follower = safe.gameObject.AddComponent<WireframeSafeAreaFollower>();
            follower.Source = safeArea;
            panel = Rect(name + "Panel", safe);
            CenterModal(panel, 344, 520);
            Image(panel, Paper, true);
            Frame(panel);
            var scroll = Rect(name + "Scroll", panel);
            Stretch(scroll);
            scroll.offsetMin = new Vector2(12, 12);
            scroll.offsetMax = new Vector2(-12, -62);
            Scroll(scroll, out content);
            return shield.gameObject;
        }

        private static void Close(string name, RectTransform panel)
        {
            var button = Button(name, panel, "閉じる", 54, 16);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(-24, 54);
            rect.anchoredPosition = new Vector2(0, -4);
        }
    }
}
