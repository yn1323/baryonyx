using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.Library;
using Baryonyx.Party;
using Baryonyx.UI;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// The trading house's "sell" (doc/features/screens.md#商会), filling the screen: the held
    /// things on the left, each one apart since each has its own quality (items, which have none,
    /// one card per kind with the count), and the chosen ones with their prices and the total on
    /// the right. Several can be chosen; a thing someone uses shows "E" and cannot be. Press "sell"
    /// to sell them for runes: they leave the list when the server answers, since it decides the
    /// prices (provisional, the same as <see cref="PriceOf"/>).
    /// </summary>
    public sealed class WorkshopSellView : GuidePanel, IEditModePreview
    {
        public const string LoadFailedMessage = "持ち物を取得できませんでした";
        public const string FailedMessage = "売れませんでした";

        // 1回で売れる数。サーバーの SELL_LIMIT（server/src/features/workshop/catalog.ts）と同じ。
        public const int MaxChosen = 50;

        public PartyMockData Party;
        public LibraryData Data;
        public float ArtBox = 112f;
        public float ChosenArtBox = 72f;
        public GuideTab[] Tabs = Array.Empty<GuideTab>();
        public ScrollRect List;
        public RectTransform Content;
        public WorkshopGoodRow Template;
        public List<WorkshopGoodRow> Rows = new();
        public TMP_Text Status;

        // 右の面：選んだ物の行と、合計。
        public RectTransform ChosenContent;
        public WorkshopGoodRow ChosenTemplate;
        public List<WorkshopGoodRow> ChosenRows = new();
        public TMP_Text Total;
        public Button Clear;
        public Button Confirm;

        private readonly HashSet<Component> bound = new();
        private readonly List<WorkshopOwned> chosen = new();
        private IReadOnlyList<WorkshopOwned> owned = Array.Empty<WorkshopOwned>();
        private IReadOnlyList<WorkshopOwned> shown = Array.Empty<WorkshopOwned>();
        private int tab;
        private bool busy;

        public IReadOnlyList<WorkshopOwned> Owned => owned;
        public IReadOnlyList<WorkshopOwned> Shown => shown;
        public IReadOnlyList<WorkshopOwned> Chosen => chosen;
        public int Tab => tab;

        // 直前の売却。テストで完了を待つために公開する。
        public Task Work { get; private set; } = Task.CompletedTask;

        private void OnEnable()
        {
            BindControls();
            ShowLoading();
            StartLoad(LoadAsync, LoadFailedMessage, ShowLoadFailed);
        }

        private void OnDisable() => Load.Cancel();

        private void BindControls()
        {
            for (int i = 0; i < Tabs.Length; i++)
            {
                var item = Tabs[i];
                if (item == null || item.Button == null || !bound.Add(item))
                    continue;
                int index = i;
                item.Button.onClick.AddListener(() => ShowTab(index));
            }
            if (Clear != null && bound.Add(Clear))
                Clear.onClick.AddListener(ClearChosen);
            if (Confirm != null && bound.Add(Confirm))
                Confirm.onClick.AddListener(Sell);
        }

        private async Task LoadAsync(CancellationToken token)
        {
            var stock = await WorkshopStock.LoadAsync(Party, token);
            if (!token.IsCancellationRequested)
                Render(stock);
        }

        /// <summary>Takes the held things and shows the chosen tab's, none chosen.</summary>
        public void Render(IReadOnlyList<WorkshopOwned> next)
        {
            owned = next ?? Array.Empty<WorkshopOwned>();
            chosen.Clear();
            ShowTab(tab);
        }

        public void ShowTab(int index)
        {
            if (busy)
                return;
            tab = Mathf.Clamp(index, 0, WorkshopStock.Kinds.Count - 1);
            PaintTab();
            if (List != null)
                List.verticalNormalizedPosition = 1f;
        }

        private void PaintTab()
        {
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i == tab;
            shown = WorkshopStock.OfKind(owned, WorkshopStock.Kinds[tab]);
            EnsureRows(shown.Count);
            for (int i = 0; i < Rows.Count; i++)
            {
                bool visible = i < shown.Count;
                Rows[i].gameObject.SetActive(visible);
                if (visible)
                    Paint(Rows[i], shown[i], EntryOf(shown[i]), ArtBox);
            }
            Set(Status, shown.Count == 0 ? EmptyText(WorkshopStock.TabLabels[tab]) : "");
            RefreshChosen();
        }

        /// <summary>Chooses the card <paramref name="index"/> of the tab, or lets it go if chosen.</summary>
        public void Toggle(int index)
        {
            if (busy || index < 0 || index >= shown.Count || shown[index].User != null)
                return;
            var thing = shown[index];
            if (!chosen.Remove(thing))
            {
                if (chosen.Count >= MaxChosen)
                    return;
                chosen.Add(thing);
            }
            RefreshChosen();
        }

        public void ClearChosen()
        {
            if (busy)
                return;
            chosen.Clear();
            RefreshChosen();
        }

        /// <summary>Sells the chosen things, and takes them off the list when the server answers.</summary>
        public void Sell()
        {
            if (busy || chosen.Count == 0)
                return;
            Work = SellAsync(chosen.ToArray());
        }

        private async Task SellAsync(IReadOnlyList<WorkshopOwned> things)
        {
            busy = true;
            RefreshChosen();
            bool failed = false;
            try
            {
                var sale = await WorkshopSession.SellAsync(things);
                owned = Without(owned, things);
                chosen.Clear();
                ShowNotice(SoldText(things.Count, sale.Runes));
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogWarning(FailedMessage + "。" + exception.Message, this);
                ShowNotice(FailedMessage);
                failed = true;
            }
            finally
            {
                busy = false;
            }
            if (this == null || !isActiveAndEnabled)
                return;
            if (failed)
                StartLoad(LoadAsync, LoadFailedMessage, ShowLoadFailed);
            else
                PaintTab();
        }

        /// <summary>The held things after a sale: the sold ones gone.</summary>
        public static IReadOnlyList<WorkshopOwned> Without(
            IReadOnlyList<WorkshopOwned> before,
            IReadOnlyList<WorkshopOwned> sold
        ) =>
            before
                .Where(thing =>
                    !sold.Any(other => other.Kind == thing.Kind && other.RowId == thing.RowId)
                )
                .ToArray();

        public static string SoldText(int count, int runes) =>
            $"{count}点を{runes:N0}ルーンで売った";

        private void RefreshChosen()
        {
            for (int i = 0; i < shown.Count && i < Rows.Count; i++)
                if (Rows[i].Card != null)
                    Rows[i].Card.Selected = chosen.Contains(shown[i]);
            EnsureChosenRows(chosen.Count);
            for (int i = 0; i < ChosenRows.Count; i++)
            {
                bool visible = i < chosen.Count;
                ChosenRows[i].gameObject.SetActive(visible);
                if (visible)
                    PaintChosen(ChosenRows[i], chosen[i], EntryOf(chosen[i]), ChosenArtBox);
            }
            Set(Total, "+" + chosen.Sum(PriceOf).ToString("N0"));
            if (Clear != null)
                Clear.interactable = !busy && chosen.Count > 0;
            if (Confirm != null)
                Confirm.interactable = !busy && chosen.Count > 0;
        }

        /// <summary>Writes a held thing on its card. The generator bakes the mock things with it too.</summary>
        public static void Paint(
            WorkshopGoodRow row,
            WorkshopOwned thing,
            LibraryEntryData entry,
            float artBox
        )
        {
            LibraryCatalogView.PaintArt(row.Art, entry, true, artBox);
            if (row.Quality != null)
            {
                row.Quality.text = thing.HasQuality ? Quality.Letter(thing.Quality) : "";
                row.Quality.color = QualityPalette.Of(thing.Quality);
            }
            Set(row.Count, thing.Kind == LibraryKind.Item ? "×" + thing.Count : "");
            if (row.InUse != null)
                row.InUse.SetActive(thing.User != null);
            if (row.Card != null)
            {
                row.Card.Selected = false;
                row.Card.Interactable = thing.User == null;
            }
        }

        /// <summary>
        /// Writes a chosen thing's row: its name, quality (or count) and price, and for equipment
        /// its effect at its own multiplier under the quality.
        /// </summary>
        public static void PaintChosen(
            WorkshopGoodRow row,
            WorkshopOwned thing,
            LibraryEntryData entry,
            float artBox
        )
        {
            LibraryCatalogView.PaintArt(row.Art, entry, true, artBox);
            Set(row.Name, thing.Name);
            string quality = thing.HasQuality
                ? $"<color=#{QualityPalette.Hex(thing.Quality)}>品質{Quality.Letter(thing.Quality)}</color>（{Quality.Times(thing.Multiplier)}）"
                : "×" + thing.Count;
            string effect = GearEffectOf(thing);
            Set(row.Detail, effect != null ? quality + "\n" + effect : quality);
            Set(row.Price, WorkshopBuyView.PriceText(PriceOf(thing)));
            if (row.Price != null)
                row.Price.color = UiPalette.Gold;
        }

        /// <summary>
        /// The provisional price, the server's too (server/src/features/workshop/catalog.ts): a
        /// quarter of the buy price by the stars, times the multiplier, rounded half up; items ten
        /// runes at ☆1, doubled for each star, times the count.
        /// </summary>
        public static int PriceOf(WorkshopOwned thing)
        {
            if (thing == null)
                return 0;
            if (thing.Kind == LibraryKind.Item)
                return 10 * (1 << Math.Max(0, thing.Stars - 1)) * thing.Count;
            return Quality.Amount(
                WorkshopGoods.PriceOfStars(Math.Max(1, thing.Stars)) / 4,
                thing.Multiplier
            );
        }

        /// <summary>The effect of a piece of equipment at its own multiplier, or null for others.</summary>
        public static string GearEffectOf(WorkshopOwned thing) =>
            thing != null && thing.Kind != LibraryKind.Skill && thing.Kind != LibraryKind.Item
                ? EquipmentCatalog.Find(thing.KindId)?.DetailAt(thing.Multiplier)
                : null;

        public static string EmptyText(string kind) => $"{kind}を持っていません";

        private LibraryEntryData EntryOf(WorkshopOwned thing) =>
            Data != null
                ? Data.Entries.FirstOrDefault(entry =>
                    entry != null && entry.Kind == thing.Kind && entry.Id == thing.KindId
                )
                : null;

        private void EnsureRows(int count)
        {
            Rows.RemoveAll(row => row == null);
            while (Rows.Count < count && Template != null && Content != null)
                Rows.Add(Instantiate(Template, Content));
            for (int i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                row.name = "Owned" + i;
                if (row.Card == null || !bound.Add(row))
                    continue;
                int index = i;
                row.Card.Clicked += () => Toggle(index);
            }
        }

        private void EnsureChosenRows(int count)
        {
            ChosenRows.RemoveAll(row => row == null);
            while (ChosenRows.Count < count && ChosenTemplate != null && ChosenContent != null)
                ChosenRows.Add(Instantiate(ChosenTemplate, ChosenContent));
            for (int i = 0; i < ChosenRows.Count; i++)
            {
                var row = ChosenRows[i];
                row.name = "Chosen" + i;
                if (row.Card == null || !bound.Add(row))
                    continue;
                int index = i;
                // 右の行を押すと、選ぶのをやめる。
                row.Card.Clicked += () =>
                {
                    if (index < chosen.Count)
                    {
                        chosen.RemoveAt(index);
                        RefreshChosen();
                    }
                };
            }
        }

        private void ShowLoading()
        {
            busy = false;
            owned = shown = Array.Empty<WorkshopOwned>();
            chosen.Clear();
            foreach (var row in Rows)
                if (row != null)
                    row.gameObject.SetActive(false);
            Set(Status, LoadingText);
            RefreshChosen();
        }

        private void ShowLoadFailed() => Set(Status, LoadFailedText);

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }

        // 停止中の表示の切り替え：仮の持ち物、3つ選んだとき、読み込む間、読めなかったとき。
        public IReadOnlyList<string> PreviewStates =>
            new[] { "持ち物", "選んだとき" }.Concat(GuidePanelLoad.PreviewStates).ToArray();

        public void ShowPreviewState(int index)
        {
            if (index <= 1)
            {
                Render(WorkshopStock.Mock());
                if (index == 1)
                    for (int i = 0, picked = 0; i < shown.Count && picked < 3; i++)
                        if (shown[i].User == null)
                        {
                            Toggle(i);
                            picked++;
                        }
                return;
            }
            ShowLoading();
            if (index == 3)
                ShowLoadFailed();
        }
    }
}
