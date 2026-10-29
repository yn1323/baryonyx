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
            yield return null;
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

            // 賭け：遊ぶと伏せたカードが混ざり、当たりを引くとルーンが増える。
            window.Open(square, WorldEventKind.Bet, null);
            yield return WaitOpened(window);
            var bet = (WorldEventBet)window.Panel;
            bet.Play.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => bet.Picking,
                seconds: 6f,
                message: "The cards were not shuffled."
            );
            bet.Picks[bet.Winner].onClick.Invoke();
            yield return SceneTests.WaitUntil(() => bet.Done, message: "The bet did not end.");
            Assert.That(
                bet.Balance,
                Is.EqualTo(
                    WorldEventSamples.PeddlerRunes
                        - WorldEventSamples.BetStake
                        + WorldEventSamples.BetPrize
                )
            );
            yield return CloseWindow(window);

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
            navigator.Open(WorldAtlasLayout.WorldKey, world.Graph.Nodes[before].Id);
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
                WorldAtlasSession.IsOpened(WorldAtlasLayout.WorldKey, world.Graph.Nodes[path[2]].Id),
                Is.False
            );

            // 足りない道は進めない。
            toll.Ask(path.GetRange(1, 3), 9999);
            Assert.That(toll.Go.interactable, Is.False);
            Assert.That(toll.Message.text, Does.Contain($"あと{9999 - (600 - each):N0}歩"));
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
    }
}
