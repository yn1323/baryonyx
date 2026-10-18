using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.UI.GuideMenu
{
    /// <summary>
    /// The read a guide screen's panel makes each time it opens (the party, the bonuses, the
    /// equipment...): one at a time, cancelled when the panel closes. A failure other than the
    /// cancel goes to the panel's handler, which shows <see cref="LoadFailedText"/>.
    /// </summary>
    public sealed class GuidePanelLoad
    {
        public const string LoadingText = "読み込み中…";
        public const string LoadFailedText = "取得できませんでした";

        /// <summary>
        /// The states a panel shows while stopped for its read (doc/rules/ui-design.md#停止中の表示の切り替え):
        /// reading, and the read failed.
        /// </summary>
        public static readonly IReadOnlyList<string> PreviewStates = new[]
        {
            "読み込み中",
            "取得できない",
        };

        private CancellationTokenSource source;

        // 実行中または直前の読み込み。テストで完了を待つために公開する。
        public Task Task { get; private set; } = Task.CompletedTask;

        public void Start(Func<CancellationToken, Task> load, Action<Exception> failed)
        {
            Cancel();
            source = new CancellationTokenSource();
            Task = Run(load, failed, source.Token);
        }

        public void Cancel()
        {
            source?.Cancel();
            source?.Dispose();
            source = null;
        }

        private static async Task Run(
            Func<CancellationToken, Task> load,
            Action<Exception> failed,
            CancellationToken token
        )
        {
            try
            {
                await load(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested)
                    failed(exception);
            }
        }
    }
}
