using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.CardLoadout
{
    /// <summary>
    /// The cards to choose from, over the whole screen under a dark veil, opened by pressing one
    /// of the six cards (doc/features/screens/loadout.md#スキル). For a custom skill's slot it lays
    /// out five to a row, the skills the person can set, then the items, then the skills of
    /// other elements (dark and not pressable), each group from a new row with a gold line
    /// between; for a unique skill, the seven stages of its tree, baked in place by the generator.
    /// It opens with the card in the slot (the stage set) chosen and scrolled to; pressing a card
    /// chooses it, and the round check at the bottom right raises <see cref="CardPressed"/> or
    /// <see cref="StagePressed"/> for it. The veil and back close it without a change. The cards
    /// pop in one after another when it opens.
    /// </summary>
    public sealed class CardLoadoutPicker : MonoBehaviour
    {
        // 一覧の1行のカードの数と、カードの間・まとまりの間・上下の余白（設計座標）。
        public int Columns = 5;
        public float Gap = 36f;
        public float GroupGap = 72f;
        public float Pad = 48f;

        // 開いたとき、幕が濃くなる時間と、カードが1枚ずつ少し遅れて浮かぶ時間（秒）。
        public const float FadeSeconds = 0.12f;
        public const float PopSeconds = 0.16f;
        public const float PopStagger = 0.018f;
        private const float PopFrom = 0.9f;
        private const int MostStaggered = 14;

        // 幕と一覧の余白。押すと何も変えずに閉じる。
        public Button[] Closers = Array.Empty<Button>();
        public CanvasGroup Group;

        // カスタムスキルの枠の候補：スクロールする一覧、カード（1枚目を写して増やす）、まとまりの間の線、「スキルを外す」。
        public GameObject ChoicesRoot;
        public ScrollRect Scroll;
        public RectTransform Content;
        public CardLoadoutCard[] Cards = Array.Empty<CardLoadoutCard>();
        public RectTransform[] Lines = Array.Empty<RectTransform>();
        public GlassCard Remove;

        // 固有スキルの覚醒ツリー：7つの段階のカード（並びと線は生成時に決める）。縦にスクロールする。
        public GameObject TreeRoot;
        public ScrollRect TreeScroll;
        public CardLoadoutCard[] Stages = Array.Empty<CardLoadoutCard>();

        // 選んだカードで決める、右下の丸いチェック。
        public Button Confirm;

        public event Action<string> CardPressed;
        public event Action<int> StagePressed;

        private readonly List<RectTransform> popping = new();
        private float openedAt = float.NegativeInfinity;
        private bool bound;

        // 選んでいるカード（決める前）。開き直すと、枠に入っているカード（セットしている段階）に戻す。
        private CardLoadoutCard chosen;
        private bool removeChosen;
        private bool chosenSet;

        public bool Open => gameObject.activeSelf;

        /// <summary>Opens over the screen with nothing in it yet; <see cref="Show"/> fills it.</summary>
        public void OpenFor(bool tree)
        {
            Bind();
            chosen = null;
            removeChosen = false;
            chosenSet = false;
            gameObject.SetActive(true);
            if (ChoicesRoot != null)
                ChoicesRoot.SetActive(!tree);
            if (TreeRoot != null)
                TreeRoot.SetActive(tree);
            if (Scroll != null)
                Scroll.verticalNormalizedPosition = 1f;
            if (TreeScroll != null)
                TreeScroll.verticalNormalizedPosition = 1f;
            openedAt = Time.unscaledTime;
            popping.Clear();
            if (Group != null)
                Group.alpha = 0f;
        }

        public void Close()
        {
            Settle();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Fills the open picker from <paramref name="state"/>: the chosen slot's choices, or the
        /// chosen unique skill's tree. <paramref name="draw"/> puts a choice on a card.
        /// </summary>
        public void Show(
            CardLoadoutState state,
            Action<CardLoadoutCard, CardLoadoutCardState, bool, string> draw
        )
        {
            if (state.Tree != null)
                ShowTree(state.Tree, draw);
            else
                ShowChoices(state, draw);
        }

        // 付けられるスキル（最後に「スキルを外す」）、アイテム、属性が合わないスキルの順に、まとまりごとに行を改める。
        private void ShowChoices(
            CardLoadoutState state,
            Action<CardLoadoutCard, CardLoadoutCardState, bool, string> draw
        )
        {
            var skills = state.Choices.Where(card => card.ItemId == null).ToList();
            var groups = new List<List<CardLoadoutCardState>>
            {
                skills.Where(card => !card.Unavailable).ToList(),
                (state.Items ?? Array.Empty<CardLoadoutCardState>()).ToList(),
                skills.Where(card => card.Unavailable).ToList(),
            };
            EnsureCards(groups.Sum(group => group.Count));
            var placed = new List<(RectTransform Rect, int Group)>();
            int next = 0;
            for (int g = 0; g < groups.Count; g++)
            {
                foreach (var choice in groups[g])
                {
                    var card = Cards[next++];
                    card.gameObject.SetActive(true);
                    card.Id = choice.Id;
                    card.Stage = -1;
                    // アイテムは持っている数だけほかの枠で使っていれば押せない。
                    bool playable = !choice.Unavailable;
                    draw(card, choice, playable, null);
                    card.SetMark(choice.Mark, choice.HeldByOther);
                    card.Glass.Interactable = playable;
                    if (!chosenSet && choice.Slot >= 0 && choice.Slot == state.Slot)
                        chosen = card;
                    placed.Add(((RectTransform)card.transform, g));
                }
                // 枠に何か入っていれば、付けられるスキルの最後に「スキルを外す」。
                if (g == 0 && Remove != null)
                {
                    Remove.gameObject.SetActive(state.CanRemove);
                    if (state.CanRemove)
                        placed.Add(((RectTransform)Remove.transform, g));
                }
            }
            for (int i = next; i < Cards.Length; i++)
                Cards[i].gameObject.SetActive(false);
            Place(placed);
            KeepChoice(!chosenSet);
            Pop(placed.Select(entry => entry.Rect));
        }

        // カードを左上から行ごとに置く。まとまりが変わるたびに行を改め、間に金の線を引く。
        private void Place(List<(RectTransform Rect, int Group)> placed)
        {
            if (Content == null || Cards.Length == 0)
                return;
            var size = ((RectTransform)Cards[0].transform).rect.size;
            float rowWidth = Columns * size.x + (Columns - 1) * Gap;
            float left = Mathf.Max(0f, (Content.rect.width - rowWidth) / 2f);
            float y = Pad;
            int col = 0;
            int group = -1;
            int line = 0;
            foreach (var (rect, g) in placed)
            {
                if (group >= 0 && g != group)
                {
                    y += size.y + GroupGap;
                    PlaceLine(line++, left, y - GroupGap / 2f, rowWidth);
                    col = 0;
                }
                else if (col == Columns)
                {
                    y += size.y + Gap;
                    col = 0;
                }
                group = g;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(left + col * (size.x + Gap), -y);
                col++;
            }
            for (int i = line; i < Lines.Length; i++)
                Lines[i].gameObject.SetActive(false);
            float height = placed.Count > 0 ? y + size.y + Pad : 0f;
            Content.sizeDelta = new Vector2(Content.sizeDelta.x, height);
        }

        private void PlaceLine(int index, float left, float y, float width)
        {
            if (index >= Lines.Length)
            {
                if (Lines.Length == 0)
                    return;
                var copy = Instantiate(Lines[0].gameObject, Lines[0].parent);
                Lines = Lines.Append((RectTransform)copy.transform).ToArray();
            }
            var line = Lines[index];
            line.gameObject.SetActive(true);
            line.anchorMin = line.anchorMax = new Vector2(0f, 1f);
            line.pivot = new Vector2(0f, 0.5f);
            line.anchoredPosition = new Vector2(left, -y);
            line.sizeDelta = new Vector2(width, line.sizeDelta.y);
        }

        // ツリーの段階。取得した段階は表、取得していない段階は封印した裏にする。未解放なら、どれも押せない。
        private void ShowTree(
            CardLoadoutTreeState tree,
            Action<CardLoadoutCard, CardLoadoutCardState, bool, string> draw
        )
        {
            for (int i = 0; i < Stages.Length; i++)
            {
                var card = Stages[i];
                var stage = tree.Stages?.FirstOrDefault(entry => entry.Stage == i);
                card.Stage = i;
                card.Id = "";
                bool acquired = stage != null && stage.Acquired && stage.Card != null;
                if (acquired)
                {
                    string note =
                        tree.Locked && i == 0 ? CardLoadoutView.UnlockText(tree.UnlockLevel) : null;
                    draw(card, stage.Card, !tree.Locked, note);
                    card.SetMark(stage.Set ? CardLoadoutView.SetMark : null, false);
                }
                else
                    card.ShowBack(true);
                card.Glass.Interactable = acquired && !tree.Locked;
                if (!chosenSet && acquired && stage.Set && !tree.Locked)
                    chosen = card;
            }
            KeepChoice(false);
            Pop(Stages.Select(card => (RectTransform)card.transform));
        }

        private void EnsureCards(int count)
        {
            if (Cards.Length == 0 || Cards.Length >= count)
                return;
            var cards = new List<CardLoadoutCard>(Cards);
            var source = Cards[0];
            while (cards.Count < count)
            {
                var copy = Instantiate(source, source.transform.parent);
                copy.name = "Choice" + cards.Count;
                copy.Glass.Clicked += () => Press(copy);
                cards.Add(copy);
            }
            Cards = cards.ToArray();
        }

        // 押されたカードを知らせる。生成で作ったカードは開いたときに一度だけつなぐ。
        private void Bind()
        {
            if (bound)
                return;
            bound = true;
            foreach (var card in Cards.Concat(Stages))
            {
                var pressed = card;
                pressed.Glass.Clicked += () => Press(pressed);
            }
            if (Remove != null)
                Remove.Clicked += ChooseRemove;
            foreach (var closer in Closers)
                if (closer != null)
                    closer.onClick.AddListener(Close);
            if (Confirm != null)
                Confirm.onClick.AddListener(Decide);
        }

        // 押したカードを選ぶ（まだ付け替えない）。
        private void Press(CardLoadoutCard card)
        {
            chosen = card;
            removeChosen = false;
            ShowChoice();
        }

        private void ChooseRemove()
        {
            chosen = null;
            removeChosen = true;
            ShowChoice();
        }

        // 右下のチェック：選んだカードで決める。
        private void Decide()
        {
            if (removeChosen)
                CardPressed?.Invoke(CardLoadoutPresenter.RemoveKey);
            else if (chosen != null && chosen.Stage >= 0)
                StagePressed?.Invoke(chosen.Stage);
            else if (chosen != null && !string.IsNullOrEmpty(chosen.Id))
                CardPressed?.Invoke(chosen.Id);
        }

        // 描き直したあとも、選んだカードを選んだままにする（見えなくなったら選ばない）。
        // 開いたばかりなら、選んだカード（枠に入っているカード）が見えるところまで一覧を送る。
        private void KeepChoice(bool reveal)
        {
            chosenSet = true;
            if (chosen != null && !chosen.gameObject.activeSelf)
                chosen = null;
            if (removeChosen && (Remove == null || !Remove.gameObject.activeSelf))
                removeChosen = false;
            ShowChoice();
            if (reveal && chosen != null)
                Reveal((RectTransform)chosen.transform);
        }

        private void ShowChoice()
        {
            foreach (var card in Cards.Concat(Stages))
                card.Glass.Selected = card == chosen;
            if (Remove != null)
                Remove.Selected = removeChosen;
            if (Confirm != null)
                Confirm.interactable = chosen != null || removeChosen;
        }

        // 一覧の上の端に、そのカードの段が来るよう送る。
        private void Reveal(RectTransform card)
        {
            if (Scroll == null || Content == null || Scroll.viewport == null)
                return;
            float room = Content.rect.height - Scroll.viewport.rect.height;
            if (room <= 0f)
                return;
            float top = -card.anchoredPosition.y - Pad;
            Content.anchoredPosition = new Vector2(
                Content.anchoredPosition.x,
                Mathf.Clamp(top, 0f, room)
            );
        }

        /// <summary>The card chosen and not decided yet (null while 「スキルを外す」 or nothing is).</summary>
        public CardLoadoutCard Chosen => chosen;

        // 開いた直後だけ、見えるカードを少し遅らせながら浮かべる。描き直し（保存の後）では浮かべない。
        private void Pop(IEnumerable<RectTransform> cards)
        {
            if (!Application.isPlaying || Time.unscaledTime - openedAt > FadeSeconds)
            {
                Settle();
                return;
            }
            popping.Clear();
            popping.AddRange(cards);
            Update();
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;
            float t = Time.unscaledTime - openedAt;
            if (Group != null)
                Group.alpha = Mathf.Clamp01(t / FadeSeconds);
            if (popping.Count == 0)
                return;
            bool done = true;
            for (int i = 0; i < popping.Count; i++)
            {
                float u = Mathf.Clamp01(
                    (t - Mathf.Min(i, MostStaggered) * PopStagger) / PopSeconds
                );
                if (u < 1f)
                    done = false;
                float eased = 1f - (1f - u) * (1f - u);
                if (popping[i] != null)
                    popping[i].localScale = Vector3.one * Mathf.Lerp(PopFrom, 1f, eased);
            }
            if (done)
                Settle();
        }

        private void Settle()
        {
            foreach (var rect in popping)
                if (rect != null)
                    rect.localScale = Vector3.one;
            popping.Clear();
            if (Group != null)
                Group.alpha = 1f;
        }
    }
}
