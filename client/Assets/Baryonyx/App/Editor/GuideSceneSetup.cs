using System;
using System.Linq;
using Baryonyx.Editor;
using Baryonyx.Library.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.Tavern.Editor;
using Baryonyx.Temple.Editor;
using Baryonyx.TravelOffice.Editor;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using Baryonyx.UI.GuideMenu.Editor;
using Baryonyx.Workshop.Editor;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds the guide screen scenes opened from Home: each has the feature's screen prefab,
    /// a bootstrap that returns to Home (and, on the travel office, sets out on the adventure),
    /// the shared shutter and input.
    /// </summary>
    public static class GuideSceneSetup
    {
        public const string ScenesFolder = "Assets/Baryonyx/App/Scenes/Guide";

        // Scene name → the feature prefab it shows. Home opens these scenes by name.
        private static readonly (string Scene, string Prefab, Action Create)[] Screens =
        {
            (SceneNames.Pub, TavernScreenAssets.PrefabPath, TavernScreenAssets.CreateAssets),
            (SceneNames.Shop, WorkshopScreenAssets.PrefabPath, WorkshopScreenAssets.CreateAssets),
            (SceneNames.Temple, TempleScreenAssets.PrefabPath, TempleScreenAssets.CreateAssets),
            (
                SceneNames.TravelOffice,
                TravelOfficeScreenAssets.PrefabPath,
                TravelOfficeScreenAssets.CreateAssets
            ),
            (SceneNames.Library, LibraryScreenAssets.PrefabPath, LibraryScreenAssets.CreateAssets),
        };

        public static string ScenePath(string scene) => $"{ScenesFolder}/{scene}.unity";

        [MenuItem("Baryonyx/App/Create Guide Scenes")]
        public static void CreateGuideScenes()
        {
            EditorGuard.RequireEditMode();
            GuideMenuAssets.CreateSharedArt();
            foreach (var screen in Screens)
            {
                screen.Create();
                CreateScene(screen.Scene, screen.Prefab);
            }
            ScreenScenes.AddToBuildSettings(
                Screens.Select(screen => ScenePath(screen.Scene)).ToArray()
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }

        // 神殿の召喚の演出（1回・10回）。光をBloomでにじませるため、カメラで描き、戦闘と同じポストプロセスを掛ける。
        // 案内人の画面（Overlay）の奥に隠れるため、召喚の画面が演出の間だけ案内人の画面を隠して出す。
        private static void AddSummonShows(
            UnityEngine.SceneManagement.Scene scene,
            UnityEngine.Camera camera
        )
        {
            camera.allowHDR = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            Hd2dStageSceneSetup.AddVolume(
                scene,
                "SummonPostProcessVolume",
                Baryonyx.Combat.Editor.BattleSkillVfxAssets.PostProcessProfilePath
            );
            foreach (
                var (path, name) in new[]
                {
                    (TempleSummonShowAssets.PrefabPath, "TempleSummonShow"),
                    (TempleSummonTenShowAssets.PrefabPath, "TempleSummonTenShow"),
                }
            )
            {
                var show = ScreenScenes.AddScreen(scene, path, name);
                var canvas = show.GetComponent<UnityEngine.Canvas>();
                canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
                show.SetActive(false);
            }
        }

        public static void CreateScene(string sceneName, string prefabPath) =>
            ScreenScenes.Rebuild(
                ScenePath(sceneName),
                scene =>
                {
                    var camera = ScreenScenes.AddCamera(
                        scene,
                        sceneName + "Camera",
                        ScreenScenes.CameraColor
                    );
                    if (sceneName == SceneNames.Temple)
                        AddSummonShows(scene, camera);
                    var screen = ScreenScenes.AddScreen(scene, prefabPath, sceneName + "Screen");
                    var bootstrap = ScreenScenes.AddObject<GuideSceneBootstrap>(
                        scene,
                        sceneName + "Bootstrap"
                    );
                    ScreenScenes.AddEventSystem(scene);
                    var transition = SceneTransitionAssets.AddTransition(
                        scene,
                        startCovered: true,
                        revealOnStart: true
                    );

                    var serialized = new SerializedObject(bootstrap);
                    serialized.FindProperty("view").objectReferenceValue =
                        screen.GetComponent<GuideMenuView>();
                    serialized.FindProperty("transition").objectReferenceValue = transition;
                    serialized.FindProperty("homeSceneName").stringValue = SceneNames.Home;
                    // 旅の案内所は世界地図のダンジョン一覧として開くため、「もどる」で世界地図へ戻る。
                    serialized.FindProperty("backSceneName").stringValue =
                        sceneName == SceneNames.TravelOffice ? SceneNames.WorldMap : "";
                    // The travel office's departure sets out on the adventure.
                    serialized.FindProperty("departs").boolValue =
                        sceneName == SceneNames.TravelOffice;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            );
    }
}
