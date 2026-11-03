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

        /// <summary>
        /// What a price the runes held cannot cover shows, beside the button it keeps from being
        /// pressed (doc/rules/ui-design.md#操作の決まり).
        /// </summary>
        public static string RunesShort(long missing) => $"ルーンが {missing:N0} 足りません";

        /// <summary>The count beside a list's heading (the shop's lists).</summary>
        public static string Count(int count) => $"{count}件";

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
