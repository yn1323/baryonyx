using TMPro;
using UnityEngine;

namespace Baryonyx.Wireframe.Editor
{
    public static partial class WireframeScreenAssets
    {
        private static void BuildHealth(RectTransform page)
        {
            var health = page.GetComponentInParent<WireframeHealthView>();
            Label("HealthTitle", page, "毎日の歩み", 26, 54);
            health.Total = Label("HealthTotal", page, "直近7日間", 20, 42);
            health.Coverage = Label("HealthCoverage", page, "", 12, 24);
            health.Coverage.color = Muted;
            var chart = Row(page, "WeekChart");
            chart.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().minHeight = 238;
            for (int i = 0; i < 7; i++)
            {
                var button = Button("HealthDay" + i, chart, "", 238, 10);
                button.image.color = Color.clear;
                health.Days[i] = button;
                var column = (RectTransform)button.transform;
                var track = Rect("HealthTrack" + i, column);
                track.anchorMin = new Vector2(.22f, .24f);
                track.anchorMax = new Vector2(.78f, .90f);
                track.offsetMin = track.offsetMax = Vector2.zero;
                Image(track, new Color(.88f, .89f, .83f), false);
                var bar = Rect("HealthBar" + i, track);
                Stretch(bar);
                bar.anchorMax = new Vector2(1, 0);
                Image(bar, i == 6 ? Gold : Accent, false);
                health.Bars[i] = bar;
                health.Dates[i] = OverlayLabel(
                    "HealthDate" + i,
                    column,
                    "—",
                    10,
                    Vector2.zero,
                    new Vector2(1, .10f)
                );
                health.Dates[i].color = Ink;
                health.Dates[i].rectTransform.offsetMin = health.Dates[i].rectTransform.offsetMax =
                    Vector2.zero;
                health.Dates[i].margin = Vector4.zero;
                Object.DestroyImmediate(health.Dates[i].GetComponent<UnityEngine.UI.Shadow>());
                health.Values[i] = OverlayLabel(
                    "HealthValue" + i,
                    column,
                    "未取得",
                    10,
                    new Vector2(0, .10f),
                    new Vector2(1, .23f)
                );
                health.Values[i].color = Ink;
                health.Values[i].rectTransform.offsetMin = health
                    .Values[i]
                    .rectTransform
                    .offsetMax = Vector2.zero;
                health.Values[i].margin = Vector4.zero;
                Object.DestroyImmediate(health.Values[i].GetComponent<UnityEngine.UI.Shadow>());
            }
            Label(
                "HealthChartHint",
                page,
                "棒をタップすると、その日のデータを開きます。",
                12,
                40
            ).color = Muted;
            health.Status = Label(
                "HealthStatus",
                page,
                "Health Connectに接続してください。",
                14,
                64
            );
            health.Connect = Button("HealthConnect", page, "Health Connectに接続", 56, 16, true);
            var actions = Row(page, "HealthActions");
            health.Refresh = Button("HealthRefresh", actions, "更新", 50, 14);
            health.Settings = Button("HealthSettings", actions, "権限・設定", 50, 14);
            Label(
                "HealthPrivacy",
                page,
                "Googleログインは不要です。\n健康データはこの端末内で表示します。",
                12,
                50
            ).color = Muted;
        }

        private static void BuildHealthDetails(
            RectTransform root,
            RectTransform safe,
            WireframeHealthView health
        )
        {
            health.DetailsOverlay = Overlay(
                "HealthDetails",
                root,
                safe,
                out var panel,
                out var content
            );
            panel.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            root.GetComponent<WireframeLayout>().HealthDetailsPanel = panel;
            panel.GetComponent<UnityEngine.UI.Image>().sprite = panelFrame;
            health.DetailsScroll = content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            Label("HealthDetailsTitle", content, "その日の元データ", 22, 44);
            health.Detail = Label("HealthJson", content, "", 12, 0);
            health.Detail.richText = false;
            Close("HealthDetailsClose", panel);
            health.CloseDetails = panel
                .Find("HealthDetailsClose")
                .GetComponent<UnityEngine.UI.Button>();
            health.DetailsOverlay.SetActive(false);
        }
    }
}
