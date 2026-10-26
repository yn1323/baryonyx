using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class CardLoadoutPresenterTests
    {
        // CardLoadoutOwnedTests も使う。
        internal sealed class FakeView : ICardLoadoutView
        {
            public event Action PrevPressed;
            public event Action NextPressed;
            public event Action<int> SlotPressed;
            public event Action<int> TabPressed;
            public event Action<string> CardPressed;
            public event Action<int> UniquePressed;
            public event Action<int> StagePressed;

            public CardLoadoutState Last;
            public readonly List<string> Notices = new();

            public void Render(CardLoadoutState state) => Last = state;

            public void ShowNotice(string message) => Notices.Add(message);

            public void Prev() => PrevPressed?.Invoke();

            public void Next() => NextPressed?.Invoke();

            // ▶を押して、その人まで進む。
            public void Person(string id)
            {
                for (int i = 0; i < 10 && Last.PersonId != id; i++)
                    Next();
            }

            public void Slot(int index) => SlotPressed?.Invoke(index);

            public void Tab(int index) => TabPressed?.Invoke(index);

            public void Card(string id) => CardPressed?.Invoke(id);

            public void Unique(int index) => UniquePressed?.Invoke(index);

            public void Stage(int stage) => StagePressed?.Invoke(stage);
        }

        private sealed class FakeStore : ICardLoadoutStore
        {
            public readonly Dictionary<string, string[]> Cards = new();

            public IReadOnlyList<string> CardsOf(string id) =>
                Cards.TryGetValue(id, out var cards) ? cards : Array.Empty<string>();

            public void SetCards(string id, IReadOnlyList<string> skills) =>
                Cards[id] = skills.ToArray();

            public string Selected { get; set; }

            // 人ごとの固有スキル。なければ空。
            public readonly Dictionary<string, CardLoadoutUnique[]> Uniques = new();

            public IReadOnlyList<CardLoadoutUnique> UniquesOf(string id) =>
                Uniques.TryGetValue(id, out var uniques)
                    ? uniques
                    : Array.Empty<CardLoadoutUnique>();

            public bool SetUniqueStage(string id, int index, int stage)
            {
                var progress = Uniques[id][index].Progress;
                if (progress.SetStage == stage)
                    return false;
                progress.Set = stage;
                return true;
            }
        }

        private PartyFormation formation;
        private FakeView view;
        private FakeStore store;
        private CardLoadoutPresenter presenter;

        [SetUp]
        public void CreatePresenter()
        {
            // 前のテストが残した人・枠・進み具合から始めない。
            PartySession.Reset();
            CardLoadoutSession.Reset();
            UniqueSkillSession.Reset();
            var roster = new[]
            {
                Member("toma", "トーマ", "Fire", "Ice"),
                Member("luka", "ルカ", "VitalThrust", "Thunder"),
                Member("mina", "ミナ", "Heal", "HolyHammer"),
                Member("anselm", "アンセルム", "EarthSplitter", "Fire"),
            };
            // ルカをパーティから外し、空きの枠も残す。
            formation = new PartyFormation(roster, new[] { "mina", null, "toma" });
            view = new FakeView();
            store = new FakeStore();
            foreach (var member in roster)
                store.Cards[member.Id] = member.Cards.Select(card => card.Skill).ToArray();
            // トーマのマナバーストは1本目の枝の2段目までと2本目の枝の1段目を取得し、1本目の枝の1段目をセット。
            // 星降りはまだ解放していない。
            store.Uniques["toma"] = new[]
            {
                Unique(
                    "ManaBurst",
                    true,
                    new UniqueSkillProgress { Set = 1, Acquired = new[] { 1, 2, 3 } }
                ),
                Unique("StarFall", false, new UniqueSkillProgress()),
            };
            presenter = Open();
        }

        [TearDown]
        public void Dispose()
        {
            presenter?.Dispose();
            CardLoadoutSession.Reset();
            PartySession.Reset();
        }

        [Test]
        public void WithoutASlotItOpensOnTheFirstCustomSlot()
        {
            presenter.Dispose();
            presenter = new CardLoadoutPresenter(
                view,
                CardLoadoutPresenter.People(formation),
                store
            );
            view.Person("toma");
            // 左の6枚のうち、いつもどれか1つを選んでいる。何も渡さなければ1つ目のカスタムスキルの枠。
            Assert.That(view.Last.Slot, Is.EqualTo(0));
            Assert.That(view.Last.Unique, Is.EqualTo(-1));
            Assert.That(view.Last.Tree, Is.Null);
            view.Slot(2);
            view.Card("Embers");
            Assert.That(store.Cards["toma"], Is.EqualTo(new[] { "Fire", "Ice", "Embers" }));

            // 固有スキルを選ぶと、右は進化ツリーになり、カードは入らない。
            view.Unique(0);
            Assert.That(view.Last.Unique, Is.EqualTo(0));
            Assert.That(view.Last.Slot, Is.EqualTo(-1));
            Assert.That(view.Last.Tree, Is.Not.Null);
            view.Card("Thunder");
            Assert.That(store.Cards["toma"], Is.EqualTo(new[] { "Fire", "Ice", "Embers" }));
        }

        [Test]
        public void ThePeoplesColumnShowsThePersonPressedKeepingTheSlot()
        {
            view.Slot(1);
            presenter.Select("anselm");
            Assert.That(view.Last.PersonId, Is.EqualTo("anselm"));
            Assert.That(view.Last.Slot, Is.EqualTo(1));
            Assert.That(store.Selected, Is.EqualTo("anselm"));
            presenter.Select("nobody");
            Assert.That(view.Last.PersonId, Is.EqualTo("anselm"));
        }

        [Test]
        public void ArrowsWalkThePartyFirstThenTheOthers()
        {
            var order = new List<string> { view.Last.PersonId };
            for (int i = 0; i < 3; i++)
            {
                view.Next();
                order.Add(view.Last.PersonId);
            }
            Assert.That(order, Is.EqualTo(new[] { "mina", "toma", "luka", "anselm" }));
            view.Next();
            Assert.That(view.Last.PersonId, Is.EqualTo("mina"));
            view.Prev();
            Assert.That(view.Last.PersonId, Is.EqualTo("anselm"));
            Assert.That(view.Last.CanSwitch, Is.True);
        }

        [Test]
        public void OpensOnThePartysFirstPersonAndTheirFirstCard()
        {
            var state = view.Last;
            // 開いたときは、パーティの先頭の人のカスタムスキルの1枚目を選んでいる。
            Assert.That(state.PersonId, Is.EqualTo("mina"));
            Assert.That(state.Slot, Is.EqualTo(0));
            Assert.That(
                state.Slots.Select(card => card.Id),
                Is.EqualTo(new[] { "Heal", "HolyHammer", null })
            );
        }

        [Test]
        public void TheLeftShowsTheUniqueSkillsAtTheStageSet()
        {
            view.Person("toma");
            var state = view.Last;
            Assert.That(state.Uniques.Count, Is.EqualTo(CardLoadoutPresenter.UniqueCount));
            // セットしている段階のカードを、カスタムスキルと同じく種類・説明・コスト付きで出す。
            Assert.That(state.Uniques[0].Card.Name, Is.EqualTo("マナバースト・烈"));
            Assert.That(state.Uniques[0].Card.KindLine, Does.Contain("攻撃").And.Contain("敵全体"));
            Assert.That(state.Uniques[0].Card.Description, Does.Contain(">"));
            Assert.That(state.Uniques[0].Card.Cost, Is.EqualTo(1));
            Assert.That(state.Uniques[0].Locked, Is.False);
            Assert.That(state.Uniques[1].Card.Name, Is.EqualTo("星降り"));
            Assert.That(state.Uniques[1].Locked, Is.True);
            Assert.That(state.Uniques[1].UnlockLevel, Is.EqualTo(20));
            // 固有スキルを選ぶまでは、右はタブと一覧のまま。
            Assert.That(state.Unique, Is.EqualTo(-1));
            Assert.That(state.Tree, Is.Null);
        }

        [Test]
        public void ChoosingAUniqueSkillShowsItsTreeWithShadowsForStagesNotAcquired()
        {
            view.Person("toma");
            view.Unique(0);
            var state = view.Last;
            Assert.That(state.Unique, Is.EqualTo(0));
            Assert.That(state.Slot, Is.EqualTo(-1));
            var tree = state.Tree;
            Assert.That(tree.Name, Is.EqualTo("マナバースト"));
            Assert.That(tree.Stages.Count, Is.EqualTo(UniqueSkillTreeShape.Count));
            Assert.That(
                tree.Stages.Select(stage => stage.Acquired),
                Is.EqualTo(new[] { true, true, true, true, false, false, false })
            );
            Assert.That(tree.Stages.Single(stage => stage.Set).Stage, Is.EqualTo(1));
            // 取得していない段階は形だけで、名前も効果も出さない。
            Assert.That(tree.Stages[4].Card, Is.Null);
            Assert.That(tree.Stages[2].Card.Name, Is.EqualTo("マナバースト・極"));
            Assert.That(tree.Stages[2].Parent, Is.EqualTo(1));
            Assert.That(tree.Stages[3].Parent, Is.EqualTo(0));

            // 人を替えても、選んでいる固有スキルのまま。カスタムスキルの枠を押すと一覧に戻る。
            view.Person("anselm");
            Assert.That(view.Last.Unique, Is.EqualTo(0));
            view.Slot(0);
            Assert.That(view.Last.Tree, Is.Null);
            Assert.That(view.Last.Slot, Is.EqualTo(0));
        }

        [Test]
        public void TappingAStageAcquiredSetsItAtOnce()
        {
            view.Person("toma");
            view.Unique(0);
            view.Stage(2);
            Assert.That(store.Uniques["toma"][0].Progress.Set, Is.EqualTo(2));
            Assert.That(view.Last.Uniques[0].Card.Name, Is.EqualTo("マナバースト・極"));
            Assert.That(view.Last.Tree.Stages[2].Set, Is.True);
            // 手前の段階（最初の段階）にも戻せる。変わったことは見ればわかるので知らせない。
            view.Stage(0);
            Assert.That(view.Last.Uniques[0].Card.Name, Is.EqualTo("マナバースト"));
            Assert.That(view.Notices, Is.Empty);
        }

        [Test]
        public void StagesNotAcquiredOrOfALockedSkillCannotBeSet()
        {
            view.Person("toma");
            view.Unique(0);
            view.Stage(4);
            view.Stage(UniqueSkillTreeShape.Count);
            Assert.That(store.Uniques["toma"][0].Progress.Set, Is.EqualTo(1));

            // 未解放の星降りは、ツリーを見られるが付け替えられない。
            view.Unique(1);
            Assert.That(view.Last.Tree.Locked, Is.True);
            view.Stage(0);
            Assert.That(store.Uniques["toma"][1].Progress.Set, Is.EqualTo(0));

            // 固有スキルを選んでいる間は、右のカードを押してもカスタムスキルは変わらない。
            var before = store.Cards["toma"].ToArray();
            view.Card("Embers");
            Assert.That(store.Cards["toma"], Is.EqualTo(before));
        }

        [Test]
        public void TheTreeShapeBranchesThreeWaysAndGoesOneStepFurther()
        {
            Assert.That(UniqueSkillTreeShape.Children(0), Is.EqualTo(new[] { 1, 3, 5 }));
            Assert.That(UniqueSkillTreeShape.Children(3), Is.EqualTo(new[] { 4 }));
            Assert.That(UniqueSkillTreeShape.Children(4), Is.Empty);
            Assert.That(
                Enumerable.Range(0, UniqueSkillTreeShape.Count).Select(UniqueSkillTreeShape.Parent),
                Is.EqualTo(new[] { -1, 0, 1, 0, 3, 0, 5 })
            );
            Assert.That(UniqueSkillTreeShape.StageAt(2, 2), Is.EqualTo(6));
            Assert.That(UniqueSkillTreeShape.Branch(4), Is.EqualTo(1));
            // どのツリーにも最初の段階があり、作った段階は進化する前の段階もある。
            foreach (var tree in UniqueSkillTrees.All)
            {
                Assert.That(tree.Root, Is.Not.Null, tree.Id);
                for (int stage = 1; stage < UniqueSkillTreeShape.Count; stage++)
                    if (tree.StageAt(stage) != null)
                        Assert.That(
                            tree.StageAt(UniqueSkillTreeShape.Parent(stage)),
                            Is.Not.Null,
                            $"{tree.Id} {stage}"
                        );
            }
        }

        [Test]
        public void TheSessionKeepsTheStageSetAndLeavesTheMockAlone()
        {
            var mock = new UniqueSkillProgress { Set = 1, Acquired = new[] { 1, 2 } };
            // 画面の生成などで、すでに読まれた進み具合を残さない。
            UniqueSkillSession.Reset();
            try
            {
                Assert.That(UniqueSkillSession.ProgressOf("toma", 0, mock).SetStage, Is.EqualTo(1));
                Assert.That(UniqueSkillSession.SetStage("toma", 0, 2, mock), Is.True);
                Assert.That(UniqueSkillSession.SetStage("toma", 0, 2, mock), Is.False);
                Assert.That(UniqueSkillSession.SetStage("toma", 0, 3, mock), Is.False);
                Assert.That(UniqueSkillSession.ProgressOf("toma", 0, mock).SetStage, Is.EqualTo(2));
                Assert.That(mock.Set, Is.EqualTo(1));
                UniqueSkillSession.Reset();
                Assert.That(UniqueSkillSession.ProgressOf("toma", 0, mock).SetStage, Is.EqualTo(1));
            }
            finally
            {
                UniqueSkillSession.Reset();
            }
        }

        [Test]
        public void TheRightListsTheCardsThePersonCanSetByCost()
        {
            view.Person("toma");
            var state = view.Last;
            // トーマは炎と氷、属性のないカードは誰でも付けられる（仮）。
            Assert.That(state.Usable, Is.EqualTo(new[] { CardElement.Fire, CardElement.Ice }));
            Assert.That(
                state.Choices.Select(card => card.Element).Distinct(),
                Is.SubsetOf(new[] { CardElement.None, CardElement.Fire, CardElement.Ice })
            );
            Assert.That(state.Choices.Select(card => card.Id), Does.Contain("Heal"));
            Assert.That(state.Choices.Select(card => card.Id), Does.Not.Contain("Slash"));
            Assert.That(state.Choices.Select(card => card.Cost), Is.Ordered);
            Assert.That(state.Tab, Is.EqualTo(CardLoadoutPresenter.SkillTab));
            // 付けているカードには、何枚目に入っているかを出す。
            Assert.That(state.Choices.Single(card => card.Id == "Ice").Slot, Is.EqualTo(1));
            Assert.That(state.Choices.Single(card => card.Id == "Embers").Slot, Is.EqualTo(-1));
        }

        [Test]
        public void TheItemTabListsNothingAndKeepsTheSlotAndTabAcrossPeople()
        {
            view.Person("toma");
            view.Slot(1);
            view.Tab(CardLoadoutPresenter.ItemTab);
            var state = view.Last;
            // 持ち物を読んでいない仮データでは、アイテムのタブには何も並べず、左の枠の選択は変えない。
            Assert.That(state.Tab, Is.EqualTo(CardLoadoutPresenter.ItemTab));
            Assert.That(state.Choices, Is.Empty);
            Assert.That(state.Slot, Is.EqualTo(1));
            // アイテムのタブでは、スキルを押しても入れない。
            var before = store.Cards["toma"].ToArray();
            view.Card("Embers");
            Assert.That(store.Cards["toma"], Is.EqualTo(before));

            // 人を替えても、枠とタブはそのまま。スキルのタブに戻すと一覧を並べる。
            view.Next();
            Assert.That(view.Last.Tab, Is.EqualTo(CardLoadoutPresenter.ItemTab));
            Assert.That(view.Last.Slot, Is.EqualTo(1));
            view.Tab(CardLoadoutPresenter.SkillTab);
            Assert.That(view.Last.Choices, Is.Not.Empty);
        }

        [Test]
        public void ACardGoesIntoTheChosenSlotAtOnce()
        {
            view.Person("toma");
            view.Slot(1);
            view.Card("Embers");

            Assert.That(store.Cards["toma"], Is.EqualTo(new[] { "Fire", "Embers", null }));
            Assert.That(view.Notices, Is.Empty);
            Assert.That(view.Last.Slots[1].Id, Is.EqualTo("Embers"));
            Assert.That(view.Last.Slot, Is.EqualTo(1));
        }

        [Test]
        public void ACardInAnotherSlotSwapsWithTheChosenSlot()
        {
            view.Person("toma");
            view.Card("Ice");

            Assert.That(store.Cards["toma"], Is.EqualTo(new[] { "Ice", "Fire", null }));
            Assert.That(view.Notices, Is.Empty);
        }

        [Test]
        public void TheSameCardOrOneThePersonCannotSetChangesNothing()
        {
            view.Person("toma");
            view.Card("Fire");
            view.Card("Slash");
            view.Card("NoSuchCard");

            Assert.That(store.Cards["toma"], Is.EqualTo(new[] { "Fire", "Ice" }));
            Assert.That(view.Notices, Is.Empty);
        }

        [Test]
        public void ACompanionOutsideThePartyCanSetCardsToo()
        {
            view.Person("luka");
            view.Slot(1);
            view.Card("PoisonNeedle");

            Assert.That(store.Cards["luka"][1], Is.EqualTo("PoisonNeedle"));
            Assert.That(view.Notices, Is.Empty);
        }

        [Test]
        public void SwitchingPeopleKeepsTheSlotAndIsRemembered()
        {
            view.Slot(1);
            view.Person("anselm");

            Assert.That(view.Last.PersonId, Is.EqualTo("anselm"));
            Assert.That(view.Last.Slot, Is.EqualTo(1));
            Assert.That(store.Selected, Is.EqualTo("anselm"));

            // 開き直すと、最後に見ていた人から見せる。
            presenter.Dispose();
            presenter = Open();
            Assert.That(view.Last.PersonId, Is.EqualTo("anselm"));
        }

        [Test]
        public void NumbersAreThePartOfTheStatNotThePersonsPower()
        {
            // ファイアは 属攻の150%。誰が付けても、ステータスによらず割合で書く。
            view.Person("toma");
            Assert.That(view.Last.Slots[0].Description, Does.Contain(">属攻の150%<"));
            view.Person("anselm");
            Assert.That(view.Last.Slots[1].Description, Does.Contain(">属攻の150%<"));
        }

        [Test]
        public void ACardShowsAtOnceAndTheSavedCardsAfter()
        {
            var calls = new List<(string, int, string)>();
            var pending = new TaskCompletionSource<bool>();
            presenter.Dispose();
            presenter = Open(
                (id, slot, skill) =>
                {
                    calls.Add((id, slot, skill));
                    // 保存する関数は、すぐに store へ当てる（PartySession と同じ）。
                    var cards = store.Cards[id].ToArray();
                    CardLoadoutRules.Apply(cards, slot, skill, out _);
                    store.Cards[id] = cards;
                    return SavedAsync(id);
                }
            );

            async Task SavedAsync(string id)
            {
                await pending.Task;
                // サーバーが保存したカードを、store が読み直す。
                store.Cards[id] = new[] { "Fire", "Embers" };
            }

            view.Person("toma");
            view.Slot(1);
            view.Card("Embers");

            // 保存を待たずに枠を変え、続けて押しても受け付ける。
            Assert.That(view.Last.Slots[1].Id, Is.EqualTo("Embers"));
            Assert.That(presenter.ChooseTask.IsCompleted, Is.False);
            view.Slot(0);
            view.Card("FlamePillar");
            Assert.That(view.Last.Slots[0].Id, Is.EqualTo("FlamePillar"));
            Assert.That(
                calls,
                Is.EqualTo(new[] { ("toma", 1, "Embers"), ("toma", 0, "FlamePillar") })
            );

            pending.SetResult(true);
            Assert.That(presenter.ChooseTask.IsCompleted, Is.True);
            Assert.That(view.Last.Slots[1].Id, Is.EqualTo("Embers"));
            Assert.That(view.Notices, Is.Empty);
        }

        [Test]
        public void AFailedSaveShowsTheCardsPutBack()
        {
            presenter.Dispose();
            var applied = false;
            presenter = Open(
                (id, slot, skill) =>
                {
                    // すぐに当てたあと、保存できずに戻す（知らせるのは SaveQueue）。
                    applied = store.Cards[id][slot] == "Ice";
                    return Task.CompletedTask;
                }
            );
            view.Person("toma");
            view.Slot(1);
            view.Card("Embers");

            Assert.That(applied, Is.True);
            Assert.That(presenter.ChooseTask.IsCompleted, Is.True);
            Assert.That(store.Cards["toma"][1], Is.EqualTo("Ice"));
            Assert.That(view.Last.Slots[1].Id, Is.EqualTo("Ice"));
            Assert.That(view.Notices, Is.Empty);
        }

        [Test]
        public void TheAdventurersPageOpensOnePersonAndSlot()
        {
            CardLoadoutSession.Selected = "luka";
            presenter.Dispose();
            presenter = new CardLoadoutPresenter(
                view,
                CardLoadoutPresenter.People(formation),
                new SessionSelected(store),
                slot: 1
            );
            Assert.That(view.Last.PersonId, Is.EqualTo("luka"));
            Assert.That(view.Last.Slot, Is.EqualTo(1));
            // 一覧・個別・装備と同じ人を共有する。
            Assert.That(PartySession.Selected, Is.EqualTo("luka"));
        }

        [Test]
        public void ThePartyKeepsTheCardsSetWhileTheAppRuns()
        {
            var data = ScriptableObject.CreateInstance<PartyMockData>();
            data.Members = new[] { Member("toma", "トーマ", "Fire", "Ice") };
            try
            {
                Assert.That(
                    PartySession.CardsOf(data, "toma"),
                    Is.EqualTo(new[] { "Fire", "Ice" })
                );
                PartySession.SetCards("toma", new[] { "Embers", "Ice" });
                Assert.That(PartySession.CardsOf(data, "toma")[0], Is.EqualTo("Embers"));
                // 仮データそのものは書き換えない。
                Assert.That(data.Members[0].Cards[0].Skill, Is.EqualTo("Fire"));
                PartySession.Reset();
                Assert.That(PartySession.CardsOf(data, "toma")[0], Is.EqualTo("Fire"));
                Assert.That(PartySession.CardsOf(data, "nobody"), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private CardLoadoutPresenter Open(Func<string, int, string, Task> save = null)
        {
            // スキルのタブを開いたときと同じ、1つ目のカスタムスキルの枠を選んで開く。
            return new CardLoadoutPresenter(
                view,
                CardLoadoutPresenter.People(formation),
                store,
                save,
                slot: 0
            );
        }

        private static CardLoadoutUnique Unique(
            string tree,
            bool unlocked,
            UniqueSkillProgress progress
        ) =>
            new()
            {
                Tree = UniqueSkillTrees.Find(tree),
                UnlockLevel = unlocked ? 5 : 20,
                Unlocked = unlocked,
                Progress = progress,
            };

        internal static PartyMember Member(string id, string name, params string[] cards) =>
            new()
            {
                Id = id,
                Name = name,
                Cards = cards.Select(skill => new PartyCard { Skill = skill }).ToArray(),
            };

        // The store, but the person on screen is the session's, as in the running app.
        private sealed class SessionSelected : ICardLoadoutStore
        {
            private readonly ICardLoadoutStore store;

            public SessionSelected(ICardLoadoutStore store) => this.store = store;

            public IReadOnlyList<string> CardsOf(string id) => store.CardsOf(id);

            public void SetCards(string id, IReadOnlyList<string> skills) =>
                store.SetCards(id, skills);

            public string Selected
            {
                get => CardLoadoutSession.Selected;
                set => CardLoadoutSession.Selected = value;
            }

            public IReadOnlyList<CardLoadoutUnique> UniquesOf(string id) => store.UniquesOf(id);

            public bool SetUniqueStage(string id, int index, int stage) =>
                store.SetUniqueStage(id, index, stage);
        }
    }
}
