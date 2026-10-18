using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;

namespace Baryonyx.Training.Editor
{
    /// <summary>
    /// The training page's inspector. Its level-up dialog is switched from the guide screen
    /// that opens the page ("育成（training） › レベルアップ").
    /// </summary>
    [CustomEditor(typeof(TrainingView))]
    public sealed class TrainingViewEditor : EditModePreviewEditor { }
}
