using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// A meeting: a party member and someone on the road talk in speech bubbles, a tap moving to
    /// the next line, and after the last line the newcomer joins the party: the scenery darkens,
    /// their picture slides in over a slowly turning light and a gold band says who joined.
    /// </summary>
    public sealed class WorldEventMeet : WorldEventPanel
    {
        public EventBubbles Talk = new();
        public CanvasGroup Join;
        public RectTransform Portrait;
        public RectTransform Band;
        public RawImage Burst;
        public TMP_Text Joined;

        private const float SlideFrom = 420f;
        private const float JoinSeconds = 0.85f;

        private Vector2 portraitHome;
        private bool homeKnown;
        private float joinedAt = -1f;

        /// <summary>The newcomer has joined (the talk is over).</summary>
        public bool Joining => joinedAt >= 0f;

        protected override void Restart()
        {
            if (!homeKnown)
            {
                portraitHome = Portrait.anchoredPosition;
                homeKnown = true;
            }
            Talk.Start(WorldEventSamples.MeetTalk, true);
            joinedAt = -1f;
            Join.alpha = 0f;
            Join.gameObject.SetActive(false);
            if (Burst != null)
                Burst.texture = WorldMapEventWindow.GlowTexture;
        }

        public override void Advance()
        {
            if (Joining)
                return;
            Talk.Advance();
            if (!Talk.Talking)
                joinedAt = Clock;
        }

        protected override void Draw(float dt)
        {
            Talk.Tick(dt, !Joining);
            if (!Joining)
                return;
            float t = Clock - joinedAt;
            Join.gameObject.SetActive(true);
            Join.alpha = Mathf.Clamp01(t / 0.25f);
            float slide = EventMotion.Decelerate(Mathf.Clamp01((t - 0.05f) / 0.5f));
            Portrait.anchoredPosition = portraitHome + new Vector2(SlideFrom * (1f - slide), 0f);
            Band.localScale = new Vector3(
                EventMotion.EaseOut(Mathf.Clamp01((t - 0.35f) / 0.3f)),
                1f,
                1f
            );
            Joined.alpha = Mathf.Clamp01((t - 0.55f) / 0.25f);
            Burst.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 12f);
            Burst.color = EventMotion.WithAlpha(
                Burst.color,
                Mathf.Clamp01(t / 0.4f) * (0.55f + 0.1f * Mathf.Sin(t * 2.5f))
            );
            Done = t >= JoinSeconds;
        }
    }
}
