using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Baryonyx.Party
{
    /// <summary>
    /// Keeps the formation and the characters' levels and card skills while the app runs. There
    /// is no server for the party yet, so they start from the mock data each time the app starts
    /// (doc/features/party.md). The mock data asset itself is never written.
    /// </summary>
    public static class PartySession
    {
        // 酒場のメニューの「編成」の項目のキー。この項目はリストの代わりに編成を開く。
        public const string GuideItemKey = "formation";

        private static PartyFormation formation;

        // 育成で上げたレベル。上げていないキャラは仮データのレベルのまま。
        private static readonly Dictionary<string, int> levels = new();

        // 酒場のカードスキルで付け替えたカード（スキルIDを枠の順に）。付け替えていないキャラは仮データのまま。
        private static readonly Dictionary<string, string[]> cards = new();

        public static PartyFormation Formation(PartyMockData data) =>
            formation ??= PartyFormation.From(data);

        /// <summary>
        /// The character's level while the app runs: the raised level, or the mock data's.
        /// Outside Play Mode (the prefab generators) it is always the mock data's level.
        /// </summary>
        public static int LevelOf(PartyMockData data, string id)
        {
            if (id != null && levels.TryGetValue(id, out int level))
                return level;
            var member = data != null ? data.Find(id) : null;
            return member != null ? member.Level : 1;
        }

        public static void SetLevel(string id, int level)
        {
            if (!string.IsNullOrEmpty(id))
                levels[id] = Mathf.Max(1, level);
        }

        /// <summary>
        /// The character's card skills (skill ids, in slot order) while the app runs: the cards
        /// set on the tavern's card skills, or the mock data's. Outside Play Mode (the prefab
        /// generators) they are always the mock data's.
        /// </summary>
        public static IReadOnlyList<string> CardsOf(PartyMockData data, string id)
        {
            if (id != null && cards.TryGetValue(id, out string[] set))
                return set;
            var member = data != null ? data.Find(id) : null;
            return member != null
                ? Array.ConvertAll(member.Cards, card => card.Skill)
                : Array.Empty<string>();
        }

        public static void SetCards(string id, IReadOnlyList<string> skills)
        {
            if (!string.IsNullOrEmpty(id) && skills != null)
                cards[id] = skills.ToArray();
        }

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            formation = null;
            levels.Clear();
            cards.Clear();
        }
    }
}
