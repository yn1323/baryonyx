using System;
using Baryonyx.Editor;
using Baryonyx.Home;
using Baryonyx.Home.Editor;
using Baryonyx.Showcase.Editor;
using UnityEditor;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds Home.unity as the camp home mock: the generated home prefab, a bootstrap
    /// that feeds it fixed sample data, the shared shutter transition, and input.
    /// </summary>
    public static class HomeSceneSetup
    {
        public const string HomeScenePath = TopHomeSceneSetup.HomeScenePath;

        [MenuItem("Baryonyx/App/Create Home Scene")]
        public static void CreateHomeScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

            HomeScreenAssets.CreateAssets();
            var data = AssetDatabase.LoadAssetAtPath<HomeMockData>(HomeScreenAssets.DataPath);
            if (data == null)
                throw new InvalidOperationException("Home screen assets were not generated.");

            ScreenScenes.Rebuild(
                HomeScenePath,
                scene =>
                {
                    ScreenScenes.AddCamera(scene, "HomeCamera", ScreenScenes.CameraColor);
                    var screen = ScreenScenes.AddScreen(
                        scene,
                        HomeScreenAssets.PrefabPath,
                        "HomeScreen"
                    );
                    var bootstrap = ScreenScenes.AddObject<HomeBootstrap>(scene, "HomeBootstrap");
                    ScreenScenes.AddEventSystem(scene);
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
                    serialized.FindProperty("adventureSceneName").stringValue =
                        SceneNames.BattleInspect;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    TopStartupSyncSetup.SetHomeSettings(bootstrap);
                }
            );

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
