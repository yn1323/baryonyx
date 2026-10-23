using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Inventory;
using Baryonyx.Party;
using Baryonyx.Training;
using Baryonyx.UI;
using Baryonyx.UI.Buttons;
using Baryonyx.UI.Cards;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.UI.UiText;

namespace Baryonyx.CardLoadout
{
    /// <summary>A card in a row (a stage of the evolution tree on the right), baked by the generator.</summary>
    [Serializable]
    public sealed class CardLoadoutSlotWidget
    {
        public Button Button;
        public GameObject Selected;

        // カードの挿絵（2倍）と、その上に重ねるキラキラ、コストと属性のアイコン。
        public RawImage Art;
        public CardFoil Foil;
        public TMP_Text Cost;
        public Image Element;
        public TMP_Text Name;
        public TMP_Text Kind;
        public TMP_Text Description;
    }

    /// <summary>
    /// One of the six small tiles over the chosen card in the selection mode: the card's art,
    /// lit gold when it is the slot chosen. Pressing another tile chooses that slot.
    /// </summary>
    [Serializable]
    public sealed class CardLoadoutTileWidget
    {
        public Button Button;
        public GameObject Selected;
        public RawImage Art;

        // 未解放の固有スキルの錠前と、空いた枠の「＋」。
        public GameObject Lock;
        public GameObject Empty;
    }

    /// <summary>One stage of the evolution tree on the right, baked by the generator.</summary>
    [Serializable]
    public sealed class CardLoadoutStageWidget
    {
        public CardLoadoutSlotWidget Card = new();

        // 取得していない段階の封印（形だけ見せる）と、そのときに隠す中身。
        public GameObject Shadow;
        public GameObject Content;

        // 未解放の固有スキルの最初の段階に重ねる錠前と、右下の「セット中」「Lv 20で解放」。
        public GameObject Lock;
        public TMP_Text Mark;
    }

    /// <summary>The evolution tree of the unique skill chosen on the left, in the right frame.</summary>
    [Serializable]
    public sealed class CardLoadoutTreeWidget
    {
        public GameObject Root;

        // 「マナバーストの進化」。
        public TMP_Text Title;

        // UniqueSkillTreeShape の順の7つの段階（1行ずつ縦に並べ、はみ出す分はスクロールする）。
        public CardLoadoutStageWidget[] Stages = Array.Empty<CardLoadoutStageWidget>();
        public ScrollRect Scroll;
    }

    /// <summary>
    /// One card's row on the right. The generator bakes one row per skill so the prefab reads in
    /// the editor; the view fills the rows with the cards the person can choose, copying the
    /// first row when it needs more (every owned card and item is a row of its own).
    /// </summary>
    [Serializable]
    public sealed class CardLoadoutRowWidget
    {
        // 今出しているカードのキー（PartyCardKey の形）。押すとこのキーを入れる。
        public string Id = "";
        public Button Button;
        public RawImage Art;
        public CardFoil Foil;
        public TMP_Text Cost;
        public Image Element;
        public TMP_Text Name;
        public TMP_Text Kind;

        // 説明の数字は選んでいる人のステータスで変わる。
        public TMP_Text Description;

        // その人の何枚目に入っているか（「1枚目」）。入っていなければ隠す。
        public TMP_Text Mark;
    }

    /// <summary>
    /// The formation's card skills change screen over the guide screen, laid out like the
    /// equipment's: the person (name, level, elements) and their stats on the left, then either
    /// their six cards (three unique skills and three custom skills, as small cards) or, in the
    /// selection mode, six tiles over the chosen slot's card shown large; the skill and item
    /// tabs and the cards they can set on the right, or the evolution tree of the unique skill
    /// chosen. 「今のデッキ」 lays the six cards out whole over the screen.
    /// CardLoadoutAssets bakes the parts and rows into the formation prefab,
    /// so they read in the editor; this view wires them up and drives its own presenter. When
    /// the app has a server (<see cref="PartySession.Source"/>), it reads the party on opening
    /// and saves every change there; otherwise it uses the mock data. Back returns to the
    /// adventurer's page. Changes are told on the guide screen's notice band.
    /// </summary>
    public sealed class CardLoadoutView
        : GuidePanel,
            ICardLoadoutView,
            IGuideBackHandler,
            IGuideBreadcrumb,
            IEditModePreview
    {
        public const string LoadFailedMessage = "スキルを取得できませんでした";

        // まだ解放していない固有スキルの挿絵の色。
        private static readonly Color LockedArt = new(0.25f, 0.25f, 0.3f, 1f);

        public PartyMockData Party;
        public TrainingMockData Training;

        // アイテムの絵（種類のIDで引く）。
        public ItemArt ItemArt;

        // 左上の人（立ち姿・名前・Lv）。
        public PartyPersonHeader Person = new();

        // 左の面の上の左右のフリックで人を替える（個別の画面と同じ）。
        public GuideSwipe Swipe;

        // 開いたときの6枚の一覧：固有スキル3枚とカスタムスキル3枚の小さなカードと「今のデッキ」。
        // カードを押すと、その枠の選択モードになる（固有スキルは右に進化ツリーを出す）。
        public GameObject Overview;
        public TrainingCardWidget[] UniqueCards = Array.Empty<TrainingCardWidget>();
        public TrainingCardWidget[] SlotCards = Array.Empty<TrainingCardWidget>();
        public Button DeckButton;

        // 選択モード：6枚の札（固有スキル3つ、続けてカスタムスキル3つ）と、選んでいる枠の今のカード、戻る。
        public GameObject Selection;
        public CardLoadoutTileWidget[] Tiles = Array.Empty<CardLoadoutTileWidget>();
        public TrainingCardWidget Chosen = new();
        public Button Back;

        // 今のデッキの全画面：「〇〇のデッキ」と閉じる×、固有スキル3枚とカスタムスキル3枚。
        public GameObject Deck;
        public TMP_Text DeckTitle;
        public Button DeckClose;
        public TrainingCardWidget[] DeckUniques = Array.Empty<TrainingCardWidget>();
        public TrainingCardWidget[] DeckSlots = Array.Empty<TrainingCardWidget>();

        // CardElement の順の、戦闘のカードの属性ごとの枠。
        public Texture[] CardFrames = Array.Empty<Texture>();

        // 固有スキルを選んでいるときに、タブと一覧の代わりに右に出す進化ツリー。
        public CardLoadoutTreeWidget Tree = new();

        // 選んでいる人のステータス（CharacterStats の順）。
        public TMP_Text[] Stats = Array.Empty<TMP_Text>();

        // 右の上のスキル・アイテムのタブ（CardLoadoutPresenter.SkillTab・ItemTab の順）。
        public GuideTab[] Tabs = Array.Empty<GuideTab>();

        // タブの下の、その人が付けられる属性の行と、そのアイコン（2つまで）。スキルのタブだけで出す。
        public GameObject UsableLine;
        public Image[] Usable = Array.Empty<Image>();

        // 読み込み中と読めなかったとき、アイテムのタブを選んでいるときだけ一覧の面に出す文字。
        public TMP_Text Status;
        public CardLoadoutRowWidget[] Rows = Array.Empty<CardLoadoutRowWidget>();
        public ScrollRect List;

        // CardElement の順の属性のアイコン。属性なしはnull。
        public Sprite[] ElementIcons = Array.Empty<Sprite>();

        private CardLoadoutPresenter presenter;
        private readonly ButtonBindings bindings = new();

        public event Action PrevPressed;
        public event Action NextPressed;
        public event Action<int> SlotPressed;
        public event Action<int> TabPressed;
        public event Action<string> CardPressed;
        public event Action<int> UniquePressed;
        public event Action<int> StagePressed;
        public event Action BackPressed;
        public event Action DeckPressed;
        public event Action DeckClosed;

        public CardLoadoutPresenter Presenter => presenter;

        // 最後に描いた状態。戻るで今のデッキを閉じるために使う。
        private CardLoadoutState shown;

        private void OnEnable()
        {
            person = null;
            shownTree = null;
            if (Swipe != null)
                Swipe.Flicked += Flick;
            shown = null;
            for (int i = 0; i < SlotCards.Length; i++)
            {
                int index = i;
                bindings.Bind(SlotCards[i].Button, () => SlotPressed?.Invoke(index));
            }
            for (int i = 0; i < UniqueCards.Length; i++)
            {
                int index = i;
                bindings.Bind(UniqueCards[i].Button, () => UniquePressed?.Invoke(index));
            }
            // 札は固有スキル3つ、続けてカスタムスキル3つ。
            for (int i = 0; i < Tiles.Length; i++)
            {
                int index = i;
                bindings.Bind(
                    Tiles[i].Button,
                    () =>
                    {
                        if (index < UniqueCards.Length)
                            UniquePressed?.Invoke(index);
                        else
                            SlotPressed?.Invoke(index - UniqueCards.Length);
                    }
                );
            }
            bindings.Bind(Back, () => BackPressed?.Invoke());
            bindings.Bind(DeckButton, () => DeckPressed?.Invoke());
            bindings.Bind(DeckClose, () => DeckClosed?.Invoke());
            for (int i = 0; i < Tree.Stages.Length; i++)
            {
                int stage = i;
                bindings.Bind(Tree.Stages[i].Card.Button, () => StagePressed?.Invoke(stage));
            }
            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                if (Tabs[i] != null)
                    bindings.Bind(Tabs[i].Button, () => TabPressed?.Invoke(index));
            }
            foreach (var row in Rows)
                BindRow(row);

            // 開くたびに、サーバーの編成とカード（なければアプリを動かしている間のもの）を読み直す。
            presenter?.Dispose();
            presenter = null;
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            if (Party == null)
                return;
            var source = PartySession.Source;
            if (source == null)
            {
                Present(PartySession.Formation(Party), null);
                return;
            }
            // 前に読んだ編成があれば、すぐ出して裏で読み直す。なければ読み終えるまで仮データを隠す。
            var known = PartySession.Known;
            if (known != null)
                Present(known, Save);
            else
                ShowLoading();
            StartLoad(
                token => LoadAsync(source, known != null, token),
                LoadFailedMessage,
                () =>
                {
                    // 前の編成を出しているときは、そのまま残して通知だけにする。
                    if (known == null)
                        ShowLoadFailed();
                },
                readAgain: known != null
            );
        }

        private async Task LoadAsync(IPartySource source, bool shown, CancellationToken token)
        {
            string shownVersion = PartySession.ReadVersion;
            // 保存を待っている変更があれば、読み直さずに手元の編成を出す。
            var formation = await PartySession.LoadAsync(Party, source, token);
            if (token.IsCancellationRequested)
                return;
            if (shown && Unchanged(shownVersion, PartySession.ReadVersion))
                return;
            Present(formation, Save);
        }

        private static Task Save(string id, int slot, string key) =>
            PartySession.SetCardAsync(id, slot, key, CardLoadoutPresenter.SaveFailedMessage);

        private void ShowLoadFailed() => Set(Status, LoadFailedText);

        private void Present(PartyFormation formation, Func<string, int, string, Task> save)
        {
            presenter?.Dispose();
            presenter = new CardLoadoutPresenter(
                this,
                CardLoadoutPresenter.People(formation),
                new CardLoadoutSessionStore(Party, Training),
                save,
                CardLoadoutSession.OpenSlot,
                unique: CardLoadoutSession.OpenUnique
            );
        }

        // 読み込むまで、仮データのカードを本当のカードとして見せない。
        private void ShowLoading()
        {
            ShowTree(false);
            ShowTabs(CardLoadoutPresenter.SkillTab);
            Set(Status, LoadingText);
            if (UsableLine != null)
                UsableLine.SetActive(false);
            Person.ShowLoading(LoadingText);
            StatGrid.Show(Stats, null);
            ShowMode(false, false);
            foreach (var widget in UniqueCards.Concat(SlotCards))
            {
                TrainingView.ShowCard(widget, null, null);
                Set(widget.Name, "");
            }
            ShowUsable(Usable, Array.Empty<CardElement>(), IconOf);
            HideRows();
        }

        // 右に、タブと一覧の代わりに進化ツリーを出す（false で戻す）。
        private void ShowTree(bool on)
        {
            if (Tree.Root != null)
                Tree.Root.SetActive(on);
            foreach (var tab in Tabs)
                if (tab != null)
                    tab.gameObject.SetActive(!on);
            // スクロールは右の面そのものに付いているので、一覧の表示範囲だけを隠し、スクロールを止める。
            if (List != null)
            {
                List.enabled = !on;
                if (List.viewport != null)
                    List.viewport.gameObject.SetActive(!on);
            }
            if (on)
            {
                if (UsableLine != null)
                    UsableLine.SetActive(false);
                Set(Status, "");
            }
        }

        private void HideRows()
        {
            foreach (var row in Rows)
                if (row.Button != null)
                    row.Button.gameObject.SetActive(false);
        }

        // 選んでいるタブに印を付け、アイテムのタブでは属性の行を隠す（アイテムは誰でも使える）。
        // 持っていなければ、持っていないことを出す。
        private void ShowTabs(int tab, bool empty = true)
        {
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i == tab;
            bool items = tab == CardLoadoutPresenter.ItemTab;
            if (UsableLine != null)
                UsableLine.SetActive(!items);
            Set(Status, items && empty ? NoItemText : "");
        }

        // アイテムのタブで、アイテムを1つも持っていないときの一覧の面の文字。
        public const string NoItemText = "持っているアイテムはありません";

        // 停止中の表示の切り替え（doc/rules/ui-design.md#停止中の表示の切り替え）。最初の表示は6枚の一覧とスキルのタブ。
        public const string TreePreviewState = "固有スキルの選択モード";
        public const string DeckPreviewState = "今のデッキ";

        public IReadOnlyList<string> PreviewStates =>
            new[] { "アイテム", TreePreviewState, DeckPreviewState }
                .Concat(GuidePanelLoad.PreviewStates)
                .ToArray();

        public void ShowPreviewState(int index)
        {
            switch (index)
            {
                case 0:
                    // 左は作り込んだ先頭の人のまま、右だけアイテムのタブにする。
                    ShowTabs(CardLoadoutPresenter.ItemTab);
                    HideRows();
                    return;
                case 1:
                    // 生成時に作り込んだ、先頭の人の1つ目の固有スキルの選択モードと進化ツリー。
                    ShowMode(true, false);
                    ShowTiles(0);
                    ShowTree(true);
                    return;
                case 2:
                    ShowMode(false, true);
                    return;
            }
            ShowLoading();
            if (index == 4)
                ShowLoadFailed();
        }

        // 左を、6枚の一覧か選択モードにし、今のデッキの全画面を出すか隠す。
        private void ShowMode(bool selecting, bool deck)
        {
            if (Overview != null)
                Overview.SetActive(!selecting);
            if (Selection != null)
                Selection.SetActive(selecting);
            if (Deck != null)
            {
                Deck.SetActive(deck);
                // 後から足された部品があっても、全画面はいつも一番手前に描く。
                if (deck)
                    Deck.transform.SetAsLastSibling();
            }
        }

        // 選択モードで選んでいる札（固有スキル3つ、続けてカスタムスキル3つの順）に金の枠を付ける。
        private void ShowTiles(int chosen)
        {
            for (int i = 0; i < Tiles.Length; i++)
                if (Tiles[i].Selected != null)
                    Tiles[i].Selected.SetActive(i == chosen);
        }

        // 左へのフリックで次の人、右へのフリックで前の人。
        private void Flick(int step)
        {
            if (step > 0)
                NextPressed?.Invoke();
            else if (step < 0)
                PrevPressed?.Invoke();
        }

        private void OnDisable()
        {
            Load.Cancel();
            bindings.Clear();
            if (Swipe != null)
                Swipe.Flicked -= Flick;
            presenter?.Dispose();
            presenter = null;
        }

        // 付け替えの画面で戻ると、冒険者の個別の画面へ戻る（同じ人を選んだまま）。
        // パンくずの「冒険者 › アリア › スキル」。読み込むまでは人の名前を出さない。
        public IReadOnlyList<string> Trail =>
            TrainingSession.ChangeTrail(Guide, person, CardLoadoutSession.GuideItemKey);

        // パンくずに出す、表示している人の名前。
        private string person;

        // 今のデッキを出しているときは、閉じるだけにする。
        public bool HandleBack()
        {
            if (shown != null && shown.DeckOpen)
            {
                DeckClosed?.Invoke();
                return true;
            }
            return TrainingSession.BackToPage(Guide);
        }

        public void Render(CardLoadoutState state)
        {
            var member = Party != null ? Party.Find(state.PersonId) : null;
            person = member?.Name;
            if (Guide != null)
                Guide.RefreshTrail();
            int level = PartySession.LevelOf(Party, state.PersonId);
            Person.Show(
                member,
                level,
                state
                    .Slots.Select(card => IconOf(card.Element))
                    .Where(icon => icon != null)
                    .Distinct()
                    .ToArray()
            );
            var growth = Training != null ? Training.Find(state.PersonId) : null;
            StatGrid.Show(Stats, growth != null ? TrainingBond.StatsOf(growth, level) : null);
            shown = state;
            var uniques = Enumerable
                .Range(0, UniqueCards.Length)
                .Select(i =>
                    UniqueCard(
                        state.Uniques != null && i < state.Uniques.Count ? state.Uniques[i] : null
                    )
                )
                .ToArray();
            var slots = Enumerable
                .Range(0, SlotCards.Length)
                .Select(i => SlotCard(i < state.Slots.Count ? state.Slots[i] : null))
                .ToArray();
            ShowMode(state.Selecting, state.DeckOpen);
            ShowCards(UniqueCards, uniques);
            ShowCards(SlotCards, slots);
            ShowCards(DeckUniques, uniques);
            ShowCards(DeckSlots, slots);
            Set(DeckTitle, $"{state.Name}のデッキ");
            if (state.Selecting)
            {
                int chosen = state.Unique >= 0 ? state.Unique : uniques.Length + state.Slot;
                var all = uniques.Concat(slots).ToArray();
                for (int i = 0; i < Tiles.Length; i++)
                    ShowTile(Tiles[i], i < all.Length ? all[i] : null);
                ShowTiles(chosen);
                var card = chosen < all.Length ? all[chosen] : null;
                TrainingView.ShowCard(Chosen, card, FrameOf(card));
            }
            ShowUsable(Usable, state.Usable, IconOf);
            ShowTabs(state.Tab, state.Choices.Count == 0);
            ShowRows(state);
            if (state.Tree != null)
                ShowTreeState(state.Tree, state.PersonId);
            else
                shownTree = null;
            ShowTree(state.Tree != null);
        }

        // 左の小さなカード・大きなカード・全画面のデッキは、個別の画面と同じ戦闘のカードの組み方で出す。
        private void ShowCards(TrainingCardWidget[] widgets, IReadOnlyList<TrainingCardState> cards)
        {
            for (int i = 0; i < widgets.Length; i++)
            {
                var card = i < cards.Count ? cards[i] : null;
                TrainingView.ShowCard(widgets[i], card, FrameOf(card));
            }
        }

        private Texture FrameOf(TrainingCardState card)
        {
            int element = (int)(card?.Element ?? CardElement.None);
            return element < CardFrames.Length ? CardFrames[element] : null;
        }

        // 固有スキルのカード。未解放なら錠前と解放するLvを重ねる。
        private TrainingCardState UniqueCard(CardLoadoutUniqueState unique)
        {
            var card = unique?.Card;
            if (card == null)
                return null;
            return TrainingCardState.Of(
                card.Id,
                TrainingCardState.UniqueType,
                card.Name,
                card.KindLine,
                card.Description,
                card.Cost,
                card.Quality,
                card.FoilStyle,
                card.Element,
                IconOf(card.Element),
                ArtOf(card),
                unique.Locked,
                unique.UnlockLevel
            );
        }

        // カスタムスキルの枠のカード。空いていれば「空き」。
        private TrainingCardState SlotCard(CardLoadoutCardState card) =>
            card == null || card.Id == null
                ? TrainingCardState.Of(
                    null,
                    "",
                    TrainingPresenter.EmptyName,
                    "",
                    "",
                    0,
                    ItemQuality.C,
                    0,
                    CardElement.None,
                    null,
                    null
                )
                : TrainingCardState.Of(
                    card.Id,
                    TrainingCardState.SkillType,
                    card.Name,
                    card.KindLine,
                    card.Description,
                    card.Cost,
                    card.Quality,
                    card.FoilStyle,
                    card.Element,
                    IconOf(card.Element),
                    ArtOf(card)
                );

        /// <summary>
        /// One tile of the selection mode: the card's art, darkened under a lock for a unique
        /// skill not unlocked yet, or 「＋」 for an empty slot.
        /// </summary>
        public static void ShowTile(CardLoadoutTileWidget widget, TrainingCardState card)
        {
            bool filled = card != null && card.Filled;
            bool locked = filled && card.Locked;
            if (widget.Art != null)
            {
                widget.Art.texture = card?.Art;
                widget.Art.enabled = filled && card.Art != null;
                widget.Art.color = locked ? LockedArt : Color.white;
            }
            if (widget.Lock != null)
                widget.Lock.SetActive(locked);
            if (widget.Empty != null)
                widget.Empty.SetActive(!filled);
        }

        private static string UnlockText(int level) => $"Lv {level}で解放";

        // 右の進化ツリー。取得した段階は名前と効果、取得していない段階は封印だけを出す。
        // 別の人か別の固有スキルのツリーを出すときは、一番上から見せる。
        private void ShowTreeState(CardLoadoutTreeState tree, string person)
        {
            string key = person + "/" + tree.Unique;
            if (key != shownTree && Tree.Scroll != null)
                Tree.Scroll.verticalNormalizedPosition = 1f;
            shownTree = key;
            Set(Tree.Title, $"{tree.Name}の覚醒");
            for (int i = 0; i < Tree.Stages.Length; i++)
            {
                var stage = i < tree.Stages.Count ? tree.Stages[i] : null;
                ShowStage(
                    Tree.Stages[i],
                    stage,
                    tree,
                    ArtOf(stage?.Card?.Id),
                    IconOf(stage?.Card?.Element ?? CardElement.None)
                );
            }
        }

        /// <summary>
        /// Shows one stage of the tree. The generator calls it too, so the prefab bakes the first
        /// person's first tree for the stopped editor's preview.
        /// </summary>
        public static void ShowStage(
            CardLoadoutStageWidget widget,
            CardLoadoutStageState stage,
            CardLoadoutTreeState tree,
            Texture art,
            Sprite element
        )
        {
            bool acquired = stage != null && stage.Acquired;
            bool locked = tree != null && tree.Locked;
            ShowSlot(
                widget.Card,
                acquired ? stage.Card : null,
                art,
                element,
                acquired && stage.Set
            );
            if (widget.Shadow != null)
                widget.Shadow.SetActive(!acquired);
            if (widget.Content != null)
                widget.Content.SetActive(acquired);
            // 取得した段階だけ押せる（未解放の固有スキルは付け替えられない）。
            if (widget.Card.Button != null)
                widget.Card.Button.interactable = acquired && !locked;
            if (widget.Card.Art != null)
                widget.Card.Art.color = locked ? LockedArt : Color.white;
            // 鍵のかかったカードは光らせない。
            if (widget.Card.Foil != null && locked)
                widget.Card.Foil.Image.enabled = false;
            if (widget.Lock != null)
                widget.Lock.SetActive(acquired && locked);
            Set(
                widget.Mark,
                !acquired ? ""
                    : locked ? UnlockText(tree.UnlockLevel)
                    : stage.Set ? SetMark
                    : ""
            );
        }

        // 今出している進化ツリー（人と固有スキル）。
        private string shownTree;

        // ツリーでセットしている段階の印。
        public const string SetMark = "セット中";

        public Sprite IconOf(CardElement element)
        {
            int index = (int)element;
            return element != CardElement.None && index < ElementIcons.Length
                ? ElementIcons[index]
                : null;
        }

        private void BindRow(CardLoadoutRowWidget row) =>
            bindings.Bind(row.Button, () => CardPressed?.Invoke(row.Id));

        // 選べるカードを、上から順に行へ入れる。行が足りなければ1行目を写して増やす。
        private void ShowRows(CardLoadoutState state)
        {
            EnsureRows(state.Choices.Count);
            for (int i = 0; i < Rows.Length; i++)
            {
                var row = Rows[i];
                if (row.Button == null)
                    continue;
                bool on = i < state.Choices.Count;
                row.Button.gameObject.SetActive(on);
                if (!on)
                    continue;
                var card = state.Choices[i];
                row.Id = card.Id;
                row.Button.transform.SetSiblingIndex(i);
                ShowRow(row, card, ArtOf(card), IconOf(card.Element));
            }
        }

        private void EnsureRows(int count)
        {
            if (Rows.Length == 0 || Rows.Length >= count || Rows[0].Button == null)
                return;
            var rows = new List<CardLoadoutRowWidget>(Rows);
            var template = Rows[0];
            var source = template.Button.gameObject;
            while (rows.Count < count)
            {
                var copy = Instantiate(source, source.transform.parent);
                copy.name = "Card_" + rows.Count;
                var widget = new CardLoadoutRowWidget
                {
                    Button = copy.GetComponent<Button>(),
                    Art = Same(template.Art, source, copy),
                    Foil = Same(template.Foil, source, copy),
                    Cost = Same(template.Cost, source, copy),
                    Element = Same(template.Element, source, copy),
                    Name = Same(template.Name, source, copy),
                    Kind = Same(template.Kind, source, copy),
                    Description = Same(template.Description, source, copy),
                    Mark = Same(template.Mark, source, copy),
                };
                rows.Add(widget);
                BindRow(widget);
            }
            Rows = rows.ToArray();
        }

        // 写した行の中で、元の行の part と同じ位置にある部品。
        private static T Same<T>(T part, GameObject source, GameObject copy)
            where T : Component
        {
            if (part == null)
                return null;
            var path = new List<int>();
            for (var at = part.transform; at != null && at != source.transform; at = at.parent)
                path.Insert(0, at.GetSiblingIndex());
            var found = copy.transform;
            foreach (int index in path)
                found = found.GetChild(index);
            return found.GetComponent<T>();
        }

        /// <summary>
        /// Shows a card (or an empty slot) in a slot. The generator calls it too, so the prefab
        /// shows the first person's cards in the editor.
        /// </summary>
        public static void ShowSlot(
            CardLoadoutSlotWidget widget,
            CardLoadoutCardState card,
            Texture art,
            Sprite element,
            bool selected
        )
        {
            bool filled = card != null && card.Id != null;
            if (widget.Selected != null)
                widget.Selected.SetActive(selected);
            if (widget.Art != null)
            {
                widget.Art.texture = art;
                widget.Art.enabled = filled && art != null;
            }
            if (widget.Foil != null)
                widget.Foil.Show(
                    art,
                    filled ? card.Quality : ItemQuality.C,
                    filled ? card.FoilStyle : 0
                );
            if (widget.Cost != null)
            {
                widget.Cost.transform.parent.gameObject.SetActive(filled);
                widget.Cost.text = filled ? card.Cost.ToString() : "";
            }
            if (widget.Element != null)
            {
                widget.Element.sprite = element;
                widget.Element.enabled = filled && element != null;
            }
            Set(widget.Name, card?.Name ?? "空き");
            Set(widget.Kind, filled ? card.KindLine : "");
            Set(widget.Description, filled ? card.Description : "");
        }

        /// <summary>
        /// One card on the right: its art, cost, element, name and kind, the description with the
        /// person's numbers, and the mark (which slot of theirs holds it, who else uses it, or how
        /// many items there are). A card that cannot be set now is dimmed.
        /// </summary>
        public static void ShowRow(
            CardLoadoutRowWidget widget,
            CardLoadoutCardState card,
            Texture art,
            Sprite element
        )
        {
            if (widget.Art != null)
            {
                widget.Art.texture = art;
                widget.Art.enabled = art != null;
            }
            if (widget.Foil != null)
                widget.Foil.Show(art, card.Quality, card.FoilStyle);
            Set(widget.Cost, card.Cost.ToString());
            if (widget.Element != null)
            {
                widget.Element.sprite = element;
                widget.Element.enabled = element != null;
            }
            Set(widget.Name, card.Name);
            Set(widget.Kind, card.KindLine);
            Set(widget.Description, card.Description);
            if (widget.Mark != null)
            {
                widget.Mark.gameObject.SetActive(!string.IsNullOrEmpty(card.Mark));
                widget.Mark.text = card.Mark ?? "";
            }
            if (widget.Button != null)
                widget.Button.interactable = !card.Unavailable;
        }

        /// <summary>The icons of the elements the person can set, with the unused icons hidden.</summary>
        public static void ShowUsable(
            Image[] icons,
            IReadOnlyList<CardElement> usable,
            Func<CardElement, Sprite> icon
        )
        {
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null)
                    continue;
                var sprite = i < usable.Count ? icon(usable[i]) : null;
                icons[i].sprite = sprite;
                icons[i].gameObject.SetActive(sprite != null);
            }
        }

        // スキルの挿絵（固有スキルの段階を含む）は仮データの挿絵を使う。
        private Texture2D ArtOf(string skillId) =>
            skillId != null && Party != null ? Party.ArtOf(skillId) : null;

        // カードの絵。アイテムはアイテムの絵、スキルはスキルの挿絵。
        private Texture2D ArtOf(CardLoadoutCardState card) =>
            card == null ? null
            : card.ItemId != null ? (ItemArt != null ? ItemArt.TextureOf(card.ItemId) : null)
            : ArtOf(card.ArtId);
    }
}
