using System;
using System.IO;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.UI.Editor
{
    public static class TranslucentTextPanelAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Shared/UI/TranslucentTextPanel/TranslucentTextPanel.prefab";

        public const string PanelTexturePath =
            "Assets/Baryonyx/Shared/UI/TranslucentTextPanel/TopTitlePanelGradient.png";
        public const string BackdropTexturePath =
            "Assets/Baryonyx/Shared/UI/TranslucentTextPanel/TopTitleBackdropGradient.png";
        private const string FontPath = "Assets/Baryonyx/Shared/UI/Fonts/DotGothic16.asset";
        public static readonly Color BackdropColor = new Color(0.3f, 0.3f, 0.3f, 0.42f);

        [MenuItem("Baryonyx/UI/Create Translucent Text Panel Prefab")]
        public static void CreatePrefab()
        {
            EnsurePrefab();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        public static void EnsurePrefab()
        {
            AssetFolders.Ensure(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                return;

            var root = new GameObject(
                "TranslucentTextPanel",
                typeof(RectTransform),
                typeof(RawImage),
                typeof(TranslucentTextPanel)
            );
            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = new Vector2(0.5f, 0.5f);
                rootRect.anchorMax = new Vector2(0.5f, 0.5f);
                rootRect.sizeDelta = new Vector2(1320f, 260f);
                rootRect.pivot = new Vector2(0.5f, 0.5f);

                var panel = root.GetComponent<RawImage>();
                panel.texture = ArtAssets.LoadTexture(PanelTexturePath);
                panel.color = Color.white;
                panel.raycastTarget = false;

                var backdropCanvasObject = new GameObject(
                    "BackdropCanvas",
                    typeof(RectTransform),
                    typeof(Canvas)
                );
                backdropCanvasObject.transform.SetParent(root.transform, false);
                var backdropCanvasRect = backdropCanvasObject.GetComponent<RectTransform>();
                backdropCanvasRect.anchorMin = new Vector2(0.5f, 0.5f);
                backdropCanvasRect.anchorMax = new Vector2(0.5f, 0.5f);
                backdropCanvasRect.sizeDelta = new Vector2(1320f, 260f);
                backdropCanvasRect.pivot = new Vector2(0.5f, 0.5f);
                backdropCanvasObject.GetComponent<Canvas>().overrideSorting = false;

                var backdropObject = new GameObject(
                    "Backdrop",
                    typeof(RectTransform),
                    typeof(RawImage)
                );
                backdropObject.transform.SetParent(backdropCanvasObject.transform, false);
                Stretch(backdropObject.GetComponent<RectTransform>());
                var backdrop = backdropObject.GetComponent<RawImage>();
                backdrop.texture = ArtAssets.LoadTexture(BackdropTexturePath);
                backdrop.color = BackdropColor;
                backdrop.raycastTarget = false;

                var labelObject = new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(TextMeshProUGUI)
                );
                labelObject.transform.SetParent(root.transform, false);
                Stretch(labelObject.GetComponent<RectTransform>());
                var labelGroup = labelObject.GetComponent<CanvasGroup>();
                labelGroup.alpha = 1f;
                labelGroup.interactable = false;
                labelGroup.blocksRaycasts = false;
                var label = labelObject.GetComponent<TextMeshProUGUI>();
                label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (label.font == null)
                    throw new InvalidOperationException($"Font asset not found: {FontPath}");
                label.fontSize = 64f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.raycastTarget = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;

                var component = root.GetComponent<TranslucentTextPanel>();
                component.Panel = panel;
                component.Backdrop = backdrop;
                component.BackdropCanvas = backdropCanvasRect;
                component.Label = label;
                component.LabelGroup = labelGroup;
                component.FontSize = 64f;
                component.BackdropSize = new Vector2(1320f, 260f);
                component.BackdropAlpha = BackdropColor.a;
                component.PulseEnabled = false;
                component.PulseDurationSeconds = 2.4f;
                component.PulseMinimumAlpha = 0.35f;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
