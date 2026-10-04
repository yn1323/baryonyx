using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;
using Baryonyx.Party;

namespace Baryonyx.Tests.PlayMode
{
    // 酒場のシーンテストで、ゲームサーバーのパーティAPIの代わりをする。
    // サーバーと同じ決まりで編成・カード・レベルを変え、答えるのを1フレーム遅らせる。
    public sealed class FakePartySource : IPartySource
    {
        private readonly List<(string Id, int Level, string[] Cards)> characters = new();
        private readonly string[] slots = new string[PartyFormation.Size];

        public FakePartySource(long runes, params string[] party)
        {
            Runes = runes;
            for (int i = 0; i < party.Length && i < slots.Length; i++)
                slots[i] = party[i];
        }

        public long Runes { get; set; }
        public bool Fail { get; set; }
        public int Loads { get; private set; }
        public List<string> Calls { get; } = new();

        public FakePartySource With(string id, int level, params string[] cards)
        {
            characters.Add((id, level, cards));
            return this;
        }

        public int LevelOf(string id) => characters.Single(entry => entry.Id == id).Level;

        public string[] CardsOf(string id) => characters.Single(entry => entry.Id == id).Cards;

        // 別の端末で変えたときのように、手元に知らせずにレベルを変える。
        public void SetLevel(string id, int level)
        {
            int index = characters.FindIndex(entry => entry.Id == id);
            characters[index] = (id, level, characters[index].Cards);
        }

        public async Task<PartyState> LoadAsync(CancellationToken token)
        {
            await Answer(token);
            Loads++;
            return State();
        }

        public async Task<PartyState> SetSlotAsync(
            int slot,
            string characterId,
            CancellationToken token
        )
        {
            await Answer(token);
            Calls.Add($"slot {slot} {characterId ?? "-"}");
            slots[slot] = characterId;
            return State();
        }

        public async Task<PartyState> SetCardAsync(
            string characterId,
            int slot,
            string skillId,
            CancellationToken token
        )
        {
            await Answer(token);
            Calls.Add($"card {characterId} {slot} {skillId}");
            var cards = CardsOf(characterId);
            int from = Array.IndexOf(cards, skillId);
            if (from >= 0)
                cards[from] = cards[slot];
            cards[slot] = skillId;
            return State();
        }

        public async Task<PartyState> LevelUpAsync(
            string characterId,
            int fromLevel,
            int toLevel,
            CancellationToken token
        )
        {
            await Answer(token);
            Calls.Add($"level {characterId} {fromLevel} {toLevel}");
            long cost = 0;
            for (int level = fromLevel; level < toLevel; level++)
                cost += level * 100L;
            if (LevelOf(characterId) != fromLevel || Runes < cost)
                throw new ServerApiException(409);
            Runes -= cost;
            SetLevel(characterId, toLevel);
            return State();
        }

        private async Task Answer(CancellationToken token)
        {
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            if (Fail)
                throw new InvalidOperationException("Test server is offline.");
        }

        private PartyState State() =>
            new(
                characters
                    .Select(entry => new PartyCharacterState(
                        entry.Id,
                        entry.Level,
                        entry.Cards.ToArray()
                    ))
                    .ToArray(),
                slots.ToArray(),
                Runes,
                30,
                100
            );
    }
}
