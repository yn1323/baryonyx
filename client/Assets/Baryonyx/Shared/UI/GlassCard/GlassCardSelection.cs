using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// On a glass card's gold rim and glow (<see cref="GlassCard.Selection"/>, over the contents).
    /// A chosen card and a deciding button light only the rim; a card that stands for a ticked
    /// box (<see cref="Wash"/>, the shop's 売る) also shows the gold wash under the contents with
    /// it, so a view that only turns the rim on and off also tints the plate.
    /// </summary>
    [ExecuteAlways]
    public sealed class GlassCardSelection : MonoBehaviour
    {
        public GameObject Tint;

        // 地を金色に塗るのは、チェックボックスのように入れた物を示す札だけ（2026-11-02、ユーザー）。
        public bool Wash;

        /// <summary>Turns the gold wash under the rim on or off, now and whenever the rim shows.</summary>
        public void SetWash(bool wash)
        {
            Wash = wash;
            ShowTint(wash && gameObject.activeSelf);
        }

        private void OnEnable() => ShowTint(Wash);

        private void OnDisable() => ShowTint(false);

        private void ShowTint(bool shown)
        {
            if (Tint != null)
                Tint.SetActive(shown);
        }
    }
}
