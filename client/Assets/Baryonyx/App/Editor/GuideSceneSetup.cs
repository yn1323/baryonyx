using System;
using System.Linq;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.Tavern.Editor;
using Baryonyx.Temple.Editor;
using Baryonyx.TravelOffice.Editor;
using Baryonyx.UI.GuideMenu;
using Baryonyx.UI.GuideMenu.Editor;
using Baryonyx.Workshop.Editor;
using UnityEditor;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds the guide screen scenes opened from the Home buttons: each has the feature's
    /// screen prefab, a bootstrap that returns to Home, the shared shutter and input.
    /// </summary>
    public static class GuideSceneSetup
    {
        public const string ScenesFolder = "Assets/Baryonyx/App/Scenes/Guide";

        // Scene name → the feature prefab it shows. Home opens these scenes by name.
        private static readonly (string Scene, string Prefab, Action Create)[] Screens =
        {
            (SceneNames.Pub, TavernScreenAssets.PrefabPath, TavernScreenAssets.CreateAssets),
            (SceneNames.Shop, WorkshopScreenAssets.PrefabPath, WorkshopScreenAssets.CreateAssets),
            (SceneNames.Temple, TempleScreenAssets.PrefabPath, TempleScreenAssets.CreateAssets),
            (
                SceneNames.TravelOffice,
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
            ScreenScenes.AddToBuildSettings(
                Screens.Select(screen => ScenePath(screen.Scene)).ToArray()
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }

        private static void CreateScene(string sceneName, string prefabPath) =>
            ScreenScenes.Rebuild(
                ScenePath(sceneName),
                scene =>
                {
                    ScreenScenes.AddCamera(scene, sceneName + "Camera", ScreenScenes.CameraColor);
                    var screen = ScreenScenes.AddScreen(scene, prefabPath, sceneName + "Screen");
                    var bootstrap = ScreenScenes.AddObject<GuideSceneBootstrap>(
                        scene,
                        sceneName + "Bootstrap"
                    );
                    ScreenScenes.AddEventSystem(scene);
                    var transition = SceneTransitionSetup.AddTransition(
                        scene,
                        startCovered: true,
                        revealOnStart: true
                    );

                    var serialized = new SerializedObject(bootstrap);
                    serialized.FindProperty("view").objectReferenceValue =
                        screen.GetComponent<GuideMenuView>();
                    serialized.FindProperty("transition").objectReferenceValue = transition;
                    serialized.FindProperty("homeSceneName").stringValue = SceneNames.Home;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            );
    }
}
