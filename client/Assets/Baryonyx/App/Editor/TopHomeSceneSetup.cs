using System;
using System.IO;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Editor;
using Baryonyx.Stages.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.Vfx.Hd2d;
using Baryonyx.Vfx.Hd2d.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.App.Editor
{
    public static class TopHomeSceneSetup
    {
        public const string TopScenePath = "Assets/Baryonyx/App/Scenes/Top.unity";
        public const string HomeScenePath = "Assets/Baryonyx/App/Scenes/Home.unity";
        private const string TextPanelPrefabPath =
            "Assets/Baryonyx/Shared/UI/TranslucentTextPanel/TranslucentTextPanel.prefab";
        public const string TopBackdropCanvasName = "TopBackdropCanvas";
        public const string TopPostProcessVolumeName = "TopPostProcessVolume";

        [MenuItem("Baryonyx/App/Create Top and Home Scenes")]
        public static void CreateScenes()
        {
            EditorGuard.RequireEditMode();

            TranslucentTextPanelAssets.EnsurePrefab();
            Hd2dAssets.EnsureAssets();
            StageSetAssets.EnsureAssets();
            CreateSceneIfMissing(
                TopScenePath,
                "TopCanvas",
                "TopScreen",
                new Color(0.035f, 0.047f, 0.075f, 1f),
                clickable: true
            );
            if (!File.Exists(HomeScenePath))
                HomeSceneSetup.CreateHomeScene();

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Baryonyx/App/Open Top Scene")]
        public static void OpenTopScene()
        {
            EditorGuard.RequireEditMode();
            EditorSceneManager.OpenScene(TopScenePath);
        }

        [MenuItem("Baryonyx/App/Open Home Scene")]
        public static void OpenHomeScene()
        {
            EditorGuard.RequireEditMode();
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
                    EnsureTopLightingVfx(scene);
                    CreateTopScreen(scene, canvas.transform, screenName);
                }

                ScreenScenes.AddCamera(scene, screenName + "Camera", screenColor);
                if (clickable)
                {
                    EnsureTopPostProcess(scene);
                    EnsureTopStage(scene);
                    TopStartupSyncSetup.EnsureTop(scene);
                }
                ScreenScenes.AddEventSystem(scene);
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

        private static void CreateTopScreen(Scene scene, Transform parent, string screenName)
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
            title.text = "てくてくダンジョン（仮）";
            component.SetFontSize(96f);
            component.SetBackdropSize(new Vector2(1320f, 260f));
            component.SetBackdropAlpha(0.2f);
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
            component.SetBackdropAlpha(0.2f);
            component.SetPulseEnabled(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component.Label);

            // 入力の受付開始に合わせて表示するため、TopSceneControllerへ結び付ける。
            var controller = parent.GetComponentInParent<TopSceneController>(true);
            if (controller == null)
                throw new InvalidOperationException(
                    "TopSceneController is missing from TopScreen."
                );
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("tapToStartPrompt").objectReferenceValue = panel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
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
            var screen = scene
                .GetRootGameObjects()
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
            EnsureTopLightingVfx(scene);
            EnsureTopPostProcess(scene);
            EnsureTopStage(scene);
            var safeArea = screen.Find("TopSafeArea");
            if (safeArea == null)
                safeArea = CreateTopSafeArea(scene, screen);
            else if (safeArea.GetComponent<SafeAreaFollower>() == null)
                safeArea.gameObject.AddComponent<SafeAreaFollower>();
            CreateTopTitlePanel(scene, safeArea);
            CreateTapToStartPanel(scene, safeArea);
            TopStartupSyncSetup.EnsureTop(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        /// <summary>
        /// Moves the background and HD-2D layers to a camera-rendered canvas so that the camera
        /// post-processing (Bloom, Vignette, Color Adjustments) reaches them, while the title
        /// and the tap target stay on the overlay canvas and keep crisp text.
        /// </summary>
        public static bool EnsureTopPostProcess(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            var roots = scene.GetRootGameObjects();
            var uiCanvas = roots.FirstOrDefault(root => root.name == "TopCanvas");
            var camera = roots
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault();
            // The stage's own look: its lens, bloom, vignette and grade.
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                StageSetAssets.StarlitGateLookPath
            );
            if (uiCanvas == null || camera == null || profile == null)
                return false;

            var changed = false;
            var backdrop = roots.FirstOrDefault(root => root.name == TopBackdropCanvasName);
            if (backdrop == null)
            {
                backdrop = CreateBackdropCanvas(scene, uiCanvas, camera);
                changed = true;
            }

            // Everything except the interactive screen belongs to the lit backdrop.
            var layers = uiCanvas
                .transform.Cast<Transform>()
                .Where(child => child.name.StartsWith("TopHd2d"))
                .ToList();
            foreach (var layer in layers)
            {
                layer.SetParent(backdrop.transform, false);
                layer.SetAsLastSibling();
                changed = true;
            }

            var cameraData = camera.GetUniversalAdditionalCameraData();
            if (!cameraData.renderPostProcessing)
            {
                cameraData.renderPostProcessing = true;
                EditorUtility.SetDirty(cameraData);
                changed = true;
            }

            var existing = roots.FirstOrDefault(root => root.name == TopPostProcessVolumeName);
            if (existing == null)
            {
                var volumeObject = new GameObject(TopPostProcessVolumeName, typeof(Volume));
                SceneManager.MoveGameObjectToScene(volumeObject, scene);
                var volume = volumeObject.GetComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 0f;
                volume.sharedProfile = profile;
                changed = true;
            }
            else if (existing.GetComponent<Volume>().sharedProfile != profile)
            {
                existing.GetComponent<Volume>().sharedProfile = profile;
                changed = true;
            }

            if (changed)
                EditorSceneManager.MarkSceneDirty(scene);
            return changed;
        }

        /// <summary>
        /// Puts Top on the 3D mountain at night (HD-2D) in place of the painted background: the
        /// camera looks along the torch-lit path to the glowing gate under the starry sky, sways
        /// slowly so near and far part, and glides in when the title opens. The painted
        /// background, the torch glows and embers pinned to it, and the light shaft from the
        /// dungeon's ceiling are removed; a stage put in earlier (the dungeon hall) is replaced.
        /// </summary>
        public static bool EnsureTopStage(Scene scene)
        {
            if (!scene.IsValid())
                return false;
            var roots = scene.GetRootGameObjects();
            bool changed = false;
            foreach (
                var name in new[]
                {
                    "TopBackground",
                    "TopHd2dFlickerLight",
                    "TopHd2dEmberEmitter",
                    "TopHd2dLightShaft",
                }
            )
            {
                var painted = roots
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(candidate => candidate.name == name);
                if (painted == null)
                    continue;
                UnityEngine.Object.DestroyImmediate(painted.gameObject);
                changed = true;
            }

            var backdrop = roots.FirstOrDefault(root => root.name == TopBackdropCanvasName);
            if (backdrop != null)
            {
                var canvas = backdrop.GetComponent<Canvas>();
                if (!Mathf.Approximately(canvas.planeDistance, 1f))
                {
                    canvas.planeDistance = 1f;
                    changed = true;
                }
            }

            var stage = roots.FirstOrDefault(root => root.name == Hd2dStageSceneSetup.StageName);
            if (
                stage != null
                && AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(stage))
                    != StageSetAssets.StarlitGatePrefabPath
            )
            {
                UnityEngine.Object.DestroyImmediate(stage);
                stage = null;
                changed = true;
            }
            if (stage == null)
            {
                var camera = roots
                    .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .First();
                var stageCamera = Hd2dStageSceneSetup.Apply(
                    scene,
                    camera,
                    StageSetAssets.StarlitGatePrefabPath,
                    StageSetAssets.TopView,
                    StageSetAssets.StarlitEnvironment
                );
                stageCamera.SwayRadius = 10f;
                stageCamera.SwayPeriod = 30f;
                changed = true;
            }

            // The title opens with the camera gliding along the path towards the gate.
            var titleCamera = roots
                .SelectMany(root => root.GetComponentsInChildren<Hd2dStageCamera>(true))
                .FirstOrDefault();
            var intro = new Vector3(0f, 0.4f, -2.2f);
            if (titleCamera != null && titleCamera.IntroOffset != intro)
            {
                titleCamera.IntroOffset = intro;
                titleCamera.IntroSeconds = 4f;
                changed = true;
            }

            if (changed)
                EditorSceneManager.MarkSceneDirty(scene);
            return changed;
        }

        private static GameObject CreateBackdropCanvas(
            Scene scene,
            GameObject uiCanvas,
            Camera camera
        )
        {
            var backdrop = new GameObject(
                TopBackdropCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler)
            );
            SceneManager.MoveGameObjectToScene(backdrop, scene);
            backdrop.transform.SetSiblingIndex(uiCanvas.transform.GetSiblingIndex());

            var canvas = backdrop.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            // Close to the camera, so the 3D hall behind it never hides the effects.
            canvas.planeDistance = 1f;

            // Match the overlay canvas scale so both canvases share the same layout units.
            var source = uiCanvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            var scaler = backdrop.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = source.uiScaleMode;
            scaler.referenceResolution = source.referenceResolution;
            scaler.screenMatchMode = source.screenMatchMode;
            scaler.matchWidthOrHeight = source.matchWidthOrHeight;
            return backdrop;
        }

        private static GameObject FindTopBackdrop(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var backdrop = roots.FirstOrDefault(root => root.name == TopBackdropCanvasName);
            if (backdrop != null)
                return backdrop;
            return roots.FirstOrDefault(root => root.name == "TopCanvas");
        }

        // Topの背景用Canvasに、まだない演出のPrefabを置く。置かなかったときはnullを返す。
        private static GameObject AddToBackdrop(Scene scene, string prefabPath, string name)
        {
            if (!scene.IsValid())
                return null;
            var backdrop = FindTopBackdrop(scene);
            if (backdrop == null)
                return null;
            var canvas = backdrop.transform;
            if (canvas.GetComponentsInChildren<Transform>(true).Any(child => child.name == name))
                return null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return null;
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException(
                    $"HD-2D prefab could not be instantiated: {prefabPath}"
                );
            instance.name = name;
            instance.transform.SetParent(canvas, false);
            return instance;
        }

        private static void StretchIfRect(GameObject instance)
        {
            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
                Stretch(rect);
        }

        public static bool EnsureTopLightingVfx(Scene scene)
        {
            var instance = AddToBackdrop(scene, Hd2dAssets.PrefabPath, "TopHd2dLightingVfx");
            if (instance == null)
                return false;
            StretchIfRect(instance);
            // 3Dの舞台の手前、タイトルと開始操作（別のCanvas）より奥に重ねる。
            instance.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        private static void EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings
                .scenes.Where(scene => scene.path != TopScenePath && scene.path != HomeScenePath)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(HomeScenePath, true));
            scenes.Insert(0, new EditorBuildSettingsScene(TopScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
