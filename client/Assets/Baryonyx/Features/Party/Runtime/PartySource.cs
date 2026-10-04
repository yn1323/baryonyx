using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;

namespace Baryonyx.Party
{
    /// <summary>One owned character as the server keeps them: the level and the card skills.</summary>
    public sealed class PartyCharacterState
    {
        public PartyCharacterState(string id, int level, IReadOnlyList<string> cards)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Level = Math.Max(1, level);
            Cards = cards ?? Array.Empty<string>();
        }

        public string Id { get; }
        public int Level { get; }

        // 枠の順のスキルのID。空いている枠はnull。
        public IReadOnlyList<string> Cards { get; }
    }

    /// <summary>A player's characters, party slots and runes as the server keeps them.</summary>
    public sealed class PartyState
    {
        private readonly Dictionary<string, PartyCharacterState> characters = new();

        public PartyState(
            IReadOnlyList<PartyCharacterState> owned,
            IReadOnlyList<string> slots,
            long runes,
            int maxLevel,
            int costPerLevel
        )
        {
            Owned = owned ?? Array.Empty<PartyCharacterState>();
            foreach (var character in Owned)
                characters.TryAdd(character.Id, character);
            Slots = slots ?? Array.Empty<string>();
            Runes = Math.Max(0L, runes);
            MaxLevel = maxLevel;
            CostPerLevel = costPerLevel;
        }

        // 持っているキャラ（持っている順）。
        public IReadOnlyList<PartyCharacterState> Owned { get; }

        // 枠ごとのキャラのID。空いている枠はnull。
        public IReadOnlyList<string> Slots { get; }
        public long Runes { get; }

        // レベルの上限と、Lv n から n+1 へ上げるのに要るルーンの n あたり。サーバーが正本で、0なら知らない。
        public int MaxLevel { get; }
        public int CostPerLevel { get; }

        public PartyCharacterState Find(string id) =>
            id != null && characters.TryGetValue(id, out var character) ? character : null;
    }

    /// <summary>Where the tavern's formation, card skills and training read and save the party.</summary>
    public interface IPartySource
    {
        Task<PartyState> LoadAsync(CancellationToken token);

        // 枠にキャラを入れる。nullなら枠のキャラを外す。
        Task<PartyState> SetSlotAsync(int slot, string characterId, CancellationToken token);

        Task<PartyState> SetCardAsync(
            string characterId,
            int slot,
            string skillId,
            CancellationToken token
        );

        // ルーンを使ってレベルを上げる。費用はサーバーが計算する。
        Task<PartyState> LevelUpAsync(
            string characterId,
            int fromLevel,
            int toLevel,
            CancellationToken token
        );
    }

    /// <summary>
    /// Reads and saves the party on the game server with the shared session, so each player has
    /// their own characters, levels, card skills and formation.
    /// </summary>
    public sealed class PartyServerSource : IPartySource
    {
        private readonly IAccountSessionRunner sessions;
        private readonly PartyApiClient api;

        public PartyServerSource(IAccountSessionRunner sessions, PartyApiClient api)
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public Task<PartyState> LoadAsync(CancellationToken token) =>
            Run(session => api.ReadAsync(session, token), token);

        public Task<PartyState> SetSlotAsync(
            int slot,
            string characterId,
            CancellationToken token
        ) =>
            Run(
                session =>
                    string.IsNullOrEmpty(characterId)
                        ? api.ClearSlotAsync(session, slot, token)
                        : api.SetSlotAsync(session, slot, characterId, token),
                token
            );

        public Task<PartyState> SetCardAsync(
            string characterId,
            int slot,
            string skillId,
            CancellationToken token
        ) => Run(session => api.SetCardAsync(session, characterId, slot, skillId, token), token);

        public Task<PartyState> LevelUpAsync(
            string characterId,
            int fromLevel,
            int toLevel,
            CancellationToken token
        )
        {
            // セッションを取り直して送り直すときも同じIDを使い、サーバーが同じ要求だと見分けられるようにする。
            string requestId = Guid.NewGuid().ToString("D");
            return Run(
                session =>
                    api.LevelUpAsync(session, characterId, requestId, fromLevel, toLevel, token),
                token
            );
        }

        private async Task<PartyState> Run(
            Func<AccountSession, Task<PartyApiClient.State>> operation,
            CancellationToken token
        ) => ToState(await sessions.WithSessionAsync(operation, token));

        // IDのないキャラは外し、枠とカードは番号の位置へ並べる。空文字はnull（空き）として扱う。
        public static PartyState ToState(PartyApiClient.State state)
        {
            var owned = (state?.characters ?? Array.Empty<PartyApiClient.Character>())
                .Where(character => character != null && !string.IsNullOrEmpty(character.id))
                .Select(character => new PartyCharacterState(
                    character.id,
                    character.level,
                    (character.cards ?? Array.Empty<string>())
                        .Select(card => string.IsNullOrEmpty(card) ? null : card)
                        .ToArray()
                ))
                .ToArray();
            var slots = new string[PartyFormation.Size];
            foreach (var slot in state?.slots ?? Array.Empty<PartyApiClient.Slot>())
                if (slot != null && slot.slot >= 0 && slot.slot < slots.Length)
                    slots[slot.slot] = string.IsNullOrEmpty(slot.characterId)
                        ? null
                        : slot.characterId;
            return new PartyState(
                owned,
                slots,
                state?.runes ?? 0,
                state?.rules?.maxLevel ?? 0,
                state?.rules?.costPerLevel ?? 0
            );
        }
    }
}
