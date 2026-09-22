using System;
using System.IO;
using Baryonyx.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.App.Editor
{
    internal static class TranslucentTextPanelPrefabSetup
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Shared/UI/TranslucentTextPanel/TranslucentTextPanel.prefab";

        private const string PanelTexturePath =
            "Assets/Baryonyx/Shared/UI/TranslucentTextPanel/TopTitlePanelGradient.png";
        private const string BackdropTexturePath =
            "Assets/Baryonyx/Shared/UI/TranslucentTextPanel/TopTitleBackdropGradient.png";
        private const string OldPanelTexturePath =
            "Assets/Baryonyx/App/Art/Top/TopTitlePanelGradient.png";
        private const string OldBackdropTexturePath =
            "Assets/Baryonyx/App/Art/Top/TopTitleBackdropGradient.png";
        private const string FontPath =
            "Assets/Baryonyx/Features/Health/UI/Fonts/DotGothic16.asset";
        private static readonly Color BackdropColor = new Color(0.3f, 0.3f, 0.3f, 0.42f);

        [MenuItem("Baryonyx/App/Create Translucent Text Panel Prefab")]
        public static void CreatePrefab()
        {
            EnsurePrefab();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        public static void EnsurePrefab()
        {
            EnsureFolders();
            MoveAssetIfNeeded(OldPanelTexturePath, PanelTexturePath);
            MoveAssetIfNeeded(OldBackdropTexturePath, BackdropTexturePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

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
                panel.texture = LoadTexture(PanelTexturePath);
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
                backdrop.texture = LoadTexture(BackdropTexturePath);
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

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Baryonyx/Shared");
            EnsureFolder("Assets/Baryonyx/Shared/UI");
            EnsureFolder("Assets/Baryonyx/Shared/UI/TranslucentTextPanel");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException($"Invalid Unity folder path: {path}");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void MoveAssetIfNeeded(string oldPath, string newPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(newPath) != null)
                return;
            if (AssetDatabase.LoadMainAssetAtPath(oldPath) == null)
                throw new InvalidOperationException($"Asset not found: {oldPath}");

            var error = AssetDatabase.MoveAsset(oldPath, newPath);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(error);
        }

        private static Texture2D LoadTexture(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                throw new InvalidOperationException($"Texture asset not found: {path}");
            return texture;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
