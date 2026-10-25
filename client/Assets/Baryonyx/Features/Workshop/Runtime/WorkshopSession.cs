using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Economy;
using Baryonyx.Networking;
using UnityEngine;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// The trading house while the app runs: buys, sells and makes on <see cref="Source"/> (the
    /// game server, set by the app), or on a mock balance without one. A purchase on the server shows at once
    /// (doc/rules/data-storage.md, optimistic updates): its price is taken off the shown balance
    /// until the server answers, and the write goes in the app's one line of saves.
    /// </summary>
    public static class WorkshopSession
    {
        // 商会の画面の「買う」の項目のキー。一覧の代わりに品物のパネルを開く。
        public const string BuyItemKey = "workshop-buy";

        // 「売る」「合成」の項目のキー。一覧の代わりに、それぞれのパネルを開く。
        public const string SellItemKey = "workshop-sell";
        public const string CraftItemKey = "workshop-craft";

        private static WorkshopMemorySource local;

        /// <summary>The server to buy from. Without it, the mock balance is spent.</summary>
        public static IWorkshopSource Source { get; set; }

        public static WorkshopMemorySource Local => local ??= new WorkshopMemorySource();

        /// <summary>
        /// Buys one of <paramref name="good"/>. With a server, the price is reserved from the
        /// shown balance at once and the balance becomes the server's when it answers; if the
        /// save fails, the price comes back and <paramref name="failedMessage"/> goes to the
        /// notice band (<see cref="SaveQueue.Failed"/>), wherever the player is by then.
        /// True once it was saved.
        /// </summary>
        public static async Task<bool> BuyAsync(WorkshopGood good, string failedMessage)
        {
            if (good == null)
                throw new ArgumentNullException(nameof(good));
            var source = Source;
            if (source == null)
                return await Spend(Local, good, failedMessage);
            RuneBalance.Reserve(good.Price);
            try
            {
                return await Spend(source, good, failedMessage, SaveQueue.Enqueue);
            }
            finally
            {
                RuneBalance.Release(good.Price);
            }
        }

        /// <summary>
        /// Makes <paramref name="recipe"/> from <paramref name="materials"/>, and sets the balance
        /// to the server's when it answers. The screen shows the made thing only then, since its
        /// quality is the server's to decide; a failure is thrown to the screen.
        /// </summary>
        public static async Task<WorkshopCrafted> CraftAsync(
            WorkshopRecipe recipe,
            IReadOnlyList<WorkshopOwned> materials,
            CancellationToken token = default
        )
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            var crafted = await Send((source, t) => source.CraftAsync(recipe, materials, t), token);
            RuneBalance.Set(crafted.BalanceAfter);
            return crafted;
        }

        /// <summary>
        /// Sells <paramref name="things"/>, and sets the balance to the server's when it answers.
        /// The screen takes the things off only then, so a failure (thrown to the screen) leaves
        /// them held.
        /// </summary>
        public static async Task<WorkshopSale> SellAsync(
            IReadOnlyList<WorkshopOwned> things,
            CancellationToken token = default
        )
        {
            var sale = await Send((source, t) => source.SellAsync(things, t), token);
            RuneBalance.Set(sale.BalanceAfter);
            return sale;
        }

        // サーバーへは保存の列に並べて送る。サーバーがなければ、手元の代わりでそのまま行う。
        private static Task<T> Send<T>(
            Func<IWorkshopSource, CancellationToken, Task<T>> send,
            CancellationToken token
        ) => Source is { } source ? SaveQueue.Enqueue(t => send(source, t)) : send(Local, token);

        private static async Task<bool> Spend(
            IWorkshopSource source,
            WorkshopGood good,
            string failedMessage,
            Func<Func<CancellationToken, Task<WorkshopPurchase>>, Task<WorkshopPurchase>> send =
                null
        )
        {
            try
            {
                Func<CancellationToken, Task<WorkshopPurchase>> buy = token =>
                    source.BuyAsync(good, token);
                var purchase = await (send != null ? send(buy) : buy(CancellationToken.None));
                RuneBalance.Set(purchase.BalanceAfter);
                return true;
            }
            // アプリを止めたときだけ取り消される。
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(failedMessage + "。" + exception.Message);
                SaveQueue.Report(failedMessage);
                return false;
            }
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
