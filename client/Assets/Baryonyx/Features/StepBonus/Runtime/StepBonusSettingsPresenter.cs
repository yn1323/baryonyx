using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.StepBonus
{
    /// <summary>What one slot shows in the left column, which doubles as today's effects.</summary>
    public readonly struct StepBonusSlotState
    {
        public StepBonusSlotState(
            string bonus,
            bool open,
            bool selected,
            string tier,
            string name,
            string effect
        )
        {
            Bonus = bonus;
            Open = open;
            Selected = selected;
            Tier = tier;
            Name = name;
            Effect = effect;
        }

        // 入れているボーナスのID。空いていればnull。
        public string Bonus { get; }

        // 今日のUPTが段階に届いて、ボーナスが効いている。
        public bool Open { get; }
        public bool Selected { get; }

        // 「1,000 UPT ×1.0」
        public string Tier { get; }
        public string Name { get; }

        // 枠の倍率を掛けた効果（例：「ドロップ率 +8.4%」）。
        public string Effect { get; }
    }

    /// <summary>What one owned bonus row shows.</summary>
    public readonly struct StepBonusRowState
    {
        public StepBonusRowState(
            string id,
            bool visible,
            bool set,
            StepBonusRank rank,
            string effect
        )
        {
            Id = id;
            Visible = visible;
            Set = set;
            Rank = rank;
            Effect = effect;
        }

        public string Id { get; }
        public StepBonusRank Rank { get; }

        // ランクの効果量（枠に入れる前）。
        public string Effect { get; }

        // 選んだタブのカテゴリに入っている。
        public bool Visible { get; }

        // どこかの枠に入れている。
        public bool Set { get; }
    }

    /// <summary>Everything the bonus settings show, recomputed after every input.</summary>
    public sealed class StepBonusSettingsState
    {
        public int SelectedSlot { get; internal set; }

        // 0は「すべて」、1からはカテゴリ（探索・ドロップ・戦闘）。
        public int Tab { get; internal set; }
        public string OwnedText { get; internal set; }
        public IReadOnlyList<StepBonusSlotState> Slots { get; internal set; }
        public IReadOnlyList<StepBonusRowState> Rows { get; internal set; }
    }

    /// <summary>
    /// The tavern's bonus settings: choose a slot on the left, then tap an owned bonus on the
    /// right to put it in. Choosing a bonus that sits in another slot swaps the two. Each change
    /// shows a notice that asks nothing of the player. With a <c>save</c> function the change is
    /// saved on the server first and the server's result is shown; otherwise (the showcase) it
    /// changes only the loadout in memory.
    /// </summary>
    public sealed class StepBonusSettingsPresenter : IDisposable
    {
        public static readonly string[] TabLabels = { "すべて", "探索", "ドロップ", "戦闘" };

        public const string SaveFailedMessage = "ボーナスを保存できませんでした";

        private readonly IStepBonusSettingsView view;
        private readonly int upt;
        private readonly Func<int, string, CancellationToken, Task<StepBonusLoadout>> save;
        private readonly CancellationTokenSource lifetime = new();
        private StepBonusLoadout loadout;
        private int slot;
        private int tab;
        private bool saving;
        private bool disposed;

        public StepBonusSettingsPresenter(
            IStepBonusSettingsView view,
            StepBonusLoadout loadout,
            int upt,
            Func<int, string, CancellationToken, Task<StepBonusLoadout>> save = null
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            this.upt = Math.Max(0, upt);
            this.save = save;
            view.SlotPressed += SelectSlot;
            view.BonusPressed += OnBonusPressed;
            view.TabPressed += SelectTab;
            Refresh();
        }

        public StepBonusSettingsState State { get; private set; }
        public StepBonusLoadout Loadout => loadout;

        // 保存を待っている間。重ねて押されても受け付けない。
        public bool Saving => saving;

        // 実行中または直前の保存。テストで完了を待つために公開する。
        public Task ChooseTask { get; private set; } = Task.CompletedTask;

        // 一覧に並べる順。ランクの高い順で、同じランクはボーナスの定義順。
        public static IEnumerable<StepBonusRoll> Order(StepBonusLoadout loadout) =>
            loadout
                .Owned.Select((roll, index) => (roll, index))
                .OrderByDescending(item => item.roll.Rank)
                .ThenBy(item => item.index)
                .Select(item => item.roll);

        public static bool InTab(StepBonusDefinition bonus, int tab) =>
            tab <= 0 || bonus != null && (int)bonus.Category == tab - 1;

        public void SelectSlot(int index)
        {
            if (disposed || index < 0 || index >= loadout.SlotCount)
                return;
            slot = index;
            Refresh();
        }

        private void OnBonusPressed(string id) => ChooseTask = ChooseAsync(id);

        // 選んでいる枠に、押したボーナスを入れる。サーバーがあれば保存してから表示を変える。
        public async Task ChooseAsync(string id)
        {
            if (disposed || saving)
                return;
            int target = slot;
            string before = loadout.Bonus(target);
            var change = loadout.Preview(target, id);
            if (change == StepBonusChange.None)
                return;
            string message =
                change == StepBonusChange.Swap
                    ? $"「{Name(id)}」と「{Name(before)}」を入れ替えました"
                    : $"{StepBonusLoadout.Upt(loadout.Tier(target))}の枠に「{Name(id)}」をセットしました";
            if (save == null)
            {
                loadout.Apply(target, id);
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
                loadout = saved ?? loadout;
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

        public void SelectTab(int index)
        {
            if (disposed || index < 0 || index >= TabLabels.Length)
                return;
            tab = index;
            Refresh();
        }

        public void Refresh()
        {
            if (disposed)
                return;
            State = Build();
            view.Render(State);
        }

        private StepBonusSettingsState Build()
        {
            var slots = new StepBonusSlotState[loadout.SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                string id = loadout.Bonus(i);
                slots[i] = new StepBonusSlotState(
                    id,
                    loadout.IsOpen(i, upt),
                    i == slot,
                    $"{StepBonusLoadout.Upt(loadout.Tier(i))} UPT {StepBonusLoadout.Times(loadout.Multiplier(i))}",
                    id != null ? Name(id) : "空き",
                    id != null
                        ? StepBonusLoadout.Effect(loadout.Definition(id), loadout.Effective(id, i))
                        : ""
                );
            }

            return new StepBonusSettingsState
            {
                SelectedSlot = slot,
                Tab = tab,
                OwnedText = $"所持 {loadout.Owned.Count} / {loadout.TotalKinds}",
                Slots = slots,
                Rows = Order(loadout)
                    .Select(roll => new StepBonusRowState(
                        roll.Id,
                        InTab(loadout.Definition(roll.Id), tab),
                        loadout.SlotOf(roll.Id) >= 0,
                        roll.Rank,
                        StepBonusLoadout.Effect(loadout.Definition(roll.Id), loadout.Value(roll.Id))
                    ))
                    .ToArray(),
            };
        }

        private string Name(string id) => loadout.Definition(id)?.Name ?? "";

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            lifetime.Cancel();
            lifetime.Dispose();
            view.SlotPressed -= SelectSlot;
            view.BonusPressed -= OnBonusPressed;
            view.TabPressed -= SelectTab;
        }
    }

    public interface IStepBonusSettingsView
    {
        event Action<int> SlotPressed;
        event Action<string> BonusPressed;
        event Action<int> TabPressed;

        void Render(StepBonusSettingsState state);

        // 操作を求めない通知（共通の通知の帯）を出す。
        void ShowNotice(string message);
    }
}
