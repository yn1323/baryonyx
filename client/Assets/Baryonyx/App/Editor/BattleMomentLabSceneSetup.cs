using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using UnityEditor;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds BattleMomentLab.unity, the debugging room for the turning points of a battle: the
    /// battle screen as on BattleInspect (the same prefab, stage, camera, lens and grade), with
    /// the hand, the energy, the deck and the end of turn put away, and the lab's controls
    /// (<see cref="BattleMomentLabAssets"/>) over it to play each moment through the battle's own
    /// code (<see cref="BattleMomentLab"/>).
    /// </summary>
    public static class BattleMomentLabSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/BattleMomentLab.unity";

        [MenuItem("Baryonyx/App/Create Battle Moment Lab Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();

            // The battle screen and the dusk highland are BattleInspect's; they are made here
            // only if they are missing, so building the lab leaves them as they are.
            BattleInspectSceneSetup.EnsureBattleAssets();
            BattleMomentLabAssets.CreateAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    var screen = BattleInspectSceneSetup.AddBattle(scene);
                    EnemyLabSceneSetup.PutCardsAway(screen);

                    var panel = ScreenScenes.AddScreen(
                        scene,
                        BattleMomentLabAssets.PrefabPath,
                        "BattleMomentLabPanel"
                    );
                    var lab = panel.GetComponent<BattleMomentLab>();
                    lab.View = screen.GetComponent<BattleInspectView>();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(lab);
                    ScreenScenes.AddEventSystem(scene);
                }
            );

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // The showcase loads scenes through SceneManager, which only finds scenes in the build.
            ScreenScenes.AddToBuildSettings(ScenePath);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
