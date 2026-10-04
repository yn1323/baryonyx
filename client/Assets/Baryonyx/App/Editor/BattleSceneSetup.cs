using System;
using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.Stages.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds Battle.unity: the battle screen on its own like BattleInspect, with every battle
    /// background (<see cref="BattleStage"/>) in the scene and a <see cref="BattleStageSelector"/>
    /// to choose which one the battle is fought on, in the Inspector. The stages share the
    /// battle's camera, so the characters stand where they do on the dusk highland.
    /// </summary>
    public static class BattleSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Battle.unity";
        public const string StagesName = "BattleStages";

        /// <summary>The background the scene opens with.</summary>
        public const BattleStage FirstStage = BattleStage.MeadowRoad;

        [MenuItem("Baryonyx/App/Create Battle Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

            // The screen and the dusk highland are BattleInspect's; they are made here only if
            // they are missing, so building this scene leaves them as they are.
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
            BattleStageSets.EnsureAssets();
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
                        BattleInspectAssets.PrefabPath,
                        "BattleInspectScreen"
                    );
                    // The camera and the dusk highland as on BattleInspect; the other stages
                    // join it under the selector.
                    var stageCamera = Hd2dStageSceneSetup.Apply(
                        scene,
                        camera,
                        StageSetAssets.DuskHighlandPrefabPath,
                        StageSetAssets.BattleView,
                        StageSetAssets.DuskEnvironment
                    );
                    var stageCanvas = screen.transform.Find("StageCanvas").GetComponent<Canvas>();
                    stageCanvas.worldCamera = camera;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(stageCanvas);
                    var drift = (RectTransform)screen.transform.Find("StageCanvas/StageDrift");
                    stageCamera.UiOffsetSources = new[]
                    {
                        drift,
                        (RectTransform)drift.Find("Stage"),
                    };
                    var lookVolume = Hd2dStageSceneSetup.AddVolume(
                        scene,
                        "StageLookVolume",
                        StageSetAssets.DuskHighlandLookPath
                    );
                    lookVolume.priority = 1f;

                    var selector = ScreenScenes.AddObject<BattleStageSelector>(scene, StagesName);
                    selector.StageCamera = stageCamera;
                    selector.LookVolume = lookVolume;
                    var stages = (BattleStage[])Enum.GetValues(typeof(BattleStage));
                    var options = new BattleStageOption[stages.Length];
                    for (int i = 0; i < stages.Length; i++)
                        options[i] = AddStage(scene, selector.transform, stages[i]);
                    selector.Options = options;
                    selector.Stage = FirstStage;

                    var previous = SceneManager.GetActiveScene();
                    SceneManager.SetActiveScene(scene);
                    selector.Apply();
                    SceneManager.SetActiveScene(previous);
                    ScreenScenes.AddEventSystem(scene);
                }
            );

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // The showcase loads scenes through SceneManager, which only finds scenes in the build.
            ScreenScenes.AddToBuildSettings(ScenePath);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }

        /// <summary>The prefab, look and air of a background.</summary>
        public static (string prefab, string look, StageSetAssets.StageEnvironment air) Source(
            BattleStage stage
        )
        {
            if (stage == BattleStage.DuskHighland)
                return (
                    StageSetAssets.DuskHighlandPrefabPath,
                    StageSetAssets.DuskHighlandLookPath,
                    StageSetAssets.DuskEnvironment
                );
            var set =
                BattleStageSets.Find(stage.ToString())
                ?? throw new InvalidOperationException("No battle stage is built for " + stage);
            return (set.PrefabPath, set.LookPath, set.Environment);
        }

        private static BattleStageOption AddStage(Scene scene, Transform parent, BattleStage stage)
        {
            var (prefabPath, lookPath, air) = Source(stage);
            // The dusk highland was put in by the camera's setup already.
            GameObject root = null;
            if (stage == BattleStage.DuskHighland)
                foreach (var candidate in scene.GetRootGameObjects())
                    if (candidate.name == Hd2dStageSceneSetup.StageName)
                        root = candidate;
            if (root == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    throw new InvalidOperationException(
                        "3D stage was not generated: " + prefabPath
                    );
                root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            }
            root.name = stage.ToString();
            root.transform.SetParent(parent, false);
            var key = root.transform.Find(StageSetAssets.KeyLightName);
            return new BattleStageOption
            {
                Stage = stage,
                Root = root,
                KeyLight = key != null ? key.GetComponent<Light>() : null,
                Look = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(lookPath),
                AmbientSky = air.Ambient,
                AmbientEquator = air.Equator ?? air.Ambient,
                AmbientGround = air.Ground ?? air.Ambient,
                Fog = air.Fog,
                FogStart = air.FogStart,
                FogEnd = air.FogEnd,
            };
        }
    }
}
