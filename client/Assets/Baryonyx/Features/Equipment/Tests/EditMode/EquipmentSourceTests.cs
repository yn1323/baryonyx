using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Equipment;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class EquipmentSourceTests
    {
        [Test]
        public void ServerAnswersBecomeTheStateWithEmptySlotsAsNull()
        {
            var state = EquipmentServerSource.ToState(
                new EquipmentApiClient.State
                {
                    change = "move",
                    from = "aria",
                    items = new[]
                    {
                        new EquipmentApiClient.Item { id = "a", equipmentId = "iron-sword" },
                        new EquipmentApiClient.Item { id = "", equipmentId = "oak-staff" },
                        null,
                        new EquipmentApiClient.Item { id = "b", equipmentId = "chainmail" },
                    },
                    characters = new[]
                    {
                        new EquipmentApiClient.Character
                        {
                            id = "toma",
                            weapon = "a",
                            armor = "",
                        },
                        new EquipmentApiClient.Character
                        {
                            id = "aria",
                            weapon = null,
                            armor = "b",
                        },
                        new EquipmentApiClient.Character { id = "", weapon = "a" },
                    },
                }
            );

            Assert.That(state.Items.Select(item => item.Id), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(state.ItemIn("toma", EquipmentSlot.Weapon), Is.EqualTo("a"));
            Assert.That(state.ItemIn("toma", EquipmentSlot.Armor), Is.Null);
            Assert.That(state.ItemIn("aria", EquipmentSlot.Weapon), Is.Null);
            Assert.That(state.WornBy("b"), Is.EqualTo("aria"));
            Assert.That(state.Change, Is.EqualTo("move"));
            Assert.That(state.From, Is.EqualTo("aria"));

            var empty = EquipmentServerSource.ToState(null);
            Assert.That(empty.Items, Is.Empty);
            Assert.That(empty.Change, Is.Empty);
            Assert.That(empty.From, Is.Null);
        }

        [Test]
        public async Task TheLocalSourceFollowsTheServersRules()
        {
            var source = new EquipmentLocalSource();
            var state = await source.LoadAsync(CancellationToken.None);
            Assert.That(state.Items.Count, Is.EqualTo(EquipmentCatalog.All.Count));
            foreach (var (character, weapon, armor) in EquipmentCatalog.StarterEquipped)
            {
                Assert.That(
                    Kind(state, state.ItemIn(character, EquipmentSlot.Weapon)),
                    Is.EqualTo(weapon)
                );
                Assert.That(
                    Kind(state, state.ItemIn(character, EquipmentSlot.Armor)),
                    Is.EqualTo(armor)
                );
            }
            string sword = Item(state, "iron-sword");
            string robe = Item(state, "magic-robe");

            // ほかのキャラの装備は、そのキャラから外して付け替える。
            var moved = await source.EquipAsync(
                "toma",
                EquipmentSlot.Weapon,
                sword,
                CancellationToken.None
            );
            Assert.That((moved.Change, moved.From), Is.EqualTo(("move", "aria")));
            Assert.That(moved.ItemIn("aria", EquipmentSlot.Weapon), Is.Null);
            Assert.That(moved.WornBy(sword), Is.EqualTo("toma"));

            // 同じ装備、枠に合わない装備、持っていない装備では何も変わらない。
            Assert.That(
                (await source.EquipAsync("toma", EquipmentSlot.Weapon, sword, default)).Change,
                Is.EqualTo("none")
            );
            Assert.That(
                (await source.EquipAsync("toma", EquipmentSlot.Weapon, robe, default)).Change,
                Is.EqualTo("none")
            );
            Assert.That(
                (await source.EquipAsync("toma", EquipmentSlot.Weapon, "nothing", default)).Change,
                Is.EqualTo("none")
            );

            var equipped = await source.EquipAsync(
                "aria",
                EquipmentSlot.Weapon,
                Item(state, "wooden-sword"),
                default
            );
            Assert.That(equipped.Change, Is.EqualTo("equip"));
            var replaced = await source.EquipAsync(
                "aria",
                EquipmentSlot.Weapon,
                Item(state, "flame-dagger"),
                default
            );
            Assert.That(replaced.Change, Is.EqualTo("replace"));

            var removed = await source.UnequipAsync("aria", EquipmentSlot.Weapon, default);
            Assert.That(removed.Change, Is.EqualTo("remove"));
            Assert.That(removed.ItemIn("aria", EquipmentSlot.Weapon), Is.Null);
            Assert.That(removed.Items.Count, Is.EqualTo(EquipmentCatalog.All.Count));
            Assert.That(
                (await source.UnequipAsync("aria", EquipmentSlot.Weapon, default)).Change,
                Is.EqualTo("none")
            );
        }

        private static string Item(EquipmentState state, string equipmentId) =>
            state.Items.First(item => item.EquipmentId == equipmentId).Id;

        private static string Kind(EquipmentState state, string itemId) =>
            state.Find(itemId)?.EquipmentId;
    }
}
