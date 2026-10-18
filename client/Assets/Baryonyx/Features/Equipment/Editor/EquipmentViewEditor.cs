using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;

namespace Baryonyx.Equipment.Editor
{
    /// <summary>
    /// The equipment change screen's inspector: shows the armour and accessory tabs and the
    /// loading states while stopped. The guide screen that opens the screen lists them too
    /// ("装備（equipment） › 防具").
    /// </summary>
    [CustomEditor(typeof(EquipmentView))]
    public sealed class EquipmentViewEditor : EditModePreviewEditor { }
}
