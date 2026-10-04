using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Party;
using Baryonyx.Training;
using Baryonyx.UI.Buttons;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.UI.UiText;

namespace Baryonyx.CardLoadout
{
    /// <summary>One of the person's four slots on the left, baked by the generator.</summary>
    [Serializable]
    public sealed class CardLoadoutSlotWidget
    {
        public Button Button;
        public GameObject Selected;

        // カードの挿絵（2倍）と、その上に重ねるコストと属性のアイコン。
        public RawImage Art;
        public TMP_Text Cost;
        public Image Element;
        public TMP_Text Name;
        public TMP_Text Kind;
        public TMP_Text Description;
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
    /// The tavern's card skills over the whole guide screen: the people's tabs and the chosen
    /// person's four cards on the left, the cards they can set on the right. CardLoadoutAssets
    /// bakes the tabs, slots and rows into the tavern prefab, so they read in the editor; this
    /// view wires them up and drives its own presenter. When the app has a server
    /// (<see cref="PartySession.Source"/>), it reads the party on opening and saves every change
    /// there; otherwise it uses the mock data. Back returns to the screen that opened the card
    /// skills for one person, if any. Changes are told on the guide screen's notice band.
    /// </summary>
    public sealed class CardLoadoutView : MonoBehaviour, ICardLoadoutView, IGuideBackHandler
    {
        public const string LoadingText = GuidePanelLoad.LoadingText;
        public const string LoadFailedText = GuidePanelLoad.LoadFailedText;
        public const string LoadFailedMessage = "スキルを取得できませんでした";

        public PartyMockData Party;
        public TrainingMockData Training;

        // 通知は案内人の画面の通知の帯に出す。
        public GuideMenuView Guide;

        // 左上の仲間のタブ。
        public PartyTabStrip People = new();
        public CardLoadoutSlotWidget[] Slots = Array.Empty<CardLoadoutSlotWidget>();

        // 右の見出しの横の、その人が付けられる属性のアイコン（2つまで）。
        public Image[] Usable = Array.Empty<Image>();
        public TMP_Text Count;
        public CardLoadoutRowWidget[] Rows = Array.Empty<CardLoadoutRowWidget>();
        public ScrollRect List;

        // CardElement の順の属性のアイコン。属性なしはnull。
        public Sprite[] ElementIcons = Array.Empty<Sprite>();

        private CardLoadoutPresenter presenter;
        private readonly GuidePanelLoad load = new();
        private readonly ButtonBindings bindings = new();

        public event Action<string> PersonPressed;
        public event Action<int> SlotPressed;
        public event Action<string> CardPressed;

        public CardLoadoutPresenter Presenter => presenter;
        public string LastNotice { get; private set; } = "";

        // 実行中または直前の読み込み。テストで完了を待つために公開する。
        public Task LoadTask => load.Task;

        private void OnEnable()
        {
            foreach (var person in People.Tabs)
            {
                string id = person.Id;
                bindings.Bind(person.Button, () => PersonPressed?.Invoke(id));
            }
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                bindings.Bind(Slots[i].Button, () => SlotPressed?.Invoke(index));
            }
            foreach (var row in Rows)
            {
                string id = row.Id;
                bindings.Bind(row.Button, () => CardPressed?.Invoke(id));
            }

            // 開くたびに、サーバーの編成とカード（なければアプリを動かしている間のもの）で描き直す。
            presenter?.Dispose();
            presenter = null;
            People.Forget();
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
            load.Start(token => LoadAsync(source, token), LoadFailed);
        }

        private async Task LoadAsync(IPartySource source, CancellationToken token)
        {
            var state = await source.LoadAsync(token);
            if (token.IsCancellationRequested)
                return;
            Present(
                PartySession.Use(Party, state),
                (id, slot, skill, cancel) => SaveAsync(source, id, slot, skill, cancel)
            );
        }

        private void LoadFailed(Exception exception)
        {
            Debug.LogWarning("スキルを取得できませんでした。" + exception.Message, this);
            Set(Count, LoadFailedText);
            ShowNotice(LoadFailedMessage);
        }

        // 保存した結果に置き換える。カードは store が PartySession から読み直す。
        private async Task SaveAsync(
            IPartySource source,
            string id,
            int slot,
            string skill,
            CancellationToken token
        ) => PartySession.Use(Party, await source.SetCardAsync(id, slot, skill, token));

        private void Present(
            PartyFormation formation,
            Func<string, int, string, CancellationToken, Task> save
        )
        {
            var (people, partyCount) = CardLoadoutPresenter.People(formation);
            presenter = new CardLoadoutPresenter(
                this,
                people,
                partyCount,
                new CardLoadoutSessionStore(Party, Training),
                save
            );
        }

        // 読み込むまで、仮データのカードを本当のカードとして見せない。
        private void ShowLoading()
        {
            Set(Count, LoadingText);
            People.Hide();
            foreach (var widget in Slots)
            {
                ShowSlot(widget, null, null, null, false);
                Set(widget.Name, "");
            }
            ShowUsable(Usable, Array.Empty<CardElement>(), IconOf);
            foreach (var row in Rows)
                if (row.Button != null)
                    row.Button.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            load.Cancel();
            bindings.Clear();
            presenter?.Dispose();
            presenter = null;
            CardLoadoutSession.ForgetBack();
        }

        // ほかの画面（育成）から開いたときは、「もどる」でその画面へ戻る。
        public bool HandleBack() => CardLoadoutSession.TakeBack();

        public void Render(CardLoadoutState state)
        {
            People.Show(state.People, state.PartyCount, state.Person);
            for (int i = 0; i < Slots.Length; i++)
                ShowSlot(
                    Slots[i],
                    i < state.Slots.Count ? state.Slots[i] : null,
                    i < state.Slots.Count ? ArtOf(state.Slots[i].Id) : null,
                    i < state.Slots.Count ? IconOf(state.Slots[i].Element) : null,
                    i == state.Slot
                );
            ShowUsable(Usable, state.Usable, IconOf);
            if (Count != null)
                Count.text = state.CountText;
            ShowRows(state);
        }

        public void ShowNotice(string message)
        {
            LastNotice = message ?? "";
            if (Guide != null)
                Guide.ShowToast(message);
        }

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

        // 左の枠の挿絵は、右の行に作り込んだ挿絵を使う。
        private Texture ArtOf(string id)
        {
            if (id == null)
                return null;
            foreach (var row in Rows)
                if (row.Id == id && row.Art != null)
                    return row.Art.texture;
            return null;
        }
    }
}
