using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Party;
using Baryonyx.UI.Cards;

namespace Baryonyx.CardLoadout
{
    /// <summary>One card as the card skills show it, with the numbers for the person on screen.</summary>
    public sealed class CardLoadoutCardState
    {
        // 空いている枠ではnull。
        public string Id { get; internal set; }
        public string Name { get; internal set; }

        // 「攻撃・敵単体」（リッチテキスト）。
        public string KindLine { get; internal set; }

        // 説明。数字はその人のステータスから求め、何をするかで色を変える（リッチテキスト）。
        public string Description { get; internal set; }
        public int Cost { get; internal set; }
        public CardElement Element { get; internal set; }

        // その人のどの枠に入っているか（0から）。入っていなければ-1。
        public int Slot { get; internal set; } = -1;
    }

    /// <summary>Everything the card skills show, recomputed after every input.</summary>
    public sealed class CardLoadoutState
    {
        // 上のタブに並べる人のID。パーティの枠の順、続けてパーティにいない仲間を持っている順。
        public IReadOnlyList<string> People { get; internal set; }
        public int PartyCount { get; internal set; }

        // 選んでいる人（People の位置）とその人。
        public int Person { get; internal set; }
        public string PersonId { get; internal set; }
        public string Name { get; internal set; }

        // その人が付けられる属性（属性のないカードは誰でも付けられる）。
        public IReadOnlyList<CardElement> Usable { get; internal set; }

        // 選んでいる枠と、4つの枠のカード。
        public int Slot { get; internal set; }
        public IReadOnlyList<CardLoadoutCardState> Slots { get; internal set; }

        // 右に並べる、その人が付けられるカード（コストの低い順）。
        public IReadOnlyList<CardLoadoutCardState> Choices { get; internal set; }
        public string CountText { get; internal set; }
    }

    /// <summary>Where the card skills read and write each person's cards and the numbers on them.</summary>
    public interface ICardLoadoutStore
    {
        IReadOnlyList<string> CardsOf(string id);
        void SetCards(string id, IReadOnlyList<string> skills);

        // カードの威力のもとになる、いまのレベルのステータス。
        int StatOf(string id, CardStat stat);

        // UPTで威力が変わるカードに使う、今日のUPT。
        int Upt { get; }
        string Selected { get; set; }
    }

    public interface ICardLoadoutView
    {
        event Action<string> PersonPressed;
        event Action<int> SlotPressed;
        event Action<string> CardPressed;

        void Render(CardLoadoutState state);

        // 操作を求めない通知（共通の通知の帯）を出す。
        void ShowNotice(string message);
    }

    /// <summary>
    /// The tavern's card skills: choose a person on the tabs (the party first, then the other
    /// companions), a slot on the left, then tap a card on the right to set it there at once.
    /// A card the person holds in another slot swaps with the chosen slot. Each change shows a
    /// notice that asks nothing of the player.
    /// </summary>
    public sealed class CardLoadoutPresenter : IDisposable
    {
        private readonly ICardLoadoutView view;
        private readonly IReadOnlyList<PartyMember> people;
        private readonly int partyCount;
        private readonly ICardLoadoutStore store;
        private int person;
        private int slot;
        private bool disposed;

        public CardLoadoutPresenter(
            ICardLoadoutView view,
            IReadOnlyList<PartyMember> people,
            int partyCount,
            ICardLoadoutStore store
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.people = people ?? Array.Empty<PartyMember>();
            this.partyCount = Math.Clamp(partyCount, 0, this.people.Count);
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            person = Math.Max(0, IndexOf(store.Selected));
            view.PersonPressed += SelectPerson;
            view.SlotPressed += SelectSlot;
            view.CardPressed += Choose;
            Refresh();
        }

        public CardLoadoutState State { get; private set; }

        private PartyMember Current => people.Count > 0 ? people[person] : null;

        /// <summary>The people in tab order: the party's slots first, then the others as owned.</summary>
        public static (IReadOnlyList<PartyMember> People, int PartyCount) People(
            PartyFormation formation
        )
        {
            if (formation == null)
                return (Array.Empty<PartyMember>(), 0);
            var party = Enumerable
                .Range(0, formation.SlotCount)
                .Select(formation.Member)
                .Where(id => id != null)
                .Select(formation.Find)
                .Where(member => member != null)
                .ToArray();
            return (party.Concat(formation.Bench).ToArray(), party.Length);
        }

        public void SelectPerson(string id)
        {
            int index = IndexOf(id);
            if (disposed || index < 0 || index == person)
                return;
            person = index;
            slot = 0;
            store.Selected = id;
            Refresh();
        }

        public void SelectSlot(int index)
        {
            if (disposed || index < 0 || index >= CardLoadoutRules.Size || index == slot)
                return;
            slot = index;
            Refresh();
        }

        // 選んでいる枠に、押したカードを入れる。
        public void Choose(string skill)
        {
            var member = Current;
            var card = CardSkills.Find(skill);
            if (disposed || member == null || card == null)
                return;
            if (!CardLoadoutRules.CanUse(CardLoadoutRules.Usable(member), card))
                return;
            var cards = Cards(member.Id);
            string before = cards[slot];
            var change = CardLoadoutRules.Apply(cards, slot, skill, out _);
            if (change == CardLoadoutChange.None)
                return;
            store.SetCards(member.Id, cards);
            string name = CardSkills.Find(before)?.Name;
            view.ShowNotice(
                change == CardLoadoutChange.Swap
                    ? $"{member.Name}の「{card.Name}」と「{name}」を入れ替えました"
                : name == null ? $"{member.Name}に「{card.Name}」をセットしました"
                : $"{member.Name}の「{name}」を「{card.Name}」に替えました"
            );
            Refresh();
        }

        public void Refresh()
        {
            if (disposed)
                return;
            var member = Current;
            if (member == null)
            {
                State = new CardLoadoutState
                {
                    People = Array.Empty<string>(),
                    Usable = Array.Empty<CardElement>(),
                    Slots = Array.Empty<CardLoadoutCardState>(),
                    Choices = Array.Empty<CardLoadoutCardState>(),
                    CountText = "",
                };
                view.Render(State);
                return;
            }
            var usable = CardLoadoutRules.Usable(member);
            var cards = Cards(member.Id);
            var choices = CardLoadoutRules.Choices(usable);
            State = new CardLoadoutState
            {
                People = people.Select(entry => entry.Id).ToArray(),
                PartyCount = partyCount,
                Person = person,
                PersonId = member.Id,
                Name = member.Name,
                Usable = usable,
                Slot = slot,
                Slots = cards.Select((id, i) => Card(member.Id, id, i)).ToArray(),
                Choices = choices
                    .Select(card => Card(member.Id, card.Id, Array.IndexOf(cards, card.Id)))
                    .ToArray(),
                CountText = $"{choices.Count}枚",
            };
            view.Render(State);
        }

        // その人の4枚。足りない枠はnull（空き）にする。
        private string[] Cards(string id)
        {
            var cards = store.CardsOf(id) ?? Array.Empty<string>();
            return Enumerable
                .Range(0, CardLoadoutRules.Size)
                .Select(i => i < cards.Count ? cards[i] : null)
                .ToArray();
        }

        private CardLoadoutCardState Card(string person, string id, int slotIndex)
        {
            var card = CardSkills.Find(id);
            if (card == null)
                return new CardLoadoutCardState { Name = "空き", Slot = slotIndex };
            return new CardLoadoutCardState
            {
                Id = card.Id,
                Name = card.Name,
                KindLine = CardText.KindLine(card),
                Description = CardText.Description(
                    card,
                    stat => store.StatOf(person, stat),
                    store.Upt
                ),
                Cost = card.Cost,
                Element = card.Element,
                Slot = slotIndex,
            };
        }

        private int IndexOf(string id)
        {
            for (int i = 0; i < people.Count; i++)
                if (people[i].Id == id)
                    return i;
            return -1;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            view.PersonPressed -= SelectPerson;
            view.SlotPressed -= SelectSlot;
            view.CardPressed -= Choose;
        }
    }
}
