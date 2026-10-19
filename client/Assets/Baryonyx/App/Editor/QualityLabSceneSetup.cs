using Baryonyx.Editor;
using Baryonyx.Inventory;
using Baryonyx.Inventory.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.UI.Editor;
using UnityEditor;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds QualityLab.unity, the debugging room for quality: the lab's screen
    /// (<see cref="QualityLabAssets"/>) alone, with a camera and the event system. Home's button
    /// opens it behind the shutter, and its own button goes back.
    /// </summary>
    public static class QualityLabSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/QualityLab.unity";

        [MenuItem("Baryonyx/App/Create Quality Lab Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();
            QualityLabAssets.CreateAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    ScreenScenes.AddCamera(scene, "Camera", ScreenScenes.CameraColor);
                    var screen = ScreenScenes.AddScreen(
                        scene,
                        QualityLabAssets.PrefabPath,
                        "QualityLabScreen"
                    );
                    ScreenScenes.AddEventSystem(scene);
                    var lab = screen.GetComponent<QualityLab>();
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
