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
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            // 持ち越すHPは端末に保存するため、テストのあいだはメモリに置く。
            AdventureVitals.UseStore(new MemoryAdventureVitalsStore());
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            AdventureVitals.UseStore(null);
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
                // 怪しい宝箱は、開けずに通り過ぎて終える。
                if (i < moves - 1)
                    state = Do(
                        source.ClearAsync(
                            room.Id,
                            CancellationToken.None,
                            null,
                            new AdventureClearOptions
                            {
                                Pass = room.Kind == AdventureRoomKind.ShadyChest,
                            }
                        )
                    );
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

        // 勝利のダイアログは、勝利の演出とドロップ（最奥の間は最大4つ）を1つずつ見せた後に出る。
        private const float VictorySeconds = 30f;

        private static IEnumerator WaitForDialog(
            UI.GameDialog dialog,
            string title,
            float seconds = 8f
        ) =>
            SceneTests.WaitUntil(
                () => dialog.IsShown && !dialog.Busy && dialog.TitleText == title,
                seconds,
                $"The dialog \"{title}\" did not show (it shows \"{dialog.TitleText}\")."
            );

        // 地図：今いるマスから次の階のマスを大きい札で出し、押したマスへ光の道を歩いて進み、宝箱を開ける。
        [UnityTest]
        public IEnumerator TheMapWalksTheRoadToTheRoomChosenAndOpensItsChest()
        {
            // 第5層の戦闘を終えた所から。次の第6層は、どの道も宝箱のマス。
            var here = StartAt(4);
            Do(
                services.Adventure.ClearAsync(
                    here.Id,
                    CancellationToken.None,
                    null,
                    new AdventureClearOptions { Pass = here.Kind == AdventureRoomKind.ShadyChest }
                )
            );
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                ExplorationReady,
                value => bootstrap = value
            );
            var view = bootstrap.View;
            var run = AdventureSession.Current.Run;
            Assert.That(view.LocationText, Is.EqualTo(Forest + " 第5層"));
            // 進む道は脈打つ道と札で伝えるため、案内は出さない。
            Assert.That(view.PromptText, Is.Empty);
            // 地図には道のマスを全部出し、停止中に作り込んだ見本の印は残さない。
            Assert.That(view.ShownMarkers.Count, Is.EqualTo(run.Map.Rooms.Count));
            Assert.That(
                view.Markers.GetComponentsInChildren<ExplorationMapMarker>(),
                Has.Length.EqualTo(view.ShownMarkers.Count)
            );
            Assert.That(view.Ground.texture, Is.Not.Null);
            foreach (var next in here.Next)
            {
                var marker = view.MarkerOf(next);
                Assert.That(marker, Is.Not.Null, next);
                Assert.That(marker.IsExit, Is.True);
                // 次に選べるマスは大きい札で、地の色がマスの種類（宝箱）を示す。
                Assert.That(
                    marker.Ring.rectTransform.sizeDelta.x,
                    Is.EqualTo(ExplorationView.LargeRing)
                );
                Assert.That(
                    marker.Plate.color,
                    Is.EqualTo(ExplorationView.PlateOf(AdventureRoomKind.Treasure))
                );
                SceneTests.AssertTouchSize(marker.Button.transform);
            }
            // 最奥の間・入口・今いるマスは、押しても進まない。
            foreach (
                var id in new[] { AdventureRouteMap.BossId, AdventureRouteMap.EntranceId, here.Id }
            )
                Assert.That(view.MarkerOf(id).Button.interactable, Is.False, id);
            SceneTests.AssertTouchSize(view.MenuButton.transform, 0.7f);
            // 4人は今いるマスに、画面の左寄りに立つ。
            Assert.That(view.PartyPosition, Is.EqualTo(view.PointOf(here.Id)));

            // 続けて押しても、1つのマスだけを選ぶ。
            var chosen = here.Next[0];
            view.MarkerOf(chosen).Button.onClick.Invoke();
            if (here.Next.Count > 1)
                view.MarkerOf(here.Next[1]).Button.onClick.Invoke();
            Assert.That(view.Walking, Is.True);
            var from = view.PartyPosition;
            var to = view.PointOf(chosen);
            yield return SceneTests.WaitUntil(
                () => view.PartyPosition.x > (from.x + to.x) / 2f,
                4f,
                "The party did not walk."
            );
            Assert.That(view.Walking, Is.True);
            // 宝箱のマスは、着くとすぐに宝箱を開け始める（開け終えるまで、ほかの操作は受け付けない）。
            yield return SceneTests.WaitUntil(
                () => view.LocationText == Forest + " 第6層" && !view.Walking,
                6f,
                "The next room did not show."
            );
            Assert.That(bootstrap.Flow.Busy, Is.True);
            // 着いたマスに立ち、地図は4人を画面の左寄りに置く所まで追いかけている。
            Assert.That(view.PartyPosition, Is.EqualTo(to));
            Assert.That(
                view.Scroll,
                Is.EqualTo(
                        Mathf.Clamp(
                            to.x - view.Map.rect.width * ExplorationView.PartyAtScreen,
                            0f,
                            view.MaxScroll
                        )
                    )
                    .Within(1f)
            );
            var saved = services
                .Adventure.LoadAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Assert.That(saved.Run.Route[^1], Is.EqualTo(chosen));
            Assert.That(saved.Run.Route.Count, Is.EqualTo(run.Route.Count + 1));
            Assert.That(view.PromptText, Is.Empty);
            Assert.That(view.ShownMarkers.Where(marker => marker.IsExit), Is.Empty);

            // 宝箱は押さなくても震えてから開き、中身を札で見せる。ダイアログは札を閉じてから、題だけで出す。
            var reveal = view.Moments.Reveal;
            yield return SceneTests.WaitUntil(
                () => reveal.Shown,
                8f,
                "The chest's plate did not open."
            );
            Assert.That(view.Dialog.IsShown, Is.False);
            yield return WaitForDialog(view.Dialog, "宝箱を開けた！");
            Assert.That(view.Dialog.BodyText, Is.Empty);
            Assert.That(view.Dialog.Choose(AdventureTexts.NextChoice), Is.True);
            Assert.That(view.Dialog.IsShown, Is.False);
            Assert.That(view.ChestSprite.texture, Is.SameAs(view.ChestOpen));
            Assert.That(
                view.ShownMarkers.Where(marker => marker.IsExit).Select(marker => marker.RoomId),
                Is.EquivalentTo(AdventureSession.Current.Run.Room.Next)
            );
        }

        // 1本指で地図を左右に動かすと道の先を見られ、先のマスはどれも種類が分かる。マスを押すと、
        // 地図は先を見ていた所から4人の所へ戻って追いかける。
        [UnityTest]
        public IEnumerator OneFingerLooksAheadAndTheWalkBringsTheMapBack()
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
            foreach (var room in run.Map.Rooms.Where(room => room.Floor > run.Floor))
                Assert.That(
                    view.MarkerOf(room.Id).Icon.sprite,
                    Is.SameAs(view.KindIcon(room.Kind)),
                    room.Id
                );
            // 地図は画面より広く、入口からは最奥の間が画面の外にある。
            Assert.That(view.Scroll, Is.Zero);
            Assert.That(view.MaxScroll, Is.GreaterThan(0f));
            Assert.That(
                view.ScreenXOf(AdventureRouteMap.BossId),
                Is.GreaterThan(view.Map.rect.width)
            );
            Assert.That(view.ScrollTrack.gameObject.activeSelf, Is.True);

            // 左へ引くと先へ進み、最奥の間が画面に入る所で止まる。右へ引くと、入口の所で止まる。
            yield return Pull(view, -200f);
            Assert.That(view.Scroll, Is.EqualTo(view.MaxScroll));
            Assert.That(view.ScreenXOf(AdventureRouteMap.BossId), Is.LessThan(view.Map.rect.width));
            yield return Pull(view, 200f);
            Assert.That(view.Scroll, Is.Zero);

            // 少し先を見たままマスを押すと、地図は4人へ戻り、着いたマスで止まる。
            yield return Drag(view, -300f);
            Assert.That(view.Scroll, Is.GreaterThan(0f));
            yield return SceneTests.WaitUntil(() => ExplorationReady(bootstrap));
            var chosen = run.Room.Next[0];
            view.MarkerOf(chosen).Button.onClick.Invoke();
            Assert.That(view.Walking, Is.True);
            // 第2層は魔物のマスで、着くとすぐ戦闘へ移るため、着いたマスが見えた所で確かめる。
            yield return SceneTests.WaitUntil(
                () => view.LocationText == Forest + " 第2層" && !view.Walking,
                6f,
                "The next room did not show."
            );
            var to = view.PointOf(chosen);
            Assert.That(
                view.Scroll,
                Is.EqualTo(
                        Mathf.Clamp(
                            to.x - view.Map.rect.width * ExplorationView.PartyAtScreen,
                            0f,
                            view.MaxScroll
                        )
                    )
                    .Within(1f)
            );
        }

        // 最奥の近くでは、地図は右の端で止まり、最奥の間が画面に入る。
        [UnityTest]
        public IEnumerator NearTheDeepestRoomTheMapShowsItsEnd()
        {
            // 最奥の直前の第9層（休憩）を終えた所から。
            var here = StartAt(8);
            Do(services.Adventure.ClearAsync(here.Id, CancellationToken.None));
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                ExplorationReady,
                value => bootstrap = value
            );
            var view = bootstrap.View;
            Assert.That(view.Scroll, Is.EqualTo(view.MaxScroll).Within(1f));
            Assert.That(view.ScreenXOf(AdventureRouteMap.BossId), Is.LessThan(view.Map.rect.width));
            Assert.That(view.MarkerOf(AdventureRouteMap.BossId).IsExit, Is.True);
            yield return Drag(view, -300f);
            Assert.That(view.Scroll, Is.EqualTo(view.MaxScroll).Within(1f));
        }

        // 地図を1本指で左右に動かす（dx は画面のピクセル、負で左へ引く）。
        private static IEnumerator Drag(ExplorationView view, float dx)
        {
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var data = new PointerEventData(EventSystem.current)
            {
                position = center + new Vector2(dx, 0f),
                delta = new Vector2(dx, 0f),
            };
            ExecuteEvents.Execute(view.Drag.gameObject, data, ExecuteEvents.dragHandler);
            yield return null;
        }

        // 地図が止まるまで、同じ向きに引き続ける。
        private static IEnumerator Pull(ExplorationView view, float dx)
        {
            for (int i = 0; i < 80; i++)
            {
                float before = view.Scroll;
                yield return Drag(view, dx);
                if (Mathf.Approximately(view.Scroll, before))
                    yield break;
            }
            Assert.Fail("The map did not stop.");
        }

        // その種類のマスがある道で冒険を始め、そのマスまで進む（途中のマスは終えて進む）。
        private AdventureRoom WalkTo(AdventureRoomKind kind)
        {
            var source = services.Adventure;
            for (int attempt = 0; attempt < 300; attempt++)
            {
                var state = Do(
                    source.StartAsync(AdventureCatalog.ForestRuins, CancellationToken.None)
                );
                var map = state.Run.Map;
                var target = map.Rooms.FirstOrDefault(room => room.Kind == kind);
                if (target == null)
                {
                    Do(source.EndAsync(AdventureEndReason.Retreat, CancellationToken.None));
                    continue;
                }
                var back = new System.Collections.Generic.Dictionary<string, string>();
                var queue = new System.Collections.Generic.Queue<string>();
                queue.Enqueue(AdventureRouteMap.EntranceId);
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    foreach (var next in map.Find(id).Next)
                        if (!back.ContainsKey(next))
                        {
                            back[next] = id;
                            queue.Enqueue(next);
                        }
                }
                var path = new System.Collections.Generic.List<string>();
                for (var id = target.Id; id != AdventureRouteMap.EntranceId; id = back[id])
                    path.Insert(0, id);
                foreach (var id in path)
                {
                    var room = map.Find(id);
                    Do(source.MoveAsync(room, CancellationToken.None));
                    if (id == target.Id)
                        break;
                    Do(
                        source.ClearAsync(
                            id,
                            CancellationToken.None,
                            null,
                            new AdventureClearOptions
                            {
                                Pass = room.Kind == AdventureRoomKind.ShadyChest,
                            }
                        )
                    );
                }
                return target;
            }
            Assert.Fail("No route had a room of " + kind);
            return null;
        }

        // 怪しい宝箱は、着くと開けるか通り過ぎるかを聞く。閉じたあとは、宝箱を押すと聞き直す。
        // 開けてミミックなら、戦闘へ移る。
        [UnityTest]
        public IEnumerator TheShadyChestAsksFirstAndAMimicGoesToBattle()
        {
            var chest = WalkTo(AdventureRoomKind.ShadyChest);
            services.Adventure.NextMimic = true;
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                ExplorationReady,
                value => bootstrap = value
            );
            var view = bootstrap.View;
            Assert.That(view.PromptText, Is.Empty);
            yield return WaitForDialog(view.Dialog, AdventureDialogs.ShadyChestTitle);
            Assert.That(view.Dialog.Choose(AdventureTexts.CloseChoice), Is.True);
            Assert.That(view.ChestButton.gameObject.activeSelf, Is.True);
            view.ChestButton.onClick.Invoke();
            yield return WaitForDialog(view.Dialog, AdventureDialogs.ShadyChestTitle);
            Assert.That(
                view.Dialog.ChoiceLabels,
                Is.EqualTo(
                    new[]
                    {
                        AdventureDialogs.OpenChoice,
                        AdventureDialogs.PassChoice,
                        AdventureTexts.CloseChoice,
                    }
                )
            );
            Assert.That(view.Dialog.Choose(AdventureDialogs.OpenChoice), Is.True);
            yield return WaitForScene(SceneNames.Battle);
            Assert.That(AdventureSession.Current.Run.RoomId, Is.EqualTo(chest.Id));
            Assert.That(AdventureSession.Current.Run.Contents.Mimic, Is.True);
            Assert.That(AdventureSession.Current.Run.InBattle, Is.True);
        }

        // 進化の祭壇は、仲間・固有スキル・進化先を選んで確かめると進化させ、物を落とさずにマスを終える。
        [UnityTest]
        public IEnumerator TheAltarEvolvesTheUniqueSkillChosen()
        {
            WalkTo(AdventureRoomKind.Altar);
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                ExplorationReady,
                value => bootstrap = value
            );
            var view = bootstrap.View;
            Assert.That(view.PromptText, Is.Empty);
            // 着くと、押さなくても祭壇の選択を開く。
            yield return WaitForDialog(view.Dialog, AdventureDialogs.AltarTitle);
            // 仲間 → （固有スキルが2つあれば）固有スキル → 進化先 → 確かめる、の順に、いつも最初を選ぶ。
            for (int step = 0; step < 4; step++)
            {
                var labels = view.Dialog.ChoiceLabels;
                if (labels.Contains(AdventureDialogs.EvolveChoice))
                {
                    Assert.That(view.Dialog.Choose(AdventureDialogs.EvolveChoice), Is.True);
                    break;
                }
                Assert.That(view.Dialog.Choose(labels[0]), Is.True);
                yield return WaitForDialog(view.Dialog, AdventureDialogs.AltarTitle);
            }
            yield return SceneTests.WaitUntil(
                () => AdventureSession.Current.Run.RoomCleared && ExplorationReady(bootstrap),
                8f,
                "The altar was not used."
            );
            Assert.That(view.Notice.LastMessage, Does.Contain("覚醒した"));
            Assert.That(
                AdventureSession.Current.Run.Drops.Where(drop =>
                    drop.RoomId == AdventureSession.Current.Run.RoomId
                ),
                Is.Empty
            );
        }

        // 行商人のマスに着くと品物の一覧を開き、確かめてから買うと売り切れになる。立ち去ると先へ進める。
        [UnityTest]
        public IEnumerator TheMerchantSellsAndTheWayGoesOnOnceLeft()
        {
            WalkTo(AdventureRoomKind.Merchant);
            services.Adventure.Runes = 100000;
            Do(services.Adventure.LoadAsync(CancellationToken.None));
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                value => value.Flow != null,
                value => bootstrap = value
            );
            var view = bootstrap.View;
            yield return WaitForDialog(view.Dialog, AdventureDialogs.MerchantTitle);
            var wares = AdventureSession.Current.Run.Contents.Wares;
            Assert.That(wares.Count, Is.EqualTo(AdventureWares.Skills + AdventureWares.Gear));
            Assert.That(view.Dialog.ChoiceLabels.Length, Is.EqualTo(wares.Count + 1));
            string first = AdventureTexts.WareLine(wares[0]);
            long before = AdventureSession.Current.Runes;
            Assert.That(view.Dialog.Choose(first), Is.True);
            yield return WaitForDialog(view.Dialog, AdventureDialogs.MerchantTitle);
            Assert.That(view.Dialog.Choose(AdventureDialogs.BuyChoice), Is.True);
            yield return SceneTests.WaitUntil(
                () =>
                    AdventureSession.Current.Run.Contents.Wares[0].Sold
                    && view.Dialog.IsShown
                    && !view.Dialog.Busy
                    && view.Dialog.TitleText == AdventureDialogs.MerchantTitle,
                8f,
                "The ware was not bought."
            );
            Assert.That(AdventureSession.Current.Runes, Is.EqualTo(before - wares[0].Price));
            Assert.That(
                view.Dialog.ChoiceLabels,
                Does.Contain(
                    AdventureTexts.WareLine(AdventureSession.Current.Run.Contents.Wares[0])
                )
            );
            Assert.That(view.Dialog.Choose(AdventureDialogs.LeaveChoice), Is.True);
            yield return SceneTests.WaitUntil(
                () => AdventureSession.Current.Run.RoomCleared && ExplorationReady(bootstrap),
                8f,
                "The merchant was not left."
            );
            Assert.That(view.ShownMarkers.Where(marker => marker.IsExit), Is.Not.Empty);
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
            // 探索の画面で「！」と帯を出し、左右からの幕が閉じてから戦闘へ移る。
            var moments = bootstrap.View.Moments;
            yield return SceneTests.WaitUntil(
                () => moments.Band.gameObject.activeSelf,
                4f,
                "The band of the monsters did not show."
            );
            Assert.That(moments.Band.Label.text, Is.EqualTo(ExplorationRoom.BattlePrompt));
            Assert.That(moments.Mark.gameObject.activeSelf, Is.True);
            yield return WaitForScene(SceneNames.Battle);
            yield return null;
            var battle = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.That(battle.Flow, Is.Not.Null);
            Assert.That(battle.Battle.EndsWithOutcome, Is.True);
            // 魔物が現れる演出のあいだは、まだ手札を配らない。
            Assert.That(battle.Battle.Acting, Is.True);
            Assert.That(battle.Battle.HandCards, Is.Empty);
            // 魔物は、幕が開ききるまで暗がりで待ち、開いてから歩み出る。
            yield return SceneTests.WaitUntil(
                () => !battle.Transition.IsCovered && !battle.Transition.IsPlaying,
                4f,
                "The battle did not open."
            );
            Assert.That(
                battle
                    .Battle.Enemies.Where(enemy => enemy.Alive)
                    .Select(enemy => enemy.Group.alpha),
                Is.All.EqualTo(0f)
            );
            // 魔物の気配の部屋は、森の小さな敵と中くらいの敵の2体だけ。奥の大きな敵はいない。
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
            // 勝利の演出のあと、ドロップを札で見せてからダイアログを出す。本文に報酬は書かない。
            yield return SceneTests.WaitUntil(
                () => bootstrap.Overlay.Reveal.Shown,
                VictorySeconds,
                "The drop was not shown."
            );
            Assert.That(dialog.IsShown, Is.False);
            yield return WaitForDialog(dialog, BattleAdventureFlow.VictoryTitle, VictorySeconds);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(dialog.BodyText, Is.Empty);
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

        // マスの間で持ち越したHPから戦闘を始め、倒れたまま来た味方のカードは手札に来ない。勝ったときの
        // HPを次のマスへ持ち越す（doc/features/stage-progression.md の HPの持ち越し）。
        [UnityTest]
        public IEnumerator TheBattleStartsFromTheCarriedHpAndKeepsTheHpItIsWonWith()
        {
            StartAt(1);
            string runId = AdventureSession.Current.Run.Id;
            AdventureVitals.Keep(runId, new[] { 0.5f, 0f, 1f, 1f });
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var battle = bootstrap.Battle;
            var half = battle.Allies.Single(ally => ally.Slot == 0);
            var fallen = battle.Allies.Single(ally => ally.Slot == 1);
            int fallenPlace = System.Array.IndexOf(battle.Allies, fallen);
            Assert.That(half.Hp, Is.EqualTo(BattleInspectView.StartHpOf(half.MaxHp, 0.5f)));
            Assert.That(fallen.Down, Is.True);
            Assert.That(
                battle.HandCards.Select(index => battle.Cards[index].Caster),
                Has.No.Member(fallenPlace)
            );

            half.Hp = Mathf.Max(1, half.MaxHp / 4);
            float kept = half.Hp / (float)half.MaxHp;
            foreach (var enemy in battle.Enemies)
                enemy.Hp = 0;
            battle.EndTurn();
            yield return WaitForDialog(
                bootstrap.Overlay.Dialog,
                BattleAdventureFlow.VictoryTitle,
                VictorySeconds
            );
            var carried = AdventureVitals.Of(runId);
            Assert.That(carried[0], Is.EqualTo(kept).Within(0.0001f));
            Assert.That(carried[1], Is.Zero);
            Assert.That(carried[2], Is.EqualTo(1f));
        }

        // 休憩のマスの焚き火を開くまで待つ。
        private static IEnumerator WaitForCampfire(ExplorationBootstrap bootstrap) =>
            SceneTests.WaitUntil(
                () => bootstrap.View.Campfire != null && bootstrap.View.Campfire.CampShown,
                8f,
                "The campfire did not open."
            );

        // 最奥の直前の休憩のマスに着くと焚き火を開き、休む・鍛える・研鑽を選ぶまで先へ進めない。
        // 戻るキーでは閉じない。休む（テント）を押すと、全員を最大HPの40%回復し、倒れた味方も起き上がる。
        [UnityTest]
        public IEnumerator TheRestHealsThePartyWhenItRests()
        {
            var rest = StartAt(8);
            Assert.That(rest.Kind, Is.EqualTo(AdventureRoomKind.Rest));
            string runId = AdventureSession.Current.Run.Id;
            AdventureVitals.Keep(runId, new[] { 0.5f, 0f, 0.9f, 1f });
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                value => value.Flow != null,
                value => bootstrap = value
            );
            yield return WaitForCampfire(bootstrap);
            var campfire = bootstrap.View.Campfire;
            // 焚き火を囲むのは編成の仲間で、空いた枠には誰も立たない。
            Assert.That(
                campfire.Party.Count(image => image.gameObject.activeSelf),
                Is.EqualTo(campfire.Members.Count(member => member != null))
            );
            Assert.That(AdventureSession.Current.Run.RoomCleared, Is.False);
            bootstrap.View.PressBack();
            Assert.That(campfire.CampShown, Is.True);
            Assert.That(bootstrap.View.Dialog.IsShown, Is.False);
            campfire.RestButton.onClick.Invoke();
            Assert.That(campfire.IsOpen, Is.False);
            yield return SceneTests.WaitUntil(
                () => AdventureSession.Current.Run.RoomCleared && ExplorationReady(bootstrap),
                8f,
                "The rest was not taken."
            );
            Assert.That(
                AdventureVitals.Of(runId),
                Is.EqualTo(new[] { 0.9f, 0.4f, 1f, 1f }).Within(0.0001f)
            );
            Assert.That(bootstrap.View.Notice.LastMessage, Is.EqualTo(AdventureTexts.Rested));
            Assert.That(bootstrap.View.PromptText, Is.Empty);
            Assert.That(AdventureVitals.HonedSlots(runId), Is.EqualTo(new[] { 0, 0, 0, 0 }));
        }

        // 鍛える（切り株と薪）を押すと仲間を台座に並べ、選ぶまで決められない。選んで決めると、その仲間を
        // その冒険のあいだ鍛え、HPは回復しない。戻るキーで焚き火へ戻る。
        [UnityTest]
        public IEnumerator TheRestTrainsAMember()
        {
            StartAt(8);
            string runId = AdventureSession.Current.Run.Id;
            AdventureVitals.Keep(runId, new[] { 0.5f, 1f, 1f, 1f });
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                value => value.Flow != null,
                value => bootstrap = value
            );
            yield return WaitForCampfire(bootstrap);
            var campfire = bootstrap.View.Campfire;
            campfire.TrainButton.onClick.Invoke();
            Assert.That(campfire.TrainShown, Is.True);
            Assert.That(campfire.TrainConfirm.interactable, Is.False);
            Assert.That(campfire.TrainTag.gameObject.activeSelf, Is.False);
            bootstrap.View.PressBack();
            Assert.That(campfire.CampShown, Is.True);
            campfire.TrainButton.onClick.Invoke();
            int slot = Enumerable
                .Range(0, campfire.TrainSlots.Length)
                .First(index => campfire.TrainSlots[index].gameObject.activeSelf);
            string name = campfire.TrainNames[slot].text;
            campfire.TrainSlots[slot].onClick.Invoke();
            Assert.That(campfire.PickedMember, Is.EqualTo(slot));
            Assert.That(campfire.TrainTag.gameObject.activeSelf, Is.True);
            Assert.That(campfire.TrainConfirm.interactable, Is.True);
            campfire.TrainConfirm.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => AdventureSession.Current.Run.RoomCleared && ExplorationReady(bootstrap),
                8f,
                "The rest was not taken."
            );
            Assert.That(AdventureVitals.HonedSlots(runId)[slot], Is.EqualTo(1));
            Assert.That(AdventureVitals.HonedSlots(runId).Sum(), Is.EqualTo(1));
            Assert.That(AdventureVitals.Of(runId)[0], Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                bootstrap.View.Notice.LastMessage,
                Is.EqualTo(AdventureTexts.HonedMember(name))
            );
        }

        // 研鑽（木箱の上の本）を押すと、仲間ごとのカードスキルを戦闘のカードで並べる。選ぶと研鑽で
        // どうなるかを出し、決めるとそのカードをその冒険のあいだ強くする。研鑽したカードは次は並ばない。
        [UnityTest]
        public IEnumerator TheRestStudiesACard()
        {
            StartAt(8);
            string runId = AdventureSession.Current.Run.Id;
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                value => value.Flow != null,
                value => bootstrap = value
            );
            yield return WaitForCampfire(bootstrap);
            var campfire = bootstrap.View.Campfire;
            var members = campfire.Members;
            campfire.StudyButton.onClick.Invoke();
            Assert.That(campfire.StudyShown, Is.True);
            Assert.That(campfire.StudyConfirm.interactable, Is.False);
            int index = Enumerable
                .Range(0, campfire.StudyCards.Length)
                .First(i => campfire.StudyCards[i].gameObject.activeSelf);
            var (slot, card) = campfire.StudiedCards[index];
            Assert.That(campfire.StudyFaces[index].Name.text, Is.EqualTo(card.Skill.Name));
            campfire.StudyCards[index].onClick.Invoke();
            Assert.That(campfire.PickedCard, Is.EqualTo(index));
            Assert.That(
                campfire.StudyLine.text,
                Is.EqualTo(ExplorationCampfire.StudyText(card.Skill))
            );
            Assert.That(
                campfire.StudyLine.text,
                Does.Contain(card.Skill.Name + AdventureParty.HonedMark)
            );
            campfire.StudyConfirm.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => AdventureSession.Current.Run.RoomCleared && ExplorationReady(bootstrap),
                8f,
                "The rest was not taken."
            );
            Assert.That(
                AdventureVitals.HonedCards(runId),
                Is.EqualTo(new[] { AdventureVitals.CardKey(slot, card.Skill.Id) })
            );
            Assert.That(
                bootstrap.View.Notice.LastMessage,
                Is.EqualTo(AdventureTexts.HonedCard(card.Skill.Name))
            );
            // 次に焚き火を開いたときは、研鑽したカードを並べない。
            campfire.Open(members, AdventureVitals.HonedCards(runId), null, null, null);
            campfire.StudyButton.onClick.Invoke();
            Assert.That(
                campfire
                    .StudiedCards.Where(entry => entry.Card != null)
                    .Select(entry => (entry.Slot, entry.Card.Skill.Id)),
                Has.No.Member((slot, card.Skill.Id))
            );
            campfire.Close();
        }

        // 「？」のマスに着くと、サーバーが決めた中身の種類のマスになり、その種類を知らせる。
        [UnityTest]
        public IEnumerator AMysteryRoomTurnsIntoWhatItHolds()
        {
            services.Adventure.MysteryKind = AdventureRoomKind.Treasure;
            var mystery = WalkTo(AdventureRoomKind.Mystery);
            var run = AdventureSession.Current.Run;
            Assert.That(run.Map.Find(mystery.Id).Kind, Is.EqualTo(AdventureRoomKind.Mystery));
            Assert.That(run.Room.Kind, Is.EqualTo(AdventureRoomKind.Treasure));
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                value => value.Flow != null,
                value => bootstrap = value
            );
            // 宝箱になったマスは、着くとすぐに宝箱を開ける。
            yield return WaitForDialog(bootstrap.View.Dialog, AdventureDialogs.TreasureTitle);
            Assert.That(
                bootstrap.View.Notice.LastMessage,
                Is.EqualTo(AdventureTexts.Revealed(AdventureRoomKind.Treasure))
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
            // 立ち上がる演出のあとに全快し、次のターンの手札を配る。
            dialog.Choose(AdventureTexts.ReviveChoice(100));
            yield return SceneTests.WaitUntil(
                () => !dialog.IsShown && !battle.Dealing && !battle.Acting,
                8f,
                "The revive did not finish."
            );
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.None));
            Assert.That(battle.Allies.All(ally => ally.Hp == ally.MaxHp), Is.True);
            Assert.That(battle.Enemies[1].Hp, Is.EqualTo(100));
            Assert.That(services.Adventure.Runes, Is.EqualTo(900));
            Assert.That(AdventureSession.Current.Run.ReviveCost, Is.EqualTo(200));

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
            yield return WaitForDialog(dialog, BattleAdventureFlow.VictoryTitle, VictorySeconds);
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

        // 冒険がないときに単体で開いた探索は、開く演出の途中で読み終えても、ホームへ戻る。
        // 「読み込み中…」のまま止まらない。
        [UnityTest]
        public IEnumerator TheExplorationOpenedWithoutAnAdventureGoesHome()
        {
            // サーバーのように、開く演出（元の長さ）の途中で冒険を読み終える。
            UI.SceneTransitionController.DurationScale = 1f;
            services.AdventureLoadSeconds = 0.05f;
            yield return SceneManager.LoadSceneAsync(
                SceneTests.ExplorationPath,
                LoadSceneMode.Single
            );
            yield return WaitForScene(SceneNames.Home);
            Assert.That(AdventureSession.Current.InProgress, Is.False);
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
