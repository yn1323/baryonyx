using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;

namespace Baryonyx.StepBonus
{
    /// <summary>A player's bonuses and slots as the server keeps them.</summary>
    public sealed class StepBonusState
    {
        public StepBonusState(
            IReadOnlyList<StepBonusRoll> owned,
            IReadOnlyList<string> slots,
            bool locked = false
        )
        {
            Owned = owned ?? Array.Empty<StepBonusRoll>();
            Slots = slots ?? Array.Empty<string>();
            Locked = locked;
        }

        public IReadOnlyList<StepBonusRoll> Owned { get; }

        // 枠ごとのボーナスのID。空いている枠はnull。
        public IReadOnlyList<string> Slots { get; }

        // 冒険の途中で、枠を付け替えられない（doc/features/step-bonus.md の枠の付け替え）。
        public bool Locked { get; }
    }

    /// <summary>Where the bonus settings read and save a player's bonuses.</summary>
    public interface IStepBonusSource
    {
        Task<StepBonusState> LoadAsync(CancellationToken token);

        Task<StepBonusState> SetSlotAsync(int slot, string bonusId, CancellationToken token);
    }

    /// <summary>
    /// Reads and saves the bonuses on the game server with the shared session, so each player
    /// has their own bonuses and slots.
    /// </summary>
    public sealed class StepBonusServerSource : IStepBonusSource
    {
        private readonly IAccountSessionRunner sessions;
        private readonly StepBonusApiClient api;

        public StepBonusServerSource(IAccountSessionRunner sessions, StepBonusApiClient api)
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public async Task<StepBonusState> LoadAsync(CancellationToken token) =>
            ToState(
                await sessions.WithSessionAsync(session => api.ReadAsync(session, token), token)
            );

        public async Task<StepBonusState> SetSlotAsync(
            int slot,
            string bonusId,
            CancellationToken token
        ) =>
            ToState(
                await sessions.WithSessionAsync(
                    session => api.SetSlotAsync(session, slot, bonusId, token),
                    token
                )
            );

        // 知らないランクのボーナスは外し、枠は番号の位置へ並べる。
        public static StepBonusState ToState(StepBonusApiClient.State state)
        {
            var owned = (state?.holdings ?? Array.Empty<StepBonusApiClient.Holding>())
                .Where(holding => holding != null && !string.IsNullOrEmpty(holding.bonusId))
                .Select(holding =>
                    holding.rank != null && Enum.IsDefined(typeof(StepBonusRank), holding.rank)
                        ? new StepBonusRoll
                        {
                            Id = holding.bonusId,
                            Rank = (StepBonusRank)Enum.Parse(typeof(StepBonusRank), holding.rank),
                        }
                        : null
                )
                .Where(roll => roll != null)
                .ToArray();
            var slots = state?.slots ?? Array.Empty<StepBonusApiClient.Slot>();
            int count = slots.Length == 0 ? 0 : slots.Max(slot => slot?.slot ?? -1) + 1;
            var ids = new string[Math.Max(0, count)];
            foreach (var slot in slots)
                if (slot != null && slot.slot >= 0 && slot.slot < ids.Length)
                    ids[slot.slot] = string.IsNullOrEmpty(slot.bonusId) ? null : slot.bonusId;
            return new StepBonusState(owned, ids, state?.locked ?? false);
        }
    }
}
