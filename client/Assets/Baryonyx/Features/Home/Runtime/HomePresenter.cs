using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.Home
{
    /// <summary>
    /// Turns home button presses into feedback. Resuming the adventure is delegated to the
    /// caller and accepted only once, so repeated taps cannot start two scene loads. When a
    /// step source is given, the step panel shows the steps and runes saved on the server,
    /// and a tap syncs the steps, turns them into runes and plays the gain. Screen buttons
    /// (tavern, workshop, temple, travel office) are handed to <c>openScreen</c> when it is given; once a
    /// screen opens, further taps are ignored while the scene changes.
    /// </summary>
    public sealed class HomePresenter : IDisposable
    {
        private readonly HomeView view;
        private readonly HomeSnapshot snapshot;
        private readonly Func<bool> startAdventure;
        private readonly IHomeStepSource steps;
        private readonly Func<HomeAction, bool> openScreen;
        private readonly CancellationTokenSource lifetime = new();
        private bool adventureStarted;
        private bool screenOpened;
        private bool disposed;

        public HomePresenter(
            HomeView view,
            HomeSnapshot snapshot,
            Func<bool> startAdventure,
            IHomeStepSource steps = null,
            Func<HomeAction, bool> openScreen = null
        )
        {
            this.view = view != null ? view : throw new ArgumentNullException(nameof(view));
            this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            this.startAdventure = startAdventure;
            this.steps = steps;
            this.openScreen = openScreen;
            view.ActionRequested += Handle;
            if (steps != null)
            {
                // 取得前や取得に失敗したときに、モックの歩数・ルーンを実際の値として見せない。
                snapshot.Steps = 0;
                snapshot.StepsKnown = false;
                snapshot.Runes = 0;
                snapshot.RunesKnown = false;
            }
            view.Render(HomeViewState.From(snapshot));
            if (steps != null)
                StepTask = RefreshStepsAsync(steps.LoadAsync, false);
        }

        public bool AdventureStarted => adventureStarted;
        public bool ScreenOpened => screenOpened;
        public bool StepSyncing => snapshot.StepSyncing;

        // 実行中または直前の歩数の取得。テストで完了を待つために公開する。
        public Task StepTask { get; private set; } = Task.CompletedTask;

        public void Handle(HomeAction action)
        {
            if (disposed || adventureStarted || screenOpened)
                return;

            if (action == HomeAction.Resume && startAdventure != null)
            {
                adventureStarted = startAdventure();
                if (!adventureStarted)
                    view.ShowToast("再開できませんでした");
                return;
            }

            if (action == HomeAction.SyncSteps && steps != null)
            {
                if (!snapshot.StepSyncing)
                    StepTask = RefreshStepsAsync(steps.SyncAsync, true);
                return;
            }

            if (openScreen != null && openScreen(action))
            {
                screenOpened = true;
                return;
            }

            view.ShowToast(HomeViewState.MessageFor(action, snapshot.StepLink));
        }

        private async Task RefreshStepsAsync(
            Func<CancellationToken, Task<HomeStepReading>> operation,
            bool announce
        )
        {
            snapshot.StepSyncing = true;
            view.Render(HomeViewState.From(snapshot));
            HomeStepReading reading;
            try
            {
                reading = await operation(lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("歩数を更新できませんでした。" + exception.Message);
                reading = new HomeStepReading(HomeStepResult.Failed, snapshot.StepLink, 0);
            }
            if (disposed)
                return;

            snapshot.StepSyncing = false;
            snapshot.StepLink = reading.Link;
            if (reading.Result == HomeStepResult.Updated)
            {
                snapshot.Steps = reading.Steps;
                snapshot.StepsKnown = true;
                if (reading.Day.HasValue)
                    snapshot.Today = reading.Day.Value;
            }
            if (reading.Runes.HasValue)
            {
                snapshot.Runes = reading.Runes.Value;
                snapshot.RunesKnown = true;
            }
            view.Render(HomeViewState.From(snapshot));
            // 付与前の残高から、サーバーと同じ付与後の残高まで数を増やして見せる。
            // 仮のルーンは歩数の同期に失敗しても付与されるため、結果より先に扱う。
            if (announce && reading.GrantedRunes > 0 && reading.Runes.HasValue)
                view.PlayRuneGain(reading.Runes.Value - reading.GrantedRunes, reading.Runes.Value);
            else if (announce && reading.Result == HomeStepResult.Updated)
                view.ShowNotice(NoRunesMessage);
            else if (announce || reading.Result == HomeStepResult.Failed)
                view.ShowToast(MessageFor(reading.Result));
        }

        public const string NoRunesMessage = "獲得ルーンはありません";

        public static string MessageFor(HomeStepResult result) =>
            result switch
            {
                HomeStepResult.Updated => "歩数を同期しました",
                HomeStepResult.Unlinked => "Health Connectとの連携が必要です",
                HomeStepResult.SettingsOpened => "Health Connectで歩数の読み取りを許可してください",
                _ => "歩数を取得できませんでした",
            };

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            lifetime.Cancel();
            lifetime.Dispose();
            if (view != null)
                view.ActionRequested -= Handle;
        }
    }
}
