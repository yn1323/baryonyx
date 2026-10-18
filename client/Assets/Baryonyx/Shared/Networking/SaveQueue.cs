using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.Networking
{
    /// <summary>
    /// The one line of writes to the game server the whole app shares (doc/rules/data-storage.md).
    /// Writes go one at a time in the order they were made, so a later change never reaches the
    /// server before an earlier one, and they keep going after the screen that made them closes.
    /// A write that fails tells <see cref="Failed"/>; the open guide screen or Home shows it on
    /// the notice band. Starting an adventure waits for the line to empty (<see cref="IdleAsync"/>).
    /// </summary>
    public static class SaveQueue
    {
        private static Task tail = Task.CompletedTask;
        private static CancellationTokenSource lifetime = new();

        // 保存に失敗したときに、通知の帯へ出す文。
        public static event Action<string> Failed;

        // 送っている、または送るのを待っている書き込みがある。
        public static bool Busy => !tail.IsCompleted;

        /// <summary>
        /// Sends <paramref name="send"/> after every write queued before it. A failure of an
        /// earlier write does not stop it. The token is cancelled only when the app stops.
        /// </summary>
        public static Task<T> Enqueue<T>(Func<CancellationToken, Task<T>> send)
        {
            if (send == null)
                throw new ArgumentNullException(nameof(send));
            var task = Run(tail, send, lifetime.Token);
            tail = task;
            return task;
        }

        private static async Task<T> Run<T>(
            Task previous,
            Func<CancellationToken, Task<T>> send,
            CancellationToken token
        )
        {
            try
            {
                await previous;
            }
            catch (Exception)
            {
                // 前の書き込みの失敗は、その書き込みを出した側が扱う。
            }
            token.ThrowIfCancellationRequested();
            return await send(token);
        }

        /// <summary>Waits until every queued write has been sent (saved or failed).</summary>
        public static async Task IdleAsync(CancellationToken token)
        {
            while (!tail.IsCompleted)
            {
                var current = tail;
                var cancelled = Task.Delay(Timeout.Infinite, token);
                if (await Task.WhenAny(current, cancelled) == cancelled)
                    token.ThrowIfCancellationRequested();
            }
        }

        // 保存に失敗したことを、開いている画面の通知の帯へ伝える。
        public static void Report(string message)
        {
            if (!string.IsNullOrEmpty(message))
                Failed?.Invoke(message);
        }

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            lifetime.Cancel();
            lifetime.Dispose();
            lifetime = new CancellationTokenSource();
            tail = Task.CompletedTask;
            Failed = null;
        }
    }
}
