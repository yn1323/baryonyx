using System;
using System.Collections.Generic;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.StepBonus
{
    /// <summary>One slot on the bonus settings, baked by the generator.</summary>
    [Serializable]
    public sealed class StepBonusSlotWidget
    {
        public Button Button;
        public Image Frame;
        public Image Icon;
        public GameObject Selected;
        public GameObject Partner;
        public TMP_Text Label;
    }

    /// <summary>One owned bonus row, baked by the generator.</summary>
    [Serializable]
    public sealed class StepBonusRowWidget
    {
        public string Id = "";
        public Button Button;
        public GameObject Selected;
        public TMP_Text Note;
    }

    /// <summary>
    /// The tavern's bonus settings on the right of the guide screen: five slots, the owned
    /// bonuses and the button that sets the chosen one. StepBonusAssets bakes the slots and rows
    /// into the tavern prefab, so they read in the editor; this view wires them up and drives
    /// its own presenter from the shared <see cref="StepBonusSession"/>.
    /// </summary>
    public sealed class StepBonusSettingsView : MonoBehaviour, IStepBonusSettingsView
    {
        // 段階に届いていない枠は、アイコンと枠を暗くする。
        public static readonly Color Closed = new(0.42f, 0.42f, 0.48f, 1f);

        public StepBonusMockData Data;

        // 通知は案内人の画面と同じものを使う。
        public GuideMenuView Guide;

        public TMP_Text Header;
        public TMP_Text Owned;
        public StepBonusSlotWidget[] Slots = Array.Empty<StepBonusSlotWidget>();
        public Color[] TierColors = Array.Empty<Color>();
        public Button[] Tabs = Array.Empty<Button>();
        public ScrollRect List;
        public StepBonusRowWidget[] Rows = Array.Empty<StepBonusRowWidget>();
        public TMP_Text FooterTitle;
        public TMP_Text FooterDetail;
        public Button Confirm;
        public TMP_Text ConfirmLabel;

        private StepBonusSettingsPresenter presenter;
        private readonly List<(Button Button, UnityEngine.Events.UnityAction Action)> bindings =
            new();

        public event Action<int> SlotPressed;
        public event Action<string> BonusPressed;
        public event Action<int> TabPressed;
        public event Action ConfirmPressed;

        public StepBonusSettingsPresenter Presenter => presenter;
        public string LastToast { get; private set; } = "";

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
            Bind(Confirm, () => ConfirmPressed?.Invoke());

            // 開くたびに、ホームから受け取った今日のUPTと共有の枠で描き直す。
            presenter?.Dispose();
            presenter =
                Data != null
                    ? new StepBonusSettingsPresenter(
                        this,
                        StepBonusSession.Loadout(Data),
                        StepBonusSession.UptOr(Data)
                    )
                    : null;
            if (List != null)
                List.verticalNormalizedPosition = 1f;
        }

        private void OnDisable()
        {
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
            Set(Header, state.HeaderText);
            Set(Owned, state.OwnedText);
            for (int i = 0; i < Slots.Length && i < state.Slots.Count; i++)
                RenderSlot(Slots[i], state.Slots[i], loadout, i);
            for (int i = 0; i < Tabs.Length; i++)
                SetActive(Tabs[i].transform.Find("Selected"), i == state.Tab);

            var rows = new Dictionary<string, StepBonusRowState>();
            foreach (var row in state.Rows)
                rows[row.Id] = row;
            foreach (var widget in Rows)
            {
                bool known = rows.TryGetValue(widget.Id, out var row);
                widget.Button.gameObject.SetActive(known && row.Visible);
                if (!known)
                    continue;
                SetActive(widget.Selected, row.Selected);
                Set(widget.Note, row.Note);
                if (widget.Note != null)
                    widget.Note.color = row.Updated ? StepBonusArt.Teal : StepBonusArt.TextFaint;
            }

            Set(FooterTitle, state.FooterTitle);
            Set(FooterDetail, state.FooterDetail);
            Set(ConfirmLabel, state.ConfirmLabel);
            if (Confirm != null)
                Confirm.interactable = state.CanConfirm;
        }

        public void ShowToast(string message)
        {
            LastToast = message ?? "";
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
            SetActive(widget.Selected, state.Selected);
            SetActive(widget.Partner, state.Partner);
            Set(widget.Label, state.Label);
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;
            button.onClick.AddListener(action);
            bindings.Add((button, action));
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null)
                component.gameObject.SetActive(active);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }
    }
}
