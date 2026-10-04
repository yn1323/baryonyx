using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// Runs the exploration of the adventure in progress (doc/features/stage-progression.md):
    /// reads where the party is, shows the map from its room, walks the road to the room tapped
    /// and saves the room chosen before showing the map from it, opens a treasure room's chest
    /// for its reward, and hands a room with a battle to the battle scene. The menu suspends the
    /// adventure to Home (it goes on from the room the party is in), or ends it and shows what it
    /// brought back. Scene changes are the caller's (<c>toHome</c>, <c>toBattle</c>); a room
    /// change inside the scene goes through <c>changeRoom</c>, which hides the map while the next
    /// room is shown.
    /// </summary>
    public sealed class ExplorationFlow : IDisposable
    {
        public const float BattleDelaySeconds = 0.9f;
        public const string MenuBody =
            "中断しても、ホームの行き先カードから\n今いる部屋の続きを再開できます。";

        private readonly ExplorationView view;
        private readonly IAdventureSource source;
        private readonly Func<bool> toHome;
        private readonly Func<bool> toBattle;
        private readonly Action<Action> changeRoom;
        private readonly Action<AdventureRun> entered;
        private readonly CancellationTokenSource lifetime = new();
        private ExplorationRoom room;
        private bool busy;
        private bool disposed;

        public ExplorationFlow(
            ExplorationView view,
            IAdventureSource source,
            Func<bool> toHome,
            Func<bool> toBattle,
            Action<Action> changeRoom = null,
            Action<AdventureRun> entered = null
        )
        {
            this.view = view != null ? view : throw new ArgumentNullException(nameof(view));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.toHome = toHome ?? throw new ArgumentNullException(nameof(toHome));
            this.toBattle = toBattle ?? throw new ArgumentNullException(nameof(toBattle));
            this.changeRoom = changeRoom ?? (show => show());
            this.entered = entered;
            view.RoomPressed += OnRoom;
            view.ChestPressed += OnChest;
            view.MenuPressed += OpenMenu;
            view.BackPressed += OpenMenu;
        }

        public AdventureState State { get; private set; }
        public ExplorationRoom Room => room;

        // シーンを離れる操作を受け付けたあと。以後の操作は受け付けない。
        public bool Leaving { get; private set; }
        public bool Busy => busy;

        // 実行中または直前の読み込み・保存。テストで完了を待つために公開する。
        public Task Work { get; private set; } = Task.CompletedTask;

        public void Start() => Work = LoadAsync();

        private async Task LoadAsync()
        {
            busy = true;
            if (view.Location != null)
                view.Location.text = "読み込み中…";
            if (view.Prompt != null)
                view.Prompt.text = "";
            AdventureState state;
            try
            {
                state = await source.LoadAsync(lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("冒険を取得できませんでした。" + exception.Message);
                if (disposed)
                    return;
                view.Dialog.Show(
                    AdventureTexts.LoadFailed,
                    "通信を確かめて、もう一度お試しください。",
                    null,
                    null,
                    new DialogChoice("もう一度", () => Work = LoadAsync()),
                    new DialogChoice(AdventureTexts.HomeChoice, GoHome)
                );
                return;
            }
            if (disposed)
                return;
            view.Dialog.Hide();
            if (!state.InProgress)
            {
                // 冒険がなければ（終えた・別の端末でやめたなど）、ホームへ戻る。
                AdventureSession.Use(state);
                GoHome();
                return;
            }
            Enter(state);
        }

        // 部屋を見せる。戦闘が残っていれば、少し見せてから戦闘へ移る。
        private void Enter(AdventureState state)
        {
            State = AdventureSession.Use(state);
            room = ExplorationRoom.From(state.Run);
            entered?.Invoke(state.Run);
            view.Render(room);
            busy = false;
            if (room.StartsBattle)
                Work = BattleAsync();
        }

        private async Task BattleAsync()
        {
            busy = true;
            try
            {
                await Awaitable.WaitForSecondsAsync(BattleDelaySeconds, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (disposed || Leaving)
                return;
            Leaving = toBattle();
            if (!Leaving)
            {
                busy = false;
                view.ShowNotice("戦闘を始められませんでした");
            }
        }

        private void OnRoom(string roomId)
        {
            if (busy || Leaving || room == null)
                return;
            foreach (var exit in room.Exits)
                if (exit.Id == roomId)
                {
                    Work = MoveAsync(exit);
                    return;
                }
        }

        // 道を歩きながら選んだ部屋を保存し、両方が済んでから、その部屋から見た地図を見せる。
        private async Task MoveAsync(ExplorationExit exit)
        {
            busy = true;
            var walked = new TaskCompletionSource<bool>();
            view.WalkTo(exit.Id, () => walked.TrySetResult(true));
            AdventureState state;
            try
            {
                state = await source.MoveAsync(exit.Room, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("部屋を選べませんでした。" + exception.Message);
                if (disposed)
                    return;
                view.ShowNotice(AdventureTexts.SaveFailed);
                Work = LoadAsync();
                return;
            }
            await walked.Task;
            if (disposed)
                return;
            changeRoom(() => Enter(state));
        }

        private void OnChest()
        {
            if (busy || Leaving || room == null || room.Chest != ExplorationChest.Closed)
                return;
            Work = OpenChestAsync();
        }

        private async Task OpenChestAsync()
        {
            busy = true;
            var run = State.Run;
            AdventureState state;
            try
            {
                state = await source.ClearAsync(run.RoomId, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("宝箱を開けられませんでした。" + exception.Message);
                if (disposed)
                    return;
                view.ShowNotice(AdventureTexts.SaveFailed);
                Work = LoadAsync();
                return;
            }
            if (disposed)
                return;
            Enter(state);
            busy = true;
            view.Dialog.Show(
                "宝箱を開けた！",
                AdventureTexts.RewardBody(view.Bonuses, state.Reward),
                state.Reward != null
                    ? AdventureTexts.BonusIcon(view.Bonuses, state.Reward.BonusId)
                    : null,
                CloseReward,
                new DialogChoice(AdventureTexts.NextChoice, CloseReward)
            );
        }

        private void CloseReward()
        {
            view.Dialog.Hide();
            busy = false;
        }

        private void OpenMenu()
        {
            if (busy || Leaving || State?.Run == null)
                return;
            view.Dialog.Show(
                AdventureTexts.MenuTitle,
                AdventureCatalog.Location(State.Run) + "\n" + MenuBody,
                null,
                view.Dialog.Hide,
                new DialogChoice(AdventureTexts.SuspendChoice, GoHome),
                new DialogChoice(AdventureTexts.QuitChoice, ConfirmQuit),
                new DialogChoice(AdventureTexts.CloseChoice, view.Dialog.Hide)
            );
        }

        private void ConfirmQuit() =>
            view.Dialog.Show(
                AdventureTexts.QuitTitle,
                AdventureTexts.QuitBody,
                null,
                OpenMenu,
                new DialogChoice(AdventureTexts.QuitChoice, () => Work = QuitAsync()),
                new DialogChoice(AdventureTexts.GoOnChoice, view.Dialog.Hide)
            );

        private async Task QuitAsync()
        {
            busy = true;
            AdventureState state;
            try
            {
                state = await source.EndAsync(AdventureEndReason.Retreat, lifetime.Token);
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
                busy = false;
                view.Dialog.Hide();
                view.ShowNotice(AdventureTexts.SaveFailed);
                return;
            }
            if (disposed)
                return;
            State = AdventureSession.Use(state);
            ShowResult(view.Dialog, view.Bonuses, state.Result, GoHome);
        }

        /// <summary>The end of an adventure: how it ended and what it brought back, then Home.</summary>
        public static void ShowResult(
            GameDialog dialog,
            StepBonus.StepBonusMockData bonuses,
            AdventureResult result,
            Action home
        )
        {
            if (result == null)
            {
                home();
                return;
            }
            dialog.Show(
                AdventureCatalog.StatusTitle(result.Status),
                AdventureTexts.ResultBody(bonuses, result),
                null,
                null,
                new DialogChoice(AdventureTexts.HomeChoice, home)
            );
        }

        private void GoHome()
        {
            if (Leaving)
                return;
            Leaving = toHome();
            if (!Leaving)
            {
                view.Dialog.Resume();
                view.ShowNotice("ホームへ戻れませんでした");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            // 待っている処理が後からトークンを読んでも例外にならないよう、取り消すだけにする。
            lifetime.Cancel();
            view.RoomPressed -= OnRoom;
            view.ChestPressed -= OnChest;
            view.MenuPressed -= OpenMenu;
            view.BackPressed -= OpenMenu;
        }
    }
}
