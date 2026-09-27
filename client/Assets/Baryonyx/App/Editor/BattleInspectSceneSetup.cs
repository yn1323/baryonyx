using System;
using Baryonyx.Combat.Editor;
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
    /// Rebuilds BattleInspect.unity: the mock card battle screen on its own, with a camera and
    /// input, so the battle look can be checked without playing through Top and Home.
    /// </summary>
    public static class BattleInspectSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/BattleInspect.unity";
        private static readonly Color CameraColor = new(0.035f, 0.047f, 0.075f, 1f);

        [MenuItem("Baryonyx/App/Create Battle Inspect Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

            BattleInspectAssets.CreateAssets();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleInspectAssets.PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Battle inspect screen was not generated.");

            var loaded = SceneManager.GetSceneByPath(ScenePath);
            if (loaded.IsValid() && loaded.isLoaded)
                throw new InvalidOperationException(
                    "Close BattleInspect.unity before rebuilding it, so open edits are not overwritten."
                );

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive
            );
            try
            {
                var cameraObject = new GameObject("BattleCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = CameraColor;
                camera.orthographic = true;
                camera.tag = "MainCamera";

                var screen = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                screen.name = "BattleInspectScreen";

                var events = new GameObject("EventSystem", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(events, scene);
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AddToBuildSettings();
            ShowcaseCatalogBuilder.RefreshCatalog();
        }

        // The showcase loads scenes through SceneManager, which only finds scenes in the build.
        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            if (Array.Exists(scenes, scene => scene.path == ScenePath))
                return;
            Array.Resize(ref scenes, scenes.Length + 1);
            scenes[^1] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = scenes;
        }
    }
}
