using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// One card of the mock hand (BattleInspectCard.prefab). The prefab owns the layout, so it
    /// can be adjusted in the Prefab editor; the screen only fills in each card's texts and art.
    /// </summary>
    public sealed class BattleInspectCardView : MonoBehaviour
    {
        /// <summary>The cost digits 0 to 9 sit side by side in one texture.</summary>
        public const int CostDigits = 10;

        /// <summary>Space kept dark after the end of each line's text before the band fades, in px.</summary>
        public const float BandMargin = 6f;

        /// <summary>The tint that turns the cost digit reddish on a card the energy cannot pay for.</summary>
        public static readonly Color ShortCost = new(1f, 0.32f, 0.28f);

        /// <summary>The back, drawn over the whole face while the card is face down.</summary>
        public RawImage Back;

        /// <summary>
        /// A dark veil over the whole card, the cost too, shown while the energy cannot pay for it.
        /// </summary>
        public RawImage Shade;

        public RawImage Frame;
        public RawImage Art;
        public RawImage Element;
        public RawImage CostDigit;
        public TMP_Text Owner;
        public TMP_Text Name;

        /// <summary>The kind and the scope on one line, e.g. "攻撃・敵単体" (rich text).</summary>
        public TMP_Text Kind;

        /// <summary>The effect, with its number in another colour (rich text).</summary>
        public TMP_Text Description;

        /// <summary>The navy gradient and the grey backdrop over it, both behind the three lines above the description.</summary>
        public BattleInspectCardBand[] Bands;

        /// <summary>
        /// Fills in one card. The element icon is hidden when <paramref name="element"/> is null.
        /// </summary>
        public void Show(
            string cardName,
            string owner,
            string kind,
            string description,
            Texture art,
            Texture frame,
            Texture element,
            int cost
        )
        {
            Name.text = cardName;
            Owner.text = owner;
            Kind.text = kind;
            Description.text = description;
            Art.texture = art;
            Frame.texture = frame;
            Element.texture = element;
            Element.enabled = element != null;
            CostDigit.uvRect = new Rect(cost / (float)CostDigits, 0f, 1f / CostDigits, 1f);
            FitBands();
        }

        /// <summary>
        /// Shows whether the energy can pay for the card: a card it cannot pay for is darkened
        /// by black of <paramref name="darkness"/> (0 to 1) and its cost turns reddish. The screen
        /// also makes it see-through while in the hand.
        /// </summary>
        public void SetPlayable(bool playable, float darkness)
        {
            if (Shade != null)
            {
                if (Shade.enabled == playable)
                    Shade.enabled = !playable;
                var black = new Color(0f, 0f, 0f, darkness);
                if (Shade.color != black)
                    Shade.color = black;
            }
            var tint = playable ? Color.white : ShortCost;
            if (CostDigit.color != tint)
                CostDigit.color = tint;
        }

        /// <summary>Ends each band line just after that line's text.</summary>
        public void FitBands()
        {
            var lines = new[] { Owner, Name, Kind };
            foreach (var band in Bands)
            {
                var ends = new float[lines.Length];
                for (int i = 0; i < lines.Length; i++)
                    ends[i] = LineEnd(lines[i], band.rectTransform);
                band.SetLineEnds(ends);
            }
        }

        private static float LineEnd(TMP_Text label, RectTransform band)
        {
            var rect = label.rectTransform;
            float left =
                band.InverseTransformPoint(rect.TransformPoint(rect.rect.min)).x - band.rect.xMin;
            float width = Mathf.Min(label.GetPreferredValues(label.text).x, rect.rect.width);
            return left + width + BandMargin;
        }
    }
}
