using System;
using System.Linq;
using Baryonyx.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Builds the shared scene transition prefab (the cover that closes and opens between
    /// scenes) and puts a configured instance into a scene. The scene setups in App decide which
    /// scenes get it and what it is wired to.
    /// </summary>
    public static class SceneTransitionAssets
    {
        public const string Folder = "Assets/Baryonyx/Shared/UI/SceneTransition";
        public const string PrefabPath = Folder + "/SceneTransition.prefab";

        // シーン間の移動は短い暗転にし、何度も行き来しても待たせない。
        private const float QuickFadeDuration = 0.15f;

        // TopからHomeへ入るときだけ、上下のシャッターで間を取る。
        private const float TopShutterDuration = 0.75f;

        // 世界地図は、読み込むまで覆ったまま待ち、左右のシャッターで開く。
        // 戦闘から何度も戻るため、Topより短くする。
        private const float WorldMapShutterDuration = 0.5f;

        /// <summary>Makes the transition prefab, or repairs its root canvas when it exists.</summary>
        public static void EnsurePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                RepairPrefabRootRect();
                return;
            }

            var root = new GameObject(
                "SceneTransition",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(SceneTransitionController)
            );
            try
            {
                var canvas = root.GetComponent<Canvas>();
                ConfigureCanvas(canvas);

                var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = UnityEngine
                    .UI
                    .CanvasScaler
                    .ScreenMatchMode
                    .MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;

                var rootRect = root.GetComponent<RectTransform>();
                NormalizeRootRect(rootRect);

                var group = root.GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;

                CreateImage(root.transform, "TransitionBlocker", new Color(0f, 0f, 0f, 0f), true);
                CreateImage(
                    root.transform,
                    "FadePanel",
                    SceneTransitionSettings.DefaultColor,
                    false
                );
                CreateImage(
                    root.transform,
                    "WipePanel",
                    SceneTransitionSettings.DefaultColor,
                    false
                );
                CreateImage(
                    root.transform,
                    "ShutterFirst",
                    SceneTransitionSettings.DefaultColor,
                    false
                );
                CreateImage(
                    root.transform,
                    "ShutterSecond",
                    SceneTransitionSettings.DefaultColor,
                    false
                );

                AssetDatabase.SaveAssets();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void RepairPrefabRootRect()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                NormalizeRootRect(root.GetComponent<RectTransform>());
                ConfigureCanvas(root.GetComponent<Canvas>());
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Recreates the shared transition in an open scene and returns its controller.
        /// </summary>
        public static SceneTransitionController AddTransition(
            Scene scene,
            bool startCovered,
            bool revealOnStart
        )
        {
            var instance = scene
                .GetRootGameObjects()
                .FirstOrDefault(candidate => candidate.name == "SceneTransition");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"Transition prefab not found: {PrefabPath}");
            if (instance != null)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
                if (source != prefab)
                    throw new InvalidOperationException(
                        $"A different root object named SceneTransition already exists in {scene.path}."
                    );
                // 本メニューが生成したインスタンスだけを作り直し、古いoverrideを残さない。
                UnityEngine.Object.DestroyImmediate(instance);
                instance = null;
            }
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "SceneTransition";

            var rootRect = instance.GetComponent<RectTransform>();
            NormalizeRootRect(rootRect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rootRect);

            var controller = instance.GetComponent<SceneTransitionController>();
            if (controller == null)
                throw new InvalidOperationException("SceneTransition prefab has no controller.");
            SetBool(controller, "startCovered", startCovered);
            SetBool(controller, "revealOnStart", revealOnStart);
            ConfigureQuickFade(controller, "defaultSettings");
            ConfigureQuickFade(controller, "enterSettings");
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            return controller;
        }

        /// <summary>
        /// Makes Top close with the long shutter (Home opens the same way) and fade in slowly on
        /// launch.
        /// </summary>
        public static void ConfigureTop(SceneTransitionController controller)
        {
            SetEnum(controller, "defaultSettings.type", SceneTransitionType.Shutter);
            SetEnum(controller, "defaultSettings.shutterAxis", SceneTransitionShutterAxis.Vertical);
            SetDurations(controller, "defaultSettings", TopShutterDuration);
            SetEnum(controller, "enterSettings.type", SceneTransitionType.Fade);
            SetDurations(controller, "enterSettings", TopShutterDuration);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }

        /// <summary>
        /// Makes the world map open with the side shutter, from whatever closed the scene before.
        /// </summary>
        public static void ConfigureWorldMap(SceneTransitionController controller)
        {
            SetEnum(controller, "enterSettings.type", SceneTransitionType.Shutter);
            SetEnum(controller, "enterSettings.shutterAxis", SceneTransitionShutterAxis.Horizontal);
            SetDurations(controller, "enterSettings", WorldMapShutterDuration);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }

        private static void ConfigureQuickFade(UnityEngine.Object target, string settingsName)
        {
            SetEnum(target, $"{settingsName}.type", SceneTransitionType.Fade);
            SetDurations(target, settingsName, QuickFadeDuration);
        }

        private static void SetDurations(
            UnityEngine.Object target,
            string settingsName,
            float duration
        )
        {
            SetFloat(target, $"{settingsName}.coverDuration", duration);
            SetFloat(target, $"{settingsName}.revealDuration", duration);
        }

        private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"Serialized field not found: {propertyName}");
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(UnityEngine.Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"Serialized field not found: {propertyName}");
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(UnityEngine.Object target, string propertyName, Enum value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"Serialized field not found: {propertyName}");
            property.enumValueIndex = Convert.ToInt32(value);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateImage(
            Transform parent,
            string name,
            Color color,
            bool raycastTarget
        )
        {
            var imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image)
            );
            imageObject.transform.SetParent(parent, false);
            var rect = imageObject.GetComponent<RectTransform>();
            Stretch(rect);
            var image = imageObject.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void NormalizeRootRect(RectTransform rect)
        {
            if (rect == null)
                throw new InvalidOperationException("SceneTransition prefab has no RectTransform.");
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void ConfigureCanvas(Canvas canvas)
        {
            if (canvas == null)
                throw new InvalidOperationException("SceneTransition prefab has no Canvas.");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
        }
    }
}
