using Baryonyx.Health;
using Baryonyx.Health.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Baryonyx.App.Editor
{
    public static class HealthAppSceneSetup
    {
        [MenuItem("Baryonyx/App/Attach Health Screen To Current Scene")]
        public static void AttachToCurrentScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.isDirty || EditorApplication.isPlaying)
                throw new System.InvalidOperationException(
                    "Save the scene and stop Play Mode first."
                );
            if (Object.FindAnyObjectByType<HealthScreenBootstrap>() != null)
                return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthScreenAssets.PrefabPath);
            var screen = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var app = new GameObject("HealthApp").AddComponent<HealthScreenBootstrap>();
            app.Screen = screen.GetComponent<HealthScreenView>();
            app.Settings = AssetDatabase.LoadAssetAtPath<HealthConnectionSettings>(
                HealthScreenAssets.SettingsPath
            );
            if (Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject(
                    "EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule)
                );
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
