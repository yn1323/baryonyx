using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// A unit's HP bar drawn as pixel art (doc/features/screens/battle-deck.md, the battle mock): a dark
    /// outline, a fill shaded in three steps and ticks every quarter, all on the dot grid. When
    /// HP drops, the lost part stays as a red trail, flashes white, holds a moment and then
    /// drains down to the fill. An ally's block lies over the top of it as a thin yellow strip.
    /// The HP itself is written in the middle of the bar, over everything. While a card is up,
    /// the bar forecasts what it would do: the part it would take or give back blinks, with a
    /// bright dot line where the HP would end, and the block it would add blinks past the strip.
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

        /// <summary>The HP now, in the middle of the bar (the max is not shown).</summary>
        public TMP_Text Number;

        /// <summary>The forecast's band over the HP a card would take or give back, in the inner area.</summary>
        public Image ForecastBand;

        /// <summary>The dot line where the HP would end.</summary>
        public RectTransform ForecastEdge;

        /// <summary>The block a card would leave, behind <see cref="Block"/>; null without block.</summary>
        public RectTransform BlockForecast;

        /// <summary>Shown beside the bar when the card would beat the enemy; null on an ally's gauge.</summary>
        public GameObject DefeatTag;

        /// <summary>The band's colour for HP taken, and for HP given back.</summary>
        public Color LossColor = new(1f, 0.62f, 0.45f);
        public Color GainColor = new(0.55f, 1f, 0.78f);

        /// <summary>How long one blink of the forecast takes, and how faint it gets, in seconds and alpha.</summary>
        public float BlinkPeriod = 0.9f;
        public float BlinkLow = 0.35f;

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
        private int shownHp = -1;
        private bool forecasting;
        private Color bandColor;
        private CanvasGroup blockForecastGroup;
        private float trail;
        private float drainFrom;
        private float hold;
        private float drain;
        private float flash;

        /// <summary>The part of the bar the fill covers, a whole number of dots.</summary>
        public float Ratio => dots / (float)InnerDots;

        /// <summary>The part of the bar the trail covers; it is the fill's once it has drained.</summary>
        public float TrailRatio => trail;

        /// <summary>The number written on the bar for <paramref name="hp"/>: never below 0.</summary>
        public static string HpText(int hp) => Mathf.Max(0, hp).ToString();

        /// <summary>
        /// Shows the HP. A drop leaves a trail behind the fill; a rise moves at once. The number
        /// changes at once either way.
        /// </summary>
        public void SetHp(int hp, int max)
        {
            ShowNumber(hp);
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

        /// <summary>
        /// Shows the HP at once, with no trail nor flash, as an HP that was so already (a party
        /// coming into a battle hurt), not one just lost.
        /// </summary>
        public void ShowHpNow(int hp, int max)
        {
            started = true;
            trailColor = TrailBody != null && flash <= 0f ? TrailBody.color : trailColor;
            if (TrailBody != null && flash > 0f)
                TrailBody.color = trailColor;
            ShowNumber(hp);
            dots = DotsOf(hp, max);
            trail = Ratio;
            hold = 0f;
            drain = 0f;
            flash = 0f;
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

        /// <summary>True while the bar shows what a card would do.</summary>
        public bool Forecasting => forecasting;

        /// <summary>
        /// Forecasts a card: the HP going from <paramref name="hp"/> to <paramref name="hpAfter"/>
        /// (out of <paramref name="max"/>) and the block from <paramref name="block"/> to
        /// <paramref name="blockAfter"/>, with the defeat tag when <paramref name="defeat"/>. The
        /// number keeps the HP now. A change too small for a dot still lights the dot at the end.
        /// </summary>
        public void Forecast(int hp, int hpAfter, int max, int block, int blockAfter, bool defeat)
        {
            forecasting = true;
            ShowBand(hp, hpAfter, max);
            ShowBlockForecast(block, blockAfter, max);
            if (DefeatTag != null)
                DefeatTag.SetActive(defeat);
            Blink();
        }

        /// <summary>Takes the forecast away.</summary>
        public void ClearForecast()
        {
            forecasting = false;
            if (ForecastBand != null)
                ForecastBand.gameObject.SetActive(false);
            if (ForecastEdge != null)
                ForecastEdge.gameObject.SetActive(false);
            if (BlockForecast != null)
                BlockForecast.gameObject.SetActive(false);
            if (DefeatTag != null)
                DefeatTag.SetActive(false);
        }

        private void ShowBand(int hp, int hpAfter, int max)
        {
            if (ForecastBand == null)
                return;
            int now = DotsOf(hp, max);
            int after = DotsOf(hpAfter, max);
            if (hpAfter < hp && after == now)
                after = Mathf.Max(0, now - 1);
            else if (hpAfter > hp && after == now)
                after = Mathf.Min(InnerDots, now + 1);
            bool shown = after != now;
            ForecastBand.gameObject.SetActive(shown);
            if (ForecastEdge != null)
                ForecastEdge.gameObject.SetActive(shown);
            if (!shown)
                return;
            bandColor = after < now ? LossColor : GainColor;
            var band = ForecastBand.rectTransform;
            band.anchorMin = new Vector2(Mathf.Min(now, after) / (float)InnerDots, 0f);
            band.anchorMax = new Vector2(Mathf.Max(now, after) / (float)InnerDots, 1f);
            if (ForecastEdge == null)
                return;
            // The line takes the dot where the HP would end: the last one left, or the last one filled.
            float x = (after < now ? after : after - 1) * DotSize;
            ForecastEdge.anchoredPosition = new Vector2(
                Mathf.Max(0f, x),
                ForecastEdge.anchoredPosition.y
            );
        }

        private void ShowBlockForecast(int block, int blockAfter, int max)
        {
            if (BlockForecast == null)
                return;
            bool shown = blockAfter > block && max > 0;
            BlockForecast.gameObject.SetActive(shown);
            if (!shown)
                return;
            int width = Mathf.Clamp(
                Mathf.RoundToInt(blockAfter / (float)max * InnerDots),
                1,
                InnerDots
            );
            BlockForecast.sizeDelta = new Vector2((width + 2) * DotSize, BlockForecast.sizeDelta.y);
        }

        // The band and the new block fade in and out; the line where the HP would end stays lit.
        private void Blink()
        {
            float wave = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / BlinkPeriod);
            float alpha = Mathf.Lerp(BlinkLow, 1f, wave);
            if (ForecastBand != null && ForecastBand.gameObject.activeSelf)
                ForecastBand.color = new Color(bandColor.r, bandColor.g, bandColor.b, alpha);
            if (BlockForecast != null && BlockForecast.gameObject.activeSelf)
            {
                if (blockForecastGroup == null)
                    blockForecastGroup = BlockForecast.GetComponent<CanvasGroup>();
                if (blockForecastGroup != null)
                    blockForecastGroup.alpha = alpha;
            }
        }

        // Set only on a change, as a set rebuilds the text's mesh.
        private void ShowNumber(int hp)
        {
            hp = Mathf.Max(0, hp);
            if (Number == null || hp == shownHp)
                return;
            shownHp = hp;
            Number.text = HpText(hp);
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
            if (forecasting)
                Blink();
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
