using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Home
{
    /// <summary>
    /// Draws a <see cref="HomeViewState"/> into the generated prefab and reports button
    /// presses as <see cref="HomeAction"/> values. It owns no game rules.
    /// </summary>
    public sealed class HomeView : MonoBehaviour
    {
        public static readonly Color GaugeFilled = new(0.37f, 0.83f, 0.78f);
        public static readonly Color GaugeAchieved = new(0.95f, 0.76f, 0.31f);
        public static readonly Color GaugeEmpty = new(0.95f, 0.91f, 0.82f, 0.14f);
        public static readonly Color TextMain = new(0.95f, 0.91f, 0.82f);
        public static readonly Color TextSub = new(0.79f, 0.75f, 0.66f);

        [Header("歩数")]
        public Button StepButton;
        public TMP_Text DateLabel;
        public GameObject StepDetails;
        public GameObject UnlinkedDetails;
        public TMP_Text StepsLabel;
        public TMP_Text GoalLabel;
        public Image[] Segments = Array.Empty<Image>();
        public TMP_Text RemainingLabel;
        public TMP_Text WeeklyLabel;
        public TMP_Text ClaimLabel;

        [Header("右上")]
        public TMP_Text RunesLabel;
        public Button SettingsButton;

        [Header("世界")]
        public Button PartyWorldButton;

        [Header("左下")]
        public Button PartyButton;
        public Button EquipmentButton;
        public Button SummonButton;
        public Button GoalsButton;

        [Header("右下")]
        public Button WorldMapButton;
        public Button ResumeButton;
        public TMP_Text DestinationNameLabel;
        public TMP_Text DestinationFloorLabel;

        [Header("通知")]
        public CanvasGroup Toast;
        public TMP_Text ToastLabel;

        [Min(0.1f)]
        public float ToastSeconds = 1.6f;

        [Min(0f)]
        public float ToastFadeSeconds = 0.3f;

        private readonly List<(Button button, UnityEngine.Events.UnityAction listener)> bindings =
            new();
        private Coroutine toastRoutine;

        public event Action<HomeAction> ActionRequested;

        public string CurrentToast => Toast != null && Toast.alpha > 0 ? ToastLabel.text : "";

        private void OnEnable()
        {
            Bind(StepButton, HomeAction.SyncSteps);
            Bind(SettingsButton, HomeAction.Settings);
            Bind(PartyWorldButton, HomeAction.Party);
            Bind(PartyButton, HomeAction.Party);
            Bind(EquipmentButton, HomeAction.Equipment);
            Bind(SummonButton, HomeAction.Summon);
            Bind(GoalsButton, HomeAction.Goals);
            Bind(WorldMapButton, HomeAction.WorldMap);
            Bind(ResumeButton, HomeAction.Resume);
            HideToast();
        }

        private void OnDisable()
        {
            foreach (var (button, listener) in bindings)
                if (button != null)
                    button.onClick.RemoveListener(listener);
            bindings.Clear();
            toastRoutine = null;
        }

        public void Render(HomeViewState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            Set(DateLabel, state.DateText);
            if (StepDetails != null)
                StepDetails.SetActive(state.ShowSteps);
            if (UnlinkedDetails != null)
                UnlinkedDetails.SetActive(!state.ShowSteps);
            Set(StepsLabel, state.StepsText);
            if (StepsLabel != null)
                StepsLabel.color = state.DailyAchieved ? GaugeAchieved : TextMain;
            Set(GoalLabel, state.GoalText);
            for (int i = 0; i < Segments.Length; i++)
            {
                if (Segments[i] == null)
                    continue;
                Segments[i].color =
                    i >= state.FilledSegments ? GaugeEmpty
                    : state.DailyAchieved ? GaugeAchieved
                    : GaugeFilled;
            }
            Set(RemainingLabel, state.RemainingText);
            if (RemainingLabel != null)
                RemainingLabel.color = state.DailyAchieved ? GaugeAchieved : TextSub;
            Set(WeeklyLabel, state.WeeklyText);
            Set(ClaimLabel, state.ClaimText);
            Set(RunesLabel, state.RunesText);
            Set(DestinationNameLabel, state.DestinationNameText);
            Set(DestinationFloorLabel, state.DestinationFloorText);
        }

        public void ShowToast(string message)
        {
            if (Toast == null || ToastLabel == null || string.IsNullOrEmpty(message))
                return;
            ToastLabel.text = message;
            Toast.alpha = 1f;
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastRoutine = isActiveAndEnabled ? StartCoroutine(FadeToast()) : null;
        }

        public void HideToast()
        {
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastRoutine = null;
            if (Toast != null)
                Toast.alpha = 0f;
        }

        private IEnumerator FadeToast()
        {
            yield return new WaitForSecondsRealtime(ToastSeconds);
            float elapsed = 0f;
            while (elapsed < ToastFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                Toast.alpha = 1f - Mathf.Clamp01(elapsed / ToastFadeSeconds);
                yield return null;
            }
            Toast.alpha = 0f;
            toastRoutine = null;
        }

        private void Bind(Button button, HomeAction action)
        {
            if (button == null)
                return;
            UnityEngine.Events.UnityAction listener = () => ActionRequested?.Invoke(action);
            button.onClick.AddListener(listener);
            bindings.Add((button, listener));
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }
    }
}
