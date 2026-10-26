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
    /// One of the person's six cards in the left frame (a unique skill or a custom skill's
    /// slot), as a row like the list's: the art, the name and kind and the whole effect. Pressing
    /// it chooses that slot; a unique skill not unlocked yet is dimmed under a lock with its
    /// level, and an empty slot shows only 「空き」.
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

        // 左の人の列と右のタブ。
        public PartySheet Sheet;

        // 左の面の上の左右のフリックで人を替える（ステータスのタブと同じ）。
        public GuideSwipe Swipe;

        // 左の6行：固有スキル3つと、カスタムスキルの3つの枠。押すとその枠を選ぶ。
        public CardLoadoutDeckRowWidget[] UniqueRows = Array.Empty<CardLoadoutDeckRowWidget>();
        public CardLoadoutDeckRowWidget[] SlotRows = Array.Empty<CardLoadoutDeckRowWidget>();

        // 固有スキルを選んでいるときに、タブと一覧の代わりに右に出す進化ツリー。
        public CardLoadoutTreeWidget Tree = new();

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
            BindRemove();

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
            foreach (var row in UniqueRows.Concat(SlotRows))
                ShowDeckRow(row, null, false, false, 0);
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

        // 停止中の表示の切り替え（doc/rules/ui-design.md#停止中の表示の切り替え）。最初の表示は、
        // 1つ目のカスタムスキルの枠を選んだスキルのタブ。
        public const string TreePreviewState = "固有スキルを選んだとき";

        public IReadOnlyList<string> PreviewStates =>
            new[] { "アイテム", TreePreviewState }.Concat(GuidePanelLoad.PreviewStates).ToArray();

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

        // 戻ると、タブを開いた項目（冒険者の一覧か育成の人選び）へ戻る。
        public bool HandleBack() => TrainingSession.LeavePage(Guide);

        public void Render(CardLoadoutState state)
        {
            var member = Party != null ? Party.Find(state.PersonId) : null;
            person = member?.Name;
            if (Guide != null)
                Guide.RefreshTrail();
            if (Sheet != null && Party != null)
            {
                var (people, partyCount) = PartySession.Formation(Party).TabOrder();
                Sheet.Show(people, partyCount, state.PersonId);
            }
            for (int i = 0; i < UniqueRows.Length; i++)
            {
                var unique =
                    state.Uniques != null && i < state.Uniques.Count ? state.Uniques[i] : null;
                ShowDeckRow(
                    UniqueRows[i],
                    unique?.Card,
                    unique?.Card != null,
                    unique != null && unique.Locked,
                    unique?.UnlockLevel ?? 0
                );
            }
            for (int i = 0; i < SlotRows.Length; i++)
            {
                var card = i < state.Slots.Count ? state.Slots[i] : null;
                ShowDeckRow(SlotRows[i], card, card != null && card.Id != null, false, 0);
            }
            ShowChosen(state.Unique, state.Slot);
            ShowUsable(Usable, state.Usable, IconOf);
            ShowTabs(state.Tab, state.Choices.Count == 0);
            ShowRows(state);
            if (state.Tree != null)
                ShowTreeState(state.Tree, state.PersonId);
            else
                shownTree = null;
            ShowTree(state.Tree != null);
        }

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
        /// One of the six rows on the left: <paramref name="card"/>'s art, name, kind and effect
        /// (<paramref name="filled"/>), or 「空き」; a unique skill not unlocked yet is dimmed
        /// under a lock, with its level in place of the effect.
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

        // 右の一覧の絵の1ドットの画素数（設計座標）。スキルの挿絵は2倍、アイテムの絵は3倍。
        public const float ArtDot = 2f;
        public const float ItemArtDot = 3f;

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
                // アイテムの絵（32×32）はスキルの挿絵（64×58）より小さいため、1ドットを大きく出す。
                var fit = widget.Art.GetComponent<PixelPerfectRawImage>();
                if (fit != null)
                    fit.DotSize = card.ItemId != null ? ItemArtDot : ArtDot;
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
