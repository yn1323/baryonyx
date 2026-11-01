using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Inventory;
using Baryonyx.Networking;

namespace Baryonyx.App
{
    /// <summary>
    /// The inventory lab's additions on the server (server/src/features/debug, open on local and
    /// Dev only): the server picks each multiplier in the quality's range and saves the things.
    /// </summary>
    public sealed class DebugGrants : IInventoryGrants
    {
        private readonly IAccountSessionRunner sessions;
        private readonly ServerApi server;

        public DebugGrants(IAccountSessionRunner sessions, ServerApi server)
        {
            this.sessions = sessions;
            this.server = server;
        }

        public async Task<InventoryGranted> GrantAsync(
            string category,
            string id,
            ItemQuality quality,
            int count,
            CancellationToken token
        )
        {
            var result = await sessions.WithSessionAsync(
                session =>
                    server.SendAsync<Result>(
                        "/v1/debug/grant",
                        "POST",
                        new Request
                        {
                            category = category,
                            id = id,
                            quality = Quality.Letter(quality),
                            count = count,
                        },
                        session.Token,
                        token
                    ),
                token
            );
            if (result == null)
                throw new InvalidOperationException("持ち物の結果がありません。");
            return new InventoryGranted
            {
                Count = result.count,
                Held = (result.granted ?? Array.Empty<Granted>())
                    .Select(each =>
                        (
                            each.multiplier,
                            Quality.TryParse(each.quality, out var letter) ? letter : ItemQuality.C
                        )
                    )
                    .ToArray(),
            };
        }

        [Serializable]
        private sealed class Request
        {
            public string category;
            public string id;
            public string quality;
            public int count;
        }

        [Serializable]
        private sealed class Result
        {
            public int count;
            public Granted[] granted;
        }

        [Serializable]
        private sealed class Granted
        {
            public float multiplier;
            public string quality;
        }
    }
}
