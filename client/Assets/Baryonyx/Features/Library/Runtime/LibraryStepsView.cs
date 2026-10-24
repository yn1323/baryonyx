using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.UI.UiText;

namespace Baryonyx.Library
{
    /// <summary>One day's row of the daily steps.</summary>
    [Serializable]
    public sealed class LibraryStepRow
    {
        public GameObject Root;
        public TMP_Text Day;
        public TMP_Text Steps;
        public TMP_Text Act;
    }

    /// <summary>
    /// The library's daily steps (doc/features/library.md): one row a day, newest first, with
    /// the steps walked and the ACT they make. The generator bakes a row from the mock days as
    /// the template; this view copies it for every day it reads.
    /// </summary>
    public sealed class LibraryStepsView : GuidePanel, IEditModePreview
    {
        public const string LoadFailedMessage = "歩数を取得できませんでした";
        public const string EmptyText = "まだ歩数の記録がありません";

        private static readonly string[] Weekdays = { "日", "月", "火", "水", "木", "金", "土" };

        public LibraryStepRow Template;
        public RectTransform Rows;
        public ScrollRect List;

        // 記録した日数（例：7日分）。
        public TMP_Text Count;

        // 読み込み中・読めなかったとき・記録がないときだけ出す文字。
        public TMP_Text Status;

        private readonly List<LibraryStepRow> rows = new();

        // 描いた行の数。テストで確かめるために公開する。
        public int RowCount => CountShown();

        private void OnEnable()
        {
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            // 前に読んだ記録があれば、すぐ出して裏で読み直す。
            var known = LibrarySession.Known;
            if (known != null)
                Render(known.Steps);
            else
                ShowLoading();
            StartLoad(
                token => LoadAsync(known != null, token),
                LoadFailedMessage,
                ShowLoadFailed,
                readAgain: known != null
            );
        }

        private void OnDisable() => Load.Cancel();

        private async Task LoadAsync(bool shown, CancellationToken token)
        {
            string shownVersion = LibrarySession.ReadVersion;
            var read = await LibrarySession.LoadAsync(token);
            if (token.IsCancellationRequested)
                return;
            if (shown && Unchanged(shownVersion, LibrarySession.ReadVersion))
                return;
            Render(read.Steps);
        }

        public void Render(IReadOnlyList<LibraryStepDay> days)
        {
            days ??= Array.Empty<LibraryStepDay>();
            if (Template?.Root != null)
                Template.Root.SetActive(false);
            while (rows.Count < days.Count && Template?.Root != null && Rows != null)
            {
                var copy = Instantiate(Template.Root, Rows);
                copy.name = "Day" + rows.Count;
                rows.Add(
                    new LibraryStepRow
                    {
                        Root = copy,
                        Day = Find(copy, Template.Day),
                        Steps = Find(copy, Template.Steps),
                        Act = Find(copy, Template.Act),
                    }
                );
            }
            for (int i = 0; i < rows.Count; i++)
            {
                bool shown = i < days.Count;
                rows[i].Root.SetActive(shown);
                if (shown)
                    Paint(rows[i], days[i]);
            }
            Set(Count, days.Count > 0 ? $"{days.Count}日分" : "");
            Set(Status, days.Count == 0 ? EmptyText : "");
        }

        /// <summary>Writes a day into a row. The generator calls it for the template too.</summary>
        public static void Paint(LibraryStepRow row, LibraryStepDay day)
        {
            Set(row.Day, DayText(day.Day));
            Set(row.Steps, $"{day.Steps:N0}<size=70%> 歩</size>");
            Set(row.Act, $"{day.Act:N0}<size=70%> ACT</size>");
        }

        // 「10/18（土）」。日付として読めなければそのまま出す。
        public static string DayText(string day) =>
            DateTime.TryParseExact(
                day,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date
            )
                ? $"{date.Month}/{date.Day}（{Weekdays[(int)date.DayOfWeek]}）"
                : day ?? "";

        private static TMP_Text Find(GameObject copy, TMP_Text original)
        {
            if (original == null)
                return null;
            var found = copy.transform.Find(original.name);
            return found != null ? found.GetComponent<TMP_Text>() : null;
        }

        private int CountShown()
        {
            int shown = 0;
            foreach (var row in rows)
                if (row.Root != null && row.Root.activeSelf)
                    shown++;
            return shown;
        }

        private void ShowLoading()
        {
            Set(Status, LoadingText);
            Set(Count, "");
            if (Template?.Root != null)
                Template.Root.SetActive(false);
            foreach (var row in rows)
                if (row.Root != null)
                    row.Root.SetActive(false);
        }

        private void ShowLoadFailed() => Set(Status, LoadFailedText);

        public IReadOnlyList<string> PreviewStates => GuidePanelLoad.PreviewStates;

        public void ShowPreviewState(int index)
        {
            ShowLoading();
            if (index == 1)
                ShowLoadFailed();
        }
    }
}
