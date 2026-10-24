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
    /// (休憩の鍛える): how many times each slot was honed, and the cards honed; and the boons
    /// chosen after won battles (戦闘後の強化), with the room whose boon is not chosen yet.
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
            var record = Record.Of(runId);
            record.Shares = string.Join(",", kept);
            record.Write();
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
            var record = Record.Of(runId);
            var counts = HonedSlots(runId);
            counts[slot]++;
            record.Honed = string.Join(",", counts);
            record.Write();
        }

        /// <summary>Hones one of a slot's card skills (once a card).</summary>
        public static void HoneCard(string runId, int slot, string skillId)
        {
            if (string.IsNullOrEmpty(runId) || string.IsNullOrEmpty(skillId))
                return;
            var record = Record.Of(runId);
            record.Cards = string.Join(
                ",",
                HonedCards(runId).Append(CardKey(slot, skillId)).Distinct()
            );
            record.Write();
        }

        // --- Boons after a won battle (戦闘後の強化) ------------------------------------------

        /// <summary>The boons chosen in the adventure, in the order chosen.</summary>
        public static IReadOnlyList<AdventureBoon> Boons(string runId)
        {
            var parts = Parts(runId);
            if (parts == null || parts.Length < 5 || parts[4] == "")
                return Array.Empty<AdventureBoon>();
            return parts[4]
                .Split(';')
                .Select(AdventureBoon.Decode)
                .Where(boon => boon != null)
                .Select(boon => boon.Value)
                .ToArray();
        }

        /// <summary>How many offers in a row came with no rainbow boon.</summary>
        public static int BoonPity(string runId)
        {
            var parts = Parts(runId);
            return parts != null && parts.Length >= 6 && int.TryParse(parts[5], out int pity)
                ? Math.Max(0, pity)
                : 0;
        }

        /// <summary>
        /// The room whose won battle still waits for a boon to be chosen, with how hard the battle
        /// was, or null.
        /// </summary>
        public static (string RoomId, AdventureBoonBattle Battle)? PendingBoon(string runId)
        {
            var parts = Parts(runId);
            if (parts == null || parts.Length < 7 || parts[6] == "")
                return null;
            var pending = parts[6].Split('/');
            return
                pending.Length == 2
                && int.TryParse(pending[1], out int battle)
                && battle > (int)AdventureBoonBattle.None
                && battle <= (int)AdventureBoonBattle.TrialMet
                ? (pending[0], (AdventureBoonBattle)battle)
                : null;
        }

        /// <summary>
        /// Keeps that <paramref name="roomId"/>'s battle was won and waits for a boon, before the
        /// win is saved, so that a boon not chosen yet is offered again after resuming.
        /// </summary>
        public static void AwaitBoon(string runId, string roomId, AdventureBoonBattle battle)
        {
            if (string.IsNullOrEmpty(runId) || string.IsNullOrEmpty(roomId))
                return;
            var record = Record.Of(runId);
            record.Pending = battle == AdventureBoonBattle.None ? "" : $"{roomId}/{(int)battle}";
            record.Write();
        }

        /// <summary>
        /// Keeps the boon chosen and stops waiting. <paramref name="rainbowOffered"/> is whether
        /// the offer had a rainbow boon, which starts the run of offers without one again.
        /// </summary>
        public static void ChooseBoon(string runId, AdventureBoon boon, bool rainbowOffered)
        {
            if (string.IsNullOrEmpty(runId))
                return;
            var record = Record.Of(runId);
            record.Boons = string.Join(
                ";",
                Boons(runId).Append(boon).Select(chosen => chosen.Encode())
            );
            record.Pity = rainbowOffered ? "0" : (BoonPity(runId) + 1).ToString();
            record.Pending = "";
            record.Write();
        }

        // 保存した文字列（冒険のID|HPの割合|鍛えた回数|鍛えたカード|選んだ強化|虹の出ない回数|選んでいない
        // 強化のマス）を区切る。別の冒険の物ならnull。
        private static string[] Parts(string runId)
        {
            if (string.IsNullOrEmpty(runId))
                return null;
            var parts = (store.Read() ?? "").Split('|');
            return parts.Length >= 2 && parts[0] == runId ? parts : null;
        }

        // 1つの冒険の保存。読んだ値を書き換えて、まとめて書く。
        private sealed class Record
        {
            public string RunId = "";
            public string Shares = string.Join(",", Enumerable.Repeat("1", Slots));
            public string Honed = string.Join(",", new int[Slots]);
            public string Cards = "";
            public string Boons = "";
            public string Pity = "0";
            public string Pending = "";

            public static Record Of(string runId)
            {
                var record = new Record { RunId = runId };
                var parts = Parts(runId);
                if (parts == null)
                    return record;
                record.Shares = parts[1];
                if (parts.Length > 2)
                    record.Honed = parts[2];
                if (parts.Length > 3)
                    record.Cards = parts[3];
                if (parts.Length > 4)
                    record.Boons = parts[4];
                if (parts.Length > 5)
                    record.Pity = parts[5];
                if (parts.Length > 6)
                    record.Pending = parts[6];
                return record;
            }

            public void Write() =>
                store.Write(string.Join("|", RunId, Shares, Honed, Cards, Boons, Pity, Pending));
        }
    }
}
