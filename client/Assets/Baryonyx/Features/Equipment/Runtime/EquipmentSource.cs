using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Inventory;

namespace Baryonyx.Equipment
{
    /// <summary>One weapon or piece of armour a player owns. Two of the same kind are two items.</summary>
    public sealed class EquipmentItem
    {
        public EquipmentItem(
            string id,
            string equipmentId,
            float multiplier = Quality.MinMultiplier
        )
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            EquipmentId = equipmentId ?? "";
            Multiplier = Math.Max(Quality.MinMultiplier, multiplier);
        }

        public string Id { get; }

        // 装備の種類（EquipmentCatalog のID）。
        public string EquipmentId { get; }

        // この1つの倍率（×1.37なら1.37）。品質は種類の上限から求める（doc/features/inventory.md の品質）。
        public float Multiplier { get; }
    }

    /// <summary>
    /// One place a character wears an item: the weapon, the armour, or one of the three accessory
    /// slots (<see cref="Index"/> 0 to 2). A slot converts to its place, so the weapon and the
    /// armour are still named by their slot.
    /// </summary>
    public readonly struct EquipmentPlace : IEquatable<EquipmentPlace>
    {
        public EquipmentPlace(EquipmentSlot slot, int index = 0)
        {
            Slot = slot;
            Index = slot == EquipmentSlot.Accessory ? index : 0;
        }

        public EquipmentSlot Slot { get; }

        // アクセサリーの枠の番号（0〜2）。武器と防具では0。
        public int Index { get; }

        public static EquipmentPlace Accessory(int index) => new(EquipmentSlot.Accessory, index);

        public static implicit operator EquipmentPlace(EquipmentSlot slot) => new(slot);

        // APIの枠の名前（「weapon」「armor」「accessory1」〜「accessory3」）。
        public string Key =>
            Slot == EquipmentSlot.Accessory
                ? $"accessory{Index + 1}"
                : EquipmentCatalog.KeyOf(Slot);

        public bool Equals(EquipmentPlace other) => Slot == other.Slot && Index == other.Index;

        public override bool Equals(object obj) => obj is EquipmentPlace other && Equals(other);

        public override int GetHashCode() => (int)Slot * 8 + Index;

        public override string ToString() => Key;
    }

    /// <summary>The items a player owns and what each character wears, as the server keeps them.</summary>
    public sealed class EquipmentState
    {
        private readonly Dictionary<(string Character, EquipmentPlace Place), string> worn = new();
        private readonly Dictionary<string, EquipmentItem> items = new();

        public EquipmentState(
            IReadOnlyList<EquipmentItem> owned,
            IEnumerable<(string Character, EquipmentPlace Place, string Item)> equipped,
            string change = null,
            string from = null
        )
        {
            Items = owned ?? Array.Empty<EquipmentItem>();
            foreach (var item in Items)
                items.TryAdd(item.Id, item);
            foreach (
                var (character, place, item) in equipped
                    ?? Array.Empty<(string, EquipmentPlace, string)>()
            )
                if (!string.IsNullOrEmpty(character) && !string.IsNullOrEmpty(item))
                    worn[(character, place)] = item;
            Change = change ?? "";
            From = from;
        }

        // 持っている装備（入手の古い順）。
        public IReadOnlyList<EquipmentItem> Items { get; }

        // 直前の変更で何が起きたか（「equip」「replace」「move」「remove」「none」）。読み込みでは空。
        public string Change { get; }

        // 「move」のとき、装備を外されたキャラのID。
        public string From { get; }

        // サーバーから読んだ中身を表す文字列。読み直して変わったかを比べる。サーバーから読んでいなければnull。
        public string Fingerprint { get; internal set; }

        public EquipmentItem Find(string itemId) =>
            itemId != null && items.TryGetValue(itemId, out var item) ? item : null;

        // キャラの枠に付けている装備のID。空いていればnull。
        public string ItemIn(string character, EquipmentPlace place) =>
            character != null && worn.TryGetValue((character, place), out string item)
                ? item
                : null;

        // その装備を付けているキャラのID。誰も付けていなければnull。
        public string WornBy(string itemId) =>
            itemId == null
                ? null
                : worn.FirstOrDefault(entry => entry.Value == itemId).Key.Character;

        public IEnumerable<(string Character, EquipmentPlace Place, string Item)> Worn =>
            worn.Select(entry => (entry.Key.Character, entry.Key.Place, entry.Value));

        /// <summary>
        /// The items worn after the character puts the item on in the slot, by the server's rules
        /// (doc/features/equipment.md): an item someone else wears moves to the character. An
        /// item not owned or for another slot changes nothing ("none"). An accessory worn in
        /// another of the character's accessory slots moves too.
        /// </summary>
        public EquipmentState WithEquipped(string characterId, EquipmentPlace place, string itemId)
        {
            var item = Find(itemId);
            if (
                string.IsNullOrEmpty(characterId)
                || item == null
                || EquipmentCatalog.Find(item.EquipmentId)?.Slot != place.Slot
            )
                return With(worn, "none");
            string current = ItemIn(characterId, place);
            if (current == itemId)
                return With(worn, "none");
            var next = new Dictionary<(string Character, EquipmentPlace Place), string>(worn);
            var holder = next.FirstOrDefault(entry => entry.Value == itemId).Key;
            if (holder.Character != null)
                next.Remove(holder);
            next[(characterId, place)] = itemId;
            if (holder.Character != null)
                return With(next, "move", holder.Character);
            return With(next, current == null ? "equip" : "replace");
        }

        /// <summary>The items worn after the character takes the slot's item off.</summary>
        public EquipmentState WithUnequipped(string characterId, EquipmentPlace place)
        {
            if (ItemIn(characterId, place) == null)
                return With(worn, "none");
            var next = new Dictionary<(string Character, EquipmentPlace Place), string>(worn);
            next.Remove((characterId, place));
            return With(next, "remove");
        }

        private EquipmentState With(
            Dictionary<(string Character, EquipmentPlace Place), string> next,
            string change,
            string from = null
        ) =>
            new(
                Items,
                next.Select(entry => (entry.Key.Character, entry.Key.Place, entry.Value)).ToArray(),
                change,
                from
            );
    }

    /// <summary>Where the formation's equipment reads and saves what the characters wear.</summary>
    public interface IEquipmentSource
    {
        Task<EquipmentState> LoadAsync(CancellationToken token);

        // キャラの枠に装備を付ける。ほかのキャラが付けていれば、そのキャラから外して付け替える。
        Task<EquipmentState> EquipAsync(
            string characterId,
            EquipmentPlace place,
            string itemId,
            CancellationToken token
        );

        Task<EquipmentState> UnequipAsync(
            string characterId,
            EquipmentPlace place,
            CancellationToken token
        );
    }

    /// <summary>
    /// Reads and saves the equipment on the game server with the shared session, so each player
    /// has their own items and what each character wears.
    /// </summary>
    public sealed class EquipmentServerSource : IEquipmentSource
    {
        private readonly IAccountSessionRunner sessions;
        private readonly EquipmentApiClient api;

        public EquipmentServerSource(IAccountSessionRunner sessions, EquipmentApiClient api)
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public Task<EquipmentState> LoadAsync(CancellationToken token) =>
            Run(session => api.ReadAsync(session, token), token);

        public Task<EquipmentState> EquipAsync(
            string characterId,
            EquipmentPlace place,
            string itemId,
            CancellationToken token
        ) => Run(session => api.EquipAsync(session, characterId, place, itemId, token), token);

        public Task<EquipmentState> UnequipAsync(
            string characterId,
            EquipmentPlace place,
            CancellationToken token
        ) => Run(session => api.UnequipAsync(session, characterId, place, token), token);

        private async Task<EquipmentState> Run(
            Func<AccountSession, Task<EquipmentApiClient.State>> operation,
            CancellationToken token
        ) => ToState(await sessions.WithSessionAsync(operation, token));

        // IDのない装備とキャラは外す。空文字の枠はnull（空き）として扱う。
        public static EquipmentState ToState(EquipmentApiClient.State state)
        {
            var items = (state?.items ?? Array.Empty<EquipmentApiClient.Item>())
                .Where(item => item != null && !string.IsNullOrEmpty(item.id))
                .Select(item => new EquipmentItem(item.id, item.equipmentId, item.multiplier))
                .ToArray();
            var worn = (state?.characters ?? Array.Empty<EquipmentApiClient.Character>())
                .Where(character => character != null && !string.IsNullOrEmpty(character.id))
                .SelectMany(character =>
                    new[]
                    {
                        (character.id, (EquipmentPlace)EquipmentSlot.Weapon, character.weapon),
                        (character.id, EquipmentSlot.Armor, character.armor),
                    }.Concat(
                        (character.accessories ?? Array.Empty<string>())
                            .Take(AccessorySlots.Count)
                            .Select(
                                (item, index) =>
                                    (character.id, EquipmentPlace.Accessory(index), item)
                            )
                    )
                );
            return new EquipmentState(
                items,
                worn,
                state?.change,
                string.IsNullOrEmpty(state?.from) ? null : state.from
            )
            {
                Fingerprint = FingerprintOf(state),
            };
        }

        // 変更の結果（change・from）は中身ではないので、指紋に含めない。
        private static string FingerprintOf(EquipmentApiClient.State state)
        {
            if (state == null)
                return null;
            var (change, from) = (state.change, state.from);
            (state.change, state.from) = (null, null);
            string json = UnityEngine.JsonUtility.ToJson(state);
            (state.change, state.from) = (change, from);
            return json;
        }
    }
}
