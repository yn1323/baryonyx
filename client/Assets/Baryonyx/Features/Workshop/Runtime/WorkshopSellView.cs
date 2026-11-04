using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Economy;
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
    /// The trading house's "sell" (doc/features/screens/shop.md#商会), filling the screen: the held
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

        // 売れたとき、左の札と右の行が消える時間。右の行からはルーンが所持ルーンへ飛ぶ。
        public const float VanishSeconds = 0.28f;

        // 読み込む間に伏せて並べるカードの数（4列×3段で一覧の見える所を埋める）。
        public const int CoverCards = 12;

        public PartyMockData Party;
        public LibraryData Data;
        public float ArtBox = 112f;
        public float ChosenArtBox = 72f;
        public GuideTab[] Tabs = Array.Empty<GuideTab>();
        public ScrollRect List;
        public RectTransform Content;
        public ItemTile Template;
        public List<ItemTile> Rows = new();
        public TMP_Text Status;

        // 右の面：選んだ物の行と、合計。
        public RectTransform ChosenContent;
        public ItemTile ChosenTemplate;
        public List<ItemTile> ChosenRows = new();
        public TMP_Text Total;
        public Button Confirm;

        private readonly HashSet<Component> bound = new();
        private readonly GlassCardCover cover = new();
        private readonly List<WorkshopOwned> chosen = new();
        private IReadOnlyList<WorkshopOwned> owned = Array.Empty<WorkshopOwned>();
        private IReadOnlyList<WorkshopOwned> shown = Array.Empty<WorkshopOwned>();
        private int tab;
        private bool busy;

        // 売れた物の札と行が消えるのを待つ。画面を閉じたら待たずに終える。
        private TaskCompletionSource<bool> vanishing;

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

        private void OnDisable()
        {
            Load.Cancel();
            vanishing?.TrySetResult(false);
        }

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
            // 持ち物のない場所の伏せたカードも、めくってから静かに消す。
            cover.Reveal(
                Rows.Take(shown.Count).Select(row => row.gameObject),
                Rows.Skip(shown.Count).Take(CoverCards - shown.Count).Select(row => row.gameObject)
            );
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

        /// <summary>
        /// Sells the chosen things. When the server answers, their cards and rows fade, the rows
        /// bursting into runes that fly into the balance, and then they leave the list.
        /// </summary>
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
            long? before = RuneBalance.Known;
            try
            {
                var sale = await WorkshopSession.SellAsync(things);
                if (this != null && isActiveAndEnabled)
                    await Vanish(
                        things,
                        before ?? sale.BalanceAfter - sale.Runes,
                        sale.BalanceAfter
                    );
                owned = Without(owned, things);
                chosen.Clear();
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

        // 売れた物の左の札と右の行を消し、右の行から得たルーンを所持ルーンへ飛ばす。
        private Task Vanish(IReadOnlyList<WorkshopOwned> sold, long from, long to)
        {
            var fading = new List<GameObject>();
            for (int i = 0; i < shown.Count && i < Rows.Count; i++)
                if (sold.Contains(shown[i]))
                    fading.Add(Rows[i].gameObject);
            var sources = new List<RectTransform>();
            for (int i = 0; i < sold.Count && i < ChosenRows.Count; i++)
                if (ChosenRows[i].gameObject.activeInHierarchy)
                {
                    fading.Add(ChosenRows[i].gameObject);
                    sources.Add((RectTransform)ChosenRows[i].transform);
                }
            if (Guide != null)
                Guide.PlayRuneGain(from, to, sources);
            vanishing = new TaskCompletionSource<bool>();
            StartCoroutine(Fade(fading, vanishing));
            return vanishing.Task;
        }

        // 少し膨らみながら薄れて消える。終わったら元の見た目に戻し、一覧を描き直すのに任せる。
        private static IEnumerator Fade(
            IReadOnlyList<GameObject> fading,
            TaskCompletionSource<bool> done
        )
        {
            var groups = fading
                .Select(item =>
                    item.TryGetComponent(out CanvasGroup group)
                        ? group
                        : item.AddComponent<CanvasGroup>()
                )
                .ToArray();
            for (
                float t = 0f;
                t < VanishSeconds && !done.Task.IsCompleted;
                t += Time.unscaledDeltaTime
            )
            {
                float k = t / VanishSeconds;
                for (int i = 0; i < groups.Length; i++)
                {
                    groups[i].alpha = 1f - k * k;
                    groups[i].transform.localScale = Vector3.one * (1f + 0.06f * k);
                }
                yield return null;
            }
            foreach (var group in groups)
                if (group != null)
                {
                    group.alpha = 1f;
                    group.transform.localScale = Vector3.one;
                }
            done.TrySetResult(true);
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
            if (Confirm != null)
                Confirm.interactable = !busy && chosen.Count > 0;
        }

        /// <summary>Writes a held thing on its card. The generator bakes the mock things with it too.</summary>
        public static void Paint(
            ItemTile row,
            WorkshopOwned thing,
            LibraryEntryData entry,
            float artBox
        )
        {
            LibraryCatalogView.PaintArt(row.Art, entry, true, artBox);
            Set(row.Name, thing.Name);
            Set(row.Detail, new string('★', Math.Max(1, thing.Stars)));
            Set(row.Price, PriceOf(thing).ToString("N0"));
            if (row.Quality != null)
            {
                row.Quality.text = thing.HasQuality ? Quality.Letter(thing.Quality) : "";
                row.Quality.color = QualityPalette.Of(thing.Quality);
                // 品質のないアイテムでは、品質の文字の枠ごと隠す。
                row.Quality.transform.parent.gameObject.SetActive(thing.HasQuality);
            }
            Set(row.Count, thing.Kind == LibraryKind.Item ? "×" + thing.Count : "");
            if (row.Mark != null)
                row.Mark.SetActive(thing.User != null);
            if (row.Card != null)
            {
                // 売る物に入れた札は、チェックボックスのように地も金色にする。
                row.Card.Checkbox = true;
                row.Card.Selected = false;
                row.Card.Interactable = thing.User == null;
            }
        }

        /// <summary>
        /// Writes a chosen thing's row (the standard row): the name with the stars, the quality and
        /// the multiplier right after it, the effect at its own multiplier on the second line (the
        /// count for items), the attributes at the top right and the price at the bottom right.
        /// </summary>
        public static void PaintChosen(
            ItemTile row,
            WorkshopOwned thing,
            LibraryEntryData entry,
            float artBox
        )
        {
            LibraryCatalogView.PaintArt(row.Art, entry, true, artBox);
            Set(
                row.Name,
                ItemText.Heading(thing.Name, thing.Stars, thing.HasQuality, thing.Quality, thing.Multiplier)
            );
            Set(row.Detail, ChosenLine(thing, entry));
            Set(row.Price, PriceOf(thing).ToString("N0"));
            if (row.Elements != null)
                row.Elements.Show(LibraryFaces.ElementsOf(thing.Kind, thing.KindId));
        }

        /// <summary>The line under a chosen thing's name: its effect at its own multiplier, or the count for items.</summary>
        public static string ChosenLine(WorkshopOwned thing, LibraryEntryData entry) =>
            thing.HasQuality ? LibraryFaces.EffectAt(entry, thing.Multiplier) : "×" + thing.Count;

        /// <summary>
        /// The provisional price, the server's too (server/src/features/workshop/catalog.ts): a
        /// twentieth of the buy price by the stars, times the multiplier, rounded half up; items ten
        /// runes at ☆1, doubled for each star, times the count.
        /// </summary>
        public static int PriceOf(WorkshopOwned thing)
        {
            if (thing == null)
                return 0;
            if (thing.Kind == LibraryKind.Item)
                return 10 * (1 << Math.Max(0, thing.Stars - 1)) * thing.Count;
            return Quality.Amount(
                WorkshopGoods.PriceOfStars(Math.Max(1, thing.Stars)) / 20,
                thing.Multiplier
            );
        }

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

        // 読み込むまで、一覧の見える所をカードの裏で埋める。
        private void ShowLoading()
        {
            busy = false;
            owned = shown = Array.Empty<WorkshopOwned>();
            chosen.Clear();
            EnsureRows(CoverCards);
            for (int i = 0; i < Rows.Count; i++)
                Rows[i].gameObject.SetActive(i < CoverCards);
            cover.Cover(Rows.Take(CoverCards).Select(row => row.gameObject));
            Set(Status, "");
            RefreshChosen();
        }

        private void ShowLoadFailed()
        {
            cover.Clear();
            foreach (var row in Rows)
                if (row != null)
                    row.gameObject.SetActive(false);
            Set(Status, LoadFailedText);
        }

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
