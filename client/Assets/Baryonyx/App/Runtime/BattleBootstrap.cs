using Baryonyx.Adventure;
using Baryonyx.Combat.Presentation;
using Baryonyx.Health;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Runs the battle scene. When the adventure in progress waits on a battle (the exploration
    /// opened the scene), the battle is fought with the room's encounter on the destination's
    /// stage and ends with its outcome (<see cref="BattleAdventureFlow"/>), going on to the
    /// exploration or Home. Opened on its own, it stays the battle mock with the stage chosen in
    /// the Inspector. It sets the encounter after the battle screen has set itself up and before
    /// the battle begins, so it runs after the screen's Awake.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class BattleBootstrap : MonoBehaviour
    {
        [SerializeField]
        private BattleInspectView battle;

        [SerializeField]
        private AdventureOverlay overlay;

        [SerializeField]
        private BattleStageSelector stages;

        [SerializeField]
        private SceneTransitionController transition;

        [SerializeField]
        private HealthConnectionSettings settings;

        private BattleAdventureFlow flow;

        public BattleInspectView Battle => battle;
        public AdventureOverlay Overlay => overlay;
        public BattleAdventureFlow Flow => flow;
        public SceneTransitionController Transition => transition;

        private void Awake()
        {
            if (battle == null || overlay == null)
            {
                Debug.LogError("BattleBootstrap requires the battle and the overlay.", this);
                return;
            }
            GameServices.GetOrCreate(settings);
            var state = AdventureSession.Current;
            if (state?.Run == null || !state.Run.InBattle)
            {
                // 冒険の外（単体で開いた戦闘のモック）では、勝敗で終わらずメニューも出さない。
                overlay.ShowMenu(false);
                return;
            }
            if (stages != null)
            {
                stages.Stage = AdventureCatalog.StageOf(state.Run.DestinationId);
                stages.Apply();
            }
            flow = new BattleAdventureFlow(
                battle,
                overlay,
                AdventureSession.SourceOrLocal,
                state,
                () => SceneLoader.Load(SceneNames.Exploration, transition, this),
                () => SceneLoader.Load(SceneNames.Home, transition, this)
            );
        }

        private void OnDestroy()
        {
            flow?.Dispose();
            flow = null;
        }
    }
}
