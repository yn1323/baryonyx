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

        /// <summary>The same, reading the colour tags in <paramref name="text"/> (rich text).</summary>
        public static void SetRich(TMP_Text label, string text)
        {
            if (label == null)
                return;
            label.richText = true;
            label.text = text ?? "";
        }
    }
}
