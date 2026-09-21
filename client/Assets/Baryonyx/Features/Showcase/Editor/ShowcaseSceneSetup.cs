using System;
using System.Linq;
using Baryonyx.Showcase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Baryonyx.Showcase.Editor
{
    public static class ShowcaseSceneSetup
    {
        [MenuItem("Baryonyx/Showcase/Create Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            if (System.IO.File.Exists(ShowcaseCatalogBuilder.ScenePath))
                throw new InvalidOperationException("Showcase scene already exists. Use Open Scene.");

            ShowcaseCatalogBuilder.RefreshCatalog();
            var catalog = AssetDatabase.LoadAssetAtPath<ShowcaseCatalog>(ShowcaseCatalogBuilder.CatalogPath);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject("ShowcaseApp");
                SceneManager.MoveGameObjectToScene(root, scene);
                var app = root.AddComponent<ShowcaseBootstrap>();
                app.Catalog = catalog;
                var cameraObject = new GameObject("ShowcaseCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.047f, 0.075f, 1f);
                camera.tag = "MainCamera";
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var events = new GameObject("EventSystem", typeof(EventSystem));
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
                SceneManager.MoveGameObjectToScene(events, scene);
                EditorSceneManager.SaveScene(scene, ShowcaseCatalogBuilder.ScenePath);
                AddToBuildSettings(ShowcaseCatalogBuilder.ScenePath);
            }
            finally
            {
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("Baryonyx/Showcase/Open Scene")]
        public static void OpenScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            EditorSceneManager.OpenScene(ShowcaseCatalogBuilder.ScenePath);
        }

        [MenuItem("Baryonyx/Showcase/Refresh Catalog and Build Settings")]
        public static void RefreshAll()
        {
            ShowcaseCatalogBuilder.RefreshCatalog();
            AddToBuildSettings(ShowcaseCatalogBuilder.ScenePath);
        }

        private static void AddToBuildSettings(string scenePath)
        {
            if (!System.IO.File.Exists(scenePath))
                return;
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(scene => scene.path == scenePath))
                return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
