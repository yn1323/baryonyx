using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Inventory;

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

        /// <summary>
        /// The slots after the bonus goes in the slot, by the server's rules, before it is saved:
        /// a bonus set in another slot swaps with the slot's bonus.
        /// </summary>
        public StepBonusState WithSlot(int slot, string bonusId)
        {
            if (slot < 0 || string.IsNullOrEmpty(bonusId))
                return this;
            var slots = new string[Math.Max(Slots.Count, slot + 1)];
            for (int i = 0; i < Slots.Count; i++)
                slots[i] = Slots[i];
            int other = Array.IndexOf(slots, bonusId);
            if (other >= 0)
                slots[other] = slots[slot];
            slots[slot] = bonusId;
            return new StepBonusState(Owned, slots, Locked);
        }
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

        // 倍率が×1.00より小さい（届いていない）ボーナスは外し、枠は番号の位置へ並べる。
        // 品質は、サーバーと同じ決まりで、ボーナスの定義の上限から求める。
        public static StepBonusState ToState(StepBonusApiClient.State state)
        {
            var owned = (state?.holdings ?? Array.Empty<StepBonusApiClient.Holding>())
                .Where(holding =>
                    holding != null
                    && !string.IsNullOrEmpty(holding.bonusId)
                    && holding.multiplier >= Quality.MinMultiplier
                )
                .Select(holding => new StepBonusRoll
                {
                    Id = holding.bonusId,
                    Multiplier = holding.multiplier,
                })
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
