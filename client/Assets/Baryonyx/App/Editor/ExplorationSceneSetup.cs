using System;
using Baryonyx.Adventure;
using Baryonyx.Adventure.Editor;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.Stages.Editor;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds Exploration.unity, the adventure's exploration: the exploration screen on the
    /// battle's camera and backgrounds (the same stages and selector as Battle.unity), so a
    /// destination is explored and fought on the same place, with the bootstrap and the shutter.
    /// </summary>
    public static class ExplorationSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Exploration.unity";

        [MenuItem("Baryonyx/App/Create Exploration Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            AdventureAssets.CreateAssets();
            BattleStageSets.EnsureAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    var camera = ScreenScenes.AddCamera(
                        scene,
                        "ExplorationCamera",
                        ScreenScenes.CameraColor
                    );
                    var screen = ScreenScenes.AddScreen(
                        scene,
                        AdventureAssets.ExplorationPrefabPath,
                        "ExplorationScreen"
                    );
                    var selector = BattleSceneSetup.AddStages(scene, camera, screen);
                    ScreenScenes.AddEventSystem(scene);
                    var transition = SceneTransitionSetup.AddTransition(
                        scene,
                        startCovered: true,
                        revealOnStart: true
                    );
                    var bootstrap = ScreenScenes.AddObject<ExplorationBootstrap>(
                        scene,
                        "ExplorationBootstrap"
                    );
                    var serialized = new SerializedObject(bootstrap);
                    serialized.FindProperty("view").objectReferenceValue =
                        screen.GetComponent<ExplorationView>();
                    serialized.FindProperty("stages").objectReferenceValue = selector;
                    serialized.FindProperty("transition").objectReferenceValue = transition;
                    serialized.FindProperty("settings").objectReferenceValue =
                        TopStartupSyncSetup.LoadSettings();
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ScreenScenes.AddToBuildSettings(ScenePath);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
