using System;
using System.IO;
using Baryonyx.Editor;
using Baryonyx.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Health.Editor
{
    // Health Connect連携モーダルのPrefabを生成する。親Canvasの全面を暗幕で覆い、
    // 本文とボタンはSafe Area内の中央に置く。
    public static class HealthLinkModalAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/Health/UI/HealthLinkModal.prefab";

        private static readonly Color Scrim = new(0.012f, 0.02f, 0.04f, 0.72f);
        private static readonly Color PanelColor = new(0.055f, 0.071f, 0.106f, 0.96f);
        private static readonly Color PanelBorder = new(0.498f, 0.890f, 0.839f, 0.55f);
        private static readonly Color TextMain = new(0.953f, 0.914f, 0.824f);
        private static readonly Color TextSub = new(0.788f, 0.749f, 0.659f);
        private static readonly Color Teal = new(0.498f, 0.890f, 0.839f);
        private static readonly Color ActionText = new(0.035f, 0.047f, 0.075f);
        private static readonly Color LaterColor = new(0.953f, 0.914f, 0.824f, 0.1f);

        [MenuItem("Baryonyx/Health/Create Link Modal Prefab")]
        public static GameObject CreatePrefab()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            var font = GameFontAssets.GetOrCreate();
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

            var root = new GameObject(
                "HealthLinkModal",
                typeof(RectTransform),
                typeof(Image),
                typeof(HealthLinkModalView)
            );
            try
            {
                var rootRect = (RectTransform)root.transform;
                Stretch(rootRect);
                // 暗幕は全面に置き、背面のタップとスクロールを止める。
                var scrim = root.GetComponent<Image>();
                scrim.color = Scrim;
                scrim.raycastTarget = true;

                var safe = Child("SafeArea", rootRect);
                Stretch(safe);
                safe.gameObject.AddComponent<SafeAreaFollower>();

                var panel = Child("Panel", safe);
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.sizeDelta = new Vector2(1200f, 0f);
                var border = panel.gameObject.AddComponent<Image>();
                border.color = PanelBorder;
                border.raycastTarget = true;
                var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var borderLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
                borderLayout.padding = new RectOffset(4, 4, 4, 4);
                borderLayout.childControlWidth = borderLayout.childControlHeight = true;
                borderLayout.childForceExpandWidth = true;
                borderLayout.childForceExpandHeight = false;

                var body = Child("Body", panel);
                var fill = body.gameObject.AddComponent<Image>();
                fill.color = PanelColor;
                fill.raycastTarget = false;
                var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(72, 72, 56, 56);
                layout.spacing = 32;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                var view = root.GetComponent<HealthLinkModalView>();
                view.TitleLabel = Label(body, "Title", font, 52, Teal, TextAlignmentOptions.Center);
                view.BodyLabel = Label(
                    body,
                    "Message",
                    font,
                    34,
                    TextSub,
                    TextAlignmentOptions.TopLeft
                );
                view.BodyLabel.lineSpacing = 12f;

                var buttons = Child("Buttons", body);
                var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 32;
                row.padding = new RectOffset(0, 0, 16, 0);
                row.childAlignment = TextAnchor.MiddleCenter;
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = false;
                row.childForceExpandHeight = false;

                (view.LaterButton, view.LaterLabel) = Button(
                    buttons,
                    "LaterButton",
                    font,
                    320,
                    LaterColor,
                    TextMain
                );
                (view.ActionButton, view.ActionLabel) = Button(
                    buttons,
                    "ActionButton",
                    font,
                    520,
                    Teal,
                    ActionText
                );

                view.Show(HealthLinkStatus.PermissionRequired);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static (Button, TMP_Text) Button(
            RectTransform parent,
            string name,
            TMP_FontAsset font,
            float width,
            Color color,
            Color textColor
        )
        {
            var rect = Child(name, parent);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            // Androidのタップ領域の目安（48dp）を十分に上回る高さにする。
            element.preferredHeight = 112;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            var label = Label(rect, "Label", font, 36, textColor, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform);
            label.raycastTarget = false;
            return (button, label);
        }

        private static TextMeshProUGUI Label(
            RectTransform parent,
            string name,
            TMP_FontAsset font,
            float size,
            Color color,
            TextAlignmentOptions alignment
        )
        {
            var rect = Child(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
