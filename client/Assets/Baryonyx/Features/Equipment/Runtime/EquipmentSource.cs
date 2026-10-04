using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;

namespace Baryonyx.Equipment
{
    /// <summary>One weapon or piece of armour a player owns. Two of the same kind are two items.</summary>
    public sealed class EquipmentItem
    {
        public EquipmentItem(string id, string equipmentId)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            EquipmentId = equipmentId ?? "";
        }

        public string Id { get; }

        // 装備の種類（EquipmentCatalog のID）。
        public string EquipmentId { get; }
    }

    /// <summary>The items a player owns and what each character wears, as the server keeps them.</summary>
    public sealed class EquipmentState
    {
        private readonly Dictionary<(string Character, EquipmentSlot Slot), string> worn = new();
        private readonly Dictionary<string, EquipmentItem> items = new();

        public EquipmentState(
            IReadOnlyList<EquipmentItem> owned,
            IEnumerable<(string Character, EquipmentSlot Slot, string Item)> equipped,
            string change = null,
            string from = null
        )
        {
            Items = owned ?? Array.Empty<EquipmentItem>();
            foreach (var item in Items)
                items.TryAdd(item.Id, item);
            foreach (
                var (character, slot, item) in equipped
                    ?? Array.Empty<(string, EquipmentSlot, string)>()
            )
                if (!string.IsNullOrEmpty(character) && !string.IsNullOrEmpty(item))
                    worn[(character, slot)] = item;
            Change = change ?? "";
            From = from;
        }

        // 持っている装備（入手の古い順）。
        public IReadOnlyList<EquipmentItem> Items { get; }

        // 直前の変更で何が起きたか（「equip」「replace」「move」「remove」「none」）。読み込みでは空。
        public string Change { get; }

        // 「move」のとき、装備を外されたキャラのID。
        public string From { get; }

        public EquipmentItem Find(string itemId) =>
            itemId != null && items.TryGetValue(itemId, out var item) ? item : null;

        // キャラの枠に付けている装備のID。空いていればnull。
        public string ItemIn(string character, EquipmentSlot slot) =>
            character != null && worn.TryGetValue((character, slot), out string item) ? item : null;

        // その装備を付けているキャラのID。誰も付けていなければnull。
        public string WornBy(string itemId) =>
            itemId == null
                ? null
                : worn.FirstOrDefault(entry => entry.Value == itemId).Key.Character;

        public IEnumerable<(string Character, EquipmentSlot Slot, string Item)> Worn =>
            worn.Select(entry => (entry.Key.Character, entry.Key.Slot, entry.Value));
    }

    /// <summary>Where the formation's equipment reads and saves what the characters wear.</summary>
    public interface IEquipmentSource
    {
        Task<EquipmentState> LoadAsync(CancellationToken token);

        // キャラの枠に装備を付ける。ほかのキャラが付けていれば、そのキャラから外して付け替える。
        Task<EquipmentState> EquipAsync(
            string characterId,
            EquipmentSlot slot,
            string itemId,
            CancellationToken token
        );

        Task<EquipmentState> UnequipAsync(
            string characterId,
            EquipmentSlot slot,
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
            EquipmentSlot slot,
            string itemId,
            CancellationToken token
        ) => Run(session => api.EquipAsync(session, characterId, slot, itemId, token), token);

        public Task<EquipmentState> UnequipAsync(
            string characterId,
            EquipmentSlot slot,
            CancellationToken token
        ) => Run(session => api.UnequipAsync(session, characterId, slot, token), token);

        private async Task<EquipmentState> Run(
            Func<AccountSession, Task<EquipmentApiClient.State>> operation,
            CancellationToken token
        ) => ToState(await sessions.WithSessionAsync(operation, token));

        // IDのない装備とキャラは外す。空文字の枠はnull（空き）として扱う。
        public static EquipmentState ToState(EquipmentApiClient.State state)
        {
            var items = (state?.items ?? Array.Empty<EquipmentApiClient.Item>())
                .Where(item => item != null && !string.IsNullOrEmpty(item.id))
                .Select(item => new EquipmentItem(item.id, item.equipmentId))
                .ToArray();
            var worn = (state?.characters ?? Array.Empty<EquipmentApiClient.Character>())
                .Where(character => character != null && !string.IsNullOrEmpty(character.id))
                .SelectMany(character =>
                    new[]
                    {
                        (character.id, EquipmentSlot.Weapon, character.weapon),
                        (character.id, EquipmentSlot.Armor, character.armor),
                    }
                );
            return new EquipmentState(
                items,
                worn,
                state?.change,
                string.IsNullOrEmpty(state?.from) ? null : state.from
            );
        }
    }
}
