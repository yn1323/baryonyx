using System;
using System.Linq;
using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.Stages.Editor;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds EnemyLab.unity, the debugging room for the enemies' behaviour: the battle screen
    /// as on BattleInspect (the same prefab, stage, camera, lens and grade), with the hand, the
    /// energy, the deck and the end of turn put away, and the lab's controls
    /// (<see cref="EnemyLabAssets"/>) over it to make the enemies act through the battle's own
    /// code (<see cref="EnemyLab"/>).
    /// </summary>
    public static class EnemyLabSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/EnemyLab.unity";

        // The battle screen's parts for playing cards, which the lab has no use for.
        private static readonly string[] CardParts =
        {
            "ScreenCanvas/SafeArea/Hand",
            "ScreenCanvas/SafeArea/Energy",
            "ScreenCanvas/SafeArea/Deck",
            "ScreenCanvas/SafeArea/EndTurn",
        };

        [MenuItem("Baryonyx/App/Create Enemy Lab Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();

            // The battle screen and the dusk highland are BattleInspect's; they are made here
            // only if they are missing, so building the lab leaves them as they are.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BattleInspectAssets.PrefabPath) == null)
                BattleInspectAssets.CreateAssets();
            if (
                AssetDatabase.LoadAssetAtPath<GameObject>(StageSetAssets.DuskHighlandPrefabPath)
                    == null
                || AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(
                    StageSetAssets.DuskHighlandLookPath
                ) == null
            )
                StageSetAssets.EnsureAssets();
            EnemyLabAssets.CreateAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    var screen = BattleInspectSceneSetup.AddBattle(scene);
                    foreach (var path in CardParts)
                    {
                        var part = screen.transform.Find(path);
                        if (part == null)
                            throw new InvalidOperationException("The battle screen has no " + path);
                        part.gameObject.SetActive(false);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
                    }

                    var view = screen.GetComponent<BattleInspectView>();
                    var panel = ScreenScenes.AddScreen(
                        scene,
                        EnemyLabAssets.PrefabPath,
                        "EnemyLabPanel"
                    );
                    var lab = panel.GetComponent<EnemyLab>();
                    lab.View = view;
                    lab.EnemyNames = view
                        .Enemies.Select(enemy => EnemyLabAssets.NameOf(enemy.Body.name))
                        .ToArray();
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
