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
    // 冒険の1周（doc/features/screens/adventure.md の冒険の画面）を、実際のシーンで通す。
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

        // 戦闘後の強化と勝利のダイアログは、勝利の演出とドロップ（最奥の間は最大4つ）を1つずつ見せた後に出る。
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

        // 宝箱から覚醒が出たら、中身を見せたあとに「宝箱の覚醒」を開き、祭壇と同じ手順で固有スキルを
        // 1段覚醒させる。選ぶまで閉じない（doc/features/skill-evolution.md の覚醒が起きる場面）。
        [UnityTest]
        public IEnumerator AChestWithAnAwakeningEvolvesAUniqueSkillChosen()
        {
            var here = StartAt(4);
            Do(
                services.Adventure.ClearAsync(
                    here.Id,
                    CancellationToken.None,
                    null,
                    new AdventureClearOptions { Pass = here.Kind == AdventureRoomKind.ShadyChest }
                )
            );
            services.Adventure.NextAwakening = true;
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                ExplorationReady,
                value => bootstrap = value
            );
            var view = bootstrap.View;
            view.MarkerOf(here.Next[0]).Button.onClick.Invoke();
            var dialog = view.Dialog;
            // 中身を札で見せたら、「宝箱を開けた！」を挟まずに覚醒を選ばせる。
            yield return WaitForDialog(
                dialog,
                AdventureDialogs.ChestAwakeningTitle,
                VictorySeconds
            );
            Assert.That(AdventureSession.Current.Run.Contents.Awakening, Is.True);
            Assert.That(dialog.BodyText, Is.EqualTo(AdventureDialogs.ChestAwakeningBody));
            // 選ぶまで閉じない：閉じる選択肢がなく、戻るキーでも閉じない。
            Assert.That(dialog.ChoiceLabels, Has.No.Member(AdventureTexts.CloseChoice));
            dialog.HandleBack();
            Assert.That(dialog.IsShown, Is.True);
            // 仲間・固有スキル・段階を、先頭から選んでいき、最後に覚醒させる。
            for (
                int i = 0;
                i < 4 && !dialog.ChoiceLabels.Contains(AdventureDialogs.EvolveChoice);
                i++
            )
                dialog.Choose(dialog.ChoiceLabels[0]);
            Assert.That(dialog.TitleText, Is.EqualTo(AdventureDialogs.ChestAwakeningTitle));
            dialog.Choose(AdventureDialogs.EvolveChoice);
            yield return SceneTests.WaitUntil(
                () => !dialog.IsShown && !bootstrap.Flow.Busy,
                4f,
                "The awakening did not end."
            );
            StringAssert.EndsWith("に覚醒した！", view.Notice.Message);
        }

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
            // 宝箱から覚醒が出ると、中身のダイアログの後に覚醒のダイアログが続くため、出ないことにする。
            services.Adventure.NextAwakening = false;
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
                // 次に選べるマスは、マスの種類（宝箱）の台を暗くせずに出す。
                Assert.That(
                    marker.Icon.sprite,
                    Is.SameAs(view.KindIcon(AdventureRoomKind.Treasure))
                );
                Assert.That(marker.Icon.color, Is.EqualTo(Color.white));
                // 次に進めるマスは脈打つ道で示し、台の後ろに光は置かない。
                Assert.That(marker.Glow.gameObject.activeSelf, Is.False, next);
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

            // 宝箱は押さなくても震えてから開き、中身をすべての札でまとめて見せる。札を閉じたら、
            // ダイアログを挟まずに次のマスを選べる。
            var reveal = view.Moments.Reveal;
            yield return SceneTests.WaitUntil(
                () => reveal.Shown,
                8f,
                "The chest's plate did not open."
            );
            Assert.That(view.Dialog.IsShown, Is.False);
            int plates = reveal.ShownCount;
            yield return SceneTests.WaitUntil(
                () => !bootstrap.Flow.Busy,
                8f,
                "The chest did not close."
            );
            yield return SceneTests.WaitUntil(() => !reveal.Shown, 2f, "The plates did not close.");
            Assert.That(plates, Is.EqualTo(Mathf.Max(1, AdventureSession.Current.Drops.Count)));
            Assert.That(view.Dialog.IsShown, Is.False);
            Assert.That(view.ChestSprite.texture, Is.SameAs(view.ChestOpen));
            Assert.That(
                view.ShownMarkers.Where(marker => marker.IsExit).Select(marker => marker.RoomId),
                Is.EquivalentTo(AdventureSession.Current.Run.Room.Next)
            );
            // 着いたマスにも、何があったかの印（宝箱）をそのまま残す。
            var arrived = view.MarkerOf(chosen);
            Assert.That(arrived.Icon.sprite, Is.SameAs(view.KindIcon(AdventureRoomKind.Treasure)));
            Assert.That(arrived.Icon.color, Is.EqualTo(Color.white));
            Assert.That(arrived.Group.alpha, Is.EqualTo(1f));
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

        // 勝った戦闘の強化の3枚が開くまで待つ（ドロップを1つずつ見せた後に開く）。
        private static IEnumerator WaitForBoons(BattleBootstrap bootstrap) =>
            SceneTests.WaitUntil(
                () => bootstrap.Overlay.Boons.IsOpen,
                VictorySeconds,
                "The boons were not offered."
            );

        private static void WinAtOnce(BattleInspectView battle)
        {
            foreach (var enemy in battle.Enemies)
                enemy.Hp = 0;
            battle.EndTurn();
        }

        // 勝つとドロップを札で見せ、戦闘後の強化を1人2つずつの中から選ぶと、そのまま次のマスを選ぶ探索へ移る
        // （doc/features/stage-progression.md の戦闘後の強化）。選んだ強化は冒険のあいだ残る。
        [UnityTest]
        public IEnumerator AVictoryGivesTheRoomsRewardAndABoonAndGoesOnToTheNextRoom()
        {
            StartAt(1);
            string runId = AdventureSession.Current.Run.Id;
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var battle = bootstrap.Battle;
            WinAtOnce(battle);
            var dialog = bootstrap.Overlay.Dialog;
            yield return SceneTests.WaitUntil(
                () => bootstrap.Overlay.Reveal.Shown,
                VictorySeconds,
                "The drop was not shown."
            );
            Assert.That(dialog.IsShown, Is.False);
            // 勝つと手札は片付き、ドロップの札と強化に重ならない。
            Assert.That(battle.HandPutAway, Is.True, "The hand is put away on the win.");
            yield return WaitForBoons(bootstrap);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(
                battle.Hand.GetComponent<CanvasGroup>().alpha,
                Is.EqualTo(0f),
                "No card shows under the boons."
            );
            Assert.That(AdventureSession.Current.Run.RoomCleared, Is.True);
            var boons = bootstrap.Overlay.Boons;
            Assert.That(boons.Offers.Count, Is.EqualTo(4 * AdventureBoons.PerMember));
            // 選ぶまでチェックは押せず、戻るキーでも閉じない。
            Assert.That(boons.Confirm.interactable, Is.False);
            bootstrap.Overlay.PressBack();
            Assert.That(boons.IsOpen, Is.True);
            Assert.That(dialog.IsShown, Is.False);
            boons.Pick(1);
            Assert.That(boons.Confirm.interactable, Is.True);
            var chosen = boons.Offers[1];
            boons.Choose();

            yield return WaitForScene(SceneNames.Exploration);
            Assert.That(AdventureVitals.Boons(runId), Is.EqualTo(new[] { chosen }));
            Assert.That(AdventureVitals.PendingBoon(runId), Is.Null);
            var exploration = Object.FindAnyObjectByType<ExplorationBootstrap>();
            yield return SceneTests.WaitUntil(() => ExplorationReady(exploration));
            Assert.That(exploration.View.Boons.IsOpen, Is.False);
            Assert.That(exploration.View.LocationText, Is.EqualTo(Forest + " 第2層"));
            Assert.That(
                exploration.Flow.Room.Exits.Select(exit => exit.Id),
                Is.EqualTo(AdventureSession.Current.Run.Room.Next)
            );
        }

        // 強化を選ぶ前にアプリが終わっても、再開した探索で同じ3枚を開き直す。
        [UnityTest]
        public IEnumerator ABoonNotChosenIsOfferedAgainOnResuming()
        {
            StartAt(1);
            string runId = AdventureSession.Current.Run.Id;
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            WinAtOnce(bootstrap.Battle);
            yield return WaitForBoons(bootstrap);
            var offered = bootstrap.Overlay.Boons.Offers.ToArray();

            var exploration = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                ExplorationReady,
                value => exploration = value
            );
            var boons = exploration.View.Boons;
            Assert.That(boons.IsOpen, Is.True);
            Assert.That(boons.Offers, Is.EqualTo(offered));
            boons.Pick(0);
            boons.Choose();
            // 決めると、上がる仲間が光ってから閉じる。
            yield return SceneTests.WaitUntil(
                () => !boons.IsOpen,
                AdventureBoonPicker.TakeSeconds + 1f,
                "The boons did not close after choosing."
            );
            Assert.That(AdventureVitals.Boons(runId), Is.EqualTo(new[] { offered[0] }));
            Assert.That(AdventureVitals.PendingBoon(runId), Is.Null);
        }

        // 選んだ強化は、次の戦闘のステータスに乗る。戦闘の途中で効く強化は、条件を満たす間だけ乗る。
        [UnityTest]
        public IEnumerator ABoonChosenGrowsThePartyInTheNextBattle()
        {
            StartAt(1);
            string runId = AdventureSession.Current.Run.Id;
            AdventureVitals.ChooseBoon(
                runId,
                new AdventureBoon(
                    "attack",
                    AdventureBoonRank.Rainbow,
                    0,
                    Combat.CardStat.PhysicalAttack
                ),
                true
            );
            AdventureVitals.ChooseBoon(
                runId,
                new AdventureBoon(
                    "party-hp",
                    AdventureBoonRank.Gold,
                    -1,
                    Combat.CardStat.PhysicalAttack
                ),
                false
            );
            AdventureVitals.ChooseBoon(
                runId,
                new AdventureBoon(
                    "backwater",
                    AdventureBoonRank.Gold,
                    0,
                    Combat.CardStat.PhysicalAttack
                ),
                false
            );
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var plain = AdventureParty.Of(
                Party.PartySession.Formation(bootstrap.Party),
                bootstrap.Party,
                bootstrap.Training
            );
            var first = bootstrap.Battle.Allies.Single(ally => ally.Slot == 0);
            Assert.That(
                first.Stats.PhysicalAttack,
                Is.EqualTo(Mathf.RoundToInt(plain[0].Stats.PhysicalAttack * 1.3f)).Within(1)
            );
            Assert.That(
                first.Stats.Hp,
                Is.EqualTo(Mathf.RoundToInt(plain[0].Stats.Hp * 1.08f)).Within(1)
            );
            // 背水は、HPが半分以下になった間だけ、物攻・属攻にさらに30%を足す。
            var battle = bootstrap.Battle;
            int place = System.Array.IndexOf(battle.Allies, first);
            first.Hp = first.MaxHp;
            Assert.That(
                battle.StatsNow(place).PhysicalAttack,
                Is.EqualTo(first.Stats.PhysicalAttack)
            );
            first.Hp = first.MaxHp / 2;
            Assert.That(
                battle.StatsNow(place).PhysicalAttack,
                Is.EqualTo(Mathf.RoundToInt(plain[0].Stats.PhysicalAttack * 1.6f)).Within(1)
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
            // パーティより速い敵がいると、確かめる前に先に動いてHPを減らすことがあるため、始まりのHPで見る。
            Assert.That(half.StartHp, Is.EqualTo(BattleInspectView.StartHpOf(half.MaxHp, 0.5f)));
            Assert.That(fallen.Down, Is.True);
            Assert.That(
                battle.HandCards.Select(index => battle.Cards[index].Caster),
                Has.No.Member(fallenPlace)
            );

            half.Hp = Mathf.Max(1, half.MaxHp / 4);
            float kept = half.Hp / (float)half.MaxHp;
            // 先に動いた速い敵がほかの味方のHPを減らしていることもあるため、勝ったときの割合と比べる。
            var other = battle.Allies.Single(ally => ally.Slot == 2);
            float otherKept = other.Hp / (float)other.MaxHp;
            WinAtOnce(battle);
            yield return WaitForBoons(bootstrap);
            var carried = AdventureVitals.Of(runId);
            Assert.That(carried[0], Is.EqualTo(kept).Within(0.0001f));
            Assert.That(carried[1], Is.Zero);
            Assert.That(carried[2], Is.EqualTo(otherKept).Within(0.0001f));
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

        // 焚き火の休む・鍛える・研鑽は、絵の中の物を押しても、その上の札を押しても選べる。
        // 休憩の1つ前のマスから、地図の休憩のマスを押して歩いて着いたところで、画面の位置を押して確かめる。
        [UnityTest]
        public IEnumerator TheCampfireTakesTapsOnItsThingsAndTags()
        {
            var before = StartAt(7);
            var cleared = Do(
                services.Adventure.ClearAsync(
                    before.Id,
                    CancellationToken.None,
                    null,
                    new AdventureClearOptions { Pass = before.Kind == AdventureRoomKind.ShadyChest }
                )
            );
            var rest = cleared.Run.Exits[0];
            Assert.That(rest.Kind, Is.EqualTo(AdventureRoomKind.Rest));
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                value => value.Flow != null,
                value => bootstrap = value
            );
            yield return SceneTests.WaitUntil(
                () => ExplorationReady(bootstrap),
                8f,
                "The exploration did not open."
            );
            bootstrap.View.MarkerOf(rest.Id).Button.onClick.Invoke();
            yield return WaitForCampfire(bootstrap);
            yield return SceneTests.WaitUntil(
                () => !bootstrap.View.Walking && !bootstrap.Transition.IsPlaying,
                8f,
                "The party did not arrive."
            );
            var campfire = bootstrap.View.Campfire;
            foreach (
                var button in new[]
                {
                    campfire.RestButton,
                    campfire.TrainButton,
                    campfire.StudyButton,
                }
            )
                Assert.That(
                    HandlerAt(button.transform),
                    Is.EqualTo(button.gameObject),
                    button.name
                );

            TapAt(campfire.TrainButton.transform.Find("Tag"));
            Assert.That(campfire.TrainShown, Is.True);
            campfire.TrainBack.onClick.Invoke();
            TapAt(campfire.StudyButton.transform.Find("Tag"));
            Assert.That(campfire.StudyShown, Is.True);
            campfire.StudyBack.onClick.Invoke();
            TapAt(campfire.RestButton.transform.Find("Tag"));
            Assert.That(campfire.IsOpen, Is.False);
            yield return SceneTests.WaitUntil(
                () => AdventureSession.Current.Run.RoomCleared && ExplorationReady(bootstrap),
                8f,
                "The rest was not taken."
            );
            Assert.That(bootstrap.View.Notice.LastMessage, Is.EqualTo(AdventureTexts.Rested));
        }

        // 画面で target の中央を押したときに、押したことを受け取る物。
        private static GameObject HandlerAt(Transform target)
        {
            var rect = (RectTransform)target;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    rect.TransformPoint(rect.rect.center)
                ),
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, target.name);
            return ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        }

        // 画面で target の中央を、指で押して離したように押す。
        private static void TapAt(Transform target)
        {
            var handler = HandlerAt(target);
            ExecuteEvents.Execute(
                handler,
                new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerClickHandler
            );
        }

        // 「？」のマスに着くと、サーバーが決めた中身の種類のマスになり、その種類を知らせる。
        [UnityTest]
        public IEnumerator AMysteryRoomTurnsIntoWhatItHolds()
        {
            services.Adventure.MysteryKind = AdventureRoomKind.Treasure;
            services.Adventure.NextAwakening = false;
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
            // 宝箱になったマスは、着くとすぐに宝箱を開け、中身を見せたらダイアログなしで次のマスを選べる。
            yield return SceneTests.WaitUntil(
                () => AdventureSession.Current.Run.RoomCleared && !bootstrap.Flow.Busy,
                VictorySeconds,
                "The chest did not open."
            );
            Assert.That(bootstrap.View.Dialog.IsShown, Is.False);
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
