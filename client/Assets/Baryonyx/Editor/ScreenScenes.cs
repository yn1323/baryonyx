using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Baryonyx.Editor
{
    // 画面のシーンをコードで作り直す生成スクリプトの共通部品。カメラと入力を置き、
    // 画面のPrefabと起動処理は呼び出し側が置く。
    public static class ScreenScenes
    {
        public static readonly Color CameraColor = new(0.035f, 0.047f, 0.075f, 1f);

        // 空のシーンを追加で開いて中身を作り、保存して閉じる。
        // 開いているシーンは、編集中の変更を消さないよう作り直さない。
        public static void Rebuild(string path, Action<Scene> populate)
        {
            EditorGuard.RequireEditMode();
            var loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded)
                throw new InvalidOperationException(
                    $"Close {Path.GetFileName(path)} before rebuilding it, so open edits are not overwritten."
                );

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive
            );
            try
            {
                populate(scene);
                EditorSceneManager.SaveScene(scene, path);
            }
            finally
            {
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static Camera AddCamera(
            Scene scene,
            string name,
            Color background,
            bool orthographic = true
        )
        {
            var cameraObject = new GameObject(name, typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.orthographic = orthographic;
            camera.tag = "MainCamera";
            return camera;
        }

        public static GameObject AddScreen(Scene scene, string prefabPath, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException(
                    "Screen prefab was not generated: " + prefabPath
                );
            var screen = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            screen.name = name;
            return screen;
        }

        public static T AddObject<T>(Scene scene, string name)
            where T : Component
        {
            var created = new GameObject(name, typeof(T));
            SceneManager.MoveGameObjectToScene(created, scene);
            return created.GetComponent<T>();
        }

        public static void AddEventSystem(Scene scene)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem));
            SceneManager.MoveGameObjectToScene(events, scene);
            events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // 展示室やほかのシーンからSceneManagerで開けるよう、ないシーンだけを末尾に足す。
        // 既存のシーンの順番と有効・無効は変えない。
        public static void AddToBuildSettings(params string[] paths)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var path in paths)
                if (scenes.All(scene => scene.path != path))
                    scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
