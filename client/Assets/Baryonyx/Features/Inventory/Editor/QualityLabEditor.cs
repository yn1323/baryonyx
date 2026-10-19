using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;

namespace Baryonyx.Inventory.Editor
{
    /// <summary>The quality lab's inspector, with buttons that show a skill, a weapon and a bonus at a quality while stopped.</summary>
    [CustomEditor(typeof(QualityLab))]
    public sealed class QualityLabEditor : EditModePreviewEditor { }
}
