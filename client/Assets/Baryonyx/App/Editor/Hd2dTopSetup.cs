using System;
using System.IO;
using Baryonyx.Showcase.Editor;
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
                TopHomeSceneSetup.EnsureTopLightShaft,
                TopHomeSceneSetup.EnsureTopFog,
                TopHomeSceneSetup.EnsureTopFlickerLight,
                TopHomeSceneSetup.EnsureTopEmberEmitter,
                TopHomeSceneSetup.EnsureTopPostProcess
            );

        [MenuItem("Baryonyx/VFX/Create HD-2D Light Shaft and Apply to Top")]
        public static void CreateLightShaftAndIntegrateTop() =>
            ApplyToTop(TopHomeSceneSetup.EnsureTopLightShaft);

        [MenuItem("Baryonyx/VFX/Create HD-2D Fog and Apply to Top")]
        public static void CreateFogAndIntegrateTop() => ApplyToTop(TopHomeSceneSetup.EnsureTopFog);

        [MenuItem("Baryonyx/VFX/Create HD-2D Flicker Light and Apply to Top")]
        public static void CreateFlickerLightAndIntegrateTop() =>
            ApplyToTop(TopHomeSceneSetup.EnsureTopFlickerLight);

        // ティルトシフトは共通のProfileとRendererにあるため、Topにはポストプロセスの設定だけを置く。
        [MenuItem("Baryonyx/VFX/Create HD-2D Tilt Shift and Apply to Top")]
        public static void CreateTiltShift() => CreatePostProcessAndIntegrateTop();

        [MenuItem("Baryonyx/VFX/Create HD-2D Post Process and Apply to Top")]
        public static void CreatePostProcessAndIntegrateTop() =>
            ApplyToTop(TopHomeSceneSetup.EnsureTopPostProcess);

        [MenuItem("Baryonyx/VFX/Create HD-2D Ember Emitter and Apply to Top")]
        public static void CreateEmberEmitterAndIntegrateTop() =>
            ApplyToTop(TopHomeSceneSetup.EnsureTopEmberEmitter);

        // 共通アセットを作り、Topがあれば各演出を置いて、変わったときだけ保存する。
        private static void ApplyToTop(params Func<Scene, bool>[] steps)
        {
            Hd2dAssets.EnsureAssets();
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
