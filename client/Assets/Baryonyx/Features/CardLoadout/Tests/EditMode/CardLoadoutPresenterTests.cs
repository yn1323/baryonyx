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
            public event Action<string> PersonPressed;
            public event Action<int> SlotPressed;
            public event Action<string> CardPressed;

            public CardLoadoutState Last;
            public readonly List<string> Notices = new();

            public void Render(CardLoadoutState state) => Last = state;

            public void ShowNotice(string message) => Notices.Add(message);

            public void Person(string id) => PersonPressed?.Invoke(id);

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
                Member("toma", "トーマ", "Fire", "Meteor", "Ice", "Blizzard"),
                Member("luka", "ルカ", "VitalThrust", "ArrowRain", "Thunder", "LightningBolt"),
                Member("mina", "ミナ", "Heal", "Protect", "HolyHammer", "HolyLight"),
                Member("anselm", "アンセルム", "EarthSplitter", "HolyHammer", "Fire", "Embers"),
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
        public void TabsListThePartyFirstThenTheOthers()
        {
            var state = view.Last;
            Assert.That(state.People, Is.EqualTo(new[] { "mina", "toma", "luka", "anselm" }));
            Assert.That(state.PartyCount, Is.EqualTo(2));
            // 開いたときは、パーティの先頭の人の1枚目を選んでいる。
            Assert.That(state.PersonId, Is.EqualTo("mina"));
            Assert.That(state.Slot, Is.EqualTo(0));
            Assert.That(
                state.Slots.Select(card => card.Id),
                Is.EqualTo(new[] { "Heal", "Protect", "HolyHammer", "HolyLight" })
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
            Assert.That(state.Choices.Single(card => card.Id == "Meteor").Slot, Is.EqualTo(1));
            Assert.That(state.Choices.Single(card => card.Id == "Embers").Slot, Is.EqualTo(-1));
        }

        [Test]
        public void ACardGoesIntoTheChosenSlotAtOnce()
        {
            view.Person("toma");
            view.Slot(1);
            view.Card("Embers");

            Assert.That(
                store.Cards["toma"],
                Is.EqualTo(new[] { "Fire", "Embers", "Ice", "Blizzard" })
            );
            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { "トーマの「メテオ」を「火の粉」に替えました" })
            );
            Assert.That(view.Last.Slots[1].Id, Is.EqualTo("Embers"));
            Assert.That(view.Last.Slot, Is.EqualTo(1));
        }

        [Test]
        public void ACardInAnotherSlotSwapsWithTheChosenSlot()
        {
            view.Person("toma");
            view.Card("Ice");

            Assert.That(
                store.Cards["toma"],
                Is.EqualTo(new[] { "Ice", "Meteor", "Fire", "Blizzard" })
            );
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

            Assert.That(
                store.Cards["toma"],
                Is.EqualTo(new[] { "Fire", "Meteor", "Ice", "Blizzard" })
            );
            Assert.That(view.Notices, Is.Empty);
        }

        [Test]
        public void ACompanionOutsideThePartyCanSetCardsToo()
        {
            view.Person("luka");
            view.Slot(3);
            view.Card("PoisonNeedle");

            Assert.That(store.Cards["luka"][3], Is.EqualTo("PoisonNeedle"));
            Assert.That(
                view.Notices.Single(),
                Is.EqualTo("ルカの「ライトニングボルト」を「毒針」に替えました")
            );
        }

        [Test]
        public void SwitchingPeopleStartsAtTheFirstSlotAndIsRemembered()
        {
            view.Slot(2);
            view.Person("anselm");

            Assert.That(view.Last.PersonId, Is.EqualTo("anselm"));
            Assert.That(view.Last.Slot, Is.EqualTo(0));
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
            Assert.That(view.Last.Slots[2].Description, Does.Contain(">120<"));
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
                    store.Cards[id] = new[] { "Fire", "Embers", "Ice", "Blizzard" };
                }
            );
            view.Person("toma");
            view.Slot(1);
            view.Card("Embers");

            // サーバーが答えるまで枠は変えず、続けて押しても受け付けない。
            Assert.That(presenter.Saving, Is.True);
            Assert.That(view.Last.Slots[1].Id, Is.EqualTo("Meteor"));
            Assert.That(store.Cards["toma"][1], Is.EqualTo("Meteor"));
            view.Card("FlamePillar");
            Assert.That(calls, Is.EqualTo(new[] { ("toma", 1, "Embers") }));

            pending.SetResult(true);
            Assert.That(presenter.ChooseTask.IsCompleted, Is.True);
            Assert.That(presenter.Saving, Is.False);
            Assert.That(view.Last.Slots[1].Id, Is.EqualTo("Embers"));
            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { "トーマの「メテオ」を「火の粉」に替えました" })
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
            Assert.That(store.Cards["toma"][1], Is.EqualTo("Meteor"));
            Assert.That(view.Last.Slots[1].Id, Is.EqualTo("Meteor"));
            Assert.That(view.Notices, Is.EqualTo(new[] { CardLoadoutPresenter.SaveFailedMessage }));
        }

        [Test]
        public void AnotherScreenOpensOnePersonAndTakesBackOnce()
        {
            int back = 0;
            CardLoadoutSession.Open("luka", () => back++);
            presenter.Dispose();
            presenter = new CardLoadoutPresenter(
                view,
                CardLoadoutPresenter.People(formation).People,
                CardLoadoutPresenter.People(formation).PartyCount,
                new SessionSelected(store)
            );
            Assert.That(view.Last.PersonId, Is.EqualTo("luka"));

            Assert.That(CardLoadoutSession.TakeBack(), Is.True);
            Assert.That(back, Is.EqualTo(1));
            // 2回目以降は、酒場のメニューへ戻る（呼び出し元へは戻らない）。
            Assert.That(CardLoadoutSession.TakeBack(), Is.False);
            Assert.That(back, Is.EqualTo(1));

            CardLoadoutSession.Open("luka", () => back++);
            CardLoadoutSession.ForgetBack();
            Assert.That(CardLoadoutSession.TakeBack(), Is.False);
        }

        [Test]
        public void ThePartyKeepsTheCardsSetWhileTheAppRuns()
        {
            var data = ScriptableObject.CreateInstance<PartyMockData>();
            data.Members = new[] { Member("toma", "トーマ", "Fire", "Meteor", "Ice", "Blizzard") };
            try
            {
                Assert.That(
                    PartySession.CardsOf(data, "toma"),
                    Is.EqualTo(new[] { "Fire", "Meteor", "Ice", "Blizzard" })
                );
                PartySession.SetCards("toma", new[] { "Embers", "Meteor", "Ice", "Blizzard" });
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
            var (people, partyCount) = CardLoadoutPresenter.People(formation);
            return new CardLoadoutPresenter(view, people, partyCount, store, save);
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
