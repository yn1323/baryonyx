using System;
using System.Collections.Generic;
using Baryonyx.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>The piles whose cards <see cref="BattlePileList"/> shows.</summary>
    public enum BattlePile
    {
        Draw,
        Discard,
        Exhaust,
    }

    /// <summary>
    /// The cards of a pile (doc/features/screens/battle.md#山札と捨て札の一覧): a tap on the deck,
    /// the discard pile or the cards out of the battle lays their cards out over the darkened
    /// battle, six to a row under the pile's picture, its name and how many it holds, the whole
    /// block in the middle of the screen. When the rows run past the screen, the header stays at
    /// the top and the rows scroll under it. A tap on a card grows it out of its place to fill
    /// the middle of the screen, and the next tap anywhere puts it back; a tap anywhere else but
    /// a drag closes the list, as the cross does. A card that awakens when used shows the light
    /// it has in the hand; its haze lies behind all the cards and fades out under the header
    /// rather than being cut where the rows are. Nothing behind it is pressed while open.
    /// </summary>
    public sealed class BattlePileList : MonoBehaviour, IPointerDownHandler
    {
        /// <summary>A card of the pile, as its card shows it.</summary>
        public struct Entry
        {
            public string Name;
            public string Owner;
            public string Kind;
            public string Description;
            public Texture Art;
            public Texture Frame;
            public Texture Element;
            public int Cost;
            public ItemQuality Quality;
            public int FoilStyle;

            /// <summary>Awakens when used: it shows the light it has in the hand.</summary>
            public bool Omen;
        }

        public static readonly string[] Titles = { "山札", "捨て札", "消滅" };

        [Tooltip("一覧の全体。開いている間だけ出し、後ろの戦闘を暗くして押せなくする。")]
        public GameObject Panel;

        [Tooltip("どこを押しても閉じるボタン（一覧の全体）。")]
        public Button Backdrop;

        public Button CloseButton;

        [Tooltip("カードが縦に流れる範囲。見出しの下で切る。カードが画面に収まるときは動かない。")]
        public ScrollRect Scroll;

        [Tooltip("カードを並べる中身。上端から下へ並べる。")]
        public RectTransform Content;

        [Tooltip("見出しの行（札置き場の絵・名前と枚数・閉じる×）。カードの上に置き、流れない。")]
        public RectTransform Header;

        [Tooltip("見出しの札置き場の絵（BattlePileの順）。開いた札置き場の絵だけを出す。")]
        public GameObject[] Icons = Array.Empty<GameObject>();

        public TMP_Text Title;

        [Tooltip("並べるカードの元。止めておき、複製して並べる。")]
        public BattleInspectCardView Template;

        [Min(1)]
        public int Columns = 6;

        public float CardScale = 1.05f;

        [Tooltip("隣のカードとの間（横・縦）。")]
        public Vector2 Gap = new(34f, 40f);

        [Tooltip("見出しの行の下からカードまでの間。")]
        public float HeaderGap = 28f;

        [Tooltip("中身の上下の余白。")]
        public float Margin = 40f;

        [Tooltip("大きく見せている間の幕。一覧の上に重ね、押せない。")]
        public Image ZoomVeil;

        [Tooltip("押したカードを画面の中央で大きく見せるカード。押せない。")]
        public BattleInspectCardView ZoomCard;

        [Tooltip("大きく見せている間の幕の濃さ。")]
        [Range(0f, 1f)]
        public float ZoomVeilAlpha = 0.7f;

        [Tooltip("大きく見せるカードが、安全領域の高さと幅に占める割合。")]
        [Range(0.2f, 1f)]
        public float ZoomFill = 0.9f;

        [Tooltip("元の場所から中央まで大きくなる時間（秒）。")]
        public float ZoomInTime = 0.22f;

        [Tooltip("もう一度押してから元の場所へ戻る時間（秒）。")]
        public float ZoomOutTime = 0.16f;

        [Tooltip("覚醒するカードの霞が、カードの流れる範囲の上下の端で消えていく幅（px）。")]
        public int GlintFade = 90;

        /// <summary>
        /// Makes the light of a card that awakens when used (<see cref="BattleEvolutionGlint"/>)
        /// under the card given, or null; the battle gives it.
        /// </summary>
        [NonSerialized]
        public Func<RectTransform, BattleEvolutionGlint> MakeGlint;

        private static readonly string CountHex = "FACC5C";

        private readonly List<BattleInspectCardView> cards = new();
        private readonly List<Entry> entries = new();
        private readonly Dictionary<BattleInspectCardView, BattleEvolutionGlint> glints = new();

        // The haze of each lit card of the list, moved out of the card into a layer behind all
        // the cards that follows the room the rows scroll in, softly cut at its edges.
        private readonly Dictionary<BattleInspectCardView, RectTransform> hazes = new();
        private RectTransform hazeLayer;

        // Where the rows' room begins under the header, from the top of the safe area: the haze
        // fades out there whether the rows scroll or not.
        private float rowsTop;
        private BattlePile pile;
        private int shown;

        // Where the tap that is being made went down, until the tap is taken.
        private Vector2? pressPoint;

        // The card shown large: it moves from one pose to another over the time given.
        private int zoomed = -1;
        private bool zoomingIn;
        private Pose from;
        private Pose to;
        private float zoomTime;
        private float zoomDuration;
        private bool scrolled;

        private struct Pose
        {
            public Vector2 Position;
            public float Scale;
            public float Veil;
        }

        /// <summary>True while the list is open.</summary>
        public bool IsOpen => Panel != null && Panel.activeSelf;

        /// <summary>The pile shown while open.</summary>
        public BattlePile Pile => pile;

        /// <summary>How many cards are laid out.</summary>
        public int Shown => shown;

        /// <summary>The cards laid out now, in order (the first <see cref="Shown"/> are used).</summary>
        public IReadOnlyList<BattleInspectCardView> Cards => cards;

        /// <summary>The card shown large (its place in the list), or -1.</summary>
        public int Zoomed => zoomed;

        /// <summary>True while the card shown large is growing or still, not on its way back.</summary>
        public bool ZoomShown => zoomed >= 0 && zoomingIn;

        /// <summary>Whether the <paramref name="index"/>th card laid out shows the light of a card that awakens when used.</summary>
        public bool Glints(int index) => index >= 0 && index < shown && Lit(cards[index]);

        /// <summary>Whether the card shown large shows that light.</summary>
        public bool ZoomGlints => zoomed >= 0 && Lit(ZoomCard);

        private void Awake()
        {
            if (Backdrop != null)
                Backdrop.onClick.AddListener(Tapped);
            if (CloseButton != null)
                CloseButton.onClick.AddListener(Close);
            // The list is built hidden, so this runs as it first opens: it must not hide it again.
            if (Template != null)
                Template.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            pressPoint = null;
            EndZoom();
        }

        private void LateUpdate() => PlaceHazes();

        private void Update()
        {
            if (zoomed < 0)
                return;
            zoomTime += Time.unscaledDeltaTime;
            float k = zoomDuration > 0f ? Mathf.Clamp01(zoomTime / zoomDuration) : 1f;
            Place(k);
            if (k >= 1f && !zoomingIn)
                EndZoom();
        }

        /// <summary>Keeps where the tap went down, to tell a tap on a card from one elsewhere.</summary>
        public void OnPointerDown(PointerEventData eventData) => pressPoint = eventData.position;

        /// <summary>Shows the <paramref name="index"/>th card large at once, as when it has grown.</summary>
        public void ShowZoomed(int index)
        {
            ZoomIn(index);
            zoomTime = zoomDuration;
            Place(1f);
            PlaceHazes();
        }

        /// <summary>Opens the list on <paramref name="cardsOf"/>, the cards of <paramref name="which"/> in order.</summary>
        public void Open(BattlePile which, IReadOnlyList<Entry> cardsOf)
        {
            if (Panel == null || Template == null)
                return;
            pile = which;
            Panel.SetActive(true);
            pressPoint = null;
            EndZoom();
            for (int i = 0; i < Icons.Length; i++)
                if (Icons[i] != null)
                    Icons[i].SetActive(i == (int)which);
            if (Title != null)
                Title.text = $"{Titles[(int)which]}　<color=#{CountHex}>{cardsOf.Count}</color>";
            shown = cardsOf.Count;
            entries.Clear();
            entries.AddRange(cardsOf);
            while (cards.Count < shown)
                cards.Add(Instantiate(Template, Template.transform.parent, false));
            for (int i = 0; i < cards.Count; i++)
            {
                bool used = i < shown;
                cards[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                ShowCard(cards[i], cardsOf[i]);
            }
            Layout();
            PlaceHazes();
        }

        /// <summary>Closes the list.</summary>
        public void Close()
        {
            if (Panel != null)
                Panel.SetActive(false);
        }

        // A tap puts back the card shown large, or shows the card tapped large, or closes the list.
        private void Tapped()
        {
            int card = pressPoint is Vector2 point ? CardAt(point) : -1;
            pressPoint = null;
            if (ZoomShown)
                ZoomOut();
            else if (card >= 0)
                ZoomIn(card);
            else
                Close();
        }

        private void ShowCard(BattleInspectCardView view, Entry card)
        {
            view.Show(
                card.Name,
                card.Owner,
                card.Kind,
                card.Description,
                card.Art,
                card.Frame,
                card.Element,
                card.Cost,
                card.Quality,
                card.FoilStyle
            );
            view.SetPlayable(true, 0f);
            ShowGlint(view, card.Omen);
        }

        // The light of a card that awakens when used, as the hand shows it but settled: it does
        // not flare as the list opens.
        private void ShowGlint(BattleInspectCardView view, bool lit)
        {
            glints.TryGetValue(view, out var glint);
            if (glint == null)
            {
                if (!lit || MakeGlint == null || (glint = MakeGlint((RectTransform)view.transform)) == null)
                    return;
                glints[view] = glint;
                if (view != ZoomCard)
                    MoveHaze(view, glint);
            }
            if (!lit)
                glint.SetLit(false);
            else if (!glint.Lit)
                glint.ShowAt(BattleEvolutionGlint.FlareSeconds);
        }

        // The haze (the light's back half; its star stays on the card) into the layer behind the cards.
        private void MoveHaze(BattleInspectCardView view, BattleEvolutionGlint glint)
        {
            var holder = new GameObject("Glint", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(HazeLayer(), false);
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = Vector2.zero;
            glint.transform.SetParent(holder, false);
            hazes[view] = holder;
        }

        private RectTransform HazeLayer()
        {
            if (hazeLayer != null)
                return hazeLayer;
            hazeLayer = new GameObject("Glints", typeof(RectTransform)).GetComponent<RectTransform>();
            hazeLayer.SetParent(Scroll.transform.parent, false);
            hazeLayer.SetSiblingIndex(Scroll.transform.GetSiblingIndex());
            hazeLayer.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, GlintFade);
            return hazeLayer;
        }

        // Each haze over its card, shown while the card is (not while it is shown large), in the
        // room the rows scroll in.
        private void PlaceHazes()
        {
            if (hazeLayer == null)
                return;
            var viewport = (RectTransform)Scroll.transform;
            hazeLayer.anchorMin = viewport.anchorMin;
            hazeLayer.anchorMax = viewport.anchorMax;
            hazeLayer.offsetMin = viewport.offsetMin;
            hazeLayer.offsetMax = new Vector2(viewport.offsetMax.x, -rowsTop);
            foreach (var pair in hazes)
            {
                var card = pair.Key.transform;
                var holder = pair.Value;
                bool shown = pair.Key.gameObject.activeInHierarchy;
                if (holder.gameObject.activeSelf != shown)
                    holder.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                holder.position = card.position;
                holder.localScale = Vector3.one * (card.lossyScale.x / hazeLayer.lossyScale.x);
            }
        }

        private bool Lit(BattleInspectCardView view) =>
            view != null && glints.TryGetValue(view, out var glint) && glint != null && glint.Lit;

        // The laid-out card under a point of the screen, or -1 (also for a card scrolled under the header).
        private int CardAt(Vector2 screenPoint)
        {
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            var eye = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)Scroll.transform, screenPoint, eye))
                return -1;
            for (int i = 0; i < shown; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)cards[i].transform, screenPoint, eye))
                    return i;
            return -1;
        }

        // The card grows out of its place to the middle; the list under it stops scrolling.
        private void ZoomIn(int index)
        {
            if (ZoomCard == null || index < 0 || index >= shown)
                return;
            EndZoom();
            zoomed = index;
            zoomingIn = true;
            scrolled = Scroll.vertical;
            Scroll.vertical = false;
            Scroll.velocity = Vector2.zero;
            ShowCard(ZoomCard, entries[index]);
            ZoomCard.gameObject.SetActive(true);
            if (ZoomVeil != null)
                ZoomVeil.gameObject.SetActive(true);
            from = PlaceOf(index);
            to = Middle();
            zoomTime = 0f;
            zoomDuration = ZoomInTime;
            cards[index].gameObject.SetActive(false);
            Place(0f);
        }

        // From wherever it is now, the card goes back to its place in the list.
        private void ZoomOut()
        {
            if (zoomed < 0 || !zoomingIn)
                return;
            from = Current();
            to = PlaceOf(zoomed);
            zoomingIn = false;
            zoomTime = 0f;
            zoomDuration = ZoomOutTime;
        }

        private void EndZoom()
        {
            if (zoomed >= 0)
            {
                if (zoomed < cards.Count)
                    cards[zoomed].gameObject.SetActive(zoomed < shown);
                if (Scroll != null)
                    Scroll.vertical = scrolled;
            }
            zoomed = -1;
            zoomingIn = false;
            if (ZoomCard != null)
                ZoomCard.gameObject.SetActive(false);
            if (ZoomVeil != null)
                ZoomVeil.gameObject.SetActive(false);
        }

        // Growing, it overshoots a little and settles; going back, it slows into its place.
        private void Place(float k)
        {
            float move = 1f - (1f - k) * (1f - k) * (1f - k);
            float grow = zoomingIn ? OutBack(k) : move;
            var rect = (RectTransform)ZoomCard.transform;
            rect.anchoredPosition = Vector2.LerpUnclamped(from.Position, to.Position, move);
            rect.localScale = Vector3.one * Mathf.LerpUnclamped(from.Scale, to.Scale, grow);
            if (ZoomVeil != null)
            {
                var color = ZoomVeil.color;
                color.a = Mathf.Lerp(from.Veil, to.Veil, move);
                ZoomVeil.color = color;
            }
        }

        private static float OutBack(float k)
        {
            const float s = 1.4f;
            float x = k - 1f;
            return 1f + x * x * ((s + 1f) * x + s);
        }

        // The pose of the card shown large now.
        private Pose Current()
        {
            var rect = (RectTransform)ZoomCard.transform;
            return new Pose
            {
                Position = rect.anchoredPosition,
                Scale = rect.localScale.x,
                Veil = ZoomVeil != null ? ZoomVeil.color.a : 0f,
            };
        }

        // The pose of the index-th card in the list, in the space of the card shown large.
        private Pose PlaceOf(int index)
        {
            var parent = (RectTransform)ZoomCard.transform.parent;
            var world = cards[index].transform.position;
            var local = (Vector2)parent.InverseTransformPoint(world);
            return new Pose
            {
                Position = local - Offset(parent),
                Scale = CardScale,
                Veil = 0f,
            };
        }

        // The middle of the safe area, the card as large as it fits there.
        private Pose Middle()
        {
            var parent = (RectTransform)ZoomCard.transform.parent;
            var room = (RectTransform)Scroll.transform.parent;
            var local = (Vector2)parent.InverseTransformPoint(room.TransformPoint(room.rect.center));
            var size = ((RectTransform)Template.transform).rect.size;
            float scale = ZoomFill * Mathf.Min(room.rect.height / size.y, room.rect.width / size.x);
            return new Pose
            {
                Position = local - Offset(parent),
                Scale = scale,
                Veil = ZoomVeilAlpha,
            };
        }

        // Where the anchors put a card at zero, in the parent's space.
        private Vector2 Offset(RectTransform parent)
        {
            var rect = (RectTransform)ZoomCard.transform;
            var anchor = (rect.anchorMin + rect.anchorMax) * 0.5f;
            return parent.rect.min + Vector2.Scale(parent.rect.size, anchor);
        }

        // The header and the rows of cards under it: the block in the middle of the screen when
        // it fits; otherwise the header at the top and the rows scrolling in the room under it.
        private void Layout()
        {
            var size = ((RectTransform)Template.transform).rect.size * CardScale;
            var step = size + Gap;
            int columns = Mathf.Max(1, Columns);
            int rows = (shown + columns - 1) / columns;
            float headerHeight = Header != null ? Header.rect.height : 0f;
            float grid = rows > 0 ? rows * step.y - Gap.y : 0f;
            float block = headerHeight + (rows > 0 ? HeaderGap : 0f) + grid;
            float room = ((RectTransform)Scroll.transform.parent).rect.height;
            bool fits = block + Margin * 2f <= room;
            float top = fits ? (room - block) * 0.5f : Margin;
            // The rows scroll below a line halfway between the header and the first row.
            rowsTop = top + headerHeight + HeaderGap * 0.5f;
            float cut = fits ? 0f : rowsTop;
            var viewport = (RectTransform)Scroll.transform;
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, -cut);
            Content.sizeDelta = new Vector2(Content.sizeDelta.x, fits ? room : HeaderGap * 0.5f + grid + Margin);
            Content.anchoredPosition = Vector2.zero;
            Scroll.vertical = !fits;
            Scroll.velocity = Vector2.zero;
            if (Header != null)
                Header.anchoredPosition = new Vector2(0f, -top);
            float first = top + headerHeight + HeaderGap - cut;
            for (int k = 0; k < shown; k++)
            {
                int row = k / columns;
                int column = k % columns;
                var rect = (RectTransform)cards[k].transform;
                rect.anchoredPosition = new Vector2(
                    (column - (columns - 1) * 0.5f) * step.x,
                    -(first + row * step.y + size.y * 0.5f)
                );
                rect.localScale = Vector3.one * CardScale;
            }
        }
    }
}
