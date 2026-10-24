using System;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The light on a card of the hand that evolves when used (doc/features/skill-evolution.md#覚醒の予兆):
    /// a soft gold light behind it that breathes, a rim of flowing light just outside its edges,
    /// and the star of an idea twinkling on its upper corner, as the omen of an evolution shows.
    /// It flares as it comes on, when the card turns face up in the hand. Made under the card by
    /// <see cref="Make"/>: the light and the rim as its first child, behind the card, the star
    /// as its last, over it.
    /// </summary>
    public sealed class BattleEvolutionGlint : MonoBehaviour
    {
        /// <summary>How long the flare lasts as the light comes on (s).</summary>
        public const float FlareSeconds = 0.5f;

        private static readonly Color Gold = new(1f, 0.84f, 0.42f);
        private static readonly Color RimColor = new(1f, 0.86f, 0.46f, 1f);
        private static readonly Color GlowColor = new(1f, 0.8f, 0.4f, 0.5f);

        // The rim's beams: how thick, how far outside the edge their core runs and how far past
        // the corners they reach, for the card at its own size.
        private const float RimWidth = 22f;
        private const float RimOut = 3f;
        private const float RimReach = 12f;
        private const float StarSize = 64f;

        [SerializeField]
        private RectTransform card;

        [SerializeField]
        private BattleVfxImage glow;

        [SerializeField]
        private BattleVfxImage[] rims = Array.Empty<BattleVfxImage>();

        [SerializeField]
        private BattleVfxImage star;

        private float age;
        private bool lit;

        /// <summary>True while the light is on.</summary>
        public bool Lit => lit;

        /// <summary>
        /// The light under <paramref name="card"/>, hidden until lit, drawn with the effects'
        /// materials: <paramref name="glowMaterial"/> the soft light, <paramref name="beam"/> the
        /// rim and <paramref name="sparkle"/> the star. Null when a material is missing.
        /// </summary>
        public static BattleEvolutionGlint Make(
            RectTransform card,
            Material glowMaterial,
            Material beam,
            Material sparkle
        )
        {
            if (card == null || glowMaterial == null || beam == null || sparkle == null)
                return null;
            BattleVfxImage.EnableShapeChannel(card);
            var back = Part("EvolutionGlint", card);
            back.SetAsFirstSibling();
            var glint = back.gameObject.AddComponent<BattleEvolutionGlint>();
            glint.card = card;
            glint.glow = Shape("Glow", back, glowMaterial);
            glint.rims = new BattleVfxImage[4];
            for (int i = 0; i < 4; i++)
            {
                glint.rims[i] = Shape("Rim" + i, back, beam);
                glint.rims[i].rectTransform.pivot = new Vector2(0f, 0.5f);
            }
            var front = Part("EvolutionGlintStar", card);
            front.SetAsLastSibling();
            glint.star = Shape("Star", front, sparkle);
            back.gameObject.SetActive(false);
            front.gameObject.SetActive(false);
            return glint;
        }

        /// <summary>
        /// Takes the light out of a copy of a card (the evolving card, a card that crumbles), so
        /// only the card itself is left.
        /// </summary>
        public static void StripFrom(GameObject copy)
        {
            if (copy == null)
                return;
            // At once, so the copy shows no light even for the frame it is made in.
            foreach (var glint in copy.GetComponentsInChildren<BattleEvolutionGlint>(true))
            {
                if (glint.star != null)
                    DestroyImmediate(glint.star.transform.parent.gameObject);
                DestroyImmediate(glint.gameObject);
            }
        }

        /// <summary>Turns the light on (flaring) or off.</summary>
        public void SetLit(bool value)
        {
            if (lit == value)
                return;
            lit = value;
            age = 0f;
            Show(value);
            if (value)
                Paint();
        }

        /// <summary>The light on, as it is <paramref name="seconds"/> after it came on (the look while the editor is stopped).</summary>
        public void ShowAt(float seconds)
        {
            lit = true;
            age = seconds;
            Show(true);
            Paint();
        }

        private void Update()
        {
            if (!lit)
                return;
            age += Time.unscaledDeltaTime;
            Paint();
        }

        private void Show(bool shown)
        {
            if (gameObject.activeSelf != shown)
                gameObject.SetActive(shown);
            if (star != null && star.transform.parent.gameObject.activeSelf != shown)
                star.transform.parent.gameObject.SetActive(shown);
        }

        // A breathing light, a flowing rim and a twinkling star, all brighter and larger for a
        // moment as the light comes on.
        private void Paint()
        {
            if (card == null)
                return;
            var size = card.rect.size;
            var half = size * 0.5f;
            float flare = 1f - Mathf.Clamp01(age / FlareSeconds);
            flare *= flare;
            float breath = 0.5f + 0.5f * Mathf.Sin(age * 2.4f);

            if (glow != null)
            {
                glow.rectTransform.sizeDelta = new Vector2(
                    size.x * (1.45f + 0.35f * flare),
                    size.y * (1.25f + 0.3f * flare)
                );
                var tint = GlowColor;
                tint.a = Mathf.Lerp(0.35f + 0.2f * breath, 1f, flare);
                glow.color = tint;
                glow.SetShape(0f, 0f, 0f, 1f + flare);
            }

            for (int i = 0; i < rims.Length && i < 4; i++)
            {
                var rim = rims[i];
                if (rim == null)
                    continue;
                bool across = i < 2;
                float side = i % 2 == 0 ? 1f : -1f;
                var rect = rim.rectTransform;
                // Top and bottom run left to right; the sides run up.
                rect.anchoredPosition = across
                    ? new Vector2(-half.x - RimReach, side * (half.y + RimOut))
                    : new Vector2(side * (half.x + RimOut), -half.y - RimReach);
                rect.sizeDelta = new Vector2(
                    (across ? half.x : half.y) * 2f + RimReach * 2f,
                    RimWidth * (1f + flare)
                );
                rect.localEulerAngles = new Vector3(0f, 0f, across ? 0f : 90f);
                rim.color = RimColor;
                rim.SetShape(
                    0.21f * i,
                    0.28f + (age * 0.25f + i * 0.17f) % 0.7f,
                    0f,
                    1.1f + 0.3f * breath + flare
                );
            }

            if (star != null)
            {
                var rect = star.rectTransform;
                // Where the omen's star glints: on the upper right corner.
                rect.anchoredPosition = new Vector2(size.x * 0.36f, size.y * 0.4f);
                float twinkle = 0.5f + 0.5f * Mathf.Sin(age * 3.1f + 1f);
                rect.sizeDelta = Vector2.one * StarSize * (0.8f + 0.3f * twinkle + 1.2f * flare);
                rect.localEulerAngles = new Vector3(0f, 0f, age * 20f);
                star.color = Color.Lerp(Gold, Color.white, 0.4f + 0.6f * flare);
                star.SetShape(0f, 0.5f, 0f, 1.2f + 0.4f * twinkle + flare);
            }
        }

        private static RectTransform Part(string name, RectTransform parent)
        {
            var part = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            part.SetParent(parent, false);
            part.anchorMin = part.anchorMax = part.pivot = new Vector2(0.5f, 0.5f);
            part.anchoredPosition = Vector2.zero;
            part.sizeDelta = parent.rect.size;
            return part;
        }

        private static BattleVfxImage Shape(string name, RectTransform parent, Material material)
        {
            var part = Part(name, parent);
            var image = part.gameObject.AddComponent<BattleVfxImage>();
            image.material = material;
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }
    }
}
