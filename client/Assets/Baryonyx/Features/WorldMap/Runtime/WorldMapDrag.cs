using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// The world map under the finger: dragging it looks around in any direction, and pinching it
    /// with two fingers (or the mouse wheel in the Editor) zooms it (<see cref="WorldMapView"/>).
    /// A drag that starts on a square moves the map too and does not press it; a tap still does.
    /// </summary>
    public sealed class WorldMapDrag
        : MonoBehaviour,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler,
            IScrollHandler
    {
        // ホイール1目盛りで変える大きさの比。
        private const float WheelStep = 1.15f;

        // 指の動き（設計座標、y が上）。
        public event Action<Vector2> Dragged;

        // 2本指の動き。動く前と後の2本の中点（設計座標、y が上）と、2本の間の長さの比。
        public event Action<Vector2, Vector2, float> Pinched;
        public event Action<bool> Holding;

        // 地図に触れている指ごとの、最後に見た位置。3本目からは数えない。
        private readonly Dictionary<int, Vector2> fingers = new();

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (
                fingers.Count >= 2
                || !Local(eventData.position - eventData.delta, eventData, out var at)
            )
                return;
            fingers[eventData.pointerId] = at;
            if (fingers.Count == 1)
                Holding?.Invoke(true);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (fingers.Remove(eventData.pointerId) && fingers.Count == 0)
                Holding?.Invoke(false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            int id = eventData.pointerId;
            if (
                !fingers.TryGetValue(id, out var before)
                || !Local(eventData.position, eventData, out var now)
            )
                return;
            fingers[id] = now;
            if (fingers.Count == 1)
            {
                Dragged?.Invoke(now - before);
                return;
            }
            // 動いた指と止まっている指の間の長さの変わり方で大きさを変え、中点の動きで地図を動かす。
            var other = Other(id);
            float was = Vector2.Distance(before, other);
            if (was < 1f)
                return;
            Pinched?.Invoke(
                (before + other) * 0.5f,
                (now + other) * 0.5f,
                Vector2.Distance(now, other) / was
            );
        }

        public void OnScroll(PointerEventData eventData)
        {
            float wheel = eventData.scrollDelta.y;
            if (wheel == 0f || !Local(eventData.position, eventData, out var at))
                return;
            Pinched?.Invoke(at, at, wheel > 0f ? WheelStep : 1f / WheelStep);
        }

        // 画面を離れる間に指を離しても、次に開いたとき触れたままにならないようにする。
        private void OnDisable()
        {
            if (fingers.Count == 0)
                return;
            fingers.Clear();
            Holding?.Invoke(false);
        }

        private Vector2 Other(int id)
        {
            foreach (var (key, position) in fingers)
                if (key != id)
                    return position;
            return Vector2.zero;
        }

        private bool Local(Vector2 screen, PointerEventData eventData, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform,
                screen,
                eventData.pressEventCamera,
                out local
            );
    }
}
