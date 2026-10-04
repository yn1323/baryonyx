using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// Sets out on an adventure from the travel office's map (doc/features/screens.md): asks
    /// first, with the record of the destination and what the adventure holds fast (the UPT
    /// bonus slots cannot change on the way; what is found stays even after a defeat), then
    /// starts the adventure on the server at the entrance and opens the exploration.
    /// </summary>
    public sealed class TravelDeparture : IDisposable
    {
        public const string GoChoice = "出発する";
        public const string StayChoice = "やめる";
        public const string InProgressMessage =
            "冒険の途中です。ホームの行き先カードから再開してください";

        public const string Rules =
            "冒険の途中は、酒場でボーナスを付け替えられません。\n負けても、手に入れた物は持ち帰れます。";

        private readonly GameDialog dialog;
        private readonly Action<string> notice;
        private readonly IAdventureSource source;
        private readonly Func<bool> explore;
        private readonly CancellationTokenSource lifetime = new();
        private bool disposed;

        public TravelDeparture(
            GameDialog dialog,
            Action<string> notice,
            IAdventureSource source,
            Func<bool> explore
        )
        {
            this.dialog = dialog != null ? dialog : throw new ArgumentNullException(nameof(dialog));
            this.notice = notice ?? (_ => { });
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.explore = explore ?? throw new ArgumentNullException(nameof(explore));
            Work = LoadAsync();
        }

        public bool Leaving { get; private set; }

        // 実行中または直前の読み込み・出発。テストで完了を待つために公開する。
        public Task Work { get; private set; }

        // 行き先ごとの記録を見せるため、開いたときに読んでおく。
        private async Task LoadAsync()
        {
            try
            {
                AdventureSession.Use(await source.LoadAsync(lifetime.Token));
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogWarning("冒険の記録を取得できませんでした。" + exception.Message);
            }
        }

        /// <summary>
        /// Asks before setting out for <paramref name="destinationId"/>. False for a place the
        /// adventure cannot go to yet, so the map tells it is coming.
        /// </summary>
        public bool Depart(string destinationId, string name)
        {
            if (disposed || Leaving || string.IsNullOrEmpty(destinationId))
                return false;
            if (AdventureSession.Current?.InProgress ?? false)
            {
                notice(InProgressMessage);
                return true;
            }
            var record = AdventureSession.Current?.RecordOf(destinationId);
            string history =
                record == null ? "はじめて訪れる場所です。"
                : record.Clears > 0
                    ? $"最深到達 {AdventureCatalog.FloorText(record.BestFloor)}（踏破 {record.Clears}回）"
                : $"最深到達 {AdventureCatalog.FloorText(record.BestFloor)}";
            dialog.Show(
                $"{name}へ出発しますか？",
                history + "\n" + Rules,
                null,
                dialog.Hide,
                new DialogChoice(GoChoice, () => Work = StartAsync(destinationId)),
                new DialogChoice(StayChoice, dialog.Hide)
            );
            return true;
        }

        private async Task StartAsync(string destinationId)
        {
            AdventureState state;
            try
            {
                state = await source.StartAsync(destinationId, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("冒険を始められませんでした。" + exception.Message);
                if (disposed)
                    return;
                dialog.Hide();
                notice(
                    exception is ServerApiException { StatusCode: 409 }
                        ? InProgressMessage
                        : "冒険を始められませんでした"
                );
                return;
            }
            if (disposed)
                return;
            AdventureSession.Use(state);
            Leaving = explore();
            if (!Leaving)
            {
                dialog.Hide();
                notice("探索を開けませんでした");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            lifetime.Cancel();
        }
    }
}
