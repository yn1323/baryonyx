using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// The faces of a button whose role changes with what it is given (the shared dialog's row
    /// of choices), one shown at a time (doc/rules/ui-design.md#決めるボタン).
    /// ActionButtonAssets.Switchable builds it.
    /// </summary>
    public sealed class ButtonFaces : MonoBehaviour
    {
        public GameObject Primary;
        public GameObject Secondary;
        public GameObject Danger;

        public ButtonRole Role { get; private set; } = ButtonRole.Secondary;

        public void Show(ButtonRole role)
        {
            Role = role;
            if (Primary != null)
                Primary.SetActive(role == ButtonRole.Primary);
            if (Secondary != null)
                Secondary.SetActive(role == ButtonRole.Secondary);
            if (Danger != null)
                Danger.SetActive(role == ButtonRole.Danger);
        }
    }
}
