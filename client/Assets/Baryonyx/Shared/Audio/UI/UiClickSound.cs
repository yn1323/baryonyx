using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baryonyx.Audio
{
    /// <summary>
    /// Plays the press sound when the button or toggle on the same object is clicked, or the denied
    /// sound when it cannot be pressed (<see cref="UiSounds"/> adds it). A click comes only when the
    /// finger lifts where it pressed without dragging, so scrolling a list plays nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiClickSound : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            if (
                eventData.button != PointerEventData.InputButton.Left
                || !TryGetComponent(out Selectable selectable)
            )
                return;
            UiSounds.Play(selectable.IsInteractable() ? UiSound.Press : UiSound.Denied);
        }
    }
}
