using System;
using Baryonyx.Adventure;
using Baryonyx.Adventure.Editor;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using UnityEditor;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds Exploration.unity, the adventure's exploration: the exploration screen (the map
    /// painted on the device) with the bootstrap and the shutter.
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
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    ScreenScenes.AddCamera(scene, "ExplorationCamera", ScreenScenes.CameraColor);
                    var screen = ScreenScenes.AddScreen(
                        scene,
                        AdventureAssets.ExplorationPrefabPath,
                        "ExplorationScreen"
                    );
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
