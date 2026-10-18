using System;
using System.Collections.Generic;
using Baryonyx.Party;
using Baryonyx.UI.GuideMenu;
using UnityEngine;

namespace Baryonyx.Training
{
    /// <summary>
    /// Keeps the character on screen while the app runs, and the runes a level-up spends when
    /// the app has no server. Levels are the party's (<see cref="PartySession"/>): with a server,
    /// the server raises them and spends its runes (doc/features/progression.md); without it,
    /// the runes are taken off the mock data's and reset when the app starts.
    /// </summary>
    public static class TrainingSession
    {
        // 編成の「育成」の項目のキー。メニューには出さず、冒険者の一覧から1人の個別の画面として開く。
        public const string GuideItemKey = "training";

        // 個別の画面を開いた編成の項目のキー（「もどる」の行き先とパンくず）。nullなら冒険者の一覧。
        public static string ReturnKey { get; set; }

        // 「もどる」で戻る編成の項目のキー。
        public static string ReturnKeyOrRoster => ReturnKey ?? PartySession.GuideItemKey;

        /// <summary>
        /// Opens the page of <paramref name="id"/> from the formation's item
        /// <paramref name="returnKey"/> (null for the adventurers' list), where back returns.
        /// </summary>
        public static bool OpenPage(GuideMenuView guide, string id, string returnKey)
        {
            if (guide == null || !guide.HasItem(GuideItemKey))
                return false;
            Selected = id;
            ReturnKey = returnKey;
            return guide.OpenItem(GuideItemKey);
        }

        // 付け替えの画面から、同じ人を選んだまま個別の画面へ戻る。
        public static bool BackToPage(GuideMenuView guide) =>
            guide != null && guide.HasItem(GuideItemKey) && guide.OpenItem(GuideItemKey);

        // 個別の画面から、開いた項目（冒険者の一覧か装備の人選び）へ戻る。
        public static bool LeavePage(GuideMenuView guide) =>
            guide != null && guide.HasItem(ReturnKeyOrRoster) && guide.OpenItem(ReturnKeyOrRoster);

        /// <summary>
        /// The breadcrumb of a change screen opened from the page: the item the page was opened
        /// from, the person (empty until read) and the change screen's item <paramref name="key"/>,
        /// e.g. 「冒険者 › アリア › 装備」.
        /// </summary>
        public static IReadOnlyList<string> ChangeTrail(
            GuideMenuView guide,
            string person,
            string key
        ) =>
            guide != null
                ? new[] { guide.LabelOf(ReturnKeyOrRoster), person, guide.LabelOf(key) }
                : Array.Empty<string>();

        // サーバーがないときに育成で使ったルーンの合計。
        public static long Spent { get; private set; }

        // 最後に見ていたキャラのID。冒険者の一覧・装備・スキルの画面と共有する。
        public static string Selected
        {
            get => PartySession.Selected;
            set => PartySession.Selected = value;
        }

        // サーバーがないときの所持ルーン。仮データの所持ルーンから使った分を引く。
        public static long RunesOr(TrainingMockData data) =>
            Math.Max(0L, (data != null ? data.MockRunes : 0) - Spent);

        public static void Spend(long runes) => Spent += Math.Max(0L, runes);

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Spent = 0;
            ReturnKey = null;
        }
    }

    /// <summary>Where the training reads and writes levels, runes and the character on screen.</summary>
    public interface ITrainingStore
    {
        int LevelOf(string id);

        // サーバーがないときだけ呼ぶ。サーバーがあれば、レベルとルーンはサーバーで変える。
        void SetLevel(string id, int level);

        // 付けているスキルのID（枠の順）。
        IReadOnlyList<string> CardsOf(string id);
        long Runes { get; }
        void Spend(long runes);

        // レベルの上限と、Lv n から n+1 へ上げるのに要るルーンの n あたり。
        int MaxLevel { get; }
        int CostPerLevel { get; }
        string Selected { get; set; }
    }

    /// <summary>
    /// The store of the running app: the party's levels and cards, and the runes and level rules
    /// of the server, or of the mock data when the app has no server.
    /// </summary>
    public sealed class TrainingSessionStore : ITrainingStore
    {
        private readonly PartyMockData party;
        private readonly TrainingMockData data;

        public TrainingSessionStore(PartyMockData party, TrainingMockData data)
        {
            this.party = party;
            this.data = data;
        }

        public int LevelOf(string id) => PartySession.LevelOf(party, id);

        public void SetLevel(string id, int level) => PartySession.SetLevel(id, level);

        public IReadOnlyList<string> CardsOf(string id) => PartySession.CardsOf(party, id);

        public long Runes => PartySession.State?.Runes ?? TrainingSession.RunesOr(data);

        public void Spend(long runes) => TrainingSession.Spend(runes);

        public int MaxLevel =>
            PartySession.State is { MaxLevel: > 1 } state ? state.MaxLevel : data.MaxLevel;

        public int CostPerLevel =>
            PartySession.State is { CostPerLevel: > 0 } state
                ? state.CostPerLevel
                : data.CostPerLevel;

        public string Selected
        {
            get => TrainingSession.Selected;
            set => TrainingSession.Selected = value;
        }
    }
}
