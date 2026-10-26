using System.Linq;
using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// The showcase's sample of the card back (<see cref="GlassCardBack"/>): cards of every list's
    /// size lie face down for <see cref="FaceDownSeconds"/>, then all turn over together, over and
    /// over, as a list does when its first read ends. The <see cref="Empty"/> ones stand for the
    /// places a list leaves empty: they turn to an empty face and fade away.
    /// </summary>
    public sealed class GlassCardBackPreview : MonoBehaviour
    {
        public GameObject[] Cards = System.Array.Empty<GameObject>();

        // 一覧に出す物がない場所の見本（めくってから消える）。
        public GameObject[] Empty = System.Array.Empty<GameObject>();

        [Tooltip("伏せておく時間（秒）。")]
        public float FaceDownSeconds = 1f;

        [Tooltip("めくってから、また伏せるまでの時間（秒）。")]
        public float FaceUpSeconds = 1.6f;

        private readonly GlassCardCover cover = new();
        private float elapsed;

        private void OnEnable()
        {
            cover.Cover(Cards.Concat(Empty));
            elapsed = 0f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (cover.Covered && elapsed >= FaceDownSeconds)
                cover.Reveal(Cards.Where(card => card != null), Empty);
            else if (!cover.Covered && elapsed >= FaceDownSeconds + FaceUpSeconds)
            {
                cover.Cover(Cards.Concat(Empty));
                elapsed = 0f;
            }
        }
    }
}
