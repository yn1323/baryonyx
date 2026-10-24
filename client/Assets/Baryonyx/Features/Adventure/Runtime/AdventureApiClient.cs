using System;
using System.Collections.Generic;
using System.Linq;
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
                AdventureRoomKind.Rest => "rest",
                AdventureRoomKind.Merchant => "merchant",
                AdventureRoomKind.ShadyChest => "shady-chest",
                AdventureRoomKind.Trial => "trial",
                AdventureRoomKind.StepDoor => "step-door",
                AdventureRoomKind.Altar => "altar",
                AdventureRoomKind.Mystery => "mystery",
                _ => "start",
            };

        /// <summary>The kind of a server's name (<see cref="KindName"/>), or null for a name it does not know.</summary>
        public static AdventureRoomKind? ParseKind(string name)
        {
            foreach (AdventureRoomKind kind in Enum.GetValues(typeof(AdventureRoomKind)))
                if (KindName(kind) == name)
                    return kind;
            return null;
        }

        public Task<State> ClearAsync(
            AccountSession session,
            string roomId,
            IReadOnlyList<AdventureEnemyTally> enemies,
            AdventureClearOptions options,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                "/v1/adventure/clear",
                "POST",
                new ClearRequest
                {
                    roomId = roomId,
                    enemies = Enemies(enemies),
                    pass = options?.Pass ?? false,
                    trialMet = options?.TrialMet ?? false,
                    evolved = options?.Evolved ?? false,
                },
                session.Token,
                token
            );

        // 怪しい宝箱を開ける。ミミックなら run.mimic が true になり、本物ならその場で終えて drops を返す。
        public Task<State> OpenAsync(
            AccountSession session,
            string roomId,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                "/v1/adventure/open",
                "POST",
                new OpenRequest { roomId = roomId },
                session.Token,
                token
            );

        // 行商人の品物を買う。同じ requestId の再送では二重に引かない。
        public Task<State> BuyAsync(
            AccountSession session,
            string requestId,
            string roomId,
            int index,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                "/v1/adventure/buy",
                "POST",
                new BuyRequest
                {
                    requestId = requestId,
                    roomId = roomId,
                    index = index,
                },
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
            IReadOnlyList<AdventureEnemyTally> enemies,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                "/v1/adventure/end",
                "POST",
                new EndRequest { reason = reason, enemies = Enemies(enemies) },
                session.Token,
                token
            );

        private static EnemyEntry[] Enemies(IReadOnlyList<AdventureEnemyTally> enemies) =>
            (enemies ?? Array.Empty<AdventureEnemyTally>())
                .Select(enemy => new EnemyEntry
                {
                    enemyId = enemy.EnemyId,
                    defeated = enemy.Defeated,
                })
                .ToArray();

        [Serializable]
        private sealed class StartRequest
        {
            public string destinationId;
        }

        [Serializable]
        private sealed class ClearRequest
        {
            public string roomId;

            // 戦闘で遭遇した敵の種類と倒した体数（ライブラリの記録）。戦闘のないマスは空。
            public EnemyEntry[] enemies;

            // 怪しい宝箱を開けずに通り過ぎる。試練の条件を満たして勝った。
            public bool pass;
            public bool trialMet;

            // 進化の祭壇で固有スキルを進化させた。
            public bool evolved;
        }

        [Serializable]
        private sealed class OpenRequest
        {
            public string roomId;
        }

        [Serializable]
        private sealed class BuyRequest
        {
            public string requestId;
            public string roomId;
            public int index;
        }

        [Serializable]
        private sealed class EnemyEntry
        {
            public string enemyId;
            public int defeated;
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
            public EnemyEntry[] enemies;
        }

        // 冒険がないときの run と、出来事のない応答の drops・revive・result はJSONではnullか、ない。
        // JsonUtilityはnullの代わりに空の値を入れるため、IDが空かどうかで見分ける。
        [Serializable]
        public sealed class State
        {
            public Run run;
            public Record[] records;
            public long runes;
            public Drop[] drops;
            public Revive revive;
            public Result result;

            // 歩数の扉を開けた応答だけにある。ほかでは0の値になる。
            public StepDoor stepDoor;
        }

        [Serializable]
        public sealed class StepDoor
        {
            public int steps;
            public int tier;
        }

        // 行商人の品物。倍率は百分率の整数（137なら×1.37）で、drops の倍率（小数）と形が違う。
        [Serializable]
        public sealed class Ware
        {
            public int index;
            public string category;
            public string id;
            public int multiplierPercent;
            public string quality;
            public int price;
            public bool sold;
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
            public Drop[] drops;

            // 開けた怪しい宝箱がミミックか。試練のマスの条件と、行商人のマスの品物（ほかのマスではない）。
            public bool mimic;
            public string trial;
            public Ware[] merchant;

            // 宝箱のマスで、宝箱から固有スキルの覚醒が出るか（出るときだけ true）。
            public bool awakening;
        }

        // 部屋で落ちた物（doc/features/drops.md）。category は item・accessory・weapon・armor・skill・material。
        [Serializable]
        public sealed class Drop
        {
            public string roomId;
            public string category;
            public string id;

            // 倍率（×1.37なら1.37）と品質（S・A・B・C）。アイテムは項目がなく、JsonUtilityでは0と空になる。
            public float multiplier;
            public string quality;
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
            public Drop[] drops;
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
