using System;
using Baryonyx.Editor;
using Baryonyx.Home;
using Baryonyx.Home.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.Stages.Editor;
using Baryonyx.Vfx.Hd2d.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds Home.unity as the camp home mock: the generated home prefab, a bootstrap
    /// that feeds it fixed sample data, the shared shutter transition, and input. The camp
    /// stands in a 3D forest glade in the daytime (HD-2D), in dappled sunlight, with the
    /// campfire's small warm light.
    /// </summary>
    public static class HomeSceneSetup
    {
        public const string HomeScenePath = TopHomeSceneSetup.HomeScenePath;

        [MenuItem("Baryonyx/App/Create Home Scene")]
        public static void CreateHomeScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

            HomeScreenAssets.CreateAssets();
            StageSetAssets.EnsureAssets();
            var data = AssetDatabase.LoadAssetAtPath<HomeMockData>(HomeScreenAssets.DataPath);
            if (data == null)
                throw new InvalidOperationException("Home screen assets were not generated.");

            ScreenScenes.Rebuild(
                HomeScenePath,
                scene =>
                {
                    var camera = ScreenScenes.AddCamera(
                        scene,
                        "HomeCamera",
                        ScreenScenes.CameraColor
                    );
                    var screen = ScreenScenes.AddScreen(
                        scene,
                        HomeScreenAssets.PrefabPath,
                        "HomeScreen"
                    );
                    var stageCamera = Hd2dStageSceneSetup.Apply(
                        scene,
                        camera,
                        StageSetAssets.ForestGladePrefabPath,
                        StageSetAssets.HomeView,
                        StageSetAssets.GladeEnvironment
                    );
                    // While the screen waits for a tap the camera drifts slowly, and it glides
                    // in when the camp opens.
                    stageCamera.SwayRadius = 6f;
                    stageCamera.SwayPeriod = 26f;
                    stageCamera.IntroOffset = new Vector3(0f, 0.35f, -0.9f);
                    stageCamera.IntroSeconds = 2.6f;
                    // The campfire's small warm light stands where the UI puts the fire; in the
                    // day the sun casts the shadows.
                    var fire = Hd2dStageKit.PointLight(
                        null,
                        "CampfireLight",
                        Hd2dStageSceneSetup.GroundUnder(
                            StageSetAssets.HomeView,
                            new Vector2(960f, 540f - HomeScreenAssets.CampfireFeetBelowCenter)
                        )
                            + Vector3.up * 0.6f,
                        new Color(1f, 0.6f, 0.28f),
                        1.6f,
                        4.5f,
                        flicker: 0.25f,
                        seed: 5f
                    );
                    SceneManager.MoveGameObjectToScene(fire.gameObject, scene);
                    Hd2dStageSceneSetup.AddVolume(
                        scene,
                        "HomePostProcessVolume",
                        StageSetAssets.ForestGladeLookPath
                    );
                    var bootstrap = ScreenScenes.AddObject<HomeBootstrap>(scene, "HomeBootstrap");
                    ScreenScenes.AddEventSystem(scene);
                    var transition = SceneTransitionSetup.AddTransition(
                        scene,
                        startCovered: true,
                        revealOnStart: true
                    );

                    var serialized = new SerializedObject(bootstrap);
                    serialized.FindProperty("view").objectReferenceValue =
                        screen.GetComponent<HomeView>();
                    serialized.FindProperty("data").objectReferenceValue = data;
                    serialized.FindProperty("transition").objectReferenceValue = transition;
                    serialized.FindProperty("adventureSceneName").stringValue =
                        SceneNames.BattleInspect;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    TopStartupSyncSetup.SetHomeSettings(bootstrap);
                }
            );

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
