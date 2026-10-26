using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Economy;
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
    /// The trading house's "craft" (doc/features/synthesis.md), filling the screen: the things
    /// that can be made on the left, kind by kind (dim when the materials are short), and the
    /// chosen one on the right with the quality it will have, the materials it can take (three,
    /// the worse ones chosen first) and the runes. Press "craft" to make it: the made thing shows
    /// when the server answers, since its quality is the server's to decide.
    /// </summary>
    public sealed class WorkshopCraftView : GuidePanel, IEditModePreview
    {
        public const string LoadFailedMessage = "合成できる物を取得できませんでした";
        public const string FailedMessage = "合成できませんでした";
        public const string NoMaterialsText = "使える材料がありません";

        public LibraryData Data;
        public PartyMockData Party;
        public float ArtBox = 80f;
        public float DetailArtBox = 224f;
        public float MaterialArtBox = 96f;
        public GuideTab[] Tabs = Array.Empty<GuideTab>();
        public ScrollRect List;
        public RectTransform Content;
        public WorkshopGoodRow Template;
        public List<WorkshopGoodRow> Rows = new();
        public TMP_Text Status;

        // 右の面：出来上がる物の絵・名前・☆・効果・出来上がる品質。
        public RawImage DetailArt;
        public TMP_Text DetailName;
        public TMP_Text DetailStars;
        public TMP_Text DetailEffect;
        public TMP_Text DetailQuality;

        // 材料：選んだ数と、使える材料の札。
        public GameObject MaterialsTitle;
        public TMP_Text MaterialCount;
        public ScrollRect MaterialList;
        public RectTransform MaterialContent;
        public WorkshopGoodRow MaterialTemplate;
        public List<WorkshopGoodRow> MaterialRows = new();
        public TMP_Text MaterialStatus;

        public GameObject Price;
        public TMP_Text PriceLabel;
        public TMP_Text Missing;
        public Button Confirm;

        private readonly HashSet<Component> bound = new();
        private readonly List<int> picked = new();
        private IReadOnlyList<WorkshopRecipe> recipes = Array.Empty<WorkshopRecipe>();
        private IReadOnlyList<WorkshopRecipe> shown = Array.Empty<WorkshopRecipe>();
        private IReadOnlyList<WorkshopOwned> owned = Array.Empty<WorkshopOwned>();
        private IReadOnlyList<WorkshopOwned> materials = Array.Empty<WorkshopOwned>();
        private int tab;
        private int selected = -1;
        private bool busy;

        public IReadOnlyList<WorkshopRecipe> Recipes => recipes;
        public IReadOnlyList<WorkshopRecipe> Shown => shown;
        public IReadOnlyList<WorkshopOwned> Owned => owned;
        public IReadOnlyList<WorkshopOwned> Materials => materials;

        /// <summary>The chosen materials' indexes in <see cref="Materials"/>, in the order chosen.</summary>
        public IReadOnlyList<int> Picked => picked;

        public int Tab => tab;
        public int Selected => selected;

        // 直前の合成。テストで完了を待つために公開する。
        public Task Work { get; private set; } = Task.CompletedTask;

        private WorkshopRecipe SelectedRecipe =>
            selected >= 0 && selected < shown.Count ? shown[selected] : null;

        private IReadOnlyList<WorkshopOwned> PickedMaterials =>
            picked
                .Where(index => index < materials.Count)
                .Select(index => materials[index])
                .ToArray();

        private void OnEnable()
        {
            BindControls();
            RuneBalance.Changed += RefreshDetail;
            ShowLoading();
            StartLoad(LoadAsync, LoadFailedMessage, ShowLoadFailed);
        }

        private void OnDisable()
        {
            Load.Cancel();
            RuneBalance.Changed -= RefreshDetail;
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
                Confirm.onClick.AddListener(Craft);
        }

        private async Task LoadAsync(CancellationToken token)
        {
            var stock = WorkshopStock.LoadAsync(Party, token);
            var records = await LibrarySession.LoadAsync(token);
            var held = await stock;
            if (!token.IsCancellationRequested)
                Render(WorkshopCraft.Recipes(Data, records), held);
        }

        /// <summary>Takes what can be made and what is held, and shows the chosen tab's, the first chosen.</summary>
        public void Render(
            IReadOnlyList<WorkshopRecipe> nextRecipes,
            IReadOnlyList<WorkshopOwned> nextOwned
        )
        {
            recipes = nextRecipes ?? Array.Empty<WorkshopRecipe>();
            owned = nextOwned ?? Array.Empty<WorkshopOwned>();
            ShowTab(tab);
        }

        public void ShowTab(int index)
        {
            if (busy)
                return;
            tab = Mathf.Clamp(index, 0, WorkshopStock.Kinds.Count - 1);
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i == tab;
            shown = recipes.Where(recipe => recipe.Kind == WorkshopStock.Kinds[tab]).ToArray();
            selected = -1;
            PaintRows();
            Set(Status, shown.Count == 0 ? EmptyText(WorkshopStock.TabLabels[tab]) : "");
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            // 右の面が空にならないよう、先頭の物を選んでおく。
            if (shown.Count > 0)
                Select(0);
            else
                ShowMaterials(null);
        }

        public void Select(int index)
        {
            if (busy || index < 0 || index >= shown.Count)
                return;
            selected = index;
            for (int i = 0; i < shown.Count && i < Rows.Count; i++)
                if (Rows[i].Card != null)
                    Rows[i].Card.Selected = i == index;
            ShowMaterials(shown[index]);
        }

        /// <summary>Chooses the material <paramref name="index"/>, or lets it go if chosen. Three at most.</summary>
        public void ToggleMaterial(int index)
        {
            if (busy || index < 0 || index >= materials.Count)
                return;
            if (!picked.Remove(index))
            {
                if (picked.Count >= WorkshopCraft.MaterialCount)
                    return;
                picked.Add(index);
            }
            PaintMaterialMarks();
            RefreshDetail();
        }

        /// <summary>Makes the chosen thing from the chosen materials, and shows it when the server answers.</summary>
        public void Craft()
        {
            if (!CanCraft())
                return;
            Work = CraftAsync(SelectedRecipe, PickedMaterials);
        }

        private async Task CraftAsync(WorkshopRecipe recipe, IReadOnlyList<WorkshopOwned> used)
        {
            busy = true;
            RefreshDetail();
            try
            {
                var crafted = await WorkshopSession.CraftAsync(recipe, used);
                owned = Applied(owned, recipe, used, crafted);
                ShowNotice(CraftedText(recipe, crafted.Multiplier));
                if (this != null && isActiveAndEnabled && DetailArt != null)
                    StartCoroutine(Punch(DetailArt.rectTransform));
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogWarning(FailedMessage + "。" + exception.Message, this);
                ShowNotice(FailedMessage);
                if (this != null && isActiveAndEnabled)
                {
                    busy = false;
                    StartLoad(LoadAsync, LoadFailedMessage, ShowLoadFailed);
                    return;
                }
            }
            finally
            {
                busy = false;
            }
            if (this == null || !isActiveAndEnabled)
                return;
            // 材料が減ったので、同じ物を選んだまま材料を選び直す。
            int keep = selected;
            PaintRows();
            if (keep >= 0 && keep < shown.Count)
                Select(keep);
        }

        /// <summary>
        /// The held things after a craft: the used materials gone (an item's count less), and the
        /// made thing added (an item's count more).
        /// </summary>
        public static IReadOnlyList<WorkshopOwned> Applied(
            IReadOnlyList<WorkshopOwned> before,
            WorkshopRecipe recipe,
            IReadOnlyList<WorkshopOwned> used,
            WorkshopCrafted crafted
        )
        {
            var after = before.Select(thing => thing.Copy()).ToList();
            foreach (var material in used)
            {
                var match = after.FirstOrDefault(thing =>
                    thing.Kind == material.Kind && thing.RowId == material.RowId
                );
                if (match == null)
                    continue;
                match.Count -= material.Count;
                if (match.Count <= 0)
                    after.Remove(match);
            }
            if (recipe.Kind == LibraryKind.Item)
            {
                var item = after.FirstOrDefault(thing =>
                    thing.Kind == LibraryKind.Item && thing.KindId == recipe.Id
                );
                if (item != null)
                {
                    item.Count++;
                    return after;
                }
            }
            after.Add(
                new WorkshopOwned
                {
                    Kind = recipe.Kind,
                    RowId = crafted.ItemId,
                    KindId = recipe.Id,
                    Name = recipe.Name,
                    Stars = recipe.Stars,
                    Multiplier =
                        recipe.Kind == LibraryKind.Item
                            ? Quality.MinMultiplier
                            : crafted.Multiplier,
                    MaxMultiplier = recipe.MaxMultiplier,
                }
            );
            return after;
        }

        private bool CanCraft()
        {
            var recipe = SelectedRecipe;
            return !busy
                && recipe != null
                && picked.Count == WorkshopCraft.MaterialCount
                && (RuneBalance.Known is not { } runes || runes >= recipe.Runes);
        }

        private void PaintRows()
        {
            EnsureRows(shown.Count);
            for (int i = 0; i < Rows.Count; i++)
            {
                bool visible = i < shown.Count;
                Rows[i].gameObject.SetActive(visible);
                if (visible)
                    Paint(Rows[i], shown[i], WorkshopCraft.CanMake(shown[i], owned), ArtBox);
            }
        }

        /// <summary>Writes a recipe's row, dim when the materials are short. The generator bakes the mock recipes with it too.</summary>
        public static void Paint(
            WorkshopGoodRow row,
            WorkshopRecipe recipe,
            bool canMake,
            float artBox
        )
        {
            LibraryCatalogView.PaintArt(row.Art, recipe.Entry, true, artBox);
            Set(row.Name, recipe.Name);
            Set(row.Detail, WorkshopBuyView.StarsText(recipe.Stars));
            Set(row.Price, "");
            if (row.Card != null)
            {
                row.Card.Selected = false;
                if (row.Card.Group != null)
                    row.Card.Group.alpha = canMake ? 1f : DimAlpha;
            }
        }

        private const float DimAlpha = 0.5f;

        private void ShowMaterials(WorkshopRecipe recipe)
        {
            materials = WorkshopCraft.Materials(recipe, owned);
            picked.Clear();
            picked.AddRange(WorkshopCraft.FirstChoice(materials));
            EnsureMaterialRows(materials.Count);
            for (int i = 0; i < MaterialRows.Count; i++)
            {
                bool visible = i < materials.Count;
                MaterialRows[i].gameObject.SetActive(visible);
                if (visible)
                    WorkshopSellView.Paint(
                        MaterialRows[i],
                        materials[i],
                        EntryOf(materials[i]),
                        MaterialArtBox
                    );
            }
            if (MaterialList != null)
                MaterialList.verticalNormalizedPosition = 1f;
            if (MaterialsTitle != null)
                MaterialsTitle.SetActive(recipe != null);
            Set(MaterialStatus, recipe != null && materials.Count == 0 ? NoMaterialsText : "");
            PaintMaterialMarks();
            RefreshDetail();
        }

        private void PaintMaterialMarks()
        {
            for (int i = 0; i < materials.Count && i < MaterialRows.Count; i++)
            {
                var row = MaterialRows[i];
                if (row.Card != null)
                    row.Card.Selected = picked.Contains(i);
                // アイテムは1つずつの札なので、数を出さない。
                Set(row.Count, "");
            }
            Set(
                MaterialCount,
                SelectedRecipe != null ? $"{picked.Count} / {WorkshopCraft.MaterialCount}" : ""
            );
        }

        private void RefreshDetail()
        {
            var recipe = SelectedRecipe;
            if (DetailArt != null)
            {
                // 作れる物がないときは、絵の後ろの光ごと隠す。
                DetailArt.transform.parent.gameObject.SetActive(recipe != null);
                if (recipe != null)
                    LibraryCatalogView.PaintArt(DetailArt, recipe.Entry, true, DetailArtBox);
            }
            Set(DetailName, recipe?.Name);
            Set(DetailStars, recipe != null ? WorkshopBuyView.StarsText(recipe.Stars) : "");
            Set(DetailEffect, recipe != null ? EffectOf(recipe) : "");
            Set(
                DetailQuality,
                recipe != null && picked.Count == WorkshopCraft.MaterialCount
                    ? QualityOf(recipe, PickedMaterials)
                    : ""
            );
            if (Price != null)
                Price.SetActive(recipe != null);
            Set(PriceLabel, recipe != null ? recipe.Runes.ToString("N0") : "");
            Set(
                Missing,
                recipe != null ? MissingText(recipe, materials.Count, RuneBalance.Known) : ""
            );
            if (Confirm != null)
                Confirm.interactable = CanCraft();
        }

        /// <summary>The made thing's quality from the materials, or nothing for an item.</summary>
        public static string QualityOf(WorkshopRecipe recipe, IReadOnlyList<WorkshopOwned> used)
        {
            if (recipe.Kind == LibraryKind.Item)
                return "";
            float multiplier = WorkshopCraft.Multiplier(recipe, used);
            var quality = Quality.Of(multiplier, recipe.MaxMultiplier);
            return $"<color=#{QualityPalette.Hex(quality)}>品質{Quality.Letter(quality)}</color>（{Quality.Times(multiplier)}）";
        }

        /// <summary>What is short: the materials (☆ and kind), or the runes. Empty when nothing is.</summary>
        public static string MissingText(WorkshopRecipe recipe, int materials, long? runes)
        {
            if (materials < WorkshopCraft.MaterialCount)
                return $"☆{recipe.MaterialStars}の{LibraryKinds.Label(recipe.Kind)}が {WorkshopCraft.MaterialCount - materials} つ足りません";
            return runes is { } balance && balance < recipe.Runes
                ? $"ルーンが {recipe.Runes - balance:N0} 足りません"
                : "";
        }

        public static string CraftedText(WorkshopRecipe recipe, float multiplier)
        {
            if (recipe.Kind == LibraryKind.Item)
                return $"{recipe.Name}を作った";
            var quality = Quality.Of(multiplier, recipe.MaxMultiplier);
            return $"{recipe.Name}（品質{Quality.Letter(quality)}）を作った";
        }

        public static string EmptyText(string kind) => $"作れる{kind}がまだありません";

        private static string EffectOf(WorkshopRecipe recipe)
        {
            var entry = recipe.Entry;
            string kind = string.Join(
                "｜",
                new[]
                {
                    entry.KindLine,
                    recipe.Kind == LibraryKind.Skill ? null : entry.Description,
                }.Where(text => !string.IsNullOrEmpty(text))
            );
            return recipe.Kind == LibraryKind.Skill ? kind + "\n" + entry.Description : kind;
        }

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
                row.name = "Recipe" + i;
                if (row.Card == null || !bound.Add(row))
                    continue;
                int index = i;
                row.Card.Clicked += () => Select(index);
            }
        }

        private void EnsureMaterialRows(int count)
        {
            MaterialRows.RemoveAll(row => row == null);
            while (
                MaterialRows.Count < count && MaterialTemplate != null && MaterialContent != null
            )
                MaterialRows.Add(Instantiate(MaterialTemplate, MaterialContent));
            for (int i = 0; i < MaterialRows.Count; i++)
            {
                var row = MaterialRows[i];
                row.name = "Material" + i;
                if (row.Card == null || !bound.Add(row))
                    continue;
                int index = i;
                row.Card.Clicked += () => ToggleMaterial(index);
            }
        }

        // 出来上がった瞬間：絵を大きくして戻す。
        private static IEnumerator Punch(RectTransform rect)
        {
            const float duration = 0.5f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                rect.localScale = Vector3.one * (1f + 0.3f * (1f - k) * (1f - k));
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        private void ShowLoading()
        {
            recipes = shown = Array.Empty<WorkshopRecipe>();
            selected = -1;
            foreach (var row in Rows)
                if (row != null)
                    row.gameObject.SetActive(false);
            Set(Status, LoadingText);
            ShowMaterials(null);
        }

        private void ShowLoadFailed() => Set(Status, LoadFailedText);

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }

        // 停止中の表示の切り替え：作れる物、選んだとき、読み込む間、読めなかったとき。
        public IReadOnlyList<string> PreviewStates =>
            new[] { "作れる物", "選んだとき" }.Concat(GuidePanelLoad.PreviewStates).ToArray();

        public void ShowPreviewState(int index)
        {
            if (index <= 1)
            {
                Render(
                    WorkshopCraft.Recipes(Data, LibraryMemorySource.Mock()),
                    WorkshopStock.Mock()
                );
                if (index == 1)
                    Select(
                        Math.Max(
                            0,
                            Array.FindIndex(
                                shown.ToArray(),
                                recipe => WorkshopCraft.CanMake(recipe, owned)
                            )
                        )
                    );
                return;
            }
            ShowLoading();
            if (index == 3)
                ShowLoadFailed();
        }
    }
}
