using TMPro;

namespace Baryonyx.UI
{
    /// <summary>Text helpers the views share.</summary>
    public static class UiText
    {
        /// <summary>Sets the label's text when the label is wired; null shows as empty.</summary>
        public static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }
    }
}
