using Baryonyx.Adventure;
using Baryonyx.Combat.Presentation;
using Baryonyx.Health;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Runs the exploration scene: the adventure in progress on the destination's stage. The
    /// next room is shown behind the shared shutter; a room with a battle opens the battle
    /// scene, and suspending or ending the adventure returns to Home.
    /// </summary>
    public sealed class ExplorationBootstrap : MonoBehaviour
    {
        [SerializeField]
        private ExplorationView view;

        [SerializeField]
        private BattleStageSelector stages;

        [SerializeField]
        private SceneTransitionController transition;

        [SerializeField]
        private HealthConnectionSettings settings;

        private ExplorationFlow flow;

        public ExplorationView View => view;
        public ExplorationFlow Flow => flow;
        public SceneTransitionController Transition => transition;

        private void Start()
        {
            if (view == null)
            {
                Debug.LogError("ExplorationBootstrap requires a view.", this);
                return;
            }
            // 単体で開いたときも、サーバーの接続を用意してから読む。
            GameServices.GetOrCreate(settings);
            flow = new ExplorationFlow(
                view,
                AdventureSession.SourceOrLocal,
                () => SceneLoader.Load(SceneNames.Home, transition, this),
                () => SceneLoader.Load(SceneNames.Battle, transition, this),
                ChangeRoom,
                UseStage
            );
            if (AdventureSession.Current?.Run != null)
                UseStage(AdventureSession.Current.Run);
            flow.Start();
        }

        private void OnDestroy()
        {
            flow?.Dispose();
            flow = null;
        }

        // シャッターで覆ってから次の部屋を見せ、開く。
        private void ChangeRoom(System.Action show)
        {
            if (
                transition == null
                || !transition.PlayOut(() =>
                {
                    show();
                    transition.PlayIn();
                })
            )
                show();
        }

        private void UseStage(AdventureRun run)
        {
            if (stages == null || run == null)
                return;
            var stage = AdventureCatalog.StageOf(run.DestinationId);
            if (stages.Stage == stage && stages.Shown != null)
                return;
            stages.Stage = stage;
            stages.Apply();
        }
    }
}
