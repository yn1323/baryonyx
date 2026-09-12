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
        public UnityEngine.UI.Button CloseButton;
        public TMP_Text DetailsTitle;
        public TMP_Text JsonText;
        public ScrollRect JsonScroll;
        private HealthScreenPresenter presenter;
        private IReadOnlyList<HealthDaySnapshot> renderedDays;
        private HealthDaySnapshot renderedDetail;
        private GameObject previousSelection;
        private HealthScreenPhase? renderedPhase;
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
            CloseButton.onClick.AddListener(() => presenter?.CloseDetails());
            for (int i = 0; i < DayButtons.Length; i++)
            {
                int index = i;
                DayButtons[i].onClick.AddListener(() => presenter?.SelectDay(index));
            }
        }

        public void Bind(HealthScreenPresenter value)
        {
            if (presenter != null)
                presenter.Changed -= Render;
            presenter = value ?? throw new ArgumentNullException(nameof(value));
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
                presenter.SignedIn
                && (
                    presenter.Phase == HealthScreenPhase.Ready
                    || presenter.Phase == HealthScreenPhase.Reading
                    || (presenter.Phase == HealthScreenPhase.Failed && presenter.CanRefresh)
                );
            SignInButton.gameObject.SetActive(!presenter.SignedIn);
            ConnectButton.gameObject.SetActive(presenter.SignedIn && !showRefresh);
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
            Status.text = presenter.Message;
            Status.gameObject.SetActive(presenter.Phase != HealthScreenPhase.Ready);
            Progress.text = presenter.Phase switch
            {
                HealthScreenPhase.SigningIn => "Googleで認証中",
                HealthScreenPhase.Connecting => "Health Connectに接続中",
                HealthScreenPhase.Reading => "歩数を取得中",
                HealthScreenPhase.Ready => "Health Connectに接続済み",
                _ => presenter.SignedIn ? "Google認証済み" : "はじめにGoogleで認証",
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
                    string count = day.HasValue
                        ? day.Steps.ToString("N0", CultureInfo.InvariantCulture) + " 歩"
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
                    ? $"{renderedDays[0].Zone}  /  今日は取得時点までの集計"
                    : "歩数データは画面を開いている間だけ保持します。";
            }
            Period.gameObject.SetActive(presenter.Days.Count > 0 || presenter.SignedIn);
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
                Canvas.ForceUpdateCanvases();
                JsonScroll.normalizedPosition = new Vector2(0, 1);
            }
            renderedDetail = presenter.SelectedDay;
            if (renderedPhase != presenter.Phase && !detailOpen)
                revealAfterLayout =
                    presenter.Phase == HealthScreenPhase.Ready
                        ? Period.rectTransform
                        : Progress.rectTransform;
            renderedPhase = presenter.Phase;
        }

        private void OnDestroy()
        {
            if (presenter != null)
                presenter.Changed -= Render;
            if (JsonText != null)
                JsonText.text = "";
        }
    }
}
