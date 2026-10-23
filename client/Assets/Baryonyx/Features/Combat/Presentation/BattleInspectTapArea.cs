using UnityEngine;
using UnityEngine.EventSystems;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Passes a tap on the battlefield on to <see cref="BattleInspectView"/>: the target of a
    /// tapped card that waits, or, with no card up, the character whose statuses to list.
    /// </summary>
    public sealed class BattleInspectTapArea : MonoBehaviour, IPointerClickHandler
    {
        public BattleInspectView View;

        public void OnPointerClick(PointerEventData eventData) =>
            View.TapScreen(eventData.position);
    }
}
