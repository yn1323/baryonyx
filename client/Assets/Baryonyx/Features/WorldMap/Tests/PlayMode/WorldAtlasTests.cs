using System.Collections;
using System.Collections.Generic;
using Baryonyx.WorldMap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace Baryonyx.Tests.PlayMode
{
    // 入れ子の地図の試作で、世界地図のマスを1つずつ進み、地域のマスに止まるとエリアマップへ入り、
    // エリアマップの入口に止まると世界地図のその地域のマスへ戻る。
    public sealed class WorldAtlasTests
    {
        private const string NavigatorPath =
            "Assets/Baryonyx/Features/WorldMap/Atlas/WorldAtlas.prefab";

        private GameObject atlas;
        private GameObject events;
        private bool charging;

        [SetUp]
        public void Prepare()
        {
            // 端末の消費のオンオフは、テストの間だけ ON にして、あとで戻す。
            charging = WorldAtlasSession.Charging;
            WorldAtlasSession.Reset();
            WorldAtlasSession.Charging = true;
            WorldAtlasNavigator.Forget();
        }

        [UnityTearDown]
        public IEnumerator Clean()
        {
            if (atlas != null)
                Object.Destroy(atlas);
            if (events != null)
                Object.Destroy(events);
            WorldAtlasSession.Reset();
            WorldAtlasSession.Charging = charging;
            WorldAtlasNavigator.Forget();
            yield return null;
        }

        // 駒が最後に立っていたマスは端末に残り、地図を開き直すと（ダンジョン一覧や戦闘から戻ると）
        // そこから始まる。覚えていたマスが地図にないときは、はじめのマスから始める。
        [UnityTest]
        public IEnumerator TheMapOpensAgainWhereThePartyLastStood()
        {
            var navigator = Spawn();
            yield return null;
            var world = navigator.Current;
            yield return SceneTests.WaitUntil(() => WorldAtlasSession.Loaded);
            int next = PathToNearestEvent(world)[0];
            string square = world.Graph.Nodes[next].Id;
            Assert.That(world.Tap(next), Is.True);
            yield return SceneTests.WaitUntil(
                () => !world.Walking,
                seconds: 5f,
                message: "The party did not reach the next square."
            );
            Assert.That(
                WorldAtlasNavigator.Remembered(),
                Is.EqualTo((WorldAtlasLayout.WorldKey, square))
            );

            Object.Destroy(atlas);
            Object.Destroy(events);
            yield return null;
            navigator = Spawn();
            yield return null;
            Assert.That(navigator.CurrentKey, Is.EqualTo(WorldAtlasLayout.WorldKey));
            world = navigator.Current;
            Assert.That(world.Graph.Nodes[world.PartyAt].Id, Is.EqualTo(square));

            Object.Destroy(atlas);
            Object.Destroy(events);
            yield return null;
            WorldAtlasNavigator.Remember(WorldAtlasLayout.WorldKey, "no-such-square");
            navigator = Spawn();
            yield return null;
            world = navigator.Current;
            Assert.That(
                world.Graph.Nodes[world.PartyAt].Id,
                Is.EqualTo(WorldAtlasLayout.StartSquare)
            );
        }

        // 冒険の世界地図では、エリアマップの左上（家）も世界地図を経ずにすぐHomeへ出る。駒の位置は
        // そのエリアマップに残り、次に地図を開くとそこから始まる。
        [UnityTest]
        public IEnumerator HomeFromAnAreaMapLeavesAtOnceAndTheAreaOpensNextTime()
        {
            var navigator = Spawn();
            int left = 0;
            navigator.Leave = () => left++;
            yield return null;
            Assert.That(navigator.Open("marina", null), Is.True);
            yield return null;
            var coast = navigator.Current;
            Assert.That(coast.Back.gameObject.activeSelf, Is.True);

            coast.Back.onClick.Invoke();
            Assert.That(left, Is.EqualTo(1));
            Assert.That(navigator.Switching, Is.False);
            Assert.That(navigator.CurrentKey, Is.EqualTo("marina"));
            Assert.That(WorldAtlasNavigator.Remembered(), Is.EqualTo(("marina", "gate-marina")));

            Object.Destroy(atlas);
            Object.Destroy(events);
            yield return null;
            navigator = Spawn();
            yield return null;
            Assert.That(navigator.CurrentKey, Is.EqualTo("marina"));
            coast = navigator.Current;
            Assert.That(coast.Graph.Nodes[coast.PartyAt].Id, Is.EqualTo("gate-marina"));
        }

        // 冒険の世界地図でも、エリアマップの入口に止まるか、立っている入口を押すと、Homeへは出ずに
        // 世界地図のその地域のマスへ戻る。
        [UnityTest]
        public IEnumerator AGateClimbsToTheWorldEvenWhenTheHomeLeaves()
        {
            var navigator = Spawn();
            int left = 0;
            navigator.Leave = () => left++;
            yield return SceneTests.WaitUntil(() => WorldAtlasSession.Loaded);
            Assert.That(navigator.Open("marina", null), Is.True);
            yield return null;
            var coast = navigator.Current;
            int gate = coast.Graph.IndexOf("gate-marina");
            // 入口の隣と世界地図の地域のマスは、開けたことにしておく（開けていないマスに立つと、始まりへ戻される）。
            Assert.That(
                WorldAtlasSession.Open(WorldAtlasLayout.WorldKey, "region-marina").Paid,
                Is.True
            );
            string beside = coast.Graph.Nodes[coast.Graph.LinksOf(gate)[0]].Id;
            Assert.That(WorldAtlasSession.Open("marina", beside).Paid, Is.True);
            navigator.Open("marina", beside);
            yield return null;
            coast = navigator.Current;

            Assert.That(coast.Tap(gate), Is.True);
            yield return WaitForSwitch(navigator, WorldAtlasLayout.WorldKey);
            Assert.That(left, Is.Zero);
            var world = navigator.Current;
            Assert.That(world.Graph.Nodes[world.PartyAt].Id, Is.EqualTo("region-marina"));

            world.Tap(world.PartyAt);
            yield return WaitForSwitch(navigator, "marina");
            coast = navigator.Current;
            coast.Tap(coast.PartyAt);
            yield return WaitForSwitch(navigator, WorldAtlasLayout.WorldKey);
            Assert.That(left, Is.Zero);
        }

        // 端末の戻るキーは、通行料の確認が開いていればそれを閉じ、なければ左上の家と同じくHomeへ出る
        // （doc/rules/ui-design.md#操作の決まり）。
        [UnityTest]
        public IEnumerator TheBackKeyClosesTheTollFirstThenLeavesLikeTheHome()
        {
            WorldAtlasSession.Source = new WorldAtlasMemorySource(600, (map, square) => 0);
            var navigator = Spawn();
            int left = 0;
            navigator.Leave = () => left++;
            yield return SceneTests.WaitUntil(
                () => WorldAtlasSession.Loaded && navigator.Current != null,
                message: "The runes were not read."
            );
            var world = navigator.Current;
            var toll = world.GetComponent<WorldAtlasToll>();
            var path = QuietPath(world, 3);
            Assert.That(path, Is.Not.Null);
            toll.Ask(path, 9999);
            Assert.That(toll.Asking, Is.True);

            navigator.PressBack();
            Assert.That(toll.Asking, Is.False);
            Assert.That(left, Is.Zero);

            navigator.PressBack();
            Assert.That(left, Is.EqualTo(1));
        }

        // 端末に覚えた位置が、開けていないマス（消費を止めて歩いた、DBを作り直した）なら、開けたマスを読んだあと
        // 地図の始まりへ戻す。そのままだと、隣がつながっていないとしてサーバーに支払いを断られ、先へ進めない。
        [UnityTest]
        public IEnumerator APartyRememberedOnAnUnpaidSquareGoesBackToTheStart()
        {
            const string unpaid = "world-37";
            WorldAtlasNavigator.Remember(WorldAtlasLayout.WorldKey, unpaid);
            var navigator = Spawn();
            yield return null;
            var world = navigator.Current;
            yield return SceneTests.WaitUntil(() => WorldAtlasSession.Loaded);
            yield return null;
            world = navigator.Current;
            Assert.That(
                world.Graph.Nodes[world.PartyAt].Id,
                Is.EqualTo(WorldAtlasLayout.StartSquare)
            );
            Assert.That(world.Cleared, Does.Not.Contain(unpaid));
            Assert.That(world.PriceOf(world.Graph.IndexOf(unpaid)), Is.GreaterThan(0));
            Assert.That(
                WorldAtlasNavigator.Remembered(),
                Is.EqualTo((WorldAtlasLayout.WorldKey, WorldAtlasLayout.StartSquare))
            );
        }

        private WorldAtlasNavigator Spawn()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldAtlasNavigator>(
                NavigatorPath
            );
#else
            WorldAtlasNavigator prefab = null;
#endif
            Assert.That(prefab, Is.Not.Null, NavigatorPath);
            events = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            var navigator = Object.Instantiate(prefab);
            atlas = navigator.gameObject;
            return navigator;
        }

        private static IEnumerator WaitForSwitch(WorldAtlasNavigator navigator, string key) =>
            SceneTests.WaitUntil(
                () => navigator.CurrentKey == key && !navigator.Switching,
                seconds: 5f,
                message: "The map did not switch to " + key + "."
            );

        private static IEnumerator WaitOpened(WorldMapEventWindow window) =>
            SceneTests.WaitUntil(
                () => !window.Opening,
                message: "The event window did not finish opening."
            );

        private static IEnumerator CloseWindow(WorldMapEventWindow window)
        {
            Assert.That(window.CanClose, Is.True);
            yield return SceneTests.WaitUntil(() => window.CloseGroup.interactable);
            SceneTests.AssertTouchSize(window.Close.transform);
            window.Close.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => !window.IsShown,
                message: "The event window did not close."
            );
        }

        // 今いるマスから、出来事を置いたいちばん近いマスまでの道（今いるマスは含まない）。
        private static List<int> PathToNearestEvent(WorldMapView map)
        {
            var from = new Dictionary<int, int> { [map.PartyAt] = -1 };
            var queue = new Queue<int>();
            queue.Enqueue(map.PartyAt);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                if (map.TryEventAt(at, out _))
                {
                    var path = new List<int>();
                    for (int i = at; i != map.PartyAt; i = from[i])
                        path.Insert(0, i);
                    return path;
                }
                foreach (int next in map.Graph.LinksOf(at))
                    if (from.TryAdd(next, at))
                        queue.Enqueue(next);
            }
            return null;
        }

        [UnityTest]
        public IEnumerator StoppingOnARegionDivesInAndAGateClimbsBack()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldAtlasNavigator>(
                NavigatorPath
            );
#else
            WorldAtlasNavigator prefab = null;
#endif
            Assert.That(prefab, Is.Not.Null, NavigatorPath);
            events = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            var navigator = Object.Instantiate(prefab);
            atlas = navigator.gameObject;
            yield return null;
            Assert.That(navigator.CurrentKey, Is.EqualTo(WorldAtlasLayout.WorldKey));

            // 世界地図は、はじめのマスから隣のマスへ1つずつ進む。遠い地域へはまだ行けない。
            var world = navigator.Current;
            Assert.That(
                world.Graph.Nodes[world.PartyAt].Id,
                Is.EqualTo(WorldAtlasLayout.StartSquare)
            );
            // デバッグルームの試作では、世界地図から出る先もダンジョン一覧もない。
            Assert.That(world.Back.gameObject.activeSelf, Is.False);
            Assert.That(world.Dungeons.gameObject.activeSelf, Is.False);
            Assert.That(world.WorldMap, Is.Null);
            // 遠い地域のマスも押せる。払うルーンを確かめる窓が出て、やめると歩かない。
            yield return SceneTests.WaitUntil(() => WorldAtlasSession.Loaded);
            var toll = world.GetComponent<WorldAtlasToll>();
            Assert.That(world.Tap(world.Graph.IndexOf("region-oro")), Is.True);
            Assert.That(toll.Asking, Is.True);
            toll.Cancel.onClick.Invoke();
            Assert.That(toll.Asking, Is.False);
            Assert.That(world.Walking, Is.False);
            // 始まりから、出来事を置いたいちばん近いマスへ向かう。始まりの隣には出来事を置かない。
            var path = PathToNearestEvent(world);
            Assert.That(path, Has.Count.GreaterThan(1));
            int next = path[0];
            // 小さなマスは、台より広い範囲で押せる。
            var hit = (RectTransform)world.Nodes[next].Find("HitArea");
            Assert.That(hit, Is.Not.Null);
            Assert.That(hit.rect.width, Is.EqualTo(80f).Within(0.01f));
            Assert.That(hit.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
            Assert.That(world.Tap(next), Is.True);
            yield return SceneTests.WaitUntil(
                () => !world.Walking,
                seconds: 5f,
                message: "The party did not reach the next square."
            );
            Assert.That(world.States[next], Is.EqualTo(WorldNodeState.Cleared));
            Assert.That(navigator.CurrentKey, Is.EqualTo(WorldAtlasLayout.WorldKey));

            // 出来事を置いていないマスでは、何も開かない。
            var window = world.EventWindow;
            Assert.That(world.TryEventAt(next, out _), Is.False);
            Assert.That(window.IsShown, Is.False);

            // 出来事を置いたマスは、その出来事の台で見せる。初めて止まると、マスからその出来事の窓が開き、
            // 閉じるまでは歩かない。始まりからいちばん近い出来事は拾い物で、閉じるとマスは何もない台に戻る。
            int found = path[^1];
            Assert.That(world.TryEventAt(found, out var placed), Is.True);
            Assert.That(placed, Is.EqualTo(WorldEventKind.Pickup));
            Assert.That(
                world.NodeImages[found].sprite,
                Is.SameAs(world.EventSprites[(int)WorldEventKind.Pickup])
            );
            foreach (int step in path.GetRange(1, path.Count - 1))
            {
                Assert.That(world.Tap(step), Is.True);
                yield return SceneTests.WaitUntil(() => !world.Walking, seconds: 5f);
            }
            Assert.That(window.IsShown, Is.True);
            Assert.That(window.Panel.Kind, Is.EqualTo(WorldEventKind.Pickup));
            Assert.That(world.Tap(world.Graph.IndexOf(WorldAtlasLayout.StartSquare)), Is.False);
            yield return SceneTests.WaitUntil(
                () => !window.IsShown,
                seconds: 5f,
                message: "The pickup did not close by itself."
            );
            Assert.That(world.NodeImages[found].sprite, Is.SameAs(world.KindSprites[0]));

            // ほかの出来事は、隣のマスから窓を開いて確かめる。
            var square = world.Nodes[next];
            window.Open(square, WorldEventKind.Meet, null);
            yield return WaitOpened(window);
            Assert.That(window.Window.localScale.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                (Vector2)window.Window.localPosition,
                Is.EqualTo(Vector2.zero).Using(Vector2EqualityComparer.Instance)
            );

            // 会話の間は×が出ず、押しても閉じない。窓を押すと、文字を出しきってから次のせりふへ進み、
            // 読み終えると仲間になる。
            var meet = (WorldEventMeet)window.Panel;
            Assert.That(window.CloseGroup.interactable, Is.False);
            window.Close.onClick.Invoke();
            Assert.That(window.IsShown, Is.True);
            var first = WorldEventSamples.MeetTalk[0];
            Assert.That(meet.Talk.Says[first.Speaker].text, Is.EqualTo(first.Text));
            for (int i = 0; i < WorldEventSamples.MeetTalk.Length * 2 && !meet.Joining; i++)
            {
                window.Next.onClick.Invoke();
                yield return null;
            }
            Assert.That(meet.Joining, Is.True);
            yield return SceneTests.WaitUntil(
                () => meet.Done,
                message: "The newcomer did not join."
            );
            yield return CloseWindow(window);
            Assert.That(world.Nodes[next].localScale.x, Is.EqualTo(1f).Within(0.001f));

            // 選択：答えは書き終えてから出る。選ぶと、その答えのもらう物が出て×が出る。
            window.Open(square, WorldEventKind.Choice, null);
            yield return WaitOpened(window);
            var choice = (WorldEventChoice)window.Panel;
            Assert.That(choice.Answers.interactable, Is.False);
            window.Next.onClick.Invoke();
            yield return SceneTests.WaitUntil(() => choice.Answers.interactable);
            choice.Options[2].onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => choice.Done,
                message: "The choice gave nothing."
            );
            Assert.That(choice.Gifts[2].gameObject.activeSelf, Is.True);
            yield return CloseWindow(window);

            // 隠し宝箱：押すと開き、中身が出るまで×は出ない。
            window.Open(square, WorldEventKind.HiddenChest, null);
            yield return WaitOpened(window);
            var chest = (WorldEventChest)window.Panel;
            Assert.That(window.CanClose, Is.False);
            window.Next.onClick.Invoke();
            yield return SceneTests.WaitUntil(() => chest.Done, message: "The chest did not open.");
            Assert.That(chest.Chest.texture, Is.SameAs(chest.OpenArt));
            yield return CloseWindow(window);

            // 拾い物：小さな窓で、×を出さずに自分で閉じる。
            window.Open(square, WorldEventKind.Pickup, null);
            Assert.That(window.Window.sizeDelta.x, Is.LessThan(1000f));
            yield return WaitOpened(window);
            Assert.That(window.CloseGroup.interactable, Is.False);
            yield return SceneTests.WaitUntil(
                () => !window.IsShown,
                seconds: 5f,
                message: "The pickup did not close by itself."
            );

            // 行商人：持っているルーンで買える品は売り切れになり、ルーンが減る。足りない品は買えない。
            window.Open(square, WorldEventKind.Peddler, null);
            yield return WaitOpened(window);
            var peddler = (WorldEventPeddler)window.Panel;
            Assert.That(window.CloseGroup.interactable, Is.True);
            peddler.Goods[3].onClick.Invoke();
            peddler.Goods[2].onClick.Invoke();
            peddler.Goods[0].onClick.Invoke();
            Assert.That(peddler.Sold(3) && peddler.Sold(2), Is.True);
            Assert.That(peddler.Sold(0), Is.False);
            Assert.That(
                peddler.Balance,
                Is.EqualTo(
                    WorldEventSamples.PeddlerRunes
                        - WorldEventSamples.PeddlerPrices[3]
                        - WorldEventSamples.PeddlerPrices[2]
                )
            );
            yield return CloseWindow(window);

            // 賭け（サイコロ）：遊ぶと賭け金が積まれ、次の目の高い・低いを当てると倍になる。
            // 当てたあとは降りられ、積んだ分をもらう。得意な仲間がいると、振り直せる。
            window.Open(square, WorldEventKind.Bet, null, null, (int)WorldBetGame.Dice);
            yield return WaitOpened(window);
            var dice = (WorldEventDice)window.Panel;
            dice.Rig(3, 5);
            dice.Play.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => dice.Guessing,
                message: "The die did not land."
            );
            Assert.That(dice.Face, Is.EqualTo(3));
            Assert.That(
                dice.StopGroup.interactable,
                Is.False,
                "Stopping needs a right guess first."
            );
            Assert.That(dice.RerollGroup.interactable, Is.True);
            dice.High.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => dice.Guessing,
                message: "The guess was not rolled."
            );
            Assert.That(dice.PotRunes, Is.EqualTo(WorldEventSamples.BetStake * 2));
            dice.Stop.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => dice.Done,
                message: "The dice bet did not end."
            );
            Assert.That(
                dice.Balance,
                Is.EqualTo(WorldEventSamples.PeddlerRunes + WorldEventSamples.BetStake)
            );
            yield return CloseWindow(window);

            // 賭け（宝箱）：開けるたびにルーンが積まれ、ミミックを開けると積んだ分を失う。
            window.Open(square, WorldEventKind.Bet, null, null, (int)WorldBetGame.Chests);
            yield return WaitOpened(window);
            var chests = (WorldEventChestRun)window.Panel;
            chests.Rig(4);
            chests.Play.onClick.Invoke();
            Assert.That(chests.Picking, Is.True);
            chests.Taps[0].onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => chests.Picking,
                message: "The chest did not open."
            );
            Assert.That(chests.PotRunes, Is.EqualTo(WorldEventSamples.ChestRunGains[0]));
            Assert.That(chests.StopGroup.interactable, Is.True);
            chests.Taps[4].onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => chests.Done,
                message: "The mimic did not end the bet."
            );
            Assert.That(chests.Lost, Is.True);
            Assert.That(chests.PotRunes, Is.Zero);
            Assert.That(
                chests.Balance,
                Is.EqualTo(WorldEventSamples.PeddlerRunes - WorldEventSamples.BetStake)
            );
            yield return CloseWindow(window);

            // 得意な仲間がいないと、助けのボタンも、選択の印の付いた答えも出ない。
            window.Helped = false;
            window.Open(square, WorldEventKind.Bet, null, null, (int)WorldBetGame.Chests);
            yield return WaitOpened(window);
            chests.Play.onClick.Invoke();
            Assert.That(chests.PeekGroup.interactable, Is.False);
            yield return CloseWindow(window);
            window.Open(square, WorldEventKind.Choice, null);
            yield return WaitOpened(window);
            var helpless = (WorldEventChoice)window.Panel;
            Assert.That(helpless.Options[^1].gameObject.activeSelf, Is.False);
            Assert.That(helpless.Options[0].gameObject.activeSelf, Is.True);
            window.Next.onClick.Invoke();
            yield return SceneTests.WaitUntil(() => helpless.Answers.interactable);
            helpless.Options[0].onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => helpless.Done,
                message: "The choice did not end."
            );
            yield return CloseWindow(window);
            window.Helped = true;

            // 戦闘：「たたかう」で白く覆い、戦闘の画面へ渡す。
            bool fought = false;
            window.Open(square, WorldEventKind.Battle, null, () => fought = true);
            yield return WaitOpened(window);
            yield return SceneTests.WaitUntil(() =>
                ((WorldEventBattle)window.Panel).FightGroup.interactable
            );
            ((WorldEventBattle)window.Panel).Fight.onClick.Invoke();
            Assert.That(window.Departing, Is.True);
            yield return SceneTests.WaitUntil(() => fought, message: "The battle was not started.");
            Assert.That(window.Flash.color.a, Is.EqualTo(1f).Within(0.001f));
            // 戦闘の画面がないこの試験では、窓を片付けておく。
            window.Open(square, WorldEventKind.Pickup, null);
            yield return SceneTests.WaitUntil(() => !window.IsShown, seconds: 5f);

            // 一度開けたマスへ戻っても、窓は開かない。
            Assert.That(world.Tap(world.Graph.IndexOf(WorldAtlasLayout.StartSquare)), Is.True);
            yield return SceneTests.WaitUntil(() => !world.Walking, seconds: 5f);
            Assert.That(window.IsShown, Is.False);

            // 地域のマスの隣から地域のマスへ進んで止まると、その地域のエリアマップの入口に入る。
            int marina = world.Graph.IndexOf("region-marina");
            int before = world.Graph.LinksOf(marina)[0];
            // 隣のマスは、開けたことにしてから立たせる（開けていないマスに立たせると、始まりへ戻される）。
            string beside = world.Graph.Nodes[before].Id;
            Assert.That(WorldAtlasSession.Open(WorldAtlasLayout.WorldKey, beside).Paid, Is.True);
            navigator.Open(WorldAtlasLayout.WorldKey, beside);
            yield return null;
            world = navigator.Current;
            Assert.That(world.Tap(marina), Is.True);
            yield return WaitForSwitch(navigator, "marina");
            var coast = navigator.Current;
            Assert.That(coast.Graph.Nodes[coast.PartyAt].Id, Is.EqualTo("gate-marina"));
            Assert.That(coast.Back.gameObject.activeSelf, Is.True);

            // 入口をもう一度押すと、世界地図のその地域のマスへ戻る。開けたマスは残る。
            coast.Tap(coast.PartyAt);
            yield return WaitForSwitch(navigator, WorldAtlasLayout.WorldKey);
            var back = navigator.Current;
            Assert.That(back.Graph.Nodes[back.PartyAt].Id, Is.EqualTo("region-marina"));
            // 戻った世界地図は、駒が地図の端に近くても、地図の外（黒）を写さない。
            Assert.That(back.Scroll.x, Is.InRange(0f, back.MaxScroll.x));
            Assert.That(back.Scroll.y, Is.InRange(0f, back.MaxScroll.y));
            Assert.That(back.States[next], Is.EqualTo(WorldNodeState.Cleared));

            // 地域から入り直し、もどるでも世界地図へ戻る。
            back.Tap(back.PartyAt);
            yield return WaitForSwitch(navigator, "marina");
            navigator.Current.Back.onClick.Invoke();
            yield return WaitForSwitch(navigator, WorldAtlasLayout.WorldKey);
        }

        // 始まりから、何もないマスか拾い物（自分で閉じる）だけを通って steps マス先にあるマスまでの道
        // （始まりを含む）。
        private static List<int> QuietPath(WorldMapView map, int steps)
        {
            var from = new Dictionary<int, int> { [map.PartyAt] = -1 };
            var depth = new Dictionary<int, int> { [map.PartyAt] = 0 };
            var queue = new Queue<int>();
            queue.Enqueue(map.PartyAt);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                if (depth[at] == steps)
                {
                    var path = new List<int>();
                    for (int i = at; i != -1; i = from[i])
                        path.Insert(0, i);
                    return path;
                }
                foreach (int next in map.Graph.LinksOf(at))
                    if (
                        (!map.TryEventAt(next, out var kind) || kind == WorldEventKind.Pickup)
                        && from.TryAdd(next, at)
                    )
                    {
                        depth[next] = depth[at] + 1;
                        queue.Enqueue(next);
                    }
            }
            return null;
        }

        // 開けていない先のマスを押すと、払うルーンを確かめてから、跳ぶ直前に1マスずつ払って歩く。
        // 止めると、その場で止まり、先のマスは払わない。足りない道は進めず、足りない歩数を出す。
        // 「次回起動まで確認しない」を選ぶと、次からは確かめずに歩く。
        [UnityTest]
        public IEnumerator FarSquaresAskThenPayOneByOneAndStopLeavesTheRestUnpaid()
        {
            WorldAtlasNavigator navigator = null;
            // サーバーの値段は、歩き出す前の画面の値段を控えて使う。画面の値段は、払ったとたんに0になる。
            var prices = new System.Collections.Generic.Dictionary<string, int>();
            WorldAtlasSession.Source = new WorldAtlasMemorySource(
                600,
                (map, square) => prices.TryGetValue(square, out int price) ? price : 0
            );
            navigator = Spawn();
            yield return SceneTests.WaitUntil(
                () => WorldAtlasSession.Loaded && navigator.Current != null,
                message: "The runes were not read."
            );
            var world = navigator.Current;
            for (int i = 0; i < world.Graph.Nodes.Count; i++)
                prices[world.Graph.Nodes[i].Id] = world.PriceOf(i);
            var toll = world.GetComponent<WorldAtlasToll>();
            Assert.That(toll, Is.Not.Null);
            var path = QuietPath(world, 3);
            Assert.That(path, Is.Not.Null);
            // 始まりの隣には出来事を置かないため、最初のマスでは止まらずに止められる。
            Assert.That(world.TryEventAt(path[1], out _), Is.False);
            int each = world.PriceOf(path[1]);
            Assert.That(each, Is.EqualTo(world.BaseCost));
            Assert.That(world.PriceOf(path[2]) + world.PriceOf(path[3]), Is.EqualTo(each * 2));

            Assert.That(world.Tap(path[3]), Is.True);
            Assert.That(toll.Asking, Is.True);
            Assert.That(world.Walking, Is.False);
            Assert.That(toll.Message.text, Does.StartWith($"合計{each * 3:N0}ルーンかかります。"));
            Assert.That(toll.Go.interactable, Is.True);
            toll.Go.onClick.Invoke();
            Assert.That(toll.Asking, Is.False);
            yield return SceneTests.WaitUntil(
                () => world.PartyAt == path[1],
                seconds: 5f,
                message: "The party did not reach the first square."
            );
            Assert.That(WorldAtlasSession.Runes, Is.EqualTo(600 - each));
            Assert.That(toll.StopRoot.activeSelf || world.CanStop, Is.True);
            SceneTests.AssertTouchSize(toll.Stop.transform);
            toll.Stop.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(world.Walking, Is.False);
            Assert.That(world.PartyAt, Is.EqualTo(path[1]));
            Assert.That(WorldAtlasSession.Runes, Is.EqualTo(600 - each));
            Assert.That(
                WorldAtlasSession.IsOpened(
                    WorldAtlasLayout.WorldKey,
                    world.Graph.Nodes[path[2]].Id
                ),
                Is.False
            );

            // 足りない道は進めない。
            toll.Ask(path.GetRange(1, 3), 9999);
            Assert.That(toll.Go.interactable, Is.False);
            Assert.That(
                toll.Message.text,
                Does.Contain($"ルーンが {9999 - (600 - each):N0} 足りません")
            );
            toll.Hide();

            // 確認を省くと、次の道からは窓を出さない。途中の拾い物では止まり、閉じると続きを歩く。
            Assert.That(world.Tap(path[3]), Is.True);
            Assert.That(toll.Asking, Is.True);
            toll.Skip.onClick.Invoke();
            toll.Go.onClick.Invoke();
            Assert.That(WorldAtlasSession.SkipConfirm, Is.True);
            yield return SceneTests.WaitUntil(
                () => !world.Walking && world.PartyAt == path[3],
                seconds: 10f,
                message: "The party did not reach the far square."
            );
            Assert.That(world.PartyAt, Is.EqualTo(path[3]));
            Assert.That(WorldAtlasSession.Runes, Is.EqualTo(600 - each * 3));

            // サーバーが受けなかった支払いは取り消され、駒はまだ開けているマスまで戻る。
            string last = world.Graph.Nodes[path[3]].Id;
            world.Unpaid(last, WorldAtlasSession.FailedMessage);
            Assert.That(world.PartyAt, Is.EqualTo(path[2]));
            Assert.That(world.Cleared, Does.Not.Contain(last));
        }

        // ダンジョンのある町のマスに初めて着くと、そのダンジョンに入れるようになったと知らせ、
        // 旅の案内所へのボタンを出す。着く前は解放していない。
        [UnityTest]
        public IEnumerator ReachingADungeonTownTellsTheDungeonOpens()
        {
            WorldAtlasSession.Source = new WorldAtlasMemorySource(1000000, (map, square) => 0);
            var navigator = Spawn();
            int dungeons = 0;
            navigator.OpenDungeons = () => dungeons++;
            yield return SceneTests.WaitUntil(
                () => WorldAtlasSession.Loaded && navigator.Current != null,
                message: "The runes were not read."
            );
            var coast = OpenBeside(navigator, "marina", "portoria");
            Assert.That(DungeonUnlocks.Reached("portoria", WorldMapProgress.Start), Is.False);
            yield return WalkTo(coast, "portoria");
            // 着くと先に町から花火が上がり、開ききってから窓が出る。
            yield return SceneTests.WaitUntil(
                () => navigator.Fireworks != null && navigator.Fireworks.Playing,
                seconds: 8f,
                message: "No fireworks went up."
            );
            Assert.That(coast.Dialog.IsShown, Is.False);
            yield return SceneTests.WaitUntil(
                () => coast.Dialog.IsShown,
                seconds: 8f,
                message: "The dungeon was not announced."
            );
            Assert.That(coast.Dialog.TitleText, Is.EqualTo(WorldAtlasNavigator.DungeonOpenedTitle));
            Assert.That(
                coast.Dialog.BodyText,
                Is.EqualTo(
                    WorldAtlasNavigator.DungeonOpenedText(DungeonUnlocks.DungeonAt("portoria").Name)
                )
            );
            Assert.That(
                coast.Dialog.ChoiceLabels,
                Is.EqualTo(new[] { WorldAtlasNavigator.DungeonGoChoice })
            );
            Assert.That(coast.Dialog.CrossShown, Is.True);
            Assert.That(DungeonUnlocks.Reached("portoria", WorldMapProgress.Start), Is.True);
            Assert.That(dungeons, Is.Zero);
        }

        // ルーンの消費を止めて歩いても、町のマスに着けばダンジョンが解放される（2026-11-05）。
        [UnityTest]
        public IEnumerator ReachingADungeonTownWhileChargingIsOffStillOpensTheDungeon()
        {
            var source = new WorldAtlasMemorySource(0, (map, square) => 100);
            WorldAtlasSession.Source = source;
            WorldAtlasSession.Charging = false;
            var navigator = Spawn();
            yield return SceneTests.WaitUntil(
                () => WorldAtlasSession.Loaded && navigator.Current != null,
                message: "The squares were not read."
            );
            var coast = OpenBeside(navigator, "marina", "portoria");
            Assert.That(DungeonUnlocks.Reached("portoria", WorldMapProgress.Start), Is.False);
            yield return WalkTo(coast, "portoria");
            yield return SceneTests.WaitUntil(
                () => coast.Dialog.IsShown,
                seconds: 16f,
                message: "The dungeon was not announced."
            );
            Assert.That(DungeonUnlocks.Reached("portoria", WorldMapProgress.Start), Is.True);
            Assert.That(source.Runes, Is.Zero, "Nothing is paid.");
        }

        // 世界地図の宝箱のマスを開けて着くと、見本の窓の代わりに、もらった物を札で見せる。
        [UnityTest]
        public IEnumerator ATreasureSquareOfTheWorldShowsWhatItGives()
        {
            WorldAtlasSession.Source = new WorldAtlasMemorySource(1000000, (map, square) => 0);
            var navigator = Spawn();
            yield return SceneTests.WaitUntil(
                () => WorldAtlasSession.Loaded && navigator.Current != null,
                message: "The runes were not read."
            );
            Assert.That(WorldMapChests.Of("world-21"), Is.Not.Empty);
            var world = OpenBeside(navigator, WorldAtlasLayout.WorldKey, "world-21");
            yield return WalkTo(world, "world-21");
            yield return SceneTests.WaitUntil(
                () => world.Reveal.Shown,
                seconds: 8f,
                message: "The treasure was not shown."
            );
            Assert.That(world.Reveal.ShownCount, Is.GreaterThan(0));
            Assert.That(world.EventWindow == null || !world.EventWindow.IsShown, Is.True);
        }

        // 地図を開き、target のほかのマスをすべて開けて、target の隣に立つ。
        private static WorldMapView OpenBeside(
            WorldAtlasNavigator navigator,
            string map,
            string target
        )
        {
            Assert.That(navigator.Open(map, null), Is.True);
            var view = navigator.Current;
            int index = view.Graph.IndexOf(target);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), target);
            foreach (var node in view.Graph.Nodes)
                if (node.Id != target)
                    WorldAtlasSession.Open(map, node.Id);
            Assert.That(
                navigator.Open(map, view.Graph.Nodes[view.Graph.LinksOf(index)[0]].Id),
                Is.True
            );
            return navigator.Current;
        }

        // target を押し、確かめる窓が出れば払って歩く。
        private static IEnumerator WalkTo(WorldMapView view, string target)
        {
            yield return null;
            Assert.That(view.Tap(view.Graph.IndexOf(target)), Is.True);
            var toll = view.GetComponent<WorldAtlasToll>();
            if (toll != null && toll.Asking)
                toll.Go.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => view.Graph.Nodes[view.PartyAt].Id == target && !view.Walking,
                seconds: 8f,
                message: "The party did not reach " + target + "."
            );
        }
    }
}
