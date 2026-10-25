using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Economy;
using Baryonyx.Networking;
using Baryonyx.Party;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 前に読んだ編成をすぐ出し、裏で読み直す流れ（パネルを開き直したとき）。
    public sealed class PartySessionReadAgainTests
    {
        // 読み込みの答えを待たせられるサーバー。読み込みは、読み始めたときの編成を返す。
        private sealed class FakeServer : IPartySource
        {
            public PartyState State;
            public TaskCompletionSource<bool> LoadGate;

            public async Task<PartyState> LoadAsync(CancellationToken token)
            {
                var read = State;
                if (LoadGate != null)
                    await LoadGate.Task;
                return read;
            }

            public Task<PartyState> SetSlotAsync(int slot, string id, CancellationToken token) =>
                Task.FromResult(State = State.WithSlot(slot, id));

            public Task<PartyState> SetCardAsync(
                string id,
                int slot,
                string skill,
                CancellationToken token
            ) => Task.FromResult(State = State.WithCard(id, slot, skill));

            public Task<PartyState> LevelUpAsync(
                string id,
                int from,
                int to,
                CancellationToken token
            ) => Task.FromResult(State = State.WithLevel(id, to, 0));
        }

        private PartyMockData data;
        private FakeServer server;
        private SynchronizationContext context;

        [SetUp]
        public void Open()
        {
            context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            SaveQueue.Reset();
            PartySession.Reset();
            RuneBalance.Reset();
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
                )
                {
                    Fingerprint = "first",
                },
            };
            PartySession.Source = server;
        }

        [TearDown]
        public void Close()
        {
            SaveQueue.Reset();
            PartySession.Reset();
            RuneBalance.Reset();
            Object.DestroyImmediate(data);
            SynchronizationContext.SetSynchronizationContext(context);
        }

        [Test]
        public void TheFormationIsKnownOnceReadFromTheServer()
        {
            Assert.That(PartySession.Known, Is.Null);
            Assert.That(PartySession.ReadVersion, Is.Null);

            var read = PartySession.LoadAsync(data, server, CancellationToken.None);

            Assert.That(read.IsCompletedSuccessfully, Is.True);
            Assert.That(PartySession.Known, Is.SameAs(read.Result));
            Assert.That(PartySession.ReadVersion, Is.EqualTo("first"));
        }

        // サーバーがない（展示室など）ときは、前に読んだ編成として扱わない。
        [Test]
        public void WithoutAServerNothingIsKnown()
        {
            PartySession.LoadAsync(data, server, CancellationToken.None).Wait();
            PartySession.Source = null;

            Assert.That(PartySession.Known, Is.Null);
        }

        // 読み直している間に変えた編成は、変える前の読み込みで戻さない。
        [Test]
        public void AChangeMadeWhileReadingAgainIsNotUndoneByTheRead()
        {
            PartySession.LoadAsync(data, server, CancellationToken.None).Wait();
            server.LoadGate = new TaskCompletionSource<bool>();
            var reading = PartySession.LoadAsync(data, server, CancellationToken.None);

            var saved = PartySession.SetSlotAsync(1, "luka", "編成を保存できませんでした");
            Assert.That(saved.IsCompletedSuccessfully, Is.True);
            server.LoadGate.SetResult(true);

            Assert.That(reading.Result.Member(1), Is.EqualTo("luka"));
            Assert.That(PartySession.Formation(data).Member(1), Is.EqualTo("luka"));
        }

        // 変更の結果（change）が違うだけなら、同じ中身として指紋も同じになる。
        [Test]
        public void TheFingerprintIgnoresTheChangeAndFollowsTheContents()
        {
            PartyApiClient.State Read(int level, string change) =>
                new()
                {
                    change = change,
                    characters = new[]
                    {
                        new PartyApiClient.Character
                        {
                            id = "toma",
                            level = level,
                            cards = new[] { "card:a", null },
                        },
                    },
                    slots = new[]
                    {
                        new PartyApiClient.Slot { slot = 0, characterId = "toma" },
                    },
                    runes = 10,
                };

            string read = PartyServerSource.ToState(Read(12, null)).Fingerprint;
            var answer = Read(12, "replace");

            Assert.That(PartyServerSource.ToState(answer).Fingerprint, Is.EqualTo(read));
            Assert.That(
                PartyServerSource.ToState(Read(13, null)).Fingerprint,
                Is.Not.EqualTo(read)
            );
            Assert.That(PartyServerSource.ToState(null).Fingerprint, Is.Null);
        }
    }
}
