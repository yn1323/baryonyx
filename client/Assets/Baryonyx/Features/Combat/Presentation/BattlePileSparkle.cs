using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The twinkle on the deck's counter while the deck holds a card that awakens when used
    /// (doc/features/screens/battle.md#戦闘画面の見た目モック): every so often a star flashes on
    /// its upper right and, a moment later, a smaller one on its lower left, turning as they
    /// grow and fade, as the weakness's glint does. Made on the counter by <see cref="Make"/>,
    /// hidden until lit.
    /// </summary>
    public sealed class BattlePileSparkle : MonoBehaviour
    {
        /// <summary>How often the stars flash (s).</summary>
        public const float Period = 2.4f;

        /// <summary>How long a star takes to grow and fade (s).</summary>
        public const float Flash = 0.55f;

        // The smaller star comes this much after the larger one (s).
        private const float SmallDelay = 0.18f;

        private const float BigStar = 88f;
        private const float SmallStar = 48f;

        private static readonly Color Gold = new(1f, 0.84f, 0.42f);

        [SerializeField]
        private RectTransform counter;

        [SerializeField]
        private BattleVfxImage big;

        [SerializeField]
        private BattleVfxImage small;

        private float age;
        private bool lit;

        /// <summary>True while the stars flash.</summary>
        public bool Lit => lit;

        /// <summary>
        /// The twinkle on <paramref name="counter"/>, over it and hidden until lit, its stars
        /// drawn with the effects' material <paramref name="sparkle"/>. Null when it is missing.
        /// </summary>
        public static BattlePileSparkle Make(RectTransform counter, Material sparkle)
        {
            if (counter == null || sparkle == null)
                return null;
            BattleVfxImage.EnableShapeChannel(counter);
            var root = new GameObject("Sparkle", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(counter, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            root.SetAsLastSibling();
            var twinkle = root.gameObject.AddComponent<BattlePileSparkle>();
            twinkle.counter = counter;
            twinkle.big = Star("Big", root, sparkle);
            twinkle.small = Star("Small", root, sparkle);
            root.gameObject.SetActive(false);
            return twinkle;
        }

        /// <summary>Starts the stars flashing (the first at once) or stops them.</summary>
        public void SetLit(bool value)
        {
            if (lit == value)
                return;
            lit = value;
            age = 0f;
            gameObject.SetActive(value);
            if (value)
                Paint();
        }

        /// <summary>The stars as they are <paramref name="seconds"/> after they started (the look while the editor is stopped).</summary>
        public void ShowAt(float seconds)
        {
            lit = true;
            age = seconds;
            gameObject.SetActive(true);
            Paint();
        }

        private void Update()
        {
            if (!lit)
                return;
            age += Time.unscaledDeltaTime;
            Paint();
        }

        private void Paint()
        {
            if (counter == null)
                return;
            var half = counter.rect.size * 0.5f;
            Twinkle(big, age, new Vector2(half.x - 22f, half.y - 6f), BigStar);
            Twinkle(small, age - SmallDelay, new Vector2(-half.x + 30f, -half.y + 10f), SmallStar);
        }

        // A star that grows quickly, turning, and fades more slowly, once a period.
        private static void Twinkle(BattleVfxImage star, float time, Vector2 at, float size)
        {
            if (star == null)
                return;
            float t = Mathf.Repeat(time, Period);
            float k = Mathf.Clamp01(t / Flash);
            // Up in the first quarter of the flash, down over the rest.
            float swell = k >= 1f ? 0f : k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f;
            swell *= swell * (3f - 2f * swell);
            var rect = star.rectTransform;
            rect.anchoredPosition = at;
            rect.sizeDelta = Vector2.one * size * (0.2f + 0.8f * swell);
            rect.localEulerAngles = new Vector3(0f, 0f, t * 90f);
            var color = Color.Lerp(Gold, Color.white, swell);
            color.a = swell;
            star.color = color;
            star.SetShape(0f, 0.5f, 0f, 0.8f + 1.8f * swell);
        }

        private static BattleVfxImage Star(string name, RectTransform parent, Material material)
        {
            var part = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            part.SetParent(parent, false);
            part.anchorMin = part.anchorMax = part.pivot = new Vector2(0.5f, 0.5f);
            var image = part.gameObject.AddComponent<BattleVfxImage>();
            image.material = material;
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }
    }
}
