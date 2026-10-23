using Baryonyx.UI.GuideMenu;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Temple
{
    /// <summary>
    /// The window over the temple that shows the summoning's rates (the kinds, the stars of
    /// equipment and items, the qualities of equipment) and everything that can come out
    /// (doc/features/monetization.md). It is baked from <see cref="TempleSummon"/>, so it shows
    /// the rates the summoning keeps. ×, the dark veil, "back" and the device back key close it.
    /// </summary>
    public sealed class TempleSummonRates : MonoBehaviour
    {
        public GameObject Window;
        public Button Veil;
        public Button Close;
        public ScrollRect Scroll;

        public bool IsOpen => Window != null && Window.activeSelf;

        private void Awake()
        {
            if (Veil != null)
                Veil.onClick.AddListener(Hide);
            if (Close != null)
                Close.onClick.AddListener(Hide);
        }

        public void Open()
        {
            if (Window != null)
                Window.SetActive(true);
            if (Scroll != null)
                Scroll.verticalNormalizedPosition = 1f;
        }

        public void Hide()
        {
            if (Window != null)
                Window.SetActive(false);
        }
    }
}
