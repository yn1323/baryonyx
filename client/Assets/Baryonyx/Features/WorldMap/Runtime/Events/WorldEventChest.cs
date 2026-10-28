using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// A hidden chest: the chest drops in and rocks now and then until it is tapped, then shakes,
    /// bursts open in a light and what it held flies up out of it into a row.
    /// </summary>
    public sealed class WorldEventChest : WorldEventPanel
    {
        public RawImage Chest;
        public Texture2D ClosedArt;
        public Texture2D OpenArt;
        public RawImage Burst;
        public RectTransform Gifts;

        private const float ShakeSeconds = 0.35f;
        private const float FlySeconds = 0.45f;
        private const float FlyGap = 0.12f;

        private Vector2[] homes;
        private float openedAt = -1f;

        public bool IsOpen => openedAt >= 0f;

        protected override void Restart()
        {
            if (homes == null)
            {
                homes = new Vector2[Gifts.childCount];
                for (int i = 0; i < homes.Length; i++)
                    homes[i] = ((RectTransform)Gifts.GetChild(i)).anchoredPosition;
            }
            openedAt = -1f;
            Chest.texture = ClosedArt;
            Chest.rectTransform.localRotation = Quaternion.identity;
            Burst.texture = WorldMapEventWindow.GlowTexture;
            Burst.color = EventMotion.WithAlpha(Burst.color, 0f);
            Gifts.gameObject.SetActive(false);
        }

        /// <summary>Opens the chest (a tap on the window).</summary>
        public override void Advance()
        {
            if (openedAt < 0f)
                openedAt = Clock;
        }

        protected override void Draw(float dt)
        {
            var chest = Chest.rectTransform;
            if (openedAt < 0f)
            {
                // 押されるのを待つ間、ときどき小さく揺れる。
                float p = Clock % 1.4f;
                float rock =
                    p < 0.45f ? Mathf.Sin(p / 0.45f * Mathf.PI * 3f) * (1f - p / 0.45f) : 0f;
                chest.localRotation = Quaternion.Euler(0f, 0f, rock * 5f);
                return;
            }
            float t = Clock - openedAt;
            if (t < ShakeSeconds)
            {
                chest.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Sin(t * 70f) * 6f * (t / ShakeSeconds)
                );
                return;
            }
            chest.localRotation = Quaternion.identity;
            Chest.texture = OpenArt;

            float burst = t - ShakeSeconds;
            Burst.rectTransform.localScale =
                Vector3.one
                * Mathf.Lerp(0.4f, 1f, EventMotion.Decelerate(Mathf.Clamp01(burst / 0.35f)));
            Burst.rectTransform.localRotation = Quaternion.Euler(0f, 0f, burst * 15f);
            Burst.color = EventMotion.WithAlpha(
                Burst.color,
                Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(burst / 0.6f))
            );

            // 中身は箱の口から弧を描いて飛び出し、並びの場所に止まる。
            Gifts.gameObject.SetActive(true);
            Vector2 mouth = Gifts.InverseTransformPoint(
                chest.position + chest.up * chest.rect.height * 0.5f * chest.lossyScale.y
            );
            bool landed = true;
            for (int i = 0; i < homes.Length; i++)
            {
                float x = Mathf.Clamp01((burst - 0.1f - i * FlyGap) / FlySeconds);
                landed &= x >= 1f;
                var gift = (RectTransform)Gifts.GetChild(i);
                float k = EventMotion.Decelerate(x);
                gift.anchoredPosition =
                    Vector2.LerpUnclamped(mouth, homes[i], k)
                    + new Vector2(0f, 160f * Mathf.Sin(Mathf.PI * k));
                gift.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, EventMotion.EaseOut(x));
                gift.gameObject.SetActive(x > 0f);
            }
            Done = landed;
        }
    }
}
