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

        // アイテムの材料はレシピで決まるため送らず、特別な材料（装備・スキル）の持ち物の行のIDだけを送る。
        public Task<CraftResult> CraftAsync(
            AccountSession session,
            string requestId,
            string category,
            string id,
            IReadOnlyList<WorkshopOwned> special,
            CancellationToken token
        )
        {
            var things = special ?? Array.Empty<WorkshopOwned>();
            return server.SendAsync<CraftResult>(
                "/v1/workshop/craft",
                "POST",
                new CraftRequest
                {
                    requestId = requestId,
                    category = category,
                    id = id,
                    materialIds = things.Select(thing => thing.RowId).ToArray(),
                },
                session.Token,
                token
            );
        }

        // スキル・装備は持ち物の行のIDを、アイテムは種類ごとの持っている数を送る。
        public Task<SellResult> SellAsync(
            AccountSession session,
            string requestId,
            IReadOnlyList<WorkshopOwned> things,
            CancellationToken token
        )
        {
            var sold = things ?? Array.Empty<WorkshopOwned>();
            string[] RowsOf(params LibraryKind[] kinds) =>
                sold.Where(thing => kinds.Contains(thing.Kind))
                    .Select(thing => thing.RowId)
                    .ToArray();
            return server.SendAsync<SellResult>(
                "/v1/workshop/sell",
                "POST",
                new SellRequest
                {
                    requestId = requestId,
                    cardIds = RowsOf(LibraryKind.Skill),
                    equipmentIds = RowsOf(
                        LibraryKind.Weapon,
                        LibraryKind.Armor,
                        LibraryKind.Accessory
                    ),
                    items = sold.Where(thing => thing.Kind == LibraryKind.Item)
                        .Select(thing => new CraftItem { id = thing.KindId, count = thing.Count })
                        .ToArray(),
                },
                session.Token,
                token
            );
        }

        [Serializable]
        private sealed class SellRequest
        {
            public string requestId;
            public string[] cardIds;
            public string[] equipmentIds;
            public CraftItem[] items;
        }

        [Serializable]
        public sealed class SellResult
        {
            public Sale sale;
        }

        [Serializable]
        public sealed class Sale
        {
            public int runes;
            public long balanceAfter;
        }

        [Serializable]
        private sealed class CraftRequest
        {
            public string requestId;
            public string category;
            public string id;
            public string[] materialIds;
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
