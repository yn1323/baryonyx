using System;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Plays shock rings side by side, over and over, for the showcase: the ring drawn from a
    /// picture (VfxShockwave.png) and the one the ring shader works out with no picture, spread
    /// and faded by the same steps as <see cref="BattleSkillVfx"/>'s ring, so the two can be
    /// compared at the same moment.
    /// </summary>
    public sealed class BattleRingComparison : MonoBehaviour
    {
        /// <summary>One ring to play, standing up or lying on the floor.</summary>
        [Serializable]
        public sealed class Ring
        {
            public RawImage Image;
            public bool Floor;
        }

        public Ring[] Rings = Array.Empty<Ring>();

        [Tooltip("輪の色。斬撃の弱点の当たりで床に出す輪と同じ色。")]
        public Color Color = BattleSkillVfx.ColorOf(BattleSkillVfxKind.Slash);

        [Tooltip("1つの輪が広がって消えるまで（秒）。")]
        [Min(0.05f)]
        public float Seconds = 0.45f;

        [Tooltip("輪が消えてから次の輪までの間（秒）。")]
        [Min(0f)]
        public float Gap = 0.6f;

        private float time;

        private void OnEnable()
        {
            time = 0f;
            Show(0f);
        }

        private void Update()
        {
            time = (time + Time.deltaTime) % (Seconds + Gap);
            Show(Mathf.Min(1f, time / Seconds));
        }

        /// <summary>Shows every ring at <paramref name="k"/> (0 to 1 of its life).</summary>
        public void Show(float k)
        {
            k = Mathf.Clamp01(k);
            foreach (var ring in Rings)
            {
                if (ring.Image == null)
                    continue;
                ring.Image.rectTransform.localScale = BattleSkillVfx.RingScale(k, ring.Floor);
                ring.Image.color = new Color(Color.r, Color.g, Color.b, 1f - k);
            }
        }
    }
}
