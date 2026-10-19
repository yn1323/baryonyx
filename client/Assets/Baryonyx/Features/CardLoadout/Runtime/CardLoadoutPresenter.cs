using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
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

        /// <summary>The rarity (☆1 to ☆5): how much the card sparkles.</summary>
        public int Rarity { get; internal set; } = 1;
        public CardElement Element { get; internal set; }

        // その人のどの枠に入っているか（0から）。入っていなければ-1。
        public int Slot { get; internal set; } = -1;
    }

    /// <summary>One of the person's two unique skills on the left: the card of the stage set.</summary>
    public sealed class CardLoadoutUniqueState
    {
        public CardLoadoutCardState Card { get; internal set; }

        // まだ解放していない固有スキル（解放するLv）。
        public bool Locked { get; internal set; }
        public int UnlockLevel { get; internal set; }
    }

    /// <summary>One of the seven stages of the evolution tree on the right.</summary>
    public sealed class CardLoadoutStageState
    {
        // 段階（0が最初の段階。UniqueSkillTreeShape の番号）と、進化する前の段階（最初は-1）。
        public int Stage { get; internal set; }
        public int Parent { get; internal set; } = -1;

        // 取得した段階は名前と効果を見せる。取得していない段階は形だけの影にする（Card は null）。
        public bool Acquired { get; internal set; }

        // 編成でセットしている段階。
        public bool Set { get; internal set; }
        public CardLoadoutCardState Card { get; internal set; }
    }

    /// <summary>The evolution tree of the unique skill chosen on the left.</summary>
    public sealed class CardLoadoutTreeState
    {
        // 左で選んでいる固有スキル（0から）と、その最初の段階の名前（「マナバースト」）。
        public int Unique { get; internal set; }
        public string Name { get; internal set; }

        // 未解放なら、段階を付け替えられない。
        public bool Locked { get; internal set; }
        public int UnlockLevel { get; internal set; }
        public IReadOnlyList<CardLoadoutStageState> Stages { get; internal set; }
    }

    /// <summary>A person's unique skill as the store gives it: its tree, when it unlocks, and their progress.</summary>
    public sealed class CardLoadoutUnique
    {
        public UniqueSkillTree Tree;
        public int UnlockLevel = 1;
        public bool Unlocked;
        public UniqueSkillProgress Progress = new();
    }

    /// <summary>Everything the card skills show, recomputed after every input.</summary>
    public sealed class CardLoadoutState
    {
        // 選んでいる人とその人。フリックではパーティの枠の順、続けてパーティにいない仲間を持っている順に替える。
        public string PersonId { get; internal set; }
        public string Name { get; internal set; }
        public bool CanSwitch { get; internal set; }

        // その人が付けられる属性（属性のないカードは誰でも付けられる）。
        public IReadOnlyList<CardElement> Usable { get; internal set; }

        // 選んでいるカスタムスキルの枠（固有スキルを選んでいるときは-1）と、2つの枠のカード。
        public int Slot { get; internal set; }
        public IReadOnlyList<CardLoadoutCardState> Slots { get; internal set; }

        // 選んでいる固有スキル（カスタムスキルの枠を選んでいるときは-1）と、固有スキル2つ。
        public int Unique { get; internal set; } = -1;
        public IReadOnlyList<CardLoadoutUniqueState> Uniques { get; internal set; }

        // 固有スキルを選んでいるときの、右の進化ツリー。カスタムスキルの枠を選んでいるときはnull。
        public CardLoadoutTreeState Tree { get; internal set; }

        // 右の上で選んでいるタブ（CardLoadoutPresenter.SkillTab か ItemTab）。
        public int Tab { get; internal set; }

        // 右に並べる、その人が付けられるカード（コストの低い順）。アイテムのタブでは空。
        public IReadOnlyList<CardLoadoutCardState> Choices { get; internal set; }
    }

    /// <summary>Where the card skills read and write each person's cards and the numbers on them.</summary>
    public interface ICardLoadoutStore
    {
        IReadOnlyList<string> CardsOf(string id);
        void SetCards(string id, IReadOnlyList<string> skills);

        string Selected { get; set; }

        // その人の固有スキル（2つ）。進化ツリーのないものはnull。
        IReadOnlyList<CardLoadoutUnique> UniquesOf(string id);

        // 固有スキル index に、取得した段階 stage をセットする。変わらなければfalse。
        bool SetUniqueStage(string id, int index, int stage);
    }

    public interface ICardLoadoutView
    {
        event Action PrevPressed;
        event Action NextPressed;
        event Action<int> SlotPressed;
        event Action<int> TabPressed;
        event Action<string> CardPressed;

        // 左の固有スキル（0から）と、右の進化ツリーの段階。
        event Action<int> UniquePressed;
        event Action<int> StagePressed;

        void Render(CardLoadoutState state);

        // 操作を求めない通知（共通の通知の帯）を出す。
        void ShowNotice(string message);
    }

    /// <summary>
    /// The formation's card skills change screen: ◀ ▶ walks the people (the party first, then
    /// the other companions) keeping the chosen slot and tab, choose a slot on the left, then tap
    /// a card on the right to set it there at once. The right's tabs pick skills or items; items
    /// cannot be owned yet, so the item tab lists nothing. Choosing one of the two unique skills
    /// on the left shows its evolution tree on the right instead (doc/features/skill-evolution.md),
    /// where tapping a stage acquired sets it; the stages not acquired are shadows. The stage set
    /// is kept while the app runs only, since the server does not keep it yet.
    /// A card the person holds in another slot swaps with the chosen slot. Each change shows a
    /// notice that asks nothing of the player. With a <c>save</c> function the change shows at
    /// once from the store and is saved on the server behind it, and the store reads the saved
    /// cards again (put back when the save failed, which <see cref="Networking.SaveQueue"/> tells
    /// on the notice band); otherwise (the showcase) the store keeps the cards in memory.
    /// </summary>
    public sealed class CardLoadoutPresenter : IDisposable
    {
        public const string SaveFailedMessage = "スキルを保存できませんでした";

        // 右の上のタブ。カスタムスキルの枠にはスキルかアイテムを入れる。
        public const int SkillTab = 0;
        public const int ItemTab = 1;

        // 1人の固有スキルの数（doc/features/party.md）。
        public const int UniqueCount = 2;

        private readonly ICardLoadoutView view;
        private readonly IReadOnlyList<PartyMember> people;
        private readonly ICardLoadoutStore store;

        // その人・枠・カードのIDを保存する。すぐに store へ当て、保存が済む（失敗なら戻す）と終わる。
        // 表示は store から読み直す。
        private readonly Func<string, int, string, Task> save;
        private int person;
        private int slot;

        // 選んでいる固有スキル。カスタムスキルの枠を選んでいるときは-1。
        private int unique = -1;
        private int tab;
        private bool disposed;

        public CardLoadoutPresenter(
            ICardLoadoutView view,
            IReadOnlyList<PartyMember> people,
            ICardLoadoutStore store,
            Func<string, int, string, Task> save = null,
            int slot = 0,
            int tab = SkillTab,
            int unique = -1
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.people = people ?? Array.Empty<PartyMember>();
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.save = save;
            person = Math.Max(0, IndexOf(store.Selected));
            this.slot = slot >= 0 && slot < CardLoadoutRules.Size ? slot : 0;
            this.tab = tab == ItemTab ? ItemTab : SkillTab;
            this.unique = unique >= 0 && unique < UniqueCount ? unique : -1;
            view.PrevPressed += Previous;
            view.NextPressed += Next;
            view.SlotPressed += SelectSlot;
            view.TabPressed += SelectTab;
            view.CardPressed += Choose;
            view.UniquePressed += SelectUnique;
            view.StagePressed += ChooseStage;
            Refresh();
        }

        public CardLoadoutState State { get; private set; }

        // 直前の付け替えの保存。テストで完了を待つために公開する。
        public Task ChooseTask { get; private set; } = Task.CompletedTask;

        private PartyMember Current => people.Count > 0 ? people[person] : null;

        /// <summary>The people in ◀ ▶ order: the party's slots first, then the others as owned.</summary>
        public static IReadOnlyList<PartyMember> People(PartyFormation formation) =>
            formation != null ? formation.TabOrder().People : Array.Empty<PartyMember>();

        public void Previous() => Step(-1);

        public void Next() => Step(1);

        // 人を替える。選んでいる枠（固有スキル）とタブはそのままにし、同じ枠のスキルを人ごとに見比べられるようにする。
        private void Step(int delta)
        {
            if (disposed || people.Count < 2)
                return;
            person = (person + delta + people.Count) % people.Count;
            store.Selected = people[person].Id;
            Refresh();
        }

        public void SelectSlot(int index)
        {
            if (disposed || index < 0 || index >= CardLoadoutRules.Size)
                return;
            if (index == slot && unique < 0)
                return;
            slot = index;
            unique = -1;
            Refresh();
        }

        // 固有スキルを選び、右に進化ツリーを出す。
        public void SelectUnique(int index)
        {
            if (disposed || index < 0 || index >= UniqueCount || index == unique)
                return;
            unique = index;
            Refresh();
        }

        // 選んでいる固有スキルに、ツリーの取得した段階をその場でセットする。
        public void ChooseStage(int stage)
        {
            var member = Current;
            if (disposed || unique < 0 || member == null)
                return;
            var skill = UniqueOf(member.Id, unique);
            if (skill?.Tree == null || !skill.Unlocked || !skill.Progress.Has(stage))
                return;
            if (skill.Tree.StageAt(stage) == null)
                return;
            // 変わったことは左の固有スキルとツリーの印を見ればわかるので知らせない。
            if (store.SetUniqueStage(member.Id, unique, stage))
                Refresh();
        }

        // 右の一覧をスキルかアイテムに切り替える。左の枠の選択は変えない。
        public void SelectTab(int index)
        {
            if (disposed || (index != SkillTab && index != ItemTab) || index == tab)
                return;
            tab = index;
            Refresh();
        }

        // 選んでいる枠に、押したカードを入れる。
        public void Choose(string skill) => ChooseTask = ChooseAsync(skill);

        // すぐに表示を変え、サーバーがあればそのあとで保存する。
        private async Task ChooseAsync(string skill)
        {
            var member = Current;
            var card = CardSkills.Find(skill);
            if (disposed || unique >= 0 || tab != SkillTab || member == null || card == null)
                return;
            if (!CardLoadoutRules.CanUse(CardLoadoutRules.Usable(member), card))
                return;
            int target = slot;
            var cards = Cards(member.Id);
            var change = CardLoadoutRules.Apply(cards, target, skill, out _);
            if (change == CardLoadoutChange.None)
                return;
            // 変わったことは枠を見ればわかるので知らせない。
            if (save == null)
            {
                store.SetCards(member.Id, cards);
                Refresh();
                return;
            }

            // save はすぐに store へ当てるので、保存を待たずに描き直す。
            var saved = save(member.Id, target, skill);
            Refresh();
            await saved;
            if (disposed)
                return;
            // 保存したカード（失敗なら戻したカード）で描き直す。
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
                    Unique = unique,
                    Uniques = Array.Empty<CardLoadoutUniqueState>(),
                    Tab = tab,
                    Choices = Array.Empty<CardLoadoutCardState>(),
                };
                view.Render(State);
                return;
            }
            var usable = CardLoadoutRules.Usable(member);
            var cards = Cards(member.Id);
            // アイテムはまだ持てないので、アイテムのタブには何も並べない。
            var choices =
                tab == SkillTab ? CardLoadoutRules.Choices(usable) : Array.Empty<CardSkill>();
            State = new CardLoadoutState
            {
                PersonId = member.Id,
                Name = member.Name,
                CanSwitch = people.Count > 1,
                Usable = usable,
                Slot = unique >= 0 ? -1 : slot,
                Slots = cards.Select((id, i) => Card(member.Id, id, i)).ToArray(),
                Unique = unique,
                Uniques = Enumerable
                    .Range(0, UniqueCount)
                    .Select(i => Unique(member.Id, UniqueOf(member.Id, i)))
                    .ToArray(),
                Tree = unique >= 0 ? Tree(member.Id, unique) : null,
                Tab = tab,
                Choices = choices
                    .Select(card => Card(member.Id, card.Id, Array.IndexOf(cards, card.Id)))
                    .ToArray(),
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

        private CardLoadoutUnique UniqueOf(string id, int index)
        {
            var uniques = store.UniquesOf(id);
            return uniques != null && index < uniques.Count ? uniques[index] : null;
        }

        // 左の固有スキル1つ。セットしている段階のカードを出す。
        private CardLoadoutUniqueState Unique(string person, CardLoadoutUnique skill)
        {
            var stage = skill?.Tree?.StageAt(skill.Progress.SetStage) ?? skill?.Tree?.Root;
            return new CardLoadoutUniqueState
            {
                Card = stage != null ? Card(person, stage.Card, -1) : null,
                Locked = skill != null && !skill.Unlocked,
                UnlockLevel = skill?.UnlockLevel ?? 0,
            };
        }

        // 右の進化ツリー。取得していない段階は形だけにする。
        private CardLoadoutTreeState Tree(string person, int index)
        {
            var skill = UniqueOf(person, index);
            var tree = skill?.Tree;
            int set = skill?.Progress.SetStage ?? 0;
            return new CardLoadoutTreeState
            {
                Unique = index,
                Name = tree?.Root?.Card.Name ?? "",
                Locked = skill != null && !skill.Unlocked,
                UnlockLevel = skill?.UnlockLevel ?? 0,
                Stages = Enumerable
                    .Range(0, UniqueSkillTreeShape.Count)
                    .Select(stage =>
                    {
                        var made = tree?.StageAt(stage);
                        bool acquired = made != null && skill.Progress.Has(stage);
                        return new CardLoadoutStageState
                        {
                            Stage = stage,
                            Parent = UniqueSkillTreeShape.Parent(stage),
                            Acquired = acquired,
                            Set = acquired && stage == set,
                            Card = acquired ? Card(person, made.Card, -1) : null,
                        };
                    })
                    .ToArray(),
            };
        }

        private CardLoadoutCardState Card(string person, string id, int slotIndex)
        {
            var card = CardSkills.Find(id);
            if (card == null)
                return new CardLoadoutCardState { Name = "空き", Slot = slotIndex };
            return Card(person, card, slotIndex);
        }

        private CardLoadoutCardState Card(string person, CardSkill card, int slotIndex) =>
            new()
            {
                Id = card.Id,
                Name = card.Name,
                KindLine = CardText.KindLine(card),
                Description = CardText.Description(card),
                Cost = card.Cost,
                Rarity = card.Rarity,
                Element = card.Element,
                Slot = slotIndex,
            };

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
            view.PrevPressed -= Previous;
            view.NextPressed -= Next;
            view.SlotPressed -= SelectSlot;
            view.TabPressed -= SelectTab;
            view.CardPressed -= Choose;
            view.UniquePressed -= SelectUnique;
            view.StagePressed -= ChooseStage;
        }
    }
}
