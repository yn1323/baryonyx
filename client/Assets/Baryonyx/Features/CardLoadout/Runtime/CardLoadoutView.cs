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
    /// <summary>One of the person's two custom skill slots on the left, baked by the generator.</summary>
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
    /// One of the person's two unique skills above the custom skills, laid out like a custom
    /// skill's slot: the card of the stage set. Pressing it shows its evolution tree on the right.
    /// </summary>
    [Serializable]
    public sealed class CardLoadoutUniqueWidget
    {
        public CardLoadoutSlotWidget Card = new();

        // 未解放のときに挿絵へ重ねる錠前と、右上の「Lv 20で解放」。
        public GameObject Lock;
        public TMP_Text Mark;
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

    /// <summary>One card's row on the right, baked by the generator.</summary>
    [Serializable]
    public sealed class CardLoadoutRowWidget
    {
        public string Id = "";
        public Button Button;
        public RawImage Art;

        // 説明の数字は選んでいる人のステータスで変わる。
        public TMP_Text Description;

        // その人の何枚目に入っているか（「1枚目」）。入っていなければ隠す。
        public TMP_Text Mark;
    }

    /// <summary>
    /// The formation's card skills change screen over the guide screen, laid out like the
    /// equipment's: the person (◀ ▶, figure, name and level), their stats, their two unique
    /// skills and their two custom skills on the left; the skill and item tabs and the cards
    /// they can set on the right, or the evolution tree of the unique skill chosen on the left.
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

        // 左上の人（立ち姿・名前・Lv）。
        public PartyPersonHeader Person = new();

        // 左の面の上の左右のフリックで人を替える（個別の画面と同じ）。
        public GuideSwipe Swipe;
        public CardLoadoutSlotWidget[] Slots = Array.Empty<CardLoadoutSlotWidget>();

        // デッキのほかの2枚（固有スキル）。押すと右に進化ツリーを出す。
        public CardLoadoutUniqueWidget[] Uniques = Array.Empty<CardLoadoutUniqueWidget>();

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

        public CardLoadoutPresenter Presenter => presenter;

        private void OnEnable()
        {
            person = null;
            shownTree = null;
            if (Swipe != null)
                Swipe.Flicked += Flick;
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                bindings.Bind(Slots[i].Button, () => SlotPressed?.Invoke(index));
            }
            for (int i = 0; i < Uniques.Length; i++)
            {
                int index = i;
                bindings.Bind(Uniques[i].Card.Button, () => UniquePressed?.Invoke(index));
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
            {
                string id = row.Id;
                bindings.Bind(row.Button, () => CardPressed?.Invoke(id));
            }

            // 開くたびに、サーバーの編成とカード（なければアプリを動かしている間のもの）で描き直す。
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
            ShowLoading();
            StartLoad(token => LoadAsync(source, token), LoadFailedMessage, ShowLoadFailed);
        }

        private async Task LoadAsync(IPartySource source, CancellationToken token)
        {
            // 保存を待っている変更があれば、読み直さずに手元の編成を出す。
            var formation = await PartySession.LoadAsync(Party, source, token);
            if (token.IsCancellationRequested)
                return;
            Present(
                formation,
                (id, slot, skill) =>
                    PartySession.SetCardAsync(
                        id,
                        slot,
                        skill,
                        CardLoadoutPresenter.SaveFailedMessage
                    )
            );
        }

        private void ShowLoadFailed() => Set(Status, LoadFailedText);

        private void Present(PartyFormation formation, Func<string, int, string, Task> save)
        {
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
            foreach (var widget in Slots)
            {
                ShowSlot(widget, null, null, null, false);
                Set(widget.Name, "");
            }
            foreach (var widget in Uniques)
            {
                ShowUnique(widget, null, null, null, false);
                Set(widget.Card.Name, "");
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

        // 選んでいるタブに印を付け、アイテムのタブでは属性の行を隠して、持っていないことだけを出す。
        private void ShowTabs(int tab)
        {
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i == tab;
            bool items = tab == CardLoadoutPresenter.ItemTab;
            if (UsableLine != null)
                UsableLine.SetActive(!items);
            Set(Status, items ? NoItemText : "");
        }

        // アイテムのタブを選んでいるときの一覧の面の文字。アイテムはまだ持てない。
        public const string NoItemText = "持っているアイテムはありません";

        // 停止中の表示の切り替え（doc/rules/ui-design.md#停止中の表示の切り替え）。最初の表示はスキルのタブ。
        public const string TreePreviewState = "固有スキルの進化ツリー";

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
                    ShowSelection(-1, 0);
                    ShowTree(true);
                    return;
            }
            ShowLoading();
            if (index == 3)
                ShowLoadFailed();
        }

        // 左で選んでいるカスタムスキルの枠か固有スキル（もう一方は-1）に金の枠を付ける。
        private void ShowSelection(int slot, int unique)
        {
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i].Selected != null)
                    Slots[i].Selected.SetActive(i == slot);
            for (int i = 0; i < Uniques.Length; i++)
                if (Uniques[i].Card.Selected != null)
                    Uniques[i].Card.Selected.SetActive(i == unique);
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

        public bool HandleBack() => TrainingSession.BackToPage(Guide);

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
            StatGrid.Show(Stats, growth?.StatsAt(level));
            for (int i = 0; i < Uniques.Length; i++)
            {
                var unique =
                    state.Uniques != null && i < state.Uniques.Count ? state.Uniques[i] : null;
                ShowUnique(
                    Uniques[i],
                    unique,
                    ArtOf(unique?.Card?.Id),
                    IconOf(unique?.Card?.Element ?? CardElement.None),
                    i == state.Unique
                );
            }
            for (int i = 0; i < Slots.Length; i++)
                ShowSlot(
                    Slots[i],
                    i < state.Slots.Count ? state.Slots[i] : null,
                    i < state.Slots.Count ? ArtOf(state.Slots[i].Id) : null,
                    i < state.Slots.Count ? IconOf(state.Slots[i].Element) : null,
                    i == state.Slot
                );
            ShowUsable(Usable, state.Usable, IconOf);
            ShowTabs(state.Tab);
            ShowRows(state);
            if (state.Tree != null)
                ShowTreeState(state.Tree, state.PersonId);
            else
                shownTree = null;
            ShowTree(state.Tree != null);
        }

        /// <summary>
        /// Shows a unique skill: the card of the stage set, like a custom skill's slot. A locked
        /// one has its art darkened under a lock and 「Lv 20で解放」 at the top right.
        /// </summary>
        public static void ShowUnique(
            CardLoadoutUniqueWidget widget,
            CardLoadoutUniqueState unique,
            Texture art,
            Sprite element,
            bool selected
        )
        {
            var card = unique?.Card;
            ShowSlot(widget.Card, card, art, element, selected);
            if (card == null)
                Set(widget.Card.Name, "");
            bool locked = unique != null && unique.Locked;
            if (widget.Card.Art != null)
                widget.Card.Art.color = locked ? LockedArt : Color.white;
            // 鍵のかかったカードは光らせない。
            if (widget.Card.Foil != null && locked)
                widget.Card.Foil.Image.enabled = false;
            if (widget.Lock != null)
                widget.Lock.SetActive(locked);
            Set(widget.Mark, locked ? UnlockText(unique.UnlockLevel) : "");
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
            Set(Tree.Title, $"{tree.Name}の進化");
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

        private void ShowRows(CardLoadoutState state)
        {
            var order = new Dictionary<string, int>();
            for (int i = 0; i < state.Choices.Count; i++)
                order[state.Choices[i].Id] = i;
            var shown = new SortedList<int, CardLoadoutRowWidget>();
            foreach (var row in Rows)
            {
                if (row.Button == null)
                    continue;
                bool on = order.TryGetValue(row.Id, out int index);
                row.Button.gameObject.SetActive(on);
                if (on)
                    shown[index] = row;
            }
            int sibling = 0;
            foreach (var (index, row) in shown)
            {
                row.Button.transform.SetSiblingIndex(sibling++);
                ShowRow(row, state.Choices[index]);
            }
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
                widget.Foil.Show(art, filled ? card.Quality : ItemQuality.C);
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

        /// <summary>The description with the person's numbers, and which of their slots holds the card.</summary>
        public static void ShowRow(CardLoadoutRowWidget widget, CardLoadoutCardState card)
        {
            Set(widget.Description, card.Description);
            if (widget.Mark != null)
            {
                widget.Mark.gameObject.SetActive(card.Slot >= 0);
                widget.Mark.text = card.Slot >= 0 ? $"{card.Slot + 1}枚目" : "";
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

        // 左の枠の挿絵は、右の行に作り込んだ挿絵を使う。固有スキルの段階は仮データの挿絵を使う。
        private Texture ArtOf(string id)
        {
            if (id == null)
                return null;
            foreach (var row in Rows)
                if (row.Id == id && row.Art != null)
                    return row.Art.texture;
            return Party != null ? Party.ArtOf(id) : null;
        }
    }
}
