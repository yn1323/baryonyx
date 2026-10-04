using System;
using Baryonyx.Combat.Editor;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.Stages.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds BattleInspect.unity: the mock card battle screen on its own, with a camera and
    /// input, so the battle look can be checked without playing through Top and Home. The
    /// battle takes place on the 3D stone circle on the highland at dusk (HD-2D); the camera
    /// follows the stage's slow circle and shake, so near and far part as it moves.
    /// </summary>
    public static class BattleInspectSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/BattleInspect.unity";

        [MenuItem("Baryonyx/App/Create Battle Inspect Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();

            BattleInspectAssets.CreateAssets();
            StageSetAssets.EnsureAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    AddBattle(scene);
                    ScreenScenes.AddEventSystem(scene);
                }
            );

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // The showcase loads scenes through SceneManager, which only finds scenes in the build.
            ScreenScenes.AddToBuildSettings(ScenePath);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }

        /// <summary>
        /// Puts the battle screen into <paramref name="scene"/> on the dusk highland, with the
        /// battle's camera and the highland's lens and grade, and returns the screen. The screen's
        /// prefab and the highland must be made already.
        /// </summary>
        public static GameObject AddBattle(Scene scene)
        {
            var camera = ScreenScenes.AddCamera(scene, "BattleCamera", ScreenScenes.CameraColor);
            var screen = ScreenScenes.AddScreen(
                scene,
                BattleInspectAssets.PrefabPath,
                "BattleInspectScreen"
            );
            var stageCamera = Hd2dStageSceneSetup.Apply(
                scene,
                camera,
                StageSetAssets.DuskHighlandPrefabPath,
                StageSetAssets.BattleView,
                StageSetAssets.DuskEnvironment
            );
            // The battlefield's canvas is drawn by the scene's camera already outside
            // Play Mode (BattleStageCamera keeps a camera that is set), so the stopped
            // scene and the Scene view show the bars and marks where the game does.
            var stageCanvas = screen.transform.Find("StageCanvas").GetComponent<Canvas>();
            stageCanvas.worldCamera = camera;
            PrefabUtility.RecordPrefabInstancePropertyModifications(stageCanvas);
            var drift = (RectTransform)screen.transform.Find("StageCanvas/StageDrift");
            stageCamera.UiOffsetSources = new[] { drift, (RectTransform)drift.Find("Stage") };
            // The highland's lens and grade over the battlefield's own bloom.
            Hd2dStageSceneSetup
                .AddVolume(scene, "StageLookVolume", StageSetAssets.DuskHighlandLookPath)
                .priority = 1f;
            return screen;
        }
    }
}
