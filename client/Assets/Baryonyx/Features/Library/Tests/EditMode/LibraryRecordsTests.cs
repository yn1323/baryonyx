using Baryonyx.Library;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // ライブラリの見つけた記録と、新しく載った印（doc/features/library.md）。
    public sealed class LibraryRecordsTests
    {
        private static LibraryRecords FromServer() =>
            LibraryServerSource.ToRecords(
                new LibraryApiClient.State
                {
                    discoveries = new[]
                    {
                        new LibraryApiClient.Discovery { kind = "character", entryId = "toma" },
                        new LibraryApiClient.Discovery { kind = "skill", entryId = "Fire" },
                        new LibraryApiClient.Discovery
                        {
                            kind = "enemy",
                            entryId = "moss-slime",
                            defeated = 4,
                        },
                        // 知らない分類やIDのない行は外す。
                        new LibraryApiClient.Discovery { kind = "dragon", entryId = "x" },
                        new LibraryApiClient.Discovery { kind = "weapon", entryId = "" },
                        null,
                    },
                },
                new LibraryApiClient.Steps
                {
                    days = new[]
                    {
                        new LibraryApiClient.Day { day = "2026-10-16", steps = 4200 },
                        new LibraryApiClient.Day { day = "2026-10-18", steps = 3240 },
                        new LibraryApiClient.Day { day = "2026-10-17", steps = 8100 },
                    },
                }
            );

        [Test]
        public void ServerRecordsTellWhatWasFoundOfEachKind()
        {
            var records = FromServer();
            Assert.That(records.IsFound(LibraryKind.Character, "toma"), Is.True);
            Assert.That(records.IsFound(LibraryKind.Skill, "Fire"), Is.True);
            // 同じIDでも、分類が違えば見つけていない。
            Assert.That(records.IsFound(LibraryKind.Skill, "toma"), Is.False);
            Assert.That(records.IsFound(LibraryKind.Weapon, ""), Is.False);
            Assert.That(
                records.FoundOf(LibraryKind.Enemy),
                Is.EquivalentTo(new[] { "moss-slime" })
            );
        }

        [Test]
        public void EnemiesKeepHowManyWereBeatenAndUnmetOnesAreZero()
        {
            var records = FromServer();
            Assert.That(records.Defeated("moss-slime"), Is.EqualTo(4));
            Assert.That(records.Defeated("moss-wolf"), Is.EqualTo(0));
        }

        [Test]
        public void DailyStepsAreNewestFirstAndActEqualsSteps()
        {
            var steps = FromServer().Steps;
            Assert.That(steps.Count, Is.EqualTo(3));
            Assert.That(steps[0].Day, Is.EqualTo("2026-10-18"));
            Assert.That(steps[2].Day, Is.EqualTo("2026-10-16"));
            Assert.That(steps[1].Act, Is.EqualTo(8100));
        }

        [Test]
        public void DayTextShowsMonthDayAndWeekday()
        {
            Assert.That(LibraryStepsView.DayText("2026-10-18"), Is.EqualTo("10/18（日）"));
            Assert.That(LibraryStepsView.DayText("nope"), Is.EqualTo("nope"));
        }

        [Test]
        public void FoundEntriesAreNewUntilOpenedAndStayOpenedPerPlayer()
        {
            var records = FromServer();
            var store = new MemoryLibrarySeenStore();
            var seen = new LibrarySeen(store, "player-a");
            Assert.That(seen.IsNew(records, LibraryKind.Character, "toma"), Is.True);
            // 見つけていないものに印は付かない。
            Assert.That(seen.IsNew(records, LibraryKind.Character, "luka"), Is.False);

            Assert.That(seen.MarkSeen(LibraryKind.Character, "toma"), Is.True);
            Assert.That(seen.MarkSeen(LibraryKind.Character, "toma"), Is.False);
            Assert.That(seen.IsNew(records, LibraryKind.Character, "toma"), Is.False);

            // 開き直しても残り、ほかの利用者には残らない。
            Assert.That(
                new LibrarySeen(store, "player-a").IsNew(records, LibraryKind.Character, "toma"),
                Is.False
            );
            Assert.That(
                new LibrarySeen(store, "player-b").IsNew(records, LibraryKind.Character, "toma"),
                Is.True
            );
        }

        [Test]
        public void MockRecordsShowBothFoundAndShadowedEntries()
        {
            var mock = LibraryMemorySource.Mock();
            Assert.That(mock.IsFound(LibraryKind.Character, "toma"), Is.True);
            Assert.That(mock.IsFound(LibraryKind.Character, "ritsu"), Is.False);
            Assert.That(mock.Steps.Count, Is.GreaterThan(0));
        }
    }
}
