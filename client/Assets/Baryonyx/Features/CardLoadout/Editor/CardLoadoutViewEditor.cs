using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;

namespace Baryonyx.CardLoadout.Editor
{
    /// <summary>
    /// The card skills' change screen inspector. Its states are switched from the guide screen
    /// that opens the panel.
    /// </summary>
    [CustomEditor(typeof(CardLoadoutView))]
    public sealed class CardLoadoutViewEditor : EditModePreviewEditor { }
}
