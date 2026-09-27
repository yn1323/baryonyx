using System;
using System.Linq;
using Baryonyx.Showcase.Editor;
using Baryonyx.Tavern.Editor;
using Baryonyx.Temple.Editor;
using Baryonyx.TravelOffice.Editor;
using Baryonyx.UI.GuideMenu;
using Baryonyx.UI.GuideMenu.Editor;
using Baryonyx.Workshop.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds the guide screen scenes opened from the Home buttons: each has the feature's
    /// screen prefab, a bootstrap that returns to Home, the shared shutter and input.
    /// </summary>
    public static class GuideSceneSetup
    {
        public const string ScenesFolder = "Assets/Baryonyx/App/Scenes";
        private static readonly Color CameraColor = new(0.035f, 0.047f, 0.075f, 1f);

        // Scene name → the feature prefab it shows. Home opens these scenes by name.
        private static readonly (string Scene, string Prefab, Action Create)[] Screens =
        {
            ("Tavern", TavernScreenAssets.PrefabPath, TavernScreenAssets.CreateAssets),
            ("Workshop", WorkshopScreenAssets.PrefabPath, WorkshopScreenAssets.CreateAssets),
            ("Temple", TempleScreenAssets.PrefabPath, TempleScreenAssets.CreateAssets),
            (
                "TravelOffice",
                TravelOfficeScreenAssets.PrefabPath,
                TravelOfficeScreenAssets.CreateAssets
            ),
        };

        public static string ScenePath(string scene) => $"{ScenesFolder}/{scene}.unity";

        [MenuItem("Baryonyx/App/Create Guide Scenes")]
        public static void CreateGuideScenes()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

            GuideMenuAssets.CreateSharedArt();
            foreach (var screen in Screens)
            {
                screen.Create();
                CreateScene(screen.Scene, screen.Prefab);
            }
            AddToBuildSettings();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }

        private static void CreateScene(string sceneName, string prefabPath)
        {
            var path = ScenePath(sceneName);
            var loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded)
                throw new InvalidOperationException(
                    $"Close {sceneName}.unity before rebuilding it, so open edits are not overwritten."
                );
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException(
                    "Screen prefab was not generated: " + prefabPath
                );

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive
            );
            try
            {
                var cameraObject = new GameObject(sceneName + "Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = CameraColor;
                camera.orthographic = true;
                camera.tag = "MainCamera";

                var screen = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                screen.name = sceneName + "Screen";

                var bootstrapObject = new GameObject(
                    sceneName + "Bootstrap",
                    typeof(GuideSceneBootstrap)
                );
                SceneManager.MoveGameObjectToScene(bootstrapObject, scene);

                var events = new GameObject("EventSystem", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(events, scene);
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

                var transition = SceneTransitionSetup.AddTransition(
                    scene,
                    startCovered: true,
                    revealOnStart: true
                );

                var serialized = new SerializedObject(
                    bootstrapObject.GetComponent<GuideSceneBootstrap>()
                );
                serialized.FindProperty("view").objectReferenceValue =
                    screen.GetComponent<GuideMenuView>();
                serialized.FindProperty("transition").objectReferenceValue = transition;
                serialized.FindProperty("homeSceneName").stringValue = "Home";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.SaveScene(scene, path);
            }
            finally
            {
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // Appends missing scenes after the existing ones, keeping their order and flags.
        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var screen in Screens)
            {
                var path = ScenePath(screen.Scene);
                if (scenes.All(scene => scene.path != path))
                    scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
