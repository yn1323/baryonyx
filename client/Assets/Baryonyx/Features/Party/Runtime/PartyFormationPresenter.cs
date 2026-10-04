using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
    /// asks nothing of the player. With a <c>save</c> function the change is saved on the server
    /// first and the server's formation is shown; otherwise (the showcase) it changes only the
    /// formation in memory.
    /// </summary>
    public sealed class PartyFormationPresenter : IDisposable
    {
        public const string KeepOneMessage = "パーティには1人以上必要です";
        public const string SaveFailedMessage = "編成を保存できませんでした";

        private readonly IPartyFormationView view;

        // 枠と、入れるキャラのID（nullなら外す）を保存し、保存したあとの編成を返す。
        private readonly Func<int, string, CancellationToken, Task<PartyFormation>> save;
        private readonly CancellationTokenSource lifetime = new();
        private PartyFormation formation;
        private int slot;
        private bool saving;
        private bool disposed;

        public PartyFormationPresenter(
            IPartyFormationView view,
            PartyFormation formation,
            Func<int, string, CancellationToken, Task<PartyFormation>> save = null
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.formation = formation ?? throw new ArgumentNullException(nameof(formation));
            this.save = save;
            view.SlotPressed += SelectSlot;
            view.MemberPressed += Choose;
            view.LeavePressed += Leave;
            Refresh();
        }

        public PartyFormationState State { get; private set; }
        public PartyFormation Formation => formation;

        // 保存を待っている間。重ねて押されても受け付けない。
        public bool Saving => saving;

        // 実行中または直前の変更。テストで完了を待つために公開する。
        public Task ChangeTask { get; private set; } = Task.CompletedTask;

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
            ChangeTask = ChangeAsync(id);
        }

        // 選んでいる枠のキャラを外す。
        public void Leave()
        {
            if (!disposed)
                ChangeTask = ChangeAsync(null);
        }

        // 選んでいる枠を変える。サーバーがあれば保存してから表示を変える。
        private async Task ChangeAsync(string id)
        {
            if (saving)
                return;
            int target = slot;
            string before = formation.Member(target);
            var change = formation.Preview(target, id);
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
            if (change == PartyChange.KeepOne || save == null)
            {
                formation.Apply(target, id);
                view.ShowNotice(message);
                Refresh();
                return;
            }

            saving = true;
            try
            {
                var saved = await save(target, id, lifetime.Token);
                if (disposed)
                    return;
                formation = saved ?? formation;
                view.ShowNotice(message);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                if (!disposed)
                    view.ShowNotice(SaveFailedMessage);
            }
            finally
            {
                saving = false;
            }
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
            lifetime.Cancel();
            lifetime.Dispose();
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
