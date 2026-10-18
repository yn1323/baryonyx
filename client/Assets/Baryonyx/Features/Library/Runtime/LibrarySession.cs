using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.Library
{
    /// <summary>
    /// The library's records while the app runs: read from <see cref="Source"/> (the game server,
    /// set by the app) each time a panel opens, or from the mock records without one. Keeps the
    /// entries the player has opened (<see cref="Seen"/>) on the device.
    /// </summary>
    public static class LibrarySession
    {
        private static LibraryMemorySource local;
        private static LibrarySeen seen;
        private static string seenOwner;

        /// <summary>The server that keeps the records. Without it, the mock records are shown.</summary>
        public static ILibrarySource Source { get; set; }

        public static ILibrarySource SourceOrLocal =>
            Source ?? (local ??= new LibraryMemorySource());

        /// <summary>Where the opened entries are kept. Tests replace it.</summary>
        public static ILibrarySeenStore SeenStore { get; set; } = new PlayerPrefsLibrarySeenStore();

        /// <summary>The records last read; empty before the first read.</summary>
        public static LibraryRecords Records { get; private set; } = LibraryRecords.Empty;

        /// <summary>The entries the player of the last read has opened.</summary>
        public static LibrarySeen Seen
        {
            get
            {
                string owner = SourceOrLocal.Owner;
                if (seen == null || seenOwner != owner)
                {
                    seen = new LibrarySeen(SeenStore, owner);
                    seenOwner = owner;
                }
                return seen;
            }
        }

        public static async Task<LibraryRecords> LoadAsync(CancellationToken token)
        {
            var records = await SourceOrLocal.LoadAsync(token);
            if (!token.IsCancellationRequested)
                Records = records ?? LibraryRecords.Empty;
            return Records;
        }

        // Play Modeに入るたびに、前の接続先・記録・読み手を持ち越さない。接続先はアプリが設定し直す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Source = null;
            Records = LibraryRecords.Empty;
            seen = null;
            seenOwner = null;
            local = null;
        }
    }
}
