using System;
using Baryonyx.Editor;
using Baryonyx.Showcase;
using UnityEditor;
using UnityEditor.SceneManagement;

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
                throw new InvalidOperationException(
                    "Showcase scene already exists. Use Open Scene."
                );

            ShowcaseCatalogBuilder.RefreshCatalog();
            var catalog = AssetDatabase.LoadAssetAtPath<ShowcaseCatalog>(
                ShowcaseCatalogBuilder.CatalogPath
            );
            ScreenScenes.Rebuild(
                ShowcaseCatalogBuilder.ScenePath,
                scene =>
                {
                    ScreenScenes.AddObject<ShowcaseBootstrap>(scene, "ShowcaseApp").Catalog =
                        catalog;
                    ScreenScenes.AddCamera(
                        scene,
                        "ShowcaseCamera",
                        ScreenScenes.CameraColor,
                        orthographic: false
                    );
                    ScreenScenes.AddEventSystem(scene);
                }
            );
            ScreenScenes.AddToBuildSettings(ShowcaseCatalogBuilder.ScenePath);
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
            if (System.IO.File.Exists(ShowcaseCatalogBuilder.ScenePath))
                ScreenScenes.AddToBuildSettings(ShowcaseCatalogBuilder.ScenePath);
        }
    }
}
