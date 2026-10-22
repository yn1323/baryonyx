using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Equipment;
using Baryonyx.Networking;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 前に読んだ装備をすぐ出し、裏で読み直す流れ（パネルを開き直したとき）。
    public sealed class EquipmentSavesReadAgainTests
    {
        // 読み込みの答えを待たせられるサーバー。読み込みは、読み始めたときの装備を返す。
        private sealed class FakeServer : IEquipmentSource
        {
            private readonly EquipmentLocalSource inner = new();
            public TaskCompletionSource<bool> LoadGate;

            public async Task<EquipmentState> LoadAsync(CancellationToken token)
            {
                var read = await inner.LoadAsync(token);
                if (LoadGate != null)
                    await LoadGate.Task;
                return read;
            }

            public Task<EquipmentState> EquipAsync(
                string characterId,
                EquipmentSlot slot,
                string itemId,
                CancellationToken token
            ) => inner.EquipAsync(characterId, slot, itemId, token);

            public Task<EquipmentState> UnequipAsync(
                string characterId,
                EquipmentSlot slot,
                CancellationToken token
            ) => inner.UnequipAsync(characterId, slot, token);
        }

        private SynchronizationContext context;

        [SetUp]
        public void Open()
        {
            context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            SaveQueue.Reset();
            EquipmentSession.Reset();
        }

        [TearDown]
        public void Close()
        {
            SaveQueue.Reset();
            EquipmentSession.Reset();
            SynchronizationContext.SetSynchronizationContext(context);
        }

        // 読み直している間に外した装備は、外す前の読み込みで戻さない。
        [Test]
        public void ARemovalMadeWhileReadingAgainIsNotUndoneByTheRead()
        {
            var server = new FakeServer();
            var saves = new EquipmentSaves(server);
            saves.LoadAsync(CancellationToken.None).Wait();
            Assert.That(saves.Current.ItemIn("toma", EquipmentSlot.Weapon), Is.Not.Null);
            server.LoadGate = new TaskCompletionSource<bool>();
            var reading = saves.LoadAsync(CancellationToken.None);

            var removed = saves.UnequipAsync("toma", EquipmentSlot.Weapon, CancellationToken.None);
            Assert.That(removed.IsCompletedSuccessfully, Is.True);
            server.LoadGate.SetResult(true);

            Assert.That(reading.Result.ItemIn("toma", EquipmentSlot.Weapon), Is.Null);
            Assert.That(saves.Current.ItemIn("toma", EquipmentSlot.Weapon), Is.Null);
        }

        [Test]
        public void TheEquipmentIsKnownOnceReadFromTheServer()
        {
            var server = new FakeServer();
            EquipmentSession.Source = server;
            Assert.That(EquipmentSession.Known, Is.Null);

            var read = EquipmentSession.SourceOrLocal.LoadAsync(CancellationToken.None);

            Assert.That(EquipmentSession.Known, Is.SameAs(read.Result));
            EquipmentSession.Source = null;
            Assert.That(EquipmentSession.Known, Is.Null);
        }

        // 変更の結果（change・from）が違うだけなら、同じ中身として指紋も同じになる。
        [Test]
        public void TheFingerprintIgnoresTheChangeAndFollowsTheContents()
        {
            EquipmentApiClient.State Read(string weapon, string change) =>
                new()
                {
                    change = change,
                    from = change == "move" ? "luka" : null,
                    items = new[]
                    {
                        new EquipmentApiClient.Item
                        {
                            id = "a",
                            equipmentId = "sword",
                            multiplier = 1f,
                        },
                    },
                    characters = new[]
                    {
                        new EquipmentApiClient.Character { id = "toma", weapon = weapon },
                    },
                };

            string read = EquipmentServerSource.ToState(Read("a", null)).Fingerprint;
            var answer = Read("a", "move");

            Assert.That(EquipmentServerSource.ToState(answer).Fingerprint, Is.EqualTo(read));
            Assert.That(EquipmentServerSource.ToState(answer).From, Is.EqualTo("luka"));
            Assert.That(
                EquipmentServerSource.ToState(Read(null, null)).Fingerprint,
                Is.Not.EqualTo(read)
            );
        }
    }
}
