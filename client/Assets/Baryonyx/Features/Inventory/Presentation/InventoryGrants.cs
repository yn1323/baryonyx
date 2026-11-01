using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Inventory
{
    /// <summary>
    /// Adds things to the player's inventory without conditions, for the inventory lab
    /// (<see cref="InventoryLab"/>): on the server's debugging API (local and Dev only).
    /// </summary>
    public interface IInventoryGrants
    {
        /// <summary>
        /// Adds <paramref name="count"/> of <paramref name="id"/>; a skill or piece of equipment
        /// gets a multiplier from <paramref name="quality"/>'s range, an item or material none.
        /// </summary>
        Task<InventoryGranted> GrantAsync(
            string category,
            string id,
            ItemQuality quality,
            int count,
            CancellationToken token
        );
    }

    /// <summary>What was added: how many, and each skill's or piece of equipment's multiplier and quality.</summary>
    public sealed class InventoryGranted
    {
        public int Count;
        public IReadOnlyList<(float Multiplier, ItemQuality Quality)> Held = Array.Empty<(
            float,
            ItemQuality
        )>();
    }
}
