using System;
using System.IO;
using System.Linq;
using Baryonyx.App;
using Baryonyx.UI;
using Baryonyx.Vfx.Hd2d;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        private const string Hd2dFogPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dFog.prefab";
        private const string Hd2dPostProcessProfilePath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Profiles/Hd2dPostProcess.asset";
        public const string TopBackdropCanvasName = "TopBackdropCanvas";
        public const string TopPostProcessVolumeName = "TopPostProcessVolume";
        private const string Hd2dEmberEmitterPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dEmberEmitter.prefab";
        private const string Hd2dFlickerLightPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dFlickerLight.prefab";

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
                    EnsureTopFog(scene);
                    EnsureTopFlickerLight(scene);
                    EnsureTopEmberEmitter(scene);
                    CreateTopScreen(scene, canvas.transform, screenName);
                }
                else
                    CreateHomeScreen(scene, canvas.transform, screenName, screenColor);

                CreateCamera(scene, screenName + "Camera", screenColor);
                if (clickable)
                    EnsureTopPostProcess(scene);
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
            var background = scene
                .GetRootGameObjects()
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
            EnsureTopFog(scene);
            EnsureTopFlickerLight(scene);
            EnsureTopEmberEmitter(scene);
            EnsureTopPostProcess(scene);
            var safeArea = screen.Find("TopSafeArea");
            if (safeArea == null)
                safeArea = CreateTopSafeArea(scene, screen);
            else if (safeArea.GetComponent<SafeAreaFollower>() == null)
                safeArea.gameObject.AddComponent<SafeAreaFollower>();
            CreateTopTitlePanel(scene, safeArea);
            CreateTapToStartPanel(scene, safeArea);
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
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Hd2dPostProcessProfilePath);
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
                .Where(child => child.name == "TopBackground" || child.name.StartsWith("TopHd2d"))
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

            if (roots.All(root => root.name != TopPostProcessVolumeName))
            {
                var volumeObject = new GameObject(TopPostProcessVolumeName, typeof(Volume));
                SceneManager.MoveGameObjectToScene(volumeObject, scene);
                var volume = volumeObject.GetComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 0f;
                volume.sharedProfile = profile;
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
            canvas.planeDistance = 10f;

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

        public static bool EnsureTopLightingVfx(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            var canvas = FindTopBackdrop(scene);
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

            var canvas = FindTopBackdrop(scene);
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

        public static bool EnsureTopFog(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            var canvas = FindTopBackdrop(scene);
            if (canvas == null)
                return false;

            var existing = canvas
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == "TopHd2dFog");
            if (existing != null)
                return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hd2dFogPrefabPath);
            if (prefab == null)
                return false;

            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException(
                    $"HD-2D fog prefab could not be instantiated: {Hd2dFogPrefabPath}"
                );

            instance.name = "TopHd2dFog";
            instance.transform.SetParent(canvas.transform, false);
            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
                Stretch(rect);

            // Fog sits directly on the background so the light shafts and particles shine through it.
            var background = canvas.transform.Find("TopBackground");
            if (background != null)
                instance.transform.SetSiblingIndex(background.GetSiblingIndex() + 1);
            else
                instance.transform.SetAsFirstSibling();

            var fog = instance.GetComponent<Hd2dFog>();
            if (fog != null)
            {
                // Appending keeps the prefab's floor mist layers un-overridden in the scene.
                fog.Layers.Add(CreateTopDeepHaze());
                PrefabUtility.RecordPrefabInstancePropertyModifications(fog);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        public static bool EnsureTopFlickerLight(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            var canvas = FindTopBackdrop(scene);
            if (canvas == null)
                return false;

            var existing = canvas
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == "TopHd2dFlickerLight");
            if (existing != null)
                return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hd2dFlickerLightPrefabPath);
            if (prefab == null)
                return false;

            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException(
                    $"HD-2D flicker light prefab could not be instantiated: {Hd2dFlickerLightPrefabPath}"
                );

            instance.name = "TopHd2dFlickerLight";
            instance.transform.SetParent(canvas.transform, false);

            AlignWithBackground(canvas.transform, instance);
            var background = canvas.transform.Find("TopBackground");

            // Lights sit above the fog so the flames are not veiled, and below the shafts.
            var fog = canvas.transform.Find("TopHd2dFog");
            if (fog != null)
                instance.transform.SetSiblingIndex(fog.GetSiblingIndex() + 1);
            else if (background != null)
                instance.transform.SetSiblingIndex(background.GetSiblingIndex() + 1);
            else
                instance.transform.SetAsFirstSibling();

            var light = instance.GetComponent<Hd2dFlickerLight>();
            if (light != null)
            {
                light.Sources = CreateTopTorches();
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        public static bool EnsureTopEmberEmitter(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            var canvas = FindTopBackdrop(scene);
            if (canvas == null)
                return false;

            var existing = canvas
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == "TopHd2dEmberEmitter");
            if (existing != null)
                return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hd2dEmberEmitterPrefabPath);
            if (prefab == null)
                return false;

            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException(
                    $"HD-2D ember emitter prefab could not be instantiated: {Hd2dEmberEmitterPrefabPath}"
                );

            instance.name = "TopHd2dEmberEmitter";
            instance.transform.SetParent(canvas.transform, false);
            AlignWithBackground(canvas.transform, instance);

            // Embers rise in front of the torch glow and behind the light shafts.
            var light = canvas.transform.Find("TopHd2dFlickerLight");
            var background = canvas.transform.Find("TopBackground");
            if (light != null)
                instance.transform.SetSiblingIndex(light.GetSiblingIndex() + 1);
            else if (background != null)
                instance.transform.SetSiblingIndex(background.GetSiblingIndex() + 1);
            else
                instance.transform.SetAsFirstSibling();

            var emitter = instance.GetComponent<Hd2dEmberEmitter>();
            if (emitter != null)
            {
                emitter.Sources = CreateTopTorchEmbers();
                PrefabUtility.RecordPrefabInstancePropertyModifications(emitter);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        private static void AlignWithBackground(Transform canvas, GameObject instance)
        {
            // Painted light sources move with the covered background, not with the screen.
            var background = canvas.Find("TopBackground");
            var backgroundImage =
                background != null ? background.GetComponent<UnityEngine.UI.RawImage>() : null;
            var aligned = instance.AddComponent<ResponsiveBackground>();
            if (backgroundImage != null && backgroundImage.texture != null)
                aligned.AspectRatio =
                    backgroundImage.texture.width / (float)backgroundImage.texture.height;
            aligned.Apply();
        }

        private static System.Collections.Generic.List<Hd2dEmberSource> CreateTopTorchEmbers()
        {
            // Same background coordinates as the flicker lights, just above each flame.
            return new System.Collections.Generic.List<Hd2dEmberSource>
            {
                CreateTorchEmbers("OuterLeft", new Vector2(0.105f, 0.745f), 10, 1f),
                CreateTorchEmbers("InnerLeft", new Vector2(0.358f, 0.652f), 6, 0.7f),
                CreateTorchEmbers("InnerRight", new Vector2(0.642f, 0.652f), 6, 0.7f),
                CreateTorchEmbers("OuterRight", new Vector2(0.894f, 0.745f), 10, 1f),
            };
        }

        private static Hd2dEmberSource CreateTorchEmbers(
            string name,
            Vector2 anchor,
            int count,
            float scale
        )
        {
            return new Hd2dEmberSource
            {
                Name = name,
                Anchor = anchor,
                SpawnArea = new Vector2(28f, 10f) * scale,
                Count = count,
                Loop = true,
                LifetimeRange = new Vector2(1.1f, 2.3f),
                Direction = 90f,
                Spread = 34f,
                SpeedRange = new Vector2(34f, 72f) * scale,
                Buoyancy = 14f * scale,
                Sway = 9f * scale,
                SwaySpeed = 1.7f,
                SizeRange = scale < 1f ? new Vector2(3f, 5f) : new Vector2(4f, 7f),
                StartColor = new Color(1f, 0.84f, 0.46f, 1f),
                EndColor = new Color(1f, 0.32f, 0.08f, 0f),
                Twinkle = 0.35f,
            };
        }

        private static System.Collections.Generic.List<Hd2dFlickerLightSource> CreateTopTorches()
        {
            // Positions are normalized to TopDungeonBackground.png (origin at the bottom left).
            // The outer torches hang on the near pillars; the inner ones on the far wall.
            return new System.Collections.Generic.List<Hd2dFlickerLightSource>
            {
                CreateTorch("OuterLeft", new Vector2(0.105f, 0.72f), new Vector2(0.17f, 0.27f), 1f),
                CreateTorch(
                    "InnerLeft",
                    new Vector2(0.358f, 0.635f),
                    new Vector2(0.37f, 0.36f),
                    0.7f
                ),
                CreateTorch(
                    "InnerRight",
                    new Vector2(0.642f, 0.635f),
                    new Vector2(0.63f, 0.36f),
                    0.7f
                ),
                CreateTorch(
                    "OuterRight",
                    new Vector2(0.894f, 0.72f),
                    new Vector2(0.83f, 0.27f),
                    1f
                ),
            };
        }

        private static Hd2dFlickerLightSource CreateTorch(
            string name,
            Vector2 anchor,
            Vector2 reflectionAnchor,
            float scale
        )
        {
            return new Hd2dFlickerLightSource
            {
                Name = name,
                Anchor = anchor,
                Color = new Color(1f, 0.58f, 0.24f, 1f),
                CoreSize = new Vector2(140f, 150f) * scale,
                CoreAlpha = 0.55f,
                HaloSize = new Vector2(460f, 460f) * scale,
                HaloAlpha = 0.22f,
                ReflectionAnchor = reflectionAnchor,
                ReflectionSize = new Vector2(420f, 100f) * scale,
                ReflectionAlpha = 0.24f,
                FlickerAmount = 0.22f,
                FlickerSpeed = 2.4f,
                SizeJitter = 0.05f,
            };
        }

        private static Hd2dFogLayer CreateTopDeepHaze()
        {
            // A pale haze inside the far archway pushes the corridor back (aerial perspective).
            return new Hd2dFogLayer
            {
                Name = "DeepHaze",
                AnchorMin = new Vector2(0.36f, 0.34f),
                AnchorMax = new Vector2(0.64f, 0.86f),
                Color = new Color(0.45f, 0.58f, 0.82f, 0.22f),
                Softness = new Vector2Int(150, 170),
                TileSize = new Vector2(420f, 300f),
                ScrollSpeed = new Vector2(5f, 3f),
                DetailOpacity = 0.5f,
                BreathAmount = 0.2f,
                BreathSpeed = 0.15f,
            };
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
            var scenes = EditorBuildSettings
                .scenes.Where(scene => scene.path != TopScenePath && scene.path != HomeScenePath)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(HomeScenePath, true));
            scenes.Insert(0, new EditorBuildSettingsScene(TopScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
