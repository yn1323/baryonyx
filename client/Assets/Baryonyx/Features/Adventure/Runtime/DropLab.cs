using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Baryonyx.Inventory;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>One clear's reward on the drop editor's right: the card with the art, the name and count, the stars and quality.</summary>
    [Serializable]
    public sealed class DropLabRewardSlot
    {
        public Button Button;
        public GlassCard Card;
        public Image Icon;
        public TMP_Text Name;
        public TMP_Text Grade;
    }

    /// <summary>
    /// The drop editor (doc/features/screens/debug-rooms.md#ドロップエディター), a debugging room
    /// to decide what each dungeon drops and gives for its first five clears. The button at the
    /// top picks the dungeon; the middle shows its drop list by category, each kind a card with
    /// its art, name and stars (a crown on one that falls only in the boss's room) and a "＋"
    /// card that adds one; the right shows the five clears' rewards. A tapped card is chosen, and
    /// the row at the bottom changes it: whether it falls only in the boss's room, a skill's
    /// stars, taking it out, or for a reward its kind, count and quality. Kinds are picked from
    /// the list of every kind, which says where each is listed now. The changes are kept only
    /// while the app runs (<see cref="Drafts"/>), and "コピー" copies the dungeons changed as JSON
    /// (<see cref="DropLabDrafts.ChangesJson"/>), to be pasted to the AI and put into the tables.
    /// </summary>
    public sealed class DropLab : MonoBehaviour
    {
        private static DropLabDrafts drafts;
        private static string lastDungeon;

        /// <summary>The drafts, kept while the app runs so leaving the room loses nothing.</summary>
        public static DropLabDrafts Drafts => drafts ??= new DropLabDrafts();

        /// <summary>Drops the drafts, back to the tables (for tests).</summary>
        public static void Forget()
        {
            drafts = null;
            lastDungeon = null;
        }

        [Header("上の段")]
        public Button HomeButton;
        public SceneTransitionController Transition;

        [Tooltip("戻り先のシーンの名前。空なら戻るボタンを隠す。")]
        public string HomeSceneName = "";

        public Button DungeonButton;
        public TMP_Text DungeonLabel;
        public TMP_Text Status;
        public Button CopyButton;

        [Header("ダンジョンの一覧")]
        public CanvasGroup DungeonGroup;
        public RectTransform DungeonContent;
        public Button DungeonTemplate;

        [Header("ドロップの一覧")]
        public ScrollRect DropScroll;

        [Tooltip("分類ごとの段の札を並べる所（DropLabDrafts.ListCategories の順）。")]
        public RectTransform[] Sections = Array.Empty<RectTransform>();

        [Tooltip("分類ごとの段の高さ。札の数に合わせて変える。")]
        public LayoutElement[] SectionSizes = Array.Empty<LayoutElement>();

        [Tooltip("物の札の見本（絵・名前・★・下の行・王冠）。札ごとに複製する。")]
        public Button TileTemplate;

        [Tooltip("段の最後に置く「＋」の札の見本。")]
        public Button AddTemplate;

        public Vector2 TileSize = new(334f, 128f);
        public float TileGap = 12f;

        [Header("クリア回数の報酬")]
        public DropLabRewardSlot[] Rewards = Array.Empty<DropLabRewardSlot>();

        [Header("選んだ物")]
        public TMP_Text Info;
        public TMP_Text Detail;
        public CanvasGroup DropActions;
        public Button BossButton;
        public TMP_Text BossLabel;
        public Button StarsButton;
        public TMP_Text StarsLabel;
        public Button RemoveButton;
        public CanvasGroup RewardActions;
        public Button ChangeButton;
        public TMP_InputField CountInput;
        public Button QualityButton;
        public TMP_Text QualityLabel;

        [Header("物を選ぶ一覧")]
        public CanvasGroup PickerGroup;
        public TMP_Text PickerTitle;
        public Button PickerClose;

        [Tooltip("分類のボタン（DropLabDrafts.RewardCategories の順）。")]
        public Button[] PickerCategories = Array.Empty<Button>();
        public RectTransform PickerContent;
        public ScrollRect PickerScroll;

        [Tooltip("種類ごとの絵。")]
        public ItemArt Art;

        // 札の子の名前。
        public const string IconName = "Icon";
        public const string LabelName = "Name";
        public const string StarsName = "Stars";
        public const string DetailName = "Detail";
        public const string BossName = "Boss";

        private static readonly Color ErrorColor = UiPalette.Fall;

        /// <summary>One card of the drop list or the picker, and the kind it is.</summary>
        public readonly struct Tile
        {
            public Tile(Button button, AdventureDropCategory category, string id)
            {
                Button = button;
                Category = category;
                Id = id;
            }

            public Button Button { get; }
            public AdventureDropCategory Category { get; }

            // 「＋」の札は空。
            public string Id { get; }
        }

        private readonly List<Tile> tiles = new();
        private readonly List<Tile> picks = new();
        private readonly List<Button> dungeonRows = new();
        private string dungeon;
        private AdventureDropCategory? chosenCategory;
        private string chosenId;
        private int chosenReward;

        // 物を選ぶ一覧を開いているときの、足す先（0はドロップの一覧、1〜5はその回の報酬）と、出している分類。
        private int pickingFor = -1;
        private AdventureDropCategory pickerCategory;
        private bool leaving;

        /// <summary>The dungeon shown.</summary>
        public string Dungeon => dungeon;

        public DropLabDrafts.Draft Draft => Drafts.Find(dungeon);

        /// <summary>The cards of the drop list, section by section, each section's "＋" last.</summary>
        public IReadOnlyList<Tile> Tiles => tiles;

        /// <summary>The cards of the picker.</summary>
        public IReadOnlyList<Tile> Picks => picks;

        /// <summary>The rows of the dungeon list, in <see cref="DropLabDrafts.All"/>'s order.</summary>
        public IReadOnlyList<Button> DungeonRows => dungeonRows;

        public bool ListingDungeons => DungeonGroup != null && DungeonGroup.blocksRaycasts;
        public bool Picking => pickingFor >= 0;
        public AdventureDropCategory PickerCategory => pickerCategory;

        /// <summary>The clear (1–5) whose reward is chosen, or 0.</summary>
        public int ChosenReward => chosenReward;

        /// <summary>The kind of the drop list chosen, or null.</summary>
        public string ChosenDrop => chosenId;

        /// <summary>Whether the home button was pressed and the room is leaving for home.</summary>
        public bool Leaving => leaving;

        private void Start()
        {
            Listen(HomeButton, GoHome);
            if (HomeButton != null)
                HomeButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(HomeSceneName));
            Listen(DungeonButton, ToggleDungeons);
            Listen(CopyButton, () => Copy());
            Listen(BossButton, () => ToggleBoss());
            Listen(StarsButton, () => NextStars());
            Listen(RemoveButton, () => Remove());
            Listen(ChangeButton, () => OpenPicker(chosenReward));
            Listen(QualityButton, () => NextQuality());
            Listen(PickerClose, ClosePicker);
            if (CountInput != null)
                CountInput.onEndEdit.AddListener(text => SetCount(text));
            for (int i = 0; i < PickerCategories.Length; i++)
            {
                var category = DropLabDrafts.RewardCategories[i];
                Listen(PickerCategories[i], () => ShowPicker(category));
            }
            for (int i = 0; i < Rewards.Length; i++)
            {
                int clears = i + 1;
                Listen(Rewards[i].Button, () => ChooseReward(clears));
            }
            BuildDungeonRows();
            Show(DungeonGroup, false);
            Show(PickerGroup, false);
            UiText.Set(Status, "");
            var first = Drafts.Find(lastDungeon) ?? Drafts.All.FirstOrDefault();
            if (first != null)
                ShowDungeon(first.Id);
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        private static void Show(CanvasGroup group, bool shown)
        {
            if (group == null)
                return;
            group.alpha = shown ? 1f : 0f;
            group.interactable = group.blocksRaycasts = shown;
        }

        /// <summary>Goes back to home behind the shutter. If home cannot be loaded, the room stays as it was.</summary>
        public void GoHome()
        {
            if (leaving)
                return;
            leaving = SceneLoader.Load(HomeSceneName, Transition, this, () => leaving = false);
        }

        // --- ダンジョン ------------------------------------------------------------------

        private void ToggleDungeons()
        {
            bool open = !ListingDungeons;
            if (open)
                PaintDungeonRows();
            Show(DungeonGroup, open);
        }

        private void BuildDungeonRows()
        {
            if (DungeonTemplate == null || DungeonContent == null)
                return;
            DungeonTemplate.gameObject.SetActive(false);
            foreach (var draft in Drafts.All)
            {
                var row = Instantiate(DungeonTemplate, DungeonContent);
                row.name = draft.Id;
                row.gameObject.SetActive(true);
                string id = draft.Id;
                row.onClick.AddListener(() => ShowDungeon(id));
                dungeonRows.Add(row);
            }
            PaintDungeonRows();
        }

        // 変えたダンジョンには、名前の前に金の点を付ける。
        private void PaintDungeonRows()
        {
            for (int i = 0; i < dungeonRows.Count && i < Drafts.All.Count; i++)
            {
                var draft = Drafts.All[i];
                string mark = Drafts.Changed(draft.Id)
                    ? $"<color=#{ColorUtility.ToHtmlStringRGB(UiPalette.Gold)}>●</color> "
                    : "";
                UiText.SetRich(
                    dungeonRows[i].GetComponentInChildren<TMP_Text>(true),
                    $"{mark}{DungeonText(draft)}"
                );
                var card = dungeonRows[i].GetComponent<GlassCard>();
                if (card != null)
                    card.Selected = draft.Id == dungeon;
            }
        }

        public static string DungeonText(DropLabDrafts.Draft draft) =>
            $"{draft.Name}　Lv{draft.Level}";

        /// <summary>Shows the dungeon's drop list and clear rewards, with nothing chosen.</summary>
        public bool ShowDungeon(string id)
        {
            var draft = Drafts.Find(id);
            if (draft == null)
                return false;
            dungeon = id;
            lastDungeon = id;
            UiText.Set(DungeonLabel, DungeonText(draft));
            Show(DungeonGroup, false);
            ClosePicker();
            Forget(false);
            Render();
            if (DropScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                DropScroll.verticalNormalizedPosition = 1f;
            }
            return true;
        }

        /// <summary>Draws the dungeon <paramref name="id"/> as it is in the tables, for the prefab and the showcase.</summary>
        public void Bake(string id)
        {
            dungeon = id;
            var draft = new DropLabDrafts().Find(id);
            UiText.Set(DungeonLabel, draft != null ? DungeonText(draft) : "");
            BuildTiles(draft);
            PaintRewards(draft);
            PaintChosen();
        }

        private void Render()
        {
            BuildTiles(Draft);
            PaintRewards(Draft);
            PaintChosen();
            PaintDungeonRows();
        }

        // --- ドロップの一覧 --------------------------------------------------------------

        // 分類ごとの段に、一覧の物の札と、最後に「＋」の札を並べ直す。段の高さは札の行の数に合わせる。
        private void BuildTiles(DropLabDrafts.Draft draft)
        {
            tiles.Clear();
            for (int s = 0; s < Sections.Length && s < DropLabDrafts.ListCategories.Length; s++)
            {
                var section = Sections[s];
                Clear(section);
                var category = DropLabDrafts.ListCategories[s];
                var kinds =
                    draft == null
                        ? Array.Empty<AdventureDrops.Entry>()
                        : draft.Drops.Where(entry => entry.Category == category).ToArray();
                foreach (var entry in kinds)
                {
                    var card = MakeTile(
                        section,
                        entry.Category,
                        entry.Id,
                        entry.Stars,
                        entry.BossOnly,
                        ""
                    );
                    var tile = new Tile(card, entry.Category, entry.Id);
                    if (Application.isPlaying)
                        card.onClick.AddListener(() => ChooseDrop(tile.Category, tile.Id));
                    tiles.Add(tile);
                }
                if (AddTemplate != null)
                {
                    var add = Instantiate(AddTemplate, section);
                    add.name = "Add";
                    add.gameObject.SetActive(true);
                    if (Application.isPlaying)
                        add.onClick.AddListener(() => OpenPicker(0, category));
                    tiles.Add(new Tile(add, category, ""));
                }
                if (s < SectionSizes.Length && SectionSizes[s] != null)
                {
                    int rows = Mathf.Max(
                        1,
                        (kinds.Length + 1 + Columns(section) - 1) / Columns(section)
                    );
                    SectionSizes[s].minHeight = SectionSizes[s].preferredHeight =
                        rows * TileSize.y + (rows - 1) * TileGap;
                }
            }
        }

        private static int Columns(RectTransform section) =>
            section.TryGetComponent(out GridLayoutGroup grid)
                ? Mathf.Max(1, grid.constraintCount)
                : 1;

        // 見本から札を1枚作る：絵・名前・★、下の行（一覧の札では空、選ぶ一覧ではどのダンジョンにあるか）、最奥だけの王冠。
        private Button MakeTile(
            RectTransform parent,
            AdventureDropCategory category,
            string id,
            int stars,
            bool boss,
            string detail
        )
        {
            var card = Instantiate(TileTemplate, parent);
            card.name = id;
            card.gameObject.SetActive(true);
            var sprite = Art != null ? Art.Find(id) : null;
            var icon = Part<Image>(card, IconName);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            UiText.Set(Part<TMP_Text>(card, LabelName), DropLabDrafts.NameOf(category, id));
            UiText.Set(Part<TMP_Text>(card, StarsName), StarsText(stars));
            UiText.Set(Part<TMP_Text>(card, DetailName), detail);
            var crown = card.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(part => part.name == BossName);
            if (crown != null)
                crown.gameObject.SetActive(boss);
            return card;
        }

        private static T Part<T>(Component card, string name)
            where T : Component =>
            card.GetComponentsInChildren<T>(true).FirstOrDefault(part => part.name == name);

        private static void Clear(Transform parent)
        {
            foreach (Transform child in parent.Cast<Transform>().ToArray())
            {
                if (Application.isPlaying)
                {
                    // 同じフレームで並べ直すため、先に外してから消す。
                    child.SetParent(null, false);
                    Destroy(child.gameObject);
                }
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        public static string StarsText(int stars) => stars > 0 ? new string('★', stars) : "";

        // --- 選んだ物 --------------------------------------------------------------------

        /// <summary>Chooses the kind in the drop list; the row at the bottom then changes it.</summary>
        public bool ChooseDrop(AdventureDropCategory category, string id)
        {
            if (Draft == null || !Draft.Has(category, id))
                return false;
            chosenCategory = category;
            chosenId = id;
            chosenReward = 0;
            PaintChosen();
            return true;
        }

        /// <summary>Chooses the reward of the <paramref name="clears"/>th clear.</summary>
        public bool ChooseReward(int clears)
        {
            if (Drafts.RewardOf(dungeon, clears) == null)
                return false;
            chosenCategory = null;
            chosenId = null;
            chosenReward = clears;
            PaintChosen();
            return true;
        }

        private void Forget(bool paint = true)
        {
            chosenCategory = null;
            chosenId = null;
            chosenReward = 0;
            if (paint)
                PaintChosen();
        }

        // 選んだ札に金の縁を付け、下の段に名前と割合（ドロップ）か回数（報酬）と、変えるボタンを出す。
        private void PaintChosen()
        {
            foreach (var tile in tiles)
                if (tile.Button != null && tile.Button.TryGetComponent(out GlassCard card))
                    card.Selected = tile.Id == chosenId && tile.Category == chosenCategory;
            for (int i = 0; i < Rewards.Length; i++)
                if (Rewards[i].Card != null)
                    Rewards[i].Card.Selected = i + 1 == chosenReward;

            var draft = Application.isPlaying ? Draft : null;
            int index =
                draft != null && chosenCategory is { } category
                    ? draft.IndexOf(category, chosenId)
                    : -1;
            var reward = draft != null ? Drafts.RewardOf(dungeon, chosenReward) : null;
            Show(DropActions, index >= 0);
            Show(RewardActions, reward != null);
            if (index >= 0)
            {
                var entry = draft.Drops[index];
                UiText.SetRich(Info, DropLine(entry.Category, entry.Id, entry.Stars, null));
                UiText.Set(Detail, ChanceText(draft.Drops, entry));
                UiText.Set(BossLabel, BossText(entry.BossOnly));
                UiText.Set(StarsLabel, $"★{entry.Stars}");
                if (StarsButton != null)
                    StarsButton.gameObject.SetActive(entry.Category == AdventureDropCategory.Skill);
            }
            else if (reward != null)
            {
                UiText.SetRich(
                    Info,
                    $"{chosenReward}回目　"
                        + DropLine(
                            reward.Category,
                            reward.Id,
                            Drafts.StarsOf(reward.Category, reward.Id),
                            reward.Quality
                        )
                        + (reward.Count > 1 ? $" ×{reward.Count}" : "")
                );
                UiText.Set(Detail, "");
                if (CountInput != null)
                    CountInput.SetTextWithoutNotify(
                        reward.Count.ToString(CultureInfo.InvariantCulture)
                    );
                if (QualityButton != null)
                    QualityButton.gameObject.SetActive(reward.Quality != null);
                if (reward.Quality is { } quality)
                    UiText.Set(QualityLabel, QualityText(quality));
            }
            else
            {
                UiText.Set(Info, "");
                UiText.Set(Detail, "");
            }
        }

        public static string BossText(bool boss) => boss ? "最奥だけ：はい" : "最奥だけ：いいえ";

        public static string QualityText(ItemQuality quality) => "品質：" + Quality.Letter(quality);

        // 「★★★ 朝露のペンダント（アクセサリー・品質A）」。★は金にする。
        private static string DropLine(
            AdventureDropCategory category,
            string id,
            int stars,
            ItemQuality? quality
        )
        {
            string kind = quality is { } letter
                ? $"{AdventureDrops.NameOf(category)}・品質{QualityPalette.Painted(letter)}"
                : AdventureDrops.NameOf(category);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(UiPalette.Gold)}>{StarsText(stars)}</color> "
                + $"{DropLabDrafts.NameOf(category, id)}（{kind}）";
        }

        /// <summary>
        /// "落ちる物1つごとの割合　魔物 4.2%　強い魔物・宝箱 3.1%　最奥 6.0%": the chance that one
        /// thing falling there is the kind (<see cref="AdventureDrops.ChanceOf"/>), "—" where it cannot fall.
        /// </summary>
        public static string ChanceText(
            IReadOnlyList<AdventureDrops.Entry> pool,
            AdventureDrops.Entry entry
        )
        {
            string Of(AdventureRoomKind row)
            {
                float chance = AdventureDrops.ChanceOf(pool, entry, row);
                return chance > 0f
                    ? (chance * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%"
                    : "—";
            }
            return "落ちる物1つごとの割合　"
                + $"魔物 {Of(AdventureRoomKind.Battle)}　"
                + $"強い魔物・宝箱 {Of(AdventureRoomKind.Elite)}　"
                + $"最奥 {Of(AdventureRoomKind.Boss)}";
        }

        // --- 変える ----------------------------------------------------------------------

        /// <summary>Makes the chosen kind fall only in the boss's room, or on the way too.</summary>
        public bool ToggleBoss()
        {
            if (chosenCategory is not { } category)
                return false;
            bool done = Drafts.ToggleBoss(dungeon, category, chosenId, out string message);
            return After(done, message);
        }

        /// <summary>Gives the chosen skill one star more (back to the fewest after ☆5).</summary>
        public bool NextStars()
        {
            if (chosenCategory is not { } category)
                return false;
            return After(Drafts.NextStars(dungeon, category, chosenId), null);
        }

        /// <summary>Takes the chosen kind out of the list.</summary>
        public bool Remove()
        {
            if (chosenCategory is not { } category)
                return false;
            string name = DropLabDrafts.NameOf(category, chosenId);
            if (!Drafts.Remove(dungeon, category, chosenId))
                return false;
            Forget(false);
            return After(true, $"{name}を一覧から外しました");
        }

        /// <summary>Sets how many of the chosen reward are given.</summary>
        public bool SetCount(string text)
        {
            if (chosenReward == 0)
                return false;
            string message = $"数は1〜{DropLabDrafts.MaxCount}にします";
            bool done =
                int.TryParse((text ?? "").Trim(), out int count)
                && Drafts.SetCount(dungeon, chosenReward, count, out message);
            // 書けなかったときは、欄を今の数に戻す。
            if (!done)
                PaintChosen();
            return After(done, message);
        }

        /// <summary>Turns the chosen reward's quality to the next.</summary>
        public bool NextQuality() =>
            chosenReward != 0 && After(Drafts.NextQuality(dungeon, chosenReward), null);

        // 変えたら描き直し、知らせを出す（なければ前の知らせを消す）。変えられなかったときは理由を赤で出す。
        private bool After(bool done, string message)
        {
            if (done)
                Render();
            if (message != null || done)
                Say(message ?? "", done);
            return done;
        }

        private void Say(string message, bool fine)
        {
            if (Status == null)
                return;
            UiText.Set(Status, message);
            Status.color = fine ? UiPalette.TextMain : ErrorColor;
        }

        // --- 物を選ぶ一覧 ----------------------------------------------------------------

        /// <summary>
        /// Opens the list of every kind to add to the drop list (<paramref name="target"/> 0) or to
        /// make the reward of that clear (1–5), on <paramref name="category"/>'s kinds (the
        /// chosen reward's when null). Materials are offered only for a reward.
        /// </summary>
        public bool OpenPicker(int target, AdventureDropCategory? category = null)
        {
            var draft = Draft;
            if (draft == null || target < 0 || target > draft.Rewards.Length)
                return false;
            if (target > 0 && Drafts.RewardOf(dungeon, target) == null)
                return false;
            pickingFor = target;
            UiText.Set(
                PickerTitle,
                target == 0 ? $"{draft.Name}の一覧に足す" : $"{target}回目の報酬を選ぶ"
            );
            for (int i = 0; i < PickerCategories.Length; i++)
                if (PickerCategories[i] != null)
                    PickerCategories[i]
                        .gameObject.SetActive(
                            target > 0
                                || DropLabDrafts.RewardCategories[i]
                                    != AdventureDropCategory.Material
                        );
            Show(DungeonGroup, false);
            Show(PickerGroup, true);
            ShowPicker(
                category ?? Drafts.RewardOf(dungeon, target)?.Category ?? AdventureDropCategory.Item
            );
            return true;
        }

        public void ClosePicker()
        {
            pickingFor = -1;
            Show(PickerGroup, false);
        }

        /// <summary>Shows every kind of <paramref name="category"/> in the open picker, each saying where it is listed now.</summary>
        public void ShowPicker(AdventureDropCategory category)
        {
            if (!Picking || PickerContent == null)
                return;
            pickerCategory = category;
            for (int i = 0; i < PickerCategories.Length; i++)
                if (
                    PickerCategories[i] != null
                    && PickerCategories[i].TryGetComponent(out GlassCard card)
                )
                    card.Selected = DropLabDrafts.RewardCategories[i] == category;
            picks.Clear();
            Clear(PickerContent);
            foreach (string id in DropLabDrafts.KindsOf(category))
            {
                var listed =
                    category == AdventureDropCategory.Material ? null : Drafts.ListOf(category, id);
                var card = MakeTile(
                    PickerContent,
                    category,
                    id,
                    Drafts.StarsOf(category, id),
                    false,
                    listed?.Name ?? ""
                );
                card.onClick.AddListener(() => Pick(category, id));
                picks.Add(new Tile(card, category, id));
            }
            if (PickerScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                PickerScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// Picks the kind from the open picker: adds it to the drop list (moving it from the list
        /// it was in) or makes it the reward, then closes the picker and chooses what changed.
        /// </summary>
        public bool Pick(AdventureDropCategory category, string id)
        {
            if (!Picking)
                return false;
            int target = pickingFor;
            if (target == 0)
            {
                bool added = Drafts.Add(dungeon, category, id, out string message);
                if (!added)
                    return After(false, message);
                ClosePicker();
                After(true, message ?? $"{DropLabDrafts.NameOf(category, id)}を足しました");
                return ChooseDrop(category, id);
            }
            Drafts.SetReward(dungeon, target, category, id);
            ClosePicker();
            After(true, $"{target}回目の報酬を{DropLabDrafts.NameOf(category, id)}にしました");
            return ChooseReward(target);
        }

        // --- クリア回数の報酬 --------------------------------------------------------------

        private void PaintRewards(DropLabDrafts.Draft draft)
        {
            for (int i = 0; i < Rewards.Length; i++)
            {
                var slot = Rewards[i];
                var reward = draft != null && i < draft.Rewards.Length ? draft.Rewards[i] : null;
                if (slot.Button != null)
                    slot.Button.gameObject.SetActive(reward != null);
                if (reward == null)
                    continue;
                var sprite = Art != null ? Art.Find(reward.Id) : null;
                if (slot.Icon != null)
                {
                    slot.Icon.sprite = sprite;
                    slot.Icon.enabled = sprite != null;
                }
                UiText.Set(
                    slot.Name,
                    DropLabDrafts.NameOf(reward.Category, reward.Id)
                        + (reward.Count > 1 ? $" ×{reward.Count}" : "")
                );
                string stars = StarsText(Drafts.StarsOf(reward.Category, reward.Id));
                UiText.SetRich(
                    slot.Grade,
                    reward.Quality is { } quality
                        ? $"{stars}  {QualityPalette.Painted(quality)}"
                        : stars
                );
            }
        }

        // --- コピー ----------------------------------------------------------------------

        /// <summary>
        /// Copies the dungeons changed as JSON to the clipboard, and says how many; with none
        /// changed it says so and copies nothing.
        /// </summary>
        public bool Copy()
        {
            int changed = Drafts.ChangedDrafts.Count;
            if (changed == 0)
            {
                Say("変えたダンジョンはまだありません", false);
                return false;
            }
            GUIUtility.systemCopyBuffer = Drafts.ChangesJson();
            Say($"変えた{changed}つのダンジョンをコピーしました", true);
            return true;
        }
    }
}
