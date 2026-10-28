using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.UI.Editor;
using Baryonyx.WorldMap;
using Baryonyx.WorldMap.Editor;
using UnityEditor;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds the adventure's world map scene: the nested world map's navigator (made by
    /// <see cref="WorldAtlasAssets"/>), the bootstrap that returns to Home or opens the list of
    /// dungeons, the shared shutter and input.
    /// </summary>
    public static class WorldSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/World.unity";

        [MenuItem("Baryonyx/App/Create World Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    ScreenScenes.AddCamera(scene, "WorldCamera", ScreenScenes.CameraColor);
                    var screen = ScreenScenes.AddScreen(
                        scene,
                        WorldAtlasAssets.NavigatorPath,
                        "WorldAtlas"
                    );
                    var bootstrap = ScreenScenes.AddObject<WorldBootstrap>(scene, "WorldBootstrap");
                    ScreenScenes.AddEventSystem(scene);
                    // 地図はResourcesからすぐに読めるため、開いたらそのまま幕を開ける。
                    var transition = SceneTransitionAssets.AddTransition(
                        scene,
                        startCovered: true,
                        revealOnStart: true
                    );
                    SceneTransitionAssets.ConfigureWorldMap(transition);
                    var serialized = new SerializedObject(bootstrap);
                    serialized.FindProperty("navigator").objectReferenceValue =
                        screen.GetComponent<WorldAtlasNavigator>();
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
