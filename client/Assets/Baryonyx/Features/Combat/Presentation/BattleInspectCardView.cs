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

        public RawImage Art;
        public RawImage Frame;
        public TMP_Text Name;
        public TMP_Text Effect;
        public TMP_Text Description;
        public TMP_Text Target;
        public RawImage CostDigit;

        /// <summary>
        /// Fills in one card. The art keeps the crop (uvRect) set in the prefab.
        /// </summary>
        public void Show(
            string cardName,
            string effect,
            string description,
            string target,
            Texture art,
            Texture frame,
            int cost
        )
        {
            Name.text = cardName;
            Effect.text = effect;
            Description.text = description;
            Target.text = target;
            Art.texture = art;
            Frame.texture = frame;
            CostDigit.uvRect = new Rect(cost / (float)CostDigits, 0f, 1f / CostDigits, 1f);
        }
    }
}
