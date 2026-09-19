using System;
using System.Linq;
using Baryonyx.Health;
using TMPro;
using UnityEngine;

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
        public bool DetailsOpen => showDetails;
        public long? TodaySteps =>
            days.LastOrDefault() is { HasValue: true } day ? day.Steps : null;

        public void Bind(HealthScreenPresenter value, bool sample)
        {
            Unbind();
            presenter = value;
            preview = sample;
            Connect.onClick.AddListener(ConnectHealth);
            Refresh.onClick.AddListener(RefreshHealth);
            Settings.onClick.AddListener(OpenSettings);
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
            if (days.Length == 0 && showDetails)
            {
                Detail.text = "";
                Close();
            }
        }

        private void Unbind()
        {
            if (presenter == null)
                return;
            presenter.Changed -= Render;
            Connect.onClick.RemoveListener(ConnectHealth);
            Refresh.onClick.RemoveListener(RefreshHealth);
            Settings.onClick.RemoveListener(OpenSettings);
            CloseDetails.onClick.RemoveListener(Close);
            foreach (var day in Days)
                day.onClick.RemoveAllListeners();
            presenter = null;
        }

        private void OnDestroy() => Unbind();
    }
}
