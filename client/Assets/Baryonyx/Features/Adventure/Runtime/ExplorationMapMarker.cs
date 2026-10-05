using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// One room on the exploration map (doc/features/screens.md): its round plate and kind icon,
    /// the glow of a strong monster or of the deepest room seen through the fog, the hint beside a
    /// room the party can go to next, and where it is tapped. <see cref="ExplorationView"/> makes
    /// one from the template for each room it shows, and fades it in as the fog clears and out
    /// as the room drops out of sight.
    /// </summary>
    public sealed class ExplorationMapMarker : MonoBehaviour
    {
        public RectTransform Body;
        public CanvasGroup Group;
        public Image Glow;
        public Image Ring;
        public Image Plate;
        public Image Icon;
        public Button Button;
        public RectTransform HintBox;
        public TMP_Text Hint;

        public string RoomId { get; set; } = "";

        // 今いる部屋から進める部屋。押すとその部屋へ進む。
        public bool IsExit { get; set; }
        public ExplorationRoomSight Sight { get; set; }

        // 見え方（1で見える）と、向かう先、薄れ始めるまでの待ち時間（秒）。
        public float Alpha { get; set; } = 1f;
        public float TargetAlpha { get; set; } = 1f;
        public float Wait { get; set; }
        public string HintText => Hint != null && HintBox.gameObject.activeSelf ? Hint.text : "";
    }
}
