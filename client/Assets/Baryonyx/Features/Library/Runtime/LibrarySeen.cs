using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Library
{
    /// <summary>Where the entries the player has looked at are kept.</summary>
    public interface ILibrarySeenStore
    {
        string Read(string owner);
        void Write(string owner, string value);
    }

    /// <summary>
    /// Keeps them on the device (doc/rules/data-storage.md, 端末に置くもの): losing them only
    /// marks the entries new again.
    /// </summary>
    public sealed class PlayerPrefsLibrarySeenStore : ILibrarySeenStore
    {
        public const string KeyPrefix = "Library.Seen.";

        public string Read(string owner) => PlayerPrefs.GetString(KeyPrefix + owner, "");

        // 印が消えても困らないため、PlayerPrefs.Save() は呼ばず、終了時の書き込みに任せる。
        public void Write(string owner, string value) =>
            PlayerPrefs.SetString(KeyPrefix + owner, value);
    }

    /// <summary>Keeps them while the app runs (tests).</summary>
    public sealed class MemoryLibrarySeenStore : ILibrarySeenStore
    {
        private readonly Dictionary<string, string> values = new();

        public string Read(string owner) => values.TryGetValue(owner, out var value) ? value : "";

        public void Write(string owner, string value) => values[owner] = value;
    }

    /// <summary>
    /// The entries a player has opened in the library: an entry found and not opened yet is
    /// marked new in the list, and opening its page clears the mark (doc/features/library.md).
    /// </summary>
    public sealed class LibrarySeen
    {
        private const char Separator = '\n';
        private readonly ILibrarySeenStore store;
        private readonly string owner;
        private readonly HashSet<string> seen;

        public LibrarySeen(ILibrarySeenStore store, string owner)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.owner = string.IsNullOrEmpty(owner) ? "local" : owner;
            seen = new HashSet<string>(
                this.store.Read(this.owner).Split(Separator, StringSplitOptions.RemoveEmptyEntries)
            );
        }

        public bool IsNew(LibraryRecords records, LibraryKind kind, string id) =>
            records != null && records.IsFound(kind, id) && !seen.Contains(Key(kind, id));

        /// <summary>Marks the entry seen. True when it was new to the player.</summary>
        public bool MarkSeen(LibraryKind kind, string id)
        {
            if (string.IsNullOrEmpty(id) || !seen.Add(Key(kind, id)))
                return false;
            store.Write(owner, string.Join(Separator, seen));
            return true;
        }

        private static string Key(LibraryKind kind, string id) => LibraryKinds.Key(kind) + ":" + id;
    }
}
