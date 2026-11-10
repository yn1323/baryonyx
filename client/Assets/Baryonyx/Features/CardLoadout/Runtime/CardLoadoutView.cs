using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Equipment;
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

        // カードの挿絵（2倍）と、その上に重ねるキラキラ。
        public RawImage Art;
        public CardFoil Foil;

        // 行の右上のコストの札（数字は0〜9を横に並べた絵から切り出す）と属性の印。
        public GameObject Cost;
        public RawImage CostDigit;
        public Image Element;
        public TMP_Text Name;
        public TMP_Text Kind;
        public TMP_Text Description;
    }

    /// <summary>
    /// One of the person's six cards in the left frame (a unique skill or a custom skill's
    /// slot), as a row: the art, the name with the cost and element at the top right, the kind
    /// and the whole effect under the name. Pressing it chooses that slot; a unique skill not
    /// unlocked yet is dimmed under a lock with its level in place of the effect, and an empty
    /// slot shows only 「空き」.
    /// </summary>
    [Serializable]
    public sealed class CardLoadoutDeckRowWidget
    {
        public CardLoadoutSlotWidget Card = new();

        // 絵・名前・効果。空いた枠では隠して「空き」を出す。
        public GameObject Content;
        public GameObject Empty;

        // 未解放の固有スキルの錠前と、効果の代わりに出す解放するLv（「Lv 30で解放」）。
        public GameObject Lock;
        public TMP_Text When;
    }

    /// <summary>One stage of the evolution tree on the right, baked by the generator.</summary>
    [Serializable]
    public sealed class CardLoadoutStageWidget
    {
        public CardLoadoutSlotWidget Card = new();

        // 取得していない段階の封印（形だけ見せる）と、そのときに隠す中身。
        public GameObject Shadow;
        public GameObject Content;

        // 未解放の固有スキルの最初の段階に重ねる錠前と、右上の「E」（セットしている段階）「Lv 20で解放」。
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

        // 行の右上のコストの札と属性の印。
        public GameObject Cost;
        public RawImage CostDigit;
        public Image Element;
        public TMP_Text Name;
        public TMP_Text Kind;

        // 説明の数字は選んでいる人のステータスで変わる。
        public TMP_Text Description;

        // 属性の左の印：使っていれば「E」（その人は金、ほかの人は灰色）、アイテムは「所持 3」。なければ隠す。
        public TMP_Text Mark;
    }

    /// <summary>
    /// The adventurer's card skills tab of the formation over the guide screen, between the
    /// people's column and the tabs (<see cref="PartySheet"/>): the person's six cards (three
    /// unique skills and three custom skills) as rows with their whole effects on the left, one
    /// of them always chosen; on the right, the skill and item tabs and the cards that can go in
    /// the chosen custom skill's slot, or the evolution tree of the unique skill chosen.
    /// CardLoadoutAssets bakes the parts and rows into the formation prefab,
    /// so they read in the editor; this view wires them up and drives its own presenter. When
    /// the app has a server (<see cref="PartySession.Source"/>), it reads the party on opening
    /// and saves every change there; otherwise it uses the mock data. Back returns to the
    /// adventurers' list. Changes are told on the guide screen's notice band.
    /// </summary>
    public sealed partial class CardLoadoutView
        : GuidePanel,
            ICardLoadoutView,
            IGuideBackHandler,
            IGuideBreadcrumb,
            IEditModePreview
    {
        public const string LoadFailedMessage = "スキルを取得できませんでした";

        // 読み込む間に右の一覧で伏せて並べる行の数（一覧の見える所）。左の6行もすべて伏せる。
        public const int CoverRows = 5;

        // まだ解放していない固有スキルの挿絵の色。
        private static readonly Color LockedArt = new(0.25f, 0.25f, 0.3f, 1f);

        // ほかの人が付けているカードの「E」の色（その人が付けているカードは金）。
        internal static readonly Color OtherMark = UiPalette.TextFaint;

        public PartyMockData Party;
        public TrainingMockData Training;

        // アイテムの絵（種類のIDで引く）。
        public ItemArt ItemArt;

        // 左の人の列と右のタブ。
        public PartySheet Sheet;

        // 左の面の上の左右のフリックで人を替える（ステータスのタブと同じ）。
        public GuideSwipe Swipe;

        // 左の6行：固有スキル3つと、カスタムスキルの3つの枠。押すとその枠を選ぶ。
        public CardLoadoutDeckRowWidget[] UniqueRows = Array.Empty<CardLoadoutDeckRowWidget>();
        public CardLoadoutDeckRowWidget[] SlotRows = Array.Empty<CardLoadoutDeckRowWidget>();

        // 左の6行の縦のスクロール（6行が面に収まらないため）。
        public ScrollRect DeckScroll;

        // 固有スキルを選んでいるときに、タブと一覧の代わりに右に出す進化ツリー。
        public CardLoadoutTreeWidget Tree = new();

        // 冒険者の「覚醒」のタブとして開くときの、覚醒先を選んで覚醒させる部品（スキルのタブではnull）。
        // 右にはタブと一覧を置かず、固有スキルもカスタムスキルもツリーで出す。
        public CardLoadoutAwakening Awakening;

        // 右の上のスキル・アイテムのタブ（CardLoadoutPresenter.SkillTab・ItemTab の順）。
        public GuideTab[] Tabs = Array.Empty<GuideTab>();

        // タブの下の、その人が付けられる属性の行と、そのアイコン（2つまで）。スキルのタブだけで出す。
        public GameObject UsableLine;
        public Image[] Usable = Array.Empty<Image>();

        // 読み込み中と読めなかったとき、アイテムのタブを選んでいるときだけ一覧の面に出す文字。
        public TMP_Text Status;
        public CardLoadoutRowWidget[] Rows = Array.Empty<CardLoadoutRowWidget>();

        // 一覧の最後の「スキルを外す」。カスタムスキルの枠に何か入っているときだけ出す。
        public Button Remove;
        public ScrollRect List;

        // CardElement の順の属性のアイコン。属性なしはnull。
        public Sprite[] ElementIcons = Array.Empty<Sprite>();

        private CardLoadoutPresenter presenter;
        private readonly ButtonBindings bindings = new();
        private readonly GlassCardCover cover = new();

        // 付け替えた左の行のカードを縦に回す（固有スキルの3行、続けてカスタムスキルの3行の順）。
        private GlassCardTurns turns;

        public event Action PrevPressed;
        public event Action NextPressed;
        public event Action<int> SlotPressed;
        public event Action<int> TabPressed;
        public event Action<string> CardPressed;
        public event Action<int> UniquePressed;
        public event Action<int> StagePressed;

        public CardLoadoutPresenter Presenter => presenter;

        private void OnEnable()
        {
            person = null;
            shownTree = null;
            Turns().Forget();
            if (Swipe != null)
                Swipe.Flicked += Flick;
            if (Sheet != null)
                Sheet.PersonPressed += Choose;
            for (int i = 0; i < SlotRows.Length; i++)
            {
                int index = i;
                bindings.Bind(SlotRows[i].Card.Button, () => SlotPressed?.Invoke(index));
            }
            for (int i = 0; i < UniqueRows.Length; i++)
            {
                int index = i;
                bindings.Bind(UniqueRows[i].Card.Button, () => UniquePressed?.Invoke(index));
            }
            // 覚醒のタブでは、ツリーの段階を押すと覚醒先を選ぶ（CardLoadoutAwakening）。セットし直さない。
            for (int i = 0; i < Tree.Stages.Length && Awakening == null; i++)
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
            BindRemove();
            BindCards();

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

        private void ShowLoadFailed()
        {
            cover.Clear();
            HideRows();
            Set(Status, LoadFailedText);
        }

        private void Present(PartyFormation formation, Func<string, int, string, Task> save)
        {
            shownFormation = formation;
            presenter?.Dispose();
            var (slot, unique) =
                Awakening != null
                    ? CardLoadoutAwakening.Opened()
                    : (CardLoadoutSession.OpenSlot, CardLoadoutSession.OpenUnique);
            presenter = new CardLoadoutPresenter(
                this,
                CardLoadoutPresenter.People(formation),
                new CardLoadoutSessionStore(Party, Training),
                save,
                slot,
                unique: unique
            );
        }

        /// <summary>Shows the person again from the party as it is now (after an awakening).</summary>
        public void Refresh() => presenter?.Refresh();

        /// <summary>Reads the party again and shows it, keeping what is shown until it is read.</summary>
        public void Reload()
        {
            var source = PartySession.Source;
            if (source == null || Party == null)
            {
                Refresh();
                return;
            }
            StartLoad(
                token => LoadAsync(source, false, token),
                LoadFailedMessage,
                () => { },
                readAgain: true
            );
        }

        // 読み込むまで、仮データのカードを本当のカードとして見せない。左の6行と右の一覧の先頭はカードの裏で埋める。
        private void ShowLoading()
        {
            Turns().Forget();
            ShowTree(false);
            if (Awakening != null)
                Awakening.Hide();
            ShowTabs(CardLoadoutPresenter.SkillTab);
            Set(Status, "");
            if (UsableLine != null)
                UsableLine.SetActive(false);
            foreach (var row in UniqueRows.Concat(SlotRows))
                ShowDeckRow(row, null, false, false, 0);
            ShowCardsLoading();
            ShowUsable(Usable, Array.Empty<CardElement>(), IconOf);
            HideRows();
            if (Remove != null)
                Remove.gameObject.SetActive(false);
            cover.Cover(
                UniqueRows
                    .Concat(SlotRows)
                    .Select(row => row.Card.Button)
                    .Concat(Rows.Take(CoverRows).Select(row => row.Button))
                    .Where(button => button != null)
                    .Select(button => button.gameObject)
            );
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

        // 停止中の表示の切り替え（doc/rules/ui-design.md#停止中の表示の切り替え）。最初の表示は、
        // 1つ目のカスタムスキルの枠を選んだスキルのタブ。
        public const string TreePreviewState = "固有スキルを選んだとき";

        public IReadOnlyList<string> PreviewStates => (
                Awakening != null ? new[] { CardLoadoutAwakening.CardPreviewState }
                : HasCards ? CardPreviewStates
                : new[] { "アイテム", TreePreviewState }
            ).Concat(GuidePanelLoad.PreviewStates).ToArray();

        public void ShowPreviewState(int index)
        {
            // 覚醒のタブ：最初の表示は焼き込んだ1つ目の固有スキルのツリー。次に1つ目のカスタムスキル。
            if (Awakening != null)
            {
                if (index == 0)
                {
                    ShowChosen(-1, 0);
                    Awakening.ShowCardPreview();
                    return;
                }
                index++;
            }
            // スキルのタブ：1つ目のカスタムスキルの枠の候補、1つ目の固有スキルのツリー、読み込みの順。
            else if (HasCards)
            {
                if (index < CardPreviewStates.Length)
                {
                    ShowPickerPreview(index == 1);
                    return;
                }
                index = index - CardPreviewStates.Length + 2;
            }
            switch (index)
            {
                case 0:
                    // 左は作り込んだ先頭の人のまま、右だけアイテムのタブにする。
                    ShowTabs(CardLoadoutPresenter.ItemTab);
                    HideRows();
                    return;
                case 1:
                    // 生成時に作り込んだ、先頭の人の1つ目の固有スキルの進化ツリー。
                    ShowChosen(0, -1);
                    ShowTree(true);
                    return;
            }
            ShowLoading();
            if (index == 3)
                ShowLoadFailed();
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
            UnbindCards();
            if (Swipe != null)
                Swipe.Flicked -= Flick;
            if (Sheet != null)
                Sheet.PersonPressed -= Choose;
            presenter?.Dispose();
            presenter = null;
        }

        // 左の列で押した人を出す。選んでいる枠とタブはそのまま。
        private void Choose(string id) => presenter?.Select(id);

        // パンくずの「冒険者 › アリア」。どのタブかはタブでわかる。読み込むまでは人の名前を出さない。
        public IReadOnlyList<string> Trail => TrainingSession.Trail(Guide, person);

        // パンくずに出す、表示している人の名前。
        private string person;

        // 戻ると、タブを開いた項目（冒険者の一覧か育成の人選び）へ戻る。覚醒の演出の間は戻らない。
        // ほかの人のスキルを付け替えるかを確かめる窓が開いていれば、窓だけを閉じる（選ぶ一覧は残す）。
        public bool HandleBack() =>
            (Awakening != null && Awakening.Busy)
            || (Dialog != null && Dialog.HandleBack())
            || ClosePicker()
            || TrainingSession.LeavePage(Guide);

        public void Render(CardLoadoutState state)
        {
            var member = Party != null ? Party.Find(state.PersonId) : null;
            person = member?.Name;
            if (Guide != null)
                Guide.RefreshTrail();
            if (Application.isPlaying)
            {
                CardLoadoutSession.LastUnique = state.Unique;
                CardLoadoutSession.LastSlot = state.Slot;
            }
            if (Sheet != null && Party != null)
            {
                var (people, partyCount) = PartySession.Formation(Party).TabOrder();
                Sheet.Show(people, partyCount, state.PersonId);
            }
            // 同じ人の行の中身が変わったら、描き替える前のカードを写して縦に回す。
            var turning = Turns();
            turning.Begin(state.PersonId);
            for (int i = 0; i < UniqueRows.Length; i++)
            {
                var unique =
                    state.Uniques != null && i < state.Uniques.Count ? state.Uniques[i] : null;
                turning.Show(
                    i,
                    CardOf(UniqueRows[i]),
                    TurnKey(unique?.Card, unique?.Card != null, unique != null && unique.Locked),
                    unique?.Card != null ? ArtOf(unique.Card) : null
                );
                ShowDeckRow(
                    UniqueRows[i],
                    unique?.Card,
                    unique?.Card != null,
                    unique != null && unique.Locked,
                    unique?.UnlockLevel ?? 0
                );
                DimForAwakening(UniqueRows[i], unique?.Card != null && !unique.Locked);
            }
            for (int i = 0; i < SlotRows.Length; i++)
            {
                var card = i < state.Slots.Count ? state.Slots[i] : null;
                bool filled = card != null && card.Id != null;
                turning.Show(
                    UniqueRows.Length + i,
                    CardOf(SlotRows[i]),
                    TurnKey(card, filled, false),
                    filled ? ArtOf(card) : null
                );
                ShowDeckRow(SlotRows[i], card, filled, false, 0);
                DimForAwakening(SlotRows[i], filled && card.ItemId == null);
            }
            if (HasCards)
                ShowCards(state, turning);
            ShowChosen(state.Unique, state.Slot);
            if (Awakening != null)
                Awakening.Show(state);
            else
            {
                ShowUsable(Usable, state.Usable, IconOf);
                ShowTabs(state.Tab, state.Choices.Count == 0);
                ShowRows(state);
                if (state.Tree != null)
                    ShowTreeState(state.Tree, state.PersonId);
                else
                    shownTree = null;
                ShowTree(state.Tree != null);
            }
            // 読み込みを待っていたら、左の6行から右の一覧の順にめくる。一覧の伏せた場所のうち、
            // カードも「スキルを外す」も来ない場所の行は、めくってから消す（外すの後ろへ回す）。
            int taken =
                state.Choices.Count + (Remove != null && Remove.gameObject.activeSelf ? 1 : 0);
            var empty = cover.Covered
                ? Rows.Skip(state.Choices.Count)
                    .Take(CoverRows - taken)
                    .Where(row => row.Button != null)
                    .Select(row => row.Button.gameObject)
                    .ToArray()
                : Array.Empty<GameObject>();
            foreach (var row in empty)
                row.transform.SetAsLastSibling();
            cover.Reveal(
                UniqueRows
                    .Concat(SlotRows)
                    .Select(row => row.Card.Button)
                    .Concat(Rows.Take(state.Choices.Count).Select(row => row.Button))
                    .Where(button => button != null)
                    .Select(button => button.gameObject),
                empty
            );
        }

        // 付け替えた行を回す部品。線の光は、新しく入れたカードの挿絵の色から取る。
        // スキルのタブの6枚（戦闘のカード）は縦の軸で回す（2026-11-03、ユーザーの指示「水平方向で」）。
        private GlassCardTurns Turns() =>
            turns ??= new GlassCardTurns(
                transform,
                art => Combat.Presentation.CardArtColor.Of(art, Color.white),
                HasCards
            );

        // 覚醒のタブで、覚醒させられない行（未解放の固有スキル、空いた枠、アイテム）を薄くする。押すことはできる。
        private void DimForAwakening(CardLoadoutDeckRowWidget row, bool awakens)
        {
            if (Awakening != null && CardOf(row) is { } card)
                card.Dimmed = !awakens;
        }

        private static GlassCard CardOf(CardLoadoutDeckRowWidget row) =>
            row.Card.Button != null && row.Card.Button.TryGetComponent(out GlassCard card)
                ? card
                : null;

        // 行が見せている物。カード（覚醒の段階で名前が変わる）、空き、未解放を区別する。
        private static string TurnKey(CardLoadoutCardState card, bool filled, bool locked) =>
            locked ? "locked"
            : filled ? $"{card.Id}/{card.Name}/{card.ItemId}"
            : card != null ? "empty"
            : "";

        // 左の6行のうち、選んでいる固有スキル（unique）かカスタムスキルの枠（slot）を金の枠で囲む。
        private void ShowChosen(int unique, int slot)
        {
            for (int i = 0; i < UniqueRows.Length; i++)
                if (UniqueRows[i].Card.Selected != null)
                    UniqueRows[i].Card.Selected.SetActive(i == unique);
            for (int i = 0; i < SlotRows.Length; i++)
                if (SlotRows[i].Card.Selected != null)
                    SlotRows[i].Card.Selected.SetActive(i == slot);
        }

        /// <summary>
        /// One of the six rows on the left: <paramref name="card"/>'s art, name, cost, element,
        /// kind and effect (<paramref name="filled"/>), or 「空き」; a unique skill not unlocked
        /// yet is dimmed under a lock, with its level in place of the effect.
        /// </summary>
        private void ShowDeckRow(
            CardLoadoutDeckRowWidget widget,
            CardLoadoutCardState card,
            bool filled,
            bool locked,
            int unlockLevel
        )
        {
            var selected = widget.Card.Selected != null && widget.Card.Selected.activeSelf;
            ShowSlot(
                widget.Card,
                filled ? card : null,
                filled ? ArtOf(card) : null,
                filled ? IconOf(card.Element) : null,
                selected
            );
            if (widget.Content != null)
                widget.Content.SetActive(filled);
            if (widget.Empty != null)
                widget.Empty.SetActive(!filled && card != null);
            if (widget.Card.Art != null)
                widget.Card.Art.color = locked ? LockedArt : Color.white;
            // 鍵のかかったカードは光らせない。
            if (widget.Card.Foil != null && locked)
                widget.Card.Foil.Image.enabled = false;
            if (widget.Card.Description != null)
                widget.Card.Description.gameObject.SetActive(!locked);
            if (widget.Lock != null)
                widget.Lock.SetActive(locked);
            Set(widget.When, locked ? UnlockText(unlockLevel) : "");
        }

        internal static string UnlockText(int level) => $"Lv {level}で解放";

        // 右の進化ツリー。取得した段階は名前と効果、取得していない段階は封印だけを出す。
        // 別の人か別の固有スキルのツリーを出すときは、一番上から見せる。
        private void ShowTreeState(CardLoadoutTreeState tree, string person)
        {
            string key = person + "/" + tree.Unique;
            if (key != shownTree && Tree.Scroll != null)
                Tree.Scroll.verticalNormalizedPosition = 1f;
            shownTree = key;
            ShowTreeTitle(Tree, tree.Name);
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
            string mark =
                !acquired ? ""
                : locked ? UnlockText(tree.UnlockLevel)
                : stage.Set ? SetMark
                : "";
            Set(widget.Mark, mark);
            // 印は右上で属性の左に並ぶため、ないときは隠して間を詰める。
            if (widget.Mark != null)
                widget.Mark.gameObject.SetActive(mark.Length > 0);
        }

        /// <summary>The tree's heading: 「マナバーストの覚醒」.</summary>
        public static void ShowTreeTitle(CardLoadoutTreeWidget tree, string name) =>
            Set(tree.Title, $"{name}の覚醒");

        // 今出している進化ツリー（人と固有スキル）。
        private string shownTree;

        // ツリーでセットしている段階の印。右の一覧で付けているカードの印とそろえる。
        public const string SetMark = CardLoadoutPresenter.EquippedMark;

        public Sprite IconOf(CardElement element)
        {
            int index = (int)element;
            return index < ElementIcons.Length ? ElementIcons[index] : null;
        }

        private void BindRow(CardLoadoutRowWidget row) =>
            bindings.Bind(row.Button, () => CardPressed?.Invoke(row.Id));

        private void BindRemove() =>
            bindings.Bind(Remove, () => CardPressed?.Invoke(CardLoadoutPresenter.RemoveKey));

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
            if (Remove != null)
            {
                Remove.gameObject.SetActive(state.CanRemove);
                Remove.transform.SetAsLastSibling();
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
                    Cost =
                        template.Cost != null
                            ? Same(template.Cost.transform, source, copy).gameObject
                            : null,
                    CostDigit = Same(template.CostDigit, source, copy),
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
            // キラキラは、挿絵と同じ所を切り出して重ねる（左の6行は挿絵の左右を切っている）。
            if (widget.Foil != null)
                widget.Foil.Show(
                    art,
                    widget.Art != null ? widget.Art.uvRect : new Rect(0f, 0f, 1f, 1f),
                    filled ? card.Quality : ItemQuality.C,
                    filled ? card.FoilStyle : 0
                );
            ShowCost(widget.Cost, widget.CostDigit, filled ? card.Cost : -1);
            ShowElement(widget.Element, filled ? element : null);
            Set(widget.Name, card?.Name ?? "空き");
            Set(widget.Kind, filled ? card.KindLine : "");
            Set(widget.Description, filled ? card.Description : "");
        }

        // 右の一覧の絵の1ドットの画素数（設計座標）。スキルの挿絵は2倍、アイテムの絵は3倍。
        public const float ArtDot = 2f;
        public const float ItemArtDot = 3f;

        // コストの札の数字の絵は、0〜9を横に並べた1枚。
        private const int CostDigits = 10;

        /// <summary>
        /// The cost plate at a row's top right, its digit cut from the 0–9 strip. Hidden for a
        /// value outside 0–9 (an empty slot). The generator calls it too.
        /// </summary>
        public static void ShowCost(GameObject cost, RawImage digit, int value)
        {
            bool on = value >= 0 && value < CostDigits;
            if (cost != null)
                cost.SetActive(on);
            if (digit != null && on)
                digit.uvRect = new Rect(value / (float)CostDigits, 0f, 1f / CostDigits, 1f);
        }

        /// <summary>
        /// The element at a row's top right. A card without one hides it, closing the gap next to
        /// the other marks. The generator calls it too.
        /// </summary>
        public static void ShowElement(Image icon, Sprite element)
        {
            if (icon == null)
                return;
            icon.sprite = element;
            icon.gameObject.SetActive(element != null);
        }

        /// <summary>
        /// One card on the right: its art, cost, element, name and kind, the description with the
        /// person's numbers, and the mark (「E」 in gold when the person holds it, in grey when
        /// someone else does, or how many items there are). A card that cannot be set now is
        /// dimmed.
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
                // アイテムの絵（32×32）はスキルの挿絵（64×58）より小さいため、1ドットを大きく出す。
                var fit = widget.Art.GetComponent<PixelPerfectRawImage>();
                if (fit != null)
                    fit.DotSize = card.ItemId != null ? ItemArtDot : ArtDot;
            }
            if (widget.Foil != null)
                widget.Foil.Show(art, card.Quality, card.FoilStyle);
            ShowCost(widget.Cost, widget.CostDigit, card.Cost);
            ShowElement(widget.Element, element);
            Set(widget.Name, card.Name);
            Set(widget.Kind, card.KindLine);
            Set(widget.Description, card.Description);
            if (widget.Mark != null)
            {
                widget.Mark.gameObject.SetActive(!string.IsNullOrEmpty(card.Mark));
                widget.Mark.text = card.Mark ?? "";
                widget.Mark.color = card.HeldByOther ? OtherMark : UiPalette.Gold;
            }
            if (widget.Button != null)
            {
                widget.Button.interactable = !card.Unavailable;
                if (widget.Button.TryGetComponent(out GlassCard glass))
                    glass.Interactable = !card.Unavailable;
            }
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
        internal Texture2D ArtOf(string skillId) =>
            skillId != null && Party != null ? Party.ArtOf(skillId) : null;

        // カードの絵。アイテムはアイテムの絵、スキルはスキルの挿絵。
        private Texture2D ArtOf(CardLoadoutCardState card) =>
            card == null ? null
            : card.ItemId != null ? (ItemArt != null ? ItemArt.TextureOf(card.ItemId) : null)
            : ArtOf(card.ArtId);
    }
}
