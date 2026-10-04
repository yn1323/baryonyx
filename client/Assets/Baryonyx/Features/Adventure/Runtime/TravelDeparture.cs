using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// Sets out on an adventure from the travel office (doc/features/screens.md): the depart
    /// button starts the adventure on the server at the entrance and opens the exploration at
    /// once, without asking.
    /// </summary>
    public sealed class TravelDeparture : IDisposable
    {
        public const string InProgressMessage =
            "冒険の途中です。ホームの行き先カードから再開してください";

        private readonly Action<string> notice;
        private readonly IAdventureSource source;
        private readonly Func<bool> explore;
        private readonly CancellationTokenSource lifetime = new();
        private bool starting;
        private bool disposed;

        public TravelDeparture(Action<string> notice, IAdventureSource source, Func<bool> explore)
        {
            this.notice = notice ?? (_ => { });
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.explore = explore ?? throw new ArgumentNullException(nameof(explore));
            Work = LoadAsync();
        }

        public bool Leaving { get; private set; }

        // 実行中または直前の読み込み・出発。テストで完了を待つために公開する。
        public Task Work { get; private set; }

        // 冒険の途中かを確かめるため、開いたときに読んでおく。
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
        /// Sets out for <paramref name="destinationId"/> at once. False for a place the
        /// adventure cannot go to yet, so the list tells it is coming.
        /// </summary>
        public bool Depart(string destinationId)
        {
            if (disposed || string.IsNullOrEmpty(destinationId))
                return false;
            // 始めている間と探索へ移る途中の連打は受け流し、冒険を1回だけ始める。
            if (starting)
                return true;
            if (AdventureSession.Current?.InProgress ?? false)
            {
                notice(InProgressMessage);
                return true;
            }
            starting = true;
            Work = StartAsync(destinationId);
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
                starting = false;
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
                starting = false;
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
