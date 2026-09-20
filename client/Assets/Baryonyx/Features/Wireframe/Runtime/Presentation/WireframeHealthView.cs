using System;
using System.Linq;
using System.Text;
using Baryonyx.Health;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Wireframe
{
    public sealed class WireframeHealthView : MonoBehaviour
    {
        public RectTransform[] Bars = new RectTransform[7];
        public TMP_Text[] Dates = new TMP_Text[7];
        public TMP_Text[] Values = new TMP_Text[7];
        public TMP_Text HomeSteps,
            Status,
            Total,
            Coverage,
            Detail;
        public UnityEngine.UI.Button Connect,
            Refresh,
            Settings;
        public UnityEngine.UI.Button[] Days = new UnityEngine.UI.Button[7];
        public UnityEngine.UI.Button CloseDetails;
        public GameObject DetailsOverlay;
        public UnityEngine.UI.ScrollRect DetailsScroll;
        private HealthScreenPresenter presenter;
        private bool preview;
        private bool showDetails;
        private HealthDaySnapshot[] days = Array.Empty<HealthDaySnapshot>();
        private GameObject rewardOverlay;
        private TMP_Text rewardTitle;
        private TMP_Text rewardBody;
        private TMP_Text rewardHistory;
        private Button rewardSignIn;
        private Button rewardClaim;
        private Button rewardHistoryButton;
        private Button rewardClose;
        private int renderedRewardClaimVersion;
        private WireframeView wireframe;
        public bool DetailsOpen => showDetails;
        public long? TodaySteps =>
            days.LastOrDefault() is { HasValue: true } day ? day.Steps : null;

        private void Awake() => CreateRewardUi();

        public void Bind(HealthScreenPresenter value, bool sample)
        {
            Unbind();
            presenter = value;
            wireframe = GetComponent<WireframeView>();
            preview = sample;
            Connect.onClick.AddListener(ConnectHealth);
            Refresh.onClick.AddListener(RefreshHealth);
            Settings.onClick.AddListener(OpenSettings);
            if (rewardSignIn != null)
                rewardSignIn.onClick.AddListener(SignInRewards);
            if (rewardClaim != null)
                rewardClaim.onClick.AddListener(ClaimRewards);
            if (rewardHistoryButton != null)
                rewardHistoryButton.onClick.AddListener(OpenRewardHistory);
            if (rewardClose != null)
                rewardClose.onClick.AddListener(CloseRewardOverlay);
            CloseDetails.onClick.AddListener(Close);
            for (int i = 0; i < Days.Length; i++)
            {
                int index = i;
                Days[i].onClick.AddListener(() => Select(index));
            }
            presenter.Changed += Render;
            Render();
        }

        private void ConnectHealth() => _ = presenter.ConnectAsync();

        private void RefreshHealth() => _ = presenter.RefreshAsync();

        private void OpenSettings() => presenter.OpenSettings();

        private void Select(int index)
        {
            if (index >= days.Length)
                return;
            Detail.text = days[index].Json;
            DetailsOverlay.SetActive(true);
            showDetails = true;
            Canvas.ForceUpdateCanvases();
            DetailsScroll.verticalNormalizedPosition = 1;
        }

        public void Close()
        {
            showDetails = false;
            DetailsOverlay.SetActive(false);
        }

        private void Render()
        {
            days = HealthWeekSummary.Chronological(presenter.Days);
            long maximum = HealthWeekSummary.Maximum(days);
            var today = days.LastOrDefault();
            HomeSteps.text =
                HealthWeekSummary.StepsLabel(today) + (today?.HasValue == true ? " 歩" : "");
            Status.text = (preview ? "Editor用サンプル\n" : "") + presenter.StatusMessage;
            if (presenter.RewardsEnabled)
                Status.text += "\n" + presenter.RewardMessage;
            Total.text =
                days.Length == 0 ? "直近7日間" : $"7日間で {HealthWeekSummary.Total(days):N0} 歩";
            Coverage.text = days.Any(day => !day.HasValue) ? "記録が取得できた日の合計です。" : "";
            for (int i = 0; i < 7; i++)
            {
                var day = i < days.Length ? days[i] : null;
                Dates[i].text = day == null ? "—" : day.Day.Substring(5).Replace('-', '/');
                Values[i].text = HealthWeekSummary.StepsLabel(day);
                Bars[i].anchorMax = new Vector2(1, HealthWeekSummary.Fraction(day, maximum));
                Bars[i].gameObject.SetActive(day?.HasValue == true && day.Steps > 0);
                Days[i].interactable = day != null;
            }
            Connect.interactable = presenter.CanConnect;
            Refresh.interactable = presenter.CanRefresh;
            Settings.interactable = presenter.CanOpenSettings;
            RenderHomeAccountState();
            bool showRewardActions = wireframe == null || wireframe.Session.Screen == WireScreen.Health;
            if (rewardSignIn != null)
            {
                rewardSignIn.gameObject.SetActive(
                    showRewardActions && presenter.RewardsEnabled && !presenter.SignedIn
                );
                rewardSignIn.interactable = presenter.CanSignIn;
            }
            if (rewardClaim != null)
            {
                rewardClaim.gameObject.SetActive(
                    showRewardActions && presenter.RewardsEnabled && presenter.SignedIn
                );
                rewardClaim.interactable = presenter.CanClaimRewards;
                rewardClaim.GetComponentInChildren<TMP_Text>(true).text =
                    $"ルーン取得（{presenter.RuneBalance:N0}）";
            }
            if (rewardHistoryButton != null)
            {
                rewardHistoryButton.gameObject.SetActive(
                    showRewardActions && presenter.RewardsEnabled && presenter.SignedIn
                );
                rewardHistoryButton.interactable = !presenter.IsBusy;
            }
            if (
                rewardOverlay != null
                && presenter.RewardsEnabled
                && presenter.RewardClaimVersion > renderedRewardClaimVersion
            )
            {
                renderedRewardClaimVersion = presenter.RewardClaimVersion;
                rewardTitle.text = presenter.LastGrantedRunes > 0
                    ? "ルーンを取得しました"
                    : "ルーンの確認結果";
                rewardBody.text = presenter.RewardMessage;
                rewardHistory.text = FormatRewardHistory();
                rewardOverlay.SetActive(true);
            }
            if (days.Length == 0 && showDetails)
            {
                Detail.text = "";
                Close();
            }
        }

        private void RenderHomeAccountState()
        {
            if (wireframe == null || presenter == null)
                return;

            bool rewardsEnabled = presenter.RewardsEnabled;
            long balance = rewardsEnabled
                ? presenter.SignedIn ? presenter.RuneBalance : 0
                : wireframe.Session.Runes;
            if (rewardsEnabled)
                wireframe.Session.SetRuneBalance(balance);
            if (wireframe.Session.Screen != WireScreen.Home)
                return;

            string auth = rewardsEnabled
                ? presenter.SignedIn ? "Google認証：接続済み" : "Google認証：未接続"
                : preview ? "Google認証：Editorプレビュー" : "Google認証：未設定";
            if (wireframe.TryText("HomeGoogle", out var google))
            {
                if (wireframe.TryText("HomeRunes", out var runes))
                    runes.text = $"ルーン  {balance:N0}";
                google.text = auth;
            }
            else if (wireframe.TryText("HomeRunes", out var runes))
                runes.text = $"ルーン  {balance:N0}  /  {auth}";
        }

        private void Unbind()
        {
            if (presenter == null)
                return;
            presenter.Changed -= Render;
            Connect.onClick.RemoveListener(ConnectHealth);
            Refresh.onClick.RemoveListener(RefreshHealth);
            Settings.onClick.RemoveListener(OpenSettings);
            if (rewardSignIn != null)
                rewardSignIn.onClick.RemoveListener(SignInRewards);
            if (rewardClaim != null)
                rewardClaim.onClick.RemoveListener(ClaimRewards);
            if (rewardHistoryButton != null)
                rewardHistoryButton.onClick.RemoveListener(OpenRewardHistory);
            if (rewardClose != null)
                rewardClose.onClick.RemoveListener(CloseRewardOverlay);
            CloseDetails.onClick.RemoveListener(Close);
            foreach (var day in Days)
                day.onClick.RemoveAllListeners();
            presenter = null;
            wireframe = null;
        }

        private void OnDestroy() => Unbind();

        private void SignInRewards() => _ = presenter.SignInAsync();

        private void ClaimRewards() => _ = presenter.ClaimRewardsAsync();

        private void CloseRewardOverlay()
        {
            if (rewardOverlay != null)
                rewardOverlay.SetActive(false);
        }

        private void OpenRewardHistory()
        {
            if (rewardOverlay == null)
                return;
            rewardTitle.text = "ルーン履歴";
            rewardBody.text = presenter.RewardMessage;
            rewardHistory.text = FormatRewardHistory();
            rewardOverlay.SetActive(true);
        }

        private string FormatRewardHistory()
        {
            var history = presenter?.RewardDays;
            if (history == null || history.Count == 0)
                return "直近7日分の履歴はありません。";
            var text = new StringBuilder("直近7日分\n");
            foreach (var day in history)
            {
                text.Append(day.day);
                text.Append("  ");
                text.Append(day.creditedRunes.ToString("N0"));
                text.Append("ルーン（");
                text.Append(day.creditedThroughValue.ToString("N0"));
                text.Append("歩）\n");
            }
            return text.ToString().TrimEnd();
        }

        private void CreateRewardUi()
        {
            var root = transform as RectTransform;
            if (root == null)
                return;
            rewardSignIn = CreateButton(
                "RewardSignIn",
                root,
                new Vector2(0, 0),
                new Vector2(0, 0),
                new Vector2(24, 24),
                new Vector2(220, 48),
                "Googleに接続"
            );
            rewardClaim = CreateButton(
                "RewardClaim",
                root,
                new Vector2(0, 0),
                new Vector2(0, 0),
                new Vector2(252, 24),
                new Vector2(220, 48),
                "ルーン取得"
            );
            rewardHistoryButton = CreateButton(
                "RewardHistory",
                root,
                new Vector2(0, 0),
                new Vector2(0, 0),
                new Vector2(480, 24),
                new Vector2(180, 48),
                "ルーン履歴"
            );
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
            panelRect.anchorMin = new Vector2(0.2f, 0.16f);
            panelRect.anchorMax = new Vector2(0.8f, 0.84f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.14f, 0.98f);
            rewardTitle = CreateText("Title", panel.transform, 28, TextAlignmentOptions.Top);
            SetFullRect(rewardTitle.rectTransform, new Vector2(0.06f, 0.8f), new Vector2(0.94f, 0.96f));
            rewardBody = CreateText("Body", panel.transform, 22, TextAlignmentOptions.Top);
            SetFullRect(rewardBody.rectTransform, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.8f));
            rewardHistory = CreateText("History", panel.transform, 18, TextAlignmentOptions.TopLeft);
            SetFullRect(rewardHistory.rectTransform, new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.6f));
            rewardClose = CreateButton(
                "Close",
                panel.transform,
                new Vector2(0.32f, 0.04f),
                new Vector2(0.68f, 0.16f),
                Vector2.zero,
                Vector2.zero,
                "閉じる"
            );
            rewardOverlay.SetActive(false);
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            float size,
            TextAlignmentOptions alignment
        )
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static void SetFullRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.35f, 0.58f, 0.98f);
            var text = CreateText("Label", buttonObject.transform, 18, TextAlignmentOptions.Center);
            SetFullRect(text.rectTransform, Vector2.zero, Vector2.one);
            text.text = label;
            return buttonObject.GetComponent<Button>();
        }
    }
}
