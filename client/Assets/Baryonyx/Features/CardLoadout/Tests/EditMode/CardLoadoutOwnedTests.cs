using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Inventory;
using Baryonyx.Party;
using NUnit.Framework;
using static Baryonyx.Tests.EditMode.CardLoadoutPresenterTests;

namespace Baryonyx.Tests.EditMode
{
    // サーバーから読んだ持ち物（持っているスキルの1枚ずつとアイテム）から付け替える（doc/features/party.md）。
    public sealed class CardLoadoutOwnedTests
    {
        // 持ち物を持つ store。付け替えは PartyState.WithCard と同じ決まりで当てる。
        private sealed class OwnedStore : ICardLoadoutStore
        {
            public PartyState State;

            public IReadOnlyList<string> CardsOf(string id) =>
                State.Find(id)?.Cards ?? Array.Empty<string>();

            public void SetCards(string id, IReadOnlyList<string> skills)
            {
                var before = State.Find(id).Cards;
                for (int i = 0; i < skills.Count; i++)
                    if (skills[i] != before[i])
                        State = State.WithCard(id, i, skills[i]);
            }

            public string Selected { get; set; }

            public IReadOnlyList<CardLoadoutUnique> UniquesOf(string id) =>
                Array.Empty<CardLoadoutUnique>();

            public bool SetUniqueStage(string id, int index, int stage) => false;

            public IReadOnlyList<string> OwnedSkills =>
                State.SkillCards.Select(card => card.Key).ToArray();

            public IReadOnlyList<PartyItem> OwnedItems => State.Items;

            public CardSkill SkillOf(string key) => State.SkillOf(key);
        }

        private const string TomaFire = "card:toma-fire";
        private const string TomaIce = "card:toma-ice";
        private const string LukaThunder = "card:luka-thunder";
        private const string GoodFire = "card:good-fire";
        private const string Embers = "card:embers";

        private FakeView view;
        private OwnedStore store;
        private CardLoadoutPresenter presenter;

        [SetUp]
        public void Open()
        {
            var roster = new[]
            {
                Member("toma", "トーマ", "Fire", "Ice"),
                Member("luka", "ルカ", "VitalThrust", "Thunder"),
            };
            store = new OwnedStore
            {
                State = new PartyState(
                    new[]
                    {
                        new PartyCharacterState("toma", 12, new[] { TomaFire, TomaIce }),
                        new PartyCharacterState("luka", 11, new[] { LukaThunder, null }),
                    },
                    new[] { "toma", "luka", null, null },
                    0,
                    30,
                    100,
                    new[]
                    {
                        new PartySkillCard("toma-fire", "Fire", 1f),
                        new PartySkillCard("toma-ice", "Ice", 1f),
                        new PartySkillCard("luka-thunder", "Thunder", 1f),
                        // 冒険で手に入れた、品質の高いファイアと火の粉。
                        new PartySkillCard("good-fire", "Fire", 2.3f),
                        new PartySkillCard("embers", "Embers", 1.2f),
                    },
                    new[] { new PartyItem("herb", 1), new PartyItem("acorn", 2) }
                ),
            };
            view = new FakeView();
            presenter = new CardLoadoutPresenter(
                view,
                CardLoadoutPresenter.People(new PartyFormation(roster, new[] { "toma", "luka" })),
                store
            );
        }

        [TearDown]
        public void Dispose()
        {
            presenter?.Dispose();
            CardLoadoutSession.Reset();
            PartySession.Reset();
        }

        [Test]
        public void TheRightListsEveryOwnedCardTheyCanSetWithItsQuality()
        {
            var state = view.Last;
            Assert.That(state.PersonId, Is.EqualTo("toma"));
            // 同じファイアでも、1枚ずつ別の行になり、品質の高い順に並ぶ。持っていないスキルは出ない。
            Assert.That(
                state.Choices.Select(card => card.Id),
                Is.EquivalentTo(new[] { TomaFire, GoodFire, TomaIce, Embers })
            );
            var fires = state.Choices.Where(card => card.Name == "ファイア").ToArray();
            Assert.That(fires.Select(card => card.Id), Is.EqualTo(new[] { GoodFire, TomaFire }));
            Assert.That(fires[0].Quality, Is.EqualTo(ItemQuality.S));
            // 雷のカード（ルカのサンダー）はトーマには付けられない。
            Assert.That(state.Choices.Select(card => card.Id), Does.Not.Contain(LukaThunder));
            Assert.That(state.Slots[0].Id, Is.EqualTo(TomaFire));
            Assert.That(state.Slots[0].ArtId, Is.EqualTo("Fire"));
            Assert.That(state.Choices.Single(card => card.Id == TomaIce).Mark, Is.EqualTo("2枚目"));
        }

        [Test]
        public void ABetterCopyReplacesTheSlotAndACardAnotherHoldsMovesHere()
        {
            view.Card(GoodFire);
            Assert.That(store.CardsOf("toma"), Is.EqualTo(new[] { GoodFire, TomaIce }));
            Assert.That(view.Last.Slots[0].Quality, Is.EqualTo(ItemQuality.S));

            // アイテムは誰でも使えるので、ルカの空いた枠に入れられる。
            view.Next();
            Assert.That(view.Last.PersonId, Is.EqualTo("luka"));
            view.Slot(1);
            view.Tab(CardLoadoutPresenter.ItemTab);
            var items = view.Last.Choices;
            Assert.That(items.Select(card => card.ItemId), Is.EqualTo(new[] { "herb", "acorn" }));
            Assert.That(items.Select(card => card.Mark), Is.EqualTo(new[] { "所持 1", "所持 2" }));
            view.Card("item:herb");
            Assert.That(store.CardsOf("luka"), Is.EqualTo(new[] { LukaThunder, "item:herb" }));
            Assert.That(view.Last.Slots[1].Name, Is.EqualTo("薬草"));
        }

        [Test]
        public void AnItemFitsAsManySlotsAsThePlayerHasOfIt()
        {
            view.Slot(1);
            view.Tab(CardLoadoutPresenter.ItemTab);
            view.Card("item:herb");
            Assert.That(store.CardsOf("toma"), Is.EqualTo(new[] { TomaFire, "item:herb" }));

            // 薬草は1つだけなので、ルカは入れられない。どんぐりは2つあるので入れられる。
            view.Next();
            view.Slot(1);
            var herb = view.Last.Choices.Single(card => card.ItemId == "herb");
            Assert.That(herb.Unavailable, Is.True);
            view.Card("item:herb");
            Assert.That(store.CardsOf("luka"), Is.EqualTo(new[] { LukaThunder, null }));
            view.Card("item:acorn");
            Assert.That(store.CardsOf("luka"), Is.EqualTo(new[] { LukaThunder, "item:acorn" }));
        }

        [Test]
        public void AnOwnedCardHeldByAnotherIsMarkedAndMovesWhenSet()
        {
            // ルカが属性のない「偵察」を使っているとき、トーマの一覧では「ルカが使用中」と出る。
            store.State = new PartyState(
                store.State.Owned,
                store.State.Slots,
                0,
                30,
                100,
                store.State.SkillCards.Append(new PartySkillCard("scout", "Scout", 1f)).ToArray(),
                store.State.Items
            ).WithCard("luka", 1, "card:scout");
            presenter.Refresh();
            var scout = view.Last.Choices.Single(card => card.Id == "card:scout");
            Assert.That(scout.Mark, Is.EqualTo("ルカが使用中"));

            view.Card("card:scout");
            Assert.That(store.CardsOf("toma"), Is.EqualTo(new[] { "card:scout", TomaIce }));
            Assert.That(store.CardsOf("luka"), Is.EqualTo(new[] { LukaThunder, null }));
        }
    }
}
