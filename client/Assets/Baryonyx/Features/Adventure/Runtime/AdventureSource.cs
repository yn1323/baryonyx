using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.StepBonus;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// Where the adventure is read and saved. Every request answers the whole state, so the
    /// screens show what was saved. A request the adventure's state does not allow fails with
    /// <see cref="Networking.ServerApiException"/> (409, or 404 with no adventure).
    /// </summary>
    public interface IAdventureSource
    {
        Task<AdventureState> LoadAsync(CancellationToken token);

        Task<AdventureState> StartAsync(string destinationId, CancellationToken token);

        // 道で次の階の部屋を選んで進む。選んだ時点で保存する。
        Task<AdventureState> MoveAsync(AdventureRoom room, CancellationToken token);

        // 今いる部屋の出来事（戦闘の勝利・宝箱）を終え、報酬を手に入れる。
        Task<AdventureState> ClearAsync(string roomId, CancellationToken token);

        // 負けた戦闘からルーンで復活する。
        Task<AdventureState> ReviveAsync(string roomId, CancellationToken token);

        Task<AdventureState> EndAsync(AdventureEndReason reason, CancellationToken token);
    }

    /// <summary>
    /// Reads and saves the adventure on the game server with the shared session, so the
    /// adventure goes on from the room chosen after the app closes.
    /// </summary>
    public sealed class AdventureServerSource : IAdventureSource
    {
        private readonly IAccountSessionRunner sessions;
        private readonly AdventureApiClient api;

        public AdventureServerSource(IAccountSessionRunner sessions, AdventureApiClient api)
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public Task<AdventureState> LoadAsync(CancellationToken token) =>
            Run(session => api.ReadAsync(session, token), token);

        public Task<AdventureState> StartAsync(string destinationId, CancellationToken token) =>
            Run(session => api.StartAsync(session, destinationId, token), token);

        public Task<AdventureState> MoveAsync(AdventureRoom room, CancellationToken token) =>
            Run(session => api.MoveAsync(session, room, token), token);

        public Task<AdventureState> ClearAsync(string roomId, CancellationToken token) =>
            Run(session => api.ClearAsync(session, roomId, token), token);

        public Task<AdventureState> ReviveAsync(string roomId, CancellationToken token)
        {
            // 応答が届かずに送り直しても、同じ要求IDで二重にルーンを使わない。
            string requestId = Guid.NewGuid().ToString("D");
            return Run(session => api.ReviveAsync(session, requestId, roomId, token), token);
        }

        public Task<AdventureState> EndAsync(AdventureEndReason reason, CancellationToken token) =>
            Run(
                session =>
                    api.EndAsync(
                        session,
                        reason == AdventureEndReason.Defeat ? "defeat" : "retreat",
                        token
                    ),
                token
            );

        private async Task<AdventureState> Run(
            Func<AccountSession, Task<AdventureApiClient.State>> operation,
            CancellationToken token
        ) => ToState(await sessions.WithSessionAsync(operation, token));

        // JsonUtilityはnullの代わりに空の値を入れるため、IDの空いた冒険・報酬・結果をなしとみなす。
        public static AdventureState ToState(AdventureApiClient.State state)
        {
            if (state == null)
                return AdventureState.Empty;
            var run = state.run;
            var records = (state.records ?? Array.Empty<AdventureApiClient.Record>())
                .Where(record => record != null && !string.IsNullOrEmpty(record.destinationId))
                .Select(record => new AdventureRecord(
                    record.destinationId,
                    record.bestFloor,
                    record.clears
                ))
                .ToArray();
            return new AdventureState(
                run != null && !string.IsNullOrEmpty(run.id) ? ToRun(run) : null,
                records,
                state.runes,
                ToReward(state.reward),
                ToResult(state.result),
                state.revive != null && !string.IsNullOrEmpty(state.revive.roomId)
                    ? state.revive.runes
                    : null
            );
        }

        // 道は、サーバーが冒険を始めたときに決めた種と部屋の数から、毎回同じ形に作る。
        private static AdventureRun ToRun(AdventureApiClient.Run run) =>
            new(
                run.id,
                run.destinationId,
                run.roomId,
                run.roomCleared,
                run.route ?? Array.Empty<string>(),
                run.revives,
                run.reviveCost,
                AdventureCatalog.Route(run.destinationId, run.seed, run.roomCount),
                ToRewards(run.rewards),
                run.floor
            );

        private static AdventureReward[] ToRewards(AdventureApiClient.Reward[] rewards) =>
            (rewards ?? Array.Empty<AdventureApiClient.Reward>())
                .Select(ToReward)
                .Where(reward => reward != null)
                .ToArray();

        // 知らないランクのボーナスは報酬から外す。
        private static AdventureReward ToReward(AdventureApiClient.Reward reward)
        {
            if (
                reward == null
                || string.IsNullOrEmpty(reward.roomId)
                || string.IsNullOrEmpty(reward.bonusId)
                || reward.rank == null
                || !Enum.IsDefined(typeof(StepBonusRank), reward.rank)
            )
                return null;
            return new AdventureReward(
                reward.roomId,
                reward.bonusId,
                (StepBonusRank)Enum.Parse(typeof(StepBonusRank), reward.rank),
                reward.outcome switch
                {
                    "updated" => AdventureRewardOutcome.Updated,
                    "discarded" => AdventureRewardOutcome.Discarded,
                    _ => AdventureRewardOutcome.Added,
                }
            );
        }

        private static AdventureResult ToResult(AdventureApiClient.Result result)
        {
            if (result == null || string.IsNullOrEmpty(result.destinationId))
                return null;
            return new AdventureResult(
                result.destinationId,
                result.status switch
                {
                    "cleared" => AdventureEndStatus.Cleared,
                    "defeated" => AdventureEndStatus.Defeated,
                    _ => AdventureEndStatus.Retreated,
                },
                result.floor,
                result.bestFloor,
                result.newRecord,
                result.clears,
                result.revives,
                ToRewards(result.rewards)
            );
        }
    }
}
