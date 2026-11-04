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
    public sealed class GuideFramedPanel : MonoBehaviour
    {
        // 金の線を引かず、余白と文字の大きさで分けるパネル（商会の合成。doc/rules/ui-design.md の線のない形）。
        // 開いている間は、ヘッダーの下の線と区切りの線を隠す。
        public bool Lineless;
    }
}
