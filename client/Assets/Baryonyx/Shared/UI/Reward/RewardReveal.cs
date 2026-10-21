using System.Collections;
using Baryonyx.Inventory;
using Baryonyx.UI.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// One thing found (a beaten enemy's drop, a chest's content): its icon, name, quality,
    /// sparkle and what became of it.
    /// </summary>
    [System.Serializable]
    public sealed class RewardItem
    {
        /// <summary>The kind of thing above its name ("ACTボーナス", "武器"); empty shows none.</summary>
        public string Kind = "";

        public string Name = "";
        public Sprite Icon;
        public ItemQuality Quality = ItemQuality.C;

        /// <summary>False for a thing with no quality (an item): no letter, and a plain rim.</summary>
        public bool Graded = true;

        /// <summary>How it sparkles (<see cref="CardFoil.Style"/>), the kind's own.</summary>
        public int FoilStyle;

        /// <summary>The line under it: added to the belongings, its multiplier raised, or let go.</summary>
        public string Note = "";
    }

    /// <summary>
    /// The plate that shows what was found, over the middle of the screen, as the light of a
    /// drop or a chest bursts: it springs open from small, the quality's letter lands on it a
    /// moment later in the quality's colour, and its rim takes that colour too. The icon
    /// sparkles as it does in the belongings (A and S). With nothing found it shows only the
    /// line. It stays until hidden. RewardRevealAssets builds it.
    /// </summary>
    public sealed class RewardReveal : MonoBehaviour
    {
        // The plate springs open, then the letter lands on it.
        public const float OpenTime = 0.32f;
        public const float LetterDelay = 0.14f;
        public const float LetterTime = 0.22f;
        public const float HideTime = 0.15f;

        public RectTransform Plate;
        public CanvasGroup Group;

        [Tooltip("品質の色にする縁。")]
        public Graphic Rim;
        public RawImage Icon;
        public CardFoil Foil;
        public TMP_Text Kind;
        public TMP_Text Name;
        public TMP_Text Quality;
        public TMP_Text Note;

        [Tooltip("何も見つからなかったときに出す文。")]
        public TMP_Text Nothing;

        private Coroutine showing;

        /// <summary>True while the plate is shown (opening, or open).</summary>
        public bool Shown => Plate != null && Plate.gameObject.activeSelf;

        private void Awake()
        {
            if (Plate != null)
                Plate.gameObject.SetActive(false);
        }

        /// <summary>Opens the plate on <paramref name="drop"/>, or on nothing found when null.</summary>
        public void Show(RewardItem drop)
        {
            if (Plate == null)
                return;
            Fill(drop);
            Plate.gameObject.SetActive(true);
            if (showing != null)
                StopCoroutine(showing);
            showing = StartCoroutine(Open(drop != null));
        }

        /// <summary>Puts the contents of <paramref name="drop"/> on the plate as it rests open (for a still frame).</summary>
        public void ShowStill(RewardItem drop)
        {
            if (Plate == null)
                return;
            Fill(drop);
            Plate.gameObject.SetActive(true);
            Pose(1f, 1f);
        }

        /// <summary>Fades the plate out.</summary>
        public void Hide()
        {
            if (!Shown)
                return;
            if (showing != null)
                StopCoroutine(showing);
            showing = isActiveAndEnabled ? StartCoroutine(Close()) : null;
            if (showing == null)
                Plate.gameObject.SetActive(false);
        }

        /// <summary>Takes the plate away at once.</summary>
        public void HideNow()
        {
            if (showing != null)
                StopCoroutine(showing);
            showing = null;
            if (Plate != null)
                Plate.gameObject.SetActive(false);
        }

        private void Fill(RewardItem drop)
        {
            bool found = drop != null;
            bool graded = found && drop.Graded;
            SetShown(Icon, found);
            SetShown(Kind, found);
            SetShown(Name, found);
            SetShown(Quality, graded);
            SetShown(Note, found);
            SetShown(Nothing, !found);
            var color = graded ? QualityPalette.Of(drop.Quality) : UiPalette.TextMain;
            if (Rim != null)
                Rim.color = new Color(color.r, color.g, color.b, graded ? 1f : 0.4f);
            if (!found)
                return;
            if (Icon != null)
            {
                Icon.texture = drop.Icon != null ? drop.Icon.texture : null;
                Icon.uvRect = CardFoil.UvOf(drop.Icon);
                Icon.enabled = Icon.texture != null;
            }
            if (Foil != null)
                Foil.Show(drop.Icon, drop.Quality, drop.FoilStyle);
            UiText.Set(Kind, drop.Kind);
            UiText.Set(Name, drop.Name);
            UiText.Set(Note, drop.Note);
            if (Quality != null)
            {
                Quality.text = Inventory.Quality.Letter(drop.Quality);
                Quality.color = color;
            }
        }

        private static void SetShown(Component part, bool shown)
        {
            if (part != null && part.gameObject.activeSelf != shown)
                part.gameObject.SetActive(shown);
        }

        private IEnumerator Open(bool found)
        {
            float total = found ? LetterDelay + LetterTime : OpenTime;
            total = Mathf.Max(total, OpenTime);
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                Pose(t / OpenTime, (t - LetterDelay) / LetterTime);
                yield return null;
            }
            Pose(1f, 1f);
            showing = null;
        }

        private IEnumerator Close()
        {
            float from = Group != null ? Group.alpha : 1f;
            for (float t = 0f; t < HideTime; t += Time.unscaledDeltaTime)
            {
                if (Group != null)
                    Group.alpha = from * (1f - t / HideTime);
                yield return null;
            }
            Plate.gameObject.SetActive(false);
            showing = null;
        }

        /// <summary>
        /// The plate at <paramref name="open"/> of its spring (0 to 1) and the letter at
        /// <paramref name="letter"/> of its landing: the letter drops in from twice its size.
        /// </summary>
        private void Pose(float open, float letter)
        {
            float k = Mathf.Clamp01(open);
            Plate.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, SpringOut(k));
            if (Group != null)
                Group.alpha = Mathf.Clamp01(k * 3f);
            if (Quality != null)
            {
                float l = Mathf.Clamp01(letter);
                Quality.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, l * l);
                Quality.alpha = l;
            }
        }

        /// <summary>An ease-out that overshoots a little and settles back, like a spring.</summary>
        public static float SpringOut(float k)
        {
            const float overshoot = 1.7f;
            float t = Mathf.Clamp01(k) - 1f;
            return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
        }
    }
}
