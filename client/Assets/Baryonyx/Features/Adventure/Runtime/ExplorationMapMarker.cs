using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// One room on the exploration map (doc/features/screens.md): its round pedestal coloured by
    /// the room's kind and its kind icon, the glow of a room to go to next or of the deepest room,
    /// and where it is tapped. <see cref="ExplorationView"/> makes one from the template for each
    /// room of the route, and fades out the rooms not chosen as the party walks on.
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

        public string RoomId { get; set; } = "";

        // 今いるマスから進めるマス。押すとそのマスへ進む。
        public bool IsExit { get; set; }

        // 見え方（1で見える）と、向かう先、薄れ始めるまでの待ち時間（秒）。
        public float Alpha { get; set; } = 1f;
        public float TargetAlpha { get; set; } = 1f;
        public float Wait { get; set; }
    }
}
