using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Baryonyx.Party
{
    /// <summary>
    /// Keeps the formation and the characters' levels and card skills, and where they are saved.
    /// The app sets <see cref="Source"/> to the game server, which keeps each player's party; the
    /// tavern's screens read it on opening and save every change there (doc/features/party.md).
    /// Without it (the showcase, or no server URL) they start from the mock data and stay only
    /// while the app runs. The mock data asset itself is never written.
    /// </summary>
    public static class PartySession
    {
        // 編成（コードではTavern）のメニューの「パーティ」の項目のキー。この項目はリストの代わりに編成を開く。
        public const string GuideItemKey = "formation";

        private static PartyFormation formation;

        // サーバーから最後に読んだ、または保存した結果。サーバーがなければnull。
        private static PartyState state;

        // サーバーがないときに育成で上げたレベル。上げていないキャラは仮データのレベルのまま。
        private static readonly Dictionary<string, int> levels = new();

        // サーバーがないときに酒場のスキルの画面で付け替えたカード（スキルIDを枠の順に）。
        private static readonly Dictionary<string, string[]> cards = new();

        // 編成・レベル・カードを読み書きするサーバー。なければ仮データを使う。
        public static IPartySource Source { get; set; }

        public static PartyState State => state;

        public static PartyFormation Formation(PartyMockData data) =>
            formation ??= PartyFormation.From(data);

        /// <summary>
        /// Replaces the party with what the server read or saved, and returns its formation. The
        /// names and art still come from the mock data.
        /// </summary>
        public static PartyFormation Use(PartyMockData data, PartyState value)
        {
            state = value;
            formation = PartyFormation.From(data, value);
            return formation;
        }

        /// <summary>
        /// The character's level: the server's, or without it the raised level or the mock
        /// data's. Outside Play Mode (the prefab generators) it is always the mock data's level.
        /// </summary>
        public static int LevelOf(PartyMockData data, string id)
        {
            if (state != null)
                return state.Find(id)?.Level ?? 1;
            if (id != null && levels.TryGetValue(id, out int level))
                return level;
            var member = data != null ? data.Find(id) : null;
            return member != null ? member.Level : 1;
        }

        // サーバーがないときだけ使う。サーバーがあれば、レベルはサーバーで上げる。
        public static void SetLevel(string id, int level)
        {
            if (!string.IsNullOrEmpty(id))
                levels[id] = Mathf.Max(1, level);
        }

        /// <summary>
        /// The character's card skills (skill ids, in slot order): the server's, or without it
        /// the cards set on the tavern's card skills or the mock data's. Outside Play Mode (the
        /// prefab generators) they are always the mock data's.
        /// </summary>
        public static IReadOnlyList<string> CardsOf(PartyMockData data, string id)
        {
            if (state != null)
                return state.Find(id)?.Cards ?? Array.Empty<string>();
            if (id != null && cards.TryGetValue(id, out string[] set))
                return set;
            var member = data != null ? data.Find(id) : null;
            return member != null
                ? Array.ConvertAll(member.Cards, card => card.Skill)
                : Array.Empty<string>();
        }

        // サーバーがないときだけ使う。サーバーがあれば、カードはサーバーで付け替える。
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
            state = null;
            levels.Clear();
            cards.Clear();
            Source = null;
        }
    }
}
