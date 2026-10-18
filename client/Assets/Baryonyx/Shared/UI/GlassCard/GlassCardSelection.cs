using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// On a glass card's gold rim and glow (<see cref="GlassCard.Selection"/>, over the contents):
    /// shows the gold wash under the contents with it, so a view that only turns the rim on and
    /// off (a button's "Selected" object) also tints the plate.
    /// </summary>
    [ExecuteAlways]
    public sealed class GlassCardSelection : MonoBehaviour
    {
        public GameObject Tint;

        private void OnEnable()
        {
            if (Tint != null)
                Tint.SetActive(true);
        }

        private void OnDisable()
        {
            if (Tint != null)
                Tint.SetActive(false);
        }
    }
}
