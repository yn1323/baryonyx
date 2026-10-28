using System.Collections;
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

        [UnityTearDown]
        public IEnumerator Clean()
        {
            if (atlas != null)
                Object.Destroy(atlas);
            if (events != null)
                Object.Destroy(events);
            yield return null;
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
            Assert.That(world.Tap(world.Graph.IndexOf("region-oro")), Is.False);
            int next = world.Graph.LinksOf(world.PartyAt)[0];
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

            // 初めて止まった何もないマスでは、マスから出来事の見本の窓が開く。1つ目は出会い。閉じるまでは歩かない。
            Assert.That(world.Graph.Nodes[next].Kind, Is.EqualTo(WorldNodeKind.Empty));
            var window = world.EventWindow;
            Assert.That(window.IsShown, Is.True);
            Assert.That(window.Panel.Kind, Is.EqualTo(WorldAtlasNavigator.TrialOrder[0]));
            Assert.That(world.Tap(world.Graph.IndexOf(WorldAtlasLayout.StartSquare)), Is.False);
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
            var square = world.Nodes[next];
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
    }
}
