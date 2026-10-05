using System.Collections;
using System.Linq;
using System.Threading;
using Baryonyx.Adventure;
using Baryonyx.App;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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

        private static readonly string Forest = AdventureCatalog.DestinationName(
            AdventureCatalog.ForestRuins
        );

        // 冒険を始め、次の階の最初の部屋へ moves 回進み、最後の部屋以外の出来事を終えた状態にする。
        private AdventureRoom StartAt(int moves)
        {
            var source = services.Adventure;
            var state = Do(source.StartAsync(AdventureCatalog.ForestRuins, CancellationToken.None));
            AdventureRoom room = null;
            for (int i = 0; i < moves; i++)
            {
                room = state.Run.Exits[0];
                state = Do(source.MoveAsync(room, CancellationToken.None));
                if (i < moves - 1)
                    state = Do(source.ClearAsync(room.Id, CancellationToken.None));
            }
            return room;
        }

        private static bool ExplorationReady(ExplorationBootstrap bootstrap) =>
            bootstrap.Flow != null
            && !bootstrap.Flow.Busy
            && !bootstrap.Transition.IsPlaying
            && !bootstrap.View.Walking
            && !bootstrap.View.Clearing;

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

        // 地図：今いる部屋から次の階の部屋と手掛かりを出し、押した部屋へ道を歩いて進み、宝箱を開ける。
        [UnityTest]
        public IEnumerator TheMapWalksTheRoadToTheRoomChosenAndOpensItsChest()
        {
            // 第5層の戦闘を終えた所から。次の第6層は、どの道も宝箱の部屋。
            var here = StartAt(4);
            Do(services.Adventure.ClearAsync(here.Id, CancellationToken.None));
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                ExplorationReady,
                value => bootstrap = value
            );
            var view = bootstrap.View;
            var run = AdventureSession.Current.Run;
            Assert.That(view.LocationText, Is.EqualTo(Forest + " 第5層"));
            Assert.That(view.PromptText, Is.EqualTo(ExplorationRoom.ChoosePrompt));
            Assert.That(view.FloorTexts[0], Is.EqualTo("第5層"));
            Assert.That(view.MapImage.texture, Is.Not.Null);
            // 停止中に見せる見本の地図の絵と印は、冒険の地図の上に残さない。
            Assert.That(view.MapPreview.gameObject.activeSelf, Is.False);
            Assert.That(
                view.Markers.GetComponentsInChildren<ExplorationMapMarker>(),
                Has.Length.EqualTo(view.ShownMarkers.Count)
            );
            foreach (var next in here.Next)
            {
                var marker = view.MarkerOf(next);
                Assert.That(marker, Is.Not.Null, next);
                Assert.That(marker.IsExit, Is.True);
                Assert.That(marker.HintText, Is.EqualTo("宝箱がありそう"));
                SceneTests.AssertTouchSize(marker.Button.transform);
            }
            // 最奥の間はいつも見え、押しても進まない。通った部屋は出さない。
            var boss = view.MarkerOf(AdventureRouteMap.BossId);
            Assert.That(boss, Is.Not.Null);
            Assert.That(boss.IsExit, Is.False);
            Assert.That(boss.Button.interactable, Is.False);
            Assert.That(view.MarkerOf(AdventureRouteMap.EntranceId), Is.Null);
            Assert.That(view.MarkerOf(here.Id), Is.Null);
            SceneTests.AssertTouchSize(view.MenuButton.transform, 0.7f);

            // 続けて押しても、1つの部屋だけを選ぶ。
            var chosen = here.Next[0];
            view.MarkerOf(chosen).Button.onClick.Invoke();
            if (here.Next.Count > 1)
                view.MarkerOf(here.Next[1]).Button.onClick.Invoke();
            Assert.That(view.Walking, Is.True);

            // 地図を消さずに、カメラが道に沿って選んだ部屋へ追いかける。
            var from = view.CameraPoint;
            var to = run.Map.PointOf(chosen);
            yield return SceneTests.WaitUntil(
                () => view.CameraPoint.x > (from.x + to.T) / 2f,
                4f,
                "The camera did not follow the party."
            );
            Assert.That(view.Walking, Is.True);
            Assert.That(view.MapImage.color.a, Is.EqualTo(1f));
            yield return SceneTests.WaitUntil(
                () => view.LocationText == Forest + " 第6層" && ExplorationReady(bootstrap),
                6f,
                "The next room did not show."
            );
            // 着いた部屋の上で止まり、霧から現れた印は見え切っている。
            Assert.That(view.CameraPoint, Is.EqualTo(new Vector2(to.T, to.S)));
            Assert.That(view.ShownMarkers.All(marker => marker.Group.alpha == 1f), Is.True);
            var saved = services
                .Adventure.LoadAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Assert.That(saved.Run.Route[^1], Is.EqualTo(chosen));
            Assert.That(saved.Run.Route.Count, Is.EqualTo(run.Route.Count + 1));
            Assert.That(view.PromptText, Is.EqualTo(ExplorationRoom.ChestPrompt));
            Assert.That(view.ShownMarkers.Where(marker => marker.IsExit), Is.Empty);
            Assert.That(view.ChestButton.gameObject.activeSelf, Is.True);

            view.ChestButton.onClick.Invoke();
            yield return WaitForDialog(view.Dialog, "宝箱を開けた！");
            Assert.That(view.Dialog.BodyText, Does.StartWith("ACTボーナス「"));
            Assert.That(view.Dialog.Choose(AdventureTexts.NextChoice), Is.True);
            Assert.That(view.Dialog.IsShown, Is.False);
            Assert.That(view.ChestSprite.texture, Is.SameAs(view.ChestOpen));
            Assert.That(
                view.ShownMarkers.Where(marker => marker.IsExit).Select(marker => marker.RoomId),
                Is.EquivalentTo(AdventureSession.Current.Run.Room.Next)
            );
        }

        // 1本指で地図を引くと道の先を見られ、先の部屋はどれも種類が分かる。カメラに近づいた部屋は
        // 大きくなる。部屋を押すと、カメラは先を見ていた所からパーティへ戻って追いかける。
        [UnityTest]
        public IEnumerator OneFingerLooksAheadAndTheWalkBringsTheCameraBack()
        {
            StartAt(0);
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                ExplorationReady,
                value => bootstrap = value
            );
            var view = bootstrap.View;
            var run = AdventureSession.Current.Run;
            var ahead = run.Map.Rooms.Where(room => room.Floor > run.Floor).ToArray();
            Assert.That(
                view.ShownMarkers.Select(marker => marker.RoomId),
                Is.EquivalentTo(ahead.Select(room => room.Id))
            );
            foreach (var room in ahead)
                Assert.That(
                    view.MarkerOf(room.Id).Icon.sprite,
                    Is.SameAs(view.KindIcon(room.Kind)),
                    room.Id
                );
            var last = ahead.First(room => room.Floor == run.Map.BossFloor - 1);
            Assert.That(view.MarkerOf(last.Id).Sight, Is.EqualTo(ExplorationRoomSight.Far));
            Assert.That(
                view.MarkerOf(AdventureRouteMap.BossId).Sight,
                Is.EqualTo(ExplorationRoomSight.Detail)
            );

            // 下へ引くと先へ進み、最奥の3階手前で止まる。最後の階の部屋も大きく見える。
            var start = view.CameraPoint;
            yield return Pull(view, -160f);
            Assert.That(view.CameraPoint.x, Is.GreaterThan(start.x));
            Assert.That(view.MarkerOf(last.Id).Sight, Is.EqualTo(ExplorationRoomSight.Detail));
            Assert.That(view.FloorTexts, Does.Contain(AdventureCatalog.FloorText(last.Floor)));
            // 上へ引くと、パーティの所で止まる。
            yield return Pull(view, 160f);
            Assert.That(view.CameraPoint, Is.EqualTo(start));

            // 少し先を見たまま部屋を押すと、カメラはパーティへ戻り、着いた部屋の上で止まる。
            yield return Drag(view, -160f);
            Assert.That(view.CameraPoint.x, Is.GreaterThan(start.x));
            yield return SceneTests.WaitUntil(() => ExplorationReady(bootstrap));
            var chosen = run.Room.Next[0];
            var to = run.Map.PointOf(chosen);
            view.MarkerOf(chosen).Button.onClick.Invoke();
            Assert.That(view.Walking, Is.True);
            yield return SceneTests.WaitUntil(
                () => view.LocationText == Forest + " 第2層",
                6f,
                "The next room did not show."
            );
            Assert.That(view.CameraPoint, Is.EqualTo(new Vector2(to.T, to.S)));
        }

        // 地図を1本指で上下に動かす（dy は画面のピクセル、負で下へ引く）。
        private static IEnumerator Drag(ExplorationView view, float dy)
        {
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var data = new PointerEventData(EventSystem.current)
            {
                position = center + new Vector2(0f, dy),
                delta = new Vector2(0f, dy),
            };
            ExecuteEvents.Execute(view.Drag.gameObject, data, ExecuteEvents.dragHandler);
            yield return null;
        }

        // カメラが止まるまで、同じ向きに引き続ける。
        private static IEnumerator Pull(ExplorationView view, float dy)
        {
            for (int i = 0; i < 60; i++)
            {
                var before = view.CameraPoint;
                yield return Drag(view, dy);
                if (view.CameraPoint == before)
                    yield break;
            }
            Assert.Fail("The camera did not stop.");
        }

        [UnityTest]
        public IEnumerator TheMenuEndsTheAdventureWithItsResult()
        {
            var here = StartAt(1);
            Do(services.Adventure.ClearAsync(here.Id, CancellationToken.None));
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                ExplorationReady,
                value => bootstrap = value
            );
            var view = bootstrap.View;

            // 端末の戻るキーでもメニューを開く。ルートは地図そのものに出ているため、メニューにない。
            view.PressBack();
            Assert.That(view.Dialog.TitleText, Is.EqualTo(AdventureTexts.MenuTitle));
            Assert.That(
                view.Dialog.ChoiceLabels,
                Is.EqualTo(
                    new[]
                    {
                        AdventureTexts.SuspendChoice,
                        AdventureTexts.QuitChoice,
                        AdventureTexts.CloseChoice,
                    }
                )
            );
            view.PressBack();
            Assert.That(view.Dialog.IsShown, Is.False);

            view.MenuButton.onClick.Invoke();
            view.Dialog.Choose(AdventureTexts.QuitChoice);
            Assert.That(view.Dialog.TitleText, Is.EqualTo(AdventureTexts.QuitTitle));
            view.Dialog.Choose(AdventureTexts.QuitChoice);
            yield return WaitForDialog(view.Dialog, "帰還");
            Assert.That(
                view.Dialog.BodyText,
                Does.StartWith(Forest + "　第2層まで到達\n最深記録を更新！")
            );
            Assert.That(AdventureSession.Current.InProgress, Is.False);

            view.Dialog.Choose(AdventureTexts.HomeChoice);
            yield return WaitForScene(SceneNames.Home);
        }

        [UnityTest]
        public IEnumerator ARoomWithABattleOpensTheBattleWithItsEncounter()
        {
            StartAt(1);
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
            StartAt(1);
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
            Assert.That(exploration.View.LocationText, Is.EqualTo(Forest + " 第2層"));
            Assert.That(
                exploration.Flow.Room.Exits.Select(exit => exit.Id),
                Is.EqualTo(AdventureSession.Current.Run.Room.Next)
            );
        }

        [UnityTest]
        public IEnumerator ADefeatOffersTheReviveAndTheReturnAlike()
        {
            StartAt(1);
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
            Assert.That(dialog.BodyText, Does.StartWith(Forest + "　第2層まで到達"));
            dialog.Choose(AdventureTexts.HomeChoice);
            yield return WaitForScene(SceneNames.Home);
            Assert.That(AdventureSession.Current.InProgress, Is.False);
        }

        [UnityTest]
        public IEnumerator BeatingTheBossEndsTheAdventureWithItsResult()
        {
            StartAt(9);
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
            Assert.That(
                dialog.BodyText,
                Does.StartWith(Forest + "　第10層まで到達\n最深記録を更新！")
            );
            dialog.Choose(AdventureTexts.HomeChoice);
            yield return WaitForScene(SceneNames.Home);
            var record = AdventureSession.Current.RecordOf(AdventureCatalog.ForestRuins);
            Assert.That(record.Clears, Is.EqualTo(1));
        }

        // 戦闘の途中でも中断でき、再開すると選んだ部屋から（戦闘の始めから）続ける。
        [UnityTest]
        public IEnumerator SuspendingTheBattleResumesFromTheRoomChosen()
        {
            var room = StartAt(1);
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
            Assert.That(home.View.DestinationFloorLabel.text, Is.EqualTo("第2層"));
            home.View.ResumeButton.onClick.Invoke();
            yield return WaitForScene(SceneNames.Exploration);
            yield return WaitForScene(SceneNames.Battle);
            yield return null;
            var resumed = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.That(resumed.Flow.RoomId, Is.EqualTo(room.Id));
            Assert.That(resumed.Battle.Enemies[1].Hp, Is.EqualTo(resumed.Battle.Enemies[1].MaxHp));
        }

        [UnityTest]
        public IEnumerator TheTravelOfficeSetsOutAtOnce()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.TravelOffice, value => guide = value);
            yield return SceneTests.WaitForTask(guide.Departure.Work);
            var view = guide.View;
            int index = System.Array.FindIndex(
                view.Definition.Destinations,
                destination => destination.Id == AdventureCatalog.ForestRuins
            );
            view.DestinationRows[index].onClick.Invoke();
            // 確認を挟まず、「出発」で冒険を始めて探索へ移る。連打しても始めるのは1回だけ。
            view.Depart.onClick.Invoke();
            view.Depart.onClick.Invoke();
            Assert.That(view.ToastMessage, Is.Empty);
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
