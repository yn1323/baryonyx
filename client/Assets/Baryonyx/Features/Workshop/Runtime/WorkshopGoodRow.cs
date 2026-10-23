using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// One card or row in the trading house's lists: its art, name, price or quality, and the
    /// line under the name. The cards of held things also show the quality's letter, how many,
    /// and a mark when someone uses the thing; a part a card does not have is null.
    /// </summary>
    public sealed class WorkshopGoodRow : MonoBehaviour
    {
        public GlassCard Card;
        public RawImage Art;
        public TMP_Text Name;
        public TMP_Text Price;
        public TMP_Text Detail;

        // 持ち物の札：左上の品質の文字、右下の数、右上の「E」（使っている物）。
        public TMP_Text Quality;
        public TMP_Text Count;
        public GameObject InUse;
    }
}
