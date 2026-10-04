using System;
using Baryonyx.Combat.Editor;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.Stages.Editor;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds CardSkillLab.unity, the debugging room for the card skills' effects: the lab's
    /// screen (<see cref="CardSkillLabAssets"/>) on the battle's 3D stone circle on the highland at
    /// dusk, with the battle's camera, lens and grade, so an effect looks as it does in battle
    /// without the rest of BattleInspect.
    /// </summary>
    public static class CardSkillLabSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/CardSkillLab.unity";

        [MenuItem("Baryonyx/App/Create Card Skill Lab Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

            // The characters' art and materials and the dusk highland are the battle's; they are
            // made here only if they are missing, so building the lab leaves them as they are.
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
            CardSkillLabAssets.CreateAssets();
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
                        CardSkillLabAssets.PrefabPath,
                        "CardSkillLabScreen"
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
                    // The 3D camera shakes and pushes in with the stage.
                    stageCamera.UiOffsetSources = new[]
                    {
                        (RectTransform)screen.transform.Find("StageCanvas/Stage"),
                    };
                    Hd2dStageSceneSetup
                        .AddVolume(scene, "StageLookVolume", StageSetAssets.DuskHighlandLookPath)
                        .priority = 1f;
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
