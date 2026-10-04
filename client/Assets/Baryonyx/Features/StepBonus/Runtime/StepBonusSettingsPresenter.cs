using System;
using System.Collections.Generic;
using System.Linq;

namespace Baryonyx.StepBonus
{
    /// <summary>What one slot shows on the bonus settings.</summary>
    public readonly struct StepBonusSlotState
    {
        public StepBonusSlotState(
            string bonus,
            bool open,
            bool selected,
            bool partner,
            string label
        )
        {
            Bonus = bonus;
            Open = open;
            Selected = selected;
            Partner = partner;
            Label = label;
        }

        // 入れているボーナスのID。空いていればnull。
        public string Bonus { get; }

        // 今日のUPTが段階に届いて、ボーナスが効いている。
        public bool Open { get; }
        public bool Selected { get; }

        // 選んだボーナスを入れると、中身を入れ替える相手の枠。
        public bool Partner { get; }

        // 「1,000 ×1.0」
        public string Label { get; }
    }

    /// <summary>What one owned bonus row shows.</summary>
    public readonly struct StepBonusRowState
    {
        public StepBonusRowState(string id, bool visible, bool selected, string note, bool updated)
        {
            Id = id;
            Visible = visible;
            Selected = selected;
            Note = note;
            Updated = updated;
        }

        public string Id { get; }

        // 選んだタブのカテゴリに入っている。
        public bool Visible { get; }
        public bool Selected { get; }

        // 「2,000でセット中」または「UP　前回の冒険で更新」。
        public string Note { get; }
        public bool Updated { get; }
    }

    /// <summary>Everything the bonus settings show, recomputed after every input.</summary>
    public sealed class StepBonusSettingsState
    {
        public int SelectedSlot { get; internal set; }
        public string SelectedBonus { get; internal set; }

        // 0は「すべて」、1からはカテゴリ（探索・ドロップ・戦闘）。
        public int Tab { get; internal set; }
        public string HeaderText { get; internal set; }
        public string OwnedText { get; internal set; }
        public IReadOnlyList<StepBonusSlotState> Slots { get; internal set; }
        public IReadOnlyList<StepBonusRowState> Rows { get; internal set; }
        public string FooterTitle { get; internal set; }
        public string FooterDetail { get; internal set; }
        public string ConfirmLabel { get; internal set; }
        public bool CanConfirm { get; internal set; }
    }

    /// <summary>
    /// The tavern's bonus settings: choose a slot, then an owned bonus, then set it. Choosing a
    /// bonus that sits in another slot swaps the two. The loadout is shared through
    /// <see cref="StepBonusSession"/>, so Home and later visits see the change.
    /// </summary>
    public sealed class StepBonusSettingsPresenter : IDisposable
    {
        public static readonly string[] TabLabels = { "すべて", "探索", "ドロップ", "戦闘" };

        private readonly IStepBonusSettingsView view;
        private readonly StepBonusLoadout loadout;
        private readonly int upt;
        private int slot;
        private string bonus;
        private int tab;
        private bool disposed;

        public StepBonusSettingsPresenter(
            IStepBonusSettingsView view,
            StepBonusLoadout loadout,
            int upt
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            this.upt = Math.Max(0, upt);
            view.SlotPressed += SelectSlot;
            view.BonusPressed += SelectBonus;
            view.TabPressed += SelectTab;
            view.ConfirmPressed += Confirm;
            Refresh();
        }

        public StepBonusSettingsState State { get; private set; }

        // 一覧に並べる順。ランクの高い順で、同じランクはボーナスの定義順。
        public static IEnumerable<StepBonusRoll> Order(StepBonusLoadout loadout) =>
            loadout
                .Owned.Select((roll, index) => (roll, index))
                .OrderByDescending(item =>
                    StepBonusLoadout.RankOf(loadout.Definition(item.roll.Id), item.roll.Value)
                )
                .ThenBy(item => item.index)
                .Select(item => item.roll);

        public static bool InTab(StepBonusDefinition bonus, int tab) =>
            tab <= 0 || bonus != null && (int)bonus.Category == tab - 1;

        public void SelectSlot(int index)
        {
            if (disposed || index < 0 || index >= loadout.SlotCount)
                return;
            slot = index;
            bonus = null;
            Refresh();
        }

        public void SelectBonus(string id)
        {
            if (disposed || !loadout.Owns(id))
                return;
            bonus = id;
            Refresh();
        }

        public void SelectTab(int index)
        {
            if (disposed || index < 0 || index >= TabLabels.Length)
                return;
            tab = index;
            if (!InTab(loadout.Definition(bonus), tab))
                bonus = null;
            Refresh();
        }

        public void Confirm()
        {
            if (disposed || bonus == null)
                return;
            string before = loadout.Bonus(slot);
            var change = loadout.Apply(slot, bonus);
            if (change == StepBonusChange.None)
                return;
            view.ShowToast(
                change == StepBonusChange.Swap
                    ? $"「{Name(bonus)}」と「{Name(before)}」を入れ替えました"
                    : $"{StepBonusLoadout.Upt(loadout.Tier(slot))}の枠に「{Name(bonus)}」をセットしました"
            );
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
            var change = loadout.Preview(slot, bonus);
            int partner = change == StepBonusChange.Swap ? loadout.SlotOf(bonus) : -1;
            var slots = new StepBonusSlotState[loadout.SlotCount];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new StepBonusSlotState(
                    loadout.Bonus(i),
                    loadout.IsOpen(i, upt),
                    i == slot,
                    i == partner,
                    $"{StepBonusLoadout.Upt(loadout.Tier(i))} {StepBonusLoadout.Times(loadout.Multiplier(i))}"
                );

            var rows = Order(loadout)
                .Select(roll =>
                {
                    int setIn = loadout.SlotOf(roll.Id);
                    string note =
                        setIn >= 0 ? $"{StepBonusLoadout.Upt(loadout.Tier(setIn))}でセット中"
                        : roll.Updated ? "UP　前回の冒険で更新"
                        : "";
                    return new StepBonusRowState(
                        roll.Id,
                        InTab(loadout.Definition(roll.Id), tab),
                        roll.Id == bonus,
                        note,
                        setIn < 0 && roll.Updated
                    );
                })
                .ToArray();

            var state = new StepBonusSettingsState
            {
                SelectedSlot = slot,
                SelectedBonus = bonus,
                Tab = tab,
                HeaderText = $"今日 {StepBonusLoadout.Upt(upt)} UPT・翌朝4:00まで有効",
                OwnedText = $"所持 {loadout.Owned.Count} / {loadout.TotalKinds}",
                Slots = slots,
                Rows = rows,
            };
            Footer(state, change);
            return state;
        }

        private void Footer(StepBonusSettingsState state, StepBonusChange change)
        {
            string tier = $"{StepBonusLoadout.Upt(loadout.Tier(slot))}の枠";
            string current = loadout.Bonus(slot);
            if (bonus == null)
            {
                state.FooterTitle = $"{tier}（{StepBonusLoadout.Times(loadout.Multiplier(slot))}）";
                state.FooterDetail =
                    current != null
                        ? $"いま：{Name(current)}　{EffectIn(current, slot)}"
                        : "空いています。入れるボーナスを選んでください";
                state.ConfirmLabel = "セットする";
                state.CanConfirm = false;
                return;
            }

            state.FooterTitle = change switch
            {
                StepBonusChange.Swap =>
                    $"{tier}：{Name(current)} ⇔ {Name(bonus)}（{StepBonusLoadout.Upt(loadout.Tier(loadout.SlotOf(bonus)))}の枠）",
                StepBonusChange.Set when current != null =>
                    $"{tier}：{Name(current)} → {Name(bonus)}",
                StepBonusChange.Set => $"{tier}：{Name(bonus)}をセット",
                _ => $"{tier}：{Name(bonus)}",
            };
            var roll = loadout.Roll(bonus);
            var definition = loadout.Definition(bonus);
            state.FooterDetail =
                $"{EffectIn(bonus, slot)}（{StepBonusLoadout.Number(definition, roll.Value)}{StepBonusLoadout.Times(loadout.Multiplier(slot))}）";
            state.ConfirmLabel = change switch
            {
                StepBonusChange.Swap => "入れ替える",
                StepBonusChange.Set => "セットする",
                _ => "セット中",
            };
            state.CanConfirm = change != StepBonusChange.None;
        }

        // 枠の倍率を掛けた効果の文。
        private string EffectIn(string id, int index) =>
            StepBonusLoadout.Effect(loadout.Definition(id), loadout.Effective(id, index));

        private string Name(string id) => loadout.Definition(id)?.Name ?? "";

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            view.SlotPressed -= SelectSlot;
            view.BonusPressed -= SelectBonus;
            view.TabPressed -= SelectTab;
            view.ConfirmPressed -= Confirm;
        }
    }

    public interface IStepBonusSettingsView
    {
        event Action<int> SlotPressed;
        event Action<string> BonusPressed;
        event Action<int> TabPressed;
        event Action ConfirmPressed;

        void Render(StepBonusSettingsState state);
        void ShowToast(string message);
    }
}
