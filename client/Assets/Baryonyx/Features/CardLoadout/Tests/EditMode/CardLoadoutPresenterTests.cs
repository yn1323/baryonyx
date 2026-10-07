using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Party;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class CardLoadoutPresenterTests
    {
        private sealed class FakeView : ICardLoadoutView
        {
            public event Action PrevPressed;
            public event Action NextPressed;
            public event Action<int> SlotPressed;
            public event Action<string> CardPressed;

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

            public void Card(string id) => CardPressed?.Invoke(id);
        }

        private sealed class FakeStore : ICardLoadoutStore
        {
            public readonly Dictionary<string, string[]> Cards = new();

            // 人ごとの 属攻。物攻・物防は固定。
            public readonly Dictionary<string, int> Magic = new();

            public IReadOnlyList<string> CardsOf(string id) =>
                Cards.TryGetValue(id, out var cards) ? cards : Array.Empty<string>();

            public void SetCards(string id, IReadOnlyList<string> skills) =>
                Cards[id] = skills.ToArray();

            public int StatOf(string id, CardStat stat) =>
                stat switch
                {
                    CardStat.MagicAttack => Magic.TryGetValue(id, out int magic) ? magic : 100,
                    CardStat.PhysicalAttack => 150,
                    CardStat.PhysicalDefense => 50,
                    _ => 0,
                };

            public int Act => 0;

            public string Selected { get; set; }
        }

        private PartyFormation formation;
        private FakeView view;
        private FakeStore store;
        private CardLoadoutPresenter presenter;

        [SetUp]
        public void CreatePresenter()
        {
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
                Is.EqualTo(new[] { "Heal", "HolyHammer" })
            );
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
            Assert.That(state.CountText, Is.EqualTo($"{state.Choices.Count}枚"));
            // 付けているカードには、何枚目に入っているかを出す。
            Assert.That(state.Choices.Single(card => card.Id == "Ice").Slot, Is.EqualTo(1));
            Assert.That(state.Choices.Single(card => card.Id == "Embers").Slot, Is.EqualTo(-1));
        }

        [Test]
        public void ACardGoesIntoTheChosenSlotAtOnce()
        {
            view.Person("toma");
            view.Slot(1);
            view.Card("Embers");

            Assert.That(store.Cards["toma"], Is.EqualTo(new[] { "Fire", "Embers" }));
            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { "トーマの「アイスランス」を「火の粉」に替えました" })
            );
            Assert.That(view.Last.Slots[1].Id, Is.EqualTo("Embers"));
            Assert.That(view.Last.Slot, Is.EqualTo(1));
        }

        [Test]
        public void ACardInAnotherSlotSwapsWithTheChosenSlot()
        {
            view.Person("toma");
            view.Card("Ice");

            Assert.That(store.Cards["toma"], Is.EqualTo(new[] { "Ice", "Fire" }));
            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { "トーマの「アイスランス」と「ファイア」を入れ替えました" })
            );
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
            Assert.That(
                view.Notices.Single(),
                Is.EqualTo("ルカの「サンダー」を「毒針」に替えました")
            );
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
        public void NumbersComeFromThePersonsStats()
        {
            // ファイアは 属攻の100%。
            store.Magic["toma"] = 318;
            store.Magic["anselm"] = 120;
            view.Person("toma");
            Assert.That(view.Last.Slots[0].Description, Does.Contain(">318<"));
            view.Person("anselm");
            Assert.That(view.Last.Slots[1].Description, Does.Contain(">120<"));
        }

        [Test]
        public void ACardIsShownAfterTheServerSavesIt()
        {
            var calls = new List<(string, int, string)>();
            var pending = new TaskCompletionSource<bool>();
            presenter.Dispose();
            presenter = Open(
                async (id, slot, skill, _) =>
                {
                    calls.Add((id, slot, skill));
                    await pending.Task;
                    // サーバーが保存したカードを、store が読み直す。
                    store.Cards[id] = new[] { "Fire", "Embers" };
                }
            );
            view.Person("toma");
            view.Slot(1);
            view.Card("Embers");

            // サーバーが答えるまで枠は変えず、続けて押しても受け付けない。
            Assert.That(presenter.Saving, Is.True);
            Assert.That(view.Last.Slots[1].Id, Is.EqualTo("Ice"));
            Assert.That(store.Cards["toma"][1], Is.EqualTo("Ice"));
            view.Card("FlamePillar");
            Assert.That(calls, Is.EqualTo(new[] { ("toma", 1, "Embers") }));

            pending.SetResult(true);
            Assert.That(presenter.ChooseTask.IsCompleted, Is.True);
            Assert.That(presenter.Saving, Is.False);
            Assert.That(view.Last.Slots[1].Id, Is.EqualTo("Embers"));
            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { "トーマの「アイスランス」を「火の粉」に替えました" })
            );
        }

        [Test]
        public void AFailedSaveKeepsTheCardsAndSaysSo()
        {
            presenter.Dispose();
            presenter = Open((_, _, _, _) => Task.FromException(new InvalidOperationException()));
            view.Person("toma");
            view.Slot(1);
            view.Card("Embers");

            Assert.That(presenter.ChooseTask.IsCompleted, Is.True);
            Assert.That(presenter.Saving, Is.False);
            Assert.That(store.Cards["toma"][1], Is.EqualTo("Ice"));
            Assert.That(view.Last.Slots[1].Id, Is.EqualTo("Ice"));
            Assert.That(view.Notices, Is.EqualTo(new[] { CardLoadoutPresenter.SaveFailedMessage }));
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

        private CardLoadoutPresenter Open(
            Func<string, int, string, System.Threading.CancellationToken, Task> save = null
        )
        {
            return new CardLoadoutPresenter(
                view,
                CardLoadoutPresenter.People(formation),
                store,
                save
            );
        }

        private static PartyMember Member(string id, string name, params string[] cards) =>
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

            public int StatOf(string id, CardStat stat) => store.StatOf(id, stat);

            public int Act => store.Act;

            public string Selected
            {
                get => CardLoadoutSession.Selected;
                set => CardLoadoutSession.Selected = value;
            }
        }
    }
}
