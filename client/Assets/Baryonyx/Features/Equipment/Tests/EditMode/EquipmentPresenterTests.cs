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
            public event Action<string> PersonPressed;
            public event Action<int> SlotPressed;
            public event Action<string> ItemPressed;
            public event Action RemovePressed;

            public EquipmentViewState Last;
            public readonly List<string> Notices = new();

            public void Render(EquipmentViewState state) => Last = state;

            public void ShowNotice(string message) => Notices.Add(message);

            public void Person(string id) => PersonPressed?.Invoke(id);

            public void Slot(EquipmentSlot slot) => SlotPressed?.Invoke((int)slot);

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
                EquipmentSlot slot,
                string itemId,
                CancellationToken token
            )
            {
                await Wait();
                return await inner.EquipAsync(characterId, slot, itemId, token);
            }

            public async Task<EquipmentState> UnequipAsync(
                string characterId,
                EquipmentSlot slot,
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
            var (people, partyCount) = formation.TabOrder();
            view = new FakeView();
            source = new GatedSource();
            presenter = new EquipmentPresenter(
                view,
                people,
                partyCount,
                EquipmentLocalSource.Starter(),
                source,
                remember: id => remembered = id
            );
        }

        [TearDown]
        public void DisposePresenter() => presenter?.Dispose();

        [Test]
        public void OpensOnTheFirstPersonsWeaponWithTheOwnedWeaponsByRarity()
        {
            var state = view.Last;
            Assert.That(
                state.People,
                Is.EqualTo(new[] { "toma", "aria", "mina", "luka", "anselm" })
            );
            Assert.That(state.PartyCount, Is.EqualTo(3));
            Assert.That(state.PersonId, Is.EqualTo("toma"));
            Assert.That(state.Slot, Is.EqualTo(EquipmentSlot.Weapon));
            Assert.That(state.Title, Is.EqualTo("武器"));
            Assert.That(
                state.Slots.Select(slot => slot.Name),
                Is.EqualTo(new[] { "樫の杖", "魔法のローブ" })
            );
            Assert.That(state.Slots[0].Stars, Is.EqualTo("★"));
            Assert.That(state.Slots[0].Detail, Is.EqualTo("属攻の 110%｜炎"));
            Assert.That(state.CanRemove, Is.True);

            var weapons = EquipmentCatalog
                .All.Where(entry => entry.Slot == EquipmentSlot.Weapon)
                .ToArray();
            Assert.That(state.CountText, Is.EqualTo($"所持 {weapons.Length}"));
            Assert.That(
                state.Choices.Select(choice => choice.EquipmentId),
                Is.EqualTo(
                    weapons.OrderByDescending(entry => entry.Rarity).Select(entry => entry.Id)
                )
            );
            // 自分が付けている装備は「装備中」、ほかの人の装備は誰が付けているかを出す。
            Assert.That(Choice("oak-staff").Mark, Is.EqualTo("装備中"));
            Assert.That(Choice("oak-staff").Worn, Is.True);
            Assert.That(Choice("iron-sword").Mark, Is.EqualTo("アリアが装備中"));
            Assert.That(Choice("iron-sword").Worn, Is.False);
            Assert.That(Choice("flame-dagger").Mark, Is.Empty);
        }

        [Test]
        public async Task ChoosingAnUnwornItemReplacesTheSlotsItem()
        {
            presenter.Choose(Choice("flame-dagger").ItemId);
            await presenter.ChangeTask;

            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { "トーマの「樫の杖」を「炎のダガー」に替えました" })
            );
            Assert.That(view.Last.Slots[0].Name, Is.EqualTo("炎のダガー"));
            Assert.That(Choice("flame-dagger").Mark, Is.EqualTo("装備中"));
            Assert.That(Choice("oak-staff").Mark, Is.Empty);
        }

        [Test]
        public async Task ChoosingAnItemSomeoneElseWearsMovesIt()
        {
            presenter.Choose(Choice("iron-sword").ItemId);
            await presenter.ChangeTask;

            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { "アリアの「鉄の剣」をトーマに付け替えました" })
            );
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
            Assert.That(view.Notices, Is.EqualTo(new[] { "アンセルムに「鉄の兜」を装備しました" }));
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
            Assert.That(view.Notices, Is.EqualTo(new[] { "トーマの「樫の杖」を外しました" }));
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
        public async Task PressesWhileSavingAreIgnored()
        {
            source.Gate = new TaskCompletionSource<bool>();
            presenter.Choose(Choice("flame-dagger").ItemId);
            var first = presenter.ChangeTask;
            Assert.That(presenter.Saving, Is.True);
            presenter.Choose(Choice("thunder-spear").ItemId);
            view.Remove();
            Assert.That(source.Saves, Is.EqualTo(1));

            source.Gate.SetResult(true);
            await first;
            Assert.That(presenter.Saving, Is.False);
            Assert.That(view.Last.Slots[0].Name, Is.EqualTo("炎のダガー"));
        }

        [Test]
        public void OpensOnTheRememberedPerson()
        {
            presenter.Dispose();
            var formation = new PartyFormation(
                new[] { Member("toma", "トーマ"), Member("mina", "ミナ") },
                new[] { "toma", "mina" }
            );
            var (people, partyCount) = formation.TabOrder();
            presenter = new EquipmentPresenter(
                view,
                people,
                partyCount,
                EquipmentLocalSource.Starter(),
                source,
                "mina"
            );
            Assert.That(view.Last.PersonId, Is.EqualTo("mina"));
            Assert.That(
                view.Last.Slots.Select(slot => slot.Name),
                Is.EqualTo(new[] { "戦鎚", "革の鎧" })
            );
        }

        private EquipmentItemState Choice(string equipmentId)
        {
            // 選んでいる枠の種類にない装備は、その種類の枠に切り替えて探す。
            var slot = EquipmentCatalog.Find(equipmentId).Slot;
            if (view.Last.Slot != slot)
                view.Slot(slot);
            return view.Last.Choices.Single(choice => choice.EquipmentId == equipmentId);
        }

        private static PartyMember Member(string id, string name) => new() { Id = id, Name = name };
    }
}
