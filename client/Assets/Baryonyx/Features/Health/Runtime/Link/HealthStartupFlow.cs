using System;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Health
{
    public enum HealthStartupPhase
    {
        Loading,
        LinkRequired,
        Failed,
        Ready,
    }

    // タイトル画面の起動処理。サーバー接続、連携の確認、同期を終えるまで開始を受け付けない。
    // 連携しない選択（あとで）も開始できる状態として扱い、運動しないと遊べない作りにしない。
    public sealed class HealthStartupFlow
    {
        private readonly HealthStepLink link;
        private bool busy;
        private bool synced;
        private bool awaitingSettings;

        public HealthStartupFlow(HealthStepLink link) =>
            this.link = link ?? throw new ArgumentNullException(nameof(link));

        public HealthStartupPhase Phase { get; private set; } = HealthStartupPhase.Loading;
        public HealthLinkStatus LinkStatus { get; private set; } = HealthLinkStatus.Linked;
        public bool Dismissed { get; private set; }
        public string FailureMessage { get; private set; } = "";

        public event Action Changed;

        public Task StartAsync(CancellationToken token) =>
            Run(
                async () =>
                {
                    await link.ConnectAsync(token);
                    await CheckAndSyncAsync(token);
                },
                token
            );

        public Task RetryAsync(CancellationToken token) =>
            Phase == HealthStartupPhase.Failed ? StartAsync(token) : Task.CompletedTask;

        // モーダルの主ボタン。許可画面を出せない状態では設定を開き、復帰時に確認し直す。
        public Task LinkAsync(CancellationToken token)
        {
            if (Phase != HealthStartupPhase.LinkRequired || busy)
                return Task.CompletedTask;
            if (LinkStatus != HealthLinkStatus.PermissionRequired)
            {
                awaitingSettings = true;
                link.OpenSettings();
                return Task.CompletedTask;
            }
            return Run(
                async () =>
                {
                    var status = await link.RequestAsync(token);
                    if (status == HealthLinkStatus.Linked)
                        await SyncAsync(token);
                    else
                        Show(HealthStartupPhase.LinkRequired, status);
                },
                token
            );
        }

        public void Dismiss()
        {
            if (Phase != HealthStartupPhase.LinkRequired || busy)
                return;
            Dismissed = true;
            awaitingSettings = false;
            Show(HealthStartupPhase.Ready, LinkStatus);
        }

        // アプリから開いた設定画面から戻ったときだけ確認し直す。
        public Task ResumeAsync(CancellationToken token)
        {
            if (!awaitingSettings || busy)
                return Task.CompletedTask;
            awaitingSettings = false;
            return Run(() => CheckAndSyncAsync(token), token);
        }

        // 開始の直前に権限を確かめ直す。trueなら遷移してよい。
        public async Task<bool> ConfirmStartAsync(CancellationToken token)
        {
            if (Phase != HealthStartupPhase.Ready || busy)
                return false;
            HealthLinkStatus status;
            try
            {
                status = await link.CheckAsync(token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // 端末側の確認に失敗しても、起動時の結果で開始を妨げない。
                return true;
            }
            if (status == HealthLinkStatus.Linked)
            {
                if (synced)
                    return true;
                await Run(() => SyncAsync(token), token);
                return Phase == HealthStartupPhase.Ready;
            }
            if (Dismissed)
                return true;
            Show(HealthStartupPhase.LinkRequired, status);
            return false;
        }

        private async Task CheckAndSyncAsync(CancellationToken token)
        {
            var status = await link.CheckAsync(token);
            if (status == HealthLinkStatus.Linked)
                await SyncAsync(token);
            else if (Dismissed)
                Show(HealthStartupPhase.Ready, status);
            else
                Show(HealthStartupPhase.LinkRequired, status);
        }

        private async Task SyncAsync(CancellationToken token)
        {
            Show(HealthStartupPhase.Loading, HealthLinkStatus.Linked);
            var result = await link.SyncAsync(token);
            if (result == HealthSyncStatus.PermissionRequired)
            {
                // 確認から読み取りまでの間に許可が取り消された。確認し直さずに案内へ戻す。
                Show(
                    Dismissed ? HealthStartupPhase.Ready : HealthStartupPhase.LinkRequired,
                    HealthLinkStatus.PermissionRequired
                );
                return;
            }
            if (result == HealthSyncStatus.ReadFailed)
            {
                Fail("歩数を読み取れませんでした");
                return;
            }
            synced = true;
            Show(HealthStartupPhase.Ready, HealthLinkStatus.Linked);
        }

        private async Task Run(Func<Task> operation, CancellationToken token)
        {
            if (busy)
                return;
            busy = true;
            FailureMessage = "";
            Show(HealthStartupPhase.Loading, LinkStatus);
            try
            {
                await operation();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // 通信・端末のエラー内容は画面へ出さず、再試行できる状態にする。
                Fail("通信に失敗しました");
            }
            finally
            {
                busy = false;
                Changed?.Invoke();
            }
        }

        private void Fail(string message)
        {
            FailureMessage = message;
            Show(HealthStartupPhase.Failed, LinkStatus);
        }

        private void Show(HealthStartupPhase phase, HealthLinkStatus status)
        {
            Phase = phase;
            LinkStatus = status;
            Changed?.Invoke();
        }
    }
}
