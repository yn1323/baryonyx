using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// One enhancement material under the enhance list: its art and how many are held. A
    /// material without art yet shows its short name in the art's place. A stone in use shows a
    /// red cross at its top right that stops using it.
    /// </summary>
    public sealed class WorkshopMaterialChip : MonoBehaviour
    {
        public GlassCard Card;
        public RawImage Art;
        public TMP_Text Name;
        public TMP_Text Count;

        // 使っている磨き石の札の右上に出す赤い×。押すと、その色を使う数を0に戻す。
        public Button Cancel;
    }
}
