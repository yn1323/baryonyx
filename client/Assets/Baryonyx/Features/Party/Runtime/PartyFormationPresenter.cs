using System;
using System.Collections.Generic;
using System.Linq;

namespace Baryonyx.Party
{
    /// <summary>What one party slot on the left shows.</summary>
    public readonly struct PartySlotState
    {
        public PartySlotState(string member, bool selected)
        {
            Member = member;
            Selected = selected;
        }

        // 入っているキャラのID。空いていればnull。
        public string Member { get; }
        public bool Selected { get; }
    }

    /// <summary>Everything the formation shows, recomputed after every input.</summary>
    public sealed class PartyFormationState
    {
        public int SelectedSlot { get; internal set; }
        public IReadOnlyList<PartySlotState> Slots { get; internal set; }

        // 右に並べる、パーティにいないキャラのID（持っている順）。
        public IReadOnlyList<string> Bench { get; internal set; }
        public string OwnedText { get; internal set; }

        // 選んでいる枠にキャラがいて、「外す」を押せる。
        public bool CanLeave { get; internal set; }
    }

    /// <summary>
    /// The tavern's formation: choose a slot on the left, then tap a character on the right to
    /// put them in, or "外す" to take the slot's member out. Each change shows a notice that
    /// asks nothing of the player. The formation lives only while the app runs.
    /// </summary>
    public sealed class PartyFormationPresenter : IDisposable
    {
        public const string KeepOneMessage = "パーティには1人以上必要です";

        private readonly IPartyFormationView view;
        private readonly PartyFormation formation;
        private int slot;
        private bool disposed;

        public PartyFormationPresenter(IPartyFormationView view, PartyFormation formation)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.formation = formation ?? throw new ArgumentNullException(nameof(formation));
            view.SlotPressed += SelectSlot;
            view.MemberPressed += Choose;
            view.LeavePressed += Leave;
            Refresh();
        }

        public PartyFormationState State { get; private set; }
        public PartyFormation Formation => formation;

        public void SelectSlot(int index)
        {
            if (disposed || index < 0 || index >= formation.SlotCount)
                return;
            slot = index;
            Refresh();
        }

        // 選んでいる枠に、押したキャラを入れる。
        public void Choose(string id)
        {
            if (disposed || id == null)
                return;
            Change(id);
        }

        // 選んでいる枠のキャラを外す。
        public void Leave()
        {
            if (!disposed)
                Change(null);
        }

        private void Change(string id)
        {
            string before = formation.Member(slot);
            var change = formation.Apply(slot, id);
            string message = change switch
            {
                PartyChange.Join => $"{formation.Name(id)}を編成しました",
                PartyChange.Replace =>
                    $"{formation.Name(before)}と{formation.Name(id)}を入れ替えました",
                PartyChange.Leave => $"{formation.Name(before)}を外しました",
                PartyChange.KeepOne => KeepOneMessage,
                _ => null,
            };
            if (message == null)
                return;
            view.ShowNotice(message);
            Refresh();
        }

        public void Refresh()
        {
            if (disposed)
                return;
            State = new PartyFormationState
            {
                SelectedSlot = slot,
                Slots = Enumerable
                    .Range(0, formation.SlotCount)
                    .Select(i => new PartySlotState(formation.Member(i), i == slot))
                    .ToArray(),
                Bench = formation.Bench.Select(member => member.Id).ToArray(),
                OwnedText = $"所持 {formation.Roster.Count}人",
                CanLeave = formation.Member(slot) != null,
            };
            view.Render(State);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            view.SlotPressed -= SelectSlot;
            view.MemberPressed -= Choose;
            view.LeavePressed -= Leave;
        }
    }

    public interface IPartyFormationView
    {
        event Action<int> SlotPressed;
        event Action<string> MemberPressed;
        event Action LeavePressed;

        void Render(PartyFormationState state);

        // 操作を求めない通知（共通の通知の帯）を出す。
        void ShowNotice(string message);
    }
}
