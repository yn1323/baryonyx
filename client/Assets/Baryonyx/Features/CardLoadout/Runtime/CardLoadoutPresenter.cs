using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        // 選んでいる人とその人。◀▶はパーティの枠の順、続けてパーティにいない仲間を持っている順に替える。
        public string PersonId { get; internal set; }
        public string Name { get; internal set; }
        public bool CanSwitch { get; internal set; }

        // その人が付けられる属性（属性のないカードは誰でも付けられる）。
        public IReadOnlyList<CardElement> Usable { get; internal set; }

        // 選んでいる枠と、カスタムスキルの2つの枠のカード。
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

        // ACTで威力が変わるカードに使う、今日のACT。
        int Act { get; }
        string Selected { get; set; }
    }

    public interface ICardLoadoutView
    {
        event Action PrevPressed;
        event Action NextPressed;
        event Action<int> SlotPressed;
        event Action<string> CardPressed;

        void Render(CardLoadoutState state);

        // 操作を求めない通知（共通の通知の帯）を出す。
        void ShowNotice(string message);
    }

    /// <summary>
    /// The formation's card skills change screen: ◀ ▶ walks the people (the party first, then
    /// the other companions) keeping the chosen slot, choose a slot on the left, then tap a card
    /// on the right to set it there at once.
    /// A card the person holds in another slot swaps with the chosen slot. Each change shows a
    /// notice that asks nothing of the player. With a <c>save</c> function the change is saved on
    /// the server first, and the store then reads the saved cards; otherwise (the showcase) the
    /// store keeps the cards in memory.
    /// </summary>
    public sealed class CardLoadoutPresenter : IDisposable
    {
        public const string SaveFailedMessage = "スキルを保存できませんでした";

        private readonly ICardLoadoutView view;
        private readonly IReadOnlyList<PartyMember> people;
        private readonly ICardLoadoutStore store;

        // その人・枠・カードのIDを保存する。保存したカードは store から読み直す。
        private readonly Func<string, int, string, CancellationToken, Task> save;
        private readonly CancellationTokenSource lifetime = new();
        private int person;
        private int slot;
        private bool saving;
        private bool disposed;

        public CardLoadoutPresenter(
            ICardLoadoutView view,
            IReadOnlyList<PartyMember> people,
            ICardLoadoutStore store,
            Func<string, int, string, CancellationToken, Task> save = null,
            int slot = 0
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.people = people ?? Array.Empty<PartyMember>();
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.save = save;
            person = Math.Max(0, IndexOf(store.Selected));
            this.slot = slot >= 0 && slot < CardLoadoutRules.Size ? slot : 0;
            view.PrevPressed += Previous;
            view.NextPressed += Next;
            view.SlotPressed += SelectSlot;
            view.CardPressed += Choose;
            Refresh();
        }

        public CardLoadoutState State { get; private set; }

        // 保存を待っている間。重ねて押されても受け付けない。
        public bool Saving => saving;

        // 実行中または直前の付け替え。テストで完了を待つために公開する。
        public Task ChooseTask { get; private set; } = Task.CompletedTask;

        private PartyMember Current => people.Count > 0 ? people[person] : null;

        /// <summary>The people in ◀ ▶ order: the party's slots first, then the others as owned.</summary>
        public static IReadOnlyList<PartyMember> People(PartyFormation formation) =>
            formation != null ? formation.TabOrder().People : Array.Empty<PartyMember>();

        public void Previous() => Step(-1);

        public void Next() => Step(1);

        // 人を替える。選んでいる枠はそのままにし、同じ枠のスキルを人ごとに見比べられるようにする。
        private void Step(int delta)
        {
            if (disposed || saving || people.Count < 2)
                return;
            person = (person + delta + people.Count) % people.Count;
            store.Selected = people[person].Id;
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
        public void Choose(string skill) => ChooseTask = ChooseAsync(skill);

        // サーバーがあれば保存してから表示を変える。
        private async Task ChooseAsync(string skill)
        {
            var member = Current;
            var card = CardSkills.Find(skill);
            if (disposed || saving || member == null || card == null)
                return;
            if (!CardLoadoutRules.CanUse(CardLoadoutRules.Usable(member), card))
                return;
            int target = slot;
            var cards = Cards(member.Id);
            string before = cards[target];
            var change = CardLoadoutRules.Apply(cards, target, skill, out _);
            if (change == CardLoadoutChange.None)
                return;
            string name = CardSkills.Find(before)?.Name;
            string message =
                change == CardLoadoutChange.Swap
                    ? $"{member.Name}の「{card.Name}」と「{name}」を入れ替えました"
                : name == null ? $"{member.Name}に「{card.Name}」をセットしました"
                : $"{member.Name}の「{name}」を「{card.Name}」に替えました";
            if (save == null)
            {
                store.SetCards(member.Id, cards);
                view.ShowNotice(message);
                Refresh();
                return;
            }

            saving = true;
            try
            {
                await save(member.Id, target, skill, lifetime.Token);
                if (disposed)
                    return;
                view.ShowNotice(message);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                if (!disposed)
                    view.ShowNotice(SaveFailedMessage);
            }
            finally
            {
                saving = false;
            }
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
                PersonId = member.Id,
                Name = member.Name,
                CanSwitch = people.Count > 1,
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

        // その人のカスタムスキル2枚。足りない枠はnull（空き）にする。
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
                    store.Act
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
            lifetime.Cancel();
            lifetime.Dispose();
            view.PrevPressed -= Previous;
            view.NextPressed -= Next;
            view.SlotPressed -= SelectSlot;
            view.CardPressed -= Choose;
        }
    }
}
