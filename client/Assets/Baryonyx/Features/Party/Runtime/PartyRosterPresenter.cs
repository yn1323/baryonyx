using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Baryonyx.Party
{
    /// <summary>Everything the adventurers' list shows, recomputed after every input.</summary>
    public sealed class PartyRosterState
    {
        // パーティの4つの枠のキャラのID。空いていればnull。
        public IReadOnlyList<string> Party { get; internal set; }

        // パーティにいない、持っているキャラのID（持っている順）。
        public IReadOnlyList<string> Bench { get; internal set; }

        // 選んでいるパーティの枠（空いた枠も選べる）。選んでいなければ-1。
        public int SelectedSlot { get; internal set; } = -1;

        // 選んでいる一覧の人のID。選んでいなければnull。
        public string SelectedBench { get; internal set; }

        // 「パーティから外す」で外せる人を選んでいる（最後の1人は外せない）。
        public bool CanRemove { get; internal set; }
    }

    /// <summary>
    /// The formation's adventurers: the party's four slots on the left and the owned characters
    /// outside the party on the right, ending with "remove from the party". Tapping a party slot
    /// and then a character on the right swaps them (or fills the empty slot); tapping in the
    /// other order does the same. With a slot chosen, "remove" empties it. Tapping the chosen card
    /// again lets it go. Holding a character opens their page (equipment, card skills, training).
    /// A change shows no notice (the cards show it); only refusals and failures do. A change
    /// shows at once; with a <c>save</c> function it is then saved on the server behind it, and
    /// the formation is drawn again from the saved one, or put back when the save failed (the
    /// failure is told on the notice band by <see cref="Networking.SaveQueue"/>). Without it (the
    /// showcase) it changes only the formation in memory.
    /// </summary>
    public sealed class PartyRosterPresenter : IDisposable
    {
        public const string KeepOneMessage = "パーティには1人以上必要です";
        public const string SaveFailedMessage = "編成を保存できませんでした";
        public const string ChooseLeaverMessage = "外す人をパーティから選んでください";
        public const string RemoveLabel = "パーティから外す";

        private readonly IPartyRosterView view;

        // 枠と、入れるキャラのID（nullなら外す）を保存する。保存できたあと（失敗したら戻したあと）の編成を返す。
        private readonly Func<int, string, Task<PartyFormation>> save;
        private PartyFormation formation;
        private int selectedSlot = -1;
        private string selectedBench;
        private bool disposed;

        public PartyRosterPresenter(
            IPartyRosterView view,
            PartyFormation formation,
            Func<int, string, Task<PartyFormation>> save = null
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.formation = formation ?? throw new ArgumentNullException(nameof(formation));
            this.save = save;
            view.SlotPressed += PressSlot;
            view.BenchPressed += PressBench;
            view.RemovePressed += PressRemove;
            view.Held += Hold;
            Refresh();
        }

        public PartyRosterState State { get; private set; }
        public PartyFormation Formation => formation;

        // 直前の変更の保存。テストで完了を待つために公開する。
        public Task ChangeTask { get; private set; } = Task.CompletedTask;

        // パーティの枠を押す。一覧の人を選んでいればその枠と入れ替え（空きなら入れ）、そうでなければ枠を選ぶ。
        public void PressSlot(int slot)
        {
            if (disposed || slot < 0 || slot >= formation.SlotCount)
                return;
            if (selectedBench != null)
            {
                ChangeTask = ChangeAsync(slot, selectedBench);
                return;
            }
            selectedSlot = selectedSlot == slot ? -1 : slot;
            Refresh();
        }

        // 一覧の人を押す。パーティの枠を選んでいればその枠と入れ替え、そうでなければその人を選ぶ。
        public void PressBench(string id)
        {
            if (disposed || formation.Find(id) == null || formation.SlotOf(id) >= 0)
                return;
            if (selectedSlot >= 0)
            {
                ChangeTask = ChangeAsync(selectedSlot, id);
                return;
            }
            selectedBench = selectedBench == id ? null : id;
            Refresh();
        }

        // 「パーティから外す」。選んでいるパーティの枠を空ける。
        public void PressRemove()
        {
            if (disposed)
                return;
            if (selectedSlot < 0 || formation.Member(selectedSlot) == null)
            {
                view.ShowNotice(ChooseLeaverMessage);
                return;
            }
            ChangeTask = ChangeAsync(selectedSlot, null);
        }

        // 長押し。その人の個別の画面（装備・スキル・育成）を開く。
        public void Hold(string id)
        {
            if (disposed || formation.Find(id) == null)
                return;
            view.OpenDetail(id);
        }

        // 枠を変えて、すぐ表示する。サーバーがあれば、そのあとで保存する。変わったことは枠を見ればわかるので知らせない。
        private async Task ChangeAsync(int slot, string id)
        {
            var change = formation.Preview(slot, id);
            bool changes = change is PartyChange.Join or PartyChange.Replace or PartyChange.Leave;
            if (changes)
                formation.Apply(slot, id);
            Release();
            if (change == PartyChange.KeepOne)
                view.ShowNotice(KeepOneMessage);
            Refresh();
            if (!changes || save == null)
                return;

            // 保存できたら保存した編成で、失敗したら戻した編成で描き直す。
            var saved = await save(slot, id);
            if (disposed)
                return;
            formation = saved ?? formation;
            Refresh();
        }

        // 入れ替えたあとは何も選んでいない状態に戻す。
        private void Release()
        {
            selectedSlot = -1;
            selectedBench = null;
        }

        public void Refresh()
        {
            if (disposed)
                return;
            if (selectedBench != null && formation.SlotOf(selectedBench) >= 0)
                selectedBench = null;
            if (selectedBench != null && formation.Find(selectedBench) == null)
                selectedBench = null;
            State = new PartyRosterState
            {
                Party = Enumerable.Range(0, formation.SlotCount).Select(formation.Member).ToArray(),
                Bench = formation.Bench.Select(member => member.Id).ToArray(),
                SelectedSlot = selectedSlot,
                SelectedBench = selectedBench,
                CanRemove =
                    selectedSlot >= 0
                    && formation.Member(selectedSlot) != null
                    && formation.Count > 1,
            };
            view.Render(State);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            view.SlotPressed -= PressSlot;
            view.BenchPressed -= PressBench;
            view.RemovePressed -= PressRemove;
            view.Held -= Hold;
        }
    }

    public interface IPartyRosterView
    {
        event Action<int> SlotPressed;
        event Action<string> BenchPressed;
        event Action RemovePressed;

        // 人のカードを長押しした（その人のID）。
        event Action<string> Held;

        void Render(PartyRosterState state);

        // 操作を求めない通知（共通の通知の帯）を出す。
        void ShowNotice(string message);

        // その人の個別の画面（装備・スキル・育成）を開く。
        void OpenDetail(string id);
    }
}
