using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Library;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 前に読んだライブラリの記録をすぐ出し、裏で読み直す流れ（パネルを開き直したとき）。
    public sealed class LibrarySessionReadAgainTests
    {
        // 読み込みの答えを待たせられるサーバー。読み込みは、読み始めたときの記録を返す。
        private sealed class FakeServer : ILibrarySource
        {
            public LibraryRecords Records = LibraryRecords.Empty;
            public TaskCompletionSource<bool> Gate;

            public string Owner => "test";

            public async Task<LibraryRecords> LoadAsync(CancellationToken token)
            {
                var read = Records;
                var gate = Gate;
                if (gate != null)
                    await gate.Task;
                return read;
            }
        }

        private FakeServer server;
        private SynchronizationContext context;

        [SetUp]
        public void Open()
        {
            context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            LibrarySession.Reset();
            server = new FakeServer();
            LibrarySession.Source = server;
        }

        [TearDown]
        public void Close()
        {
            LibrarySession.Reset();
            SynchronizationContext.SetSynchronizationContext(context);
        }

        private static LibraryRecords Records(params string[] weapons) =>
            new(
                System.Array.ConvertAll(weapons, id => (LibraryKind.Weapon, id, 0)),
                new[] { new LibraryStepDay("2026-10-24", 1200) }
            );

        // 読む前の空の記録は、前に読んだ記録として出さない。
        [Test]
        public void TheRecordsAreKnownOnceReadFromTheServer()
        {
            Assert.That(LibrarySession.Known, Is.Null);
            Assert.That(LibrarySession.ReadVersion, Is.Null);
            server.Records = Records("oak-staff");

            var read = LibrarySession.LoadAsync(CancellationToken.None);

            Assert.That(read.IsCompletedSuccessfully, Is.True);
            Assert.That(LibrarySession.Known, Is.SameAs(server.Records));
            Assert.That(LibrarySession.ReadVersion, Is.EqualTo(server.Records.Fingerprint));
        }

        // サーバーがない（展示室など）ときや、接続先を替えたときは、前に読んだ記録として扱わない。
        [Test]
        public void WithoutTheServerItWasReadFromNothingIsKnown()
        {
            LibrarySession.LoadAsync(CancellationToken.None).Wait();
            LibrarySession.Source = new FakeServer();
            Assert.That(LibrarySession.Known, Is.Null);

            LibrarySession.Source = null;
            LibrarySession.LoadAsync(CancellationToken.None).Wait();
            Assert.That(LibrarySession.Known, Is.Null);
        }

        // 先に始めた読み込みが後から届いても、新しい記録を古い記録で戻さない。
        [Test]
        public void AnOlderReadArrivingLaterDoesNotReplaceANewerOne()
        {
            var older = new TaskCompletionSource<bool>();
            server.Records = Records("oak-staff");
            server.Gate = older;
            var first = LibrarySession.LoadAsync(CancellationToken.None);
            server.Records = Records("oak-staff", "short-bow");
            server.Gate = null;
            var second = LibrarySession.LoadAsync(CancellationToken.None);
            Assert.That(second.IsCompletedSuccessfully, Is.True);

            older.SetResult(true);

            Assert.That(first.IsCompletedSuccessfully, Is.True);
            Assert.That(LibrarySession.Known.IsFound(LibraryKind.Weapon, "short-bow"), Is.True);
            Assert.That(first.Result, Is.SameAs(LibrarySession.Known));
        }

        // Play Modeに入り直す前に始めた読み込みは、入り直したあとに届いても使わない。
        [Test]
        public void AReadStartedBeforeAResetIsNotUsed()
        {
            var gate = new TaskCompletionSource<bool>();
            server.Records = Records("oak-staff");
            server.Gate = gate;
            LibrarySession.LoadAsync(CancellationToken.None);

            LibrarySession.Reset();
            LibrarySession.Source = server;
            gate.SetResult(true);

            Assert.That(LibrarySession.Known, Is.Null);
            Assert.That(LibrarySession.Records, Is.SameAs(LibraryRecords.Empty));
        }

        // 取り消した読み込みは記録を変えない。
        [Test]
        public void ACancelledReadKeepsTheRecords()
        {
            server.Records = Records("oak-staff");
            LibrarySession.LoadAsync(CancellationToken.None).Wait();
            var known = LibrarySession.Known;
            server.Records = Records("short-bow");
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();

            LibrarySession.LoadAsync(cancel.Token).Wait();

            Assert.That(LibrarySession.Known, Is.SameAs(known));
        }

        // 指紋は記録の中身だけで決まり、読んだ順には左右されない。
        [Test]
        public void TheFingerprintFollowsTheRecordsNotTheirOrder()
        {
            var a = Records("oak-staff", "short-bow");
            var b = Records("short-bow", "oak-staff");
            Assert.That(a.Fingerprint, Is.EqualTo(b.Fingerprint));

            Assert.That(Records("oak-staff").Fingerprint, Is.Not.EqualTo(a.Fingerprint));
            var beaten = new LibraryRecords(
                new[] { (LibraryKind.Enemy, "moss-slime", 3) },
                new LibraryStepDay[0]
            );
            var beatenMore = new LibraryRecords(
                new[] { (LibraryKind.Enemy, "moss-slime", 4) },
                new LibraryStepDay[0]
            );
            Assert.That(beaten.Fingerprint, Is.Not.EqualTo(beatenMore.Fingerprint));
            var walked = new LibraryRecords(
                new[]
                {
                    (LibraryKind.Weapon, "oak-staff", 0),
                    (LibraryKind.Weapon, "short-bow", 0),
                },
                new[] { new LibraryStepDay("2026-10-24", 1300) }
            );
            Assert.That(walked.Fingerprint, Is.Not.EqualTo(a.Fingerprint));
        }
    }
}
