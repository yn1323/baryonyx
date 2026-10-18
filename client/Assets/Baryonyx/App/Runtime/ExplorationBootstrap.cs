using System.Collections;
using Baryonyx.Adventure;
using Baryonyx.Health;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Runs the exploration scene: the map of the adventure in progress. The next room is shown
    /// on the same map as the camera follows the party there; a room with a battle opens the
    /// battle scene, and suspending or ending the adventure returns to Home.
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
                () => Leave(SceneNames.Home),
                () => Leave(SceneNames.Battle)
            );
            flow.Start();
        }

        // 開く演出の途中では次の演出を始められない。サーバーの返事が演出より早く届き、冒険がなくて
        // ホームへ戻るときなどは、開き終えてから移る。断ると「読み込み中…」のまま止まるため。
        private bool Leave(string scene)
        {
            if (transition == null || !transition.IsPlaying)
                return SceneLoader.Load(scene, transition, this);
            StartCoroutine(LeaveAfterOpening(scene));
            return true;
        }

        private IEnumerator LeaveAfterOpening(string scene)
        {
            while (transition != null && transition.IsPlaying)
                yield return null;
            SceneLoader.Load(scene, transition, this);
        }

        private void OnDestroy()
        {
            flow?.Dispose();
            flow = null;
        }
    }
}
