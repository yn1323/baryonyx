using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;
using UnityEngine;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// The highway's progress while the app runs (doc/rules/data-storage.md): read from
    /// <see cref="Source"/> (the game server, set by the app) each time the map opens, or from the
    /// prototype's progress without one. A move is shown at once and saved behind the walk in
    /// the <see cref="SaveQueue"/>; when it fails, the map goes back to what the server kept.
    /// </summary>
    public static class WorldMapSession
    {
        private static WorldMapMemorySource local;

        /// <summary>The server that keeps the progress. Without it, the prototype's progress is used.</summary>
        public static IWorldMapSource Source { get; set; }

        public static IWorldMapSource SourceOrLocal =>
            Source ?? (local ??= new WorldMapMemorySource());

        /// <summary>The progress last read or moved to; null before the first read.</summary>
        public static WorldMapProgress Known { get; private set; }

        public const string SaveFailedMessage = "街道の進み具合を保存できませんでした";

        public static async Task<WorldMapProgress> LoadAsync(CancellationToken token)
        {
            var progress = await SourceOrLocal.LoadAsync(token);
            if (!token.IsCancellationRequested && progress != null)
                Known = progress;
            return progress;
        }

        /// <summary>
        /// Keeps the move at once and saves it after the writes before it. True when it was saved;
        /// false when it failed, after reading the kept progress again.
        /// </summary>
        public static async Task<bool> MoveAsync(string squareId, bool clear)
        {
            var source = SourceOrLocal;
            Known = (Known ?? WorldMapProgress.Start).Moved(squareId, clear);
            try
            {
                var saved = await SaveQueue.Enqueue(token =>
                    source.MoveAsync(squareId, clear, token)
                );
                // あとから出した移動がまだ送られていなければ、手元の進み具合のほうが新しい。
                if (saved != null && !SaveQueue.Busy)
                    Known = saved;
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(SaveFailedMessage + "。" + exception.Message);
                try
                {
                    await LoadAsync(CancellationToken.None);
                }
                catch (Exception)
                {
                    // 読み直せなければ、次に地図を開いたときに読み直す。
                }
                return false;
            }
        }

        // Play Modeに入るたびに、前の接続先と進み具合を持ち越さない。接続先はアプリが設定し直す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Source = null;
            Known = null;
            local = null;
        }
    }
}
