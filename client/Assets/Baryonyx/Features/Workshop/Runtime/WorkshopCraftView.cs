using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Economy;
using Baryonyx.Inventory;
using Baryonyx.Library;
using Baryonyx.Networking;
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
    /// whose recipe has a found material on the left, kind by kind (dim when the materials are
    /// short, shadowed until made once), and the chosen one on the right with the quality it will
    /// have, its materials as tiles (held/needed; a special material's tile shows the chosen
    /// piece's quality and opens <see cref="Picker"/> to change it) and the runes. Press "craft" to
    /// make it: the made thing shows at once with the quality the same rule as the server's gives
    /// (optimistic, doc/rules/data-storage.md), and the screen reads things again if the save fails.
    /// Pressing a material's tile opens its page of the library, which tells where it is found
    /// (doc/features/synthesis.md の材料の入手先); back from it returns here with the same thing
    /// chosen. When a special material is held in more than one quality, pressing "craft" first
    /// asks which to use (<see cref="Picker"/>).
    /// </summary>
    public sealed class WorkshopCraftView : GuidePanel, IEditModePreview, IGuideEntryTarget
    {
        public const string LoadFailedMessage = "合成できる物を取得できませんでした";
        public const string FailedMessage = "合成できませんでした";
        public const string Hidden = "？？？";

        // 読み込む間に伏せて並べる行の数（一覧の見える所を埋める）。
        public const int CoverRows = 7;

        // 足りない数の色。
        private const string ShortColor = "ff8a7a";

        // 出来た物の、サーバーが答えるまでの仮の行IDの頭。
        private const string PendingPrefix = "pending-craft-";

        public LibraryData Data;
        public PartyMockData Party;
        public float ArtBox = 80f;
        public float DetailArtBox = 192f;
        public float MaterialArtBox = 96f;
        public GuideTab[] Tabs = Array.Empty<GuideTab>();
        public ScrollRect List;
        public RectTransform Content;
        public ItemTile Template;
        public List<ItemTile> Rows = new();
        public TMP_Text Status;

        // 右の面（詳しい面）：上から出来上がる品質と倍率、絵・名前・☆・効果（出来上がる倍率を掛けた値）・属性。
        public TMP_Text DetailQuality;
        public RawImage DetailArt;
        public TMP_Text DetailName;
        public TMP_Text DetailStars;
        public TMP_Text DetailEffect;
        public ElementIconRow DetailElements;

        // 材料：見出しと、レシピの材料の札（多くて4つ）。
        public GameObject MaterialsTitle;
        public List<ItemTile> MaterialRows = new();

        // 特別な材料（装備・スキル）の、使う物を選ぶ画面。
        public WorkshopMaterialPicker Picker;

        public GameObject Price;
        public TMP_Text PriceLabel;
        public TMP_Text Missing;
        public Button Confirm;

        // 合成したときの、画面全体の演出（光の渦）。
        public WorkshopCraftSwirl Swirl;

        private readonly HashSet<Component> bound = new();
        private readonly GlassCardCover cover = new();
        private readonly List<WorkshopOwned> special = new();
        private IReadOnlyList<WorkshopRecipe> recipes = Array.Empty<WorkshopRecipe>();
        private IReadOnlyList<WorkshopRecipe> shown = Array.Empty<WorkshopRecipe>();
        private IReadOnlyList<WorkshopOwned> owned = Array.Empty<WorkshopOwned>();
        private LibraryRecords records = LibraryRecords.Empty;
        private int tab;
        private int selected = -1;
        private int crafts;

        // ライブラリから戻ってきたときに選び直す物（KeyOf の形）。読み終えたら選ぶ。
        private string requested;

        public IReadOnlyList<WorkshopRecipe> Recipes => recipes;
        public IReadOnlyList<WorkshopRecipe> Shown => shown;
        public IReadOnlyList<WorkshopOwned> Owned => owned;

        /// <summary>The chosen pieces of the selected recipe's special materials, in its order (null when none is held).</summary>
        public IReadOnlyList<WorkshopOwned> Special => special;

        public int Tab => tab;
        public int Selected => selected;

        // 直前の合成。テストで完了を待つために公開する。
        public Task Work { get; private set; } = Task.CompletedTask;

        private WorkshopRecipe SelectedRecipe =>
            selected >= 0 && selected < shown.Count ? shown[selected] : null;

        private void OnEnable()
        {
            BindControls();
            RuneBalance.Changed += RefreshDetail;
            if (Picker != null)
                Picker.Close();
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
                Confirm.onClick.AddListener(PressCraft);
            for (int i = 0; i < MaterialRows.Count; i++)
            {
                var row = MaterialRows[i];
                if (row == null || row.Card == null || !bound.Add(row))
                    continue;
                int index = i;
                row.Card.Clicked += () => PressMaterial(index);
            }
        }

        private async Task LoadAsync(CancellationToken token)
        {
            var stock = WorkshopStock.LoadAsync(Party, token);
            var read = await LibrarySession.LoadAsync(token);
            var held = await stock;
            if (!token.IsCancellationRequested)
                Render(WorkshopCraft.Recipes(Data, read), held, read);
        }

        /// <summary>Takes what can be made, what is held and what was found, and shows the chosen tab's, the first chosen.</summary>
        public void Render(
            IReadOnlyList<WorkshopRecipe> nextRecipes,
            IReadOnlyList<WorkshopOwned> nextOwned,
            LibraryRecords nextRecords
        )
        {
            recipes = nextRecipes ?? Array.Empty<WorkshopRecipe>();
            owned = nextOwned ?? Array.Empty<WorkshopOwned>();
            records = nextRecords ?? LibraryRecords.Empty;
            ShowTab(tab);
            if (requested != null)
                ShowEntry(requested);
        }

        /// <summary>The chosen thing as "kind key/id", to choose it again on coming back.</summary>
        public string CurrentEntry =>
            SelectedRecipe is { } recipe ? KeyOf(recipe.Kind, recipe.Id) : null;

        public static string KeyOf(LibraryKind kind, string id) =>
            $"{LibraryKinds.GuideItemKey(kind)}/{id}";

        /// <summary>
        /// Chooses the thing <paramref name="entry"/> (<see cref="KeyOf"/>) on its tab, as when
        /// coming back from a material's page of the library; before the things are read, once
        /// they are.
        /// </summary>
        public void ShowEntry(string entry)
        {
            requested = entry;
            for (int t = 0; t < WorkshopCraft.Kinds.Count; t++)
            {
                var kind = WorkshopCraft.Kinds[t];
                var recipe = recipes.FirstOrDefault(r =>
                    r.Kind == kind && KeyOf(r.Kind, r.Id) == entry
                );
                if (recipe == null)
                    continue;
                requested = null;
                if (t != tab)
                    ShowTab(t);
                Select(shown.ToList().IndexOf(recipe));
                return;
            }
        }

        public void ShowTab(int index)
        {
            tab = Mathf.Clamp(index, 0, WorkshopCraft.TabLabels.Length - 1);
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i == tab;
            shown = recipes.Where(recipe => recipe.Kind == WorkshopCraft.Kinds[tab]).ToArray();
            selected = -1;
            PaintRows();
            Set(Status, shown.Count == 0 ? EmptyText(WorkshopCraft.TabLabels[tab]) : "");
            // 作れる物のない場所の伏せた行も、めくってから静かに消す。
            cover.Reveal(
                Rows.Take(shown.Count).Select(row => row.gameObject),
                Rows.Skip(shown.Count).Take(CoverRows - shown.Count).Select(row => row.gameObject)
            );
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
            if (index < 0 || index >= shown.Count)
                return;
            selected = index;
            for (int i = 0; i < shown.Count && i < Rows.Count; i++)
                if (Rows[i].Card != null)
                    Rows[i].Card.Selected = i == index;
            ShowMaterials(shown[index]);
        }

        /// <summary>
        /// Presses the material tile <paramref name="index"/>: opens the material's page of the
        /// library, which tells where it is found; back from it returns here.
        /// </summary>
        public void PressMaterial(int index)
        {
            var recipe = SelectedRecipe;
            if (recipe == null || index < 0 || index >= recipe.Materials.Count || Guide == null)
                return;
            var material = recipe.Materials[index];
            if (Guide.ShowInLibrary != null)
                Guide.ShowInLibrary(LibraryKinds.GuideItemKey(material.Kind), material.Id);
        }

        /// <summary>
        /// Presses "craft": a special material held in more than one quality is asked for first,
        /// one by one in the recipe's order (the picker's dark part cancels), then it crafts.
        /// </summary>
        public void PressCraft()
        {
            if (CanCraft())
                AskThenCraft(0);
        }

        private void AskThenCraft(int from)
        {
            var recipe = SelectedRecipe;
            if (recipe == null)
                return;
            for (int i = from; i < recipe.Materials.Count; i++)
            {
                var material = recipe.Materials[i];
                if (material.IsItem || !NeedsChoice(material, owned) || Picker == null)
                    continue;
                int slot = SpecialIndex(recipe, i);
                int next = i + 1;
                Picker.Open(
                    WorkshopCraft.PiecesOf(material, owned),
                    special[slot],
                    EntryOf(material.Kind, material.Id),
                    piece =>
                    {
                        ChoosePiece(slot, piece);
                        AskThenCraft(next);
                    }
                );
                return;
            }
            Craft();
        }

        /// <summary>
        /// Whether the special material is held unused in more than one quality, so the person
        /// chooses which to use; with one, or all of one quality, the worst is used as it is.
        /// </summary>
        public static bool NeedsChoice(RecipeMaterial material, IEnumerable<WorkshopOwned> owned) =>
            WorkshopCraft
                .ChoicesOf(material, owned)
                .Select(piece => Quality.Of(piece.Multiplier, piece.MaxMultiplier))
                .Distinct()
                .Count() > 1;

        /// <summary>
        /// Opens the picker for the material tile <paramref name="index"/> when it is a special
        /// material (equipment or a skill); an item's tile does nothing.
        /// </summary>
        public void OpenPicker(int index)
        {
            var recipe = SelectedRecipe;
            if (recipe == null || Picker == null || index >= recipe.Materials.Count)
                return;
            var material = recipe.Materials[index];
            if (material.IsItem)
                return;
            int slot = SpecialIndex(recipe, index);
            var pieces = WorkshopCraft.PiecesOf(material, owned);
            if (pieces.Count == 0)
                return;
            Picker.Open(
                pieces,
                special[slot],
                EntryOf(material.Kind, material.Id),
                piece => ChoosePiece(slot, piece)
            );
        }

        /// <summary>Uses <paramref name="piece"/> as the special material <paramref name="slot"/> (in the recipe's order).</summary>
        public void ChoosePiece(int slot, WorkshopOwned piece)
        {
            if (slot < 0 || slot >= special.Count || piece == null || piece.User != null)
                return;
            special[slot] = piece;
            PaintMaterials();
            RefreshDetail();
        }

        /// <summary>
        /// Makes the chosen thing from its materials and shows it at once: the materials leave,
        /// the made thing joins under a stand-in row ID until the server answers with its own.
        /// A failed save is told, and the screen reads what is held again.
        /// </summary>
        public void Craft()
        {
            if (!CanCraft())
                return;
            var recipe = SelectedRecipe;
            var used = special.ToArray();
            float multiplier = WorkshopCraft.Multiplier(recipe, used);
            // 材料の札の位置は、選び直して描き直す前に控える。
            var swirl = SwirlPlan(recipe, multiplier, UnityEngine.Random.value);
            string pending = PendingPrefix + ++crafts;
            owned = Applied(
                owned,
                recipe,
                used,
                new WorkshopCrafted(pending, multiplier, recipe.Runes, 0)
            );
            // まだ手に入れていなかった物は、作ったので名前・絵・効果を見せる。
            if (!recipe.Found)
                recipes = recipes
                    .Select(r =>
                        r == recipe
                            ? new WorkshopRecipe(
                                r.Entry,
                                r.Definition,
                                r.Stars,
                                r.MaxMultiplier,
                                true
                            )
                            : r
                    )
                    .ToArray();
            // 材料が減ったので、同じ物を選んだまま材料を選び直す。
            int keep = selected;
            shown = recipes.Where(r => r.Kind == WorkshopCraft.Kinds[tab]).ToArray();
            PaintRows();
            if (keep >= 0 && keep < shown.Count)
                Select(keep);
            string crafted = CraftedText(recipe, multiplier);
            if (Swirl != null)
            {
                if (Confirm != null)
                    StartCoroutine(Press(Confirm.transform));
                // 出来た物は演出の中で現れ、右上の絵の位置へ戻ってから、そこに出す。材料の絵は札から飛び立つ。
                SetDetailArtShown(false);
                SetMaterialArtShown(false);
                Swirl.Play(swirl, () => Landed(crafted));
            }
            else
            {
                Landed(crafted);
            }
            Work = SaveAsync(recipe, used, pending);
        }

        // 出来た物が右上へ戻った：絵を出して大きくして戻し、作った物を知らせる（null なら知らせない）。
        private void Landed(string crafted)
        {
            if (this == null)
                return;
            SetDetailArtShown(true);
            SetMaterialArtShown(true);
            if (crafted != null)
                ShowNotice(crafted);
            if (isActiveAndEnabled && DetailArt != null)
                StartCoroutine(Punch(DetailArt.rectTransform));
        }

        private void SetDetailArtShown(bool shown)
        {
            if (DetailArt != null)
                DetailArt.canvasRenderer.SetAlpha(shown ? 1f : 0f);
        }

        private void SetMaterialArtShown(bool shown)
        {
            foreach (var row in MaterialRows)
                if (row != null && row.Art != null)
                    row.Art.canvasRenderer.SetAlpha(shown ? 1f : 0f);
        }

        /// <summary>
        /// What the craft effect shows for <paramref name="recipe"/>: the shown materials' art from
        /// their tiles, the made thing's art going back to the big art on the right, and the colour
        /// of the quality it is made at.
        /// </summary>
        public WorkshopCraftSwirl.Plan SwirlPlan(
            WorkshopRecipe recipe,
            float multiplier,
            float seed
        )
        {
            var plan = new WorkshopCraftSwirl.Plan { Seed = seed };
            if (Swirl == null || recipe == null)
                return plan;
            foreach (var row in MaterialRows)
            {
                var art = row != null ? row.Art : null;
                if (art == null || !row.gameObject.activeInHierarchy || art.texture == null)
                    continue;
                plan.Pieces.Add(
                    new WorkshopCraftSwirl.Piece
                    {
                        Art = art.texture,
                        Uv = art.uvRect,
                        Tint = art.color,
                        Size = art.rectTransform.rect.size,
                        From = Swirl.PlaceOf(art.rectTransform),
                    }
                );
            }
            var entry = recipe.Entry;
            if (entry != null && entry.Art != null)
            {
                plan.ItemArt = entry.Art;
                plan.ItemUv = entry.ArtUv;
                plan.ItemTint = entry.ArtTint;
                var dots = entry.ArtDots;
                float dot = Mathf.Max(
                    1f,
                    Mathf.Floor(DetailArtBox / Mathf.Max(1f, Mathf.Max(dots.x, dots.y)))
                );
                plan.ItemSize = dots * dot;
            }
            if (DetailArt != null)
                plan.ItemHome = Swirl.PlaceOf(DetailArt.rectTransform);
            plan.Quality = QualityPalette.Of(Quality.Of(multiplier, recipe.MaxMultiplier));
            plan.Look = WorkshopCraftSwirl.LookOf(recipe.Stars);
            return plan;
        }

        private async Task SaveAsync(
            WorkshopRecipe recipe,
            IReadOnlyList<WorkshopOwned> used,
            string pending
        )
        {
            try
            {
                var crafted = await WorkshopSession.CraftAsync(recipe, used);
                // 仮の行IDを本当のIDへ替える。材料に選んでいれば、同じ物なので一緒に替わる。
                var made = owned.FirstOrDefault(thing => thing.RowId == pending);
                if (made != null)
                {
                    made.RowId = crafted.ItemId;
                    made.Multiplier = crafted.Multiplier;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(FailedMessage + "。" + exception.Message);
                if (this == null || !isActiveAndEnabled)
                {
                    // 画面を閉じていれば、開いている案内人の画面かホームで知らせる。
                    SaveQueue.Report(FailedMessage);
                    return;
                }
                ShowNotice(FailedMessage);
                StartLoad(LoadAsync, LoadFailedMessage, ShowLoadFailed);
                return;
            }
            if (this != null && isActiveAndEnabled)
                RefreshDetail();
        }

        /// <summary>
        /// The held things after a craft: the recipe's items taken from their counts (a kind used
        /// up gone), the special materials gone, and the made thing added.
        /// </summary>
        public static IReadOnlyList<WorkshopOwned> Applied(
            IReadOnlyList<WorkshopOwned> before,
            WorkshopRecipe recipe,
            IReadOnlyList<WorkshopOwned> used,
            WorkshopCrafted crafted
        )
        {
            var after = before.Select(thing => thing.Copy()).ToList();
            foreach (var material in recipe.Materials.Where(material => material.IsItem))
            {
                var item = after.FirstOrDefault(thing =>
                    thing.Kind == LibraryKind.Item && thing.KindId == material.Id
                );
                if (item == null)
                    continue;
                item.Count -= material.Count;
                if (item.Count <= 0)
                    after.Remove(item);
            }
            foreach (var piece in used.Where(piece => piece != null))
                after.RemoveAll(thing => thing.Kind == piece.Kind && thing.RowId == piece.RowId);
            after.Add(
                new WorkshopOwned
                {
                    Kind = recipe.Kind,
                    RowId = crafted.ItemId,
                    KindId = recipe.Id,
                    Name = recipe.Name,
                    Stars = recipe.Stars,
                    Multiplier = crafted.Multiplier,
                    MaxMultiplier = recipe.MaxMultiplier,
                }
            );
            return after;
        }

        private bool CanCraft()
        {
            var recipe = SelectedRecipe;
            // サーバーの答えを待っている出来た物は、本当の行IDが届くまで材料に使えない。
            return recipe != null
                && WorkshopCraft.CanMake(recipe, owned)
                && special.All(piece => piece != null && !piece.RowId.StartsWith(PendingPrefix))
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

        /// <summary>
        /// Writes a recipe's row: shadowed and unnamed until made once, dim when the materials
        /// are short. The generator bakes the mock recipes with it too.
        /// </summary>
        public static void Paint(ItemTile row, WorkshopRecipe recipe, bool canMake, float artBox)
        {
            LibraryCatalogView.PaintArt(row.Art, recipe.Entry, recipe.Found, artBox);
            // 名前のすぐ右に☆、2行目に効果、右上に属性。品質は作るまで決まらないため、行には出さない。
            Set(
                row.Name,
                ItemText.Heading(recipe.Found ? recipe.Name : Hidden, recipe.Stars, false, default, 1f)
            );
            Set(
                row.Detail,
                recipe.Found ? LibraryFaces.EffectAt(recipe.Entry, Quality.MinMultiplier) : Hidden
            );
            if (row.Elements != null)
                row.Elements.Show(
                    recipe.Found ? LibraryFaces.ElementsOf(recipe.Kind, recipe.Id) : null
                );
            if (row.Card != null)
            {
                row.Card.Selected = false;
                row.Card.Dimmed = !canMake;
            }
        }

        private void ShowMaterials(WorkshopRecipe recipe)
        {
            special.Clear();
            special.AddRange(WorkshopCraft.FirstChoice(recipe, owned));
            if (MaterialsTitle != null)
                MaterialsTitle.SetActive(recipe != null);
            PaintMaterials();
            RefreshDetail();
        }

        private void PaintMaterials()
        {
            var recipe = SelectedRecipe;
            var materials = recipe?.Materials ?? Array.Empty<RecipeMaterial>();
            for (int i = 0; i < MaterialRows.Count; i++)
            {
                var row = MaterialRows[i];
                bool visible = i < materials.Count;
                row.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var material = materials[i];
                var piece = material.IsItem ? null : special[SpecialIndex(recipe, i)];
                PaintMaterial(
                    row,
                    material,
                    EntryOf(material.Kind, material.Id),
                    records.IsFound(material.Kind, material.Id),
                    WorkshopCraft.HaveOf(material, owned),
                    piece,
                    MaterialArtBox
                );
            }
        }

        /// <summary>
        /// Writes a material's tile: the art (shadowed until found), the held and needed counts
        /// (the held red when short), the name, and for a special material the chosen piece's
        /// quality at the top left.
        /// </summary>
        public static void PaintMaterial(
            ItemTile row,
            RecipeMaterial material,
            LibraryEntryData entry,
            bool found,
            int have,
            WorkshopOwned piece,
            float artBox
        )
        {
            LibraryCatalogView.PaintArt(row.Art, entry, found, artBox);
            Set(row.Name, found ? entry?.Name : Hidden);
            Set(row.Stars, ItemText.Stars(entry?.Rarity ?? 0));
            Set(row.Count, CountText(have, material.Count));
            if (row.Quality != null)
            {
                row.Quality.transform.parent.gameObject.SetActive(piece != null);
                if (piece != null)
                {
                    row.Quality.text = Quality.Letter(piece.Quality);
                    row.Quality.color = QualityPalette.Of(piece.Quality);
                }
            }
            if (row.Mark != null)
                row.Mark.SetActive(false);
        }

        /// <summary>The held and needed counts, "1/2", the held red when short.</summary>
        public static string CountText(int have, int need) =>
            have < need ? $"<color=#{ShortColor}>{have}</color>/{need}" : $"{have}/{need}";

        private void RefreshDetail()
        {
            var recipe = SelectedRecipe;
            // 作れる物がないときは、絵の後ろの光ごと隠す。
            if (DetailArt != null)
                DetailArt.transform.parent.gameObject.SetActive(recipe != null);
            // 特別な材料を選び終えるまでは、出来上がる品質を出さず、効果は×1.00の値にする。
            bool chosen = recipe != null && special.All(piece => piece != null);
            float multiplier = chosen ? WorkshopCraft.Multiplier(recipe, special) : Quality.MinMultiplier;
            if (recipe != null)
                LibraryCatalogView.PaintArt(DetailArt, recipe.Entry, recipe.Found, DetailArtBox);
            Show(DetailQuality, chosen ? QualityOf(recipe, special) : "");
            Show(
                DetailName,
                recipe == null ? ""
                    : recipe.Found ? recipe.Name
                    : Hidden
            );
            Show(DetailStars, recipe != null ? ItemText.Stars(recipe.Stars) : "");
            Show(
                DetailEffect,
                recipe == null ? ""
                    : recipe.Found ? LibraryFaces.EffectAt(recipe.Entry, multiplier)
                    : Hidden
            );
            if (DetailElements != null)
            {
                DetailElements.Show(
                    recipe != null && recipe.Found
                        ? LibraryFaces.ElementsOf(recipe.Kind, recipe.Id)
                        : null
                );
                DetailElements.gameObject.SetActive(DetailElements.Any);
            }
            if (Price != null)
                Price.SetActive(recipe != null);
            Set(PriceLabel, recipe != null ? recipe.Runes.ToString("N0") : "");
            Set(
                Missing,
                recipe != null ? MissingText(recipe, owned, records, RuneBalance.Known) : ""
            );
            if (Confirm != null)
                Confirm.interactable = CanCraft();
        }

        /// <summary>
        /// The made thing's quality from the special materials: the letter in its color, large,
        /// and the multiplier, with no "quality" label (C ×1.00 without any special material).
        /// </summary>
        public static string QualityOf(WorkshopRecipe recipe, IReadOnlyList<WorkshopOwned> used)
        {
            float multiplier = WorkshopCraft.Multiplier(recipe, used);
            var quality = Quality.Of(multiplier, recipe.MaxMultiplier);
            return ItemText.DetailQuality(quality, multiplier);
        }

        /// <summary>What is short: the first material short (its name hidden until found), or the runes. Empty when nothing is.</summary>
        public static string MissingText(
            WorkshopRecipe recipe,
            IReadOnlyList<WorkshopOwned> held,
            LibraryRecords found,
            long? runes
        )
        {
            foreach (var material in recipe.Materials)
            {
                int have = WorkshopCraft.HaveOf(material, held);
                if (have >= material.Count)
                    continue;
                string name =
                    found != null && found.IsFound(material.Kind, material.Id)
                        ? NameOf(material)
                        : Hidden;
                return $"{name}が {material.Count - have} つ足りません";
            }
            return runes is { } balance && balance < recipe.Runes
                ? $"ルーンが {recipe.Runes - balance:N0} 足りません"
                : "";
        }

        public static string CraftedText(WorkshopRecipe recipe, float multiplier)
        {
            var quality = Quality.Of(multiplier, recipe.MaxMultiplier);
            return $"{recipe.Name}（品質{Quality.Letter(quality)}）を作った";
        }

        public static string EmptyText(string kind) => $"作れる{kind}がまだありません";

        // 材料の名前（定義の名前）。
        private static string NameOf(RecipeMaterial material) =>
            material.Kind switch
            {
                LibraryKind.Item => Items.ItemCards.Find(material.Id)?.Name ?? material.Id,
                LibraryKind.Skill => Combat.CardSkills.Find(material.Id)?.Name ?? material.Id,
                _ => Equipment.EquipmentCatalog.Find(material.Id)?.Name ?? material.Id,
            };

        // 特別な材料の、レシピの材料の並びでの番号から、選んだ物の並びでの番号へ。
        private static int SpecialIndex(WorkshopRecipe recipe, int materialIndex) =>
            recipe.Materials.Take(materialIndex).Count(material => !material.IsItem);

        private LibraryEntryData EntryOf(LibraryKind kind, string id) =>
            Data != null
                ? Data.Entries.FirstOrDefault(entry =>
                    entry != null && entry.Kind == kind && entry.Id == id
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

        // 押した瞬間：ボタンが沈んで戻る。
        private static IEnumerator Press(Transform button)
        {
            const float duration = 0.16f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                button.localScale = Vector3.one * (1f - 0.08f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            button.localScale = Vector3.one;
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

        // 読み込むまで、一覧の見える所をカードの裏で埋める。
        private void ShowLoading()
        {
            recipes = shown = Array.Empty<WorkshopRecipe>();
            selected = -1;
            EnsureRows(CoverRows);
            for (int i = 0; i < Rows.Count; i++)
                Rows[i].gameObject.SetActive(i < CoverRows);
            cover.Cover(Rows.Take(CoverRows).Select(row => row.gameObject));
            Set(Status, "");
            ShowMaterials(null);
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

        // 詳しい面の行：書くことのない行は隠して、列を詰める。
        private static void Show(TMP_Text label, string text)
        {
            Set(label, text);
            if (label != null)
                label.gameObject.SetActive(label.text.Length > 0);
        }

        // 停止中の表示の切り替え：作れる物、選んだとき、使う物を選ぶ、読み込む間、読めなかったとき。
        public IReadOnlyList<string> PreviewStates =>
            new[] { "作れる物", "選んだとき", "使う物を選ぶ" }
                .Concat(GuidePanelLoad.PreviewStates)
                .Concat(SwirlStates.Select(state => state.Name))
                .ToArray();

        // 合成の演出の止めたコマ：見た目と、押してからの秒（弾ける拍からの秒は Burst に足す）。
        private static readonly (
            string Name,
            WorkshopCraftSwirl.Look Look,
            float Seconds,
            bool FromBurst
        )[] SwirlStates =
        {
            ("合成の演出・材料が飛ぶ", WorkshopCraftSwirl.Look.Plain, 0.18f, false),
            ("合成の演出・渦", WorkshopCraftSwirl.Look.Plain, 0.55f, false),
            ("合成の演出・詰まる", WorkshopCraftSwirl.Look.Plain, 0.86f, false),
            ("合成の演出・弾ける", WorkshopCraftSwirl.Look.Plain, 0.05f, true),
            ("合成の演出・現れる", WorkshopCraftSwirl.Look.Plain, 0.22f, true),
            ("合成の演出・☆4 渦", WorkshopCraftSwirl.Look.Gold, 0.55f, false),
            ("合成の演出・☆4 溜める", WorkshopCraftSwirl.Look.Gold, -0.06f, true),
            ("合成の演出・☆4 弾ける", WorkshopCraftSwirl.Look.Gold, 0.05f, true),
            ("合成の演出・☆4 現れる", WorkshopCraftSwirl.Look.Gold, 0.25f, true),
            ("合成の演出・☆5 渦", WorkshopCraftSwirl.Look.Rainbow, 0.55f, false),
            ("合成の演出・☆5 溜める", WorkshopCraftSwirl.Look.Rainbow, -0.08f, true),
            ("合成の演出・☆5 弾ける", WorkshopCraftSwirl.Look.Rainbow, 0.06f, true),
            ("合成の演出・☆5 現れる", WorkshopCraftSwirl.Look.Rainbow, 0.3f, true),
        };
        private const int SwirlStateFirst = 5;

        public void ShowPreviewState(int index)
        {
            if (Picker != null)
                Picker.Close();
            if (Swirl != null)
                Swirl.gameObject.SetActive(false);
            SetDetailArtShown(true);
            SetMaterialArtShown(true);
            if (index >= SwirlStateFirst && index < SwirlStateFirst + SwirlStates.Length)
            {
                var state = SwirlStates[index - SwirlStateFirst];
                var beats = WorkshopCraftSwirl.Beats.Of(state.Look);
                ShowSwirlState(state.Look, state.Seconds + (state.FromBurst ? beats.Burst : 0f));
                return;
            }
            if (index <= 2)
            {
                var mock = LibraryMemorySource.Mock();
                Render(WorkshopCraft.Recipes(Data, mock), WorkshopStock.Mock(), mock);
                if (index == 0)
                    return;
                // 特別な材料のあるレシピを選び、使う物を選ぶ画面ではその材料の札を開く。
                int pick = Math.Max(
                    0,
                    Array.FindIndex(
                        shown.ToArray(),
                        recipe => recipe.Materials.Any(material => !material.IsItem)
                    )
                );
                Select(pick);
                if (index == 2 && SelectedRecipe is { } recipe)
                    OpenPicker(recipe.Materials.ToList().FindIndex(material => !material.IsItem));
                return;
            }
            ShowLoading();
            if (index == 4)
                ShowLoadFailed();
        }

        // 仮の持ち物で鉄の剣（材料の札が2つ）を選び、合成の演出の1コマを止めて描く。
        private void ShowSwirlState(WorkshopCraftSwirl.Look look, float seconds)
        {
            var mock = LibraryMemorySource.Mock();
            Render(WorkshopCraft.Recipes(Data, mock), WorkshopStock.Mock(), mock);
            int sword = Math.Max(
                0,
                Array.FindIndex(shown.ToArray(), recipe => recipe.Id == "iron-sword")
            );
            Select(sword);
            if (Swirl == null || SelectedRecipe is not { } recipe)
                return;
            Combat.Presentation.BattleVfxNoise.Ensure();
            Combat.Presentation.BattleVfxImage.EnableShapeChannel(Swirl.transform);
            Canvas.ForceUpdateCanvases();
            // 仮の持ち物で作れるのは☆2の物だけなので、☆4・☆5の見た目は見た目だけ替えて見せる。
            var plan = SwirlPlan(recipe, WorkshopCraft.Multiplier(recipe, special), 0.37f);
            plan.Look = look;
            Swirl.gameObject.SetActive(true);
            SetDetailArtShown(seconds >= WorkshopCraftSwirl.Beats.Of(look).Seconds);
            SetMaterialArtShown(seconds < WorkshopCraftSwirl.Beats.FlyStart);
            Swirl.Show(seconds, plan);
        }
    }
}
