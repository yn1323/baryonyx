using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.Health;
using Baryonyx.Party;
using Baryonyx.Training;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Runs the exploration scene: the map of the adventure in progress. The next room is shown
    /// on the same map as the camera follows the party there; a room with a battle opens the
    /// battle scene, and suspending or ending the adventure returns to Home. The party walks the
    /// map as one pawn; the rest and the altar choose from the formation's members (read from the
    /// server when not read yet in this run).
    /// </summary>
    public sealed class ExplorationBootstrap : MonoBehaviour
    {
        [SerializeField]
        private ExplorationView view;

        [SerializeField]
        private SceneTransitionController transition;

        [SerializeField]
        private HealthConnectionSettings settings;

        // 編成の名前と絵。
        [SerializeField]
        private PartyMockData party;

        // 進化の祭壇で、固有スキルの段階を引く育成の仮データ。
        [SerializeField]
        private TrainingMockData training;

        // 読んだ編成。読めるまでは、アプリを動かしている間の編成を使う。
        private PartyFormation formation;

        private ExplorationFlow flow;
        private readonly CancellationTokenSource lifetime = new();

        public ExplorationView View => view;
        public ExplorationFlow Flow => flow;
        public SceneTransitionController Transition => transition;
        public PartyMockData Party => party;

        // 編成の読み込みと地図への反映。テストで完了を待つために公開する。
        public Task PartyTask { get; private set; } = Task.CompletedTask;

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
                () => Leave(SceneNames.Battle),
                null,
                () =>
                    AdventureAltar.ChoicesOf(
                        formation ?? (party != null ? PartySession.Formation(party) : null),
                        party,
                        training
                    ),
                (choice, stage) => AdventureAltar.Evolve(choice, stage, training),
                () =>
                    party != null
                        ? AdventureParty.Of(
                            formation ?? PartySession.Formation(party),
                            party,
                            training
                        )
                        : Array.Empty<Combat.Presentation.BattlePartyMember>()
            );
            flow.Start();
            PartyTask = UsePartyAsync();
        }

        private async Task UsePartyAsync()
        {
            if (party == null)
            {
                Debug.LogError("ExplorationBootstrap requires the party mock data.", this);
                return;
            }
            try
            {
                formation = await PartySession.CurrentAsync(party, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                // 読めなければ、休憩と祭壇は仮データの編成で選ぶ。
                Debug.LogWarning("編成を取得できませんでした。" + exception.Message);
            }
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
            lifetime.Cancel();
            lifetime.Dispose();
            flow?.Dispose();
            flow = null;
        }
    }
}
