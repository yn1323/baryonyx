using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The notes beside a raised card, one frosted-glass plaque for each keyword its text names
    /// (doc/features/screens/battle.md, the battle mock): the keyword, then what it means. The plaques
    /// stack down from the top of this rect, each as tall as its text, and the rect grows to hold
    /// them all.
    /// </summary>
    public sealed class BattleInspectKeywordTips : MonoBehaviour
    {
        /// <summary>The plaques, used from the first; the ones left over are hidden.</summary>
        public RectTransform[] Slots;

        public TMP_Text[] Titles;
        public TMP_Text[] Bodies;

        /// <summary>Space between two plaques, in px.</summary>
        public float Gap = 10f;

        /// <summary>The plaque's height above the body's text (padding and the title), in px.</summary>
        public float Head = 58f;

        /// <summary>The plaque's padding under the body's text, in px.</summary>
        public float Foot = 18f;

        private IReadOnlyList<CardKeyword> shown;

        /// <summary>The width of the plaques, in px.</summary>
        public float Width => ((RectTransform)transform).rect.width;

        /// <summary>
        /// Shows a plaque for each of <paramref name="keywords"/> (as many as there are slots),
        /// or nothing for none. Showing the same list again does nothing.
        /// </summary>
        public void Show(IReadOnlyList<CardKeyword> keywords)
        {
            if (ReferenceEquals(keywords, shown))
                return;
            shown = keywords;
            int count = keywords == null ? 0 : Mathf.Min(keywords.Count, Slots.Length);
            gameObject.SetActive(count > 0);
            float top = 0f;
            for (int i = 0; i < Slots.Length; i++)
            {
                bool used = i < count;
                Slots[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                Titles[i].text = keywords[i].Name;
                Bodies[i].text = keywords[i].Text;
                float bodyWidth = Bodies[i].rectTransform.rect.width;
                float height = Mathf.Ceil(
                    Head + Bodies[i].GetPreferredValues(keywords[i].Text, bodyWidth, 0f).y + Foot
                );
                Slots[i].anchoredPosition = new Vector2(0f, -top);
                Slots[i].sizeDelta = new Vector2(Slots[i].sizeDelta.x, height);
                top += height + Gap;
            }
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Max(0f, top - Gap));
        }

        public void Hide() => Show(null);
    }
}
