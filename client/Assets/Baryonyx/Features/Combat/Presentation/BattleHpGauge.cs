using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// A unit's HP bar drawn as pixel art (doc/features/screens.md, the battle mock): a dark
    /// outline, a fill shaded in three steps and ticks every quarter, all on the dot grid. When
    /// HP drops, the lost part stays as a red trail, flashes white, holds a moment and then
    /// drains down to the fill. An ally's block lies over the top of it as a thin yellow strip.
    /// </summary>
    public sealed class BattleHpGauge : MonoBehaviour
    {
        /// <summary>The HP, stretched from the left of the inner area (inside the outline).</summary>
        public RectTransform Fill;

        /// <summary>The HP just lost, behind <see cref="Fill"/>.</summary>
        public RectTransform Trail;

        /// <summary>The trail's body, flashed white as the HP drops.</summary>
        public Image TrailBody;

        /// <summary>The block strip over the top of the bar; null on an enemy's gauge.</summary>
        public RectTransform Block;

        /// <summary>The width of the inner area in dots, and the size of a dot in px.</summary>
        public int InnerDots = 28;
        public float DotSize = 4f;

        /// <summary>How long the trail waits before draining, and how long it drains, in seconds.</summary>
        public float TrailHold = 0.35f;
        public float TrailDrain = 0.4f;
        public float FlashTime = 0.12f;

        private static readonly Color Flash = new(1f, 0.96f, 0.9f);

        private Color trailColor;
        private bool started;
        private int dots;
        private float trail;
        private float drainFrom;
        private float hold;
        private float drain;
        private float flash;

        /// <summary>The part of the bar the fill covers, a whole number of dots.</summary>
        public float Ratio => dots / (float)InnerDots;

        /// <summary>The part of the bar the trail covers; it is the fill's once it has drained.</summary>
        public float TrailRatio => trail;

        /// <summary>Shows the HP. A drop leaves a trail behind the fill; a rise moves at once.</summary>
        public void SetHp(int hp, int max)
        {
            int next = DotsOf(hp, max);
            if (!started)
            {
                started = true;
                trailColor = TrailBody != null ? TrailBody.color : Color.white;
                trail = next / (float)InnerDots;
            }
            else if (next < dots)
            {
                // The trail keeps what was shown, even while it still drains from an earlier hit.
                trail = Mathf.Max(trail, Ratio);
                drainFrom = trail;
                hold = TrailHold;
                drain = 0f;
                flash = FlashTime;
            }
            dots = next;
            if (trail < Ratio)
                trail = Ratio;
            Fill.anchorMax = new Vector2(Ratio, 1f);
            ShowTrail();
        }

        /// <summary>Shows a block on the scale of the max HP; a block over it fills the bar.</summary>
        public void SetBlock(int block, int max)
        {
            if (Block == null)
                return;
            Block.gameObject.SetActive(block > 0 && max > 0);
            if (block <= 0 || max <= 0)
                return;
            int width = Mathf.Clamp(Mathf.RoundToInt(block / (float)max * InnerDots), 1, InnerDots);
            // The strip's own edge takes a dot on each side, over the bar's outline.
            Block.sizeDelta = new Vector2((width + 2) * DotSize, Block.sizeDelta.y);
        }

        /// <summary>
        /// Any HP shows at least a dot, and only a full bar fills every dot, so a sliver of HP or
        /// a scratch still shows.
        /// </summary>
        private int DotsOf(int hp, int max)
        {
            if (hp <= 0 || max <= 0)
                return 0;
            if (hp >= max)
                return InnerDots;
            return Mathf.Clamp(Mathf.RoundToInt(hp / (float)max * InnerDots), 1, InnerDots - 1);
        }

        private void Update()
        {
            if (!started)
                return;
            if (flash > 0f)
            {
                flash -= Time.deltaTime;
                if (TrailBody != null)
                    TrailBody.color = Color.Lerp(
                        trailColor,
                        Flash,
                        Mathf.Clamp01(flash / FlashTime)
                    );
            }
            if (trail <= Ratio)
                return;
            if (hold > 0f)
            {
                hold -= Time.deltaTime;
                return;
            }
            drain += Time.deltaTime / TrailDrain;
            float t = 1f - (1f - Mathf.Clamp01(drain)) * (1f - Mathf.Clamp01(drain));
            trail = drain >= 1f ? Ratio : Mathf.Lerp(drainFrom, Ratio, t);
            ShowTrail();
        }

        private void ShowTrail()
        {
            // The trail steps down a dot at a time, like the rest of the pixel art.
            float shown = Mathf.Max(dots, Mathf.Round(trail * InnerDots)) / InnerDots;
            Trail.anchorMax = new Vector2(shown, 1f);
            Trail.gameObject.SetActive(shown > Ratio);
        }
    }
}
