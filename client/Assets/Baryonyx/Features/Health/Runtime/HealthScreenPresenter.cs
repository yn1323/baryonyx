using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Health
{
    public enum HealthScreenPhase
    {
        ReadyToConnect,
        Connecting,
        Reading,
        Ready,
        PermissionRequired,
        Unavailable,
        UpdateRequired,
        Failed,
    }

    // This class owns the flow. Unity and SDK code are supplied at the boundary.
    public sealed class HealthScreenPresenter : IDisposable
    {
        private readonly IGoogleSignInProvider authentication;
        private readonly IHealthDataProvider health;
        private readonly CancellationTokenSource lifetime = new();
        private CancellationTokenSource operation;
        private Task active = Task.CompletedTask;
        private TaskCompletionSource<bool> foregroundSignal;
        private bool busy;
        private bool authenticating;
        private bool signingOut;
        private bool foreground = true;
        private bool systemDialog;
        private bool resumeRequested;
        private bool connected;
        private bool connectionRequested;
        private bool disposed;
        private int generation;

        public event Action Changed;
        public HealthScreenPhase Phase { get; private set; } = HealthScreenPhase.ReadyToConnect;
        public bool SignedIn { get; private set; }
        public bool IsBusy => busy || signingOut;
        public string GoogleMessage { get; private set; } = "未接続。歩数の表示には不要です。";
        public string Message { get; private set; } =
            "接続して、歩数の読み取りを許可してください。";
        public IReadOnlyList<HealthDaySnapshot> Days { get; private set; } =
            Array.Empty<HealthDaySnapshot>();
        public HealthDaySnapshot SelectedDay { get; private set; }
        public bool CanSignIn => !disposed && foreground && !SignedIn && !IsBusy;
        public bool CanConnect => !disposed && foreground && !IsBusy;
        public bool CanRefresh => CanConnect && connected;
        public bool CanOpenSettings =>
            CanConnect
            && (
                Phase == HealthScreenPhase.PermissionRequired
                || Phase == HealthScreenPhase.Unavailable
                || Phase == HealthScreenPhase.UpdateRequired
            );

        public HealthScreenPresenter(
            IGoogleSignInProvider authentication,
            IHealthDataProvider health
        )
        {
            this.authentication =
                authentication ?? throw new ArgumentNullException(nameof(authentication));
            this.health = health ?? throw new ArgumentNullException(nameof(health));
        }

        public Task SignInAsync()
        {
            if (!CanSignIn)
                return Task.CompletedTask;
            authenticating = true;
            return RunAsync(SignInCoreAsync);
        }

        public Task ConnectAsync()
        {
            if (!CanConnect)
                return Task.CompletedTask;
            connectionRequested = true;
            return RunAsync((version, token) => ReadAsync(true, version, token));
        }

        public Task RefreshAsync() =>
            CanRefresh
                ? RunAsync((version, token) => ReadAsync(false, version, token))
                : Task.CompletedTask;

        private Task RunAsync(Func<int, CancellationToken, Task> action)
        {
            resumeRequested = false;
            busy = true;
            operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            active = RunCoreAsync(action, generation, operation);
            return active;
        }

        private async Task RunCoreAsync(
            Func<int, CancellationToken, Task> action,
            int version,
            CancellationTokenSource cancellation
        )
        {
            try
            {
                await action(version, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                if (Current(version))
                {
                    if (authenticating)
                        SetGoogleMessage("Google接続を中断しました。もう一度お試しください。");
                    else
                        SetPhase(
                            HealthScreenPhase.ReadyToConnect,
                            "処理を中断しました。もう一度お試しください。"
                        );
                }
            }
            catch (Exception)
            {
                if (Current(version))
                {
                    if (authenticating)
                        SetGoogleMessage(
                            "Google接続を完了できませんでした。もう一度お試しください。"
                        );
                    else
                    {
                        ClearDays();
                        SetPhase(
                            HealthScreenPhase.Failed,
                            "処理を完了できませんでした。もう一度お試しください。"
                        );
                    }
                }
            }
            finally
            {
                authenticating = false;
                systemDialog = false;
                busy = false;
                operation = null;
                cancellation.Dispose();
                Notify();
                if (resumeRequested && Current(version) && foreground && !signingOut)
                {
                    resumeRequested = false;
                    if (connectionRequested)
                        _ = RunAsync((current, token) => ReadAsync(false, current, token));
                }
            }
        }

        private async Task SignInCoreAsync(int version, CancellationToken token)
        {
            SetGoogleMessage("Googleの画面で認証してください。");
            systemDialog = true;
            var result = await authentication.SignInAsync(lifetime.Token);
            await WaitForForegroundAsync();
            systemDialog = false;
            if (!Current(version) || signingOut)
                return;
            token.ThrowIfCancellationRequested();
            SignedIn = result == GoogleSignInStatus.Success;
            string message = result switch
            {
                GoogleSignInStatus.Success => "Googleに接続済みです。",
                GoogleSignInStatus.NotConfigured =>
                    "Google接続の設定が未完了です。歩数の表示は利用できます。",
                GoogleSignInStatus.Unsupported => "この環境ではGoogle接続を利用できません。",
                _ => "認証を完了できませんでした。キャンセルした場合も、ここからやり直せます。",
            };
            SetGoogleMessage(message);
        }

        private async Task ReadAsync(bool requestPermission, int version, CancellationToken token)
        {
            ClearDays();
            SetPhase(
                HealthScreenPhase.Connecting,
                "Health Connectの利用可否と権限を確認しています…"
            );
            var availability = await health.GetAvailabilityAsync(token);
            if (!Current(version))
                return;
            token.ThrowIfCancellationRequested();
            if (availability != HealthAvailability.Available)
            {
                connected = false;
                SetPhase(
                    availability == HealthAvailability.UpdateRequired
                        ? HealthScreenPhase.UpdateRequired
                        : HealthScreenPhase.Unavailable,
                    availability == HealthAvailability.UpdateRequired
                        ? "Health Connectのインストールまたは更新が必要です。"
                        : "この端末ではHealth Connectを利用できません。"
                );
                return;
            }
            var permission = await health.GetPermissionAsync(token);
            if (!Current(version))
                return;
            token.ThrowIfCancellationRequested();
            if (permission != HealthPermission.Granted && requestPermission)
            {
                systemDialog = true;
                permission = await health.RequestPermissionAsync(lifetime.Token);
                await WaitForForegroundAsync();
                systemDialog = false;
                if (!Current(version))
                    return;
                token.ThrowIfCancellationRequested();
            }
            if (permission != HealthPermission.Granted)
            {
                PermissionRequired();
                return;
            }
            SetPhase(HealthScreenPhase.Reading, "直近7日分の歩数を取得しています…");
            var result = await health.ReadRecentDaysAsync(token);
            if (!Current(version))
                return;
            token.ThrowIfCancellationRequested();
            if (result.Status == HealthReadStatus.PermissionRequired)
            {
                PermissionRequired();
                return;
            }
            if (result.Status == HealthReadStatus.Unavailable)
            {
                connected = false;
                SetPhase(
                    HealthScreenPhase.Unavailable,
                    "Health Connectを利用できません。設定を確認してください。"
                );
                return;
            }
            if (result.Status != HealthReadStatus.Success)
                throw new InvalidOperationException("Health read failed.");
            Days = HealthDaySnapshot.ParseWeek(result.RawJson);
            connected = true;
            bool any = false;
            foreach (var day in Days)
                any |= day.HasValue;
            SetPhase(
                HealthScreenPhase.Ready,
                any
                    ? "取得できました。日付を選ぶとJSONの詳細を確認できます。"
                    : "7日間の歩数データはまだありません。各日のJSONは確認できます。"
            );
        }

        private void PermissionRequired()
        {
            connected = false;
            ClearDays();
            SetPhase(
                HealthScreenPhase.PermissionRequired,
                "歩数の読み取りが許可されていません。再接続するか、設定で許可してください。"
            );
        }

        public void SelectDay(int index)
        {
            if (disposed || IsBusy || !foreground || index < 0 || index >= Days.Count)
                return;
            SelectedDay = Days[index];
            Notify();
        }

        public void CloseDetails()
        {
            SelectedDay = null;
            Notify();
        }

        public void OpenSettings()
        {
            if (!CanOpenSettings)
                return;
            try
            {
                health.OpenSettings();
            }
            catch (Exception)
            {
                SetPhase(
                    HealthScreenPhase.Failed,
                    "設定を開けませんでした。端末の設定からHealth Connectを開いてください。"
                );
            }
        }

        public async Task SignOutAsync()
        {
            if (disposed || signingOut)
                return;
            signingOut = true;
            SignedIn = false;
            SetGoogleMessage("Google接続を解除しています…");
            try
            {
                // Wait for a pending system dialog before another SDK operation.
                await active;
                if (disposed)
                    return;
                bool success = await authentication.SignOutAsync(lifetime.Token);
                SetGoogleMessage(
                    success
                        ? "Google接続を解除しました。歩数の表示は引き続き利用できます。"
                        : "Google接続を解除できませんでした。もう一度接続してお試しください。"
                );
            }
            catch (Exception)
            {
                if (!disposed)
                    SetGoogleMessage(
                        "Google接続を解除できませんでした。もう一度接続してお試しください。"
                    );
            }
            finally
            {
                signingOut = false;
                Notify();
                if (resumeRequested && !disposed && foreground && connectionRequested)
                    _ = RunAsync((version, token) => ReadAsync(false, version, token));
            }
        }

        public void SetForeground(bool value)
        {
            if (disposed || foreground == value)
                return;
            foreground = value;
            if (!value)
            {
                foregroundSignal = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                // Restore health snapshots after returning from a Google dialog, too.
                if (authenticating)
                    resumeRequested = connectionRequested;
                ClearDays();
                if (!systemDialog)
                    operation?.Cancel();
                Notify();
                return;
            }
            foregroundSignal?.TrySetResult(true);
            if (systemDialog)
                return;
            if (IsBusy)
                resumeRequested = connectionRequested;
            else if (connectionRequested)
                _ = RunAsync((version, token) => ReadAsync(false, version, token));
            else
                Notify();
        }

        private Task WaitForForegroundAsync() =>
            foreground || disposed ? Task.CompletedTask : foregroundSignal.Task;

        private bool Current(int version) => !disposed && version == generation;

        private void ClearDays()
        {
            Days = Array.Empty<HealthDaySnapshot>();
            SelectedDay = null;
        }

        private void SetPhase(HealthScreenPhase phase, string message)
        {
            Phase = phase;
            Message = message;
            Notify();
        }

        private void SetGoogleMessage(string message)
        {
            GoogleMessage = message;
            Notify();
        }

        private void Notify()
        {
            if (!disposed)
                Changed?.Invoke();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            generation++;
            SignedIn = false;
            connected = false;
            ClearDays();
            Changed = null;
            lifetime.Cancel();
            foregroundSignal?.TrySetResult(true);
            lifetime.Dispose();
        }
    }
}
