using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Equipment
{
    /// <summary>
    /// The equipment while the app runs, for the showcase or without a server URL: the mock items
    /// and what the first four wear, changed by the same rules as the server and forgotten when
    /// the app stops.
    /// </summary>
    public sealed class EquipmentLocalSource : IEquipmentSource
    {
        private EquipmentState state = Starter();

        /// <summary>
        /// The mock items (one of each, ids "local-1"…, and one more of a kind two of the first
        /// four wear) and what the first four wear.
        /// </summary>
        public static EquipmentState Starter()
        {
            var owned = EquipmentCatalog
                .All.Select((entry, i) => new EquipmentItem($"local-{i + 1}", entry.Id))
                .ToList();
            var worn = new HashSet<string>();
            string ItemOf(string equipmentId)
            {
                var item = owned.FirstOrDefault(item =>
                    item.EquipmentId == equipmentId && !worn.Contains(item.Id)
                );
                if (item == null)
                {
                    item = new EquipmentItem($"local-{owned.Count + 1}", equipmentId);
                    owned.Add(item);
                }
                worn.Add(item.Id);
                return item.Id;
            }
            var equipped = EquipmentCatalog
                .StarterEquipped.SelectMany(entry =>
                    new[]
                    {
                        (
                            entry.Character,
                            (EquipmentPlace)EquipmentSlot.Weapon,
                            ItemOf(entry.Weapon)
                        ),
                        (entry.Character, EquipmentSlot.Armor, ItemOf(entry.Armor)),
                    }
                )
                .ToArray();
            return new EquipmentState(owned.ToArray(), equipped);
        }

        public Task<EquipmentState> LoadAsync(CancellationToken token) =>
            Task.FromResult(new EquipmentState(state.Items, state.Worn));

        public Task<EquipmentState> EquipAsync(
            string characterId,
            EquipmentPlace place,
            string itemId,
            CancellationToken token
        ) => Task.FromResult(state = state.WithEquipped(characterId, place, itemId));

        public Task<EquipmentState> UnequipAsync(
            string characterId,
            EquipmentPlace place,
            CancellationToken token
        ) => Task.FromResult(state = state.WithUnequipped(characterId, place));
    }
}
