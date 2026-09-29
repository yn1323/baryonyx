using UnityEngine;
using UnityEngine.EventSystems;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>Passes a card's press, drag and release on to <see cref="BattleInspectView"/>.</summary>
    public sealed class BattleInspectCardInput
        : MonoBehaviour,
            IPointerDownHandler,
            IDragHandler,
            IPointerUpHandler
    {
        public BattleInspectView View;
        public int Index;

        // Only the finger that pressed the card moves it.
        private int pointer = int.MinValue;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointer != int.MinValue)
                return;
            pointer = eventData.pointerId;
            View.PressCard(Index, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == pointer)
                View.DragCard(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointer)
                return;
            pointer = int.MinValue;
            View.ReleaseCard(eventData.position);
        }
    }
}
