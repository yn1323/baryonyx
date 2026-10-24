using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Economy;

namespace Baryonyx.Enhancement
{
    /// <summary>What one polish or awakening left: the multiplier or stage, the balance, and the materials held.</summary>
    public sealed class EnhancementOutcome
    {
        public EnhancementOutcome(
            float multiplier,
            int stage,
            long balanceAfter,
            IReadOnlyDictionary<string, int> materials
        )
        {
            Multiplier = multiplier;
            Stage = stage;
            BalanceAfter = balanceAfter;
            Materials = materials;
        }

        // 品質強化のあとの倍率。覚醒では0。
        public float Multiplier { get; }

        // 覚醒した段階。品質強化では-1。
        public int Stage { get; }
        public long BalanceAfter { get; }
        public IReadOnlyDictionary<string, int> Materials { get; }
    }

    /// <summary>Where the enhancement materials are held and the enhancements are made.</summary>
    public interface IEnhancementSource
    {
        Task<IReadOnlyDictionary<string, int>> LoadAsync(CancellationToken token);

        Task<EnhancementOutcome> PolishAsync(
            EnhancementTarget target,
            EnhancementMaterial stone,
            int count,
            CancellationToken token
        );

        Task<EnhancementOutcome> AwakenAsync(AwakeningOption option, CancellationToken token);
    }

    /// <summary>Enhances on the game server with the shared session. The server decides the result and the cost.</summary>
    public sealed class EnhancementServerSource : IEnhancementSource
    {
        private readonly IAccountSessionRunner sessions;
        private readonly EnhancementApiClient api;

        public EnhancementServerSource(IAccountSessionRunner sessions, EnhancementApiClient api)
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public async Task<IReadOnlyDictionary<string, int>> LoadAsync(CancellationToken token) =>
            CountsOf(
                (
                    await sessions.WithSessionAsync(session => api.ReadAsync(session, token), token)
                )?.materials
            );

        public async Task<EnhancementOutcome> PolishAsync(
            EnhancementTarget target,
            EnhancementMaterial stone,
            int count,
            CancellationToken token
        )
        {
            if (target == null || stone == null)
                throw new ArgumentNullException(target == null ? nameof(target) : nameof(stone));
            // 応答が届かずに送り直しても、同じ要求IDで二重に使わない。
            var request = new EnhancementApiClient.PolishRequest
            {
                requestId = Guid.NewGuid().ToString("D"),
                category = EnhancementTargets.KeyOf(target.Category),
                itemId = target.ItemId,
                characterId = target.CharacterId,
                index = Math.Max(0, target.Index),
                materialId = stone.Id,
                count = count,
            };
            var result = await sessions.WithSessionAsync(
                session => api.PolishAsync(session, request, token),
                token
            );
            var polished =
                result?.enhancement
                ?? throw new InvalidOperationException("品質強化の結果がありません。");
            return new EnhancementOutcome(
                polished.multiplier,
                -1,
                polished.balanceAfter,
                CountsOf(result.materials)
            );
        }

        public async Task<EnhancementOutcome> AwakenAsync(
            AwakeningOption option,
            CancellationToken token
        )
        {
            if (option == null)
                throw new ArgumentNullException(nameof(option));
            if (option.IsSkillCard)
            {
                var cardRequest = new EnhancementApiClient.SkillCardAwakenRequest
                {
                    requestId = Guid.NewGuid().ToString("D"),
                    cardId = option.OwnedCard,
                    branch = option.Branch,
                };
                var cardResult = await sessions.WithSessionAsync(
                    session => api.AwakenSkillCardAsync(session, cardRequest, token),
                    token
                );
                var awakened =
                    cardResult?.awakening
                    ?? throw new InvalidOperationException("覚醒の結果がありません。");
                return new EnhancementOutcome(
                    0f,
                    awakened.branch > 0 ? awakened.branch : option.Branch,
                    awakened.balanceAfter,
                    CountsOf(cardResult.materials)
                );
            }
            var request = new EnhancementApiClient.AwakenRequest
            {
                requestId = Guid.NewGuid().ToString("D"),
                characterId = option.CharacterId,
                index = option.Index,
                stage = option.Stage,
            };
            var result = await sessions.WithSessionAsync(
                session => api.AwakenAsync(session, request, token),
                token
            );
            var awakening =
                result?.awakening
                ?? throw new InvalidOperationException("覚醒の結果がありません。");
            return new EnhancementOutcome(
                0f,
                awakening.stage,
                awakening.balanceAfter,
                CountsOf(result.materials)
            );
        }

        private static IReadOnlyDictionary<string, int> CountsOf(
            IEnumerable<EnhancementApiClient.Material> materials
        ) =>
            (materials ?? Array.Empty<EnhancementApiClient.Material>())
                .Where(material => material != null && !string.IsNullOrEmpty(material.id))
                .GroupBy(material => material.id)
                .ToDictionary(group => group.Key, group => Math.Max(0, group.Last().count));
    }

    /// <summary>
    /// Enhances without a server (the showcase, or no server URL): a few mock materials, the
    /// balance the screen shows (a mock balance before one is read), and the same rules as the
    /// server, kept only while the app runs.
    /// </summary>
    public sealed class EnhancementMemorySource : IEnhancementSource
    {
        public const long DefaultRunes = 5000;

        public static readonly IReadOnlyDictionary<string, int> MockMaterials = new Dictionary<
            string,
            int
        >
        {
            [EnhancementMaterials.White] = 8,
            [EnhancementMaterials.Blue] = 4,
            [EnhancementMaterials.Purple] = 2,
            [EnhancementMaterials.Gold] = 1,
            [EnhancementMaterials.Shard] = 2,
        };

        private readonly Dictionary<string, int> materials;
        private readonly bool shownBalance;
        private long runes;

        // shownBalance が false なら、画面の残高を読まずに自分の残高だけを使う（サーバーの代役にするとき）。
        public EnhancementMemorySource(
            IReadOnlyDictionary<string, int> materials = null,
            long runes = DefaultRunes,
            bool shownBalance = true
        )
        {
            this.materials = new Dictionary<string, int>(materials ?? MockMaterials);
            this.runes = runes;
            this.shownBalance = shownBalance;
        }

        public long Runes => shownBalance ? RuneBalance.Known ?? runes : runes;

        public Task<IReadOnlyDictionary<string, int>> LoadAsync(CancellationToken token) =>
            Task.FromResult(Snapshot());

        public Task<EnhancementOutcome> PolishAsync(
            EnhancementTarget target,
            EnhancementMaterial stone,
            int count,
            CancellationToken token
        )
        {
            token.ThrowIfCancellationRequested();
            if (target == null || stone == null || !stone.IsPolish || count < 1)
                return Fail("強化できない組み合わせです。");
            if (target.AtMax)
                return Fail("上限に届いています。");
            if (
                count
                > EnhancementMaterials.UsefulCount(target.Multiplier, stone, target.MaxMultiplier)
            )
                return Fail("上限を超える数です。");
            int cost = stone.Runes * count;
            if (!Spend(stone.Id, count, cost, out string reason))
                return Fail(reason);
            float after = EnhancementMaterials.Polished(
                target.Multiplier,
                stone,
                count,
                target.MaxMultiplier
            );
            return Task.FromResult(new EnhancementOutcome(after, -1, runes, Snapshot()));
        }

        public Task<EnhancementOutcome> AwakenAsync(AwakeningOption option, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (option == null)
                return Fail("覚醒できない段階です。");
            if (
                !Spend(
                    EnhancementMaterials.Shard,
                    EnhancementMaterials.ShardsPerStage,
                    option.Runes,
                    out string reason
                )
            )
                return Fail(reason);
            return Task.FromResult(new EnhancementOutcome(0f, option.Stage, runes, Snapshot()));
        }

        private bool Spend(string materialId, int count, int cost, out string reason)
        {
            materials.TryGetValue(materialId, out int held);
            long balance = Runes;
            reason =
                held < count ? "素材が足りません。"
                : balance < cost ? "ルーンが足りません。"
                : null;
            if (reason != null)
                return false;
            materials[materialId] = held - count;
            runes = balance - cost;
            return true;
        }

        private IReadOnlyDictionary<string, int> Snapshot() =>
            new Dictionary<string, int>(materials);

        private static Task<EnhancementOutcome> Fail(string message) =>
            Task.FromException<EnhancementOutcome>(new InvalidOperationException(message));
    }
}
