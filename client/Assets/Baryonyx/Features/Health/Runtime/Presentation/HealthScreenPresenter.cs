using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.ExerciseRewards;
using Baryonyx.Networking;

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
        private const string ConnectionPrompt =
            "Health Connectに接続し、「歩数」の読み取りを許可してください。";
        private readonly IGoogleSignInProvider authentication;
        private readonly IHealthDataProvider health;
        private readonly HealthServerSync rewards;
        private readonly HealthScreenOperations operations = new();
        private bool authenticating;
        private bool signingOut;
        private bool resumeRequested;
        private bool connected;
        private bool connectionRequested;
        private bool initialized;
        private bool requirementCheckPending;
        private bool checkingRequirements;
        private HealthDay[] latestHealthDays = Array.Empty<HealthDay>();
        private int rewardClaimVersion;

        private enum SettingsVisit
        {
            None,
            WaitingForDeparture,
            WaitingForReturn,
        }

        private SettingsVisit settingsVisit;
        private HealthRequirementState requirementState;

        public event Action Changed;
        public HealthScreenPhase Phase { get; private set; } = HealthScreenPhase.ReadyToConnect;
        public bool SignedIn { get; private set; }
        public bool IsBusy => operations.IsBusy || signingOut;
        public bool HasReadFailures { get; private set; }
        public bool RequiresStepsPermission { get; private set; }
        public HealthRequirementMessage RequirementNotice { get; private set; } =
            new(HealthRequirementCode.Ready);
        public string StatusMessage =>
            checkingRequirements ? "歩数の利用条件を確認しています…"
            : IsBusy
            || Phase == HealthScreenPhase.Failed
            || HasReadFailures
            || RequiresStepsPermission
                ? Message
            : RequirementNotice.HasNotice ? RequirementNotice.Text
            : Message;
        public string GoogleMessage { get; private set; } = "未接続。歩数の表示には不要です。";
        public string Message { get; private set; } = ConnectionPrompt;
        public string RewardMessage { get; private set; } = "運動報酬サーバーは未設定です。";
        public long RuneBalance { get; private set; }
        public long LastGrantedRunes { get; private set; }
        public int RewardClaimVersion => rewardClaimVersion;
        public IReadOnlyList<ExerciseRewardsApiClient.RewardDay> RewardDays { get; private set; } =
            Array.Empty<ExerciseRewardsApiClient.RewardDay>();
        public IReadOnlyList<HealthDaySnapshot> Days { get; private set; } =
            Array.Empty<HealthDaySnapshot>();
        public HealthDaySnapshot SelectedDay { get; private set; }
        public bool CanSignIn =>
            !operations.IsDisposed && operations.IsForeground && !SignedIn && !IsBusy;
        public bool CanConnect => !operations.IsDisposed && operations.IsForeground && !IsBusy;
        public bool CanRefresh => CanConnect && connected;
        public bool RewardsEnabled => rewards != null;
        public bool CanClaimRewards =>
            rewards != null
            && rewards.IsSignedIn
            && latestHealthDays.Length == 7
            && !operations.IsDisposed
            && operations.IsForeground
            && !IsBusy;
        public bool CanOpenSettings =>
            CanConnect
            && (
                Phase == HealthScreenPhase.PermissionRequired
                || Phase == HealthScreenPhase.Unavailable
                || Phase == HealthScreenPhase.UpdateRequired
                || Phase == HealthScreenPhase.Ready
                || RequirementNotice.HasNotice
            );

        public HealthScreenPresenter(
            IGoogleSignInProvider authentication,
            IHealthDataProvider health,
            HealthServerSync rewards = null
        )
        {
            this.authentication =
                authentication ?? throw new ArgumentNullException(nameof(authentication));
            this.health = health ?? throw new ArgumentNullException(nameof(health));
            this.rewards = rewards;
            if (rewards != null)
                RewardMessage = "Google接続後に運動報酬を取得できます。";
        }

        public Task InitializeAsync()
        {
            if (operations.IsDisposed || initialized || health is not IHealthRequirementProvider)
                return Task.CompletedTask;
            initialized = true;
            requirementCheckPending = true;
            return RunPendingAsync();
        }

        private Task RunPendingAsync()
        {
            if (operations.IsDisposed || !operations.IsForeground || IsBusy)
                return Task.CompletedTask;
            if (requirementCheckPending)
            {
                requirementCheckPending = false;
                return RunAsync(CheckRequirementsAsync);
            }
            if (resumeRequested && connectionRequested)
                return RunAsync((version, token) => ReadAsync(false, version, token));
            return Task.CompletedTask;
        }

        private async Task CheckRequirementsAsync(int version, CancellationToken token)
        {
            checkingRequirements = true;
            Notify();
            try
            {
                var state = await ((IHealthRequirementProvider)health).GetRequirementsAsync(token);
                token.ThrowIfCancellationRequested();
                if (!Current(version) || !operations.IsForeground)
                    return;
                requirementState = state ?? new HealthRequirementState();
                RequirementNotice = HealthRequirementCheck.Evaluate(requirementState);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                if (Current(version))
                    requirementCheckPending = true;
            }
            catch (Exception)
            {
                if (Current(version) && !token.IsCancellationRequested)
                {
                    requirementState = new HealthRequirementState();
                    RequirementNotice = HealthRequirementCheck.Evaluate(requirementState);
                }
            }
            // Returning from settings also restores any previously requested health list.
            if (Current(version) && connectionRequested)
                resumeRequested = true;
        }

        private void UpdateRequirementHealth(
            HealthAvailability? availability,
            HealthPermission permission,
            HealthStepsDataState data
        )
        {
            if (requirementState == null)
                return;
            requirementState = requirementState.WithHealthState(availability, permission, data);
            RequirementNotice = HealthRequirementCheck.Evaluate(requirementState);
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
            return operations.RunAsync(
                (version, token) => RunCoreAsync(action, version, token),
                OperationCompleted
            );
        }

        private void OperationCompleted()
        {
            authenticating = false;
            checkingRequirements = false;
            Notify();
            if (!operations.IsDisposed)
                _ = RunPendingAsync();
        }

        private async Task RunCoreAsync(
            Func<int, CancellationToken, Task> action,
            int version,
            CancellationToken token
        )
        {
            try
            {
                await action(version, token);
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
        }

        private async Task SignInCoreAsync(int version, CancellationToken token)
        {
            SetGoogleMessage("Googleの画面で認証してください。");
            var result = await operations.RunSystemDialogAsync(authentication.SignInAsync);
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
            if (SignedIn && rewards != null)
            {
                if (
                    authentication is not IGoogleCredentialProvider credentials
                    || string.IsNullOrWhiteSpace(credentials.ServerIdToken)
                )
                {
                    RewardMessage =
                        "Google接続は完了しましたが、サーバー認証情報を取得できません。";
                }
                else
                {
                    try
                    {
                        await rewards.SignInAsync(credentials.ServerIdToken, token);
                        var balance = await rewards.ReadBalanceAsync(token);
                        RuneBalance = balance.balance;
                        RewardMessage = "運動報酬サーバーに接続しました。";
                        if (connected && latestHealthDays.Length == 7)
                            await SyncAndApplyRewardsAsync(token);
                    }
                    catch (Exception)
                    {
                        RewardMessage =
                            "運動報酬を取得できませんでした。あとで再試行してください。";
                    }
                    Notify();
                }
            }
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
                UpdateRequirementHealth(
                    availability,
                    HealthPermission.Unknown,
                    HealthStepsDataState.NotChecked
                );
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
            // An explicit connection also offers data types added since the previous version.
            if (requestPermission)
            {
                permission = await operations.RunSystemDialogAsync(health.RequestPermissionAsync);
                if (!Current(version))
                    return;
                token.ThrowIfCancellationRequested();
            }
            if (permission != HealthPermission.Granted)
            {
                PermissionRequired();
                return;
            }
            SetPhase(HealthScreenPhase.Reading, "直近7日分の健康データを取得しています…");
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
                UpdateRequirementHealth(
                    HealthAvailability.Unavailable,
                    HealthPermission.Unknown,
                    HealthStepsDataState.NotChecked
                );
                connected = false;
                SetPhase(
                    HealthScreenPhase.Unavailable,
                    "Health Connectを利用できません。設定を確認してください。"
                );
                return;
            }
            if (result.Status != HealthReadStatus.Success)
                throw new InvalidOperationException("Health read failed.");
            latestHealthDays = result.Days ?? Array.Empty<HealthDay>();
            Days = HealthDaySnapshot.ParseWeek(result.RawJson);
            connected = true;
            bool any = false;
            bool anySteps = false;
            bool stepsFailed = false;
            foreach (var day in Days)
            {
                any |= day.HasValue || day.HasAdditionalData;
                anySteps |= day.HasValue;
                stepsFailed |= day.StepsStatus == "failed";
                HasReadFailures |= day.HasReadFailures;
                RequiresStepsPermission |= day.StepsStatus == "permission_required";
            }
            UpdateRequirementHealth(
                HealthAvailability.Available,
                RequiresStepsPermission ? HealthPermission.NotGranted : HealthPermission.Granted,
                RequiresStepsPermission ? HealthStepsDataState.NotChecked
                    : anySteps ? HealthStepsDataState.Present
                    : stepsFailed ? HealthStepsDataState.Failed
                    : HealthStepsDataState.Empty
            );
            SetPhase(
                HealthScreenPhase.Ready,
                RequiresStepsPermission
                        ? "歩数の読み取りが未許可です。「歩数の読み取りを許可」から許可してください。ほかの健康データは日付から確認できます。"
                    : HasReadFailures
                        ? "一部の項目を取得できませんでした。JSONで状態を確認し、更新してください。"
                    : any ? "取得できました。日付を選ぶと健康データのJSONを確認できます。"
                    : "許可された項目に7日間の記録はありません。各日のJSONで状態を確認できます。"
            );
            if (rewards != null && rewards.IsSignedIn)
                await SyncAndApplyRewardsAsync(token);
            else if (rewards != null)
            {
                RewardMessage = "Google接続後に、今回の歩数をルーンへ変換できます。";
                Notify();
            }
        }

        public Task ClaimRewardsAsync()
        {
            if (!CanClaimRewards)
                return Task.CompletedTask;
            return RunAsync(ClaimRewardsCoreAsync);
        }

        private async Task ClaimRewardsCoreAsync(int version, CancellationToken token)
        {
            // 明示操作ではHealth Connectを読み直してから同じ保存・請求処理を行う。
            await ReadAsync(false, version, token);
        }

        private async Task SyncAndApplyRewardsAsync(CancellationToken token)
        {
            try
            {
                var result = await rewards.SyncAndClaimAsync(latestHealthDays, token);
                LastGrantedRunes = result.grantedRunes;
                RuneBalance = result.balance;
                RewardDays = result.days ?? Array.Empty<ExerciseRewardsApiClient.RewardDay>();
                rewardClaimVersion++;
                RewardMessage =
                    result.grantedRunes > 0
                        ? $"{result.grantedRunes:N0}ルーンを取得しました。残高 {result.balance:N0}ルーン"
                        : $"新しく取得できるルーンはありません。残高 {result.balance:N0}ルーン";
            }
            catch (ServerApiException exception)
            {
                RewardMessage =
                    exception.StatusCode == 401
                        ? "運動報酬のセッションが切れています。Googleに再接続してください。"
                        : "運動報酬を取得できませんでした。あとで再試行してください。";
            }
            catch (Exception)
            {
                RewardMessage = "運動報酬を取得できませんでした。あとで再試行してください。";
            }
            Notify();
        }

        private void PermissionRequired()
        {
            UpdateRequirementHealth(
                HealthAvailability.Available,
                HealthPermission.NotGranted,
                HealthStepsDataState.NotChecked
            );
            connected = false;
            ClearDays();
            SetPhase(
                HealthScreenPhase.PermissionRequired,
                "健康データの読み取り許可を確認してください。再接続するか、設定で項目を選んでください。"
            );
        }

        public void SelectDay(int index)
        {
            if (
                operations.IsDisposed
                || IsBusy
                || !operations.IsForeground
                || index < 0
                || index >= Days.Count
            )
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
            _ = RunAsync(OpenSettingsCoreAsync);
        }

        private async Task OpenSettingsCoreAsync(int version, CancellationToken token)
        {
            settingsVisit = SettingsVisit.WaitingForDeparture;
            operations.BeginSystemDialog();
            Notify();
            try
            {
                if (health is IHealthRequirementProvider requirements)
                {
                    bool opened = await requirements.OpenSettingsAsync(
                        RequirementNotice.Destination,
                        operations.LifetimeToken
                    );
                    if (!Current(version))
                        return;
                    if (!opened)
                        throw new InvalidOperationException("Settings did not open.");
                }
                else
                    health.OpenSettings();
                // A successful retry must not leave the previous launch error above new notices.
                if (Current(version) && Phase == HealthScreenPhase.Failed && !connectionRequested)
                    SetPhase(HealthScreenPhase.ReadyToConnect, ConnectionPrompt);
            }
            catch (Exception)
            {
                settingsVisit = SettingsVisit.None;
                requirementCheckPending = false;
                if (Current(version))
                    SetPhase(
                        HealthScreenPhase.Failed,
                        "設定を開けませんでした。端末の設定からHealth Connectまたはシステム更新を確認してください。"
                    );
            }
        }

        public async Task SignOutAsync()
        {
            if (operations.IsDisposed || signingOut)
                return;
            signingOut = true;
            SignedIn = false;
            SetGoogleMessage("Google接続を解除しています…");
            try
            {
                // Wait for a pending system dialog before another SDK operation.
                await operations.Active;
                if (operations.IsDisposed)
                    return;
                bool success = await authentication.SignOutAsync(operations.LifetimeToken);
                if (rewards != null)
                {
                    try
                    {
                        await rewards.SignOutAsync(operations.LifetimeToken);
                    }
                    catch (Exception) { }
                    RewardDays = Array.Empty<ExerciseRewardsApiClient.RewardDay>();
                    RuneBalance = 0;
                    LastGrantedRunes = 0;
                    RewardMessage = "Google接続を解除しました。";
                }
                SetGoogleMessage(
                    success
                        ? "Google接続を解除しました。歩数の表示は引き続き利用できます。"
                        : "Google接続を解除できませんでした。もう一度接続してお試しください。"
                );
            }
            catch (Exception)
            {
                if (!operations.IsDisposed)
                    SetGoogleMessage(
                        "Google接続を解除できませんでした。もう一度接続してお試しください。"
                    );
            }
            finally
            {
                signingOut = false;
                Notify();
                _ = RunPendingAsync();
            }
        }

        public void SetForeground(bool value)
        {
            if (!operations.SetForeground(value))
                return;
            if (!value)
            {
                if (settingsVisit == SettingsVisit.WaitingForDeparture)
                    settingsVisit = SettingsVisit.WaitingForReturn;
                // Restore health snapshots after returning from a Google dialog, too.
                if (authenticating)
                    resumeRequested = connectionRequested;
                ClearDays();
                operations.CancelBackgroundOperation();
                Notify();
                return;
            }
            if (settingsVisit == SettingsVisit.WaitingForReturn)
            {
                settingsVisit = SettingsVisit.None;
                requirementCheckPending = health is IHealthRequirementProvider;
            }
            if (operations.IsSystemDialog)
                return;
            if (IsBusy)
                resumeRequested = connectionRequested;
            else
            {
                resumeRequested = connectionRequested;
                _ = RunPendingAsync();
                Notify();
            }
        }

        private bool Current(int version) => operations.IsCurrent(version);

        private void ClearDays()
        {
            HasReadFailures = false;
            RequiresStepsPermission = false;
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
            if (!operations.IsDisposed)
                Changed?.Invoke();
        }

        public void Dispose()
        {
            if (operations.IsDisposed)
                return;
            SignedIn = false;
            connected = false;
            latestHealthDays = Array.Empty<HealthDay>();
            ClearDays();
            rewards?.Dispose();
            Changed = null;
            operations.Dispose();
        }
    }
}
