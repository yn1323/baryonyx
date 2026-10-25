using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.App;
using Baryonyx.Health;
using Baryonyx.Networking;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 冒険の画面で保存できなかったとき（doc/features/screens/adventure.md）。
    public sealed class AdventureSaveFailureTests
    {
        private TestGameServices services;
        private FlakyAdventure adventure;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            AdventureVitals.UseStore(new MemoryAdventureVitalsStore());
            // 冒険の代役だけを、マスの移動を失敗させられるものに替える。
            adventure = new FlakyAdventure(services.Adventure);
            GameServices.Override(
                new GameServices(
                    new HealthStepLink(services.Provider, services.Server, new MemoryLinkStore()),
                    true,
                    adventure: adventure
                )
            );
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            AdventureVitals.UseStore(null);
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(AdventureSaveFailureTests));
        }

        private static bool ExplorationReady(ExplorationBootstrap bootstrap) =>
            bootstrap.Flow != null
            && !bootstrap.Flow.Busy
            && !bootstrap.Transition.IsPlaying
            && !bootstrap.View.Walking
            && !bootstrap.View.Clearing;

        private static bool BattleReady(BattleBootstrap bootstrap) =>
            !bootstrap.Transition.IsPlaying
            && !bootstrap.Transition.IsCovered
            && !bootstrap.Battle.Dealing
            && !bootstrap.Battle.Acting;

        // 選んだマスを保存できなかったとき（マスの選択と移動）。
        [UnityTest]
        public IEnumerator AMoveThatFailedToSaveIsToldAndTheAdventureIsReadAgain()
        {
            AdventureSession.Use(
                services
                    .Adventure.StartAsync(AdventureCatalog.ForestRuins, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
            );
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                ExplorationReady,
                value => bootstrap = value
            );
            var view = bootstrap.View;
            string chosen = AdventureSession.Current.Run.Exits[0].Id;
            int loads = adventure.Loads;

            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("マスを選べませんでした")
            );
            adventure.FailNextMove = true;
            view.MarkerOf(chosen).Button.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => adventure.Loads > loads && ExplorationReady(bootstrap),
                6f,
                "The exploration did not read the adventure again."
            );

            Assert.That(view.Notice.Message, Is.EqualTo(AdventureTexts.SaveFailed));
            // サーバーに残っている入口から、もう一度選べる。
            Assert.That(
                AdventureSession.Current.Run.RoomId,
                Is.EqualTo(AdventureRouteMap.EntranceId)
            );
            Assert.That(view.MarkerOf(chosen).IsExit, Is.True);
            Assert.That(view.MarkerOf(chosen).Button.interactable, Is.True);
        }

        // 戦闘中に冒険をやめる知らせが届かなかったら、知らせて戦闘へ戻り、メニューをまた開ける。
        [UnityTest]
        public IEnumerator AQuitThatFailedToSaveDuringABattleGoesBackToTheBattle()
        {
            var state = services
                .Adventure.StartAsync(AdventureCatalog.ForestRuins, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            AdventureSession.Use(
                services
                    .Adventure.MoveAsync(state.Run.Exits[0], CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
            );
            var bootstrap = default(BattleBootstrap);
            yield return SceneTests.Load(
                SceneTests.BattlePath,
                (System.Func<BattleBootstrap, bool>)(_ => true),
                value => bootstrap = value
            );
            yield return SceneTests.WaitUntil(
                () => BattleReady(bootstrap),
                8f,
                "The battle did not get ready."
            );
            var overlay = bootstrap.Overlay;

            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("冒険を終えられませんでした")
            );
            adventure.FailNextEnd = true;
            overlay.MenuButton.onClick.Invoke();
            overlay.Dialog.Choose(AdventureTexts.QuitChoice);
            Assert.That(overlay.Dialog.TitleText, Is.EqualTo(AdventureTexts.QuitTitle));
            overlay.Dialog.Choose(AdventureTexts.QuitChoice);
            yield return SceneTests.WaitForTask(bootstrap.Flow.Work);

            Assert.That(overlay.Notice.Message, Is.EqualTo(AdventureTexts.SaveFailed));
            Assert.That(AdventureSession.Current.InProgress, Is.True);
            // 確かめの窓は押せるまま残る。続けると戦闘へ戻り、メニューをまた開ける。
            overlay.Dialog.Choose(AdventureTexts.GoOnChoice);
            yield return SceneTests.WaitUntil(
                () => !overlay.Dialog.IsShown,
                2f,
                "The dialog did not close."
            );
            overlay.MenuButton.onClick.Invoke();
            Assert.That(overlay.Dialog.IsShown, Is.True);
            Assert.That(overlay.Dialog.TitleText, Is.EqualTo(AdventureTexts.MenuTitle));
        }

        private sealed class MemoryLinkStore : IHealthLinkStore
        {
            public int PermissionDenials { get; set; }
        }

        // マスの移動と冒険を終える知らせを、1回ずつ失敗させられる代役。ほかは端末内の冒険へ渡す。
        private sealed class FlakyAdventure : IAdventureSource
        {
            private readonly AdventureLocalSource inner;

            public FlakyAdventure(AdventureLocalSource inner) => this.inner = inner;

            public bool FailNextMove;
            public bool FailNextEnd;
            public int Loads;

            public Task<AdventureState> LoadAsync(CancellationToken token)
            {
                Loads++;
                return inner.LoadAsync(token);
            }

            public Task<AdventureState> StartAsync(string destinationId, CancellationToken token) =>
                inner.StartAsync(destinationId, token);

            public Task<AdventureState> MoveAsync(AdventureRoom room, CancellationToken token)
            {
                if (!FailNextMove)
                    return inner.MoveAsync(room, token);
                FailNextMove = false;
                return Task.FromException<AdventureState>(new ServerApiException(0));
            }

            public Task<AdventureState> ClearAsync(
                string roomId,
                CancellationToken token,
                IReadOnlyList<AdventureEnemyTally> enemies = null,
                AdventureClearOptions options = null
            ) => inner.ClearAsync(roomId, token, enemies, options);

            public Task<AdventureState> OpenAsync(string roomId, CancellationToken token) =>
                inner.OpenAsync(roomId, token);

            public Task<AdventureState> BuyAsync(
                string roomId,
                int index,
                CancellationToken token
            ) => inner.BuyAsync(roomId, index, token);

            public Task<AdventureState> ReviveAsync(string roomId, CancellationToken token) =>
                inner.ReviveAsync(roomId, token);

            public Task<AdventureState> EndAsync(
                AdventureEndReason reason,
                CancellationToken token,
                IReadOnlyList<AdventureEnemyTally> enemies = null
            )
            {
                if (!FailNextEnd)
                    return inner.EndAsync(reason, token, enemies);
                FailNextEnd = false;
                return Task.FromException<AdventureState>(new ServerApiException(0));
            }
        }
    }
}
