using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// One enhancement material under the enhance list: its art and how many are held. A
    /// material without art yet shows its short name in the art's place.
    /// </summary>
    public sealed class WorkshopMaterialChip : MonoBehaviour
    {
        public GlassCard Card;
        public RawImage Art;
        public TMP_Text Name;
        public TMP_Text Count;
    }
}
