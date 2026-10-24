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
    /// standing a fallen one up again. It also keeps what the rests honed in this adventure
    /// (休憩の鍛える): how many times each slot was honed, and the cards honed.
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
            var parts = Parts(runId);
            if (parts == null)
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
            Write(runId, string.Join(",", kept), HonedSlots(runId), HonedCards(runId));
        }

        /// <summary>Heals each slot by the rest's share of its maximum HP, a fallen one too.</summary>
        public static float[] Rest(string runId)
        {
            var shares = Of(runId).Select(share => Mathf.Min(1f, share + RestHeal)).ToArray();
            Keep(runId, shares);
            return shares;
        }

        /// <summary>How many times each slot was honed in the adventure.</summary>
        public static int[] HonedSlots(string runId)
        {
            var counts = new int[Slots];
            var parts = Parts(runId);
            if (parts == null || parts.Length < 3)
                return counts;
            var written = parts[2].Split(',');
            for (int slot = 0; slot < Slots && slot < written.Length; slot++)
                if (int.TryParse(written[slot], out int count))
                    counts[slot] = Math.Max(0, count);
            return counts;
        }

        /// <summary>The cards honed in the adventure, as <see cref="CardKey"/>.</summary>
        public static IReadOnlyCollection<string> HonedCards(string runId)
        {
            var parts = Parts(runId);
            return parts == null || parts.Length < 4 || parts[3] == ""
                ? Array.Empty<string>()
                : parts[3].Split(',').Distinct().ToArray();
        }

        /// <summary>The key of a slot's card skill among the cards honed.</summary>
        public static string CardKey(int slot, string skillId) => $"{slot}:{skillId}";

        /// <summary>Hones a slot once more (its attacks grow by <see cref="AdventureParty.HoneStat"/> each).</summary>
        public static void HoneSlot(string runId, int slot)
        {
            if (string.IsNullOrEmpty(runId) || slot < 0 || slot >= Slots)
                return;
            var counts = HonedSlots(runId);
            counts[slot]++;
            Write(runId, SharesText(runId), counts, HonedCards(runId));
        }

        /// <summary>Hones one of a slot's card skills (once a card).</summary>
        public static void HoneCard(string runId, int slot, string skillId)
        {
            if (string.IsNullOrEmpty(runId) || string.IsNullOrEmpty(skillId))
                return;
            var cards = HonedCards(runId).Append(CardKey(slot, skillId)).Distinct().ToArray();
            Write(runId, SharesText(runId), HonedSlots(runId), cards);
        }

        // 保存した文字列（冒険のID|HPの割合|鍛えた回数|鍛えたカード）を区切る。別の冒険の物ならnull。
        private static string[] Parts(string runId)
        {
            if (string.IsNullOrEmpty(runId))
                return null;
            var parts = (store.Read() ?? "").Split('|');
            return parts.Length >= 2 && parts[0] == runId ? parts : null;
        }

        private static string SharesText(string runId) =>
            Parts(runId) is { } parts ? parts[1] : string.Join(",", Enumerable.Repeat("1", Slots));

        private static void Write(
            string runId,
            string shares,
            IReadOnlyList<int> slots,
            IEnumerable<string> cards
        ) =>
            store.Write(
                runId + "|" + shares + "|" + string.Join(",", slots) + "|" + string.Join(",", cards)
            );
    }
}
