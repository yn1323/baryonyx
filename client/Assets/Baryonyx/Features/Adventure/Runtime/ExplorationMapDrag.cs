using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The exploration map under the finger (doc/features/screens.md): dragging it looks along
    /// the route ahead (<see cref="ExplorationView.Look"/>). A drag that starts on a room's plate
    /// or the chest moves the map too, and does not press them; a tap still does.
    /// </summary>
    public sealed class ExplorationMapDrag : MonoBehaviour, IDragHandler
    {
        // 指の動き（地図の設計座標、y が上）。
        public event Action<Vector2> Dragged;

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
