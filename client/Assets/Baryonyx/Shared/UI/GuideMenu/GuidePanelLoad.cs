using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.UI.GuideMenu
{
    /// <summary>
    /// The read a guide screen's panel makes each time it opens (the party, the
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

        /// <summary>
        /// Starts <paramref name="load"/>. With a <paramref name="name"/>, a development build logs
        /// the time from opening until it is drawn, beside the request times of
        /// <see cref="Baryonyx.Networking.ServerApi"/>, to tell the drawing from the reading.
        /// </summary>
        public void Start(
            Func<CancellationToken, Task> load,
            Action<Exception> failed,
            string name = null
        )
        {
            Cancel();
            source = new CancellationTokenSource();
            Task = Run(load, failed, name, source.Token);
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
            string name,
            CancellationToken token
        )
        {
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                await load(token);
                if (name != null && Debug.isDebugBuild && !token.IsCancellationRequested)
                    Debug.Log(TimingLine(name, watch.Elapsed.TotalMilliseconds));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested)
                    failed(exception);
            }
        }

        // 計測のログの1行：開いてから描き終えるまでの時間。
        internal static string TimingLine(string name, double milliseconds) =>
            $"[計測] 画面 {name} {Math.Round(milliseconds):0}ms";
    }
}
