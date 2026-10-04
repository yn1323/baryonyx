using System;
using System.Collections.Generic;
using Baryonyx.Combat;
using Baryonyx.Party;
using Baryonyx.Training;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.CardLoadout
{
    /// <summary>One person's tab at the top of the left, baked by the generator.</summary>
    [Serializable]
    public sealed class CardLoadoutPersonWidget
    {
        public string Id = "";
        public Button Button;
        public GameObject Selected;
    }

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
    /// view wires them up and drives its own presenter. Back returns to the screen that opened
    /// the card skills for one person, if any. Changes are told on the guide screen's notice band.
    /// </summary>
    public sealed class CardLoadoutView : MonoBehaviour, ICardLoadoutView, IGuideBackHandler
    {
        public PartyMockData Party;
        public TrainingMockData Training;

        // 通知は案内人の画面の通知の帯に出す。
        public GuideMenuView Guide;

        public ScrollRect PeopleScroll;
        public CardLoadoutPersonWidget[] People = Array.Empty<CardLoadoutPersonWidget>();

        // パーティとほかの仲間の間の区切り。
        public GameObject PeopleDivider;
        public CardLoadoutSlotWidget[] Slots = Array.Empty<CardLoadoutSlotWidget>();

        // 右の見出しの横の、その人が付けられる属性のアイコン（2つまで）。
        public Image[] Usable = Array.Empty<Image>();
        public TMP_Text Count;
        public CardLoadoutRowWidget[] Rows = Array.Empty<CardLoadoutRowWidget>();
        public ScrollRect List;

        // CardElement の順の属性のアイコン。属性なしはnull。
        public Sprite[] ElementIcons = Array.Empty<Sprite>();

        private CardLoadoutPresenter presenter;
        private string shownPerson;
        private readonly List<(Button Button, UnityEngine.Events.UnityAction Action)> bindings =
            new();

        public event Action<string> PersonPressed;
        public event Action<int> SlotPressed;
        public event Action<string> CardPressed;

        public CardLoadoutPresenter Presenter => presenter;
        public string LastNotice { get; private set; } = "";

        private void OnEnable()
        {
            foreach (var person in People)
            {
                string id = person.Id;
                Bind(person.Button, () => PersonPressed?.Invoke(id));
            }
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                Bind(Slots[i].Button, () => SlotPressed?.Invoke(index));
            }
            foreach (var row in Rows)
            {
                string id = row.Id;
                Bind(row.Button, () => CardPressed?.Invoke(id));
            }

            // 開くたびに、アプリを動かしている間の編成とカードで描き直す。
            presenter?.Dispose();
            presenter = null;
            shownPerson = null;
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            if (Party != null)
            {
                var (people, partyCount) = CardLoadoutPresenter.People(
                    PartySession.Formation(Party)
                );
                presenter = new CardLoadoutPresenter(
                    this,
                    people,
                    partyCount,
                    new CardLoadoutSessionStore(Party, Training)
                );
            }
        }

        private void OnDisable()
        {
            foreach (var (button, action) in bindings)
                if (button != null)
                    button.onClick.RemoveListener(action);
            bindings.Clear();
            presenter?.Dispose();
            presenter = null;
            CardLoadoutSession.ForgetBack();
        }

        // ほかの画面（育成）から開いたときは、「もどる」でその画面へ戻る。
        public bool HandleBack() => CardLoadoutSession.TakeBack();

        public void Render(CardLoadoutState state)
        {
            ShowPeople(state);
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

        // パーティの枠の順、区切り、続けてほかの仲間の順にタブを並べ、選んでいる人を金の枠にする。
        private void ShowPeople(CardLoadoutState state)
        {
            var order = new Dictionary<string, int>();
            for (int i = 0; i < state.People.Count; i++)
                order[state.People[i]] = i;
            var tabs = new SortedList<int, Transform>();
            foreach (var person in People)
            {
                if (person.Button == null)
                    continue;
                bool shown = order.TryGetValue(person.Id, out int index);
                person.Button.gameObject.SetActive(shown);
                if (person.Selected != null)
                    person.Selected.SetActive(shown && index == state.Person);
                if (shown)
                    tabs[index] = person.Button.transform;
            }
            bool divided = state.PartyCount > 0 && state.PartyCount < state.People.Count;
            if (PeopleDivider != null)
                PeopleDivider.SetActive(divided);
            int sibling = 0;
            foreach (var (index, tab) in tabs)
            {
                if (divided && index == state.PartyCount)
                    PeopleDivider.transform.SetSiblingIndex(sibling++);
                tab.SetSiblingIndex(sibling++);
            }

            // 人が替わったときだけ、選んだタブが見えるように横へ送る。
            if (state.PersonId != shownPerson)
            {
                shownPerson = state.PersonId;
                if (state.Person >= 0 && tabs.TryGetValue(state.Person, out var selected))
                    Reveal((RectTransform)selected);
            }
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

        // 横に並んだタブのうち、選んだタブが見える位置まで中身を送る。
        private void Reveal(RectTransform tab)
        {
            // 停止中（Prefabの生成）は、並べたままにする。
            if (!Application.isPlaying)
                return;
            if (PeopleScroll == null || PeopleScroll.content == null || tab == null)
                return;
            var viewport =
                PeopleScroll.viewport != null
                    ? PeopleScroll.viewport
                    : (RectTransform)PeopleScroll.transform;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            tab.GetWorldCorners(corners);
            float left = viewport.InverseTransformPoint(corners[0]).x;
            float right = viewport.InverseTransformPoint(corners[2]).x;
            var bounds = viewport.rect;
            float shift =
                left < bounds.xMin ? bounds.xMin - left
                : right > bounds.xMax ? bounds.xMax - right
                : 0f;
            if (shift != 0f)
                PeopleScroll.content.anchoredPosition += new Vector2(shift, 0f);
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;
            button.onClick.AddListener(action);
            bindings.Add((button, action));
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }
    }
}
