using UnityEngine;
using UnityEngine.EventSystems;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Passes a tap on the battlefield on to <see cref="BattleInspectView"/> while a tapped card
    /// waits for its target.
    /// </summary>
    public sealed class BattleInspectTapArea : MonoBehaviour, IPointerClickHandler
    {
        public BattleInspectView View;

        public void OnPointerClick(PointerEventData eventData) =>
            View.TapScreen(eventData.position);
    }
}
