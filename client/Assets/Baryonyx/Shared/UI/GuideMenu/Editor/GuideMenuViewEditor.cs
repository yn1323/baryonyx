using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;

namespace Baryonyx.UI.GuideMenu.Editor
{
    /// <summary>The guide screen's inspector, with the buttons that open each item while stopped.</summary>
    [CustomEditor(typeof(GuideMenuView))]
    public sealed class GuideMenuViewEditor : EditModePreviewEditor { }
}
