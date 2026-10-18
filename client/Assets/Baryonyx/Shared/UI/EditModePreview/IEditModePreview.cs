using System.Collections.Generic;

namespace Baryonyx.UI.EditModePreview
{
    /// <summary>
    /// A screen that switches between UIs in the same scene while playing (pages, item panels,
    /// overlays, dialogs). It names those states so the editor can show each one while stopped
    /// (doc/rules/ui-design.md#停止中の表示の切り替え). The editor calls
    /// <see cref="ShowPreviewState"/> on an unsaved copy of the screen, never on the saved one.
    /// </summary>
    public interface IEditModePreview
    {
        // Inspectorのボタンに出す、状態の名前。Play中に操作で切り替わる順に並べる。
        IReadOnlyList<string> PreviewStates { get; }

        /// <summary>
        /// Shows the state at <paramref name="index"/> with the data baked into the prefab.
        /// Runs while stopped, where Awake, Start and OnEnable have not run: change only the
        /// objects under this component's GameObject, and do not load, save or touch static state.
        /// </summary>
        void ShowPreviewState(int index);
    }
}
