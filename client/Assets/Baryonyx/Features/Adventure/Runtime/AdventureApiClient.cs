using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Networking;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The server's adventure API: the adventure in progress, the room chosen, the events done,
    /// the revives paid with runes and the end of an adventure
    /// (server/src/features/adventure/routes.ts).
    /// </summary>
    public sealed class AdventureApiClient
    {
        private readonly ServerApi server;

        public AdventureApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public Task<State> ReadAsync(AccountSession session, CancellationToken token) =>
            server.SendAsync<State>("/v1/adventure", "GET", null, session.Token, token);

        public Task<State> StartAsync(
            AccountSession session,
            string destinationId,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                "/v1/adventure",
                "POST",
                new StartRequest { destinationId = destinationId },
                session.Token,
                token
            );

        // 部屋のIDと階と種類は、冒険の種から作った道のもの。サーバーは1階ずつ進むことを確かめる。
        public Task<State> MoveAsync(
            AccountSession session,
            AdventureRoom room,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                "/v1/adventure/move",
                "POST",
                new MoveRequest
                {
                    roomId = room.Id,
                    floor = room.Floor,
                    kind = KindName(room.Kind),
                },
                session.Token,
                token
            );

        public static string KindName(AdventureRoomKind kind) =>
            kind switch
            {
                AdventureRoomKind.Battle => "battle",
                AdventureRoomKind.Elite => "elite",
                AdventureRoomKind.Treasure => "treasure",
                AdventureRoomKind.Boss => "boss",
                _ => "start",
            };

        public Task<State> ClearAsync(
            AccountSession session,
            string roomId,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                "/v1/adventure/clear",
                "POST",
                new RoomRequest { roomId = roomId },
                session.Token,
                token
            );

        public Task<State> ReviveAsync(
            AccountSession session,
            string requestId,
            string roomId,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                "/v1/adventure/revive",
                "POST",
                new ReviveRequest { requestId = requestId, roomId = roomId },
                session.Token,
                token
            );

        public Task<State> EndAsync(
            AccountSession session,
            string reason,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                "/v1/adventure/end",
                "POST",
                new EndRequest { reason = reason },
                session.Token,
                token
            );

        [Serializable]
        private sealed class StartRequest
        {
            public string destinationId;
        }

        [Serializable]
        private sealed class RoomRequest
        {
            public string roomId;
        }

        [Serializable]
        private sealed class MoveRequest
        {
            public string roomId;
            public int floor;
            public string kind;
        }

        [Serializable]
        private sealed class ReviveRequest
        {
            public string requestId;
            public string roomId;
        }

        [Serializable]
        private sealed class EndRequest
        {
            public string reason;
        }

        // 冒険がないときの run と、出来事のない応答の reward・revive・result はJSONではnullになる。
        // JsonUtilityはnullの代わりに空の値を入れるため、IDが空かどうかで見分ける。
        [Serializable]
        public sealed class State
        {
            public Run run;
            public Record[] records;
            public long runes;
            public Reward reward;
            public Revive revive;
            public Result result;
        }

        [Serializable]
        public sealed class Run
        {
            public string id;
            public string destinationId;

            // 道を作る乱数の種と、入口と最奥の間のあいだに通る部屋の数。
            public int seed;
            public int roomCount;
            public string roomId;
            public int floor;
            public string roomKind;
            public bool roomCleared;
            public string[] route;
            public int revives;
            public int reviveCost;
            public Reward[] rewards;
        }

        [Serializable]
        public sealed class Reward
        {
            public string roomId;
            public string bonusId;
            public string rank;
            public string outcome;
        }

        [Serializable]
        public sealed class Revive
        {
            public string roomId;
            public int runes;
            public long balanceAfter;
        }

        [Serializable]
        public sealed class Result
        {
            public string runId;
            public string destinationId;
            public string status;
            public int floor;
            public int bestFloor;
            public bool newRecord;
            public int clears;
            public int revives;
            public Reward[] rewards;
        }

        [Serializable]
        public sealed class Record
        {
            public string destinationId;
            public int bestFloor;
            public int clears;
        }
    }
}
