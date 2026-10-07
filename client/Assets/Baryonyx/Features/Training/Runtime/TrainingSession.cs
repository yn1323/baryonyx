using System;
using System.Collections.Generic;
using Baryonyx.Party;
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
