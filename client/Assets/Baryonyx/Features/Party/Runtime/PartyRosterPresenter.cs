using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Party
{
    /// <summary>What the button under the chosen adventurer does.</summary>
    public enum PartyRosterAction
    {
        // 選んでいる人がいない。
        None,

        // パーティにいる人を外す。
        Leave,

        // 控えの人を空いている枠に入れる。
        Join,

        // 控えの人と入れ替える相手を、パーティの4枠から選び始める。
        Swap,

        // 入れ替える相手を選ぶのをやめる。
        Cancel,
    }

    /// <summary>Everything the adventurers' list shows, recomputed after every input.</summary>
    public sealed class PartyRosterState
    {
        // パーティの4つの枠のキャラのID。空いていればnull。
        public IReadOnlyList<string> Party { get; internal set; }

        // パーティにいない、持っているキャラのID（持っている順）。
        public IReadOnlyList<string> Bench { get; internal set; }

        // 選んでいる人のID。誰も持っていなければnull。
        public string Selected { get; internal set; }

        // 控えの人を入れる相手を、パーティの枠から選んでいる。
        public bool Swapping { get; internal set; }
        public PartyRosterAction Action { get; internal set; }
        public string ActionLabel { get; internal set; }

        // 押してもパーティが変わらない（最後の1人を外すなど）ときは押せない。
        public bool CanAct { get; internal set; }

        // 選んでいる人の個別の画面（装備・スキル・育成）を開ける。
        public bool CanOpen { get; internal set; }
        public string OwnedText { get; internal set; }
    }

    /// <summary>
    /// The formation's adventurers: every owned character as a tile, the party's four slots
    /// first and the bench below. Tapping a tile chooses the person, whose details show beside
    /// the list; the button under them opens their page. The second button changes the party:
    /// a member leaves, a bench character joins an empty slot, or, with the party full, the
    /// party's tiles wait for the one to swap with. Each change shows a notice that asks nothing
    /// of the player. With a <c>save</c> function the change is saved on the server first and
    /// the server's formation is shown; otherwise (the showcase) it changes only the formation
    /// in memory.
    /// </summary>
    public sealed class PartyRosterPresenter : IDisposable
    {
        public const string KeepOneMessage = "パーティには1人以上必要です";
        public const string SaveFailedMessage = "編成を保存できませんでした";
        public const string LeaveLabel = "パーティから外す";
        public const string JoinLabel = "パーティに入れる";
        public const string CancelLabel = "やめる";

        private readonly IPartyRosterView view;

        // 枠と、入れるキャラのID（nullなら外す）を保存し、保存したあとの編成を返す。
        private readonly Func<int, string, CancellationToken, Task<PartyFormation>> save;
        private readonly Action<string> remember;
        private readonly CancellationTokenSource lifetime = new();
        private PartyFormation formation;
        private string selected;
        private bool swapping;
        private bool saving;
        private bool disposed;

        public PartyRosterPresenter(
            IPartyRosterView view,
            PartyFormation formation,
            string selected = null,
            Func<int, string, CancellationToken, Task<PartyFormation>> save = null,
            Action<string> remember = null
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.formation = formation ?? throw new ArgumentNullException(nameof(formation));
            this.save = save;
            this.remember = remember;
            this.selected = formation.Find(selected) != null ? selected : First();
            view.SlotPressed += PressSlot;
            view.BenchPressed += PressBench;
            view.ActionPressed += Act;
            view.OpenPressed += Open;
            Refresh();
        }

        public PartyRosterState State { get; private set; }
        public PartyFormation Formation => formation;

        // 保存を待っている間。重ねて押されても受け付けない。
        public bool Saving => saving;

        // 実行中または直前の変更。テストで完了を待つために公開する。
        public Task ChangeTask { get; private set; } = Task.CompletedTask;

        // パーティの枠を押す。入れ替える相手を選んでいるときはその枠と入れ替え、空いた枠なら控えの人を入れる。
        public void PressSlot(int slot)
        {
            if (disposed || saving || slot < 0 || slot >= formation.SlotCount)
                return;
            string member = formation.Member(slot);
            bool bench = selected != null && formation.SlotOf(selected) < 0;
            if (swapping || (member == null && bench))
            {
                if (member == selected)
                    return;
                ChangeTask = ChangeAsync(slot, selected);
                return;
            }
            if (member != null)
                Choose(member);
        }

        public void PressBench(string id)
        {
            if (disposed || saving || formation.Find(id) == null || formation.SlotOf(id) >= 0)
                return;
            swapping = false;
            Choose(id);
        }

        public void Act()
        {
            if (disposed || saving)
                return;
            switch (ActionFor())
            {
                case PartyRosterAction.Leave:
                    ChangeTask = ChangeAsync(formation.SlotOf(selected), null);
                    break;
                case PartyRosterAction.Join:
                    ChangeTask = ChangeAsync(EmptySlot(), selected);
                    break;
                case PartyRosterAction.Swap:
                    swapping = true;
                    Refresh();
                    break;
                case PartyRosterAction.Cancel:
                    swapping = false;
                    Refresh();
                    break;
            }
        }

        public void Open()
        {
            if (disposed || saving || swapping || selected == null)
                return;
            view.OpenDetail(selected);
        }

        private void Choose(string id)
        {
            if (id == selected)
            {
                Refresh();
                return;
            }
            selected = id;
            remember?.Invoke(id);
            Refresh();
        }

        // 枠を変える。サーバーがあれば保存してから表示を変える。
        private async Task ChangeAsync(int slot, string id)
        {
            string before = formation.Member(slot);
            var change = formation.Preview(slot, id);
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
                formation.Apply(slot, id);
                swapping = false;
                view.ShowNotice(message);
                Refresh();
                return;
            }

            saving = true;
            Refresh();
            try
            {
                var saved = await save(slot, id, lifetime.Token);
                if (disposed)
                    return;
                formation = saved ?? formation;
                swapping = false;
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
            if (formation.Find(selected) == null)
                selected = First();
            var action = ActionFor();
            State = new PartyRosterState
            {
                Party = Enumerable.Range(0, formation.SlotCount).Select(formation.Member).ToArray(),
                Bench = formation.Bench.Select(member => member.Id).ToArray(),
                Selected = selected,
                Swapping = swapping,
                Action = action,
                ActionLabel = action switch
                {
                    PartyRosterAction.Leave => LeaveLabel,
                    PartyRosterAction.Cancel => CancelLabel,
                    PartyRosterAction.Join or PartyRosterAction.Swap => JoinLabel,
                    _ => "",
                },
                CanAct =
                    !saving
                    && action != PartyRosterAction.None
                    && (action != PartyRosterAction.Leave || formation.Count > 1),
                CanOpen = !saving && !swapping && selected != null,
                OwnedText = $"所持 {formation.Roster.Count}人",
            };
            view.Render(State);
        }

        private PartyRosterAction ActionFor()
        {
            if (selected == null)
                return PartyRosterAction.None;
            if (swapping)
                return PartyRosterAction.Cancel;
            if (formation.SlotOf(selected) >= 0)
                return PartyRosterAction.Leave;
            return EmptySlot() >= 0 ? PartyRosterAction.Join : PartyRosterAction.Swap;
        }

        private int EmptySlot()
        {
            for (int i = 0; i < formation.SlotCount; i++)
                if (formation.Member(i) == null)
                    return i;
            return -1;
        }

        // 最初に選ぶ人。パーティの先頭、いなければ持っている最初の人。
        private string First()
        {
            for (int i = 0; i < formation.SlotCount; i++)
                if (formation.Member(i) != null)
                    return formation.Member(i);
            return formation.Roster.Count > 0 ? formation.Roster[0].Id : null;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            lifetime.Cancel();
            lifetime.Dispose();
            view.SlotPressed -= PressSlot;
            view.BenchPressed -= PressBench;
            view.ActionPressed -= Act;
            view.OpenPressed -= Open;
        }
    }

    public interface IPartyRosterView
    {
        event Action<int> SlotPressed;
        event Action<string> BenchPressed;
        event Action ActionPressed;
        event Action OpenPressed;

        void Render(PartyRosterState state);

        // 操作を求めない通知（共通の通知の帯）を出す。
        void ShowNotice(string message);

        // 選んだ人の個別の画面（装備・スキル・育成）を開く。
        void OpenDetail(string id);
    }
}
