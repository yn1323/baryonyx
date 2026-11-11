using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.Stages.Editor;
using Baryonyx.UI.Editor;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds VfxLookLab.unity, the debugging room for the look the effects are built on: the
    /// lab's screen (<see cref="VfxLookLabAssets"/>) on the battle's 3D stone circle on the
    /// highland at dusk, with the battle's camera, bloom, lens and grade, so a look is judged as
    /// it would show in battle. Home's button opens it, and its own button goes back.
    /// </summary>
    public static class VfxLookLabSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/VfxLookLab.unity";

        [MenuItem("Baryonyx/App/Create Vfx Look Lab Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();

            // The enemy's art and material and the dusk highland are the battle's; they are made
            // here only if they are missing, so building the lab leaves them as they are.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BattleInspectAssets.PrefabPath) == null)
                BattleInspectAssets.CreateAssets();
            if (
                AssetDatabase.LoadAssetAtPath<GameObject>(StageSetAssets.DuskHighlandPrefabPath)
                    == null
                || AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(
                    StageSetAssets.DuskHighlandLookPath
                ) == null
            )
                StageSetAssets.EnsureAssets();
            VfxLookLabAssets.CreateAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    var camera = ScreenScenes.AddCamera(
                        scene,
                        "BattleCamera",
                        ScreenScenes.CameraColor
                    );
                    var screen = ScreenScenes.AddScreen(
                        scene,
                        VfxLookLabAssets.PrefabPath,
                        "VfxLookLabScreen"
                    );
                    var stageCamera = Hd2dStageSceneSetup.Apply(
                        scene,
                        camera,
                        StageSetAssets.DuskHighlandPrefabPath,
                        StageSetAssets.BattleView,
                        StageSetAssets.DuskEnvironment
                    );
                    // Drawn by the scene's camera already outside Play Mode, as on BattleInspect.
                    var stageCanvas = screen.transform.Find("StageCanvas").GetComponent<Canvas>();
                    stageCanvas.worldCamera = camera;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(stageCanvas);
                    stageCamera.UiOffsetSources = new[]
                    {
                        (RectTransform)screen.transform.Find("StageCanvas/Stage"),
                    };
                    Hd2dStageSceneSetup
                        .AddVolume(scene, "StageLookVolume", StageSetAssets.DuskHighlandLookPath)
                        .priority = 1f;
                    ScreenScenes.AddEventSystem(scene);
                    // Opened from home's button, it comes in behind the shutter and goes back
                    // the same way.
                    var lab = screen.GetComponent<VfxLookLab>();
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
            // The showcase loads scenes through SceneManager, which only finds scenes in the build.
            ScreenScenes.AddToBuildSettings(ScenePath);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
