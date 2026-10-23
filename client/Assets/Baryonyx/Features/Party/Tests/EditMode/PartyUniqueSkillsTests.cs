using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.EditMode
{
    // 固有スキルの進み具合の保存（doc/features/skill-evolution.md#覚醒の保存）：編成と一緒に読み、変更を待ち行列で送る。
    public sealed class PartyUniqueSkillsTests
    {
        // サーバーの応答（server/src/features/party/routes.ts）を JsonUtility で読んだ形。
        private const string Response =
            "{\"characters\":["
            + "{\"id\":\"toma\",\"level\":14,\"cards\":[],\"uniques\":["
            + "{\"index\":0,\"set\":3,\"acquired\":[0,1,3]},"
            + "{\"index\":1,\"set\":0,\"acquired\":[0]}]},"
            + "{\"id\":\"luka\",\"level\":3,\"cards\":[]}],"
            + "\"slots\":[],\"runes\":0,\"rules\":{\"maxLevel\":30,\"costPerLevel\":100}}";

        private sealed class FakeServer : IPartySource, IPartyUniqueSkillSource
        {
            public PartyState State = PartyServerSource.ToState(
                JsonUtility.FromJson<PartyApiClient.State>(Response)
            );
            public Exception Fail;
            public readonly List<string> Sent = new();

            public Task<PartyState> LoadAsync(CancellationToken token) => Task.FromResult(State);

            public Task<PartyState> SetSlotAsync(int slot, string id, CancellationToken token) =>
                Task.FromResult(State);

            public Task<PartyState> SetCardAsync(
                string id,
                int slot,
                string key,
                CancellationToken token
            ) => Task.FromResult(State);

            public Task<PartyState> LevelUpAsync(
                string id,
                int from,
                int to,
                CancellationToken token
            ) => Task.FromResult(State);

            public Task<PartyState> SetUniqueAsync(
                string id,
                int index,
                int stage,
                CancellationToken token
            ) => Answer($"set {id} {index} {stage}");

            public Task<PartyState> EvolveUniqueAsync(
                string id,
                int index,
                int stage,
                CancellationToken token
            ) => Answer($"evolve {id} {index} {stage}");

            private Task<PartyState> Answer(string call)
            {
                Sent.Add(call);
                return Fail != null ? Task.FromException<PartyState>(Fail) : Task.FromResult(State);
            }
        }

        [SetUp]
        public void Forget()
        {
            UniqueSkillSession.Reset();
            PartyUniqueSkills.Reset();
        }

        [TearDown]
        public void PutBack()
        {
            PartySession.Source = null;
            UniqueSkillSession.Reset();
            PartyUniqueSkills.Reset();
        }

        [Test]
        public void TheServersProgressIsReadWithTheParty()
        {
            var state = PartyServerSource.ToState(
                JsonUtility.FromJson<PartyApiClient.State>(Response)
            );
            var toma = state.Find("toma").Uniques;
            Assert.That(toma.Count, Is.EqualTo(2));
            Assert.That(toma[0].SetStage, Is.EqualTo(3));
            Assert.That(
                toma[0].Acquired,
                Is.EqualTo(new[] { 1, 3 }),
                "The first stage is not written."
            );
            Assert.That(toma[1].SetStage, Is.Zero);
            Assert.That(state.Find("luka").Uniques, Is.Null, "Not answered.");

            PartyUniqueSkills.Use(state);
            Assert.That(UniqueSkillSession.ProgressOf("toma", 0, null).SetStage, Is.EqualTo(3));
            Assert.That(UniqueSkillSession.ProgressOf("toma", 0, null).Has(1), Is.True);
        }

        [UnityTest]
        public IEnumerator ChangesAreSentInOrderAndAFailureIsTold()
        {
            var server = new FakeServer();
            PartySession.Source = server;
            var told = new List<string>();
            void Tell(string message) => told.Add(message);
            SaveQueue.Failed += Tell;
            try
            {
                PartyUniqueSkills.SetStage("toma", 0, 0);
                PartyUniqueSkills.Evolve("toma", 0, 5);
                while (!PartyUniqueSkills.Work.IsCompleted)
                    yield return null;
                Assert.That(server.Sent, Is.EqualTo(new[] { "set toma 0 0", "evolve toma 0 5" }));
                Assert.That(PartyUniqueSkills.Saving, Is.False);
                Assert.That(told, Is.Empty);
                // Once saved, the server's progress is the app's.
                Assert.That(UniqueSkillSession.ProgressOf("toma", 0, null).SetStage, Is.EqualTo(3));

                server.Fail = new InvalidOperationException("409");
                PartyUniqueSkills.Evolve("toma", 1, 1);
                while (!PartyUniqueSkills.Work.IsCompleted)
                    yield return null;
                Assert.That(told, Is.EqualTo(new[] { PartyUniqueSkills.SaveFailedMessage }));
                Assert.That(PartyUniqueSkills.Saving, Is.False);
            }
            finally
            {
                SaveQueue.Failed -= Tell;
            }
        }

        [Test]
        public void WithoutAServerNothingIsSent()
        {
            PartySession.Source = null;
            PartyUniqueSkills.Evolve("toma", 0, 1);
            Assert.That(PartyUniqueSkills.Saving, Is.False);
        }
    }
}
