using Baryonyx.Adventure;
using Baryonyx.Adventure.Editor;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.UI.Editor;
using UnityEditor;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds DropLab.unity, the drop editor: the editor's screen (<see cref="DropLabAssets"/>)
    /// alone, with a camera and the event system. Home's list of debugging rooms opens it behind
    /// the shutter, and its own button goes back.
    /// </summary>
    public static class DropLabSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/DropLab.unity";

        [MenuItem("Baryonyx/App/Create Drop Lab Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();
            DropLabAssets.CreateAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    ScreenScenes.AddCamera(scene, "Camera", ScreenScenes.CameraColor);
                    var screen = ScreenScenes.AddScreen(
                        scene,
                        DropLabAssets.PrefabPath,
                        "DropLabScreen"
                    );
                    ScreenScenes.AddEventSystem(scene);
                    var lab = screen.GetComponent<DropLab>();
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
