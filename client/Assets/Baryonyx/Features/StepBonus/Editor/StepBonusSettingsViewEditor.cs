using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;

namespace Baryonyx.StepBonus.Editor
{
    /// <summary>
    /// The bonus settings' inspector. Its tabs and read states are switched from the guide
    /// screen that opens the panel.
    /// </summary>
    [CustomEditor(typeof(StepBonusSettingsView))]
    public sealed class StepBonusSettingsViewEditor : EditModePreviewEditor { }
}
