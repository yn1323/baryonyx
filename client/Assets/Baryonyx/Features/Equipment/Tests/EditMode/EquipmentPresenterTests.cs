using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Equipment;
using Baryonyx.Party;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class EquipmentPresenterTests
    {
        private sealed class FakeView : IEquipmentView
        {
            public event Action PrevPressed;
            public event Action NextPressed;
            public event Action<int> SlotPressed;
            public event Action<string> ItemPressed;
            public event Action RemovePressed;

            public EquipmentViewState Last;
            public readonly List<string> Notices = new();

            public void Render(EquipmentViewState state) => Last = state;

            public void ShowNotice(string message) => Notices.Add(message);

            public void Prev() => PrevPressed?.Invoke();

            public void Next() => NextPressed?.Invoke();

            // ▶を押して、その人まで進む。
            public void Person(string id)
            {
                for (int i = 0; i < 10 && Last.PersonId != id; i++)
                    Next();
            }

            public void Slot(EquipmentSlot slot) => SlotPressed?.Invoke((int)slot);

            public void SlotAt(int index) => SlotPressed?.Invoke(index);

            public void Item(string id) => ItemPressed?.Invoke(id);

            public void Remove() => RemovePressed?.Invoke();
        }

        // 保存が終わるまで待たせたり、失敗させたりできる装備の置き場。中身はアプリ内の置き場に任せる。
        private sealed class GatedSource : IEquipmentSource
        {
            private readonly EquipmentLocalSource inner = new();

            public bool Fail { get; set; }
            public TaskCompletionSource<bool> Gate { get; set; }
            public int Saves { get; private set; }

            public Task<EquipmentState> LoadAsync(CancellationToken token) =>
                inner.LoadAsync(token);

            public async Task<EquipmentState> EquipAsync(
                string characterId,
                EquipmentPlace slot,
                string itemId,
                CancellationToken token
            )
            {
                await Wait();
                return await inner.EquipAsync(characterId, slot, itemId, token);
            }

            public async Task<EquipmentState> UnequipAsync(
                string characterId,
                EquipmentPlace slot,
                CancellationToken token
            )
            {
                await Wait();
                return await inner.UnequipAsync(characterId, slot, token);
            }

            private async Task Wait()
            {
                Saves++;
                if (Gate != null)
                    await Gate.Task;
                if (Fail)
                    throw new InvalidOperationException("offline");
            }
        }

        private FakeView view;
        private GatedSource source;
        private EquipmentPresenter presenter;
        private string remembered;

        [SetUp]
        public void CreatePresenter()
        {
            var roster = new[]
            {
                Member("toma", "トーマ"),
                Member("luka", "ルカ"),
                Member("aria", "アリア"),
                Member("mina", "ミナ"),
                Member("anselm", "アンセルム"),
            };
            // ルカをパーティから外し、空きの枠も残す。
            var formation = new PartyFormation(roster, new[] { "toma", null, "aria", "mina" });
            var (people, _) = formation.TabOrder();
            view = new FakeView();
            source = new GatedSource();
            presenter = new EquipmentPresenter(
                view,
                people,
                EquipmentLocalSource.Starter(),
                source,
                remember: id => remembered = id
            );
        }

        [TearDown]
        public void DisposePresenter() => presenter?.Dispose();

        // 左のステータスには装備の上昇分、右の行には付け替えたときの増減を出す（doc/features/screens/loadout.md#装備）。
        [Test]
        public void ShowsTheRaiseAndHowEachItemWouldChangeTheStats()
        {
            presenter.Dispose();
            var toma = new Combat.CharacterStats(309, 94, 148, 282, 248, 188, 40, 67);
            presenter = new EquipmentPresenter(
                view,
                new[] { Member("toma", "トーマ") },
                EquipmentLocalSource.Starter(),
                source,
                statsOf: id => toma
            );
            var state = view.Last;
            // 樫の杖（属攻 +11%）と魔法のローブ（属攻 +12%・物防 +11%）。
            Assert.That(state.Raise.MagicAttack, Is.EqualTo(65));
            Assert.That(state.Stats.Value.MagicAttack, Is.EqualTo(347));
            Assert.That(state.Raise.PhysicalAttack, Is.Zero);

            // 付けている樫の杖の行は出さず、氷晶のワンド（属攻 +14%・会心 +5%）は属攻と会心が上がる。
            var worn = state.Choices.Single(choice => choice.Name == "樫の杖" && choice.Worn);
            Assert.That(worn.Changes, Is.Empty);
            var wand = state.Choices.Single(choice => choice.Name == "氷晶のワンド");
            Assert.That(wand.Changes, Is.EqualTo(new[] { (3, 8), (6, 2) })); // 282 × 26% = 73.32
            // 雷鳴の槍（物攻 +14%）は物攻が上がり、属攻が下がる。
            var spear = state.Choices.Single(choice => choice.Name == "雷鳴の槍");
            Assert.That(spear.Changes, Is.EqualTo(new[] { (1, 13), (3, -31) }));
            Assert.That(
                EquipmentView.ChangeText(spear.Changes),
                Does.StartWith("物攻 <color=#")
                    .And.Contains(">▲13</color>　属攻 <color=#")
                    .And.EndWith(">▼31</color>")
            );
        }

        [Test]
        public void OpensOnTheFirstPersonsWeaponWithTheOwnedWeaponsByRarity()
        {
            var state = view.Last;
            Assert.That(state.PersonId, Is.EqualTo("toma"));
            Assert.That(state.CanSwitch, Is.True);
            Assert.That(state.Slot, Is.EqualTo(EquipmentSlot.Weapon));
            Assert.That(state.Title, Is.EqualTo("武器"));
            Assert.That(
                state.Slots.Select(slot => slot.Name),
                Is.EqualTo(new[] { "樫の杖", "魔法のローブ" })
            );
            Assert.That(state.Slots[0].Stars, Is.EqualTo("★"));
            Assert.That(state.Slots[0].Detail, Is.EqualTo("属攻 +11%"));
            Assert.That(state.CanRemove, Is.True);

            var weapons = EquipmentCatalog
                .All.Where(entry => entry.Slot == EquipmentSlot.Weapon)
                .ToArray();
            Assert.That(
                state.Choices.Select(choice => choice.EquipmentId),
                Is.EqualTo(
                    weapons
                        .OrderByDescending(entry => entry.Rarity)
                        // 樫の杖は、トーマとミナの2本を持っている。
                        .SelectMany(entry =>
                            entry.Id == "oak-staff"
                                ? new[] { entry.Id, entry.Id }
                                : new[] { entry.Id }
                        )
                )
            );
            // 自分が付けている装備は「装備中」、ほかの人の装備は誰が付けているかを出す。
            Assert.That(Choice("oak-staff").Mark, Is.EqualTo("装備中"));
            Assert.That(Choice("oak-staff").Worn, Is.True);
            Assert.That(Choice("iron-sword").Mark, Is.EqualTo("アリアが装備中"));
            Assert.That(Choice("iron-sword").Worn, Is.False);
            Assert.That(Choice("flame-dagger").Mark, Is.Empty);
        }

        // アクセサリーの1つ目の枠はLv1から開いていて、持っているアクセサリーを付け、ほかの枠へ移せる。
        // 2つ目と3つ目はLv20・Lv40で開くまで、一覧が空で付けられない。
        [Test]
        public async Task AnAccessorySlotTakesAnAccessoryOnceItIsOpen()
        {
            view.SlotAt(2);
            Assert.That(view.Last.Accessory, Is.EqualTo(0));
            Assert.That(view.Last.Title, Is.EqualTo(EquipmentPresenter.AccessoryTitle));
            Assert.That(
                view.Last.Choices.Select(choice => EquipmentCatalog.Find(choice.EquipmentId).Slot),
                Is.Not.Empty.And.All.EqualTo(EquipmentSlot.Accessory)
            );
            string charm = Choice("moss-charm").ItemId;
            presenter.Choose(charm);
            await presenter.ChangeTask;
            Assert.That(view.Notices, Is.Empty);
            Assert.That(view.Last.Accessories[0].ItemId, Is.EqualTo(charm));
            Assert.That(view.Last.CanRemove, Is.True);
            Assert.That(
                source.LoadAsync(default).Result.ItemIn("toma", EquipmentPlace.Accessory(0)),
                Is.EqualTo(charm)
            );

            // トーマ（Lv1）の2つ目の枠はまだ開いていない。
            view.SlotAt(3);
            Assert.That(view.Last.Choices, Is.Empty);
            presenter.Choose(charm);
            await presenter.ChangeTask;
            Assert.That(view.Last.Accessories[1].ItemId, Is.Null);

            view.SlotAt(2);
            presenter.Remove();
            await presenter.ChangeTask;
            Assert.That(view.Last.Accessories[0].ItemId, Is.Null);
        }

        [Test]
        public async Task AHighLevelPersonWearsAnAccessoryInTheThirdSlot()
        {
            presenter.Dispose();
            presenter = new EquipmentPresenter(
                view,
                new[] { Member("toma", "トーマ") },
                EquipmentLocalSource.Starter(),
                source,
                accessory: 2,
                levelOf: _ => 40
            );
            string pearl = Choice("kraken-pearl").ItemId;
            presenter.Choose(pearl);
            await presenter.ChangeTask;
            Assert.That(view.Last.Accessories[2].ItemId, Is.EqualTo(pearl));
            Assert.That(view.Last.Accessories[2].Name, Is.EqualTo("大ダコの黒真珠"));
        }

        [Test]
        public async Task ChoosingAnUnwornItemReplacesTheSlotsItem()
        {
            presenter.Choose(Choice("flame-dagger").ItemId);
            await presenter.ChangeTask;

            Assert.That(view.Notices, Is.Empty);
            Assert.That(view.Last.Slots[0].Name, Is.EqualTo("炎のダガー"));
            Assert.That(Choice("flame-dagger").Mark, Is.EqualTo("装備中"));
            Assert.That(Choice("oak-staff").Mark, Is.Empty);
        }

        [Test]
        public async Task ChoosingAnItemSomeoneElseWearsMovesIt()
        {
            presenter.Choose(Choice("iron-sword").ItemId);
            await presenter.ChangeTask;

            Assert.That(view.Notices, Is.Empty);
            Assert.That(view.Last.Slots[0].Name, Is.EqualTo("鉄の剣"));
            view.Person("aria");
            Assert.That(view.Last.Slots[0].ItemId, Is.Null);
            Assert.That(view.Last.Slots[0].Name, Is.EqualTo("なし"));
            Assert.That(view.Last.CanRemove, Is.False);
            Assert.That(remembered, Is.EqualTo("aria"));
        }

        [Test]
        public async Task TheArmourSlotListsArmourAndAnEmptySlotTakesAnItem()
        {
            view.Person("anselm");
            Assert.That(
                view.Last.Slots.Select(slot => slot.ItemId),
                Is.EqualTo(new string[] { null, null })
            );
            view.Slot(EquipmentSlot.Armor);
            Assert.That(view.Last.Title, Is.EqualTo("防具"));
            Assert.That(
                view.Last.Choices.Select(choice => EquipmentCatalog.Find(choice.EquipmentId).Slot),
                Is.All.EqualTo(EquipmentSlot.Armor)
            );

            presenter.Choose(Choice("iron-helm").ItemId);
            await presenter.ChangeTask;
            Assert.That(view.Notices, Is.Empty);
            Assert.That(view.Last.Slots[1].Name, Is.EqualTo("鉄の兜"));

            // 武器を押しても、防具の枠には入らない。
            int saves = source.Saves;
            presenter.Choose(view.Last.Slots[0].ItemId ?? "local-1");
            await presenter.ChangeTask;
            Assert.That(source.Saves, Is.EqualTo(saves));
        }

        [Test]
        public async Task RemoveTakesTheSlotsItemOff()
        {
            view.Remove();
            await presenter.ChangeTask;
            Assert.That(view.Notices, Is.Empty);
            Assert.That(view.Last.Slots[0].ItemId, Is.Null);
            Assert.That(view.Last.CanRemove, Is.False);
            // 外した装備は持ち物に残る。
            Assert.That(Choice("oak-staff").Mark, Is.Empty);

            int saves = source.Saves;
            view.Remove();
            await presenter.ChangeTask;
            Assert.That(source.Saves, Is.EqualTo(saves));
        }

        [Test]
        public async Task AFailedSaveLeavesTheScreenAndTellsSo()
        {
            source.Fail = true;
            var before = view.Last.Slots[0].ItemId;
            presenter.Choose(Choice("flame-dagger").ItemId);
            await presenter.ChangeTask;

            Assert.That(view.Notices, Is.EqualTo(new[] { EquipmentPresenter.SaveFailedMessage }));
            Assert.That(view.Last.Slots[0].ItemId, Is.EqualTo(before));
        }

        [Test]
        public async Task AChangeShowsAtOnceAndPressesGoOnWhileSaving()
        {
            source.Gate = new TaskCompletionSource<bool>();
            presenter.Choose(Choice("flame-dagger").ItemId);
            var first = presenter.ChangeTask;
            // 保存を待たずに枠を変える。
            Assert.That(first.IsCompleted, Is.False);
            Assert.That(view.Last.Slots[0].Name, Is.EqualTo("炎のダガー"));
            presenter.Choose(Choice("thunder-spear").ItemId);
            Assert.That(view.Last.Slots[0].Name, Is.EqualTo(Choice("thunder-spear").Name));
            Assert.That(source.Saves, Is.EqualTo(2));

            source.Gate.SetResult(true);
            await first;
            await presenter.ChangeTask;
            Assert.That(view.Last.Slots[0].Name, Is.EqualTo(Choice("thunder-spear").Name));
            Assert.That(view.Notices, Is.Empty);
        }

        // 枠と行のキラキラには、その1つの品質と、種類ごとの光り方（デバッグルームと同じ）を渡す。
        [Test]
        public void ItemsCarryTheirQualityAndSparkle()
        {
            presenter.Dispose();
            var state = new EquipmentState(
                new[]
                {
                    new EquipmentItem("top", "flame-dagger", 1.8f),
                    new EquipmentItem("plain", "iron-sword"),
                },
                new[] { ("toma", (EquipmentPlace)EquipmentSlot.Weapon, "top") }
            );
            var (people, _) = new PartyFormation(
                new[] { Member("toma", "トーマ") },
                new[] { "toma" }
            ).TabOrder();
            presenter = new EquipmentPresenter(view, people, state, source);
            var worn = view.Last.Slots[0];
            Assert.That(worn.Quality, Is.EqualTo(Inventory.ItemQuality.S));
            Assert.That(worn.FoilStyle, Is.EqualTo(EquipmentCatalog.FoilStyleOf("flame-dagger")));
            var plain = view.Last.Choices.Single(item => item.ItemId == "plain");
            Assert.That(plain.Quality, Is.EqualTo(Inventory.ItemQuality.C));
            Assert.That(plain.FoilStyle, Is.EqualTo(EquipmentCatalog.FoilStyleOf("iron-sword")));
            Assert.That(worn.FoilStyle, Is.Not.EqualTo(plain.FoilStyle));
        }

        [Test]
        public void OpensOnTheRememberedPerson()
        {
            presenter.Dispose();
            var formation = new PartyFormation(
                new[] { Member("toma", "トーマ"), Member("mina", "ミナ") },
                new[] { "toma", "mina" }
            );
            var (people, _) = formation.TabOrder();
            presenter = new EquipmentPresenter(
                view,
                people,
                EquipmentLocalSource.Starter(),
                source,
                "mina",
                slot: EquipmentSlot.Armor
            );
            Assert.That(view.Last.PersonId, Is.EqualTo("mina"));
            Assert.That(
                view.Last.Slots.Select(slot => slot.Name),
                Is.EqualTo(new[] { "樫の杖", "革の鎧" })
            );
            // 渡された枠（防具）を選んで開く。
            Assert.That(view.Last.Slot, Is.EqualTo(EquipmentSlot.Armor));
        }

        [Test]
        public void ArrowsWalkThePartyThenTheOthersKeepingTheSlot()
        {
            view.Slot(EquipmentSlot.Armor);
            var order = new List<string> { view.Last.PersonId };
            for (int i = 0; i < 4; i++)
            {
                view.Next();
                order.Add(view.Last.PersonId);
                Assert.That(view.Last.Slot, Is.EqualTo(EquipmentSlot.Armor));
            }
            Assert.That(order, Is.EqualTo(new[] { "toma", "aria", "mina", "luka", "anselm" }));
            Assert.That(remembered, Is.EqualTo("anselm"));
            // 端から反対の端へ回る。
            view.Next();
            Assert.That(view.Last.PersonId, Is.EqualTo("toma"));
            view.Prev();
            Assert.That(view.Last.PersonId, Is.EqualTo("anselm"));
        }

        private EquipmentItemState Choice(string equipmentId)
        {
            // 選んでいる枠の種類にない装備は、その種類の枠に切り替えて探す。アクセサリーは、
            // アクセサリーの枠を選んでいれば、その枠のまま探す。
            var slot = EquipmentCatalog.Find(equipmentId).Slot;
            bool chosen =
                slot == EquipmentSlot.Accessory
                    ? view.Last.Accessory >= 0
                    : view.Last.Accessory < 0 && view.Last.Slot == slot;
            if (!chosen)
                view.Slot(slot);
            // 同じ種類を2つ持つ物（トーマとミナの樫の杖）は、仮データで先に作った1つ目（トーマの物）を返す。
            return view
                .Last.Choices.Where(choice => choice.EquipmentId == equipmentId)
                .OrderBy(choice => int.Parse(choice.ItemId.Substring("local-".Length)))
                .First();
        }

        private static PartyMember Member(string id, string name) => new() { Id = id, Name = name };
    }
}
