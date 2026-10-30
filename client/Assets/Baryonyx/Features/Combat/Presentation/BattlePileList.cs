using System;
using System.Collections.Generic;
using Baryonyx.Inventory;
using TMPro;
using UnityEngine;
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
    /// block in the middle of the screen; rows past the screen scroll. A tap anywhere but a drag
    /// closes it, as the cross does. Nothing behind it is pressed while open.
    /// </summary>
    public sealed class BattlePileList : MonoBehaviour
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
        }

        public static readonly string[] Titles = { "山札", "捨て札", "消滅" };

        [Tooltip("一覧の全体。開いている間だけ出し、後ろの戦闘を暗くして押せなくする。")]
        public GameObject Panel;

        [Tooltip("どこを押しても閉じるボタン（一覧の全体）。")]
        public Button Backdrop;

        public Button CloseButton;

        [Tooltip("縦に流れる範囲。カードが画面に収まるときは動かない。")]
        public ScrollRect Scroll;

        [Tooltip("見出しとカードを並べる中身。上端から下へ並べる。")]
        public RectTransform Content;

        [Tooltip("見出しの行（札置き場の絵・名前と枚数・閉じる×）。カードの上に置く。")]
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

        private static readonly string CountHex = "FACC5C";

        private readonly List<BattleInspectCardView> cards = new();
        private BattlePile pile;
        private int shown;

        /// <summary>True while the list is open.</summary>
        public bool IsOpen => Panel != null && Panel.activeSelf;

        /// <summary>The pile shown while open.</summary>
        public BattlePile Pile => pile;

        /// <summary>How many cards are laid out.</summary>
        public int Shown => shown;

        /// <summary>The cards laid out now, in order (the first <see cref="Shown"/> are used).</summary>
        public IReadOnlyList<BattleInspectCardView> Cards => cards;

        private void Awake()
        {
            if (Backdrop != null)
                Backdrop.onClick.AddListener(Close);
            if (CloseButton != null)
                CloseButton.onClick.AddListener(Close);
            // The list is built hidden, so this runs as it first opens: it must not hide it again.
            if (Template != null)
                Template.gameObject.SetActive(false);
        }

        /// <summary>Opens the list on <paramref name="cardsOf"/>, the cards of <paramref name="which"/> in order.</summary>
        public void Open(BattlePile which, IReadOnlyList<Entry> cardsOf)
        {
            if (Panel == null || Template == null)
                return;
            pile = which;
            Panel.SetActive(true);
            for (int i = 0; i < Icons.Length; i++)
                if (Icons[i] != null)
                    Icons[i].SetActive(i == (int)which);
            if (Title != null)
                Title.text = $"{Titles[(int)which]}　<color=#{CountHex}>{cardsOf.Count}</color>";
            shown = cardsOf.Count;
            while (cards.Count < shown)
                cards.Add(Instantiate(Template, Template.transform.parent, false));
            for (int i = 0; i < cards.Count; i++)
            {
                bool used = i < shown;
                cards[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                var card = cardsOf[i];
                cards[i]
                    .Show(
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
                cards[i].SetPlayable(true, 0f);
            }
            Layout();
        }

        /// <summary>Closes the list.</summary>
        public void Close()
        {
            if (Panel != null)
                Panel.SetActive(false);
        }

        // The header and the rows of cards under it, the block in the middle of the screen when
        // it fits and from the top, scrolling, when it does not.
        private void Layout()
        {
            var size = ((RectTransform)Template.transform).rect.size * CardScale;
            var step = size + Gap;
            int columns = Mathf.Max(1, Columns);
            int rows = (shown + columns - 1) / columns;
            float headerHeight = Header != null ? Header.rect.height : 0f;
            float grid = rows > 0 ? rows * step.y - Gap.y : 0f;
            float block = headerHeight + (rows > 0 ? HeaderGap : 0f) + grid;
            float room = ((RectTransform)Scroll.transform).rect.height;
            float height = Mathf.Max(room, block + Margin * 2f);
            float top = (height - block) * 0.5f;
            Content.sizeDelta = new Vector2(Content.sizeDelta.x, height);
            Content.anchoredPosition = Vector2.zero;
            Scroll.vertical = height > room;
            Scroll.velocity = Vector2.zero;
            if (Header != null)
                Header.anchoredPosition = new Vector2(0f, -top);
            float first = top + headerHeight + HeaderGap;
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
