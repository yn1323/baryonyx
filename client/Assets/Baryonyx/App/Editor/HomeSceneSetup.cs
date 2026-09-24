using System;
using Baryonyx.Home;
using Baryonyx.Home.Editor;
using Baryonyx.Showcase.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds Home.unity as the camp home mock: the generated home prefab, a bootstrap
    /// that feeds it fixed sample data, the shared shutter transition, and input.
    /// </summary>
    public static class HomeSceneSetup
    {
        public const string HomeScenePath = TopHomeSceneSetup.HomeScenePath;
        private static readonly Color CameraColor = new(0.035f, 0.047f, 0.075f, 1f);

        [MenuItem("Baryonyx/App/Create Home Scene")]
        public static void CreateHomeScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

            HomeScreenAssets.CreateAssets();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HomeScreenAssets.PrefabPath);
            var data = AssetDatabase.LoadAssetAtPath<HomeMockData>(HomeScreenAssets.DataPath);
            if (prefab == null || data == null)
                throw new InvalidOperationException("Home screen assets were not generated.");

            var loaded = SceneManager.GetSceneByPath(HomeScenePath);
            if (loaded.IsValid() && loaded.isLoaded)
                throw new InvalidOperationException(
                    "Close Home.unity before rebuilding it, so open edits are not overwritten."
                );

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive
            );
            try
            {
                var cameraObject = new GameObject("HomeCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = CameraColor;
                camera.orthographic = true;
                camera.tag = "MainCamera";

                var screen = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                screen.name = "HomeScreen";

                var bootstrapObject = new GameObject("HomeBootstrap", typeof(HomeBootstrap));
                SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
                var bootstrap = bootstrapObject.GetComponent<HomeBootstrap>();

                var events = new GameObject("EventSystem", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(events, scene);
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

                var transition = SceneTransitionSetup.AddTransition(
                    scene,
                    startCovered: true,
                    revealOnStart: true
                );

                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("view").objectReferenceValue =
                    screen.GetComponent<HomeView>();
                serialized.FindProperty("data").objectReferenceValue = data;
                serialized.FindProperty("transition").objectReferenceValue = transition;
                serialized.FindProperty("adventureSceneName").stringValue = "";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.SaveScene(scene, HomeScenePath);
            }
            finally
            {
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
