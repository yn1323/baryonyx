using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;
using UnityEngine;

namespace Baryonyx.StepBonus
{
    /// <summary>
    /// Keeps the bonus slots while the app runs and where they are saved. The app sets
    /// <see cref="Source"/> to the game server, which keeps each player's bonuses and slots;
    /// a change shows at once and is saved behind it (<see cref="OptimisticState{T}"/>). Without
    /// it (the showcase, or no server URL) the slots start from the mock data and stay only
    /// while the app runs. Home hands over today's ACT so the tavern shows the same open
    /// slots.
    /// </summary>
    public static class StepBonusSession
    {
        // 酒場のメニューの「ボーナス」の項目のキー。この項目はリストの代わりにボーナス設定を開く。
        public const string GuideItemKey = "bonus";

        private static StepBonusLoadout loadout;

        // サーバーが確かめた持ち物と枠と、保存を待っている付け替え。まだ読んでいなければnull。
        private static OptimisticState<StepBonusState> saves;

        // 持ち物と枠を読み書きするサーバー。なければ仮データを使う。
        public static IStepBonusSource Source { get; set; }

        // ホームで取得した今日のACT。取得していなければnull。
        public static int? TodayAct { get; set; }

        public static StepBonusLoadout Loadout(StepBonusMockData data) =>
            loadout ??= StepBonusLoadout.From(data);

        // サーバーから読んだ、または保存した結果に置き換える。
        public static void Use(StepBonusLoadout value) => loadout = value;

        // サーバーが確かめた持ち物と枠に、保存を待っている付け替えを当てたもの。まだ読んでいなければnull。
        public static StepBonusState State => saves?.Current;

        /// <summary>
        /// The player's bonuses and slots from the server. While changes wait to be saved it
        /// returns the shown slots without reading, since the server would answer with the
        /// slots before them.
        /// </summary>
        public static async Task<StepBonusState> LoadAsync(
            IStepBonusSource source,
            CancellationToken token
        )
        {
            if (saves is { Pending: true })
                return saves.Current;
            var state = await source.LoadAsync(token);
            if (saves == null)
                saves = new OptimisticState<StepBonusState>(state, source.LoadAsync);
            else
                saves.Confirm(state);
            return saves.Current;
        }

        /// <summary>
        /// Puts the bonus in the slot at once and saves it behind the writes before it. Returns
        /// the slots to show once saved, or put back when it failed (told on the notice band).
        /// </summary>
        public static async Task<StepBonusState> SetSlotAsync(
            IStepBonusSource source,
            int slot,
            string bonusId,
            Func<Exception, string> failedMessage
        )
        {
            if (saves == null)
                throw new InvalidOperationException("The bonuses are saved after they are read.");
            await saves.ChangeAsync(
                state => state.WithSlot(slot, bonusId),
                token => source.SetSlotAsync(slot, bonusId, token),
                failedMessage
            );
            return saves.Current;
        }

        public static int ActOr(StepBonusMockData data) =>
            TodayAct ?? (data != null ? data.TodayAct : 0);

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            loadout = null;
            saves = null;
            TodayAct = null;
            Source = null;
        }
    }
}
