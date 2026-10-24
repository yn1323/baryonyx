using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Equipment;
using Baryonyx.Networking;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 装備の付け替えをすぐに出し、裏でサーバーへ保存する置き場（doc/rules/data-storage.md）。
    public sealed class EquipmentSavesTests
    {
        // 答えを待たせたり、失敗させたりできるサーバー。中身はアプリ内の置き場に任せる。
        private sealed class FakeServer : IEquipmentSource
        {
            private readonly EquipmentLocalSource inner = new();
            public bool Fail;
            public TaskCompletionSource<bool> Gate;
            public int Loads;

            public Task<EquipmentState> LoadAsync(CancellationToken token)
            {
                Loads++;
                return inner.LoadAsync(token);
            }

            public async Task<EquipmentState> EquipAsync(
                string characterId,
                EquipmentPlace slot,
                string itemId,
                CancellationToken token
            )
            {
                await Wait();
                return await inner.EquipAsync(characterId, slot, itemId, token);
            }

            public async Task<EquipmentState> UnequipAsync(
                string characterId,
                EquipmentPlace slot,
                CancellationToken token
            )
            {
                await Wait();
                return await inner.UnequipAsync(characterId, slot, token);
            }

            private async Task Wait()
            {
                if (Gate != null)
                    await Gate.Task;
                if (Fail)
                    throw new InvalidOperationException("offline");
            }
        }

        private readonly List<string> failures = new();

        [SetUp]
        public void ResetQueue()
        {
            SaveQueue.Reset();
            failures.Clear();
            SaveQueue.Failed += failures.Add;
        }

        [TearDown]
        public void ClearQueue() => SaveQueue.Reset();

        [Test]
        public async Task ARemovalShowsAtOnceAndAReadWhileSavingKeepsIt()
        {
            var server = new FakeServer { Gate = new TaskCompletionSource<bool>() };
            var saves = new EquipmentSaves(server);
            var before = await saves.LoadAsync(CancellationToken.None);
            string worn = before.ItemIn("toma", EquipmentSlot.Weapon);
            Assert.That(worn, Is.Not.Null);

            var saved = saves.UnequipAsync("toma", EquipmentSlot.Weapon, CancellationToken.None);
            Assert.That(saves.Current.ItemIn("toma", EquipmentSlot.Weapon), Is.Null);
            // 保存を待つ間に開き直しても、サーバーから保存前の装備を読まない。
            var reread = await saves.LoadAsync(CancellationToken.None);
            Assert.That(reread.ItemIn("toma", EquipmentSlot.Weapon), Is.Null);
            Assert.That(server.Loads, Is.EqualTo(1));

            server.Gate.SetResult(true);
            Assert.That((await saved).ItemIn("toma", EquipmentSlot.Weapon), Is.Null);
            Assert.That(failures, Is.Empty);
        }

        [Test]
        public async Task AFailedChangeReturnsTheItemsPutBack()
        {
            var server = new FakeServer { Fail = true };
            var saves = new EquipmentSaves(server);
            var before = await saves.LoadAsync(CancellationToken.None);
            string worn = before.ItemIn("toma", EquipmentSlot.Weapon);

            var saved = await saves.UnequipAsync("toma", EquipmentSlot.Weapon, default);
            Assert.That(saved.ItemIn("toma", EquipmentSlot.Weapon), Is.EqualTo(worn));
            Assert.That(failures, Is.EqualTo(new[] { EquipmentPresenter.SaveFailedMessage }));
        }
    }
}
