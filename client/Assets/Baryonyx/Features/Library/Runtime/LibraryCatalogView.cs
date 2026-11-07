using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Combat.Records;
using Baryonyx.SkillEvolution;
using Baryonyx.Training;
using Baryonyx.UI;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using Baryonyx.WorldMap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.UI.UiText;

namespace Baryonyx.Library
{
    /// <summary>
    /// One kind of the library (doc/features/library.md) inside the guide screen's frame: the
    /// cards of every entry on the left, in the definitions' order, and the chosen entry on the
    /// right. Entries the player has not found show only their number and the art as a shadow,
    /// without the name. The generator bakes the cards from <see cref="Data"/>; this view reads
    /// what the player found each time it opens (<see cref="LibrarySession"/>) and draws it.
    /// </summary>
    public sealed class LibraryCatalogView
        : GuidePanel,
            IEditModePreview,
            IGuideBackHandler,
            IGuideEntryTarget
    {
        public const string LoadFailedMessage = "ライブラリを取得できませんでした";
        public const string PreparingText = "準備中";

        // 読み込む間に伏せる、先頭のカードの数（4列×3段で、開いたときに見える所）。
        public const int CoverCards = 12;

        // まだ見つけていない絵の影の色。
        public static readonly Color ShadowTint = new(0.02f, 0.03f, 0.08f, 0.88f);

        public LibraryKind Kind;
        public LibraryData Data;

        // 合成のレシピ（武器・防具・アクセサリー・スキル）：見出しと材料の札。レシピのない物には出さない。
        public GameObject Recipe;
        public List<ItemTile> RecipeRows = new();

        // キャラの固有スキルのツリーと、進化の仮の進み具合。
        public TrainingMockData Training;

        // キャラクター：固有スキル3つを、今セットしている段階の戦闘のカード（効果の文まで）と覚醒の進み具合で、
        // パッシブを1行ずつ（アイコン・名前・効果・解放するLv）で出す。2つの見出しで分ける（2026-11-04、
        // ユーザーの指示。それまでは右の面の下に固有スキルを文字だけで出していた）。
        public GameObject CharacterSkills;
        public ScrollRect CharacterScroll;
        public TrainingCardWidget[] UniqueCards = Array.Empty<TrainingCardWidget>();
        public TMP_Text[] UniqueSteps = Array.Empty<TMP_Text>();
        public TrainingSkillWidget[] PassiveRows = Array.Empty<TrainingSkillWidget>();
        public Baryonyx.Party.PartyMockData Party;
        public Texture[] CardFrames = Array.Empty<Texture>();
        public Sprite[] ElementIcons = Array.Empty<Sprite>();

        public ItemTile[] Tiles = Array.Empty<ItemTile>();

        // 装備の武器・防具・アクセサリーを切り替えるタブと、それぞれが開く項目。タブのない分類は空。
        public GuideTab[] KindTabs = Array.Empty<GuideTab>();
        public string[] KindTabKeys = Array.Empty<string>();

        // 絵を置く正方形の一辺（設計座標）。ドット絵は、収まるいちばん大きい整数倍で出す。
        public float TileArtBox = 192f;
        public float DetailArtBox = 320f;
        public ScrollRect List;

        // 見つけた数と全体の数（例：12/50）。
        public TMP_Text Count;

        // 読み込み中・読めなかったとき・定義がないとき（準備中）だけ一覧に出す文字。
        public TMP_Text Status;

        [Header("個別")]
        public GameObject Detail;
        public RawImage DetailArt;
        public TMP_Text DetailNumber;
        public TMP_Text DetailName;
        public TMP_Text DetailRarity;
        public TMP_Text DetailKind;
        public TMP_Text DetailBody;
        public TMP_Text DetailExtra;

        // 入手先（武器・防具・アクセサリー・スキル・アイテム）：解放したダンジョンのうち、その物が落ちる所。
        // 金の「入手先」とダンジョン名の札を並べ、合成のレシピがあればその下、なければ説明の下（SourcesTop）に置く。
        // 物は1つの行き先でしか落ちない（doc/features/drops.md の行き先ごとの一覧）ため、札は1枚。
        public RectTransform Sources;
        public Button SourcePlace;
        public TMP_Text SourcePlaceName;
        public float SourcesTop;
        public float SourcesUnderRecipe;

        // ダンジョン名の札を押したときに、そのダンジョンに入るかを確かめる窓（画面に1つ）。
        public GameDialog Dialog;

        // 説明の下の属性のアイコン（装備の属性、スキルの属性）。
        public ElementIconRow DetailElements;

        // 敵の開示した弱点：「弱点」の行と、開示した弱点のアイコン、まだないときの文字。
        public GameObject WeaknessRow;
        public Image[] DetailIcons = Array.Empty<Image>();
        public TMP_Text WeaknessNote;

        private readonly List<(GlassCard Card, Action Click)> bindings = new();
        private readonly List<(Button Button, UnityEngine.Events.UnityAction Click)> tabBindings =
            new();
        private readonly GlassCardCover cover = new();
        private LibraryRecords records = LibraryRecords.Empty;

        // 街道の進み具合。入手先には、解放したダンジョンだけを出す（DungeonUnlocks）。
        private WorldMapProgress highway = WorldMapSession.Known ?? WorldMapProgress.Start;
        private string selected;

        // 札に出している入手先。
        private LibraryDropPlaces.Place? source;

        /// <summary>The entry shown on the right, or null.</summary>
        public string Selected => selected;

        public string CurrentEntry => selected;

        /// <summary>
        /// Shows the entry <paramref name="id"/> on the right and scrolls its card into view, as
        /// when the craft opens a material's page (doc/features/synthesis.md の材料の入手先).
        /// The records read later keep it chosen.
        /// </summary>
        public void ShowEntry(string id)
        {
            if (Array.FindIndex(Tiles, tile => tile != null && tile.Id == id) < 0)
                return;
            Select(id);
            ScrollTo(id);
        }

        // 一覧を動かして、その札を見える所に出す。
        private void ScrollTo(string id)
        {
            var tile = Array.Find(Tiles, t => t != null && t.Id == id);
            if (List == null || List.content == null || List.viewport == null || tile == null)
                return;
            Canvas.ForceUpdateCanvases();
            float content = List.content.rect.height;
            float viewport = List.viewport.rect.height;
            if (content <= viewport)
                return;
            var rect = (RectTransform)tile.transform;
            // 札の中心を、見える所の中央に寄せる。
            float center = -rect.anchoredPosition.y + rect.rect.height * (rect.pivot.y - 0.5f);
            float top = Mathf.Clamp(center - viewport / 2f, 0f, content - viewport);
            List.verticalNormalizedPosition = 1f - top / (content - viewport);
        }

        private void OnEnable()
        {
            foreach (var tile in Tiles)
            {
                if (tile.Card == null)
                    continue;
                string id = tile.Id;
                Action click = () => Select(id);
                tile.Card.Clicked += click;
                bindings.Add((tile.Card, click));
            }
            for (int i = 0; i < KindTabs.Length && i < KindTabKeys.Length; i++)
            {
                var tab = KindTabs[i];
                if (tab == null || tab.Button == null)
                    continue;
                string key = KindTabKeys[i];
                tab.Chosen = key == LibraryKinds.GuideItemKey(Kind);
                UnityEngine.Events.UnityAction click = () => OpenKind(key);
                tab.Button.onClick.AddListener(click);
                tabBindings.Add((tab.Button, click));
            }
            if (SourcePlace != null)
                SourcePlace.onClick.AddListener(AskToEnter);
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            selected = null;
            if (Tiles.Length == 0)
            {
                ShowPreparing();
                return;
            }
            // 前に読んだ記録があれば、すぐ出して裏で読み直す。なければ読み終えるまで、どれを見つけたかを隠す。
            var known = LibrarySession.Known;
            if (known != null)
                Render(known);
            else
                ShowLoading();
            StartLoad(
                token => LoadAsync(known != null, token),
                LoadFailedMessage,
                ShowLoadFailed,
                readAgain: known != null
            );
        }

        private void OnDisable()
        {
            Load.Cancel();
            foreach (var (card, click) in bindings)
                if (card != null)
                    card.Clicked -= click;
            bindings.Clear();
            foreach (var (button, click) in tabBindings)
                if (button != null)
                    button.onClick.RemoveListener(click);
            tabBindings.Clear();
            if (SourcePlace != null)
                SourcePlace.onClick.RemoveListener(AskToEnter);
            if (Dialog != null && Dialog.IsShown)
                Dialog.Hide();
        }

        /// <summary>The device's back key closes the dialog asking to enter a dungeon first.</summary>
        public bool HandleBack() => Dialog != null && Dialog.HandleBack();

        // 入手先のダンジョン名を押した：そのダンジョンに入るかを確かめ、「行く」で冒険を始める。
        private void AskToEnter()
        {
            if (source is { } place)
                DungeonEntry.Ask(Dialog, Guide, place);
        }

        // 同じ案内人の画面の、ほかの分類のパネルへ切り替える。
        private void OpenKind(string key)
        {
            if (
                Guide != null
                && !string.IsNullOrEmpty(key)
                && key != LibraryKinds.GuideItemKey(Kind)
            )
                Guide.OpenItem(key);
        }

        private async Task LoadAsync(bool shown, CancellationToken token)
        {
            string shownVersion = LibrarySession.ReadVersion;
            int shownWeaknesses = RevealedWeaknessSession.Known.Count;
            string shownOpen = OpenDestinations();
            // 記録と、敵の開示した弱点と、街道の進み具合は互いに依存しないため、同時に読む。
            var reading = LibrarySession.LoadAsync(token);
            var weaknesses =
                Kind == LibraryKind.Enemy
                    ? RevealedWeaknessSession.LoadAsync(token)
                    : Task.CompletedTask;
            var towns = HasDropPlaces ? LoadTownsAsync(token) : Task.CompletedTask;
            await Task.WhenAll(reading, weaknesses, towns);
            if (token.IsCancellationRequested)
                return;
            // 読み直しても変わらなければ、出している記録のまま描き直さない。
            if (
                shown
                && Unchanged(shownVersion, LibrarySession.ReadVersion)
                && shownWeaknesses == RevealedWeaknessSession.Known.Count
                && shownOpen == OpenDestinations()
            )
                return;
            Render(await reading);
        }

        private bool HasDropPlaces =>
            Kind
                is LibraryKind.Weapon
                    or LibraryKind.Armor
                    or LibraryKind.Accessory
                    or LibraryKind.Skill
                    or LibraryKind.Item;

        // 入手先に出せる（解放した）行き先。読み直して変わったかを比べる。
        private string OpenDestinations() =>
            string.Join(
                ",",
                Adventure
                    .AdventureCatalog.Areas.Where(area => LibraryDropPlaces.IsOpen(area, highway))
                    .Select(area => area.Id)
            );

        // 着いた街を読み直す。読めなければ、手元の進み具合のまま入手先を出す。
        private async Task LoadTownsAsync(CancellationToken token)
        {
            var progress = await LibraryDropPlaces.LoadReachAsync(token);
            if (progress != null)
                highway = progress;
        }

        /// <summary>
        /// Draws the records: found entries, shadows, new marks and the count. When none is chosen
        /// yet, chooses the first found entry, leaving its new mark.
        /// </summary>
        public void Render(LibraryRecords next)
        {
            records = next ?? LibraryRecords.Empty;
            Set(Status, "");
            // 右の面が空にならないよう、まだ選んでいなければ最初の見つけた物を選んでおく。新しく載った印は、
            // 押して見るまで残す。見つけた物がなければ空のままにする。
            if (selected == null)
            {
                var first = Array.Find(
                    Tiles,
                    tile => tile != null && records.IsFound(Kind, tile.Id)
                );
                selected = first != null ? first.Id : null;
            }
            int found = 0;
            var seen = LibrarySession.Seen;
            foreach (var tile in Tiles)
            {
                var entry = EntryOf(tile.Id);
                bool isFound = records.IsFound(Kind, tile.Id);
                if (isFound)
                    found++;
                if (tile.Card != null)
                {
                    tile.Card.gameObject.SetActive(true);
                    tile.Card.Selected = tile.Id == selected;
                }
                PaintArt(tile.Art, entry, isFound, TileArtBox);
                if (tile.Name != null)
                    tile.Name.gameObject.SetActive(isFound);
                if (tile.Stars != null)
                    tile.Stars.gameObject.SetActive(isFound);
                if (tile.Mark != null)
                    tile.Mark.SetActive(seen.IsNew(records, Kind, tile.Id));
            }
            Set(Count, $"{found}/{Tiles.Length}");
            cover.Reveal(
                Tiles.Where(tile => tile.Card != null).Select(tile => tile.Card.gameObject)
            );
            ShowDetail(selected);
        }

        /// <summary>Shows the entry on the right and clears its new mark.</summary>
        public void Select(string id)
        {
            selected = id;
            if (records.IsFound(Kind, id))
                LibrarySession.Seen.MarkSeen(Kind, id);
            foreach (var tile in Tiles)
            {
                if (tile.Card != null)
                    tile.Card.Selected = tile.Id == id;
                if (tile.Id == id && tile.Mark != null)
                    tile.Mark.SetActive(false);
            }
            ShowDetail(id);
        }

        private void ShowDetail(string id)
        {
            var entry = EntryOf(id);
            if (Detail != null)
                Detail.SetActive(entry != null);
            if (entry == null)
                return;
            bool found = records.IsFound(Kind, id);
            PaintArt(DetailArt, entry, found, DetailArtBox);
            Set(DetailNumber, NumberText(entry.Number));
            Set(DetailName, found ? entry.Name : "");
            Set(DetailRarity, found && entry.Rarity > 0 ? new string('★', entry.Rarity) : "");
            // 装備の種類（武器・防具）は、左の分類で分かるため書かない。
            bool gear =
                entry.Kind is LibraryKind.Weapon or LibraryKind.Armor or LibraryKind.Accessory;
            Set(DetailKind, found && !gear ? entry.KindLine : "");
            // まだ見つけていないことは、名前を消した影の絵でわかるため書かない。
            Set(DetailBody, found ? entry.Description : "");
            Set(DetailExtra, found ? ExtraOf(entry) : "");
            ShowCharacter(found ? entry.Id : null);
            if (DetailElements != null)
            {
                DetailElements.Show(found ? LibraryFaces.ElementsOf(entry.Kind, entry.Id) : null);
                DetailElements.gameObject.SetActive(DetailElements.Any);
            }
            ShowWeaknesses(found ? entry.Id : null);
            var recipe = found ? Workshop.WorkshopRecipes.Find(Kind, entry.Id) : null;
            ShowRecipe(recipe);
            // 入手先は、まだ見つけていない物にも出す（合成の材料の札から開いたとき、どこで集めるかを見せる）。
            ShowSources(entry.Id, recipe != null);
        }

        // 入手先（doc/features/library.md の入手先）。解放したダンジョンで落ちる物だけに出す。
        private void ShowSources(string id, bool underRecipe)
        {
            if (Sources == null)
                return;
            var places = LibraryDropPlaces.Of(Kind, id, highway);
            source = places.Count > 0 ? places[0] : null;
            Sources.gameObject.SetActive(source != null);
            if (source is not { } place)
                return;
            Set(SourcePlaceName, place.Text);
            Sources.anchoredPosition = new Vector2(
                Sources.anchoredPosition.x,
                -(underRecipe ? SourcesUnderRecipe : SourcesTop)
            );
        }

        /// <summary>The dungeon shown in the sources, or null when none is.</summary>
        public string SourceDestination => source?.Destination;

        public static string NumberText(int number) => $"No.{number:000}";

        // 合成のレシピ（doc/features/library.md の合成のレシピ）。材料を1種類ずつ、絵と要る数と名前で並べる。
        // まだ見つけていない材料は影にして名前を伏せる。持っている数は出さない。
        private void ShowRecipe(Workshop.RecipeDefinition recipe)
        {
            if (Recipe != null)
                Recipe.SetActive(recipe != null);
            if (recipe == null)
                return;
            for (int i = 0; i < RecipeRows.Count; i++)
            {
                var row = RecipeRows[i];
                bool visible = i < recipe.Materials.Count;
                row.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var material = recipe.Materials[i];
                bool found = records.IsFound(material.Kind, material.Id);
                var entry =
                    Data != null
                        ? Array.Find(
                            Data.Entries,
                            e => e != null && e.Kind == material.Kind && e.Id == material.Id
                        )
                        : null;
                Workshop.WorkshopCraftView.PaintMaterial(
                    row,
                    material,
                    entry,
                    found,
                    material.Count,
                    null,
                    RecipeArtBox
                );
                Set(row.Count, "×" + material.Count);
            }
        }

        // 合成のレシピの材料の絵の枠の一辺。
        public float RecipeArtBox = 64f;

        private LibraryEntryData EntryOf(string id)
        {
            if (Data == null || string.IsNullOrEmpty(id))
                return null;
            return Array.Find(Data.Entries, entry => entry.Kind == Kind && entry.Id == id);
        }

        /// <summary>
        /// Draws an entry's art, or its shadow when not found. The generator calls it too, so the
        /// prefab shows the art in the editor.
        /// </summary>
        public static void PaintArt(RawImage image, LibraryEntryData entry, bool found, float box)
        {
            if (image == null)
                return;
            image.texture = entry?.Art;
            image.enabled = entry?.Art != null;
            if (entry == null || entry.Art == null)
                return;
            image.uvRect = entry.ArtUv;
            image.color = found ? entry.ArtTint : ShadowTint;
            var dots = entry.ArtDots;
            float dot = Mathf.Max(1f, Mathf.Floor(box / Mathf.Max(1f, Mathf.Max(dots.x, dots.y))));
            image.rectTransform.sizeDelta = dots * dot;
        }

        // キャラの固有スキル（取得した段階の名前、取得していない段階は影）と、敵の倒した数。
        private string ExtraOf(LibraryEntryData entry) =>
            Kind switch
            {
                LibraryKind.Enemy =>
                    $"倒した数　<color=#ffd766>{records.Defeated(entry.Id):N0}</color>",
                _ => "",
            };

        /// <summary>The line under a unique skill's card: how many of its stages are awakened.</summary>
        public static string StepsText(int acquired) =>
            $"覚醒 {acquired}/{UniqueSkillTreeShape.Count}";

        /// <summary>A passive's level to unlock, written only when it is not there from the start.</summary>
        public static string UnlockText(int level) => level > 1 ? $"Lv {level}で解放" : "";

        // キャラクターの固有スキルとパッシブ。ほかの分類と、まだ見つけていないキャラでは出さない。
        private void ShowCharacter(string characterId)
        {
            var character =
                characterId != null && Kind == LibraryKind.Character && Training != null
                    ? Training.Find(characterId)
                    : null;
            if (CharacterSkills != null)
                CharacterSkills.SetActive(character != null);
            if (character == null)
                return;
            if (CharacterScroll != null)
                CharacterScroll.verticalNormalizedPosition = 1f;
            for (int i = 0; i < UniqueCards.Length; i++)
            {
                var unique = i < character.Uniques.Length ? character.Uniques[i] : null;
                var tree = unique != null ? UniqueSkillTrees.Find(unique.Tree) : null;
                CardSkill card = null;
                int acquired = 0;
                if (tree != null)
                {
                    var progress = UniqueSkillSession.ProgressOf(
                        characterId,
                        i,
                        character.MockProgressOf(i)
                    );
                    card = tree.StageAt(progress.SetStage)?.Card;
                    for (int stage = 0; stage < UniqueSkillTreeShape.Count; stage++)
                        if (progress.Has(stage))
                            acquired++;
                }
                Workshop.WorkshopSkillCard.Show(
                    UniqueCards[i],
                    card,
                    Inventory.Quality.MinMultiplier,
                    Inventory.Quality.MinMultiplier,
                    true,
                    Party,
                    CardFrames,
                    ElementIcons
                );
                if (i < UniqueSteps.Length)
                    Set(UniqueSteps[i], card != null ? StepsText(acquired) : "");
            }
            for (int i = 0; i < PassiveRows.Length; i++)
            {
                var row = PassiveRows[i];
                var passive = i < character.Passives.Length ? character.Passives[i] : null;
                if (row?.Name == null)
                    continue;
                row.Name.transform.parent.gameObject.SetActive(passive != null);
                if (passive == null)
                    continue;
                if (row.Icon != null)
                    row.Icon.texture = passive.Icon;
                Set(row.Name, passive.Name);
                Set(row.Description, passive.Description);
                Set(row.When, UnlockText(passive.UnlockLevel));
            }
        }

        public const string NoWeaknessText = "まだ開示していない";

        private void ShowWeaknesses(string enemyId)
        {
            var enemy = Kind == LibraryKind.Enemy ? CombatEnemies.Find(enemyId) : null;
            if (WeaknessRow != null)
                WeaknessRow.SetActive(enemy != null);
            int shown = 0;
            if (enemy != null && Data != null)
                foreach (var element in enemy.Weaknesses)
                {
                    if (shown >= DetailIcons.Length)
                        break;
                    int index = (int)element;
                    var icon = index < Data.ElementIcons.Length ? Data.ElementIcons[index] : null;
                    if (
                        icon == null
                        || DetailIcons[shown] == null
                        || !RevealedWeaknessSession.IsRevealed(enemy.Id, element)
                    )
                        continue;
                    DetailIcons[shown].sprite = icon;
                    ShowIcon(DetailIcons[shown], true);
                    shown++;
                }
            for (int i = shown; i < DetailIcons.Length; i++)
                if (DetailIcons[i] != null)
                    ShowIcon(DetailIcons[i], false);
            Set(WeaknessNote, enemy != null && shown == 0 ? NoWeaknessText : "");
        }

        // アイコンは行の中の枠に入っているため、枠ごと出し入れして、隠したアイコンの場所を詰める。
        private static void ShowIcon(Image icon, bool shown)
        {
            var cell = icon.transform.parent;
            (cell != null ? cell.gameObject : icon.gameObject).SetActive(shown);
            icon.gameObject.SetActive(shown);
        }

        // 読み込むまで、どれを見つけたかを見せない。先頭のカードは伏せて並べ、残りは隠す。
        private void ShowLoading()
        {
            Set(Status, "");
            Set(Count, "");
            var covered = Tiles.Take(CoverCards).Where(tile => tile.Card != null).ToArray();
            foreach (var tile in Tiles)
                if (tile.Card != null)
                    tile.Card.gameObject.SetActive(false);
            cover.Cover(covered.Select(tile => tile.Card.gameObject));
            foreach (var tile in covered)
                tile.Card.Selected = false;
            if (Detail != null)
                Detail.SetActive(false);
        }

        private void ShowLoadFailed()
        {
            cover.Clear();
            foreach (var tile in Tiles)
                if (tile.Card != null)
                    tile.Card.gameObject.SetActive(false);
            Set(Status, LoadFailedText);
        }

        private void ShowPreparing()
        {
            Set(Status, PreparingText);
            Set(Count, "");
            if (Detail != null)
                Detail.SetActive(false);
        }

        // 停止中の表示の切り替え：サーバーから読み込む間と、読めなかったときの表示。
        public IReadOnlyList<string> PreviewStates => GuidePanelLoad.PreviewStates;

        public void ShowPreviewState(int index)
        {
            ShowLoading();
            if (index == 1)
                ShowLoadFailed();
        }
    }
}
