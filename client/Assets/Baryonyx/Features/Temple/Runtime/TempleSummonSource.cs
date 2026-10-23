using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Economy;
using Baryonyx.Inventory;
using Baryonyx.Library;
using Baryonyx.Networking;
using UnityEngine;

namespace Baryonyx.Temple
{
    /// <summary>What one summoning gave, what it spent, and the rune balance after it.</summary>
    public readonly struct TempleSummonPull
    {
        public TempleSummonPull(IReadOnlyList<SummonResult> results, int runes, long balanceAfter)
        {
            Results = results ?? Array.Empty<SummonResult>();
            Runes = runes;
            BalanceAfter = balanceAfter;
        }

        public IReadOnlyList<SummonResult> Results { get; }
        public int Runes { get; }
        public long BalanceAfter { get; }
    }

    /// <summary>Where the temple summons.</summary>
    public interface ITempleSummonSource
    {
        Task<TempleSummonPull> SummonAsync(int count, CancellationToken token);
    }

    /// <summary>The server's summoning API (server/src/features/summon/routes.ts).</summary>
    public sealed class TempleSummonApiClient
    {
        private readonly ServerApi server;

        public TempleSummonApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public Task<Result> SummonAsync(
            AccountSession session,
            string requestId,
            int count,
            CancellationToken token
        ) =>
            server.SendAsync<Result>(
                "/v1/summon",
                "POST",
                new SummonRequest { requestId = requestId, count = count },
                session.Token,
                token
            );

        [Serializable]
        private sealed class SummonRequest
        {
            public string requestId;
            public int count;
        }

        [Serializable]
        public sealed class Result
        {
            public Pull summon;
        }

        [Serializable]
        public sealed class Pull
        {
            public int count;
            public int runes;
            public long balanceAfter;
            public Drawn[] results;
        }

        // 装備でない物の倍率・品質は、JSONのnullが既定値（0・空）になる。
        [Serializable]
        public sealed class Drawn
        {
            public string category;
            public string id;
            public int stars;
            public string quality;
            public bool isNew;
            public int bond;
            public int tokens;
            public int shards;
        }
    }

    /// <summary>Summons on the game server with the shared session. The server decides what comes out.</summary>
    public sealed class TempleSummonServerSource : ITempleSummonSource
    {
        private readonly IAccountSessionRunner sessions;
        private readonly TempleSummonApiClient api;

        public TempleSummonServerSource(IAccountSessionRunner sessions, TempleSummonApiClient api)
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public async Task<TempleSummonPull> SummonAsync(int count, CancellationToken token)
        {
            // 応答が届かずに送り直しても、同じ要求IDで二重に引かない。
            string requestId = Guid.NewGuid().ToString("D");
            var result = await sessions.WithSessionAsync(
                session => api.SummonAsync(session, requestId, count, token),
                token
            );
            var pull =
                result?.summon ?? throw new InvalidOperationException("召喚の結果がありません。");
            return new TempleSummonPull(
                (pull.results ?? Array.Empty<TempleSummonApiClient.Drawn>())
                    .Select(ResultOf)
                    .ToArray(),
                pull.runes,
                pull.balanceAfter
            );
        }

        private static SummonResult ResultOf(TempleSummonApiClient.Drawn drawn)
        {
            var kind = drawn.category switch
            {
                "character" => LibraryKind.Character,
                "weapon" => LibraryKind.Weapon,
                "armor" => LibraryKind.Armor,
                "accessory" => LibraryKind.Accessory,
                _ => LibraryKind.Item,
            };
            ItemQuality? quality = Quality.TryParse(drawn.quality, out var parsed) ? parsed : null;
            return new SummonResult(
                new SummonEntry(kind, drawn.id, drawn.stars),
                quality,
                kind == LibraryKind.Character
                    ? new SummonBond(drawn.isNew, drawn.bond, drawn.shards, drawn.tokens)
                    : null
            );
        }
    }

    /// <summary>
    /// Summons without a server (the showcase, or no server URL): draws mock results at the
    /// screen's rates, spends the balance the screen shows (a mock balance before one is read),
    /// and keeps nothing.
    /// </summary>
    public sealed class TempleSummonMemorySource : ITempleSummonSource
    {
        public const long DefaultRunes = 30000;

        private readonly IReadOnlyList<SummonEntry> pool;
        private readonly System.Random random = new();
        private long runes;

        public TempleSummonMemorySource(IReadOnlyList<SummonEntry> pool, long runes = DefaultRunes)
        {
            this.pool = pool ?? Array.Empty<SummonEntry>();
            this.runes = runes;
        }

        public long Runes => RuneBalance.Known ?? runes;

        public Task<TempleSummonPull> SummonAsync(int count, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int price = TempleSummon.PriceOf(count);
            long balance = Runes;
            if (balance < price)
                throw new InvalidOperationException(TempleSummonSession.ShortMessage);
            runes = balance - price;
            return Task.FromResult(
                new TempleSummonPull(
                    TempleSummon.Roll(pool, count, random.NextDouble),
                    price,
                    runes
                )
            );
        }
    }

    /// <summary>
    /// The temple's summoning while the app runs: summons on <see cref="Source"/> (the game server,
    /// set by the app), or draws mock results without one. The results show only when the
    /// server answers, since what comes out is the server's to decide; the balance becomes the
    /// server's then.
    /// </summary>
    public static class TempleSummonSession
    {
        public const string ShortMessage = "ルーンが足りません";
        public const string FailedMessage = "召喚できませんでした";

        private static TempleSummonMemorySource local;

        /// <summary>The server to summon on. Without it, mock results are drawn.</summary>
        public static ITempleSummonSource Source { get; set; }

        /// <summary>The mock source, drawing from <paramref name="pool"/> the first time it is made.</summary>
        public static TempleSummonMemorySource LocalFor(IReadOnlyList<SummonEntry> pool) =>
            local ??= new TempleSummonMemorySource(pool);

        /// <summary>Summons <paramref name="count"/> times and sets the shown balance to what is left.</summary>
        public static async Task<TempleSummonPull> SummonAsync(
            int count,
            IReadOnlyList<SummonEntry> mockPool,
            CancellationToken token = default
        )
        {
            var source = Source;
            var pull = await (
                source != null
                    ? SaveQueue.Enqueue(t => source.SummonAsync(count, t))
                    : LocalFor(mockPool).SummonAsync(count, token)
            );
            RuneBalance.Set(pull.BalanceAfter);
            return pull;
        }

        // Play Modeに入るたびに、前の接続先と仮の残高を持ち越さない。接続先はアプリが設定し直す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Source = null;
            local = null;
        }
    }
}
