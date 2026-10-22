using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Economy;
using Baryonyx.Networking;
using UnityEngine;

namespace Baryonyx.Party
{
    /// <summary>
    /// Keeps the formation and the characters' levels and card skills, and where they are saved.
    /// The app sets <see cref="Source"/> to the game server, which keeps each player's party; the
    /// tavern's screens read it on opening and save every change there (doc/features/party.md).
    /// A change shows at once and is saved behind it in order (<see cref="OptimisticState{T}"/>);
    /// one that cannot be saved goes back and is told on the notice band. Without a server (the
    /// showcase, or no server URL) they start from the mock data and stay only while the app
    /// runs. The mock data asset itself is never written.
    /// </summary>
    public static class PartySession
    {
        // 編成（コードではTavern）のメニューの「冒険者」の項目のキー。この項目はリストの代わりに冒険者の一覧を開く。
        public const string GuideItemKey = "formation";

        private static PartyFormation formation;

        // 編成を作るときの名前と絵。サーバーの結果を使うたびに覚える。
        private static PartyMockData data;

        // サーバーが確かめた編成と、保存を待っている変更。サーバーがなければnull。
        private static OptimisticState<PartyState> saves;

        // サーバーがないときに育成で上げたレベル。上げていないキャラは仮データのレベルのまま。
        private static readonly Dictionary<string, int> levels = new();

        // サーバーがないときに酒場のスキルの画面で付け替えたカード（PartyCardKey の形で枠の順に）。
        private static readonly Dictionary<string, string[]> cards = new();

        // 編成・レベル・カードを読み書きするサーバー。なければ仮データを使う。
        public static IPartySource Source { get; set; }

        // 最後に選んでいた冒険者のID。一覧・個別・付け替えの画面が共有し、開き直すとその人から見せる。
        public static string Selected { get; set; }

        // サーバーが確かめた編成に、保存を待っている変更を当てたもの。サーバーがなければnull。
        public static PartyState State => saves?.Current;

        // 保存を待っている変更がある。
        public static bool Saving => saves?.Pending ?? false;

        public static PartyFormation Formation(PartyMockData data) =>
            formation ??= PartyFormation.From(data);

        /// <summary>
        /// Replaces the party with what the server read or saved, and returns its formation. The
        /// names and art still come from the mock data.
        /// </summary>
        public static PartyFormation Use(PartyMockData data, PartyState value)
        {
            PartySession.data = data;
            if (value == null)
                return Formation(data);
            // 固有スキルの進み具合も、サーバーが確かめたものにそろえる（doc/features/skill-evolution.md）。
            PartyUniqueSkills.Use(value);
            if (saves == null)
            {
                var source = Source;
                saves = new OptimisticState<PartyState>(
                    value,
                    source != null ? token => source.LoadAsync(token) : null
                );
                saves.Changed += Show;
                Show();
            }
            else
                saves.Confirm(value);
            return formation;
        }

        // 保存を待っている変更も含めた編成で作り直す。右上のルーンは確かめた残高にし、
        // 保存を待っているレベルアップの分は RuneBalance の取り置きで引く。
        private static void Show()
        {
            RuneBalance.Set(saves.Confirmed.Runes);
            formation = PartyFormation.From(data, saves.Current);
        }

        /// <summary>
        /// The formation a panel shows on opening: the server's when <paramref name="source"/> is
        /// set (it replaces the party), otherwise the one kept while the app runs.
        /// </summary>
        public static async Task<PartyFormation> LoadAsync(
            PartyMockData data,
            IPartySource source,
            CancellationToken token
        )
        {
            if (source == null)
                return Formation(data);
            // 保存を待っている間は、読み直すと保存前の編成が返るので、手元の編成を出す。
            if (Saving)
                return Use(data, saves.Confirmed);
            return Use(data, await source.LoadAsync(token));
        }

        /// <summary>
        /// The formation now, for the screens that show the party without changing it (Home, the
        /// adventure): the one already read from the server or kept while the app runs, and
        /// otherwise read from the server first.
        /// </summary>
        public static Task<PartyFormation> CurrentAsync(PartyMockData data, CancellationToken token)
        {
            var source = Source;
            if (source == null || saves != null)
                return Task.FromResult(Formation(data));
            return LoadAsync(data, source, token);
        }

        /// <summary>The members in the party's slots, in slot order; null for an empty slot.</summary>
        public static IReadOnlyList<PartyMember> Members(PartyFormation formation)
        {
            var members = new PartyMember[PartyFormation.Size];
            for (int slot = 0; slot < members.Length; slot++)
                members[slot] = formation?.Find(formation.Member(slot));
            return members;
        }

        /// <summary>
        /// Puts the character in the slot (null empties it) at once and saves it on the server
        /// behind the writes before it. Returns the formation to show once it is saved, or put
        /// back when it failed (told as <paramref name="failedMessage"/>).
        /// </summary>
        public static Task<PartyFormation> SetSlotAsync(
            int slot,
            string characterId,
            string failedMessage
        ) =>
            Save(
                state => state.WithSlot(slot, characterId),
                (source, token) => source.SetSlotAsync(slot, characterId, token),
                failedMessage
            );

        /// <summary>
        /// Sets the character's card slot to the key (<see cref="PartyCardKey"/>) at once and
        /// saves it (see <see cref="SetSlotAsync"/>).
        /// </summary>
        public static Task<PartyFormation> SetCardAsync(
            string characterId,
            int slot,
            string key,
            string failedMessage
        ) =>
            Save(
                state => state.WithCard(characterId, slot, key),
                (source, token) => source.SetCardAsync(characterId, slot, key, token),
                failedMessage
            );

        /// <summary>
        /// Raises the character at once, taking <paramref name="runes"/> off the balance, and
        /// saves it; the server works out what it really costs (see <see cref="SetSlotAsync"/>).
        /// </summary>
        public static async Task<PartyFormation> LevelUpAsync(
            string characterId,
            int fromLevel,
            int toLevel,
            long runes,
            Func<Exception, string> failedMessage
        )
        {
            RuneBalance.Reserve(runes);
            try
            {
                return await Save(
                    state => state.WithLevel(characterId, toLevel, runes),
                    (source, token) => source.LevelUpAsync(characterId, fromLevel, toLevel, token),
                    failedMessage
                );
            }
            finally
            {
                RuneBalance.Release(runes);
            }
        }

        private static Task<PartyFormation> Save(
            Func<PartyState, PartyState> apply,
            Func<IPartySource, CancellationToken, Task<PartyState>> send,
            string failedMessage
        ) => Save(apply, send, _ => failedMessage);

        private static async Task<PartyFormation> Save(
            Func<PartyState, PartyState> apply,
            Func<IPartySource, CancellationToken, Task<PartyState>> send,
            Func<Exception, string> failedMessage
        )
        {
            var source = Source;
            if (source == null || saves == null)
                throw new InvalidOperationException(
                    "The party is saved after it is read from the server."
                );
            await saves.ChangeAsync(apply, token => send(source, token), failedMessage);
            return formation;
        }

        /// <summary>
        /// The character's level: the server's, or without it the raised level or the mock
        /// data's. Outside Play Mode (the prefab generators) it is always the mock data's level.
        /// </summary>
        public static int LevelOf(PartyMockData data, string id)
        {
            var state = State;
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
        /// The character's custom skill slots as keys (<see cref="PartyCardKey"/>, in slot order):
        /// the server's, or without it the skill ids set on the tavern's card skills or the mock
        /// data's. Outside Play Mode (the prefab generators) they are always the mock data's.
        /// </summary>
        public static IReadOnlyList<string> CardsOf(PartyMockData data, string id)
        {
            var state = State;
            if (state != null)
                return state.Find(id)?.Cards ?? Array.Empty<string>();
            if (id != null && cards.TryGetValue(id, out string[] set))
                return set;
            var member = data != null ? data.Find(id) : null;
            return member != null
                ? Array.ConvertAll(member.Cards, card => card.Skill)
                : Array.Empty<string>();
        }

        /// <summary>
        /// The skill ids of the character's slots, in slot order; null for an item or an empty
        /// slot (<see cref="CardsOf"/> resolved through the owned cards).
        /// </summary>
        public static IReadOnlyList<string> SkillIdsOf(PartyMockData data, string id)
        {
            var keys = CardsOf(data, id);
            var state = State;
            return state == null ? keys : keys.Select(state.SkillIdOf).ToArray();
        }

        /// <summary>The skill a slot's key stands for, with its multiplier (see <see cref="PartyState.SkillOf"/>).</summary>
        public static Combat.CardSkill SkillOf(string key) =>
            State != null ? State.SkillOf(key)
            : PartyCardKey.IsItem(key) ? null
            : Combat.CardSkills.Find(key);

        /// <summary>
        /// The element icons of the character's card skills, in card order without repeats
        /// (<see cref="SkillIdsOf"/> and <see cref="PartyMockData.IconOf"/>).
        /// </summary>
        public static IReadOnlyList<Sprite> ElementIconsOf(PartyMockData data, string id) =>
            data == null || id == null
                ? Array.Empty<Sprite>()
                : data.IconsOf(SkillIdsOf(data, id).Where(skill => skill != null));

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
            data = null;
            saves = null;
            levels.Clear();
            cards.Clear();
            Source = null;
            Selected = null;
        }
    }
}
