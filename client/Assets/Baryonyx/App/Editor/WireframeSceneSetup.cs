using System;
using System.IO;
using Baryonyx.Wireframe;
using Baryonyx.Wireframe.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Baryonyx.App.Editor
{
    public static class WireframeSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Wireframe.unity";

        [MenuItem("Baryonyx/Wireframe/Build Android APK")]
        public static void BuildAndroid()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Select the Android build target first.");
            if (File.Exists(ScenePath))
                WireframeScreenAssets.CreateAssets();
            else
                CreateScene();
            var previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(ScenePath, true),
                };
                Baryonyx.Editor.CI.AndroidBuild.Build();
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        [MenuItem("Baryonyx/Wireframe/Create Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            if (File.Exists(ScenePath))
                throw new InvalidOperationException(
                    "Wireframe scene already exists. Use Open Scene."
                );
            WireframeScreenAssets.CreateAssets();
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive
            );
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    WireframeScreenAssets.PrefabPath
                );
                var screen = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var app = new GameObject("WireframeApp").AddComponent<WireframeBootstrap>();
                SceneManager.MoveGameObjectToScene(app.gameObject, scene);
                app.View = screen.GetComponent<WireframeView>();
                app.Data = AssetDatabase.LoadAssetAtPath<WireframeData>(
                    WireframeScreenAssets.DataPath
                );
                var events = new GameObject(
                    "EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule)
                );
                SceneManager.MoveGameObjectToScene(events, scene);
                var camera = new GameObject("WireframeCamera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.93f, .94f, .95f);
                camera.tag = "MainCamera";
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("Baryonyx/Wireframe/Open Scene")]
        public static void OpenScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save your open scenes first.");
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
