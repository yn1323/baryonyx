using System;
using System.Collections.Generic;
using System.Linq;

namespace Baryonyx.Party
{
    /// <summary>What putting a character into a slot, or taking one out, would do.</summary>
    public enum PartyChange
    {
        // 何も変わらない（同じキャラ、空きを外す、範囲外など）。
        None,

        // 空いている枠に入れる。
        Join,

        // 枠のキャラを、パーティにいないキャラと入れ替える。
        Replace,

        // 枠のキャラを外して空きにする。
        Leave,

        // 最後の1人は外せない。
        KeepOne,
    }

    /// <summary>
    /// The four party slots and the characters the player owns. A slot takes a character who is
    /// not in the party yet; taking a member out leaves the slot empty, but at least one member
    /// always stays (doc/features/party.md).
    /// </summary>
    public sealed class PartyFormation
    {
        public const int Size = 4;

        private readonly PartyMember[] roster;
        private readonly string[] slots;

        public PartyFormation(IEnumerable<PartyMember> roster, IEnumerable<string> slots)
        {
            this.roster = (roster ?? Enumerable.Empty<PartyMember>())
                .Where(member => member != null && !string.IsNullOrEmpty(member.Id))
                .ToArray();
            this.slots = new string[Size];
            var given = (slots ?? Enumerable.Empty<string>()).Take(Size).ToArray();
            for (int i = 0; i < given.Length; i++)
                // 持っていないキャラと、2つ目の枠に入った同じキャラは空きにする。
                if (Find(given[i]) != null && Array.IndexOf(this.slots, given[i]) < 0)
                    this.slots[i] = given[i];
        }

        public static PartyFormation From(PartyMockData data) =>
            data != null
                ? new PartyFormation(data.Members, data.Formation)
                : new PartyFormation(null, null);

        public IReadOnlyList<PartyMember> Roster => roster;
        public int SlotCount => slots.Length;
        public int Count => slots.Count(id => id != null);

        // 枠に入っているキャラのID。空いていればnull。
        public string Member(int slot) => slot >= 0 && slot < slots.Length ? slots[slot] : null;

        public int SlotOf(string id) => id == null ? -1 : Array.IndexOf(slots, id);

        public PartyMember Find(string id) =>
            string.IsNullOrEmpty(id) ? null : Array.Find(roster, member => member.Id == id);

        public string Name(string id) => Find(id)?.Name ?? "";

        // パーティにいない、持っているキャラ（持っている順）。
        public IEnumerable<PartyMember> Bench => roster.Where(member => SlotOf(member.Id) < 0);

        /// <summary>What <see cref="Apply"/> would do. A null id takes the member out.</summary>
        public PartyChange Preview(int slot, string id)
        {
            if (slot < 0 || slot >= slots.Length)
                return PartyChange.None;
            if (id == null)
                return slots[slot] == null ? PartyChange.None
                    : Count <= 1 ? PartyChange.KeepOne
                    : PartyChange.Leave;
            // 右に並ぶのはパーティにいないキャラだけなので、パーティ内の並べ替えはしない。
            if (Find(id) == null || SlotOf(id) >= 0)
                return PartyChange.None;
            return slots[slot] == null ? PartyChange.Join : PartyChange.Replace;
        }

        public PartyChange Apply(int slot, string id)
        {
            var change = Preview(slot, id);
            if (change is PartyChange.Join or PartyChange.Replace or PartyChange.Leave)
                slots[slot] = id;
            return change;
        }
    }
}
