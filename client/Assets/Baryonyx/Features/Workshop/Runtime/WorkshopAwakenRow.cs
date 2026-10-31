using Baryonyx.UI.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// One row of the skill awakening list: a glass card with, from the left, the person who has
    /// the skill (their battle sprite, or nothing for a card no one has), the card's art with its
    /// element (no cost: the awakening scene's card shows it), the name, the kind and the effect,
    /// and the quality's letter and multiplier at the right end.
    /// </summary>
    public sealed class WorkshopAwakenRow : MonoBehaviour
    {
        public Button Button;

        // ガラスのカード。読み込む間は、これを伏せる（GlassCardCover）。
        public GameObject Card;

        // 行の高さ。キャラが替わる行だけ、カードの上を空ける（カードは下端に寄せてある）。
        public LayoutElement Size;

        public RawImage Character;
        public RawImage Art;
        public CardFoil Foil;
        public Image Element;
        public TMP_Text Name;
        public TMP_Text Kind;
        public TMP_Text Description;
        public TMP_Text QualityLetter;
        public TMP_Text Multiplier;
    }
}
