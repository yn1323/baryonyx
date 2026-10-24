using System;
using System.Collections.Generic;
using System.Text;

namespace Baryonyx.Library
{
    /// <summary>One day of the daily steps: the day (yyyy-MM-dd) and the steps walked.</summary>
    public readonly struct LibraryStepDay
    {
        public LibraryStepDay(string day, int steps)
        {
            Day = day ?? "";
            Steps = Math.Max(0, steps);
        }

        public string Day { get; }
        public int Steps { get; }

        // 1歩＝1ACT（doc/features/exercise-rewards.md のゲーム共通の単位）。
        public int Act => Steps;
    }

    /// <summary>
    /// What the player has found (doc/features/library.md): the entries of each kind found at
    /// least once, the enemies' defeats, and the daily steps, newest first. The definitions
    /// (names, art) are the features' own; this only says which ids were found.
    /// </summary>
    public sealed class LibraryRecords
    {
        public static readonly LibraryRecords Empty = new(
            Array.Empty<(LibraryKind, string, int)>(),
            Array.Empty<LibraryStepDay>()
        );

        private readonly Dictionary<(LibraryKind Kind, string Id), int> found = new();

        public LibraryRecords(
            IEnumerable<(LibraryKind Kind, string Id, int Defeated)> discoveries,
            IReadOnlyList<LibraryStepDay> steps
        )
        {
            foreach (
                var (kind, id, defeated) in discoveries ?? Array.Empty<(LibraryKind, string, int)>()
            )
                if (!string.IsNullOrEmpty(id))
                    found[(kind, id)] = Math.Max(0, defeated);
            Steps = steps ?? Array.Empty<LibraryStepDay>();
        }

        public IReadOnlyList<LibraryStepDay> Steps { get; }

        private string fingerprint;

        /// <summary>
        /// The records as one string, the same for the same records in any order of reading:
        /// compared after reading again, so a panel keeps what it shows when nothing changed.
        /// </summary>
        public string Fingerprint => fingerprint ??= FingerprintOf();

        private string FingerprintOf()
        {
            var lines = new List<string>(found.Count);
            foreach (var pair in found)
                lines.Add($"{pair.Key.Kind}:{pair.Key.Id}:{pair.Value}");
            lines.Sort(StringComparer.Ordinal);
            var text = new StringBuilder(string.Join("\n", lines));
            foreach (var day in Steps)
                text.Append('\n').Append(day.Day).Append('=').Append(day.Steps);
            return text.ToString();
        }

        public bool IsFound(LibraryKind kind, string id) =>
            !string.IsNullOrEmpty(id) && found.ContainsKey((kind, id));

        /// <summary>How many of the enemy the party has beaten; 0 for one never met.</summary>
        public int Defeated(string enemyId) =>
            found.TryGetValue((LibraryKind.Enemy, enemyId ?? ""), out int count) ? count : 0;

        /// <summary>The ids found of the kind, in no particular order.</summary>
        public IEnumerable<string> FoundOf(LibraryKind kind)
        {
            foreach (var key in found.Keys)
                if (key.Kind == kind)
                    yield return key.Id;
        }
    }
}
