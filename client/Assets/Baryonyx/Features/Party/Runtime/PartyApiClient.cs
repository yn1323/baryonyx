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
            string card,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                $"/v1/party/characters/{Uri.EscapeDataString(characterId)}/cards/{slot}",
                "PUT",
                new SetCardRequest { card = card },
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
            // 枠に入れる物（PartyCardKey の形：card:<ID> か item:<アイテムのID>）。
            public string card;
        }

        /// <summary>Sets the stage the formation uses of a character's unique skill (doc/features/skill-evolution.md).</summary>
        public Task<State> SetUniqueAsync(
            AccountSession session,
            string characterId,
            int index,
            int stage,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                $"/v1/party/characters/{characterId}/uniques/{index}",
                "PUT",
                new StageRequest { stage = stage },
                session.Token,
                token
            );

        /// <summary>
        /// Saves one evolution of a character's unique skill in an adventure's battle: the stage
        /// is acquired and set. Sent again, it changes nothing.
        /// </summary>
        public Task<State> EvolveUniqueAsync(
            AccountSession session,
            string characterId,
            int index,
            int stage,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                $"/v1/party/characters/{characterId}/uniques/{index}/evolve",
                "POST",
                new StageRequest { stage = stage },
                session.Token,
                token
            );

        [Serializable]
        private sealed class StageRequest
        {
            public int stage;
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
            public SkillCard[] skillCards;
            public Item[] items;
            public long runes;
            public Rules rules;
        }

        // 持っているスキルの1枚。倍率（×1.37なら1.37）と品質（S〜C）を持つ。
        [Serializable]
        public sealed class SkillCard
        {
            public string id;
            public string skillId;
            public float multiplier;
            public string quality;
        }

        [Serializable]
        public sealed class Item
        {
            public string itemId;
            public int count;
        }

        [Serializable]
        public sealed class Character
        {
            public string id;
            public int level;

            // 枠の順の中身（card:<持っているスキルのID> か item:<アイテムのID>）。空いている枠はnullまたは空文字。
            public string[] cards;

            // 2つの固有スキルの進み具合（セットしている段階と、取得した段階）。
            public Unique[] uniques;
        }

        [Serializable]
        public sealed class Unique
        {
            public int index;
            public int set;
            public int[] acquired;
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
