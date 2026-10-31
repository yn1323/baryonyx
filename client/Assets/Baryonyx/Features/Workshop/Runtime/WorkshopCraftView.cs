using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Economy;
using Baryonyx.Enhancement;
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
    /// The last tab makes polishing stones: three of a colour and runes make one of the next
    /// (doc/features/enhancement.md), shown when the server answers.
    /// </summary>
    public sealed class WorkshopCraftView : GuidePanel, IEditModePreview
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
        public WorkshopGoodRow Template;
        public List<WorkshopGoodRow> Rows = new();
        public TMP_Text Status;

        // 右の面：出来上がる物の絵・名前・☆・効果・出来上がる品質。
        public RawImage DetailArt;
        public TMP_Text DetailName;
        public TMP_Text DetailStars;
        public TMP_Text DetailEffect;
        public TMP_Text DetailQuality;

        // 材料：見出しと、レシピの材料の札（多くて4つ）。
        public GameObject MaterialsTitle;
        public List<WorkshopGoodRow> MaterialRows = new();

        // 特別な材料（装備・スキル）の、使う物を選ぶ画面。
        public WorkshopMaterialPicker Picker;

        public GameObject Price;
        public TMP_Text PriceLabel;
        public TMP_Text Missing;
        public Button Confirm;

        // 合成したときの、画面全体の演出（光の渦）。
        public WorkshopCraftSwirl Swirl;

        // 磨き石のタブの、石の絵（白・青・紫・金の順）。生成で ItemArt から写す。
        public Texture[] StoneArts = Array.Empty<Texture>();

        // 磨き石のタブに並べる、合成で作れる色（青・紫・金）。
        public static readonly IReadOnlyList<EnhancementMaterial> Stones =
            EnhancementMaterials.Combined.ToArray();

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
        private bool combining;

        // 直前の石の合成が保存できなかったか。演出が戻ったときに「作った」と知らせない。
        private bool stoneFailed;

        public IReadOnlyList<WorkshopRecipe> Recipes => recipes;
        public IReadOnlyList<WorkshopRecipe> Shown => shown;
        public IReadOnlyList<WorkshopOwned> Owned => owned;

        /// <summary>The chosen pieces of the selected recipe's special materials, in its order (null when none is held).</summary>
        public IReadOnlyList<WorkshopOwned> Special => special;

        public int Tab => tab;
        public int Selected => selected;

        /// <summary>Whether the polishing stones' tab (the last one) is shown.</summary>
        public bool StonesTab => tab == WorkshopCraft.Kinds.Count;

        private EnhancementMaterial SelectedStone =>
            StonesTab && selected >= 0 && selected < Stones.Count ? Stones[selected] : null;

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
                Confirm.onClick.AddListener(Craft);
            for (int i = 0; i < MaterialRows.Count; i++)
            {
                var row = MaterialRows[i];
                if (row == null || row.Card == null || !bound.Add(row))
                    continue;
                int index = i;
                row.Card.Clicked += () => OpenPicker(index);
            }
        }

        private async Task LoadAsync(CancellationToken token)
        {
            var stock = WorkshopStock.LoadAsync(Party, token);
            var stones = EnhancementSession.LoadAsync(token);
            var read = await LibrarySession.LoadAsync(token);
            var held = await stock;
            await stones;
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
        }

        public void ShowTab(int index)
        {
            tab = Mathf.Clamp(index, 0, WorkshopCraft.TabLabels.Length - 1);
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i == tab;
            if (StonesTab)
            {
                ShowStones();
                return;
            }
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
            if (StonesTab)
            {
                SelectStone(index);
                return;
            }
            if (index < 0 || index >= shown.Count)
                return;
            selected = index;
            for (int i = 0; i < shown.Count && i < Rows.Count; i++)
                if (Rows[i].Card != null)
                    Rows[i].Card.Selected = i == index;
            ShowMaterials(shown[index]);
        }

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
            if (StonesTab)
            {
                Combine();
                return;
            }
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
        public static void Paint(
            WorkshopGoodRow row,
            WorkshopRecipe recipe,
            bool canMake,
            float artBox
        )
        {
            LibraryCatalogView.PaintArt(row.Art, recipe.Entry, recipe.Found, artBox);
            Set(row.Name, recipe.Found ? recipe.Name : Hidden);
            Set(row.Detail, WorkshopBuyView.StarsText(recipe.Stars));
            Set(row.Price, "");
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
            WorkshopGoodRow row,
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
            if (row.InUse != null)
                row.InUse.SetActive(false);
        }

        /// <summary>The held and needed counts, "1/2", the held red when short.</summary>
        public static string CountText(int have, int need) =>
            have < need ? $"<color=#{ShortColor}>{have}</color>/{need}" : $"{have}/{need}";

        private void RefreshDetail()
        {
            if (StonesTab)
            {
                RefreshStone();
                return;
            }
            var recipe = SelectedRecipe;
            if (DetailArt != null)
            {
                // 作れる物がないときは、絵の後ろの光ごと隠す。
                DetailArt.transform.parent.gameObject.SetActive(recipe != null);
                if (recipe != null)
                    LibraryCatalogView.PaintArt(
                        DetailArt,
                        recipe.Entry,
                        recipe.Found,
                        DetailArtBox
                    );
            }
            Set(
                DetailName,
                recipe == null ? ""
                    : recipe.Found ? recipe.Name
                    : Hidden
            );
            Set(DetailStars, recipe != null ? WorkshopBuyView.StarsText(recipe.Stars) : "");
            Set(
                DetailEffect,
                recipe == null ? ""
                    : recipe.Found ? EffectOf(recipe)
                    : Hidden
            );
            Set(
                DetailQuality,
                recipe != null && special.All(piece => piece != null)
                    ? QualityOf(recipe, special)
                    : ""
            );
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
            return $"<voffset=-0.15em><size=200%>{QualityPalette.Painted(quality)}</size></voffset>  {Quality.Times(multiplier)}";
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

        private static string EffectOf(WorkshopRecipe recipe)
        {
            var entry = recipe.Entry;
            return recipe.Kind == LibraryKind.Skill
                ? string.Join(
                    "\n",
                    new[] { entry.KindLine, entry.Description }.Where(text =>
                        !string.IsNullOrEmpty(text)
                    )
                )
                : entry.Description;
        }

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

        // 磨き石のタブ：作れる色を並べ、先頭を選んでおく。
        private void ShowStones()
        {
            shown = Array.Empty<WorkshopRecipe>();
            selected = -1;
            special.Clear();
            EnsureRows(Stones.Count);
            for (int i = 0; i < Rows.Count; i++)
                Rows[i].gameObject.SetActive(i < Stones.Count);
            PaintStones();
            Set(Status, "");
            cover.Reveal(
                Rows.Take(Stones.Count).Select(row => row.gameObject),
                Rows.Skip(Stones.Count).Take(CoverRows - Stones.Count).Select(row => row.gameObject)
            );
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            SelectStone(0);
        }

        private void PaintStones()
        {
            for (int i = 0; i < Stones.Count && i < Rows.Count; i++)
                PaintStone(
                    Rows[i],
                    Stones[i],
                    CanCombine(Stones[i]),
                    StoneArtOf(Stones[i]),
                    ArtBox
                );
        }

        public void SelectStone(int index)
        {
            if (index < 0 || index >= Stones.Count)
                return;
            selected = index;
            for (int i = 0; i < Stones.Count && i < Rows.Count; i++)
                if (Rows[i].Card != null)
                    Rows[i].Card.Selected = i == index;
            RefreshStone();
        }

        /// <summary>Writes a stone's row: its art, name and stars, dim when it cannot be made now.</summary>
        public static void PaintStone(
            WorkshopGoodRow row,
            EnhancementMaterial stone,
            bool canMake,
            Texture art,
            float artBox
        )
        {
            PaintTexture(row.Art, art, artBox);
            Set(row.Name, stone.Name);
            Set(row.Detail, WorkshopBuyView.StarsText(stone.Stars));
            Set(row.Price, "");
            if (row.Card != null)
            {
                row.Card.Selected = false;
                row.Card.Dimmed = !canMake;
            }
        }

        // 右の面：作る石の絵・名前・☆・上げ幅と上限、持っている数。材料は1つ下の色の札を1枚。
        private void RefreshStone()
        {
            var made = SelectedStone;
            var from = EnhancementMaterials.CombineFrom(made);
            if (DetailArt != null)
            {
                DetailArt.transform.parent.gameObject.SetActive(made != null);
                if (made != null)
                    PaintTexture(DetailArt, StoneArtOf(made), DetailArtBox);
            }
            Set(DetailName, made?.Name ?? "");
            Set(DetailStars, made != null ? WorkshopBuyView.StarsText(made.Stars) : "");
            Set(DetailEffect, made != null ? StoneEffect(made) : "");
            Set(
                DetailQuality,
                made != null
                    ? WorkshopEnhanceView.CountText(EnhancementSession.CountOf(made.Id))
                    : ""
            );
            if (MaterialsTitle != null)
                MaterialsTitle.SetActive(made != null);
            for (int i = 0; i < MaterialRows.Count; i++)
            {
                var row = MaterialRows[i];
                bool visible = i == 0 && from != null;
                row.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                PaintTexture(row.Art, StoneArtOf(from), MaterialArtBox);
                Set(row.Name, from.Name);
                Set(
                    row.Count,
                    CountText(
                        EnhancementSession.CountOf(from.Id),
                        EnhancementMaterials.CombineInputs
                    )
                );
                if (row.Quality != null)
                    row.Quality.transform.parent.gameObject.SetActive(false);
                if (row.InUse != null)
                    row.InUse.SetActive(false);
            }
            if (Price != null)
                Price.SetActive(made != null);
            Set(PriceLabel, made != null ? made.CombineRunes.ToString("N0") : "");
            Set(Missing, made != null ? StoneMissingText(made, RuneBalance.Known) : "");
            if (Confirm != null)
                Confirm.interactable = CanCombine(made);
        }

        private bool CanCombine(EnhancementMaterial made)
        {
            var from = EnhancementMaterials.CombineFrom(made);
            return !combining
                && from != null
                && EnhancementSession.CountOf(from.Id) >= EnhancementMaterials.CombineInputs
                && (RuneBalance.Known is not { } runes || runes >= made.CombineRunes);
        }

        /// <summary>
        /// Makes one stone of the chosen colour from three of the one below, and shows it when
        /// the server answers. The effect plays at once, the three stones flying into the swirl.
        /// </summary>
        public void Combine()
        {
            var made = SelectedStone;
            if (!CanCombine(made))
                return;
            var plan = StonePlan(made, UnityEngine.Random.value);
            combining = true;
            stoneFailed = false;
            RefreshStone();
            string text = CombinedText(made);
            if (Swirl != null)
            {
                if (Confirm != null)
                    StartCoroutine(Press(Confirm.transform));
                SetDetailArtShown(false);
                SetMaterialArtShown(false);
                Swirl.Play(plan, () => Landed(stoneFailed ? null : text));
            }
            Work = CombineAsync(made, Swirl == null ? text : null);
        }

        private async Task CombineAsync(EnhancementMaterial made, string notice)
        {
            try
            {
                await EnhancementSession.CombineAsync(made, 1);
                if (notice != null && this != null && isActiveAndEnabled)
                    ShowNotice(notice);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogWarning(FailedMessage + "。" + exception.Message);
                stoneFailed = true;
                if (this == null || !isActiveAndEnabled)
                    SaveQueue.Report(FailedMessage);
                else
                    ShowNotice(FailedMessage);
            }
            finally
            {
                combining = false;
                if (this != null && isActiveAndEnabled && StonesTab)
                {
                    PaintStones();
                    if (selected >= 0 && selected < Rows.Count && Rows[selected].Card != null)
                        Rows[selected].Card.Selected = true;
                    RefreshStone();
                }
            }
        }

        /// <summary>
        /// What the combine effect shows: three of the lower stone flying from its tile, side by
        /// side, the made stone going back to the big art, ringed in the made stone's colour.
        /// </summary>
        public WorkshopCraftSwirl.Plan StonePlan(EnhancementMaterial made, float seed)
        {
            var plan = new WorkshopCraftSwirl.Plan { Seed = seed };
            if (Swirl == null || made == null)
                return plan;
            var tile = MaterialRows.Count > 0 ? MaterialRows[0] : null;
            var art = tile != null ? tile.Art : null;
            if (art != null && art.texture != null && tile.gameObject.activeInHierarchy)
            {
                var size = art.rectTransform.rect.size;
                var from = Swirl.PlaceOf(art.rectTransform);
                int pieces = Math.Min(EnhancementMaterials.CombineInputs, Swirl.Pieces.Length);
                for (int i = 0; i < pieces; i++)
                    plan.Pieces.Add(
                        new WorkshopCraftSwirl.Piece
                        {
                            Art = art.texture,
                            Uv = art.uvRect,
                            Tint = art.color,
                            Size = size,
                            // 3つの石が札の上で横に並んで飛び立つ。
                            From = from + new Vector2((i - (pieces - 1) / 2f) * size.x * 0.7f, 0f),
                        }
                    );
            }
            var madeArt = StoneArtOf(made);
            if (madeArt != null)
            {
                plan.ItemArt = madeArt;
                var dots = new Vector2(madeArt.width, madeArt.height);
                float dot = Mathf.Max(
                    1f,
                    Mathf.Floor(DetailArtBox / Mathf.Max(1f, Mathf.Max(dots.x, dots.y)))
                );
                plan.ItemSize = dots * dot;
            }
            if (DetailArt != null)
                plan.ItemHome = Swirl.PlaceOf(DetailArt.rectTransform);
            plan.Quality = WorkshopEnhanceView.StoneColor(made);
            plan.Look = WorkshopCraftSwirl.LookOf(made.Stars);
            return plan;
        }

        private Texture StoneArtOf(EnhancementMaterial stone)
        {
            for (int i = 0; i < EnhancementMaterials.Polishes.Count && i < StoneArts.Length; i++)
                if (EnhancementMaterials.Polishes[i] == stone)
                    return StoneArts[i];
            return null;
        }

        /// <summary>How far a stone moves the quality gauge, and up to where: "ゲージを10%進める（Aまで）".</summary>
        public static string StoneEffect(EnhancementMaterial stone) =>
            $"ゲージを{stone.RatePercent}%進める（"
            + (stone.Ceiling is { } ceiling ? $"{Quality.Letter(ceiling)}まで" : "上限まで")
            + "）";

        /// <summary>What is short to make <paramref name="made"/>: the lower stones, or the runes. Empty when nothing is.</summary>
        public static string StoneMissingText(EnhancementMaterial made, long? runes)
        {
            var from = EnhancementMaterials.CombineFrom(made);
            if (from == null)
                return "";
            int have = EnhancementSession.CountOf(from.Id);
            if (have < EnhancementMaterials.CombineInputs)
                return $"{from.Name}が {EnhancementMaterials.CombineInputs - have} つ足りません";
            return runes is { } balance && balance < made.CombineRunes
                ? $"ルーンが {made.CombineRunes - balance:N0} 足りません"
                : "";
        }

        public static string CombinedText(EnhancementMaterial made) => $"{made.Name}を作った";

        // ドット絵を、枠に収まるいちばん大きい整数倍で出す。
        private static void PaintTexture(RawImage image, Texture art, float box)
        {
            if (image == null)
                return;
            image.texture = art;
            image.enabled = art != null;
            if (art == null)
                return;
            image.uvRect = new Rect(0f, 0f, 1f, 1f);
            image.color = Color.white;
            float dot = Mathf.Max(1f, Mathf.Floor(box / Mathf.Max(art.width, art.height)));
            image.rectTransform.sizeDelta = new Vector2(art.width, art.height) * dot;
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

        // 停止中の表示の切り替え：作れる物、選んだとき、使う物を選ぶ、読み込む間、読めなかったとき。
        public IReadOnlyList<string> PreviewStates =>
            new[] { "作れる物", "選んだとき", "使う物を選ぶ" }
                .Concat(GuidePanelLoad.PreviewStates)
                .Concat(SwirlStates.Select(state => state.Name))
                .Concat(StoneStates.Select(state => state.Name))
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

        // 磨き石のタブと、石の合成の演出の止めたコマ：作る色（Stones の番号）と押してからの秒（負なら一覧だけ）。
        private static readonly (
            string Name,
            int Stone,
            float Seconds,
            bool FromBurst
        )[] StoneStates =
        {
            ("磨き石", 0, -1f, false),
            ("磨き石の合成・飛ぶ", 0, 0.18f, false),
            ("磨き石の合成・渦", 0, 0.55f, false),
            ("磨き石の合成・現れる", 0, 0.22f, true),
            ("磨き石の合成・金 渦", 2, 0.55f, false),
            ("磨き石の合成・金 現れる", 2, 0.25f, true),
        };
        private static int StoneStateFirst => SwirlStateFirst + SwirlStates.Length;

        public void ShowPreviewState(int index)
        {
            if (Picker != null)
                Picker.Close();
            if (Swirl != null)
                Swirl.gameObject.SetActive(false);
            SetDetailArtShown(true);
            SetMaterialArtShown(true);
            if (index >= StoneStateFirst && index < StoneStateFirst + StoneStates.Length)
            {
                var state = StoneStates[index - StoneStateFirst];
                float burst = WorkshopCraftSwirl
                    .Beats.Of(WorkshopCraftSwirl.LookOf(Stones[state.Stone].Stars))
                    .Burst;
                ShowStoneState(state.Stone, state.Seconds + (state.FromBurst ? burst : 0f));
                return;
            }
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

        // 仮の素材で磨き石のタブを開いて色を選び、seconds が0以上なら石の合成の演出の1コマを止めて描く。
        private void ShowStoneState(int stone, float seconds)
        {
            // 停止中はサーバーがなく、仮の素材がその場で読める。
            if (EnhancementSession.Source == null)
                _ = EnhancementSession.LoadAsync(CancellationToken.None);
            var mock = LibraryMemorySource.Mock();
            Render(WorkshopCraft.Recipes(Data, mock), WorkshopStock.Mock(), mock);
            ShowTab(WorkshopCraft.Kinds.Count);
            SelectStone(stone);
            if (seconds < 0f || Swirl == null || SelectedStone is not { } made)
                return;
            Combat.Presentation.BattleVfxNoise.Ensure();
            Combat.Presentation.BattleVfxImage.EnableShapeChannel(Swirl.transform);
            Canvas.ForceUpdateCanvases();
            var plan = StonePlan(made, 0.37f);
            Swirl.gameObject.SetActive(true);
            SetDetailArtShown(seconds >= WorkshopCraftSwirl.Beats.Of(plan.Look).Seconds);
            SetMaterialArtShown(seconds < WorkshopCraftSwirl.Beats.FlyStart);
            Swirl.Show(seconds, plan);
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
