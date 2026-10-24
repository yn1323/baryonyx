using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Networking;

namespace Baryonyx.Equipment
{
    /// <summary>
    /// The server's equipment API: the weapons, armour and accessories a player owns and what each
    /// character wears (server/src/features/equipment/routes.ts).
    /// </summary>
    public sealed class EquipmentApiClient
    {
        private readonly ServerApi server;

        public EquipmentApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public Task<State> ReadAsync(AccountSession session, CancellationToken token) =>
            server.SendAsync<State>("/v1/equipment", "GET", null, session.Token, token);

        public Task<State> EquipAsync(
            AccountSession session,
            string characterId,
            EquipmentPlace place,
            string itemId,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                Path(characterId, place),
                "PUT",
                new EquipRequest { itemId = itemId },
                session.Token,
                token
            );

        // 本文の null は JsonUtility が空文字にして送るため、外す操作は DELETE に分けている。
        public Task<State> UnequipAsync(
            AccountSession session,
            string characterId,
            EquipmentPlace place,
            CancellationToken token
        ) =>
            server.SendAsync<State>(Path(characterId, place), "DELETE", null, session.Token, token);

        private static string Path(string characterId, EquipmentPlace place) =>
            $"/v1/equipment/characters/{Uri.EscapeDataString(characterId)}/{place.Key}";

        [Serializable]
        private sealed class EquipRequest
        {
            public string itemId;
        }

        [Serializable]
        public sealed class State
        {
            // 変更したときだけ、何が起きたか（「equip」「replace」「move」「remove」「none」）が入る。
            public string change;

            // 「move」のとき、装備を外されたキャラのID。
            public string from;
            public Item[] items;
            public Character[] characters;
        }

        [Serializable]
        public sealed class Item
        {
            public string id;
            public string equipmentId;

            // 倍率（×1.37なら1.37）と品質（S・A・B・C）。
            public float multiplier;
            public string quality;
        }

        [Serializable]
        public sealed class Character
        {
            public string id;

            // 付けている装備のID。空いている枠はnullまたは空文字。
            public string weapon;
            public string armor;

            // アクセサリーの3つの枠。空いている枠はnullまたは空文字。
            public string[] accessories;
        }
    }
}
