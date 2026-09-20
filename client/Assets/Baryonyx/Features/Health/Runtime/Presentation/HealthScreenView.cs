using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Health
{
    public sealed class HealthScreenView : MonoBehaviour
    {
        public TMP_Text Progress;
        public TMP_Text Status;
        public TMP_Text GoogleStatus;
        public TMP_Text Period;
        public TMP_Text Footnote;
        public UnityEngine.UI.Button SignInButton;
        public UnityEngine.UI.Button ConnectButton;
        public UnityEngine.UI.Button RefreshButton;
        public UnityEngine.UI.Button SignOutButton;
        public UnityEngine.UI.Button SettingsButton;
        public UnityEngine.UI.Button[] DayButtons;
        public TMP_Text[] DayLabels;
        public TMP_Text[] DayDates;
        public GameObject EmptyState;
        public ScrollRect MainScroll;
        public GameObject DetailsOverlay;
        public UnityEngine.UI.Button CopyButton;
        public UnityEngine.UI.Button CloseButton;
        public TMP_Text DetailsTitle;
        public TMP_Text JsonText;
        public ScrollRect JsonScroll;
        private HealthScreenPresenter presenter;
#if UNITY_EDITOR || !UNITY_ANDROID
        private bool preview;
#endif
        private IReadOnlyList<HealthDaySnapshot> renderedDays;
        private HealthJsonDetails details;
        private HealthScreenPhase? renderedPhase;
        private HealthRequirementCode renderedRequirement;
        private RectTransform revealAfterLayout;
        private GameObject rewardOverlay;
        private TMP_Text rewardOverlayTitle;
        private TMP_Text rewardOverlayBody;
        private TMP_Text rewardOverlayHistory;
        private Button rewardClaimButton;
        private Button rewardHistoryButton;
        private Button rewardCloseButton;
        private int renderedRewardClaimVersion;

        private void Awake()
        {
            CreateRewardUi();
            SignInButton.onClick.AddListener(() =>
            {
                if (presenter != null)
                    _ = presenter.SignInAsync();
            });
            ConnectButton.onClick.AddListener(() =>
            {
                if (presenter != null)
                    _ = presenter.ConnectAsync();
            });
            RefreshButton.onClick.AddListener(() =>
            {
                if (presenter != null)
                    _ = presenter.RefreshAsync();
            });
            SignOutButton.onClick.AddListener(() =>
            {
                if (presenter != null)
                    _ = presenter.SignOutAsync();
            });
            SettingsButton.onClick.AddListener(() => presenter?.OpenSettings());
            details = new HealthJsonDetails(
                DetailsOverlay,
                MainScroll,
                JsonScroll,
                DetailsTitle,
                JsonText,
                CopyButton,
                CloseButton,
                () => presenter?.CloseDetails()
            );
            for (int i = 0; i < DayButtons.Length; i++)
            {
                int index = i;
                DayButtons[i].onClick.AddListener(() => presenter?.SelectDay(index));
            }
        }

        public void Bind(HealthScreenPresenter value, bool preview = false)
        {
            if (presenter != null)
                presenter.Changed -= Render;
            presenter = value ?? throw new ArgumentNullException(nameof(value));
#if UNITY_EDITOR || !UNITY_ANDROID
            this.preview = preview;
#endif
            renderedDays = null;
            details.Reset();
            presenter.Changed += Render;
            Render();
        }

        private void Update() => details.HandleInput();

        private void LateUpdate()
        {
            if (revealAfterLayout == null || DetailsOverlay.activeSelf)
                return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                MainScroll.viewport,
                revealAfterLayout
            );
            var viewport = MainScroll.viewport.rect;
            if (bounds.min.y < viewport.yMin || bounds.max.y > viewport.yMax)
            {
                MainScroll.StopMovement();
                var position = MainScroll.content.anchoredPosition;
                position.y = Mathf.Clamp(
                    position.y + viewport.yMax - bounds.max.y,
                    0,
                    Mathf.Max(0, MainScroll.content.rect.height - viewport.height)
                );
                MainScroll.content.anchoredPosition = position;
            }
            revealAfterLayout = null;
        }

        private void Render()
        {
            bool detailOpen = presenter.SelectedDay != null;
#if UNITY_EDITOR || !UNITY_ANDROID
            details.Render(presenter.SelectedDay, preview);
#else
            details.Render(presenter.SelectedDay, false);
#endif
            RenderActions(detailOpen);
            RenderStatus();
            RenderDays(detailOpen);
            RenderRewards();
            RevealStatusOnChange(detailOpen);
#if UNITY_EDITOR || !UNITY_ANDROID
            if (preview)
                RenderPreview();
#endif
        }

        private void RenderActions(bool detailOpen)
        {
            bool showRefresh =
                presenter.Phase == HealthScreenPhase.Ready
                || presenter.Phase == HealthScreenPhase.Reading
                || (presenter.Phase == HealthScreenPhase.Failed && presenter.CanRefresh);
            SignInButton.GetComponentInChildren<TMP_Text>(true).text = "Googleに接続";
            SignOutButton.GetComponentInChildren<TMP_Text>(true).text = "Google接続を解除";
            SignInButton.gameObject.SetActive(!presenter.SignedIn);
            ConnectButton.gameObject.SetActive(!showRefresh || presenter.RequiresStepsPermission);
            ConnectButton.GetComponentInChildren<TMP_Text>(true).text =
                presenter.RequiresStepsPermission ? "歩数の読み取りを許可" : "運動データに接続";
            RefreshButton.gameObject.SetActive(showRefresh);
            SignOutButton.gameObject.SetActive(presenter.SignedIn);
            SignInButton.interactable = presenter.CanSignIn && !detailOpen;
            ConnectButton.interactable = presenter.CanConnect && !detailOpen;
            RefreshButton.interactable = presenter.CanRefresh && !detailOpen;
            SignOutButton.interactable = presenter.SignedIn && !presenter.IsBusy && !detailOpen;
            SettingsButton.gameObject.SetActive(presenter.CanOpenSettings);
            SettingsButton.interactable = !detailOpen;
            SettingsButton.GetComponentInChildren<TMP_Text>(true).text =
                presenter.RequirementNotice.Destination == HealthSettingsDestination.Device
                    ? "端末の設定を開く"
                    : "Health Connectの設定を開く";
            if (rewardClaimButton != null)
            {
                rewardClaimButton.gameObject.SetActive(presenter.RewardsEnabled);
                rewardClaimButton.interactable = presenter.CanClaimRewards && !detailOpen;
                rewardClaimButton.GetComponentInChildren<TMP_Text>(true).text =
                    $"ルーンを取得（残高 {presenter.RuneBalance:N0}）";
            }
            if (rewardHistoryButton != null)
            {
                rewardHistoryButton.gameObject.SetActive(presenter.RewardsEnabled);
                rewardHistoryButton.interactable = !detailOpen && !presenter.IsBusy;
            }
        }

        private void RenderRewards()
        {
            if (rewardOverlay == null || !presenter.RewardsEnabled)
                return;
            if (presenter.RewardClaimVersion > renderedRewardClaimVersion)
            {
                renderedRewardClaimVersion = presenter.RewardClaimVersion;
                rewardOverlayTitle.text =
                    presenter.LastGrantedRunes > 0 ? "ルーンを取得しました" : "ルーンの確認結果";
                rewardOverlayBody.text = presenter.RewardMessage;
                rewardOverlayHistory.text = FormatRewardHistory();
                rewardOverlay.SetActive(true);
            }
            else if (rewardOverlay.activeSelf)
            {
                rewardOverlayBody.text = presenter.RewardMessage;
                rewardOverlayHistory.text = FormatRewardHistory();
            }
        }

        private string FormatRewardHistory()
        {
            var history = presenter.RewardDays;
            if (history == null || history.Count == 0)
                return "直近7日分のルーン履歴はありません。";
            var text = new StringBuilder("直近7日分のルーン履歴\n");
            foreach (var day in history)
            {
                text.Append(day.day);
                text.Append("  ");
                text.Append(day.creditedRunes.ToString("N0", CultureInfo.InvariantCulture));
                text.Append(" ルーン（");
                text.Append(day.creditedThroughValue.ToString("N0", CultureInfo.InvariantCulture));
                text.Append("歩）\n");
            }
            return text.ToString().TrimEnd();
        }

        private void CreateRewardUi()
        {
            var root = transform as RectTransform;
            if (root == null)
                return;
            rewardClaimButton = CreateButton(
                "RewardClaimButton",
                root,
                new Vector2(1, 1),
                new Vector2(1, 1),
                new Vector2(-24, -24),
                new Vector2(260, 56),
                "ルーンを取得"
            );
            rewardClaimButton.onClick.AddListener(() => _ = presenter?.ClaimRewardsAsync());
            rewardHistoryButton = CreateButton(
                "RewardHistoryButton",
                root,
                new Vector2(1, 1),
                new Vector2(1, 1),
                new Vector2(-24, -88),
                new Vector2(260, 48),
                "ルーン履歴"
            );
            rewardHistoryButton.onClick.AddListener(() =>
            {
                if (rewardOverlay != null)
                {
                    rewardOverlayTitle.text = "ルーン履歴";
                    rewardOverlayBody.text = presenter?.RewardMessage ?? "";
                    rewardOverlayHistory.text = FormatRewardHistory();
                    rewardOverlay.SetActive(true);
                }
            });
            rewardOverlay = new GameObject("RewardOverlay", typeof(RectTransform), typeof(Image));
            rewardOverlay.transform.SetParent(root, false);
            var overlayRect = (RectTransform)rewardOverlay.transform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            rewardOverlay.GetComponent<Image>().color = new Color(0, 0, 0, 0.72f);
            var panel = new GameObject("RewardPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(rewardOverlay.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.12f, 0.16f);
            panelRect.anchorMax = new Vector2(0.88f, 0.84f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.14f, 0.98f);
            rewardOverlayTitle = CreateText("Title", panel.transform, 30, TextAlignmentOptions.Top);
            rewardOverlayTitle.rectTransform.anchorMin = new Vector2(0.06f, 0.78f);
            rewardOverlayTitle.rectTransform.anchorMax = new Vector2(0.94f, 0.96f);
            rewardOverlayTitle.rectTransform.offsetMin = Vector2.zero;
            rewardOverlayTitle.rectTransform.offsetMax = Vector2.zero;
            rewardOverlayBody = CreateText("Body", panel.transform, 24, TextAlignmentOptions.Top);
            rewardOverlayBody.rectTransform.anchorMin = new Vector2(0.08f, 0.61f);
            rewardOverlayBody.rectTransform.anchorMax = new Vector2(0.92f, 0.78f);
            rewardOverlayBody.rectTransform.offsetMin = Vector2.zero;
            rewardOverlayBody.rectTransform.offsetMax = Vector2.zero;
            rewardOverlayHistory = CreateText(
                "History",
                panel.transform,
                20,
                TextAlignmentOptions.TopLeft
            );
            rewardOverlayHistory.rectTransform.anchorMin = new Vector2(0.1f, 0.18f);
            rewardOverlayHistory.rectTransform.anchorMax = new Vector2(0.9f, 0.6f);
            rewardOverlayHistory.rectTransform.offsetMin = Vector2.zero;
            rewardOverlayHistory.rectTransform.offsetMax = Vector2.zero;
            rewardCloseButton = CreateButton(
                "Close",
                panel.transform,
                new Vector2(0.3f, 0.04f),
                new Vector2(0.7f, 0.16f),
                Vector2.zero,
                Vector2.zero,
                "閉じる"
            );
            rewardCloseButton.onClick.AddListener(() => rewardOverlay.SetActive(false));
            rewardOverlay.SetActive(false);
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            float size,
            TextAlignmentOptions alignment
        )
        {
            var text = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            ).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size,
            string label
        )
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            buttonObject.transform.SetParent(parent, false);
            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.35f, 0.58f, 0.98f);
            var text = CreateText("Label", buttonObject.transform, 20, TextAlignmentOptions.Center);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.text = label;
            return buttonObject.GetComponent<Button>();
        }

        private void RenderStatus()
        {
            GoogleStatus.text = presenter.GoogleMessage;
            Status.text = presenter.StatusMessage;
            Status.gameObject.SetActive(
                presenter.Phase != HealthScreenPhase.Ready
                    || presenter.HasReadFailures
                    || presenter.RequiresStepsPermission
                    || presenter.RequirementNotice.HasNotice
            );
            Progress.text = presenter.Phase switch
            {
                HealthScreenPhase.Connecting => "Health Connectに接続中",
                HealthScreenPhase.Reading => "健康データを取得中",
                HealthScreenPhase.Ready => "Health Connectに接続済み",
                _ => "Health Connect接続",
            };
        }

        private void RenderDays(bool detailOpen)
        {
            if (!ReferenceEquals(renderedDays, presenter.Days))
            {
                renderedDays = presenter.Days;
                for (int i = 0; i < DayButtons.Length; i++)
                {
                    bool exists = i < renderedDays.Count;
                    DayButtons[i].gameObject.SetActive(exists);
                    if (!exists)
                    {
                        DayLabels[i].text = "";
                        DayDates[i].text = "";
                        continue;
                    }
                    var day = renderedDays[i];
                    var date = DateTime.ParseExact(
                        day.Day,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture
                    );
                    string count =
                        day.HasValue
                            ? day.Steps.ToString("N0", CultureInfo.InvariantCulture) + " 歩"
                        : day.StepsStatus == "permission_required" ? "歩数は未許可"
                        : day.StepsStatus == "failed" ? "歩数の取得失敗"
                        : day.HasAdditionalData ? "歩数なし・JSONあり"
                        : "データなし";
                    DayDates[i].text = $"{date:MM/dd}（{"日月火水木金土"[(int)date.DayOfWeek]}）";
                    DayLabels[i].text = count;
                }
                bool hasDays = renderedDays.Count > 0;
                EmptyState.SetActive(!hasDays);
                Period.text = hasDays
                    ? $"{renderedDays[^1].Day}  〜  {renderedDays[0].Day}"
                    : "今日を含む直近7日間";
                Footnote.text = hasDays
                    ? $"{renderedDays[0].Zone}  /  今日は取得時点まで。日付を選ぶと、ほかの健康データもJSONで確認できます。"
                    : "健康データは画面を開いている間だけ保持します。";
            }
            Period.gameObject.SetActive(presenter.Days.Count > 0);
            foreach (var button in DayButtons)
                button.interactable = !presenter.IsBusy && !detailOpen;
        }

        private void RevealStatusOnChange(bool detailOpen)
        {
            if (
                (
                    renderedPhase != presenter.Phase
                    || renderedRequirement != presenter.RequirementNotice.Code
                ) && !detailOpen
            )
                revealAfterLayout =
                    presenter.Phase == HealthScreenPhase.Ready
                    && !presenter.RequiresStepsPermission
                    && !presenter.RequirementNotice.HasNotice
                        ? Period.rectTransform
                        : Progress.rectTransform;
            renderedPhase = presenter.Phase;
            renderedRequirement = presenter.RequirementNotice.Code;
        }

#if UNITY_EDITOR || !UNITY_ANDROID
        private void RenderPreview()
        {
            SignInButton.GetComponentInChildren<TMP_Text>(true).text =
                "Google接続を試す（サンプル）";
            SignOutButton.GetComponentInChildren<TMP_Text>(true).text =
                "Google接続を解除（サンプル）";
            Progress.text = "サンプルデータ / プレビュー";
            ConnectButton.GetComponentInChildren<TMPro.TMP_Text>(true).text =
                "サンプルデータを表示";
            GoogleStatus.text = presenter.SignedIn
                ? "Google接続済みのサンプルです。実際の認証は行いません。"
                : "未接続のサンプルです。歩数の表示とは別に操作できます。";
            Footnote.text = "架空の健康データです。日付を選ぶと血圧のサンプルJSONも確認できます。";
            Status.text = presenter.Phase switch
            {
                HealthScreenPhase.ReadyToConnect =>
                    "サンプルデータを表示して、日別の歩数を確認できます。",
                HealthScreenPhase.Ready => "日付を選ぶとサンプルJSONを確認できます。",
                HealthScreenPhase.Failed =>
                    "プレビューを表示できませんでした。もう一度お試しください。",
                _ => "サンプルデータを準備しています…",
            };
        }
#endif

        private void OnDestroy()
        {
            if (presenter != null)
                presenter.Changed -= Render;
            details?.Dispose();
        }
    }
}
