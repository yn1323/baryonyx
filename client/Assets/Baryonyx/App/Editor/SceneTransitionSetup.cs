using System;
using System.IO;
using System.Linq;
using Baryonyx.Showcase.Editor;
using Baryonyx.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.App.Editor
{
    public static class SceneTransitionSetup
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Shared/UI/SceneTransition/SceneTransition.prefab";
        private const string TopScenePath = "Assets/Baryonyx/App/Scenes/Top.unity";
        private const string MainScenePath = "Assets/Baryonyx/App/Scenes/Main.unity";
        private const float TransitionDuration = 0.75f;

        [MenuItem("Baryonyx/App/Create Scene Transition Assets")]
        public static void CreateAssetsAndIntegrateTopMain()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

            EnsureFolder("Assets/Baryonyx/Shared/UI");
            EnsureFolder("Assets/Baryonyx/Shared/UI/SceneTransition");
            EnsurePrefab();
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);
            AddToScene(TopScenePath, startCovered: false, revealOnStart: false, configureTop: true);
            AddToScene(MainScenePath, startCovered: true, revealOnStart: true, configureTop: false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }

        private static void EnsurePrefab()
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

        private static void AddToScene(
            string scenePath,
            bool startCovered,
            bool revealOnStart,
            bool configureTop
        )
        {
            if (!File.Exists(scenePath))
                throw new InvalidOperationException($"Scene not found: {scenePath}");

            var loaded = SceneManager.GetSceneByPath(scenePath);
            var ownsScene = !loaded.IsValid() || !loaded.isLoaded;
            var scene = ownsScene
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive)
                : loaded;
            var wasDirty = scene.isDirty;
            try
            {
                var instance = scene
                    .GetRootGameObjects()
                    .FirstOrDefault(candidate => candidate.name == "SceneTransition");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null)
                    throw new InvalidOperationException(
                        $"Transition prefab not found: {PrefabPath}"
                    );
                if (instance != null)
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
                    if (source != prefab)
                        throw new InvalidOperationException(
                            $"A different root object named SceneTransition already exists in {scenePath}."
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
                    throw new InvalidOperationException(
                        "SceneTransition prefab has no controller."
                    );
                SetBool(controller, "startCovered", startCovered);
                SetBool(controller, "revealOnStart", revealOnStart);
                ConfigureShutter(controller, "defaultSettings");
                ConfigureShutter(controller, "enterSettings");
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller);

                if (configureTop)
                {
                    var topController = scene
                        .GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<TopSceneController>(true))
                        .SingleOrDefault();
                    if (topController == null)
                        throw new InvalidOperationException(
                            "TopSceneController is missing from Top scene."
                        );

                    var serialized = new SerializedObject(topController);
                    serialized.FindProperty("nextSceneName").stringValue = "Main";
                    serialized.FindProperty("transition").objectReferenceValue = controller;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(topController);
                }

                // 既に開いていてdirtyなシーンは、ユーザーの未保存調整を上書きしない。
                // その場合は変更を開いたまま残し、ユーザーが内容を確認して保存できるようにする。
                if (ownsScene || !wasDirty)
                    EditorSceneManager.SaveScene(scene, scenePath);
            }
            finally
            {
                if (ownsScene)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ConfigureShutter(UnityEngine.Object target, string settingsName)
        {
            SetEnum(target, $"{settingsName}.type", SceneTransitionType.Shutter);
            SetEnum(target, $"{settingsName}.shutterAxis", SceneTransitionShutterAxis.Vertical);
            SetFloat(target, $"{settingsName}.coverDuration", TransitionDuration);
            SetFloat(target, $"{settingsName}.revealDuration", TransitionDuration);
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folder))
                throw new InvalidOperationException($"Invalid asset folder path: {path}");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
