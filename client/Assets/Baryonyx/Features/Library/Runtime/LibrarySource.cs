using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;

namespace Baryonyx.Library
{
    /// <summary>Where the library's records are read. The library only reads them.</summary>
    public interface ILibrarySource
    {
        Task<LibraryRecords> LoadAsync(CancellationToken token);

        // 新しく載った印を消すための「もう見た」記録を、利用者ごとに分けるための名前。読む前は空。
        string Owner { get; }
    }

    /// <summary>Reads the library's records from the game server with the shared session.</summary>
    public sealed class LibraryServerSource : ILibrarySource
    {
        private readonly IAccountSessionRunner sessions;
        private readonly LibraryApiClient api;

        public LibraryServerSource(IAccountSessionRunner sessions, LibraryApiClient api)
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public string Owner { get; private set; } = "";

        public Task<LibraryRecords> LoadAsync(CancellationToken token) =>
            sessions.WithSessionAsync(
                async session =>
                {
                    Owner = session.UserId ?? "";
                    // 見つけた物と歩数は互いに依存しないため、同時に読む。
                    var state = api.ReadAsync(session, token);
                    var steps = api.ReadStepsAsync(session, token);
                    await Task.WhenAll(state, steps);
                    return ToRecords(await state, await steps);
                },
                token
            );

        // 知らない分類や、IDのない行は外す。
        public static LibraryRecords ToRecords(
            LibraryApiClient.State state,
            LibraryApiClient.Steps steps
        )
        {
            var discoveries = new List<(LibraryKind, string, int)>();
            foreach (var entry in state?.discoveries ?? Array.Empty<LibraryApiClient.Discovery>())
                if (
                    entry != null
                    && !string.IsNullOrEmpty(entry.entryId)
                    && LibraryKinds.TryParse(entry.kind, out var kind)
                )
                    discoveries.Add((kind, entry.entryId, entry.defeated));
            var days = (steps?.days ?? Array.Empty<LibraryApiClient.Day>())
                .Where(day => day != null && !string.IsNullOrEmpty(day.day))
                .OrderByDescending(day => day.day, StringComparer.Ordinal)
                .Select(day => new LibraryStepDay(day.day, day.steps))
                .ToArray();
            return new LibraryRecords(discoveries, days);
        }
    }

    /// <summary>
    /// Mock records without a server (the showcase, or no server URL): the first characters, the
    /// starting cards, part of the equipment and a few of the first destinations' items found,
    /// the slime met and beaten, and a week of steps, so the library and the travel office's
    /// treasures show both the found and the shadowed entries.
    /// </summary>
    public sealed class LibraryMemorySource : ILibrarySource
    {
        private readonly LibraryRecords records;

        public LibraryMemorySource(LibraryRecords records = null) =>
            this.records = records ?? Mock();

        public string Owner => "local";

        public Task<LibraryRecords> LoadAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(records);
        }

        public static LibraryRecords Mock()
        {
            var found = new List<(LibraryKind, string, int)>();
            void Add(LibraryKind kind, int defeated, params string[] ids) =>
                found.AddRange(ids.Select(id => (kind, id, defeated)));
            Add(LibraryKind.Character, 0, "toma", "luka", "aria", "mina");
            Add(
                LibraryKind.Skill,
                0,
                "Fire",
                "Ice",
                "VitalThrust",
                "Thunder",
                "Slash",
                "ShieldBash",
                "Heal",
                "HolyHammer"
            );
            Add(LibraryKind.Weapon, 0, "oak-staff", "short-bow", "iron-sword", "war-hammer");
            Add(LibraryKind.Armor, 0, "magic-robe", "traveler-cloak", "chainmail", "leather-armor");
            Add(LibraryKind.Accessory, 0, "moss-charm", "acorn-brooch");
            Add(LibraryKind.Item, 0, "herb", "acorn", "moss-wad", "dried-fish");
            found.Add((LibraryKind.Enemy, "moss-slime", 12));
            found.Add((LibraryKind.Enemy, "moss-wolf", 3));
            var steps = new[]
            {
                new LibraryStepDay("2026-10-18", 3_240),
                new LibraryStepDay("2026-10-17", 8_412),
                new LibraryStepDay("2026-10-16", 6_105),
                new LibraryStepDay("2026-10-15", 10_230),
                new LibraryStepDay("2026-10-14", 4_870),
                new LibraryStepDay("2026-10-13", 7_655),
                new LibraryStepDay("2026-10-12", 2_310),
            };
            return new LibraryRecords(found, steps);
        }
    }
}
