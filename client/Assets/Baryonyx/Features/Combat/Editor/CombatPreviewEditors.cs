using Baryonyx.Combat.Presentation;
using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;

namespace Baryonyx.Combat.Editor
{
    /// <summary>The battle screen's inspector, with buttons that show its bands and a raised card while stopped.</summary>
    [CustomEditor(typeof(BattleInspectView))]
    public sealed class BattleInspectViewEditor : EditModePreviewEditor { }

    /// <summary>The card skill lab's inspector, with buttons that open and close its list while stopped.</summary>
    [CustomEditor(typeof(CardSkillLab))]
    public sealed class CardSkillLabEditor : EditModePreviewEditor { }
}
