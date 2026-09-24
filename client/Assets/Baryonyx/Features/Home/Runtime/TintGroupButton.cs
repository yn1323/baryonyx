using System;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Home
{
    /// <summary>
    /// A <see cref="Button"/> whose Color Tint also reaches the icon and labels drawn over its
    /// target graphic. Buttons with a dark backdrop show no press feedback otherwise, because
    /// darkening near-black changes nothing visible.
    /// </summary>
    public sealed class TintGroupButton : Button
    {
        [SerializeField]
        private Graphic[] tintGraphics = Array.Empty<Graphic>();

        public Graphic[] TintGraphics => tintGraphics;

        public void SetTintGraphics(Graphic[] graphics)
        {
            tintGraphics = graphics ?? Array.Empty<Graphic>();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (!gameObject.activeInHierarchy || transition != Transition.ColorTint)
                return;

            var block = colors;
            Color tint = state switch
            {
                SelectionState.Normal => block.normalColor,
                SelectionState.Highlighted => block.highlightedColor,
                SelectionState.Pressed => block.pressedColor,
                SelectionState.Selected => block.selectedColor,
                SelectionState.Disabled => block.disabledColor,
                _ => Color.black,
            };
            tint *= block.colorMultiplier;
            float duration = instant ? 0f : block.fadeDuration;
            foreach (var graphic in tintGraphics)
                if (graphic != null && graphic != targetGraphic)
                    graphic.CrossFadeColor(tint, duration, true, true);
        }
    }
}
