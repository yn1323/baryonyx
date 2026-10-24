using System;
using System.Collections.Generic;
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
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.UI.UiText;

namespace Baryonyx.Library
{
    /// <summary>One entry's card in the list, baked for every entry of the kind.</summary>
    [Serializable]
    public sealed class LibraryTileWidget
    {
        public string Id = "";
        public GlassCard Card;
        public RawImage Art;
        public TMP_Text Number;
        public TMP_Text Name;

        // 新しく載った印。
        public GameObject New;
    }

    /// <summary>
    /// One kind of the library (doc/features/library.md) inside the guide screen's frame: the
    /// cards of every entry on the left, in the definitions' order, and the chosen entry on the
    /// right. Entries the player has not found show only their number and the art as a shadow,
    /// without the name. The generator bakes the cards from <see cref="Data"/>; this view reads
    /// what the player found each time it opens (<see cref="LibrarySession"/>) and draws it.
    /// </summary>
    public sealed class LibraryCatalogView : GuidePanel, IEditModePreview
    {
        public const string LoadFailedMessage = "ライブラリを取得できませんでした";
        public const string PreparingText = "準備中";

        // まだ見つけていない絵の影の色。
        public static readonly Color ShadowTint = new(0.02f, 0.03f, 0.08f, 0.88f);

        public LibraryKind Kind;
        public LibraryData Data;

        // キャラの固有スキルのツリーと、進化の仮の進み具合。
        public TrainingMockData Training;

        public LibraryTileWidget[] Tiles = Array.Empty<LibraryTileWidget>();

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

        // 敵の開示した弱点：「弱点」の行と、開示した弱点のアイコン、まだないときの文字。
        public GameObject WeaknessRow;
        public Image[] DetailIcons = Array.Empty<Image>();
        public TMP_Text WeaknessNote;

        private readonly List<(GlassCard Card, Action Click)> bindings = new();
        private readonly List<(Button Button, UnityEngine.Events.UnityAction Click)> tabBindings =
            new();
        private LibraryRecords records = LibraryRecords.Empty;
        private string selected;

        /// <summary>The entry shown on the right, or null.</summary>
        public string Selected => selected;

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
            // 記録と、敵の開示した弱点は互いに依存しないため、同時に読む。
            var reading = LibrarySession.LoadAsync(token);
            var weaknesses =
                Kind == LibraryKind.Enemy
                    ? RevealedWeaknessSession.LoadAsync(token)
                    : Task.CompletedTask;
            await Task.WhenAll(reading, weaknesses);
            if (token.IsCancellationRequested)
                return;
            // 読み直しても変わらなければ、出している記録のまま描き直さない。
            if (
                shown
                && Unchanged(shownVersion, LibrarySession.ReadVersion)
                && shownWeaknesses == RevealedWeaknessSession.Known.Count
            )
                return;
            Render(await reading);
        }

        /// <summary>Draws the records: found entries, shadows, new marks and the count.</summary>
        public void Render(LibraryRecords next)
        {
            records = next ?? LibraryRecords.Empty;
            Set(Status, "");
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
                if (tile.New != null)
                    tile.New.SetActive(seen.IsNew(records, Kind, tile.Id));
            }
            Set(Count, $"{found}/{Tiles.Length}");
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
                if (tile.Id == id && tile.New != null)
                    tile.New.SetActive(false);
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
            Set(DetailKind, found ? entry.KindLine : "");
            // まだ見つけていないことは、名前を消した影の絵でわかるため書かない。
            Set(DetailBody, found ? entry.Description : "");
            Set(DetailExtra, found ? ExtraOf(entry) : "");
            ShowWeaknesses(found ? entry.Id : null);
        }

        public static string NumberText(int number) => $"No.{number:000}";

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
                LibraryKind.Character => UniquesOf(entry.Id),
                LibraryKind.Enemy =>
                    $"倒した数　<color=#ffd766>{records.Defeated(entry.Id):N0}</color>",
                _ => "",
            };

        private string UniquesOf(string characterId)
        {
            var character = Training != null ? Training.Find(characterId) : null;
            if (character == null)
                return "";
            var text = new StringBuilder();
            for (int i = 0; i < character.Uniques.Length; i++)
            {
                var tree = UniqueSkillTrees.Find(character.Uniques[i].Tree);
                if (tree == null)
                    continue;
                var progress = UniqueSkillSession.ProgressOf(
                    characterId,
                    i,
                    character.MockProgressOf(i)
                );
                int acquired = 0;
                var stages = new List<string>();
                for (int stage = 0; stage < UniqueSkillTreeShape.Count; stage++)
                {
                    var card = tree.StageAt(stage)?.Card;
                    if (card != null && progress.Has(stage))
                    {
                        acquired++;
                        stages.Add(card.Name);
                    }
                    else
                        stages.Add(ShadowName);
                }
                if (text.Length > 0)
                    text.Append('\n');
                text.Append(
                    $"<color=#fde45a>固有スキル</color>　{acquired}/{UniqueSkillTreeShape.Count}\n"
                );
                text.Append(string.Join("・", stages));
            }
            return text.ToString();
        }

        // 取得していない段階の名前の代わりに置く、影の帯。
        private const string ShadowName = "<color=#2a3150>■■■</color>";

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

        // 読み込むまで、どれを見つけたかを見せない。
        private void ShowLoading()
        {
            Set(Status, LoadingText);
            Set(Count, "");
            foreach (var tile in Tiles)
                if (tile.Card != null)
                    tile.Card.gameObject.SetActive(false);
            if (Detail != null)
                Detail.SetActive(false);
        }

        private void ShowLoadFailed() => Set(Status, LoadFailedText);

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
