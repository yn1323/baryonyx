using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Combat;
using Baryonyx.SkillEvolution;

namespace Baryonyx.Party
{
    /// <summary>
    /// One owned character as the server keeps them: the level, the card skills and the progress
    /// of the unique skills.
    /// </summary>
    public sealed class PartyCharacterState
    {
        public PartyCharacterState(
            string id,
            int level,
            IReadOnlyList<string> cards,
            IReadOnlyList<UniqueSkillProgress> uniques = null,
            int bond = 0,
            int tokens = 0
        )
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Level = Math.Max(1, level);
            Cards = cards ?? Array.Empty<string>();
            Uniques = uniques;
            Bond = Math.Max(0, bond);
            Tokens = Math.Max(0, tokens);
        }

        public string Id { get; }
        public int Level { get; }

        // 枠の順の中身（PartyCardKey の形）。空いている枠はnull。
        public IReadOnlyList<string> Cards { get; }

        // 固有スキルの番号の順の進み具合（doc/features/skill-evolution.md）。サーバーが返さなければnull。
        public IReadOnlyList<UniqueSkillProgress> Uniques { get; }

        // 絆の段と、持っている絆のしるし（doc/features/monetization.md のキャラが被ったとき）。
        public int Bond { get; }
        public int Tokens { get; }
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
            int costPerLevel,
            IReadOnlyList<PartySkillCard> skillCards = null,
            IReadOnlyList<PartyItem> items = null
        )
        {
            Owned = owned ?? Array.Empty<PartyCharacterState>();
            SkillCards = skillCards;
            Items = items;
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

        // 持っているスキルの1枚ずつと、持っているアイテム。サーバーから読んでいなければnull（仮データの
        // カードはスキルIDのまま、どのスキルも×1.00で使える）。
        public IReadOnlyList<PartySkillCard> SkillCards { get; }
        public IReadOnlyList<PartyItem> Items { get; }

        // レベルの上限と、Lv n から n+1 へ上げるのに要るルーンの n あたり。サーバーが正本で、0なら知らない。
        public int MaxLevel { get; }
        public int CostPerLevel { get; }

        // サーバーから読んだ中身を表す文字列。読み直して変わったかを比べる。サーバーから読んでいなければnull。
        public string Fingerprint { get; internal set; }

        public PartyCharacterState Find(string id) =>
            id != null && characters.TryGetValue(id, out var character) ? character : null;

        public PartySkillCard SkillCardOf(string key)
        {
            if (SkillCards == null || !PartyCardKey.IsCard(key))
                return null;
            string id = PartyCardKey.IdOf(key);
            foreach (var card in SkillCards)
                if (card.Id == id)
                    return card;
            return null;
        }

        /// <summary>
        /// The skill a slot's key stands for, with the owned card's multiplier; a mock skill id is
        /// the definition itself. Null for an item, an empty slot or an unknown key.
        /// </summary>
        public CardSkill SkillOf(string key) =>
            string.IsNullOrEmpty(key) || PartyCardKey.IsItem(key) ? null
            : PartyCardKey.IsCard(key) ? SkillCardOf(key)?.Skill
            : CardSkills.Find(key);

        /// <summary>The skill id a slot's key stands for (for the element icons and art), or null.</summary>
        public string SkillIdOf(string key) =>
            string.IsNullOrEmpty(key) || PartyCardKey.IsItem(key) ? null
            : PartyCardKey.IsCard(key) ? SkillCardOf(key)?.SkillId
            : key;

        // 以下は、サーバーへ保存する前に画面へ出すための、サーバーと同じ決まりの変更（doc/features/party.md）。

        /// <summary>The party with the slot set to the character (null empties it).</summary>
        public PartyState WithSlot(int slot, string characterId)
        {
            if (slot < 0 || slot >= Slots.Count)
                return this;
            var slots = Slots.ToArray();
            slots[slot] = string.IsNullOrEmpty(characterId) ? null : characterId;
            return Copy(Owned, slots, Runes);
        }

        /// <summary>
        /// The party with the character's card slot set to the key. A key in the character's
        /// other slot swaps with the slot's card; an owned skill card another character holds
        /// leaves that character (an item stays, since it is counted, not moved). A null key
        /// empties the slot.
        /// </summary>
        public PartyState WithCard(string characterId, int slot, string key)
        {
            var target = Find(characterId);
            if (target == null || slot < 0 || slot >= target.Cards.Count)
                return this;
            bool moves = PartyCardKey.IsCard(key) && !target.Cards.Contains(key);
            var owned = Owned
                .Select(character =>
                {
                    var cards = character.Cards.ToArray();
                    if (character.Id == characterId)
                    {
                        // 外すときは、ほかの空きの枠と入れ替えない。
                        int other = key != null ? Array.IndexOf(cards, key) : -1;
                        if (other >= 0)
                            cards[other] = cards[slot];
                        cards[slot] = key;
                    }
                    else if (moves)
                    {
                        for (int i = 0; i < cards.Length; i++)
                            if (cards[i] == key)
                                cards[i] = null;
                    }
                    else
                        return character;
                    return new PartyCharacterState(
                        character.Id,
                        character.Level,
                        cards,
                        character.Uniques,
                        character.Bond,
                        character.Tokens
                    );
                })
                .ToArray();
            return Copy(owned, Slots, Runes);
        }

        /// <summary>The party with the character raised to the level and the runes spent.</summary>
        public PartyState WithLevel(string characterId, int level, long runes) =>
            With(
                characterId,
                character => new PartyCharacterState(
                    character.Id,
                    level,
                    character.Cards,
                    character.Uniques,
                    character.Bond,
                    character.Tokens
                ),
                runes
            );

        /// <summary>The party with the character's bond one stage deeper and one token spent.</summary>
        public PartyState WithBond(string characterId) =>
            With(
                characterId,
                character =>
                    character.Tokens < 1
                        ? character
                        : new PartyCharacterState(
                            character.Id,
                            character.Level,
                            character.Cards,
                            character.Uniques,
                            character.Bond + 1,
                            character.Tokens - 1
                        ),
                0
            );

        private PartyState With(
            string characterId,
            Func<PartyCharacterState, PartyCharacterState> change,
            long spent
        )
        {
            if (Find(characterId) == null)
                return this;
            var owned = Owned
                .Select(character => character.Id == characterId ? change(character) : character)
                .ToArray();
            return Copy(owned, Slots, Runes - spent);
        }

        private PartyState Copy(
            IReadOnlyList<PartyCharacterState> owned,
            IReadOnlyList<string> slots,
            long runes
        ) => new(owned, slots, runes, MaxLevel, CostPerLevel, SkillCards, Items);
    }

    /// <summary>Where the tavern's formation, card skills and training read and save the party.</summary>
    public interface IPartySource
    {
        Task<PartyState> LoadAsync(CancellationToken token);

        // 枠にキャラを入れる。nullなら枠のキャラを外す。
        Task<PartyState> SetSlotAsync(int slot, string characterId, CancellationToken token);

        // 枠に入れる物は PartyCardKey の形で渡す。null なら枠を空きにする。
        Task<PartyState> SetCardAsync(
            string characterId,
            int slot,
            string key,
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
    /// Saves the progress of the characters' unique skills (doc/features/skill-evolution.md#覚醒の保存):
    /// the stage the formation sets, and each evolution in an adventure's battle.
    /// </summary>
    public interface IPartyUniqueSkillSource
    {
        Task<PartyState> SetUniqueAsync(
            string characterId,
            int index,
            int stage,
            CancellationToken token
        );

        Task<PartyState> EvolveUniqueAsync(
            string characterId,
            int index,
            int stage,
            CancellationToken token
        );
    }

    /// <summary>Deepens a character's bond with one of its tokens (doc/features/monetization.md).</summary>
    public interface IPartyBondSource
    {
        // bond は深めたあとの段。
        Task<PartyState> DeepenBondAsync(string characterId, int bond, CancellationToken token);
    }

    /// <summary>
    /// Reads and saves the party on the game server with the shared session, so each player has
    /// their own characters, levels, card skills and formation.
    /// </summary>
    public sealed class PartyServerSource : IPartySource, IPartyUniqueSkillSource, IPartyBondSource
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

        // 空のキーは枠を空きにする（DELETE で送る）。
        public Task<PartyState> SetCardAsync(
            string characterId,
            int slot,
            string key,
            CancellationToken token
        ) =>
            Run(
                session =>
                    string.IsNullOrEmpty(key)
                        ? api.ClearCardAsync(session, characterId, slot, token)
                        : api.SetCardAsync(session, characterId, slot, key, token),
                token
            );

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

        public Task<PartyState> SetUniqueAsync(
            string characterId,
            int index,
            int stage,
            CancellationToken token
        ) => Run(session => api.SetUniqueAsync(session, characterId, index, stage, token), token);

        public Task<PartyState> EvolveUniqueAsync(
            string characterId,
            int index,
            int stage,
            CancellationToken token
        ) =>
            Run(session => api.EvolveUniqueAsync(session, characterId, index, stage, token), token);

        public Task<PartyState> DeepenBondAsync(
            string characterId,
            int bond,
            CancellationToken token
        ) => Run(session => api.DeepenBondAsync(session, characterId, bond, token), token);

        private async Task<PartyState> Run(
            Func<AccountSession, Task<PartyApiClient.State>> operation,
            CancellationToken token
        ) => ToState(await sessions.WithSessionAsync(operation, token));

        // 固有スキルの進み具合を番号の位置へ並べる。サーバーが返さなければnull。最初の段階は取得済みに書かない。
        private static UniqueSkillProgress[] UniquesOf(PartyApiClient.Unique[] uniques)
        {
            if (uniques == null)
                return null;
            int count = 0;
            foreach (var unique in uniques)
                if (unique != null && unique.index >= 0)
                    count = Math.Max(count, unique.index + 1);
            var progress = new UniqueSkillProgress[count];
            for (int i = 0; i < count; i++)
                progress[i] = new UniqueSkillProgress();
            foreach (var unique in uniques)
                if (unique != null && unique.index >= 0)
                    progress[unique.index] = new UniqueSkillProgress
                    {
                        Set = unique.set,
                        Acquired = (unique.acquired ?? Array.Empty<int>())
                            .Where(stage => stage > 0)
                            .ToArray(),
                        // 倍率を返さない古いサーバーでは×1.00。
                        Multiplier = Math.Max(Inventory.Quality.MinMultiplier, unique.multiplier),
                    };
            return progress;
        }

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
                        .ToArray(),
                    UniquesOf(character.uniques),
                    character.bond,
                    character.tokens
                ))
                .ToArray();
            var slots = new string[PartyFormation.Size];
            foreach (var slot in state?.slots ?? Array.Empty<PartyApiClient.Slot>())
                if (slot != null && slot.slot >= 0 && slot.slot < slots.Length)
                    slots[slot.slot] = string.IsNullOrEmpty(slot.characterId)
                        ? null
                        : slot.characterId;
            var skillCards = (state?.skillCards ?? Array.Empty<PartyApiClient.SkillCard>())
                .Where(card => card != null && !string.IsNullOrEmpty(card.id))
                .Select(card => new PartySkillCard(card.id, card.skillId, card.multiplier))
                .ToArray();
            var items = (state?.items ?? Array.Empty<PartyApiClient.Item>())
                .Where(item => item != null && !string.IsNullOrEmpty(item.itemId) && item.count > 0)
                .Select(item => new PartyItem(item.itemId, item.count))
                .ToArray();
            return new PartyState(
                owned,
                slots,
                state?.runes ?? 0,
                state?.rules?.maxLevel ?? 0,
                state?.rules?.costPerLevel ?? 0,
                skillCards,
                items
            )
            {
                Fingerprint = FingerprintOf(state),
            };
        }

        // 変更の結果（change）は中身ではないので、指紋に含めない。
        private static string FingerprintOf(PartyApiClient.State state)
        {
            if (state == null)
                return null;
            string change = state.change;
            state.change = null;
            string json = UnityEngine.JsonUtility.ToJson(state);
            state.change = change;
            return json;
        }
    }
}
