using System.Collections;
using System.Linq;
using System.Threading;
using Baryonyx.Adventure;
using Baryonyx.App;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 冒険の1周（doc/features/screens.md の冒険の画面）を、実際のシーンで通す。
    // 冒険はテストごとに新しい、アプリ内だけの冒険（TestGameServices.Adventure）を使う。
    public sealed class AdventureLoopTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices() => services = TestGameServices.Use();

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(AdventureLoopTests));
        }

        private AdventureState Do(System.Threading.Tasks.Task<AdventureState> task) =>
            AdventureSession.Use(task.GetAwaiter().GetResult());

        // 冒険を始め、通る部屋を順に選び、最後の部屋以外の出来事を終えた状態にする。
        private void StartAt(params string[] rooms)
        {
            var source = services.Adventure;
            Do(source.StartAsync(AdventureLocalSource.ForestRuins, CancellationToken.None));
            for (int i = 0; i < rooms.Length; i++)
            {
                Do(source.MoveAsync(rooms[i], CancellationToken.None));
                if (i < rooms.Length - 1)
                    Do(source.ClearAsync(rooms[i], CancellationToken.None));
            }
        }

        private static bool ExplorationReady(ExplorationBootstrap bootstrap) =>
            bootstrap.Flow != null && !bootstrap.Flow.Busy && !bootstrap.Transition.IsPlaying;

        // 開く演出（シャッター）が開き切り、最初の手札を配り終えてから操作する。
        // 配っている間は、ターン終了を受け付けない。
        private static bool BattleReady(BattleBootstrap bootstrap) =>
            !bootstrap.Transition.IsPlaying
            && !bootstrap.Transition.IsCovered
            && !bootstrap.Battle.Dealing
            && !bootstrap.Battle.Acting;

        private static IEnumerator WaitForScene(string name) =>
            SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == name,
                8f,
                name + " did not open."
            );

        private static IEnumerator WaitForDialog(UI.GameDialog dialog, string title) =>
            SceneTests.WaitUntil(
                () => dialog.IsShown && !dialog.Busy && dialog.TitleText == title,
                8f,
                $"The dialog \"{title}\" did not show (it shows \"{dialog.TitleText}\")."
            );

        [UnityTest]
        public IEnumerator TheExplorationWalksIntoTheRoomChosenAndOpensItsChest()
        {
            StartAt();
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                ExplorationReady,
                value => bootstrap = value
            );
            var view = bootstrap.View;
            Assert.That(view.LocationText, Is.EqualTo("森の遺跡 B1F"));
            Assert.That(view.PromptText, Is.EqualTo(ExplorationRoom.ChoosePrompt));
            Assert.That(
                view.Doors.Select(door => door.Button.gameObject.activeSelf),
                Is.EqualTo(new[] { true, true })
            );
            Assert.That(view.Doors[1].Hint.text, Is.EqualTo("宝箱がありそう"));
            foreach (var door in view.Doors)
                SceneTests.AssertTouchSize(door.Button.transform);
            SceneTests.AssertTouchSize(view.MenuButton.transform, 0.7f);

            // 続けて押しても、1つの部屋だけを選ぶ。
            view.Doors[1].Button.onClick.Invoke();
            view.Doors[0].Button.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => view.LocationText == "森の遺跡 B2F" && ExplorationReady(bootstrap),
                5f,
                "The next room did not show."
            );
            var saved = services
                .Adventure.LoadAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Assert.That(saved.Run.Route, Is.EqualTo(new[] { "entrance", "hidden-store" }));
            Assert.That(view.PromptText, Is.EqualTo(ExplorationRoom.ChestPrompt));
            Assert.That(
                view.Doors.Select(door => door.Button.gameObject.activeSelf),
                Is.EqualTo(new[] { false, false })
            );
            Assert.That(view.ChestButton.gameObject.activeSelf, Is.True);

            view.ChestButton.onClick.Invoke();
            yield return WaitForDialog(view.Dialog, "宝箱を開けた！");
            Assert.That(view.Dialog.BodyText, Does.StartWith("UPTボーナス「"));
            Assert.That(view.Dialog.Choose(AdventureTexts.NextChoice), Is.True);
            Assert.That(view.Dialog.IsShown, Is.False);
            Assert.That(view.ChestSprite.texture, Is.SameAs(view.ChestOpen));
            Assert.That(
                view.Doors.Select(door => door.Button.gameObject.activeSelf),
                Is.EqualTo(new[] { true, true })
            );
        }

        [UnityTest]
        public IEnumerator TheMenuShowsTheRouteAndEndsTheAdventureWithItsResult()
        {
            StartAt("hidden-store");
            Do(services.Adventure.ClearAsync("hidden-store", CancellationToken.None));
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                ExplorationReady,
                value => bootstrap = value
            );
            var view = bootstrap.View;

            // 端末の戻るキーでもメニューを開く。
            view.PressBack();
            Assert.That(view.Dialog.TitleText, Is.EqualTo(AdventureTexts.MenuTitle));
            Assert.That(
                view.Dialog.ChoiceLabels,
                Is.EqualTo(
                    new[]
                    {
                        AdventureTexts.RouteChoice,
                        AdventureTexts.SuspendChoice,
                        AdventureTexts.QuitChoice,
                        AdventureTexts.CloseChoice,
                    }
                )
            );
            view.Dialog.Choose(AdventureTexts.RouteChoice);
            Assert.That(view.Route.IsShown, Is.True);
            Assert.That(
                view.Route.RoomCount,
                Is.EqualTo(AdventureLocalSource.ForestRuinsRooms.Length)
            );
            Assert.That(view.Route.HereRoom, Is.EqualTo("hidden-store"));
            view.PressBack();
            Assert.That(view.Route.IsShown, Is.False);

            view.MenuButton.onClick.Invoke();
            view.Dialog.Choose(AdventureTexts.QuitChoice);
            Assert.That(view.Dialog.TitleText, Is.EqualTo(AdventureTexts.QuitTitle));
            view.Dialog.Choose(AdventureTexts.QuitChoice);
            yield return WaitForDialog(view.Dialog, "帰還");
            Assert.That(
                view.Dialog.BodyText,
                Does.StartWith("森の遺跡　B2Fまで到達\n最深記録を更新！")
            );
            Assert.That(AdventureSession.Current.InProgress, Is.False);

            view.Dialog.Choose(AdventureTexts.HomeChoice);
            yield return WaitForScene(SceneNames.Home);
        }

        [UnityTest]
        public IEnumerator ARoomWithABattleOpensTheBattleWithItsEncounter()
        {
            StartAt("moss-hall");
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                value => value.Flow != null,
                value => bootstrap = value
            );
            Assert.That(bootstrap.View.PromptText, Is.EqualTo(ExplorationRoom.BattlePrompt));
            yield return WaitForScene(SceneNames.Battle);
            yield return null;
            var battle = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.That(battle.Flow, Is.Not.Null);
            Assert.That(battle.Battle.EndsWithOutcome, Is.True);
            // 魔物の気配の部屋は、苔スライムと苔むした狼だけ。森の守り手はいない。
            Assert.That(
                battle.Battle.Enemies.Select(enemy => enemy.Alive),
                Is.EqualTo(new[] { true, true, false })
            );
        }

        private IEnumerator LoadBattle(System.Action<BattleBootstrap> found)
        {
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
            found(bootstrap);
        }

        [UnityTest]
        public IEnumerator AVictoryGivesTheRoomsRewardAndGoesOnToTheNextRoom()
        {
            StartAt("moss-hall");
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var battle = bootstrap.Battle;
            foreach (var enemy in battle.Enemies)
                enemy.Hp = 0;
            battle.EndTurn();
            var dialog = bootstrap.Overlay.Dialog;
            yield return WaitForDialog(dialog, BattleAdventureFlow.VictoryTitle);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(AdventureSession.Current.Run.RoomCleared, Is.True);
            Assert.That(dialog.ChoiceLabels, Is.EqualTo(new[] { AdventureTexts.NextChoice }));

            dialog.Choose(AdventureTexts.NextChoice);
            yield return WaitForScene(SceneNames.Exploration);
            var exploration = Object.FindAnyObjectByType<ExplorationBootstrap>();
            yield return SceneTests.WaitUntil(() => ExplorationReady(exploration));
            Assert.That(exploration.View.LocationText, Is.EqualTo("森の遺跡 B2F"));
            Assert.That(exploration.Flow.Room.Exits.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ADefeatOffersTheReviveAndTheReturnAlike()
        {
            StartAt("moss-hall");
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var battle = bootstrap.Battle;
            var dialog = bootstrap.Overlay.Dialog;
            battle.Enemies[1].Hp = 100;
            foreach (var ally in battle.Allies)
                ally.Hp = 0;
            battle.EndTurn();
            yield return WaitForDialog(dialog, BattleAdventureFlow.DefeatTitle);
            Assert.That(
                dialog.ChoiceLabels,
                Is.EqualTo(new[] { AdventureTexts.ReviveChoice(100), AdventureTexts.ReturnChoice })
            );

            // 復活すると味方は全快し、敵の減ったHPはそのまま。ルーンは100減る。
            dialog.Choose(AdventureTexts.ReviveChoice(100));
            yield return SceneTests.WaitUntil(
                () => !dialog.IsShown,
                5f,
                "The revive did not finish."
            );
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.None));
            Assert.That(battle.Allies.All(ally => ally.Hp == ally.MaxHp), Is.True);
            Assert.That(battle.Enemies[1].Hp, Is.EqualTo(100));
            Assert.That(services.Adventure.Runes, Is.EqualTo(900));
            Assert.That(AdventureSession.Current.Run.ReviveCost, Is.EqualTo(200));

            yield return SceneTests.WaitUntil(() => !battle.Dealing && !battle.Acting);
            foreach (var ally in battle.Allies)
                ally.Hp = 0;
            battle.EndTurn();
            yield return WaitForDialog(dialog, BattleAdventureFlow.DefeatTitle);
            dialog.Choose(AdventureTexts.ReturnChoice);
            yield return WaitForDialog(dialog, "冒険の終わり");
            Assert.That(dialog.BodyText, Does.StartWith("森の遺跡　B2Fまで到達"));
            dialog.Choose(AdventureTexts.HomeChoice);
            yield return WaitForScene(SceneNames.Home);
            Assert.That(AdventureSession.Current.InProgress, Is.False);
        }

        [UnityTest]
        public IEnumerator BeatingTheBossEndsTheAdventureWithItsResult()
        {
            StartAt("moss-hall", "root-gallery", "sanctum");
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var battle = bootstrap.Battle;
            Assert.That(battle.Enemies.All(enemy => enemy.Alive), Is.True);
            foreach (var enemy in battle.Enemies)
                enemy.Hp = 0;
            battle.EndTurn();
            var dialog = bootstrap.Overlay.Dialog;
            yield return WaitForDialog(dialog, BattleAdventureFlow.VictoryTitle);
            dialog.Choose(dialog.ChoiceLabels[0]);
            yield return WaitForDialog(dialog, "踏破！");
            Assert.That(dialog.BodyText, Does.StartWith("森の遺跡　B4Fまで到達\n最深記録を更新！"));
            dialog.Choose(AdventureTexts.HomeChoice);
            yield return WaitForScene(SceneNames.Home);
            var record = AdventureSession.Current.RecordOf(AdventureLocalSource.ForestRuins);
            Assert.That(record.Clears, Is.EqualTo(1));
        }

        // 戦闘の途中でも中断でき、再開すると選んだ部屋から（戦闘の始めから）続ける。
        [UnityTest]
        public IEnumerator SuspendingTheBattleResumesFromTheRoomChosen()
        {
            StartAt("moss-hall");
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            bootstrap.Overlay.PressBack();
            var dialog = bootstrap.Overlay.Dialog;
            Assert.That(dialog.TitleText, Is.EqualTo(AdventureTexts.MenuTitle));
            dialog.Choose(AdventureTexts.SuspendChoice);
            yield return WaitForScene(SceneNames.Home);
            var home = Object.FindAnyObjectByType<HomeBootstrap>();
            yield return SceneTests.WaitUntil(() => SceneTests.HomeReady(home));
            yield return SceneTests.WaitForTask(home.AdventureTask);
            Assert.That(home.View.DestinationFloorLabel.text, Is.EqualTo("B2F"));
            home.View.ResumeButton.onClick.Invoke();
            yield return WaitForScene(SceneNames.Exploration);
            yield return WaitForScene(SceneNames.Battle);
            yield return null;
            var resumed = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.That(resumed.Flow.RoomId, Is.EqualTo("moss-hall"));
            Assert.That(resumed.Battle.Enemies[1].Hp, Is.EqualTo(resumed.Battle.Enemies[1].MaxHp));
        }

        [UnityTest]
        public IEnumerator TheTravelOfficeAsksAndSetsOut()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.TravelOffice, value => guide = value);
            yield return SceneTests.WaitForTask(guide.Departure.Work);
            var view = guide.View;
            int index = System.Array.FindIndex(
                view.Definition.Destinations,
                destination => destination.Id == AdventureLocalSource.ForestRuins
            );
            view.DestinationRows[index].onClick.Invoke();
            view.Depart.onClick.Invoke();
            Assert.That(view.Dialog.TitleText, Is.EqualTo("森の遺跡へ出発しますか？"));
            Assert.That(view.Dialog.BodyText, Does.Contain("はじめて訪れる場所です。"));
            // 戻るキーはダイアログを閉じ、旅の案内所に残る。
            view.PressBack();
            Assert.That(view.Dialog.IsShown, Is.False);
            Assert.That(guide.Presenter.Left, Is.False);

            view.Depart.onClick.Invoke();
            view.Dialog.Choose(TravelDeparture.GoChoice);
            yield return WaitForScene(SceneNames.Exploration);
            Assert.That(AdventureSession.Current.Run.RoomId, Is.EqualTo("entrance"));
        }

        // 冒険の外で開いた戦闘はモックのまま：勝敗で終わらず、冒険のメニューも出さない。
        [UnityTest]
        public IEnumerator TheBattleOpenedOnItsOwnStaysTheMock()
        {
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            Assert.That(bootstrap.Flow, Is.Null);
            Assert.That(bootstrap.Battle.EndsWithOutcome, Is.False);
            Assert.That(bootstrap.Overlay.MenuButton.gameObject.activeSelf, Is.False);
        }
    }
}
