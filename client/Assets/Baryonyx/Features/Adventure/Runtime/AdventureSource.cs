using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Inventory;

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

        // 今いるマスの出来事（戦闘の勝利・宝箱など）を終え、報酬を手に入れる。戦闘なら遭遇した敵を添える。
        // 怪しい宝箱を開けずに通り過ぎるときと、試練の条件を満たしたときは options で伝える。
        Task<AdventureState> ClearAsync(
            string roomId,
            CancellationToken token,
            IReadOnlyList<AdventureEnemyTally> enemies = null,
            AdventureClearOptions options = null
        );

        // 怪しい宝箱を開ける。ミミックなら戦闘のマスになり、本物ならその場で終えて物を落とす。
        Task<AdventureState> OpenAsync(string roomId, CancellationToken token);

        // 行商人から、品物の番号の物をルーンで買う。
        Task<AdventureState> BuyAsync(string roomId, int index, CancellationToken token);

        // 負けた戦闘からルーンで復活する。
        Task<AdventureState> ReviveAsync(string roomId, CancellationToken token);

        // 負けて終えるときは、負けた戦闘で遭遇した敵を添える。
        Task<AdventureState> EndAsync(
            AdventureEndReason reason,
            CancellationToken token,
            IReadOnlyList<AdventureEnemyTally> enemies = null
        );
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

        public Task<AdventureState> ClearAsync(
            string roomId,
            CancellationToken token,
            IReadOnlyList<AdventureEnemyTally> enemies = null,
            AdventureClearOptions options = null
        ) => Run(session => api.ClearAsync(session, roomId, enemies, options, token), token);

        public Task<AdventureState> OpenAsync(string roomId, CancellationToken token) =>
            Run(session => api.OpenAsync(session, roomId, token), token);

        public Task<AdventureState> BuyAsync(string roomId, int index, CancellationToken token)
        {
            // 応答が届かずに送り直しても、同じ要求IDで二重に買わない。
            string requestId = Guid.NewGuid().ToString("D");
            return Run(session => api.BuyAsync(session, requestId, roomId, index, token), token);
        }

        public Task<AdventureState> ReviveAsync(string roomId, CancellationToken token)
        {
            // 応答が届かずに送り直しても、同じ要求IDで二重にルーンを使わない。
            string requestId = Guid.NewGuid().ToString("D");
            return Run(session => api.ReviveAsync(session, requestId, roomId, token), token);
        }

        public Task<AdventureState> EndAsync(
            AdventureEndReason reason,
            CancellationToken token,
            IReadOnlyList<AdventureEnemyTally> enemies = null
        ) =>
            Run(
                session =>
                    api.EndAsync(
                        session,
                        reason == AdventureEndReason.Defeat ? "defeat" : "retreat",
                        enemies,
                        token
                    ),
                token
            );

        private async Task<AdventureState> Run(
            Func<AccountSession, Task<AdventureApiClient.State>> operation,
            CancellationToken token
        ) => ToState(await sessions.WithSessionAsync(operation, token));

        // JsonUtilityはnullの代わりに空の値を入れるため、IDの空いた冒険・ドロップ・結果をなしとみなす。
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
                state.drops != null && state.drops.Length > 0 ? ToDrops(state.drops) : null,
                ToResult(state.result),
                state.revive != null && !string.IsNullOrEmpty(state.revive.roomId)
                    ? state.revive.runes
                    : null,
                state.stepDoor != null
                && (state.stepDoor.steps > 0 || state.stepDoor.tier > 0 || IsStepDoor(run))
                    ? new AdventureStepDoor(state.stepDoor.steps, state.stepDoor.tier)
                    : null
            );
        }

        // 歩数の扉を終えた応答。0歩の扉も、扉のマスなら結果として見せる。
        private static bool IsStepDoor(AdventureApiClient.Run run) =>
            run != null && run.roomKind == "step-door" && run.roomCleared;

        // 道は、サーバーが冒険を始めたときに決めた種と部屋の数から、毎回同じ形に作る。
        // 「？」のマスは、サーバーが保存した中身の種類を正体として持たせる。
        private static AdventureRun ToRun(AdventureApiClient.Run run)
        {
            var map = AdventureCatalog.Route(run.destinationId, run.seed, run.roomCount);
            var revealed =
                map.Find(run.roomId)?.Kind == AdventureRoomKind.Mystery
                    ? AdventureApiClient.ParseKind(run.roomKind)
                    : null;
            return new(
                run.id,
                run.destinationId,
                run.roomId,
                run.roomCleared,
                run.route ?? Array.Empty<string>(),
                run.revives,
                run.reviveCost,
                map,
                ToDrops(run.drops),
                run.floor,
                new AdventureContents(
                    run.mimic,
                    AdventureTrials.Parse(run.trial),
                    (run.merchant ?? Array.Empty<AdventureApiClient.Ware>())
                        .Select(ToWare)
                        .Where(ware => ware != null)
                        .ToArray(),
                    revealed is AdventureRoomKind.Mystery or AdventureRoomKind.Start
                        ? null
                        : revealed
                )
            );
        }

        // 知らない分類や品質の品物は並べない。
        private static AdventureWare ToWare(AdventureApiClient.Ware ware)
        {
            if (
                ware == null
                || string.IsNullOrEmpty(ware.id)
                || !AdventureDrops.TryParse(ware.category, out var category)
                || !Quality.TryParse(ware.quality, out var quality)
            )
                return null;
            return new AdventureWare(
                ware.index,
                category,
                ware.id,
                ware.multiplierPercent / 100f,
                quality,
                ware.price,
                ware.sold
            );
        }

        private static AdventureDrop[] ToDrops(AdventureApiClient.Drop[] drops) =>
            (drops ?? Array.Empty<AdventureApiClient.Drop>())
                .Select(ToDrop)
                .Where(drop => drop != null)
                .ToArray();

        // 知らない分類や品質、倍率が届いていない物（アイテムを除く）はドロップから外す。
        private static AdventureDrop ToDrop(AdventureApiClient.Drop drop)
        {
            if (
                drop == null
                || string.IsNullOrEmpty(drop.roomId)
                || string.IsNullOrEmpty(drop.id)
                || !AdventureDrops.TryParse(drop.category, out var category)
            )
                return null;
            if (category is AdventureDropCategory.Item or AdventureDropCategory.Material)
                return new AdventureDrop(drop.roomId, category, drop.id);
            if (
                drop.multiplier < Quality.MinMultiplier
                || !Quality.TryParse(drop.quality, out var quality)
            )
                return null;
            return new AdventureDrop(drop.roomId, category, drop.id, drop.multiplier, quality);
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
                ToDrops(result.drops)
            );
        }
    }
}
