using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Economy;

namespace Baryonyx.Workshop
{
    /// <summary>What one purchase spent, and the rune balance after it.</summary>
    public readonly struct WorkshopPurchase
    {
        public WorkshopPurchase(int runes, long balanceAfter)
        {
            Runes = runes;
            BalanceAfter = balanceAfter;
        }

        public int Runes { get; }
        public long BalanceAfter { get; }
    }

    /// <summary>What one craft made, what it spent, and the rune balance after it.</summary>
    public readonly struct WorkshopCrafted
    {
        public WorkshopCrafted(string itemId, float multiplier, int runes, long balanceAfter)
        {
            ItemId = itemId ?? "";
            Multiplier = multiplier;
            Runes = runes;
            BalanceAfter = balanceAfter;
        }

        // 出来た持ち物の行のID（アイテムは種類のID）。
        public string ItemId { get; }
        public float Multiplier { get; }
        public int Runes { get; }
        public long BalanceAfter { get; }
    }

    /// <summary>What one sale earned, and the rune balance after it.</summary>
    public readonly struct WorkshopSale
    {
        public WorkshopSale(int runes, long balanceAfter)
        {
            Runes = runes;
            BalanceAfter = balanceAfter;
        }

        public int Runes { get; }
        public long BalanceAfter { get; }
    }

    /// <summary>Where the trading house sells, buys and makes things.</summary>
    public interface IWorkshopSource
    {
        Task<WorkshopSale> SellAsync(IReadOnlyList<WorkshopOwned> things, CancellationToken token);

        Task<WorkshopPurchase> BuyAsync(WorkshopGood good, CancellationToken token);

        Task<WorkshopCrafted> CraftAsync(
            WorkshopRecipe recipe,
            IReadOnlyList<WorkshopOwned> materials,
            CancellationToken token
        );
    }

    /// <summary>Buys, sells and makes on the game server with the shared session. The server decides the prices.</summary>
    public sealed class WorkshopServerSource : IWorkshopSource
    {
        private readonly IAccountSessionRunner sessions;
        private readonly WorkshopApiClient api;

        public WorkshopServerSource(IAccountSessionRunner sessions, WorkshopApiClient api)
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public async Task<WorkshopPurchase> BuyAsync(WorkshopGood good, CancellationToken token)
        {
            if (good == null)
                throw new ArgumentNullException(nameof(good));
            // 応答が届かずに送り直しても、同じ要求IDで二重に買わない。
            string requestId = Guid.NewGuid().ToString("D");
            var result = await sessions.WithSessionAsync(
                session =>
                    api.BuyAsync(
                        session,
                        requestId,
                        WorkshopGoods.KeyOf(good.Kind),
                        good.Id,
                        token
                    ),
                token
            );
            var purchase =
                result?.purchase ?? throw new InvalidOperationException("購入の結果がありません。");
            return new WorkshopPurchase(purchase.runes, purchase.balanceAfter);
        }

        public async Task<WorkshopSale> SellAsync(
            IReadOnlyList<WorkshopOwned> things,
            CancellationToken token
        )
        {
            string requestId = Guid.NewGuid().ToString("D");
            var result = await sessions.WithSessionAsync(
                session => api.SellAsync(session, requestId, things, token),
                token
            );
            var sale =
                result?.sale ?? throw new InvalidOperationException("売却の結果がありません。");
            return new WorkshopSale(sale.runes, sale.balanceAfter);
        }

        public async Task<WorkshopCrafted> CraftAsync(
            WorkshopRecipe recipe,
            IReadOnlyList<WorkshopOwned> materials,
            CancellationToken token
        )
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            string requestId = Guid.NewGuid().ToString("D");
            var result = await sessions.WithSessionAsync(
                session =>
                    api.CraftAsync(
                        session,
                        requestId,
                        WorkshopCraft.KeyOf(recipe.Kind),
                        recipe.Id,
                        materials,
                        token
                    ),
                token
            );
            var craft =
                result?.craft ?? throw new InvalidOperationException("合成の結果がありません。");
            return new WorkshopCrafted(
                craft.itemId,
                craft.multiplier,
                craft.runes,
                craft.balanceAfter
            );
        }
    }

    /// <summary>
    /// Buys, sells and makes without a server (the showcase, or no server URL): spends and earns
    /// the balance the screen shows (a mock balance before one is read), and changes nothing in
    /// the belongings.
    /// </summary>
    public sealed class WorkshopMemorySource : IWorkshopSource
    {
        public const long DefaultRunes = 5000;

        private long runes;

        public WorkshopMemorySource(long runes = DefaultRunes) => this.runes = runes;

        public long Runes => RuneBalance.Known ?? runes;

        public Task<WorkshopPurchase> BuyAsync(WorkshopGood good, CancellationToken token)
        {
            if (good == null)
                throw new ArgumentNullException(nameof(good));
            token.ThrowIfCancellationRequested();
            long balance = Runes;
            if (balance < good.Price)
                return Task.FromException<WorkshopPurchase>(
                    new InvalidOperationException("ルーンが足りません。")
                );
            runes = balance - good.Price;
            return Task.FromResult(new WorkshopPurchase(good.Price, runes));
        }

        // 持ち物は変えず、売値の合計を足す。
        public Task<WorkshopSale> SellAsync(
            IReadOnlyList<WorkshopOwned> things,
            CancellationToken token
        )
        {
            token.ThrowIfCancellationRequested();
            int earned = (things ?? Array.Empty<WorkshopOwned>()).Sum(WorkshopSellView.PriceOf);
            runes = Runes + earned;
            return Task.FromResult(new WorkshopSale(earned, runes));
        }

        // 持ち物は変えず、出来た物の倍率を材料から計算して返す。
        public Task<WorkshopCrafted> CraftAsync(
            WorkshopRecipe recipe,
            IReadOnlyList<WorkshopOwned> materials,
            CancellationToken token
        )
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            token.ThrowIfCancellationRequested();
            long balance = Runes;
            if (balance < recipe.Runes)
                return Task.FromException<WorkshopCrafted>(
                    new InvalidOperationException("ルーンが足りません。")
                );
            if ((materials?.Count ?? 0) != WorkshopCraft.MaterialCount)
                return Task.FromException<WorkshopCrafted>(
                    new InvalidOperationException("材料が足りません。")
                );
            runes = balance - recipe.Runes;
            string itemId =
                recipe.Kind == Library.LibraryKind.Item
                    ? recipe.Id
                    : "local-" + Guid.NewGuid().ToString("N");
            return Task.FromResult(
                new WorkshopCrafted(
                    itemId,
                    WorkshopCraft.Multiplier(recipe, materials),
                    recipe.Runes,
                    runes
                )
            );
        }
    }
}
