using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// Lights a number up to tell it has just gone up (the level and stats after a level-up;
    /// doc/features/screens/adventurers.md#レベルアップしたときの演出): twice in a row, a soft
    /// pale-gold glow behind the text swells out and fades with a tighter, whiter one at its
    /// middle, and the text brightens a little toward white without changing its colour's hue. The glows are
    /// made in code (<see cref="SoftLight"/>) next to the text, outside its parent's layout.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TextGlow : MonoBehaviour
    {
        // ふくらむ回数と、次がふくらみ始めるまでの間（秒）。
        public const int Pulses = 2;
        public const float PulseGap = 0.36f;

        // 1回ごとに明るくなりきるまでと、消えるまで（秒）。全体は2回目が消えるまで。
        public const float RiseSeconds = 0.1f;
        public const float FallSeconds = 0.28f;
        public const float Seconds = PulseGap * (Pulses - 1) + RiseSeconds + FallSeconds;

        // 1回ごとに光が広がる大きさ（出始めと消える時の倍率）。芯の光はこの半分だけ広がる。
        private const float SwellFrom = 0.8f;
        private const float SwellTo = 1.25f;

        // いちばん明るいときの、外の光・芯の光の濃さと、文字を白へ寄せる割合。
        private const float HaloAlpha = 0.85f;
        private const float CoreAlpha = 0.6f;
        private const float TextLift = 0.6f;

        // 光の大きさ（文字の範囲に対する倍率と、足す余白）。
        private const float HaloWidth = 1.6f;
        private const float HaloHeight = 2.1f;
        private const float CoreWidth = 1.15f;
        private const float CoreHeight = 1.3f;
        private const float Padding = 24f;

        private static readonly Color HaloColor = new(1f, 0.88f, 0.6f);
        private static readonly Color CoreColor = new(1f, 0.97f, 0.88f);

        private TMP_Text text;
        private RawImage halo;
        private RawImage core;
        private Color resting;
        private float elapsed = float.PositiveInfinity;

        public bool Glowing => elapsed < Seconds;

        /// <summary>Lights <paramref name="text"/> up for a beat. Does nothing while stopped or off screen.</summary>
        public static void Pulse(TMP_Text text)
        {
            if (!Application.isPlaying || text == null || !text.isActiveAndEnabled)
                return;
            if (!text.TryGetComponent(out TextGlow glow))
                glow = text.gameObject.AddComponent<TextGlow>();
            glow.Begin(text);
        }

        private void Begin(TMP_Text target)
        {
            Settle();
            text = target;
            resting = text.color;
            halo = MakeGlow("Halo");
            core = MakeGlow("Core");
            elapsed = 0f;
            enabled = true;
            Fit(SwellFrom);
            Show(0f);
        }

        private void Update()
        {
            if (!Glowing)
            {
                enabled = false;
                return;
            }
            // ゲームの時間で進める（案内人の画面は時間を止めない。録画でも実際の速さで写る）。
            elapsed += Time.deltaTime;
            if (!Glowing)
            {
                Settle();
                return;
            }
            // いまいちばん明るい回の明るさと、その回の進み具合（広がる大きさに使う）。
            float strength = 0f;
            float swell = 0f;
            for (int i = 0; i < Pulses; i++)
            {
                float t = elapsed - i * PulseGap;
                if (t < 0f || t > RiseSeconds + FallSeconds)
                    continue;
                float s =
                    t < RiseSeconds
                        ? Mathf.SmoothStep(0f, 1f, t / RiseSeconds)
                        : 1f - Mathf.SmoothStep(0f, 1f, (t - RiseSeconds) / FallSeconds);
                if (s < strength)
                    continue;
                strength = s;
                swell = t / (RiseSeconds + FallSeconds);
            }
            Fit(Mathf.Lerp(SwellFrom, SwellTo, Mathf.Sin(swell * Mathf.PI / 2f)));
            Show(strength);
        }

        private void OnDisable() => Settle();

        private RawImage MakeGlow(string name)
        {
            var rect = new GameObject(
                "TextGlow" + name,
                typeof(RectTransform)
            ).GetComponent<RectTransform>();
            rect.SetParent(text.transform.parent, false);
            // 文字の後ろに描く。親の並べ方には入れない。
            rect.SetSiblingIndex(text.transform.GetSiblingIndex());
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = SoftLight.Glow;
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }

        // 光を、いま書かれている文字の範囲の中心に合わせ、swell 倍に広げて置く。
        private void Fit(float swell)
        {
            text.ForceMeshUpdate();
            var bounds = text.textBounds;
            var parent = (RectTransform)text.transform.parent;
            var center = parent.InverseTransformPoint(text.transform.TransformPoint(bounds.center));
            var position = (Vector2)center - parent.rect.center;
            float coreSwell = 1f + (swell - 1f) / 2f;
            Place(halo, position, bounds.size, HaloWidth * swell, HaloHeight * swell);
            Place(core, position, bounds.size, CoreWidth * coreSwell, CoreHeight * coreSwell);
        }

        private static void Place(
            RawImage image,
            Vector2 position,
            Vector2 size,
            float width,
            float height
        )
        {
            var rect = image.rectTransform;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size.x * width + Padding, size.y * height + Padding);
        }

        private void Show(float strength)
        {
            halo.color = new Color(HaloColor.r, HaloColor.g, HaloColor.b, HaloAlpha * strength);
            core.color = new Color(CoreColor.r, CoreColor.g, CoreColor.b, CoreAlpha * strength);
            var lifted = Color.Lerp(resting, Color.white, TextLift * strength);
            text.color = new Color(lifted.r, lifted.g, lifted.b, resting.a);
        }

        // 光り終えた姿（光なし、元の文字の色）に戻す。光らせていなければ何もしない。
        private void Settle()
        {
            if (halo == null && core == null)
                return;
            elapsed = float.PositiveInfinity;
            if (halo != null)
                Destroy(halo.gameObject);
            if (core != null)
                Destroy(core.gameObject);
            halo = null;
            core = null;
            if (text != null)
                text.color = resting;
        }
    }
}
