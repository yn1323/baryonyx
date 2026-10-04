using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat.Presentation;
using Baryonyx.Networking;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// Runs a battle of the adventure on the battle screen (doc/features/combat.md): the room's
    /// encounter on the destination's stage, and, when the battle ends, the reward of the room
    /// and the way on to the next room, or, after a defeat, the revive paid with runes or the
    /// return with what was found (both shown alike, with no countdown). The boss's defeat ends
    /// the adventure with its result. The menu suspends the adventure (the battle begins again
    /// on resuming) or ends it. Scene changes are the caller's.
    /// </summary>
    public sealed class BattleAdventureFlow : IDisposable
    {
        public const float VictoryDelaySeconds = 1.4f;
        public const float DefeatDelaySeconds = 1.0f;
        public const string VictoryTitle = "勝利！";
        public const string DefeatTitle = "全滅してしまった…";

        private readonly BattleInspectView battle;
        private readonly AdventureOverlay overlay;
        private readonly IAdventureSource source;
        private readonly Func<bool> toExploration;
        private readonly Func<bool> toHome;
        private readonly CancellationTokenSource lifetime = new();
        private AdventureState state;
        private bool busy;
        private bool disposed;

        public BattleAdventureFlow(
            BattleInspectView battle,
            AdventureOverlay overlay,
            IAdventureSource source,
            AdventureState state,
            Func<bool> toExploration,
            Func<bool> toHome
        )
        {
            this.battle = battle != null ? battle : throw new ArgumentNullException(nameof(battle));
            this.overlay =
                overlay != null ? overlay : throw new ArgumentNullException(nameof(overlay));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.toExploration =
                toExploration ?? throw new ArgumentNullException(nameof(toExploration));
            this.toHome = toHome ?? throw new ArgumentNullException(nameof(toHome));
            if (state.Run == null || !state.Run.InBattle)
                throw new ArgumentException("The adventure has no battle to fight.", nameof(state));

            var encounter = AdventureCatalog.Encounter(state.Run.Room.Encounter);
            battle.EndsWithOutcome = true;
            battle.UseEncounter(encounter.Enemies, encounter.HpScale);
            battle.Finished += OnFinished;
            overlay.MenuPressed += OpenMenu;
            overlay.BackPressed += OpenMenu;
            overlay.ShowMenu(true);
        }

        public AdventureState State => state;
        public string RoomId => state.Run?.RoomId ?? "";
        public bool Leaving { get; private set; }

        // 実行中または直前の保存。テストで完了を待つために公開する。
        public Task Work { get; private set; } = Task.CompletedTask;

        private GameDialog Dialog => overlay.Dialog;

        private void OnFinished(BattleOutcome outcome)
        {
            if (disposed || Leaving)
                return;
            busy = true;
            Work = outcome == BattleOutcome.Victory ? VictoryAsync() : DefeatAsync();
        }

        private async Task<bool> Pause(float seconds)
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(seconds, lifetime.Token);
                return !disposed;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        // 勝った部屋を終え、報酬を見せて次の部屋へ。最奥のボスなら冒険の結果を見せてホームへ。
        private async Task VictoryAsync()
        {
            if (!await Pause(VictoryDelaySeconds))
                return;
            AdventureState cleared;
            try
            {
                cleared = await source.ClearAsync(RoomId, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("勝利を保存できませんでした。" + exception.Message);
                if (disposed)
                    return;
                Dialog.Show(
                    AdventureTexts.SaveFailed,
                    "通信を確かめて、もう一度お試しください。",
                    null,
                    null,
                    new DialogChoice("もう一度", () => Work = VictoryAsync())
                );
                return;
            }
            if (disposed)
                return;
            state = AdventureSession.Use(cleared);
            var reward = cleared.Reward;
            var result = cleared.Result;
            Dialog.Show(
                VictoryTitle,
                AdventureTexts.RewardBody(overlay.Bonuses, reward),
                reward != null ? AdventureTexts.BonusIcon(overlay.Bonuses, reward.BonusId) : null,
                null,
                result != null
                    ? new DialogChoice(
                        "つぎへ",
                        () => ExplorationFlow.ShowResult(Dialog, overlay.Bonuses, result, GoHome)
                    )
                    : new DialogChoice(AdventureTexts.NextChoice, GoExplore)
            );
        }

        // 復活と帰還を同じ重みで並べる。どちらを選んでも手に入れた物は残る。
        private async Task DefeatAsync()
        {
            if (!await Pause(DefeatDelaySeconds))
                return;
            try
            {
                // 所持ルーンと次の復活の費用を、最新の値で見せる。
                state = AdventureSession.Use(await source.LoadAsync(lifetime.Token));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("冒険を取得できませんでした。" + exception.Message);
            }
            if (disposed)
                return;
            ShowRevive();
        }

        private void ShowRevive()
        {
            var run = state.Run;
            int cost = run?.ReviveCost ?? 0;
            bool canRevive = run != null && run.InBattle && state.Runes >= cost;
            Dialog.Show(
                DefeatTitle,
                AdventureTexts.ReviveBody(cost, state.Runes),
                null,
                null,
                new DialogChoice(
                    AdventureTexts.ReviveChoice(cost),
                    () => Work = ReviveAsync(),
                    canRevive
                ),
                new DialogChoice(
                    AdventureTexts.ReturnChoice,
                    () => Work = EndAsync(AdventureEndReason.Defeat)
                )
            );
        }

        private async Task ReviveAsync()
        {
            AdventureState revived;
            try
            {
                revived = await source.ReviveAsync(RoomId, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("復活できませんでした。" + exception.Message);
                if (disposed)
                    return;
                overlay.ShowNotice(
                    exception is ServerApiException { StatusCode: 409 }
                        ? "ルーンが足りないか、冒険の状態が変わりました"
                        : "復活できませんでした"
                );
                // 断られたら最新の残高と費用で選び直す。
                Work = DefeatAsync();
                return;
            }
            if (disposed)
                return;
            state = AdventureSession.Use(revived);
            Dialog.Hide();
            busy = false;
            battle.Revive();
            overlay.ShowNotice($"ルーン {revived.RevivedFor ?? 0:N0} を使って復活した");
        }

        private void OpenMenu()
        {
            if (busy || Leaving || Dialog.IsShown)
                return;
            Dialog.Show(
                AdventureTexts.MenuTitle,
                AdventureCatalog.Location(state.Run) + "\n" + AdventureTexts.SuspendBody,
                null,
                Dialog.Hide,
                new DialogChoice(AdventureTexts.SuspendChoice, GoHome),
                new DialogChoice(AdventureTexts.QuitChoice, ConfirmQuit),
                new DialogChoice(AdventureTexts.GoOnChoice, Dialog.Hide)
            );
        }

        private void ConfirmQuit() =>
            Dialog.Show(
                AdventureTexts.QuitTitle,
                AdventureTexts.QuitBody,
                null,
                OpenMenu,
                new DialogChoice(
                    AdventureTexts.QuitChoice,
                    () => Work = EndAsync(AdventureEndReason.Retreat)
                ),
                new DialogChoice(AdventureTexts.GoOnChoice, Dialog.Hide)
            );

        private async Task EndAsync(AdventureEndReason reason)
        {
            busy = true;
            AdventureState ended;
            try
            {
                ended = await source.EndAsync(reason, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("冒険を終えられませんでした。" + exception.Message);
                if (disposed)
                    return;
                overlay.ShowNotice(AdventureTexts.SaveFailed);
                Dialog.Resume();
                return;
            }
            if (disposed)
                return;
            state = AdventureSession.Use(ended);
            ExplorationFlow.ShowResult(Dialog, overlay.Bonuses, ended.Result, GoHome);
        }

        private void GoExplore()
        {
            if (Leaving)
                return;
            Leaving = toExploration();
            if (!Leaving)
                Dialog.Resume();
        }

        private void GoHome()
        {
            if (Leaving)
                return;
            Leaving = toHome();
            if (!Leaving)
                Dialog.Resume();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            lifetime.Cancel();
            battle.Finished -= OnFinished;
            overlay.MenuPressed -= OpenMenu;
            overlay.BackPressed -= OpenMenu;
        }
    }
}
