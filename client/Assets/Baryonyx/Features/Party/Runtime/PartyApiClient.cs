using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Networking;

namespace Baryonyx.Party
{
    /// <summary>
    /// The server's party API: the characters a player owns with their levels and card skills,
    /// the four party slots and the runes a level-up spends (server/src/features/party/routes.ts).
    /// </summary>
    public sealed class PartyApiClient
    {
        private readonly ServerApi server;

        public PartyApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public Task<State> ReadAsync(AccountSession session, CancellationToken token) =>
            server.SendAsync<State>("/v1/party", "GET", null, session.Token, token);

        public Task<State> SetSlotAsync(
            AccountSession session,
            int slot,
            string characterId,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                "/v1/party/slots/" + slot,
                "PUT",
                new SetSlotRequest { characterId = characterId },
                session.Token,
                token
            );

        // 本文の null は JsonUtility が空文字にして送るため、外す操作は DELETE に分けている。
        public Task<State> ClearSlotAsync(
            AccountSession session,
            int slot,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                "/v1/party/slots/" + slot,
                "DELETE",
                null,
                session.Token,
                token
            );

        public Task<State> SetCardAsync(
            AccountSession session,
            string characterId,
            int slot,
            string skillId,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                $"/v1/party/characters/{Uri.EscapeDataString(characterId)}/cards/{slot}",
                "PUT",
                new SetCardRequest { skillId = skillId },
                session.Token,
                token
            );

        public Task<State> LevelUpAsync(
            AccountSession session,
            string characterId,
            string requestId,
            int fromLevel,
            int toLevel,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                $"/v1/party/characters/{Uri.EscapeDataString(characterId)}/level-up",
                "POST",
                new LevelUpRequest
                {
                    requestId = requestId,
                    fromLevel = fromLevel,
                    toLevel = toLevel,
                },
                session.Token,
                token
            );

        [Serializable]
        private sealed class SetSlotRequest
        {
            public string characterId;
        }

        [Serializable]
        private sealed class SetCardRequest
        {
            public string skillId;
        }

        [Serializable]
        private sealed class LevelUpRequest
        {
            public string requestId;
            public int fromLevel;
            public int toLevel;
        }

        [Serializable]
        public sealed class State
        {
            // 変更したときだけ、何が起きたか（編成は「join」など、カードは「replace」など）が入る。
            public string change;
            public Character[] characters;
            public Slot[] slots;
            public long runes;
            public Rules rules;
        }

        [Serializable]
        public sealed class Character
        {
            public string id;
            public int level;

            // 枠の順のスキルのID。空いている枠はnullまたは空文字。
            public string[] cards;
        }

        [Serializable]
        public sealed class Slot
        {
            public int slot;
            public string characterId;
        }

        [Serializable]
        public sealed class Rules
        {
            public int maxLevel;
            public int costPerLevel;
        }
    }
}
