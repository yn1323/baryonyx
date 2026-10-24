using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Economy;
using Baryonyx.Networking;

namespace Baryonyx.App
{
    /// <summary>
    /// The debugging menu's "ルーン+10000": adds runes on the server (server/src/features/debug,
    /// open on local and Dev only), or to the shown balance without a server.
    /// </summary>
    public sealed class DebugRunes
    {
        public const long Amount = 10000;

        /// <summary>Without a server: adds to the shown balance only.</summary>
        public static readonly DebugRunes Offline = new(null, null);

        private readonly IAccountSessionRunner sessions;
        private readonly ServerApi server;

        public DebugRunes(IAccountSessionRunner sessions, ServerApi server)
        {
            this.sessions = sessions;
            this.server = server;
        }

        /// <summary>Adds <see cref="Amount"/> runes and returns the balance after it.</summary>
        public async Task<long> AddAsync(CancellationToken token)
        {
            if (sessions == null || server == null)
                return (RuneBalance.Known ?? 0) + Amount;
            var result = await sessions.WithSessionAsync(
                session =>
                    server.SendAsync<Result>(
                        "/v1/debug/runes",
                        "POST",
                        new Request(),
                        session.Token,
                        token
                    ),
                token
            );
            return result?.balanceAfter
                ?? throw new InvalidOperationException("ルーンの結果がありません。");
        }

        [Serializable]
        private sealed class Request { }

        [Serializable]
        private sealed class Result
        {
            public long balanceAfter;
        }
    }
}
