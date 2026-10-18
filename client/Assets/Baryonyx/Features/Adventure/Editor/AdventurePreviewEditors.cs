using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;

namespace Baryonyx.Adventure.Editor
{
    /// <summary>The exploration screen's inspector, with buttons that show its dialogs while stopped.</summary>
    [CustomEditor(typeof(ExplorationView))]
    public sealed class ExplorationViewEditor : EditModePreviewEditor { }

    /// <summary>The battle's adventure overlay inspector, with buttons that show its dialogs while stopped.</summary>
    [CustomEditor(typeof(AdventureOverlay))]
    public sealed class AdventureOverlayEditor : EditModePreviewEditor { }
}
