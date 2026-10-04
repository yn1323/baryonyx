using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    /// The tavern's bonus settings over the whole guide screen: the slots and today's effects on
    /// the left, the owned bonuses on the right. StepBonusAssets bakes the rows into the tavern
    /// prefab, so they read in the editor; this view wires them up and drives its own presenter.
    /// When the app has a server (<see cref="StepBonusSession.Source"/>), it reads the player's
    /// bonuses on opening and saves every change there; otherwise it uses the mock data.
    /// Changes are told on the guide screen's notice band.
    /// </summary>
    public sealed class StepBonusSettingsView : MonoBehaviour, IStepBonusSettingsView
    {
        // 段階に届いていない枠は、アイコン・枠・文字を暗くする。
        public static readonly Color Closed = new(0.42f, 0.42f, 0.48f, 1f);

        public const string LoadingText = "読み込み中…";
        public const string LoadFailedText = "取得できませんでした";
        public const string LoadFailedMessage = "ボーナスを取得できませんでした";

        public StepBonusMockData Data;

        // 通知は案内人の画面の通知の帯に出す。
        public GuideMenuView Guide;

        public TMP_Text Owned;
        public StepBonusSlotWidget[] Slots = Array.Empty<StepBonusSlotWidget>();
        public Color[] TierColors = Array.Empty<Color>();
        public Button[] Tabs = Array.Empty<Button>();
        public ScrollRect List;
        public StepBonusRowWidget[] Rows = Array.Empty<StepBonusRowWidget>();

        private StepBonusSettingsPresenter presenter;
        private CancellationTokenSource loading;
        private readonly List<(Button Button, UnityEngine.Events.UnityAction Action)> bindings =
            new();

        public event Action<int> SlotPressed;
        public event Action<string> BonusPressed;
        public event Action<int> TabPressed;

        public StepBonusSettingsPresenter Presenter => presenter;
        public string LastNotice { get; private set; } = "";

        // 実行中または直前の読み込み。テストで完了を待つために公開する。
        public Task LoadTask { get; private set; } = Task.CompletedTask;

        private void OnEnable()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                Bind(Slots[i].Button, () => SlotPressed?.Invoke(index));
            }
            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                Bind(Tabs[i], () => TabPressed?.Invoke(index));
            }
            foreach (var row in Rows)
            {
                string id = row.Id;
                Bind(row.Button, () => BonusPressed?.Invoke(id));
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
            loading = new CancellationTokenSource();
            LoadTask = LoadAsync(source, loading.Token);
        }

        private async Task LoadAsync(IStepBonusSource source, CancellationToken token)
        {
            try
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
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (token.IsCancellationRequested)
                    return;
                Debug.LogWarning("UPTボーナスを取得できませんでした。" + exception.Message, this);
                Set(Owned, LoadFailedText);
                ShowNotice(LoadFailedMessage);
            }
        }

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
                StepBonusSession.UptOr(Data),
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

        private void OnDisable()
        {
            loading?.Cancel();
            loading?.Dispose();
            loading = null;
            foreach (var (button, action) in bindings)
                if (button != null)
                    button.onClick.RemoveListener(action);
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
            for (int i = 0; i < Tabs.Length; i++)
            {
                var selected = Tabs[i].transform.Find("Selected");
                if (selected != null)
                    selected.gameObject.SetActive(i == state.Tab);
            }

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

        public void ShowNotice(string message)
        {
            LastNotice = message ?? "";
            if (Guide != null)
                Guide.ShowToast(message);
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

        private void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;
            button.onClick.AddListener(action);
            bindings.Add((button, action));
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }
    }
}
