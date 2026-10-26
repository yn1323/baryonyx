using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;

namespace Baryonyx.Tavern.Editor
{
    /// <summary>
    /// The adventurers' list inspector. Its states are switched from the guide screen that opens
    /// the panel ("冒険者 › 読み込み中").
    /// </summary>
    [CustomEditor(typeof(AdventurerRosterView))]
    public sealed class AdventurerRosterViewEditor : EditModePreviewEditor { }

    /// <summary>
    /// The growth picker inspector. Its states are switched from the guide screen that
    /// opens the panel ("育成 › 読み込み中").
    /// </summary>
    [CustomEditor(typeof(AdventurerPickerView))]
    public sealed class AdventurerPickerViewEditor : EditModePreviewEditor { }
}
