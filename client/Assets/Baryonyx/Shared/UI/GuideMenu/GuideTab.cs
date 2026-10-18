using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI.GuideMenu
{
    /// <summary>
    /// A tab at the top of a pane inside the guide screen's frame (GuideFrameAssets builds it):
    /// the label in the sub text colour, and in light gold over a gold underline when chosen. A
    /// tab that is not ready (<see cref="Locked"/>) shows a lock and takes no taps.
    /// </summary>
    public sealed class GuideTab : MonoBehaviour
    {
        public Button Button;
        public TMP_Text Label;

        // 選んだときの金の下線。
        public GameObject Selected;

        // まだ使えない（準備中の）タブの錠前。
        public GameObject Lock;

        public bool Chosen
        {
            get => Selected != null && Selected.activeSelf;
            set
            {
                if (Selected != null)
                    Selected.SetActive(value);
                if (Label == null)
                    return;
                var color = value ? UiPalette.GoldLight : UiPalette.TextSub;
                // 準備中のタブは薄くする。
                color.a = Locked ? LockedAlpha : 1f;
                Label.color = color;
            }
        }

        public bool Locked => Lock != null && Lock.activeSelf;

        private const float LockedAlpha = 0.5f;
    }
}
