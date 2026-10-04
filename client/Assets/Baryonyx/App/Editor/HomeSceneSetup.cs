using System;
using Baryonyx.Editor;
using Baryonyx.Home;
using Baryonyx.Home.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.Stages.Editor;
using Baryonyx.UI.Editor;
using Baryonyx.Vfx.Hd2d;
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
            EditorGuard.RequireEditMode();

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
                    AddCampfire(scene);
                    Hd2dStageSceneSetup.AddVolume(
                        scene,
                        "HomePostProcessVolume",
                        StageSetAssets.ForestGladeLookPath
                    );
                    var bootstrap = ScreenScenes.AddObject<HomeBootstrap>(scene, "HomeBootstrap");
                    ScreenScenes.AddEventSystem(scene);
                    var transition = SceneTransitionAssets.AddTransition(
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
                        SceneNames.Exploration;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    TopStartupSyncSetup.SetHomeSettings(bootstrap);
                }
            );

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }

        // The campfire burns where the UI stands its logs: a computed flame rising out of the
        // logs, its embers and its small warm light. In the day the sun casts the shadows.
        private static void AddCampfire(Scene scene)
        {
            var ground = Hd2dStageSceneSetup.GroundUnder(
                StageSetAssets.HomeView,
                new Vector2(960f, 540f - HomeScreenAssets.CampfireFeetBelowCenter)
            );
            var campfire = Hd2dStageKit.Group(null, "Campfire", ground);
            SceneManager.MoveGameObjectToScene(campfire, scene);
            var parent = campfire.transform;
            // The root sits low between the logs and just behind their board, so the logs hide
            // the base of the fire and the flames rise out of them.
            var root = ground + new Vector3(0f, 0.05f, 0.06f);
            Hd2dStageKit.Flame(
                parent,
                "CampfireFlame",
                root,
                new Vector2(0.56f, 0.7f),
                AssetDatabase.LoadAssetAtPath<Material>(HomeScreenAssets.CampfireFlameMaterialPath)
            );
            Hd2dStageKit.Embers(
                parent,
                "CampfireEmbers",
                root + Vector3.up * 0.4f,
                AssetDatabase.LoadAssetAtPath<Material>(Hd2dStageKit.EmberMaterialPath),
                4f,
                0.1f,
                0.07f
            );
            Hd2dStageKit.PointLight(
                parent,
                "CampfireLight",
                ground + Vector3.up * 0.6f,
                new Color(1f, 0.6f, 0.28f),
                1.6f,
                4.5f,
                flicker: 0.25f,
                seed: 5f
            );
            // The embers rise while the scene is edited too, as the stages' own do.
            campfire.AddComponent<Hd2dParticlePreview>();
        }
    }
}
