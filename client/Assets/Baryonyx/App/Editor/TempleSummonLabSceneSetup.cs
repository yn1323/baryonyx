using Baryonyx.Combat.Editor;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.Temple;
using Baryonyx.Temple.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds TempleSummonLab.unity, the debugging room for the summoning's presentation: the
    /// summoning-once presentation (<see cref="TempleSummonShowAssets"/>) drawn by a camera with
    /// the battle's Bloom, so the light spreads as it will on the summoning screen, and the lab's
    /// controls (<see cref="TempleSummonLabAssets"/>) over it (<see cref="TempleSummonLab"/>).
    /// </summary>
    public static class TempleSummonLabSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/TempleSummonLab.unity";

        [MenuItem("Baryonyx/App/Create Temple Summon Lab Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();
            TempleSummonShowAssets.CreateAssets();
            TempleSummonTenShowAssets.CreateAssets();
            TempleSummonLabAssets.CreateAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    var camera = ScreenScenes.AddCamera(scene, "SummonCamera", Color.black);
                    camera.allowHDR = true;
                    camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                    Hd2dStageSceneSetup.AddVolume(
                        scene,
                        "SummonPostProcessVolume",
                        BattleSkillVfxAssets.PostProcessProfilePath
                    );
                    // 光をBloomでにじませるため、演出はカメラで描く。
                    GameObject Add(string path, string name)
                    {
                        var added = ScreenScenes.AddScreen(scene, path, name);
                        var canvas = added.GetComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = 10f;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
                        return added;
                    }
                    var show = Add(TempleSummonShowAssets.PrefabPath, "TempleSummonShow");
                    var tenShow = Add(TempleSummonTenShowAssets.PrefabPath, "TempleSummonTenShow");
                    tenShow.SetActive(false);
                    var panel = ScreenScenes.AddScreen(
                        scene,
                        TempleSummonLabAssets.PrefabPath,
                        "TempleSummonLabPanel"
                    );
                    var lab = panel.GetComponent<TempleSummonLab>();
                    lab.Show = show.GetComponent<TempleSummonShow>();
                    lab.TenShow = tenShow.GetComponent<TempleSummonTenShow>();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(lab);
                    ScreenScenes.AddEventSystem(scene);
                }
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ScreenScenes.AddToBuildSettings(ScenePath);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
