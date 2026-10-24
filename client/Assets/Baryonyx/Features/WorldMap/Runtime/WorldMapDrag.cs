using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// The world map under the finger: dragging it looks around in any direction
    /// (<see cref="WorldMapView"/>). A drag that starts on a square moves the map too and does not
    /// press it; a tap still does.
    /// </summary>
    public sealed class WorldMapDrag
        : MonoBehaviour,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler
    {
        // 指の動き（設計座標、y が上）。
        public event Action<Vector2> Dragged;
        public event Action<bool> Holding;

        public void OnBeginDrag(PointerEventData eventData) => Holding?.Invoke(true);

        public void OnEndDrag(PointerEventData eventData) => Holding?.Invoke(false);

        public void OnDrag(PointerEventData eventData)
        {
            var rect = (RectTransform)transform;
            var camera = eventData.pressEventCamera;
            if (
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect,
                    eventData.position,
                    camera,
                    out var now
                )
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect,
                    eventData.position - eventData.delta,
                    camera,
                    out var before
                )
            )
                Dragged?.Invoke(now - before);
        }
    }
}
