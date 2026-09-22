using System;
using System.IO;
using System.Linq;
using Baryonyx.App;
using Baryonyx.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Baryonyx.App.Editor
{
    public static class TopHomeSceneSetup
    {
        public const string TopScenePath = "Assets/Baryonyx/App/Scenes/Top.unity";
        public const string HomeScenePath = "Assets/Baryonyx/App/Scenes/Home.unity";
        private const string TopBackgroundTexturePath =
            "Assets/Baryonyx/App/Art/Top/TopDungeonBackground.png";
        private const string TextPanelPrefabPath =
            "Assets/Baryonyx/Shared/UI/TranslucentTextPanel/TranslucentTextPanel.prefab";
        private const string Hd2dLightingPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dLightingVfx.prefab";
        private const string Hd2dLightShaftPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dLightShaft.prefab";

        [MenuItem("Baryonyx/App/Create Top and Home Scenes")]
        public static void CreateScenes()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

            TranslucentTextPanelPrefabSetup.EnsurePrefab();
            Hd2dLightingVfxAssetSetup.EnsureAssets();
            CreateSceneIfMissing(
                TopScenePath,
                "TopCanvas",
                "TopScreen",
                new Color(0.035f, 0.047f, 0.075f, 1f),
                clickable: true
            );
            CreateSceneIfMissing(
                HomeScenePath,
                "HomeCanvas",
                "HomeScreen",
                new Color(0.94f, 0.96f, 0.94f, 1f),
                clickable: false
            );

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Baryonyx/App/Open Top Scene")]
        public static void OpenTopScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            EditorSceneManager.OpenScene(TopScenePath);
        }

        [MenuItem("Baryonyx/App/Open Home Scene")]
        public static void OpenHomeScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            EditorSceneManager.OpenScene(HomeScenePath);
        }

        private static void CreateSceneIfMissing(
            string scenePath,
            string canvasName,
            string screenName,
            Color screenColor,
            bool clickable
        )
        {
            if (File.Exists(scenePath))
            {
                if (clickable)
                    UpdateTopScene(scenePath);
                return;
            }

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive
            );
            try
            {
                var canvas = CreateCanvas(scene, canvasName);
                if (clickable)
                {
                    CreateTopBackground(scene, canvas.transform);
                    EnsureTopLightingVfx(scene);
                    EnsureTopLightShaft(scene);
                    CreateTopScreen(scene, canvas.transform, screenName);
                }
                else
                    CreateHomeScreen(scene, canvas.transform, screenName, screenColor);

                CreateCamera(scene, screenName + "Camera", screenColor);
                CreateEventSystem(scene);
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            finally
            {
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Canvas CreateCanvas(Scene scene, string canvasName)
        {
            var canvasObject = new GameObject(
                canvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster)
            );
            SceneManager.MoveGameObjectToScene(canvasObject, scene);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            return canvas;
        }

        private static void CreateTopBackground(Scene scene, Transform parent)
        {
            var background = new GameObject(
                "TopBackground",
                typeof(RectTransform),
                typeof(UnityEngine.UI.RawImage),
                typeof(ResponsiveBackground)
            );
            SceneManager.MoveGameObjectToScene(background, scene);
            background.transform.SetParent(parent, false);
            Stretch(background.GetComponent<RectTransform>());

            var rawImage = background.GetComponent<UnityEngine.UI.RawImage>();
            var texture = LoadTexture(TopBackgroundTexturePath);
            rawImage.texture = texture;
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;
            background.GetComponent<ResponsiveBackground>().AspectRatio =
                texture.width / (float)texture.height;
        }

        private static void CreateTopScreen(
            Scene scene,
            Transform parent,
            string screenName
        )
        {
            var screen = new GameObject(
                screenName,
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button),
                typeof(TopSceneController)
            );
            SceneManager.MoveGameObjectToScene(screen, scene);
            screen.transform.SetParent(parent, false);
            Stretch(screen.GetComponent<RectTransform>());

            var image = screen.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            var button = screen.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.None,
            };

            var safeArea = CreateTopSafeArea(scene, screen.transform);
            CreateTopTitlePanel(scene, safeArea);
            CreateTapToStartPanel(scene, safeArea);
        }

        private static RectTransform CreateTopSafeArea(Scene scene, Transform parent)
        {
            var safeArea = new GameObject(
                "TopSafeArea",
                typeof(RectTransform),
                typeof(SafeAreaFollower)
            );
            SceneManager.MoveGameObjectToScene(safeArea, scene);
            safeArea.transform.SetParent(parent, false);
            Stretch(safeArea.GetComponent<RectTransform>());
            return safeArea.GetComponent<RectTransform>();
        }

        private static void CreateTopTitlePanel(Scene scene, Transform parent)
        {
            var prefab = LoadTextPanelPrefab();
            var panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            panel.name = "TopTitlePanel";
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.5f);
            rect.anchorMax = new Vector2(0.9f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 120f);
            rect.sizeDelta = new Vector2(0f, 300f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var component = panel.GetComponent<TranslucentTextPanel>();
            var title = component.Label;
            title.text = "てくてくダンジョン";
            component.SetFontSize(128f);
            component.SetBackdropSize(new Vector2(1320f, 260f));
            component.SetPulseEnabled(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(title);
        }

        private static void CreateTapToStartPanel(Scene scene, Transform parent)
        {
            var panel = (GameObject)PrefabUtility.InstantiatePrefab(LoadTextPanelPrefab(), scene);
            panel.name = "TapToStartPanel";
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.16f);
            rect.anchorMax = new Vector2(0.5f, 0.16f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(820f, 112f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var component = panel.GetComponent<TranslucentTextPanel>();
            component.SetBackdropSize(new Vector2(760f, 92f));
            component.SetText("TAP TO START");
            component.SetFontSize(48f);
            component.SetPulseEnabled(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component.Label);
        }

        private static GameObject LoadTextPanelPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TextPanelPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"UI prefab not found: {TextPanelPrefabPath}");
            return prefab;
        }

        private static void UpdateTopScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var screen = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(candidate => candidate.name == "TopScreen");
            if (screen == null)
                throw new InvalidOperationException($"TopScreen not found in {scenePath}");

            var current = screen
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == "TopTitlePanel");
            if (current != null)
                UnityEngine.Object.DestroyImmediate(current.gameObject);
            var currentTap = screen
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == "TapToStartPanel");
            if (currentTap != null)
                UnityEngine.Object.DestroyImmediate(currentTap.gameObject);
            var background = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(candidate => candidate.name == "TopBackground");
            if (background != null)
            {
                var rawImage = background.GetComponent<UnityEngine.UI.RawImage>();
                var responsive = background.GetComponent<ResponsiveBackground>();
                if (responsive == null)
                    responsive = background.gameObject.AddComponent<ResponsiveBackground>();
                if (rawImage != null && rawImage.texture != null)
                    responsive.AspectRatio =
                        rawImage.texture.width / (float)rawImage.texture.height;
            }
            EnsureTopLightingVfx(scene);
            EnsureTopLightShaft(scene);
            var safeArea = screen.Find("TopSafeArea");
            if (safeArea == null)
                safeArea = CreateTopSafeArea(scene, screen);
            else if (safeArea.GetComponent<SafeAreaFollower>() == null)
                safeArea.gameObject.AddComponent<SafeAreaFollower>();
            CreateTopTitlePanel(scene, safeArea);
            CreateTapToStartPanel(scene, safeArea);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        public static bool EnsureTopLightingVfx(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            var canvas = scene
                .GetRootGameObjects()
                .FirstOrDefault(root => root.name == "TopCanvas");
            if (canvas == null)
                return false;

            var existing = canvas
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == "TopHd2dLightingVfx");
            if (existing != null)
                return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hd2dLightingPrefabPath);
            if (prefab == null)
                return false;

            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException(
                    $"HD-2D lighting prefab could not be instantiated: {Hd2dLightingPrefabPath}"
                );

            instance.name = "TopHd2dLightingVfx";
            instance.transform.SetParent(canvas.transform, false);
            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
                Stretch(rect);

            var background = canvas.transform.Find("TopBackground");
            var screen = canvas.transform.Find("TopScreen");
            if (background != null && screen != null)
            {
                var siblingIndex = Mathf.Min(
                    background.GetSiblingIndex() + 1,
                    screen.GetSiblingIndex()
                );
                instance.transform.SetSiblingIndex(siblingIndex);
            }
            else
                instance.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        public static bool EnsureTopLightShaft(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            var canvas = scene
                .GetRootGameObjects()
                .FirstOrDefault(root => root.name == "TopCanvas");
            if (canvas == null)
                return false;

            var existing = canvas
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == "TopHd2dLightShaft");
            if (existing != null)
                return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hd2dLightShaftPrefabPath);
            if (prefab == null)
                return false;

            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException(
                    $"HD-2D light shaft prefab could not be instantiated: {Hd2dLightShaftPrefabPath}"
                );

            instance.name = "TopHd2dLightShaft";
            instance.transform.SetParent(canvas.transform, false);
            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
                Stretch(rect);

            var background = canvas.transform.Find("TopBackground");
            var screen = canvas.transform.Find("TopScreen");
            if (background != null && screen != null)
            {
                var siblingIndex = Mathf.Min(
                    background.GetSiblingIndex() + 1,
                    screen.GetSiblingIndex()
                );
                instance.transform.SetSiblingIndex(siblingIndex);
            }
            else
                instance.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
                throw new InvalidOperationException($"Texture asset not found: {assetPath}");
            return texture;
        }

        private static void CreateHomeScreen(
            Scene scene,
            Transform parent,
            string screenName,
            Color color
        )
        {
            var screen = new GameObject(
                screenName,
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image)
            );
            SceneManager.MoveGameObjectToScene(screen, scene);
            screen.transform.SetParent(parent, false);
            Stretch(screen.GetComponent<RectTransform>());

            var image = screen.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void CreateCamera(Scene scene, string cameraName, Color backgroundColor)
        {
            var cameraObject = new GameObject(cameraName, typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            camera.orthographic = true;
            camera.tag = "MainCamera";
        }

        private static void CreateEventSystem(Scene scene)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem));
            SceneManager.MoveGameObjectToScene(events, scene);
            events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.path != TopScenePath && scene.path != HomeScenePath)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(HomeScenePath, true));
            scenes.Insert(0, new EditorBuildSettingsScene(TopScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
