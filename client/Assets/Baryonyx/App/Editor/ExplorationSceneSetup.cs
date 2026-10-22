using System;
using Baryonyx.Adventure;
using Baryonyx.Adventure.Editor;
using Baryonyx.Editor;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.Training;
using Baryonyx.Training.Editor;
using Baryonyx.UI.Editor;
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
            EditorGuard.RequireEditMode();
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
                    var transition = SceneTransitionAssets.AddTransition(
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
                    serialized.FindProperty("party").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
                    serialized.FindProperty("training").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<TrainingMockData>(TrainingAssets.DataPath);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ScreenScenes.AddToBuildSettings(ScenePath);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
