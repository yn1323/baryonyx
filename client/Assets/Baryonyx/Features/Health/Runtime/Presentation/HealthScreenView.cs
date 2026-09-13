using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
        private HealthDaySnapshot renderedDetail;
        private GameObject previousSelection;
        private HealthScreenPhase? renderedPhase;
        private HealthRequirementCode renderedRequirement;
        private RectTransform revealAfterLayout;

        private void Awake()
        {
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
            CopyButton.onClick.AddListener(CopyJson);
            CloseButton.onClick.AddListener(() => presenter?.CloseDetails());
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
            renderedDetail = null;
            presenter.Changed += Render;
            Render();
        }

        private void Update()
        {
            if (
                DetailsOverlay.activeSelf
                && Keyboard.current?.escapeKey.wasPressedThisFrame == true
            )
                presenter?.CloseDetails();
        }

        private void CopyJson()
        {
            if (!DetailsOverlay.activeSelf || presenter?.SelectedDay == null)
                return;
            GUIUtility.systemCopyBuffer = presenter.SelectedDay.Json;
            CopyButton.GetComponentInChildren<TMP_Text>(true).text = "コピーしました";
        }

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
            if (detailOpen && !DetailsOverlay.activeSelf)
            {
                var events = UnityEngine.EventSystems.EventSystem.current;
                previousSelection = events != null ? events.currentSelectedGameObject : null;
            }
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
            if (detailOpen && !DetailsOverlay.activeSelf)
            {
                MainScroll.StopMovement();
                DetailsOverlay.SetActive(true);
                CloseButton.Select();
            }
            else if (!detailOpen && DetailsOverlay.activeSelf)
            {
                DetailsOverlay.SetActive(false);
                JsonText.text = "";
                var events = UnityEngine.EventSystems.EventSystem.current;
                if (
                    previousSelection != null
                    && previousSelection.activeInHierarchy
                    && events != null
                )
                    events.SetSelectedGameObject(previousSelection);
                previousSelection = null;
            }
            MainScroll.enabled = !detailOpen;
            if (detailOpen && !ReferenceEquals(renderedDetail, presenter.SelectedDay))
            {
                DetailsTitle.text = presenter.SelectedDay.Day + "  /  JSON";
                JsonText.text = presenter.SelectedDay.Json;
                CopyButton.GetComponentInChildren<TMP_Text>(true).text = "JSONをコピー";
                Canvas.ForceUpdateCanvases();
                JsonScroll.normalizedPosition = new Vector2(0, 1);
            }
            renderedDetail = presenter.SelectedDay;
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
#if UNITY_EDITOR || !UNITY_ANDROID
            if (preview)
                RenderPreview();
#endif
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
            if (presenter.SelectedDay != null)
                DetailsTitle.text = presenter.SelectedDay.Day + " / サンプルJSON";
        }
#endif

        private void OnDestroy()
        {
            if (presenter != null)
                presenter.Changed -= Render;
            if (JsonText != null)
                JsonText.text = "";
        }
    }
}
