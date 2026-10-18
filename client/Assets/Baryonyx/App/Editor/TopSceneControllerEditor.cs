using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;

namespace Baryonyx.App.Editor
{
    /// <summary>The title screen's inspector, with buttons that show each prompt while stopped.</summary>
    [CustomEditor(typeof(TopSceneController))]
    public sealed class TopSceneControllerEditor : EditModePreviewEditor { }
}
