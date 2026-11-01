using Baryonyx.Editor;
using Baryonyx.Inventory;
using Baryonyx.Inventory.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.UI.Editor;
using UnityEditor;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds InventoryLab.unity, the debugging room that adds things to the inventory: the lab's screen
    /// (<see cref="InventoryLabAssets"/>) alone, with a camera and the event system. Home's button
    /// opens it behind the shutter, and its own button goes back.
    /// </summary>
    public static class InventoryLabSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/InventoryLab.unity";

        [MenuItem("Baryonyx/App/Create Inventory Lab Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();
            InventoryLabAssets.CreateAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    ScreenScenes.AddCamera(scene, "Camera", ScreenScenes.CameraColor);
                    var screen = ScreenScenes.AddScreen(
                        scene,
                        InventoryLabAssets.PrefabPath,
                        "InventoryLabScreen"
                    );
                    ScreenScenes.AddEventSystem(scene);
                    var lab = screen.GetComponent<InventoryLab>();
                    lab.Transition = SceneTransitionAssets.AddTransition(
                        scene,
                        startCovered: true,
                        revealOnStart: true
                    );
                    lab.HomeSceneName = SceneNames.Home;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(lab);
                }
            );

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // The showcase and home load scenes through SceneManager, which only finds scenes in the build.
            ScreenScenes.AddToBuildSettings(ScenePath);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
