using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Library;
using Baryonyx.Networking;

namespace Baryonyx.Workshop
{
    /// <summary>The server's trading house API (server/src/features/workshop/routes.ts).</summary>
    public sealed class WorkshopApiClient
    {
        private readonly ServerApi server;

        public WorkshopApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public Task<Result> BuyAsync(
            AccountSession session,
            string requestId,
            string category,
            string id,
            CancellationToken token
        ) =>
            server.SendAsync<Result>(
                "/v1/workshop/buy",
                "POST",
                new BuyRequest
                {
                    requestId = requestId,
                    category = category,
                    id = id,
                },
                session.Token,
                token
            );

        // 装備・スキルは持ち物の行のIDを、アイテムは種類ごとの数を送る。
        public Task<CraftResult> CraftAsync(
            AccountSession session,
            string requestId,
            string category,
            string id,
            IReadOnlyList<WorkshopOwned> materials,
            CancellationToken token
        )
        {
            var things = materials ?? Array.Empty<WorkshopOwned>();
            bool items = category == "item";
            return server.SendAsync<CraftResult>(
                "/v1/workshop/craft",
                "POST",
                new CraftRequest
                {
                    requestId = requestId,
                    category = category,
                    id = id,
                    materialIds = items ? null : things.Select(thing => thing.RowId).ToArray(),
                    items = items
                        ? things
                            .GroupBy(thing => thing.KindId)
                            .Select(group => new CraftItem
                            {
                                id = group.Key,
                                count = group.Sum(thing => thing.Count),
                            })
                            .ToArray()
                        : null,
                },
                session.Token,
                token
            );
        }

        [Serializable]
        private sealed class CraftRequest
        {
            public string requestId;
            public string category;
            public string id;
            public string[] materialIds;
            public CraftItem[] items;
        }

        [Serializable]
        private sealed class CraftItem
        {
            public string id;
            public int count;
        }

        [Serializable]
        public sealed class CraftResult
        {
            public Craft craft;
        }

        [Serializable]
        public sealed class Craft
        {
            public string category;
            public string id;
            public string itemId;
            public float multiplier;
            public string quality;
            public int runes;
            public long balanceAfter;
        }

        [Serializable]
        private sealed class BuyRequest
        {
            public string requestId;
            public string category;
            public string id;
        }

        [Serializable]
        public sealed class Result
        {
            public Purchase purchase;
        }

        [Serializable]
        public sealed class Purchase
        {
            public string category;
            public string id;
            public string itemId;
            public float multiplier;
            public string quality;
            public int runes;
            public long balanceAfter;
        }
    }
}
