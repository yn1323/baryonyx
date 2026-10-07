using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Baryonyx.Training
{
    /// <summary>
    /// A horizontal flick over the adventurer's page: to the left shows the next person, to the
    /// right the previous one. It sits on the layer that holds the page, so a drag that starts on
    /// a frame or a slot reaches it too; once the finger moves, the slot's tap is cancelled.
    /// </summary>
    public sealed class TrainingSwipe
        : MonoBehaviour,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler
    {
        // 指を離すまでに横へ動いた距離（設計座標）。縦の動きより大きいときだけ人を替える。
        public const float Distance = 120f;

        /// <summary>1 for the next person, -1 for the previous one.</summary>
        public event Action<int> Flicked;

        // ドラッグを受け取るために実装する（動いている間は何もしない）。
        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            int step = StepOf((eventData.position - eventData.pressPosition) / scale);
            if (step != 0)
                Flicked?.Invoke(step);
        }

        /// <summary>
        /// The person a flick of <paramref name="delta"/> (design pixels) moves to: 1 for a long
        /// enough flick to the left, -1 to the right, 0 for a short or mostly vertical one.
        /// </summary>
        public static int StepOf(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) < Distance || Mathf.Abs(delta.x) <= Mathf.Abs(delta.y))
                return 0;
            return delta.x < 0 ? 1 : -1;
        }
    }
}
