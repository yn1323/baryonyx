using UnityEngine;

namespace Baryonyx.UI.GuideMenu
{
    /// <summary>
    /// Marks a guide screen's feature panel that fills the screen inside the guide screen's frame:
    /// while it is open, the background is blurred, the header ("back", the breadcrumb, the rune
    /// balance and ×) sits on a translucent band with thin lines between its parts
    /// (GuideFrameAssets builds them). The panel draws its
    /// own translucent panes under the header (e.g. the formation's adventurers).
    /// </summary>
    public sealed class GuideFramedPanel : MonoBehaviour { }
}
