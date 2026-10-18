using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.UI.Buttons;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.UI.UiText;

namespace Baryonyx.StepBonus
{
    /// <summary>One slot row in the left column, baked by the generator.</summary>
    [Serializable]
    public sealed class StepBonusSlotWidget
    {
        public Button Button;
        public Image Frame;
        public Image Icon;
        public GameObject Selected;
        public TMP_Text Tier;
        public TMP_Text Name;
        public TMP_Text Effect;
    }

    /// <summary>One owned bonus row in the right list, baked by the generator.</summary>
    [Serializable]
    public sealed class StepBonusRowWidget
    {
        public string Id = "";
        public Button Button;
        public Image RankFrame;
        public TMP_Text Rank;
        public TMP_Text Effect;

        // どこかの枠に入れているときだけ出す印。
        public GameObject SetMark;
    }

    /// <summary>
    /// The tavern's bonus settings over the whole guide screen, in the guide frame: the slots and
    /// today's effects on the dark pane on the left, the owned bonuses on the right. StepBonusAssets bakes the rows into the tavern
    /// prefab, so they read in the editor; this view wires them up and drives its own presenter.
    /// When the app has a server (<see cref="StepBonusSession.Source"/>), it reads the player's
    /// bonuses on opening and saves every change there; otherwise it uses the mock data.
    /// Changes are told on the guide screen's notice band.
    /// </summary>
    public sealed class StepBonusSettingsView : GuidePanel, IStepBonusSettingsView, IEditModePreview
    {
        // 段階に届いていない枠は、アイコン・枠・文字を暗くする。
        public static readonly Color Closed = new(0.42f, 0.42f, 0.48f, 1f);

        public const string LoadFailedMessage = "ボーナスを取得できませんでした";

        public StepBonusMockData Data;

        public TMP_Text Owned;
        public StepBonusSlotWidget[] Slots = Array.Empty<StepBonusSlotWidget>();
        public Color[] TierColors = Array.Empty<Color>();
        public GuideTab[] Tabs = Array.Empty<GuideTab>();
        public ScrollRect List;
        public StepBonusRowWidget[] Rows = Array.Empty<StepBonusRowWidget>();

        private StepBonusSettingsPresenter presenter;
        private readonly ButtonBindings bindings = new();

        public event Action<int> SlotPressed;
        public event Action<string> BonusPressed;
        public event Action<int> TabPressed;

        public StepBonusSettingsPresenter Presenter => presenter;

        private void OnEnable()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                bindings.Bind(Slots[i].Button, () => SlotPressed?.Invoke(index));
            }
            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                if (Tabs[i] != null)
                    bindings.Bind(Tabs[i].Button, () => TabPressed?.Invoke(index));
            }
            foreach (var row in Rows)
            {
                string id = row.Id;
                bindings.Bind(row.Button, () => BonusPressed?.Invoke(id));
            }

            // 開くたびに、サーバーの持ち物と枠（なければ仮データ）で描き直す。
            presenter?.Dispose();
            presenter = null;
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            if (Data == null)
                return;
            var source = StepBonusSession.Source;
            if (source == null)
            {
                Present(StepBonusSession.Loadout(Data), null);
                return;
            }
            ShowLoading();
            StartLoad(
                token => LoadAsync(source, token),
                LoadFailedMessage,
                ShowLoadFailed,
                "ACTボーナスを取得できませんでした"
            );
        }

        private async Task LoadAsync(IStepBonusSource source, CancellationToken token)
        {
            var state = await source.LoadAsync(token);
            var loadout = StepBonusLoadout.From(Data, state);
            if (token.IsCancellationRequested)
                return;
            StepBonusSession.Use(loadout);
            Present(
                loadout,
                (slot, id, cancel) => SaveAsync(source, slot, id, cancel),
                state.Locked
            );
            if (state.Locked)
                ShowNotice(StepBonusSettingsPresenter.LockedMessage);
        }

        private void ShowLoadFailed() => Set(Owned, LoadFailedText);

        private async Task<StepBonusLoadout> SaveAsync(
            IStepBonusSource source,
            int slot,
            string id,
            CancellationToken token
        )
        {
            var loadout = StepBonusLoadout.From(Data, await source.SetSlotAsync(slot, id, token));
            StepBonusSession.Use(loadout);
            return loadout;
        }

        private void Present(
            StepBonusLoadout loadout,
            Func<int, string, CancellationToken, Task<StepBonusLoadout>> save,
            bool locked = false
        ) =>
            presenter = new StepBonusSettingsPresenter(
                this,
                loadout,
                StepBonusSession.ActOr(Data),
                save,
                locked
            );

        // 読み込むまで、仮データの中身を本当の持ち物として見せない。
        private void ShowLoading()
        {
            Set(Owned, LoadingText);
            foreach (var widget in Slots)
            {
                if (widget.Icon != null)
                    widget.Icon.enabled = false;
                if (widget.Selected != null)
                    widget.Selected.SetActive(false);
                Set(widget.Name, "");
                Set(widget.Effect, "");
            }
            foreach (var row in Rows)
                row.Button.gameObject.SetActive(false);
        }

        // 停止中の表示の切り替え：カテゴリのタブごとの一覧と、サーバーから読み込む間・読めなかったときの表示。
        // タブは、静的なセッションではなく仮データの持ち物から、Play中と同じ絞り込み（InTab）で行を出す。
        public IReadOnlyList<string> PreviewStates =>
            StepBonusSettingsPresenter.TabLabels.Concat(GuidePanelLoad.PreviewStates).ToArray();

        public void ShowPreviewState(int index)
        {
            int tabs = StepBonusSettingsPresenter.TabLabels.Length;
            if (index >= tabs)
            {
                ShowLoading();
                if (index == tabs + 1)
                    ShowLoadFailed();
                return;
            }
            ShowTab(index);
            if (Data == null)
                return;
            foreach (var row in Rows)
                row.Button.gameObject.SetActive(
                    Array.Exists(Data.Owned, roll => roll.Id == row.Id)
                        && StepBonusSettingsPresenter.InTab(Data.Find(row.Id), index)
                );
        }

        private void OnDisable()
        {
            Load.Cancel();
            bindings.Clear();
            presenter?.Dispose();
            presenter = null;
        }

        public void Render(StepBonusSettingsState state)
        {
            var loadout = StepBonusSession.Loadout(Data);
            Set(Owned, state.OwnedText);
            for (int i = 0; i < Slots.Length && i < state.Slots.Count; i++)
                RenderSlot(Slots[i], state.Slots[i], loadout, i);
            ShowTab(state.Tab);

            var rows = new Dictionary<string, StepBonusRowState>();
            foreach (var row in state.Rows)
                rows[row.Id] = row;
            foreach (var widget in Rows)
            {
                bool known = rows.TryGetValue(widget.Id, out var row);
                widget.Button.gameObject.SetActive(known && row.Visible);
                if (!known)
                    continue;
                if (widget.SetMark != null)
                    widget.SetMark.SetActive(row.Set);
                var color = StepBonusArt.Rank(row.Rank);
                Set(widget.Rank, row.Rank.ToString());
                if (widget.Rank != null)
                    widget.Rank.color = color;
                if (widget.RankFrame != null)
                    widget.RankFrame.color = color;
                Set(widget.Effect, row.Effect);
            }

            // 一覧は持ち物の順（ランクの高い順）に並べ直す。
            var byId = new Dictionary<string, StepBonusRowWidget>();
            foreach (var widget in Rows)
                byId[widget.Id] = widget;
            for (int i = 0; i < state.Rows.Count; i++)
                if (byId.TryGetValue(state.Rows[i].Id, out var widget))
                    widget.Button.transform.SetSiblingIndex(i);
        }

        // 選んでいるタブにだけ金の下線を出し、文字を明るい金にする。
        private void ShowTab(int tab)
        {
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i == tab;
        }

        private void RenderSlot(
            StepBonusSlotWidget widget,
            StepBonusSlotState state,
            StepBonusLoadout loadout,
            int index
        )
        {
            var bonus = loadout.Definition(state.Bonus);
            if (widget.Icon != null)
            {
                widget.Icon.sprite = bonus?.Icon;
                widget.Icon.enabled = bonus != null && bonus.Icon != null;
                widget.Icon.color = state.Open ? Color.white : Closed;
            }
            if (widget.Frame != null && index < TierColors.Length)
                widget.Frame.color = state.Open ? TierColors[index] : TierColors[index] * Closed;
            if (widget.Selected != null)
                widget.Selected.SetActive(state.Selected);
            Set(widget.Tier, state.Tier);
            Set(widget.Name, state.Name);
            Set(widget.Effect, state.Effect);
            var faint = state.Open ? 1f : 0.55f;
            foreach (var label in new[] { widget.Name, widget.Effect })
                if (label != null)
                    label.alpha = faint;
        }
    }
}
