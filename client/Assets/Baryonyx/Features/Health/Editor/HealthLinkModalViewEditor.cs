using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;

namespace Baryonyx.Health.Editor
{
    /// <summary>The link modal's inspector, with buttons that show each link state while stopped.</summary>
    [CustomEditor(typeof(HealthLinkModalView))]
    public sealed class HealthLinkModalViewEditor : EditModePreviewEditor { }
}
