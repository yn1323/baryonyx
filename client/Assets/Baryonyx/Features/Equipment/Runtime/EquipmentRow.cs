using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Equipment
{
    /// <summary>
    /// One owned item's row on the right of the equipment, baked by the generator. The view
    /// copies a baked row when the player owns more items than were baked.
    /// </summary>
    public sealed class EquipmentRow : MonoBehaviour
    {
        public Button Button;
        public Image Icon;
        public TMP_Text Name;
        public TMP_Text Stars;
        public TMP_Text Detail;

        // 誰が付けているか（「装備中」「アリアが装備中」）。誰も付けていなければ隠す。
        public TMP_Text Mark;

        // 行が出している装備のID。
        public string ItemId { get; set; }
    }
}
