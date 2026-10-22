using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>Where the party's HP in the adventure is kept.</summary>
    public interface IAdventureVitalsStore
    {
        string Read();
        void Write(string value);
    }

    /// <summary>
    /// Keeps it on the device (doc/features/stage-progression.md, 冒険中の状態の保存先): losing it
    /// only brings the party back at full HP.
    /// </summary>
    public sealed class PlayerPrefsAdventureVitalsStore : IAdventureVitalsStore
    {
        public const string Key = "Adventure.Vitals";

        public string Read() => PlayerPrefs.GetString(Key, "");

        public void Write(string value)
        {
            PlayerPrefs.SetString(Key, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Keeps it while the app runs (tests).</summary>
    public sealed class MemoryAdventureVitalsStore : IAdventureVitalsStore
    {
        private string value = "";

        public string Read() => value;

        public void Write(string value) => this.value = value ?? "";
    }

    /// <summary>
    /// The HP the party carries from one room to the next (doc/features/stage-progression.md,
    /// HPの持ち越し), as a share of each member's maximum HP, by party slot: 1 is full and 0 is
    /// fallen. It belongs to one adventure; another adventure starts at full HP. A battle won
    /// keeps the HP it ended with, and a rest heals each member by a share of the maximum HP,
    /// standing a fallen one up again.
    /// </summary>
    public static class AdventureVitals
    {
        public const int Slots = 4;

        // 休憩で回復する、最大HPに対する割合（doc/features/stage-progression.md のマスの中身）。
        public const float RestHeal = 0.4f;

        private static IAdventureVitalsStore store = new PlayerPrefsAdventureVitalsStore();

        /// <summary>Uses another store (tests). Null goes back to the device.</summary>
        public static void UseStore(IAdventureVitalsStore value) =>
            store = value ?? new PlayerPrefsAdventureVitalsStore();

        /// <summary>The share of maximum HP each slot carries into the adventure's next battle.</summary>
        public static float[] Of(string runId)
        {
            var full = Enumerable.Repeat(1f, Slots).ToArray();
            if (string.IsNullOrEmpty(runId))
                return full;
            var parts = (store.Read() ?? "").Split('|');
            if (parts.Length != 2 || parts[0] != runId)
                return full;
            var shares = parts[1].Split(',');
            for (int slot = 0; slot < Slots && slot < shares.Length; slot++)
                if (
                    float.TryParse(
                        shares[slot],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float share
                    )
                )
                    full[slot] = Mathf.Clamp01(share);
            // 全員が倒れたまま次の戦闘に入ることはない（全滅は復活か冒険の終わり）。
            return full.All(share => share <= 0f) ? Enumerable.Repeat(1f, Slots).ToArray() : full;
        }

        /// <summary>Keeps the share of maximum HP each slot ended the battle with.</summary>
        public static void Keep(string runId, IReadOnlyList<float> shares)
        {
            if (string.IsNullOrEmpty(runId) || shares == null)
                return;
            var kept = new string[Slots];
            for (int slot = 0; slot < Slots; slot++)
            {
                float share = slot < shares.Count ? Mathf.Clamp01(shares[slot]) : 1f;
                kept[slot] = share.ToString("0.####", CultureInfo.InvariantCulture);
            }
            store.Write(runId + "|" + string.Join(",", kept));
        }

        /// <summary>Heals each slot by the rest's share of its maximum HP, a fallen one too.</summary>
        public static float[] Rest(string runId)
        {
            var shares = Of(runId).Select(share => Mathf.Min(1f, share + RestHeal)).ToArray();
            Keep(runId, shares);
            return shares;
        }
    }
}
