using System;
using System.IO;
using System.Linq;
using Baryonyx.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
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
        private const string TopTitleGradientTexturePath =
            "Assets/Baryonyx/App/Art/Top/TopTitlePanelGradient.png";
        private const string TopTitleBackdropGradientTexturePath =
            "Assets/Baryonyx/App/Art/Top/TopTitleBackdropGradient.png";
        private static readonly Color TopTitleBackdropColor = new Color(
            0.3f,
            0.3f,
            0.3f,
            0.42f
        );

        [MenuItem("Baryonyx/App/Create Top and Home Scenes")]
        public static void CreateScenes()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

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
                return;

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
                typeof(UnityEngine.UI.RawImage)
            );
            SceneManager.MoveGameObjectToScene(background, scene);
            background.transform.SetParent(parent, false);
            Stretch(background.GetComponent<RectTransform>());

            var rawImage = background.GetComponent<UnityEngine.UI.RawImage>();
            rawImage.texture = LoadTexture(TopBackgroundTexturePath);
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;
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

            CreateTopTitlePanel(scene, screen.transform);
        }

        private static void CreateTopTitleBackdrop(Scene scene, Transform parent)
        {
            var backdrop = new GameObject(
                "TopTitleBackdropCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.RawImage)
            );
            SceneManager.MoveGameObjectToScene(backdrop, scene);
            backdrop.transform.SetParent(parent, false);

            var rect = backdrop.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1320f, 260f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var canvas = backdrop.GetComponent<Canvas>();
            canvas.overrideSorting = false;

            var rawImage = backdrop.GetComponent<UnityEngine.UI.RawImage>();
            rawImage.texture = LoadTexture(TopTitleBackdropGradientTexturePath);
            rawImage.color = TopTitleBackdropColor;
            rawImage.raycastTarget = false;
        }

        private static void CreateTopTitlePanel(Scene scene, Transform parent)
        {
            var panel = new GameObject(
                "TopTitlePanel",
                typeof(RectTransform),
                typeof(UnityEngine.UI.RawImage)
            );
            SceneManager.MoveGameObjectToScene(panel, scene);
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.5f);
            rect.anchorMax = new Vector2(0.9f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 120f);
            rect.sizeDelta = new Vector2(0f, 300f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var rawImage = panel.GetComponent<UnityEngine.UI.RawImage>();
            rawImage.texture = LoadTexture(TopTitleGradientTexturePath);
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;

            CreateTopTitleBackdrop(scene, panel.transform);
            CreateTopTitle(scene, panel.transform);
        }

        private static void CreateTopTitle(Scene scene, Transform parent)
        {
            var titleObject = new GameObject(
                "TopTitle",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );
            SceneManager.MoveGameObjectToScene(titleObject, scene);
            titleObject.transform.SetParent(parent, false);

            var rect = titleObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var title = titleObject.GetComponent<TextMeshProUGUI>();
            title.text = "てくてくダンジョン";
            title.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Baryonyx/Features/Health/UI/Fonts/DotGothic16.asset"
            );
            title.fontSize = 128f;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
            title.raycastTarget = false;
            title.textWrappingMode = TextWrappingModes.NoWrap;
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
