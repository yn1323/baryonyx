using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Economy;
using Baryonyx.Inventory;
using Baryonyx.Library;
using Baryonyx.Party;
using Baryonyx.Training;
using Baryonyx.UI;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// The trading house's "buy" (doc/features/screens/shop.md#商会), filling the screen: the tabs and
    /// a grid of every skill, weapon, armor and accessory the library has found
    /// (<see cref="WorkshopGoods"/>) on the left, read again each time it opens, and the chosen
    /// one's art (a skill as its deck card), quality, effect and price on the right. Press "buy" to buy one at the lowest
    /// quality: it shows at once and is saved behind (<see cref="WorkshopSession.BuyAsync"/>),
    /// so the same one can be bought again right away. A good the balance cannot pay for can be
    /// chosen, but not bought.
    /// </summary>
    public sealed class WorkshopBuyView : GuidePanel, IEditModePreview
    {
        public const string LoadFailedMessage = "品物を取得できませんでした";
        public const string BuyFailedMessage = "買えませんでした";
        public const string EmptyText = "まだ手に入れた物がありません";

        // 買う物の品質。絵（スキルはカード）の真上の1行に、品質の文字を品質の色で大きく、横に倍率を書く。
        // 「品質」とは書かない。装備もスキルも同じ位置・同じ形にする。
        public const ItemQuality BoughtQuality = ItemQuality.C;
        public static readonly string QualityText =
            $"<size=300%>{QualityPalette.Painted(BoughtQuality)}</size><voffset=0.3em>　{Inventory.Quality.Times(Inventory.Quality.MinMultiplier)}</voffset>";

        // 読み込む間に伏せて並べるカードの数（4列×3段で一覧の見える所を埋める）。
        public const int CoverCards = 12;

        public static readonly string[] TabLabels = { "武器", "防具", "アクセサリー", "スキル" };

        public LibraryData Data;
        public PartyMockData Party;

        // 絵を置く正方形の一辺（設計座標）。ドット絵は、収まるいちばん大きい整数倍で出す。
        public float ArtBox = 128f;
        public float DetailArtBox = 256f;
        public GuideTab[] Tabs = Array.Empty<GuideTab>();
        public ScrollRect List;
        public RectTransform Content;

        // カードの見本（隠してある）。品物が焼いたカードより多ければ、これを複製する。
        public WorkshopGoodRow Template;

        // 生成のときに仮の記録の品物で焼いたカード。実行中は品物の数に合わせて増やし、余りを隠す。
        public List<WorkshopGoodRow> Rows = new();

        // 読み込み中・読めなかったとき・品物がないときだけ一覧に出す文字。
        public TMP_Text Status;

        // 右の面：上から品質、選んだ品物の絵・名前・☆・効果・持っている数。品物がないときは何も出さない。
        public TMP_Text DetailQuality;
        public RawImage DetailArt;
        public TMP_Text DetailName;
        public TMP_Text DetailLine;
        public TMP_Text DetailEffect;
        public TMP_Text DetailOwned;

        // スキルを選んだときに、絵・名前・効果の代わりに出すデッキのカード。属性ごとの枠とアイコン。
        public TrainingCardWidget DetailCard = new();
        public Texture[] CardFrames = Array.Empty<Texture>();
        public Sprite[] ElementIcons = Array.Empty<Sprite>();

        // 「買う」の左の値段と、ルーンが足りないときの文字。
        public GameObject Price;
        public TMP_Text PriceLabel;
        public TMP_Text Missing;
        public Button Confirm;

        private readonly HashSet<Component> bound = new();
        private readonly GlassCardCover cover = new();
        private IReadOnlyList<WorkshopGood> goods = Array.Empty<WorkshopGood>();
        private IReadOnlyList<WorkshopGood> shown = Array.Empty<WorkshopGood>();
        private IReadOnlyList<WorkshopOwned> owned;
        private int tab;
        private int selected = -1;

        // 買った回数。読み直している間に買ったら、その前の読み込みで持っている数を戻さない。
        private int bought;

        public IReadOnlyList<WorkshopGood> Goods => goods;

        /// <summary>The goods of the chosen tab, in the grid's order.</summary>
        public IReadOnlyList<WorkshopGood> Shown => shown;

        public int Tab => tab;

        /// <summary>The chosen good's index in <see cref="Shown"/>, or -1.</summary>
        public int Selected => selected;

        // 保存を待っている、または直前の購入。テストで完了を待つために公開する。
        public Task BuyTask { get; private set; } = Task.CompletedTask;

        private void OnEnable()
        {
            BindControls();
            RuneBalance.Changed += RefreshConfirm;
            // 前に読んだライブラリの記録があれば、品物をすぐ出して裏で読み直す。持っている数は、
            // 前に読んだ編成と装備があれば添える。なければ読み終えるまで品物を出さない。
            var known = LibrarySession.Known;
            if (known != null)
            {
                owned = WorkshopStock.Known();
                Render(WorkshopGoods.Of(Data, known));
            }
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
            RuneBalance.Changed -= RefreshConfirm;
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
                Confirm.onClick.AddListener(Buy);
        }

        private async Task LoadAsync(bool shown, CancellationToken token)
        {
            string shownVersion = LibrarySession.ReadVersion;
            int boughtBefore = bought;
            var stock = LoadOwnedAsync(token);
            var records = await LibrarySession.LoadAsync(token);
            if (token.IsCancellationRequested)
                return;
            var read = await stock;
            if (token.IsCancellationRequested)
                return;
            // 読んでいる間に買ったら、読んだ持ち物は買う前のものなので、手元で足した数を残す。
            // 前の持ち物を出しているときは、読めなくてもそのまま残す。
            if (!shown || (read != null && bought == boughtBefore))
                owned = read;
            // 品物が変わらなければ、選んでいる品物のまま、持っている数だけを出し直す。
            if (shown && Unchanged(shownVersion, LibrarySession.ReadVersion))
                RefreshConfirm();
            else
                Render(WorkshopGoods.Of(Data, records));
        }

        // 持っている数は添えるだけなので、読めなくても品物は出す。
        private async Task<IReadOnlyList<WorkshopOwned>> LoadOwnedAsync(CancellationToken token)
        {
            try
            {
                return await WorkshopStock.LoadAsync(Party, token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("持ち物を取得できませんでした。" + exception.Message, this);
                return null;
            }
        }

        /// <summary>Takes the goods and shows the chosen tab's, the first chosen.</summary>
        public void Render(IReadOnlyList<WorkshopGood> next)
        {
            goods = next ?? Array.Empty<WorkshopGood>();
            ShowTab(tab);
        }

        /// <summary>Shows the goods of the tab <paramref name="index"/> (<see cref="WorkshopGoods.Kinds"/>), the first chosen.</summary>
        public void ShowTab(int index)
        {
            tab = Mathf.Clamp(index, 0, WorkshopGoods.Kinds.Count - 1);
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i == tab;
            shown = goods.Where(good => good.Kind == WorkshopGoods.Kinds[tab]).ToArray();
            selected = -1;
            EnsureRows(shown.Count);
            for (int i = 0; i < Rows.Count; i++)
            {
                bool visible = i < shown.Count;
                Rows[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                Paint(Rows[i], shown[i], ArtBox);
                if (Rows[i].Card != null)
                    Rows[i].Card.Selected = false;
            }
            // 右の面が空にならないよう、先頭の品物を選んでおく。
            if (shown.Count > 0)
            {
                selected = 0;
                if (Rows.Count > 0 && Rows[0].Card != null)
                    Rows[0].Card.Selected = true;
            }
            Set(Status, goods.Count == 0 ? EmptyText : "");
            // 品物のない場所の伏せたカードも、めくってから静かに消す。
            cover.Reveal(
                Rows.Take(shown.Count).Select(row => row.gameObject),
                Rows.Skip(shown.Count).Take(CoverCards - shown.Count).Select(row => row.gameObject)
            );
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            RefreshConfirm();
        }

        public void Select(int index)
        {
            if (index < 0 || index >= shown.Count)
                return;
            selected = index;
            for (int i = 0; i < shown.Count && i < Rows.Count; i++)
                if (Rows[i].Card != null)
                    Rows[i].Card.Selected = i == index;
            RefreshConfirm();
        }

        /// <summary>
        /// Buys one of the chosen good, if the balance the app knows can pay for it, and tells it
        /// at once. A failed save comes back on the notice band later.
        /// </summary>
        public void Buy()
        {
            var good = SelectedGood;
            if (!CanBuy(good))
                return;
            ShowNotice(BoughtText(good));
            bought++;
            var buying = WorkshopSession.BuyAsync(good, BuyFailedMessage);
            if (owned != null)
                owned = owned
                    .Append(new WorkshopOwned { Kind = good.Kind, KindId = good.Id })
                    .ToArray();
            BuyTask = TakeBackIfFailedAsync(buying, good);
            RefreshConfirm();
        }

        // 保存できなかったら、値段と一緒に、手元で足した持っている数の1つも戻す。
        private async Task TakeBackIfFailedAsync(Task<bool> buying, WorkshopGood good)
        {
            if (await buying || this == null || owned == null)
                return;
            int index = -1;
            for (int i = owned.Count - 1; i >= 0 && index < 0; i--)
                if (owned[i].Kind == good.Kind && owned[i].KindId == good.Id)
                    index = i;
            if (index < 0)
                return;
            owned = owned.Where((_, i) => i != index).ToArray();
            RefreshConfirm();
        }

        private WorkshopGood SelectedGood =>
            selected >= 0 && selected < shown.Count ? shown[selected] : null;

        // 保存を待っている購入の分は、残高から引いてある。残高を読めていないときは、サーバーに任せて買わせる。
        private static bool CanBuy(WorkshopGood good) =>
            good != null && (RuneBalance.Known is not { } runes || runes >= good.Price);

        private void RefreshConfirm()
        {
            var good = SelectedGood;
            if (Confirm != null)
                Confirm.interactable = CanBuy(good);
            ShowDetail(good);
        }

        private void ShowDetail(WorkshopGood good)
        {
            // スキルは、買ったときの品質（×1.00）のデッキのカードで見せる。
            var card = good?.Kind == LibraryKind.Skill ? CardSkills.Find(good.Id) : null;
            WorkshopSkillCard.Show(
                DetailCard,
                card,
                Inventory.Quality.MinMultiplier,
                CardSkillQuality.LimitOf(card),
                false,
                Party,
                CardFrames,
                ElementIcons
            );
            if (DetailArt != null)
            {
                // 品物がないときは、絵の後ろの光ごと隠す。
                DetailArt.transform.parent.gameObject.SetActive(good != null && card == null);
                if (good != null && card == null)
                    LibraryCatalogView.PaintArt(DetailArt, good.Entry, true, DetailArtBox);
            }
            Set(DetailQuality, good != null ? QualityText : "");
            Set(DetailName, card == null ? good?.Name : "");
            Set(DetailLine, good != null ? StarsText(good.Stars) : "");
            Set(DetailEffect, good != null && card == null ? EffectOf(good) : "");
            Set(
                DetailOwned,
                good != null && owned != null
                    ? OwnedText(WorkshopStock.CountOf(owned, good.Kind, good.Id))
                    : ""
            );
            // 右の面は中身を上下中央に並べるため、書くことのない行は詰める。
            foreach (
                var label in new[]
                {
                    DetailQuality,
                    DetailName,
                    DetailLine,
                    DetailEffect,
                    DetailOwned,
                }
            )
                if (label != null)
                    label.gameObject.SetActive(label.text.Length > 0);
            if (Price != null)
                Price.SetActive(good != null);
            Set(PriceLabel, good != null ? good.Price.ToString("N0") : "");
            Set(Missing, good != null ? MissingText(good, RuneBalance.Known) : "");
        }

        private void EnsureRows(int count)
        {
            Rows.RemoveAll(row => row == null);
            while (Rows.Count < count && Template != null && Content != null)
                Rows.Add(Instantiate(Template, Content));
            for (int i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                row.name = "Good" + i;
                if (row.Card == null || !bound.Add(row))
                    continue;
                int index = i;
                row.Card.Clicked += () => Select(index);
            }
        }

        /// <summary>Writes the good on its card. The generator bakes the mock goods with it too.</summary>
        public static void Paint(WorkshopGoodRow row, WorkshopGood good, float artBox)
        {
            Set(row.Name, good.Name);
            Set(row.Price, good.Price.ToString("N0"));
            // 買う物はいつも品質C。売るの札と同じく左上に品質の色で書く。
            if (row.Quality != null)
            {
                row.Quality.text = Inventory.Quality.Letter(BoughtQuality);
                row.Quality.color = QualityPalette.Of(BoughtQuality);
            }
            Set(row.Detail, new string('★', good.Stars));
            LibraryCatalogView.PaintArt(row.Art, good.Entry, true, artBox);
        }

        // 品物を読むまで、一覧の見える所をカードの裏で埋める。
        private void ShowLoading()
        {
            goods = shown = Array.Empty<WorkshopGood>();
            selected = -1;
            EnsureRows(CoverCards);
            for (int i = 0; i < Rows.Count; i++)
            {
                Rows[i].gameObject.SetActive(i < CoverCards);
                if (Rows[i].Card != null)
                    Rows[i].Card.Selected = false;
            }
            cover.Cover(Rows.Take(CoverCards).Select(row => row.gameObject));
            Set(Status, "");
            RefreshConfirm();
        }

        private void ShowLoadFailed()
        {
            cover.Clear();
            foreach (var row in Rows)
                if (row != null)
                    row.gameObject.SetActive(false);
            Set(Status, LoadFailedText);
        }

        public static string PriceText(int price) => $"{price:N0} ルーン";

        public static string StarsText(int stars) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(UiPalette.Gold)}>{new string('★', stars)}</color>";

        /// <summary>
        /// The good's effect: the skill's kind, target and cost over its text, or the equipment's
        /// effect alone, since the tab already says which kind it is.
        /// </summary>
        public static string EffectOf(WorkshopGood good)
        {
            var entry = good.Entry;
            return good.Kind == LibraryKind.Skill
                ? Join(LibraryKinds.Label(LibraryKind.Skill), entry.KindLine)
                    + "\n"
                    + entry.Description
                : entry.Description;
        }

        public static string OwnedText(int count) => $"持っている数　{count}";

        /// <summary>How many runes are missing to buy <paramref name="good"/>, or empty.</summary>
        public static string MissingText(WorkshopGood good, long? runes) =>
            runes is { } balance && balance < good.Price
                ? $"ルーンが {good.Price - balance:N0} 足りません"
                : "";

        public static string BoughtText(WorkshopGood good) =>
            $"{good.Name}を {good.Price:N0} ルーンで買った";

        private static string Join(string first, string second) =>
            string.Join("｜", new[] { first, second }.Where(text => !string.IsNullOrEmpty(text)));

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }

        // 停止中の表示の切り替え：焼いた仮の品物、選んだとき、サーバーから読み込む間、読めなかったとき。
        public IReadOnlyList<string> PreviewStates =>
            new[] { "品物", "選んだとき" }.Concat(GuidePanelLoad.PreviewStates).ToArray();

        public void ShowPreviewState(int index)
        {
            if (index <= 1)
            {
                foreach (var row in Rows)
                    if (row != null)
                        row.gameObject.SetActive(true);
                Set(Status, "");
                for (int i = 0; i < Rows.Count; i++)
                    if (Rows[i] != null && Rows[i].Card != null)
                        Rows[i].Card.Selected = index == 1 && i == 1;
                ShowDetail(index == 1 ? PreviewGood : null);
                return;
            }
            ShowLoading();
            if (index == 3)
                ShowLoadFailed();
        }

        // 停止中に選んだときの見た目を確かめる品物（焼いた2枚目）。
        private WorkshopGood PreviewGood =>
            WorkshopGoods.Of(Data, LibraryMemorySource.Mock()) is { Count: > 1 } mock
                ? mock[1]
                : null;
    }
}
