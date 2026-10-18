using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;

namespace Baryonyx.UI.Editor
{
    /// <summary>The notice band's inspector, with buttons that show it still while stopped.</summary>
    [CustomEditor(typeof(NoticeBand))]
    public sealed class NoticeBandEditor : EditModePreviewEditor { }
}
