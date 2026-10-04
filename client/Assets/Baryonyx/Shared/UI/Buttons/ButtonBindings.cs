using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Baryonyx.UI.Buttons
{
    /// <summary>
    /// The click handlers a view adds while it is open: bound in OnEnable and removed together in
    /// OnDisable, so reopening the view does not stack them. Unwired buttons are skipped.
    /// </summary>
    public sealed class ButtonBindings
    {
        private readonly List<(Button Button, UnityAction Action)> bindings = new();

        public void Bind(Button button, UnityAction action)
        {
            if (button == null)
                return;
            button.onClick.AddListener(action);
            bindings.Add((button, action));
        }

        public void Clear()
        {
            foreach (var (button, action) in bindings)
                if (button != null)
                    button.onClick.RemoveListener(action);
            bindings.Clear();
        }
    }
}
