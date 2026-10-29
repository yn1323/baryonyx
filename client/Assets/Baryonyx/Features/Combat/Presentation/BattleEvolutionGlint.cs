using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The light on a card of the hand that evolves when used (doc/features/skill-evolution.md#覚醒の予兆):
    /// the same haze of light as behind the stage picked in <see cref="BattleEvolutionChoice"/>
    /// (Battle Card Aura: an oval of haze, platinum near the card and amber further out, coming
    /// apart like mist, with no lines), breathing behind it, and the star of an idea twinkling
    /// on its upper corner, as the omen of an evolution shows (2026-10-29: the soft light and
    /// the rim of flowing beams it had were changed for the haze). It flares as it comes on,
    /// when the card turns face up in the hand. Made under the card by <see cref="Make"/>: the
    /// haze as its first child, behind the card, the star as its last, over it.
    /// </summary>
    public sealed class BattleEvolutionGlint : MonoBehaviour
    {
        /// <summary>How long the flare lasts as the light comes on (s).</summary>
        public const float FlareSeconds = 0.5f;

        private static readonly Color Gold = new(1f, 0.84f, 0.42f);

        private const float StarSize = 64f;

        // The haze's board around the card, as a multiple of the card's size, so the oval (about
        // twice the card) fades out inside it; and the haze's own sizes at the choice's card
        // (half of it 257 px wide), scaled to the card of the hand.
        private static readonly Vector2 HazeBoard = new(2.2f, 2.5f);

        // In the hand the cards overlap, so mostly the haze over the card's top shows: there it
        // is taller and brighter than behind the stage picked.
        private static readonly Vector4 HazeRadius = new(1.6f, 1.75f, 0f, 0f);
        private const float HazeIntensity = 2.2f;
        private const float ChoiceCardHalf = 257f;
        private const float HazeScale = 230f;
        private const float HazeRimReach = 26f;

        [SerializeField]
        private RectTransform card;

        [SerializeField]
        private RawImage haze;

        [SerializeField]
        private BattleVfxImage star;

        private float age;
        private bool lit;
        private Vector2 sized;

        // The haze's own material, made by Make (a copy of the card has none of its own).
        private Material made;

        /// <summary>True while the light is on.</summary>
        public bool Lit => lit;

        /// <summary>
        /// The light under <paramref name="card"/>, hidden until lit: the haze drawn with its own
        /// copy of <paramref name="hazeMaterial"/> (Battle Card Aura) and the star with the
        /// effects' material <paramref name="sparkle"/>. Null when a material is missing.
        /// </summary>
        public static BattleEvolutionGlint Make(
            RectTransform card,
            Material hazeMaterial,
            Material sparkle
        )
        {
            if (card == null || hazeMaterial == null || sparkle == null)
                return null;
            BattleVfxImage.EnableShapeChannel(card);
            var back = Part("EvolutionGlint", card);
            back.SetAsFirstSibling();
            var glint = back.gameObject.AddComponent<BattleEvolutionGlint>();
            glint.card = card;
            var hazePart = Part("Haze", back);
            glint.haze = hazePart.gameObject.AddComponent<RawImage>();
            glint.haze.raycastTarget = false;
            glint.haze.color = Color.clear;
            glint.made = new Material(hazeMaterial)
            {
                name = hazeMaterial.name + " (Hand)",
                hideFlags = HideFlags.DontSave,
            };
            glint.haze.material = glint.made;
            glint.haze.material.SetTexture("_Noise", BattleCardAuraNoise.Texture);
            glint.haze.material.SetVector("_GlowRadius", HazeRadius);
            glint.haze.material.SetFloat("_GlowIntensity", HazeIntensity);
            glint.haze.material.SetVector(
                "_Seed",
                new Vector4(Random.value * 40f, 3.1f, 1.7f, 0f)
            );
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

        private void OnDestroy()
        {
            if (made == null)
                return;
            if (Application.isPlaying)
                Destroy(made);
            else
                DestroyImmediate(made);
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

        // The haze, breathing by itself, and a twinkling star, both brighter and larger for a
        // moment as the light comes on.
        private void Paint()
        {
            if (card == null)
                return;
            var size = card.rect.size;
            float flare = 1f - Mathf.Clamp01(age / FlareSeconds);
            flare *= flare;

            if (haze != null)
            {
                var rect = haze.rectTransform;
                // The board and the card's shape in it follow the card's size.
                if (size != sized)
                {
                    sized = size;
                    rect.sizeDelta = Vector2.Scale(size, HazeBoard);
                    var material = haze.material;
                    float k = size.x * 0.5f / ChoiceCardHalf;
                    material.SetVector("_Size", rect.sizeDelta);
                    material.SetVector("_CardHalf", size * 0.5f);
                    material.SetFloat("_GlowScale", HazeScale * k);
                    material.SetFloat("_RimReach", HazeRimReach * k);
                }
                rect.localScale = Vector3.one * (1f + 0.12f * flare);
                haze.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.85f, 1f, flare));
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
