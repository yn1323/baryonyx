using System.Collections;
using Baryonyx.App;
using Baryonyx.WorldMap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 街道の世界地図の2本指のピンチとホイールでの拡大・縮小を、実際のシーンで通す。
    public sealed class WorldMapZoomTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            WorldMapSession.Reset();
            WorldMapSession.Source = new WorldMapMemorySource(WorldMapProgress.Start);
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            WorldMapSession.Reset();
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(WorldMapZoomTests));
        }

        // 指を広げると近づき、2本の中点の下にあった地図の場所は中点の下に残る。
        // 狭めると、地図が画面を埋めるところで止まる。
        [UnityTest]
        public IEnumerator PinchingZoomsAroundTheFingers()
        {
            var map = default(WorldMapBootstrap);
            yield return SceneTests.LoadWorldMap(value => map = value);
            var view = map.View;
            var drag = view.Drag;
            Assert.That(view.Zoom, Is.EqualTo(1f));

            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var left = center - new Vector2(100f, 0f);
            var right = center + new Vector2(100f, 0f);
            var under = MapPoint(view, center);
            drag.OnBeginDrag(Finger(0, left));
            drag.OnBeginDrag(Finger(1, right));
            drag.OnDrag(Finger(1, right + new Vector2(100f, 0f), new Vector2(100f, 0f)));

            Assert.That(view.Zoom, Is.EqualTo(1.5f).Within(0.001f));
            var after = center + new Vector2(50f, 0f);
            Assert.That(Vector2.Distance(MapPoint(view, after), under), Is.LessThan(2f));

            // 1本を離すと、残った指で地図を動かせる。
            drag.OnEndDrag(Finger(1, right));
            var scroll = view.Scroll;
            var moved = left + new Vector2(-40f, -40f);
            drag.OnDrag(Finger(0, moved, new Vector2(-40f, -40f)));
            Assert.That(view.Scroll, Is.Not.EqualTo(scroll));
            Assert.That(view.Zoom, Is.EqualTo(1.5f).Within(0.001f));

            // 大きく狭めても、画面に地図の外は入らない。
            drag.OnBeginDrag(Finger(1, moved + new Vector2(300f, 0f)));
            drag.OnDrag(Finger(1, moved + new Vector2(2f, 0f), new Vector2(-298f, 0f)));
            Assert.That(view.Zoom, Is.LessThan(1f));
            AssertMapCoversScreen(view);
            drag.OnEndDrag(Finger(1, moved));
            drag.OnEndDrag(Finger(0, moved));
        }

        // ホイールを奥へ回すと近づき、手前へ回すと離れる。近づく上限で止まる。
        [UnityTest]
        public IEnumerator TheWheelZooms()
        {
            var map = default(WorldMapBootstrap);
            yield return SceneTests.LoadWorldMap(value => map = value);
            var view = map.View;
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);

            view.Drag.OnScroll(Wheel(center, 1f));
            Assert.That(view.Zoom, Is.GreaterThan(1f));
            for (int i = 0; i < 20; i++)
                view.Drag.OnScroll(Wheel(center, 1f));
            Assert.That(view.Zoom, Is.EqualTo(WorldMapView.MaxZoom));

            for (int i = 0; i < 40; i++)
                view.Drag.OnScroll(Wheel(center, -1f));
            Assert.That(view.Zoom, Is.LessThan(1f));
            AssertMapCoversScreen(view);
        }

        private static PointerEventData Finger(int id, Vector2 position, Vector2 delta = default) =>
            new(EventSystem.current)
            {
                pointerId = id,
                position = position,
                delta = delta,
            };

        private static PointerEventData Wheel(Vector2 position, float y) =>
            new(EventSystem.current) { position = position, scrollDelta = new Vector2(0f, y) };

        private static Vector2 MapPoint(WorldMapView view, Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                view.Map,
                screen,
                null,
                out var local
            );
            return local;
        }

        private static void AssertMapCoversScreen(WorldMapView view)
        {
            var corners = new Vector3[4];
            view.Map.GetWorldCorners(corners);
            var screen = new Vector3[4];
            view.Viewport.GetWorldCorners(screen);
            Assert.That(corners[0].x, Is.LessThanOrEqualTo(screen[0].x + 1f));
            Assert.That(corners[0].y, Is.LessThanOrEqualTo(screen[0].y + 1f));
            Assert.That(corners[2].x, Is.GreaterThanOrEqualTo(screen[2].x - 1f));
            Assert.That(corners[2].y, Is.GreaterThanOrEqualTo(screen[2].y - 1f));
        }
    }
}
