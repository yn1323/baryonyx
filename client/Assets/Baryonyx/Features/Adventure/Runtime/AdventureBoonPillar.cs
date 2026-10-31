using System;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The light that rises over a member when a boon is taken (doc/features/screens/adventure.md,
    /// 戦闘後の強化): a soft halo and a bright narrow core shooting up from the member's feet, and
    /// thin lines rising inside them, tinted by the boon's step and stronger the higher the step.
    /// <see cref="Show"/> draws it at a time since the take, so the picker plays it frame by frame
    /// and a still can be taken at any moment.
    /// </summary>
    public sealed class AdventureBoonPillar : MonoBehaviour
    {
        // 柱が床から伸び切るまで（秒）と、光が引き切るまで（秒、決めてから）。
        public const float RiseSeconds = 0.3f;
        public const float Seconds = 1f;

        // 段階ごとの色（白は青白、金は金。虹は色相を回す）。
        private static readonly Color WhiteTint = new(0.72f, 0.86f, 1f, 1f);
        private static readonly Color GoldTint = new(1f, 0.84f, 0.45f, 1f);

        [Tooltip("広く柔らかい光（下から伸びる）と、細く明るい芯。")]
        public Image Halo;
        public Image Core;

        [Tooltip("柱の中を昇る細い線。段階が上がるほど多く使う。")]
        public Image[] Lines = Array.Empty<Image>();

        [Tooltip("伸び切った芯の高さと幅、広い光の高さと幅、広い光の中心の高さ（足元から。設計座標）。")]
        public float Height = 640f;
        public float CoreWidth = 70f;
        public float HaloHeight = 520f;
        public float HaloWidth = 300f;
        public float HaloCenter = 110f;

        [Tooltip("線の昇る速さ（設計座標/秒）と長さ。線ごとにこの幅でずらす。")]
        public Vector2 LineSpeed = new(520f, 820f);
        public Vector2 LineLength = new(70f, 150f);

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Draws the light <paramref name="seconds"/> after the take of a boon of <paramref name="rank"/>.</summary>
        public void Show(float seconds, AdventureBoonRank rank)
        {
            gameObject.SetActive(seconds >= 0f && seconds < Seconds);
            if (!gameObject.activeSelf)
                return;
            float strength = Strength(rank);
            // 立ち上がり：床から上へ速く伸び、伸び切る直前で減速する。
            float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(seconds / RiseSeconds), 3f);
            // 光の強さ：伸び切るころに最も明るく、そのあと余韻として引く。
            float fade = Mathf.Clamp01((seconds - RiseSeconds) / (Seconds - RiseSeconds));
            float glow = rise * (1f - fade) * (1f - fade);

            var tint = TintOf(rank, seconds, 0f, 0.2f);
            if (Halo != null)
            {
                // 広い光は仲間を包むように、体の中ほどを中心に上下へ広がる。
                Halo.rectTransform.anchoredPosition = new Vector2(
                    0f,
                    HaloCenter - HaloHeight * rise * 0.5f
                );
                Halo.rectTransform.sizeDelta = new Vector2(
                    HaloWidth * (0.8f + 0.2f * rise),
                    HaloHeight * rise
                );
                Halo.color = WithAlpha(tint, 0.4f * glow * strength);
            }
            if (Core != null)
            {
                // 芯は引くときに細くなり、白に近い色のまま消える。
                Core.rectTransform.sizeDelta = new Vector2(
                    CoreWidth * (1f - 0.6f * fade),
                    Height * rise * 1.05f
                );
                Core.color = WithAlpha(Color.Lerp(tint, Color.white, 0.6f), 0.8f * glow * strength);
            }

            int used = LineCount(rank);
            for (int i = 0; i < Lines.Length; i++)
            {
                var line = Lines[i];
                if (line == null)
                    continue;
                line.gameObject.SetActive(i < used);
                if (i >= used)
                    continue;
                // 線ごとに、出る時刻・横の位置・速さ・長さをずらし、一斉に昇らせない。
                float a = Mathf.Repeat(i * 0.618f + 0.13f, 1f);
                float b = Mathf.Repeat(i * 0.371f + 0.57f, 1f);
                float start = 0.05f + 0.45f * b;
                float life = 0.45f;
                float t = (seconds - start) / life;
                float speed = Mathf.Lerp(LineSpeed.x, LineSpeed.y, a);
                float length = Mathf.Lerp(LineLength.x, LineLength.y, 1f - a);
                var rect = line.rectTransform;
                rect.anchoredPosition = new Vector2(
                    (a - 0.5f) * HaloWidth * 0.8f,
                    Mathf.Max(0f, seconds - start) * speed
                );
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, length);
                float alpha = t <= 0f || t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI);
                line.color = WithAlpha(
                    TintOf(rank, seconds, i / (float)used, 0.45f),
                    0.9f * alpha * strength
                );
            }
        }

        // 段階ごとの強さ（白は控えめ、虹がいちばん強い）と、使う線の数。
        private static float Strength(AdventureBoonRank rank) =>
            rank switch
            {
                AdventureBoonRank.Rainbow => 1f,
                AdventureBoonRank.Gold => 0.85f,
                _ => 0.65f,
            };

        private int LineCount(AdventureBoonRank rank) =>
            Mathf.Min(
                Lines.Length,
                rank switch
                {
                    AdventureBoonRank.Rainbow => 8,
                    AdventureBoonRank.Gold => 6,
                    _ => 4,
                }
            );

        // 段階の色。虹は時間と線ごとの位置 offset で色相をずらし、saturation の淡さで塗る。
        private static Color TintOf(
            AdventureBoonRank rank,
            float seconds,
            float offset,
            float saturation
        ) =>
            rank switch
            {
                AdventureBoonRank.Rainbow => Color.HSVToRGB(
                    Mathf.Repeat(seconds * 0.6f + offset, 1f),
                    saturation,
                    1f
                ),
                AdventureBoonRank.Gold => GoldTint,
                _ => WhiteTint,
            };

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
