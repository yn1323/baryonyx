using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Networking;

namespace Baryonyx.StepBonus
{
    /// <summary>
    /// The server's ACT bonus API: the bonuses a player owns and the five slots
    /// (server/src/features/step-bonus/routes.ts).
    /// </summary>
    public sealed class StepBonusApiClient
    {
        private readonly ServerApi server;

        public StepBonusApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public Task<State> ReadAsync(AccountSession session, CancellationToken token) =>
            server.SendAsync<State>("/v1/step-bonus", "GET", null, session.Token, token);

        public Task<State> SetSlotAsync(
            AccountSession session,
            int slot,
            string bonusId,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                "/v1/step-bonus/slots/" + slot,
                "PUT",
                new SetSlotRequest { bonusId = bonusId },
                session.Token,
                token
            );

        [Serializable]
        private sealed class SetSlotRequest
        {
            public string bonusId;
        }

        [Serializable]
        public sealed class State
        {
            // 枠に入れたときだけ「set」「swap」「none」のどれかが入る。
            public string change;
            public Slot[] slots;
            public Holding[] holdings;

            // 冒険の途中で、枠を付け替えられない。
            public bool locked;
        }

        [Serializable]
        public sealed class Slot
        {
            public int slot;
            public string bonusId;
        }

        [Serializable]
        public sealed class Holding
        {
            public string bonusId;
            public string rank;
            public string acquiredAt;
            public string updatedAt;
        }
    }
}
