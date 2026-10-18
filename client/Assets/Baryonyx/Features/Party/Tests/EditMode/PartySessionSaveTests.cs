using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Economy;
using Baryonyx.Networking;
using Baryonyx.Party;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 編成の変更をすぐに出し、裏でサーバーへ保存する流れ（doc/rules/data-storage.md）。
    public sealed class PartySessionSaveTests
    {
        // 答えを待たせたり、失敗させたりできるサーバー。答えは、受け取った変更を当てた編成。
        private sealed class FakeServer : IPartySource
        {
            public PartyState State;
            public Exception Fail;
            public TaskCompletionSource<bool> Gate;
            public readonly List<string> Calls = new();

            public Task<PartyState> LoadAsync(CancellationToken token) => Task.FromResult(State);

            public Task<PartyState> SetSlotAsync(int slot, string id, CancellationToken token) =>
                Answer($"slot {slot} {id}", state => state.WithSlot(slot, id));

            public Task<PartyState> SetCardAsync(
                string id,
                int slot,
                string skill,
                CancellationToken token
            ) => Answer($"card {id} {slot} {skill}", state => state.WithCard(id, slot, skill));

            public Task<PartyState> LevelUpAsync(
                string id,
                int from,
                int to,
                CancellationToken token
            ) => Answer($"level {id} {from} {to}", state => state.WithLevel(id, to, 300));

            private async Task<PartyState> Answer(string call, Func<PartyState, PartyState> change)
            {
                Calls.Add(call);
                if (Gate != null)
                    await Gate.Task;
                if (Fail != null)
                    throw Fail;
                return State = change(State);
            }
        }

        private PartyMockData data;
        private FakeServer server;
        private readonly List<string> failures = new();

        [SetUp]
        public void Open()
        {
            SaveQueue.Reset();
            PartySession.Reset();
            RuneBalance.Reset();
            failures.Clear();
            SaveQueue.Failed += failures.Add;
            data = ScriptableObject.CreateInstance<PartyMockData>();
            data.Members = new[]
            {
                new PartyMember { Id = "toma", Name = "トーマ" },
                new PartyMember { Id = "luka", Name = "ルカ" },
            };
            server = new FakeServer
            {
                State = new PartyState(
                    new[]
                    {
                        new PartyCharacterState("toma", 12, new[] { "Fire", "Ice" }),
                        new PartyCharacterState("luka", 3, new string[] { null, null }),
                    },
                    new[] { "toma", null, null, null },
                    1000,
                    30,
                    100
                ),
            };
            PartySession.Source = server;
            PartySession.LoadAsync(data, server, CancellationToken.None).Wait();
        }

        [TearDown]
        public void Close()
        {
            SaveQueue.Reset();
            PartySession.Reset();
            RuneBalance.Reset();
            UnityEngine.Object.DestroyImmediate(data);
        }

        [Test]
        public void AChangeShowsAtOnceAndIsSavedInOrder()
        {
            server.Gate = new TaskCompletionSource<bool>();
            var slot = PartySession.SetSlotAsync(1, "luka", "編成を保存できませんでした");
            var card = PartySession.SetCardAsync("toma", 0, "Ice", "スキルを保存できませんでした");

            // 保存を待たずに、編成もカードも変わって見える。
            Assert.That(PartySession.Formation(data).Member(1), Is.EqualTo("luka"));
            Assert.That(PartySession.CardsOf(data, "toma"), Is.EqualTo(new[] { "Ice", "Fire" }));
            Assert.That(PartySession.Saving, Is.True);
            // 前の書き込みが終わるまで、次は送らない。
            Assert.That(server.Calls, Is.EqualTo(new[] { "slot 1 luka" }));

            server.Gate.SetResult(true);
            Assert.That(slot.IsCompleted && card.IsCompleted, Is.True);
            Assert.That(server.Calls, Is.EqualTo(new[] { "slot 1 luka", "card toma 0 Ice" }));
            Assert.That(PartySession.Saving, Is.False);
            Assert.That(card.Result.Member(1), Is.EqualTo("luka"));
            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void AFailedChangeGoesBackAndIsTold()
        {
            server.Fail = new InvalidOperationException("offline");
            var saved = PartySession.SetSlotAsync(1, "luka", "編成を保存できませんでした");

            Assert.That(saved.IsCompleted, Is.True);
            Assert.That(saved.Result.Member(1), Is.Null);
            Assert.That(PartySession.Formation(data).Member(1), Is.Null);
            Assert.That(failures, Is.EqualTo(new[] { "編成を保存できませんでした" }));
        }

        [Test]
        public void AReadWhileSavingKeepsTheShownParty()
        {
            server.Gate = new TaskCompletionSource<bool>();
            PartySession.SetSlotAsync(1, "luka", "");

            // 画面を開き直しても、保存前の編成を読み直さない。
            var formation = PartySession.LoadAsync(data, server, CancellationToken.None).Result;
            Assert.That(formation.Member(1), Is.EqualTo("luka"));
            server.Gate.SetResult(true);
        }

        [Test]
        public void ALevelUpSpendsTheRunesAtOnceAndGivesThemBackWhenItFails()
        {
            Assert.That(RuneBalance.Known, Is.EqualTo(1000));
            server.Gate = new TaskCompletionSource<bool>();
            server.Fail = new InvalidOperationException("offline");
            var saved = PartySession.LevelUpAsync(
                "toma",
                12,
                13,
                300,
                _ => "レベルアップできませんでした"
            );

            Assert.That(PartySession.LevelOf(data, "toma"), Is.EqualTo(13));
            Assert.That(PartySession.State.Runes, Is.EqualTo(700));
            Assert.That(RuneBalance.Known, Is.EqualTo(700));
            // 保存を待つ間にほかの画面が残高を読み直しても、先に使った分は引いたまま。
            RuneBalance.Set(1000);
            Assert.That(RuneBalance.Known, Is.EqualTo(700));

            server.Gate.SetResult(true);
            Assert.That(saved.IsCompleted, Is.True);
            Assert.That(PartySession.LevelOf(data, "toma"), Is.EqualTo(12));
            Assert.That(RuneBalance.Known, Is.EqualTo(1000));
            Assert.That(failures, Is.EqualTo(new[] { "レベルアップできませんでした" }));
        }

        [Test]
        public void ASavedLevelUpShowsTheServersBalance()
        {
            PartySession.LevelUpAsync("toma", 12, 13, 300, _ => "");

            Assert.That(PartySession.LevelOf(data, "toma"), Is.EqualTo(13));
            Assert.That(RuneBalance.Known, Is.EqualTo(700));
            Assert.That(PartySession.Saving, Is.False);
        }
    }
}
