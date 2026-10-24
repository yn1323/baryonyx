using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.Combat.Presentation;
using Baryonyx.Health;
using Baryonyx.Party;
using Baryonyx.Training;
using Baryonyx.UI;
using Baryonyx.WorldMap;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Runs the battle scene. Opened from the highway's map (<see cref="WorldMapSession.Battle"/>),
    /// it fights the square's fixed enemies from full HP and returns to the map
    /// (<see cref="WorldMapBattleFlow"/>). When the adventure in progress waits on a battle (the
    /// exploration opened the scene), the battle is fought with the room's encounter on the
    /// destination's stage and ends with its outcome (<see cref="BattleAdventureFlow"/>), going on to the
    /// exploration or Home. Opened on its own, it stays the battle mock with the stage chosen in
    /// the Inspector. It sets the encounter and the formation's party (<see cref="AdventureParty"/>)
    /// after the battle screen has set itself up and before the battle begins, so it runs after
    /// the screen's Awake. When the party has not been read from the server yet in this run, the
    /// battle waits for it to be read before it begins.
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

        // 編成の名前・絵・スキルの挿絵と、レベルごとのステータス・固有スキルの仮データ。
        [SerializeField]
        private PartyMockData party;

        [SerializeField]
        private TrainingMockData training;

        private BattleAdventureFlow flow;
        private WorldMapBattleFlow worldMapFlow;
        private readonly CancellationTokenSource lifetime = new();

        public BattleInspectView Battle => battle;
        public AdventureOverlay Overlay => overlay;
        public BattleAdventureFlow Flow => flow;
        public WorldMapBattleFlow WorldMapFlow => worldMapFlow;
        public SceneTransitionController Transition => transition;
        public PartyMockData Party => party;
        public TrainingMockData Training => training;

        // 編成の読み込みと戦闘への反映。テストで完了を待つために公開する。
        public Task PartyTask { get; private set; } = Task.CompletedTask;

        private void Awake()
        {
            if (battle == null || overlay == null)
            {
                Debug.LogError("BattleBootstrap requires the battle and the overlay.", this);
                return;
            }
            GameServices.GetOrCreate(settings);
            // 街道の地図から開いたときは、そのマスの決まった敵と戦い、地図へ戻る。
            if (WorldMapEncounters.Of(WorldMapSession.Battle) is { } road)
            {
                if (stages != null)
                {
                    stages.Stage = road.Stage;
                    stages.Apply();
                }
                worldMapFlow = new WorldMapBattleFlow(
                    battle,
                    overlay,
                    WorldMapSession.Battle,
                    () => SceneLoader.Load(SceneNames.WorldMap, transition, this),
                    () => SceneLoader.Load(SceneNames.Battle, transition, this)
                );
                battle.HoldEntrance = () =>
                    transition != null && (transition.IsCovered || transition.IsPlaying);
                PartyTask = UsePartyAsync();
                return;
            }
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
            // 魔物は、探索から移ってきた幕が開ききってから歩み出る。読み込み直後の重いフレームに
            // 出現が埋もれないようにするため。
            battle.HoldEntrance = () =>
                transition != null && (transition.IsCovered || transition.IsPlaying);
            PartyTask = UsePartyAsync();
        }

        // 編成の4人とデッキで戦う。読み終えるまで戦闘画面を止め、始まりの演出を待たせる。
        private async Task UsePartyAsync()
        {
            if (party == null || training == null)
            {
                Debug.LogError("BattleBootstrap requires the party and training mock data.", this);
                return;
            }
            var loading = PartySession.CurrentAsync(party, lifetime.Token);
            if (!loading.IsCompleted)
                battle.enabled = false;
            PartyFormation formation;
            try
            {
                formation = await loading;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                // 読めなければ、アプリを動かしている間の編成（仮データ）で戦う。
                Debug.LogWarning("編成を取得できませんでした。" + exception.Message);
                formation = PartySession.Formation(party);
            }
            if (battle == null)
                return;
            var members = AdventureParty.Of(formation, party, training, overlay.ItemArt);
            if (flow != null)
            {
                // 冒険の戦闘は、マスの間で持ち越したHPから始める（doc/features/stage-progression.md）。
                var shares = AdventureVitals.Of(flow.State.Run?.Id);
                for (int slot = 0; slot < members.Count && slot < shares.Length; slot++)
                    if (members[slot] != null)
                        members[slot].HpShare = shares[slot];
                // 休憩で鍛えた仲間とカードと、戦闘後に選んだ強化は、その冒険のあいだ効く。
                AdventureParty.Hone(
                    members,
                    flow.State.Run?.Id,
                    AdventureBoons.IsStrongBattle(flow.State.Run)
                );
                flow.Party = members;
            }
            battle.UseParty(members);
            battle.enabled = true;
        }

        private void OnDestroy()
        {
            lifetime.Cancel();
            lifetime.Dispose();
            flow?.Dispose();
            flow = null;
            worldMapFlow?.Dispose();
            worldMapFlow = null;
        }
    }
}
