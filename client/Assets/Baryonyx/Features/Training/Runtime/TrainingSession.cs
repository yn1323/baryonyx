using System;
using System.Collections.Generic;
using Baryonyx.Party;
using UnityEngine;

namespace Baryonyx.Training
{
    /// <summary>
    /// Keeps the training's runes and the character on screen while the app runs. Levels are
    /// the party's (<see cref="PartySession"/>). There is no server for levels yet, so the
    /// runes a level-up spends are only taken off the balance Home read from the server, and
    /// both reset when the app starts (doc/features/progression.md).
    /// </summary>
    public static class TrainingSession
    {
        // 酒場のメニューの「育成」の項目のキー。この項目はリストの代わりに育成を開く。
        public const string GuideItemKey = "training";

        // ホームで取得した所持ルーン。取得していなければnull。
        public static long? HomeRunes { get; set; }

        // 育成で使ったルーンの合計。サーバーには保存しない。
        public static long Spent { get; private set; }

        // 最後に見ていたキャラのID。開き直すとそのキャラから見せる。
        public static string Selected { get; set; }

        public static long RunesOr(TrainingMockData data) =>
            Math.Max(0L, (HomeRunes ?? (data != null ? data.MockRunes : 0)) - Spent);

        public static void Spend(long runes) => Spent += Math.Max(0L, runes);

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            HomeRunes = null;
            Spent = 0;
            Selected = null;
        }
    }

    /// <summary>Where the training reads and writes levels, runes and the character on screen.</summary>
    public interface ITrainingStore
    {
        int LevelOf(string id);
        void SetLevel(string id, int level);

        // 付けているカードスキルのID（枠の順）。
        IReadOnlyList<string> CardsOf(string id);
        long Runes { get; }
        void Spend(long runes);
        string Selected { get; set; }
    }

    /// <summary>The store of the running app: the party's levels and cards, and the training's runes.</summary>
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

        public long Runes => TrainingSession.RunesOr(data);

        public void Spend(long runes) => TrainingSession.Spend(runes);

        public string Selected
        {
            get => TrainingSession.Selected;
            set => TrainingSession.Selected = value;
        }
    }
}
