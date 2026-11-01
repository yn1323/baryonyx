using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.Inventory;
using Baryonyx.Library;
using Baryonyx.UI;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.TravelOffice
{
    /// <summary>The treasure button at the right end of a destination's row, with its count.</summary>
    [Serializable]
    public sealed class TravelTreasureRow
    {
        public string DestinationId = "";
        public Button Open;
        public TMP_Text Count;
    }

    /// <summary>
    /// One treasure's tile in the window: which treasure it is, and its card (the shared
    /// <see cref="ItemTile"/>: the art or shadow, the stars, the "？" and the boss's crown).
    /// </summary>
    [Serializable]
    public sealed class TravelTreasureTile
    {
        public string Id = "";
        public LibraryKind Kind;
        public bool BossOnly;
        public ItemTile Tile;

        public GlassCard Card => Tile != null ? Tile.Card : null;
        public RawImage Art => Tile != null ? Tile.Art : null;
        public TMP_Text Stars => Tile != null ? Tile.Stars : null;

        // まだ見つけていないときに、影の代わりに出す「？」（スキルの札だけ）。
        public GameObject Unknown => Tile != null ? Tile.Unknown : null;
    }

    /// <summary>
    /// One clear's reward in the window's left column (doc/features/stage-progression.md,
    /// ダンジョンのクリア回数の報酬): its mark on the line, the card with the art, name, stars and
    /// quality and the runes, a check once received, and a gold rim on the next one.
    /// </summary>
    [Serializable]
    public sealed class TravelClearRewardRow
    {
        public Image Mark;
        public Image Link;
        public CanvasGroup Card;
        public Image Art;
        public TMP_Text Name;
        public TMP_Text Grade;
        public TMP_Text Runes;
        public GameObject Received;
        public GameObject Next;

        // まだ受け取っていないスキルの札の「？」（挿絵が四角く、影では形がわからないため）。
        public GameObject Unknown;
    }

    /// <summary>The tiles of one destination, baked into the window; only the open one is shown.</summary>
    [Serializable]
    public sealed class TravelTreasureList
    {
        public string DestinationId = "";
        public string Title = "";
        public RectTransform Content;
        public TravelTreasureTile[] Tiles = Array.Empty<TravelTreasureTile>();
    }

    /// <summary>
    /// The travel office's treasures (doc/features/screens/guide.md): each destination's row shows how
    /// many of its treasures the library has found, and its button opens a window in the middle of
    /// the screen with every treasure of the destination, the found ones in their own art and the
    /// others as shadows, and how many times the destination was cleared. The records are read
    /// from the library each time the screen opens; the clears come from the adventure read by
    /// the departure (<see cref="AdventureSession.Current"/>).
    /// The back button and the device back key close the window first.
    /// </summary>
    public sealed class TravelTreasureView : MonoBehaviour, IGuideBackHandler, IEditModePreview
    {
        public const string LoadFailedMessage = "お宝の記録を取得できませんでした";
        public const string HiddenName = "？？？";

        public GuideMenuView Guide;
        public LibraryData Data;
        public float TileArtBox = 96f;

        public TravelTreasureRow[] Rows = Array.Empty<TravelTreasureRow>();

        // 画面の中央の窓と、その後ろの暗い幕（押すと閉じる）。
        public GameObject Window;
        public Button Veil;
        public Button Close;
        public TMP_Text Title;
        public TMP_Text Count;

        // その行き先をクリアした（最奥の主を倒した）回数と、左の列のクリア回数の報酬（1〜5回目）。
        public TMP_Text Clears;
        public TravelClearRewardRow[] Rewards = Array.Empty<TravelClearRewardRow>();
        public ItemArt ItemArt;
        public Color MarkLit = new(1f, 0.843f, 0.4f, 1f);
        public Color MarkDim = new(0.2f, 0.24f, 0.33f, 1f);
        public float ReceivedAlpha = 0.6f;
        public ScrollRect Scroll;
        public TravelTreasureList[] Lists = Array.Empty<TravelTreasureList>();

        // 窓の下の、選んだお宝の名前・☆・分類と説明。
        public TMP_Text Info;
        public TMP_Text InfoBody;

        private readonly GuidePanelLoad load = new();
        private readonly List<Action> unbind = new();
        private LibraryRecords records;
        private int open = -1;
        private TravelTreasureTile selected;

        // 実行中または直前の読み込み。テストで完了を待つために公開する。
        public Task LoadTask => load.Task;

        /// <summary>The open destination's index in <see cref="Lists"/>, or -1.</summary>
        public int OpenIndex => open;

        public bool IsOpen => Window != null && Window.activeSelf;

        private void OnEnable()
        {
            for (int i = 0; i < Rows.Length; i++)
            {
                var button = Rows[i]?.Open;
                if (button == null)
                    continue;
                string id = Rows[i].DestinationId;
                UnityEngine.Events.UnityAction click = () => Open(id);
                button.onClick.AddListener(click);
                unbind.Add(() => button.onClick.RemoveListener(click));
            }
            foreach (var button in new[] { Close, Veil })
            {
                if (button == null)
                    continue;
                button.onClick.AddListener(Hide);
                unbind.Add(() => button.onClick.RemoveListener(Hide));
            }
            foreach (var tile in Lists.SelectMany(list => list.Tiles))
            {
                if (tile.Card == null)
                    continue;
                string id = tile.Id;
                var kind = tile.Kind;
                Action click = () => Select(kind, id);
                var card = tile.Card;
                card.Clicked += click;
                unbind.Add(() => card.Clicked -= click);
            }
            Hide();
            // 前に読んだ記録があれば、すぐ出して裏で読み直す。なければ数を「-」、お宝を影にして待つ。
            var known = LibrarySession.Known;
            Render(known);
            load.Start(token => LoadAsync(known != null, token), Failed);
        }

        private void OnDisable()
        {
            load.Cancel();
            foreach (var action in unbind)
                action();
            unbind.Clear();
        }

        private async Task LoadAsync(bool shown, CancellationToken token)
        {
            string shownVersion = LibrarySession.ReadVersion;
            var read = await LibrarySession.LoadAsync(token);
            if (token.IsCancellationRequested)
                return;
            // 読み直しても変わらなければ、出している記録と選んだお宝のまま描き直さない。
            if (shown && shownVersion != null && shownVersion == LibrarySession.ReadVersion)
                return;
            Render(read);
        }

        private void Failed(Exception exception)
        {
            Debug.LogWarning(LoadFailedMessage + "。" + exception.Message);
            if (Guide != null)
                Guide.ShowToast(LoadFailedMessage);
        }

        /// <summary>
        /// Draws the records on the rows' counts and the tiles. Before they are read
        /// (<paramref name="next"/> is null), the counts show "-" and every tile is a shadow.
        /// </summary>
        public void Render(LibraryRecords next)
        {
            records = next;
            foreach (var row in Rows)
                if (row?.Count != null)
                    row.Count.text = TravelTreasures.CountText(row.DestinationId, records);
            foreach (var list in Lists)
            foreach (var tile in list.Tiles)
                Paint(tile, IsFound(tile.Kind, tile.Id));
            ShowCount();
            ShowInfo();
        }

        /// <summary>Opens the window on the destination's treasures.</summary>
        public void Open(string destinationId)
        {
            int index = Array.FindIndex(Lists, list => list.DestinationId == destinationId);
            if (index < 0)
                return;
            Show(index);
            if (Scroll != null)
                Scroll.verticalNormalizedPosition = 1f;
        }

        public void Hide()
        {
            open = -1;
            selected = null;
            if (Window != null)
                Window.SetActive(false);
        }

        // 窓を開いている間は、「もどる」と端末の戻るキーで窓を閉じる。
        public bool HandleBack()
        {
            if (!IsOpen)
                return false;
            Hide();
            return true;
        }

        /// <summary>Shows the treasure's name, stars and kind under the tiles.</summary>
        public void Select(LibraryKind kind, string id)
        {
            if (open < 0)
                return;
            selected = Array.Find(Lists[open].Tiles, tile => tile.Kind == kind && tile.Id == id);
            foreach (var tile in Lists[open].Tiles)
                if (tile.Card != null)
                    tile.Card.Selected = tile == selected;
            ShowInfo();
        }

        private void Show(int index)
        {
            open = index;
            selected = null;
            if (Window != null)
                Window.SetActive(true);
            for (int i = 0; i < Lists.Length; i++)
                if (Lists[i].Content != null)
                    Lists[i].Content.gameObject.SetActive(i == index);
            foreach (var tile in Lists[index].Tiles)
                if (tile.Card != null)
                    tile.Card.Selected = false;
            if (Scroll != null && Lists[index].Content != null)
                Scroll.content = Lists[index].Content;
            Set(Title, Lists[index].Title);
            ShowCount();
            ShowInfo();
        }

        private void ShowCount()
        {
            if (open < 0)
                return;
            string id = Lists[open].DestinationId;
            Set(Count, TravelTreasures.CountText(id, records));
            var state = AdventureSession.Current;
            Set(Clears, ClearsText(state, id));
            ShowRewards(id, state == null ? -1 : state.RecordOf(id)?.Clears ?? 0);
        }

        /// <summary>
        /// Paints the destination's five clear rewards: those already received dimmed with a
        /// check, the next one in a gold rim, and the marks lit up to the clears. With
        /// <paramref name="clears"/> below 0 (not read yet) none is received or next.
        /// </summary>
        public void ShowRewards(string destinationId, int clears)
        {
            var rewards = AdventureClearRewards.Of(destinationId);
            for (int i = 0; i < Rewards.Length; i++)
            {
                var row = Rewards[i];
                bool used = i < rewards.Count;
                if (row.Card != null)
                    row.Card.gameObject.SetActive(used);
                if (!used)
                    continue;
                bool received = i < clears;
                bool next = i == clears;
                var drop = rewards[i].Drop;
                // 受け取った回だけ絵と名前を出す。まだの回は形だけの影と「？？？」にし、ルーンだけを見せる
                // （何をもらえるかは伏せ、影の形で種類だけわかる、2026-11-01、ユーザー）。
                bool square = drop.Category == AdventureDropCategory.Skill;
                var icon = AdventureTexts.DropIcon(drop, ItemArt);
                if (row.Art != null)
                {
                    row.Art.sprite = icon;
                    row.Art.enabled = icon != null && (received || !square);
                    row.Art.color = received ? Color.white : LibraryCatalogView.ShadowTint;
                }
                if (row.Unknown != null)
                    row.Unknown.SetActive(square && !received);
                string count = rewards[i].Count > 1 ? $" ×{rewards[i].Count}" : "";
                Set(row.Name, received ? AdventureTexts.DropName(drop) + count : HiddenName);
                string stars = StarsText(drop.Stars);
                Set(
                    row.Grade,
                    !received ? ""
                        : drop.Quality is { } quality
                            ? $"{stars}  {QualityPalette.Painted(quality)}"
                        : stars
                );
                Set(row.Runes, rewards[i].Runes.ToString("N0"));
                if (row.Received != null)
                    row.Received.SetActive(received);
                if (row.Next != null)
                    row.Next.SetActive(next);
                if (row.Card != null)
                    row.Card.alpha = received ? ReceivedAlpha : 1f;
                if (row.Mark != null)
                    row.Mark.color = received ? MarkLit : MarkDim;
                if (row.Link != null)
                    row.Link.color = i + 1 < clears ? MarkLit : MarkDim;
            }
        }

        /// <summary>
        /// How many times the destination was cleared (its master beaten), from the adventure's
        /// records (doc/features/stage-progression.md#ダンジョンのクリア回数の報酬). "-" before
        /// the records are read.
        /// </summary>
        public static string ClearsText(AdventureState state, string destinationId) =>
            state == null ? "クリア -回" : $"クリア {state.RecordOf(destinationId)?.Clears ?? 0}回";

        private void ShowInfo()
        {
            var tile = open >= 0 ? selected : null;
            if (tile == null)
            {
                Set(Info, "");
                Set(InfoBody, "");
                return;
            }
            var entry = EntryOf(tile.Kind, tile.Id);
            bool found = entry != null && IsFound(tile.Kind, tile.Id);
            // まだ見つけていないお宝は名前を伏せるが、☆は札と同じく出す。分類は段の名前でわかるため書かない。
            string stars = entry != null ? StarsText(entry.Rarity) : "";
            Set(
                Info,
                Join(
                    found ? entry.Name : HiddenName,
                    stars.Length > 0
                        ? $"<color=#{ColorUtility.ToHtmlStringRGB(UiPalette.Gold)}>{stars}</color>"
                        : ""
                )
            );
            // 主の物は札の王冠で、まだ見つけていない物は伏せた名前でわかるため、説明だけを書く。
            Set(InfoBody, found ? entry.Description : "");
        }

        private void Paint(TravelTreasureTile tile, bool found)
        {
            var entry = EntryOf(tile.Kind, tile.Id);
            LibraryCatalogView.PaintArt(tile.Art, entry, found, TileArtBox);
            // スキルの挿絵は四角く、影にしても形がわからないため、「？」に替える。
            if (tile.Unknown != null)
            {
                tile.Unknown.SetActive(!found);
                if (!found && tile.Art != null)
                    tile.Art.enabled = false;
            }
            // まだ見つけていないお宝は形だけの影にするが、☆は出して珍しさを伝える。
            if (tile.Stars != null)
                tile.Stars.text = entry != null ? StarsText(entry.Rarity) : "";
        }

        private bool IsFound(LibraryKind kind, string id) =>
            records != null && records.IsFound(kind, id);

        private LibraryEntryData EntryOf(LibraryKind kind, string id) =>
            Data != null
                ? Array.Find(Data.Entries, entry => entry.Kind == kind && entry.Id == id)
                : null;

        public static string StarsText(int stars) => stars > 0 ? new string('★', stars) : "";

        private static string Join(string first, string second) =>
            string.Join("　", new[] { first, second }.Where(text => !string.IsNullOrEmpty(text)));

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }

        // 停止中の表示の切り替え：行き先ごとに、お宝の窓を開いたときの表示（Prefabに焼いた仮の記録のまま）。
        public IReadOnlyList<string> PreviewStates => Lists.Select(list => list.Title).ToArray();

        public void ShowPreviewState(int index)
        {
            if (index < 0 || index >= Lists.Length)
                return;
            if (Window != null)
                Window.SetActive(true);
            for (int i = 0; i < Lists.Length; i++)
                if (Lists[i].Content != null)
                    Lists[i].Content.gameObject.SetActive(i == index);
            if (Scroll != null)
                Scroll.content = Lists[index].Content;
            Set(Title, Lists[index].Title);
            // 停止中は冒険の記録がないため、2回クリアした見本で報酬の列を見せる。
            Set(Clears, $"クリア {PreviewClears}回");
            ShowRewards(Lists[index].DestinationId, PreviewClears);
        }

        public const int PreviewClears = 2;
    }
}
