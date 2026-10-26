using System.Collections;
using System.Collections.Generic;
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
        /// <summary>The kind of thing above its name ("武器", "アイテム"); empty shows none.</summary>
        public string Kind = "";

        public string Name = "";
        public Sprite Icon;
        public ItemQuality Quality = ItemQuality.C;

        /// <summary>False for a thing with no quality (an item): no letter, and a plain rim.</summary>
        public bool Graded = true;

        /// <summary>How it sparkles (<see cref="CardFoil.Style"/>), the kind's own.</summary>
        public int FoilStyle;

        /// <summary>The line under it: that it was added to the belongings.</summary>
        public string Note = "";
    }

    /// <summary>
    /// The plate that shows what was found, over the middle of the screen, as the light of a
    /// drop or a chest bursts: it springs open from small, the quality's letter lands on it a
    /// moment later in the quality's colour, and its rim takes that colour too. The icon
    /// sparkles as it does in the belongings (A and S). With nothing found it shows only the
    /// line. It stays until hidden. RewardRevealAssets builds it. Several things found at once
    /// (a chest's contents) open together, each on a copy of the plate, in one or two columns.
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

        // まとめて見せるとき、札の縦と横の間隔と、1枚ずつ開く間（札の大きさは RewardRevealAssets.PlateSize）。
        public const float RowGap = 20f;
        public const float ColumnGap = 24f;
        public const float Stagger = 0.08f;

        // 2つまでは縦に、3つからは2列に並べる。
        public const int OneColumnMost = 2;

        private Coroutine showing;

        // まとめて見せるときに足した札の写し（2つ目から）と、元の札の位置。
        private readonly List<Parts> copies = new();
        private Vector2 home;
        private bool homeKept;
        private int count = 1;

        /// <summary>True while the plate is shown (opening, or open).</summary>
        public bool Shown => Plate != null && Plate.gameObject.activeSelf;

        /// <summary>How many plates the last <see cref="Show(RewardItem)"/> or <see cref="ShowAll"/> opened.</summary>
        public int ShownCount => Shown ? count : 0;

        private void Awake()
        {
            if (Plate != null)
                Plate.gameObject.SetActive(false);
        }

        /// <summary>Opens the plate on <paramref name="drop"/>, or on nothing found when null.</summary>
        public void Show(RewardItem drop) =>
            ShowAll(drop != null ? new[] { drop } : System.Array.Empty<RewardItem>());

        /// <summary>
        /// Opens a plate for each of <paramref name="drops"/> together, the first (the best) at
        /// the plate's place; or the plate on nothing found when there are none.
        /// </summary>
        public void ShowAll(IReadOnlyList<RewardItem> drops)
        {
            if (Plate == null)
                return;
            KeepHome();
            count = Mathf.Max(1, drops?.Count ?? 0);
            var plates = PlatesFor(count);
            for (int i = 0; i < plates.Count; i++)
            {
                Fill(plates[i], drops != null && i < drops.Count ? drops[i] : null);
                plates[i].Plate.anchoredPosition = home + OffsetOf(i, count);
                plates[i].Plate.gameObject.SetActive(true);
            }
            if (showing != null)
                StopCoroutine(showing);
            showing = StartCoroutine(Open(plates, drops != null && drops.Count > 0));
        }

        /// <summary>Puts the contents of <paramref name="drop"/> on the plate as it rests open (for a still frame).</summary>
        public void ShowStill(RewardItem drop)
        {
            if (Plate == null)
                return;
            KeepHome();
            count = 1;
            var plates = PlatesFor(1);
            Fill(plates[0], drop);
            Plate.anchoredPosition = home;
            Plate.gameObject.SetActive(true);
            Pose(plates[0], 1f, 1f);
        }

        /// <summary>Fades the plates out.</summary>
        public void Hide()
        {
            if (!Shown)
                return;
            if (showing != null)
                StopCoroutine(showing);
            showing = isActiveAndEnabled ? StartCoroutine(Close()) : null;
            if (showing == null)
                HideAll();
        }

        /// <summary>Takes the plates away at once.</summary>
        public void HideNow()
        {
            if (showing != null)
                StopCoroutine(showing);
            showing = null;
            HideAll();
        }

        /// <summary>
        /// Where the plate <paramref name="index"/> of <paramref name="total"/> sits from the
        /// plate's own place: the set is centred there, read left to right, top to bottom.
        /// </summary>
        public Vector2 OffsetOf(int index, int total)
        {
            var size = Plate != null ? Plate.rect.size : Vector2.zero;
            int columns = total > OneColumnMost ? 2 : 1;
            int rows = (total + columns - 1) / columns;
            int column = index % columns;
            int row = index / columns;
            // 最後の行が1枚だけなら、真ん中に置く。
            bool alone = columns == 2 && row == rows - 1 && total % 2 == 1;
            float x = alone ? 0f : (column - (columns - 1) / 2f) * (size.x + ColumnGap);
            float y = ((rows - 1) / 2f - row) * (size.y + RowGap);
            return new Vector2(x, y);
        }

        private void KeepHome()
        {
            if (homeKept || Plate == null)
                return;
            home = Plate.anchoredPosition;
            homeKept = true;
        }

        // The original plate and as many copies as needed, the copies made once and kept.
        private List<Parts> PlatesFor(int total)
        {
            var plates = new List<Parts> { Own() };
            for (int i = 1; i < total; i++)
            {
                if (copies.Count < i)
                {
                    var copy = Instantiate(Plate.gameObject, Plate.parent);
                    copy.name = $"{Plate.name}{i + 1}";
                    copies.Add(Parts.Copy(Own(), Plate, (RectTransform)copy.transform));
                }
                plates.Add(copies[i - 1]);
            }
            for (int i = total - 1; i < copies.Count; i++)
                copies[i].Plate.gameObject.SetActive(false);
            return plates;
        }

        private Parts Own() =>
            new()
            {
                Plate = Plate,
                Group = Group,
                Rim = Rim,
                Icon = Icon,
                Foil = Foil,
                Kind = Kind,
                Name = Name,
                Quality = Quality,
                Note = Note,
                Nothing = Nothing,
            };

        private void HideAll()
        {
            if (Plate != null)
                Plate.gameObject.SetActive(false);
            foreach (var copy in copies)
                if (copy.Plate != null)
                    copy.Plate.gameObject.SetActive(false);
        }

        /// <summary>The parts of one plate (the original, or a copy of it).</summary>
        private sealed class Parts
        {
            public RectTransform Plate;
            public CanvasGroup Group;
            public Graphic Rim;
            public RawImage Icon;
            public CardFoil Foil;
            public TMP_Text Kind;
            public TMP_Text Name;
            public TMP_Text Quality;
            public TMP_Text Note;
            public TMP_Text Nothing;

            // The copy's parts at the same places under it as the original's under the plate.
            public static Parts Copy(Parts own, RectTransform plate, RectTransform copy) =>
                new()
                {
                    Plate = copy,
                    Group = copy.GetComponent<CanvasGroup>(),
                    Rim = Same(own.Rim, plate, copy),
                    Icon = Same(own.Icon, plate, copy),
                    Foil = Same(own.Foil, plate, copy),
                    Kind = Same(own.Kind, plate, copy),
                    Name = Same(own.Name, plate, copy),
                    Quality = Same(own.Quality, plate, copy),
                    Note = Same(own.Note, plate, copy),
                    Nothing = Same(own.Nothing, plate, copy),
                };

            private static T Same<T>(T part, Transform plate, Transform copy)
                where T : Component
            {
                if (part == null)
                    return null;
                if (part.transform == plate)
                    return copy.GetComponent<T>();
                string path = part.name;
                for (var t = part.transform.parent; t != null && t != plate; t = t.parent)
                    path = t.name + "/" + path;
                var found = copy.Find(path);
                return found != null ? found.GetComponent<T>() : null;
            }
        }

        private static void Fill(Parts plate, RewardItem drop)
        {
            bool found = drop != null;
            bool graded = found && drop.Graded;
            SetShown(plate.Icon, found);
            SetShown(plate.Kind, found);
            SetShown(plate.Name, found);
            SetShown(plate.Quality, graded);
            SetShown(plate.Note, found);
            SetShown(plate.Nothing, !found);
            var color = graded ? QualityPalette.Of(drop.Quality) : UiPalette.TextMain;
            if (plate.Rim != null)
                plate.Rim.color = new Color(color.r, color.g, color.b, graded ? 1f : 0.4f);
            if (!found)
                return;
            if (plate.Icon != null)
            {
                plate.Icon.texture = drop.Icon != null ? drop.Icon.texture : null;
                plate.Icon.uvRect = CardFoil.UvOf(drop.Icon);
                plate.Icon.enabled = plate.Icon.texture != null;
            }
            if (plate.Foil != null)
                plate.Foil.Show(drop.Icon, drop.Quality, drop.FoilStyle);
            UiText.Set(plate.Kind, drop.Kind);
            UiText.Set(plate.Name, drop.Name);
            UiText.Set(plate.Note, drop.Note);
            if (plate.Quality != null)
            {
                plate.Quality.text = Inventory.Quality.Letter(drop.Quality);
                plate.Quality.color = color;
            }
        }

        private static void SetShown(Component part, bool shown)
        {
            if (part != null && part.gameObject.activeSelf != shown)
                part.gameObject.SetActive(shown);
        }

        private IEnumerator Open(List<Parts> plates, bool found)
        {
            float each = found ? LetterDelay + LetterTime : OpenTime;
            each = Mathf.Max(each, OpenTime);
            float total = each + Stagger * (plates.Count - 1);
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < plates.Count; i++)
                {
                    float own = t - Stagger * i;
                    Pose(plates[i], own / OpenTime, (own - LetterDelay) / LetterTime);
                }
                yield return null;
            }
            foreach (var plate in plates)
                Pose(plate, 1f, 1f);
            showing = null;
        }

        private IEnumerator Close()
        {
            var plates = new List<Parts> { Own() };
            foreach (var copy in copies)
                if (copy.Plate != null && copy.Plate.gameObject.activeSelf)
                    plates.Add(copy);
            var from = new float[plates.Count];
            for (int i = 0; i < plates.Count; i++)
                from[i] = plates[i].Group != null ? plates[i].Group.alpha : 1f;
            for (float t = 0f; t < HideTime; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < plates.Count; i++)
                    if (plates[i].Group != null)
                        plates[i].Group.alpha = from[i] * (1f - t / HideTime);
                yield return null;
            }
            HideAll();
            showing = null;
        }

        /// <summary>
        /// A plate at <paramref name="open"/> of its spring (0 to 1) and the letter at
        /// <paramref name="letter"/> of its landing: the letter drops in from twice its size.
        /// </summary>
        private static void Pose(Parts plate, float open, float letter)
        {
            float k = Mathf.Clamp01(open);
            plate.Plate.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, SpringOut(k));
            if (plate.Group != null)
                plate.Group.alpha = Mathf.Clamp01(k * 3f);
            if (plate.Quality != null)
            {
                float l = Mathf.Clamp01(letter);
                plate.Quality.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, l * l);
                plate.Quality.alpha = l;
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
