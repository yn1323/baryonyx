using Baryonyx.Adventure;
using Baryonyx.Health;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Runs the exploration scene: the map of the adventure in progress. The next room is shown
    /// while the map fades; a room with a battle opens the battle scene, and suspending or ending
    /// the adventure returns to Home.
    /// </summary>
    public sealed class ExplorationBootstrap : MonoBehaviour
    {
        [SerializeField]
        private ExplorationView view;

        [SerializeField]
        private SceneTransitionController transition;

        [SerializeField]
        private HealthConnectionSettings settings;

        private ExplorationFlow flow;

        public ExplorationView View => view;
        public ExplorationFlow Flow => flow;
        public SceneTransitionController Transition => transition;

        // 冒険の流れが地図を描くため、見本の道は描かせない。
        private void Awake()
        {
            if (view != null)
                view.SampleSeed = 0;
        }

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
                view.Crossfade
            );
            flow.Start();
        }

        private void OnDestroy()
        {
            flow?.Dispose();
            flow = null;
        }
    }
}
