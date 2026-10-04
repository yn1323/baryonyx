using System;
using System.IO;
using System.Linq;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.App.Editor
{
    public static class SceneTransitionSetup
    {
        private const string TopScenePath = TopHomeSceneSetup.TopScenePath;
        private const string HomeScenePath = TopHomeSceneSetup.HomeScenePath;

        [MenuItem("Baryonyx/App/Create Scene Transition Assets")]
        public static void CreateAssetsAndIntegrateTopHome()
        {
            EditorGuard.RequireEditMode();

            AssetFolders.Ensure(SceneTransitionAssets.Folder);
            SceneTransitionAssets.EnsurePrefab();
            AssetDatabase.ImportAsset(
                SceneTransitionAssets.PrefabPath,
                ImportAssetOptions.ForceSynchronousImport
            );
            // Topも覆った状態で開き、開き終わるまで起動直後のタップを遮る。
            AddToScene(TopScenePath, startCovered: true, revealOnStart: true, configureTop: true);
            AddToScene(HomeScenePath, startCovered: true, revealOnStart: true, configureTop: false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }

        private static void AddToScene(
            string scenePath,
            bool startCovered,
            bool revealOnStart,
            bool configureTop
        )
        {
            if (!File.Exists(scenePath))
                throw new InvalidOperationException($"Scene not found: {scenePath}");

            var loaded = SceneManager.GetSceneByPath(scenePath);
            var ownsScene = !loaded.IsValid() || !loaded.isLoaded;
            var scene = ownsScene
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive)
                : loaded;
            var wasDirty = scene.isDirty;
            try
            {
                var controller = SceneTransitionAssets.AddTransition(
                    scene,
                    startCovered,
                    revealOnStart
                );
                if (configureTop)
                {
                    var topController = scene
                        .GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<TopSceneController>(true))
                        .SingleOrDefault();
                    if (topController == null)
                        throw new InvalidOperationException(
                            "TopSceneController is missing from Top scene."
                        );

                    var serialized = new SerializedObject(topController);
                    serialized.FindProperty("nextSceneName").stringValue = SceneNames.Home;
                    serialized.FindProperty("transition").objectReferenceValue = controller;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(topController);
                }

                foreach (
                    var bootstrap in scene
                        .GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<HomeBootstrap>(true))
                )
                {
                    var serialized = new SerializedObject(bootstrap);
                    serialized.FindProperty("transition").objectReferenceValue = controller;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(bootstrap);
                }

                // 既に開いていてdirtyなシーンは、ユーザーの未保存調整を上書きしない。
                // その場合は変更を開いたまま残し、ユーザーが内容を確認して保存できるようにする。
                if (ownsScene || !wasDirty)
                    EditorSceneManager.SaveScene(scene, scenePath);
            }
            finally
            {
                if (ownsScene)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
