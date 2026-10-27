using System.Collections;
using Baryonyx.WorldMap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            Assert.That(world.Back.gameObject.activeSelf, Is.False);
            Assert.That(world.Tap(world.Graph.IndexOf("region-oro")), Is.False);
            int next = world.Graph.LinksOf(world.PartyAt)[0];
            Assert.That(world.Tap(next), Is.True);
            yield return SceneTests.WaitUntil(
                () => !world.Walking,
                seconds: 5f,
                message: "The party did not reach the next square."
            );
            Assert.That(world.States[next], Is.EqualTo(WorldNodeState.Cleared));
            Assert.That(navigator.CurrentKey, Is.EqualTo(WorldAtlasLayout.WorldKey));

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
