using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.UI.Editor;
using Baryonyx.WorldMap;
using Baryonyx.WorldMap.Editor;
using UnityEditor;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds the world map scene (the highway prototype): the screen prefab, the bootstrap
    /// that returns to Home or opens the list of dungeons, the shared shutter and input.
    /// </summary>
    public static class WorldMapSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/WorldMap.unity";

        [MenuItem("Baryonyx/App/Create World Map Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();
            WorldMapScreenAssets.CreateAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    ScreenScenes.AddCamera(scene, "WorldMapCamera", ScreenScenes.CameraColor);
                    var screen = ScreenScenes.AddScreen(
                        scene,
                        WorldMapScreenAssets.PrefabPath,
                        "WorldMapScreen"
                    );
                    var bootstrap = ScreenScenes.AddObject<WorldMapBootstrap>(
                        scene,
                        "WorldMapBootstrap"
                    );
                    ScreenScenes.AddEventSystem(scene);
                    // 地図を今いるマスへ置くまで覆い、開くのはWorldMapBootstrapに任せる。
                    var transition = SceneTransitionAssets.AddTransition(
                        scene,
                        startCovered: true,
                        revealOnStart: false
                    );
                    SceneTransitionAssets.ConfigureWorldMap(transition);
                    var serialized = new SerializedObject(bootstrap);
                    serialized.FindProperty("view").objectReferenceValue =
                        screen.GetComponent<WorldMapView>();
                    serialized.FindProperty("transition").objectReferenceValue = transition;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            );
            ScreenScenes.AddToBuildSettings(ScenePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
