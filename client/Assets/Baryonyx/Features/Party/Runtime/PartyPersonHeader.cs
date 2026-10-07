using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.UI.UiText;

namespace Baryonyx.Party
{
    /// <summary>
    /// The person at the top of the formation's change screens (equipment, card skills): ◀ and ▶
    /// around the figure, the name, the level and the elements they can use. Baked by the
    /// generator; the screens show the person and wire the arrows.
    /// </summary>
    [Serializable]
    public sealed class PartyPersonHeader
    {
        public Button Prev;
        public Button Next;
        public RawImage Figure;
        public TMP_Text Name;
        public TMP_Text Level;
        public Image[] Elements = Array.Empty<Image>();

        public void Show(
            PartyMember member,
            int level,
            IReadOnlyList<Sprite> elements,
            bool canSwitch
        )
        {
            if (Figure != null)
            {
                Figure.enabled = member != null && member.Art != null;
                if (Figure.enabled)
                    PartyArt.Paint(Figure, member, trim: false);
            }
            Set(Name, member?.Name ?? "");
            Set(Level, member != null ? level.ToString() : "");
            for (int i = 0; i < Elements.Length; i++)
            {
                if (Elements[i] == null)
                    continue;
                var icon = elements != null && i < elements.Count ? elements[i] : null;
                Elements[i].sprite = icon;
                Elements[i].gameObject.SetActive(icon != null);
            }
            if (Prev != null)
                Prev.interactable = canSwitch;
            if (Next != null)
                Next.interactable = canSwitch;
        }

        // 読み込むまで、仮データの人を本当の仲間として見せない。
        public void ShowLoading(string text)
        {
            Show(null, 0, null, false);
            Set(Name, text);
        }
    }
}
