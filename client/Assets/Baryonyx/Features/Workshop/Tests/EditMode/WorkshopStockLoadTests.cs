using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Economy;
using Baryonyx.Equipment;
using Baryonyx.Library;
using Baryonyx.Networking;
using Baryonyx.Party;
using Baryonyx.Workshop;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Baryonyx.Tests.EditMode
{
    // 商会の持ち物の読み込み：編成と装備を同時に読み、前に読んだ持ち物を先に出せるようにする。
    public sealed class WorkshopStockLoadTests
    {
        private sealed class FakeParty : IPartySource
        {
            public TaskCompletionSource<bool> Gate = new();
            public int Loads;

            public PartyState State = new(
                new[] { new PartyCharacterState("toma", 3, new string[] { null }) },
                new[] { "toma", null, null, null },
                500,
                30,
                100
            );

            public async Task<PartyState> LoadAsync(CancellationToken token)
            {
                Loads++;
                await Gate.Task;
                return State;
            }

            public Task<PartyState> SetSlotAsync(int slot, string id, CancellationToken token) =>
                throw new NotSupportedException();

            public Task<PartyState> SetCardAsync(
                string id,
                int slot,
                string skill,
                CancellationToken token
            ) => throw new NotSupportedException();

            public Task<PartyState> LevelUpAsync(
                string id,
                int from,
                int to,
                CancellationToken token
            ) => throw new NotSupportedException();
        }

        private sealed class FakeEquipment : IEquipmentSource
        {
            public TaskCompletionSource<bool> Gate = new();
            public int Loads;

            public async Task<EquipmentState> LoadAsync(CancellationToken token)
            {
                Loads++;
                await Gate.Task;
                return new EquipmentState(
                    new[] { new EquipmentItem("item-1", "wooden-sword") },
                    new[] { ("toma", new EquipmentPlace(EquipmentSlot.Weapon), "item-1") }
                );
            }

            public Task<EquipmentState> EquipAsync(
                string characterId,
                EquipmentPlace place,
                string itemId,
                CancellationToken token
            ) => throw new NotSupportedException();

            public Task<EquipmentState> UnequipAsync(
                string characterId,
                EquipmentPlace place,
                CancellationToken token
            ) => throw new NotSupportedException();
        }

        private PartyMockData data;
        private FakeParty party;
        private FakeEquipment equipment;
        private SynchronizationContext context;

        [SetUp]
        public void Open()
        {
            context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            SaveQueue.Reset();
            PartySession.Reset();
            EquipmentSession.Reset();
            RuneBalance.Reset();
            data = ScriptableObject.CreateInstance<PartyMockData>();
            data.Members = new[]
            {
                new PartyMember { Id = "toma", Name = "トーマ" },
            };
            party = new FakeParty();
            equipment = new FakeEquipment();
            PartySession.Source = party;
            EquipmentSession.Source = equipment;
        }

        [TearDown]
        public void Close()
        {
            SaveQueue.Reset();
            PartySession.Reset();
            EquipmentSession.Reset();
            RuneBalance.Reset();
            Object.DestroyImmediate(data);
            SynchronizationContext.SetSynchronizationContext(context);
        }

        // 装備は、編成を読み終えるのを待たずに読み始める。
        [Test]
        public void ThePartyAndTheEquipmentAreReadTogether()
        {
            var loading = WorkshopStock.LoadAsync(data, CancellationToken.None);

            Assert.That(party.Loads, Is.EqualTo(1));
            Assert.That(equipment.Loads, Is.EqualTo(1));
            Assert.That(loading.IsCompleted, Is.False);

            party.Gate.SetResult(true);
            Assert.That(loading.IsCompleted, Is.False);
            equipment.Gate.SetResult(true);

            Assert.That(loading.IsCompletedSuccessfully, Is.True);
            var sword = loading.Result.Single(thing => thing.RowId == "item-1");
            Assert.That(sword.Kind, Is.EqualTo(LibraryKind.Weapon));
            Assert.That(sword.User, Is.EqualTo("トーマ"));
        }

        // 編成と装備の両方を読むまでは、前に読んだ持ち物として出さない。
        [Test]
        public void TheStockIsKnownOnlyOnceBothAreRead()
        {
            Assert.That(WorkshopStock.Known(), Is.Null);
            var loading = WorkshopStock.LoadAsync(data, CancellationToken.None);
            party.Gate.SetResult(true);
            Assert.That(WorkshopStock.Known(), Is.Null);

            equipment.Gate.SetResult(true);

            Assert.That(
                WorkshopStock.Known().Select(thing => thing.RowId),
                Is.EqualTo(loading.Result.Select(thing => thing.RowId))
            );
        }

        // どちらかが読めなければ、読み込みは失敗する。
        [Test]
        public void AFailedReadFailsTheLoad()
        {
            var loading = WorkshopStock.LoadAsync(data, CancellationToken.None);
            party.Gate.SetResult(true);
            equipment.Gate.SetException(new InvalidOperationException("offline"));

            Assert.That(loading.IsFaulted, Is.True);
            Assert.That(loading.Exception.InnerException, Is.TypeOf<InvalidOperationException>());
        }
    }
}
