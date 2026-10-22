using Baryonyx.Adventure;
using Baryonyx.Adventure.Editor;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using UnityEditor;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds ExplorationMomentLab.unity, the debugging room for the turning points of the
    /// exploration: the exploration screen on its sample route, with no adventure behind it
    /// (no bootstrap), and the lab's controls (<see cref="ExplorationMomentLabAssets"/>) over it
    /// to play each moment with the screen's own parts (<see cref="ExplorationMomentLab"/>).
    /// </summary>
    public static class ExplorationMomentLabSceneSetup
    {
        public const string ScenePath =
            "Assets/Baryonyx/App/Scenes/Debug/ExplorationMomentLab.unity";

        [MenuItem("Baryonyx/App/Create Exploration Moment Lab Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();
            // The exploration screen is the adventure's; it is made here only if it is missing.
            if (
                AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
                    AdventureAssets.ExplorationPrefabPath
                ) == null
            )
                AdventureAssets.CreateAssets();
            ExplorationMomentLabAssets.CreateAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    ScreenScenes.AddCamera(scene, "ExplorationCamera", ScreenScenes.CameraColor);
                    var screen = ScreenScenes.AddScreen(
                        scene,
                        AdventureAssets.ExplorationPrefabPath,
                        "ExplorationScreen"
                    );
                    var panel = ScreenScenes.AddScreen(
                        scene,
                        ExplorationMomentLabAssets.PrefabPath,
                        "ExplorationMomentLabPanel"
                    );
                    var view = screen.GetComponent<ExplorationView>();
                    var lab = panel.GetComponent<ExplorationMomentLab>();
                    lab.View = view;
                    lab.Moments = view.Moments;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(lab);
                    ScreenScenes.AddEventSystem(scene);
                }
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ScreenScenes.AddToBuildSettings(ScenePath);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
