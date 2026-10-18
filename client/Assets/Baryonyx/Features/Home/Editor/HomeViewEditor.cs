using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;

namespace Baryonyx.Home.Editor
{
    /// <summary>The home screen's inspector, with buttons that show its other states while stopped.</summary>
    [CustomEditor(typeof(HomeView))]
    public sealed class HomeViewEditor : EditModePreviewEditor { }
}
