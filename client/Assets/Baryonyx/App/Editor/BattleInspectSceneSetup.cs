using System;
using Baryonyx.Combat.Editor;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using UnityEditor;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds BattleInspect.unity: the mock card battle screen on its own, with a camera and
    /// input, so the battle look can be checked without playing through Top and Home.
    /// </summary>
    public static class BattleInspectSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/BattleInspect.unity";

        [MenuItem("Baryonyx/App/Create Battle Inspect Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

            BattleInspectAssets.CreateAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    ScreenScenes.AddCamera(scene, "BattleCamera", ScreenScenes.CameraColor);
                    ScreenScenes.AddScreen(
                        scene,
                        BattleInspectAssets.PrefabPath,
                        "BattleInspectScreen"
                    );
                    ScreenScenes.AddEventSystem(scene);
                }
            );

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // The showcase loads scenes through SceneManager, which only finds scenes in the build.
            ScreenScenes.AddToBuildSettings(ScenePath);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
