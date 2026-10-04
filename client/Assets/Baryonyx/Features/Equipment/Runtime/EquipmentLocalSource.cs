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
        private readonly List<EquipmentItem> items;
        private readonly Dictionary<(string Character, EquipmentSlot Slot), string> worn;

        public EquipmentLocalSource()
        {
            var starter = Starter();
            items = starter.Items.ToList();
            worn = starter.Worn.ToDictionary(
                entry => (entry.Character, entry.Slot),
                entry => entry.Item
            );
        }

        /// <summary>The mock items (one of each, ids "local-1"…) and what the first four wear.</summary>
        public static EquipmentState Starter()
        {
            var owned = EquipmentCatalog
                .All.Select((entry, i) => new EquipmentItem($"local-{i + 1}", entry.Id))
                .ToArray();
            string ItemOf(string equipmentId) =>
                owned.First(item => item.EquipmentId == equipmentId).Id;
            var equipped = EquipmentCatalog.StarterEquipped.SelectMany(entry =>
                new[]
                {
                    (entry.Character, EquipmentSlot.Weapon, ItemOf(entry.Weapon)),
                    (entry.Character, EquipmentSlot.Armor, ItemOf(entry.Armor)),
                }
            );
            return new EquipmentState(owned, equipped);
        }

        public Task<EquipmentState> LoadAsync(CancellationToken token) =>
            Task.FromResult(State(null));

        public Task<EquipmentState> EquipAsync(
            string characterId,
            EquipmentSlot slot,
            string itemId,
            CancellationToken token
        )
        {
            var item = items.Find(entry => entry.Id == itemId);
            if (
                string.IsNullOrEmpty(characterId)
                || item == null
                || EquipmentCatalog.Find(item.EquipmentId)?.Slot != slot
            )
                return Task.FromResult(State("none"));
            worn.TryGetValue((characterId, slot), out string current);
            if (current == itemId)
                return Task.FromResult(State("none"));
            var holder = worn.FirstOrDefault(entry => entry.Value == itemId).Key;
            if (holder.Character != null)
                worn.Remove(holder);
            worn[(characterId, slot)] = itemId;
            if (holder.Character != null)
                return Task.FromResult(State("move", holder.Character));
            return Task.FromResult(State(current == null ? "equip" : "replace"));
        }

        public Task<EquipmentState> UnequipAsync(
            string characterId,
            EquipmentSlot slot,
            CancellationToken token
        ) =>
            Task.FromResult(
                characterId != null && worn.Remove((characterId, slot))
                    ? State("remove")
                    : State("none")
            );

        private EquipmentState State(string change, string from = null) =>
            new(
                items.ToArray(),
                worn.Select(entry => (entry.Key.Character, entry.Key.Slot, entry.Value)).ToArray(),
                change,
                from
            );
    }
}
