using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Networking;

namespace Baryonyx.Enhancement
{
    /// <summary>The server's enhancement API (server/src/features/enhancement/routes.ts).</summary>
    public sealed class EnhancementApiClient
    {
        private readonly ServerApi server;

        public EnhancementApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public Task<State> ReadAsync(AccountSession session, CancellationToken token) =>
            server.SendAsync<State>("/v1/enhancement", "GET", null, session.Token, token);

        public Task<PolishResult> PolishAsync(
            AccountSession session,
            PolishRequest request,
            CancellationToken token
        ) =>
            server.SendAsync<PolishResult>(
                "/v1/enhancement/polish",
                "POST",
                request,
                session.Token,
                token
            );

        public Task<AwakenResult> AwakenAsync(
            AccountSession session,
            AwakenRequest request,
            CancellationToken token
        ) =>
            server.SendAsync<AwakenResult>(
                "/v1/enhancement/awaken",
                "POST",
                request,
                session.Token,
                token
            );

        /// <summary>Makes stones of a colour from three each of the colour below (doc/features/enhancement.md).</summary>
        public Task<CombineResult> CombineAsync(
            AccountSession session,
            CombineRequest request,
            CancellationToken token
        ) =>
            server.SendAsync<CombineResult>(
                "/v1/enhancement/combine",
                "POST",
                request,
                session.Token,
                token
            );

        /// <summary>Awakens an owned skill card into a branch (doc/features/skill-awakening.md).</summary>
        public Task<AwakenResult> AwakenSkillCardAsync(
            AccountSession session,
            SkillCardAwakenRequest request,
            CancellationToken token
        ) =>
            server.SendAsync<AwakenResult>(
                "/v1/enhancement/awaken",
                "POST",
                request,
                session.Token,
                token
            );

        [Serializable]
        public sealed class Material
        {
            public string id;
            public int count;
        }

        [Serializable]
        public sealed class State
        {
            public Material[] materials;
        }

        // 品質強化の要求。スキル・装備は itemId、固有スキルは characterId と index で選ぶ。
        // JsonUtilityは空の項目も送るため、サーバーは分類で使う項目だけを読む。
        [Serializable]
        public sealed class PolishRequest
        {
            public string requestId;
            public string category;
            public string itemId;
            public string characterId;
            public int index;
            public string materialId;
            public int count;
        }

        [Serializable]
        public sealed class AwakenRequest
        {
            public string requestId;
            public string characterId;
            public int index;
            public int stage;
        }

        // 持っているスキルの1枚の覚醒の要求。固有スキルの項目は送らない。
        [Serializable]
        public sealed class SkillCardAwakenRequest
        {
            public string requestId;
            public string cardId;
            public int branch;
        }

        // 磨き石の合成の要求。作る色（青・紫・金）と作る数。
        [Serializable]
        public sealed class CombineRequest
        {
            public string requestId;
            public string materialId;
            public int count;
        }

        [Serializable]
        public sealed class Combined
        {
            public string materialId;
            public string from;
            public int count;
            public int runes;
            public long balanceAfter;
        }

        [Serializable]
        public sealed class CombineResult
        {
            public Combined combine;
            public Material[] materials;
        }

        [Serializable]
        public sealed class Polished
        {
            public string category;
            public string target;
            public float multiplier;
            public string quality;
            public int runes;
            public long balanceAfter;
        }

        [Serializable]
        public sealed class PolishResult
        {
            public Polished enhancement;
            public Material[] materials;
        }

        [Serializable]
        public sealed class Awakening
        {
            public string characterId;
            public int index;
            public int stage;

            // 持っているスキルの1枚の覚醒では、カードのIDと覚醒した枝（1か2）が入る。
            public string cardId;
            public int branch;
            public int runes;
            public long balanceAfter;
        }

        [Serializable]
        public sealed class AwakenResult
        {
            public Awakening awakening;
            public Material[] materials;
        }
    }
}
