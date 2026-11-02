using System;
using System.Collections.Generic;
using Baryonyx.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// The attribute icons of a piece of equipment in a row (doc/features/equipment.md#装備の属性),
    /// baked by ElementIconRowAssets. The row's layout group lines them up from its side, so a
    /// hidden icon leaves no gap; equipment without an attribute shows none.
    /// </summary>
    public sealed class ElementIconRow : MonoBehaviour
    {
        // CardElement の順の属性のアイコン（None は使わない）。
        public Sprite[] Icons = Array.Empty<Sprite>();

        // 並べるアイコンの置き場所。属性の数より多い分は隠す。
        public Image[] Slots = Array.Empty<Image>();

        /// <summary>Shows one icon per element, in the given order; none hides them all.</summary>
        public void Show(IReadOnlyList<CardElement> elements)
        {
            int count = 0;
            if (elements != null)
                foreach (var element in elements)
                {
                    var icon = IconOf(element);
                    if (icon == null || count >= Slots.Length)
                        continue;
                    Slots[count].sprite = icon;
                    Slots[count].gameObject.SetActive(true);
                    count++;
                }
            for (int i = count; i < Slots.Length; i++)
                Slots[i].gameObject.SetActive(false);
        }

        public bool Any
        {
            get
            {
                foreach (var slot in Slots)
                    if (slot != null && slot.gameObject.activeSelf)
                        return true;
                return false;
            }
        }

        private Sprite IconOf(CardElement element)
        {
            int index = (int)element;
            return element != CardElement.None && index < Icons.Length ? Icons[index] : null;
        }
    }
}
