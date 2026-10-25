using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Economy;
using Baryonyx.Enhancement;
using Baryonyx.Networking;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 強化の答えを待つ間と、失敗したとき（doc/features/enhancement.md の確定の待ち方）。
    // 通知の帯と一覧の読み直しは、商会の画面のPlayModeテスト（WorkshopEnhanceTests）が確かめる。
    public sealed class EnhancementSessionTests
    {
        private sealed class FakeServer : IEnhancementSource
        {
            public TaskCompletionSource<bool> Gate;

            public Task<IReadOnlyDictionary<string, int>> LoadAsync(CancellationToken token) =>
                Task.FromResult<IReadOnlyDictionary<string, int>>(
                    new Dictionary<string, int> { [EnhancementMaterials.White] = 5 }
                );

            public async Task<EnhancementOutcome> PolishAsync(
                EnhancementTarget target,
                EnhancementMaterial stone,
                int count,
                CancellationToken token
            )
            {
                await Gate.Task;
                throw new ServerApiException(0);
            }

            public Task<EnhancementOutcome> AwakenAsync(
                AwakeningOption option,
                CancellationToken token
            ) => throw new NotSupportedException();
        }

        [SetUp]
        public void Open()
        {
            SaveQueue.Reset();
            EnhancementSession.Reset();
            RuneBalance.Reset();
        }

        [TearDown]
        public void Close()
        {
            SaveQueue.Reset();
            EnhancementSession.Reset();
            RuneBalance.Reset();
        }

        [Test]
        public async Task AFailedEnhancementGivesTheRunesBackAndKeepsTheMaterials()
        {
            var server = new FakeServer { Gate = new TaskCompletionSource<bool>() };
            EnhancementSession.Source = server;
            await EnhancementSession.LoadAsync(CancellationToken.None);
            RuneBalance.Set(1000);
            var white = EnhancementMaterials.Find(EnhancementMaterials.White);

            var polishing = EnhancementSession.PolishAsync(
                new EnhancementTarget { Category = EnhancementCategory.Weapon },
                white,
                2
            );
            // 答えを待つ間は、払うルーンを残高から引いて見せる。
            Assert.That(RuneBalance.Known, Is.EqualTo(1000 - white.Runes * 2));

            server.Gate.SetResult(true);
            ServerApiException failure = null;
            try
            {
                await polishing;
            }
            catch (ServerApiException exception)
            {
                failure = exception;
            }
            // 失敗は画面へ返し、画面が通知の帯で知らせて一覧を読み直す。
            Assert.That(failure, Is.Not.Null);
            Assert.That(RuneBalance.Known, Is.EqualTo(1000));
            Assert.That(EnhancementSession.CountOf(EnhancementMaterials.White), Is.EqualTo(5));
        }
    }
}
