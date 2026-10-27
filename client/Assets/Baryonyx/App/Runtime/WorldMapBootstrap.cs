using System.Collections;
using Baryonyx.UI;
using Baryonyx.WorldMap;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Runs the world map scene (the highway prototype): the button at the top left returns to
    /// Home, the one at the bottom right opens the list of dungeons (for now, the travel
    /// office), and reaching a square with a battle opens the battle scene, behind the shared
    /// shutter. The scene stays covered until the map has read where the party is, then opens
    /// with the side shutter.
    /// </summary>
    public sealed class WorldMapBootstrap : MonoBehaviour
    {
        [SerializeField]
        private WorldMapView view;

        [SerializeField]
        private SceneTransitionController transition;

        // 読み込みが遅いときは、これ以上覆ったまま待たせず、「読み込み中…」の地図を見せる。
        private const float MaxCoveredSeconds = 3f;

        public WorldMapView View => view;
        public SceneTransitionController Transition => transition;

        private void Start()
        {
            if (view == null)
            {
                Debug.LogError("WorldMapBootstrap requires a view.", this);
                return;
            }
            view.BackPressed += () => SceneLoader.Load(SceneNames.Home, transition, this);
            view.DungeonsPressed += () =>
                SceneLoader.Load(SceneNames.TravelOffice, transition, this);
            view.BattleRequested += Fight;
            StartCoroutine(RevealWhenLoaded());
        }

        // 初期のマスに置いた地図を見せてから今いるマスへ飛ばないよう、読み終えてから開く。
        private IEnumerator RevealWhenLoaded()
        {
            // 地図の Start が読み込みを始めるのを待つ。
            yield return null;
            float waited = 0f;
            while (!view.LoadTask.IsCompleted && waited < MaxCoveredSeconds)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
            if (transition != null && transition.IsCovered)
                transition.PlayIn();
        }

        // 戦闘のあるマスに着いたら、そのマスの戦闘を開く。開けなければ、元のマスへ戻す。
        private void Fight(string squareId)
        {
            WorldMapSession.Battle = squareId;
            if (!SceneLoader.Load(SceneNames.Battle, transition, this, Cancel))
                Cancel();
        }

        private void Cancel()
        {
            WorldMapSession.Battle = null;
            if (view != null)
                view.CancelBattle();
        }
    }
}
