using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.Library
{
    /// <summary>
    /// The library's records while the app runs: read from <see cref="Source"/> (the game server,
    /// set by the app) each time a panel opens, or from the mock records without one. A panel
    /// opened again shows the records read before (<see cref="Known"/>) while it reads them
    /// again. Keeps the entries the player has opened (<see cref="Seen"/>) on the device.
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

        // 最後に読み終えた記録を読んだ置き場。今の Source と違えば、その記録は先に出さない。
        private static ILibrarySource readFrom;

        // 読み始めた回数と、使った読み込みの番号。先に始めた読み込みが後から届いても、新しい記録を戻さない。
        private static int started;
        private static int used;

        /// <summary>
        /// The records read from the server earlier in this run, which a panel shows at once while
        /// it reads them again; null before the first read or without a server, so the empty
        /// records before a read are never shown as the player's.
        /// </summary>
        public static LibraryRecords Known => Source != null && readFrom == Source ? Records : null;

        /// <summary>The fingerprint of <see cref="Known"/>, compared after reading again; null without it.</summary>
        public static string ReadVersion => Known?.Fingerprint;

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
            var source = SourceOrLocal;
            int at = ++started;
            var records = await source.LoadAsync(token);
            if (!token.IsCancellationRequested && at > used)
            {
                used = at;
                Records = records ?? LibraryRecords.Empty;
                readFrom = source;
            }
            return Records;
        }

        // Play Modeに入るたびに、前の接続先・記録・読み手を持ち越さない。接続先はアプリが設定し直す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Source = null;
            Records = LibraryRecords.Empty;
            readFrom = null;
            // 前のPlay Modeで始めた読み込みが後から届いても使わない。
            used = started;
            seen = null;
            seenOwner = null;
            local = null;
        }
    }
}
