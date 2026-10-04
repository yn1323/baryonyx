using System;
using System.IO;
using Baryonyx.Showcase.Editor;
using Baryonyx.Stages.Editor;
using Baryonyx.Vfx.Hd2d.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Baryonyx.App.Editor
{
    // HD-2Dの演出の共通アセットを作り、Topのシーンへ置く。アセットの作成は Hd2dAssets が行う。
    public static class Hd2dTopSetup
    {
        private const string TopScenePath = TopHomeSceneSetup.TopScenePath;

        [MenuItem("Baryonyx/VFX/Create HD-2D Lighting VFX Assets and Apply to Top")]
        public static void CreateAssetsAndIntegrateTop() =>
            ApplyToTop(
                TopHomeSceneSetup.EnsureTopLightingVfx,
                TopHomeSceneSetup.EnsureTopPostProcess,
                TopHomeSceneSetup.EnsureTopStage
            );

        // The torches' light and embers on Top come from the 3D stage (its point lights, flames
        // and particles), so the 2D flicker light and ember emitter are no longer put on Top,
        // nor the light shaft from the dungeon's ceiling now that Top is outdoors. The 2D fog is
        // not put on Top either: the 3D stage's distance fog already hazes the far side.
        [MenuItem("Baryonyx/VFX/Create HD-2D Stage and Apply to Top")]
        public static void CreateStageAndIntegrateTop() =>
            ApplyToTop(TopHomeSceneSetup.EnsureTopPostProcess, TopHomeSceneSetup.EnsureTopStage);

        // ティルトシフトは共通のProfileとRendererにあるため、Topにはポストプロセスの設定だけを置く。
        [MenuItem("Baryonyx/VFX/Create HD-2D Tilt Shift and Apply to Top")]
        public static void CreateTiltShift() => CreatePostProcessAndIntegrateTop();

        [MenuItem("Baryonyx/VFX/Create HD-2D Post Process and Apply to Top")]
        public static void CreatePostProcessAndIntegrateTop() =>
            ApplyToTop(TopHomeSceneSetup.EnsureTopPostProcess);

        // 共通アセットを作り、Topがあれば各演出を置いて、変わったときだけ保存する。
        private static void ApplyToTop(params Func<Scene, bool>[] steps)
        {
            Hd2dAssets.EnsureAssets();
            StageSetAssets.EnsureAssets();
            if (File.Exists(TopScenePath))
            {
                var scene = EditorSceneManager.OpenScene(TopScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                    throw new InvalidOperationException(
                        $"Top scene could not be opened: {TopScenePath}"
                    );
                bool changed = false;
                foreach (var step in steps)
                    changed |= step(scene);
                if (changed)
                    EditorSceneManager.SaveScene(scene, TopScenePath);
            }
            ShowcaseCatalogBuilder.RefreshCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
