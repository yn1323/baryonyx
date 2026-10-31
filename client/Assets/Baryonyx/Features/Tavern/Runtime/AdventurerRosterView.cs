using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Party;
using Baryonyx.Training;
using Baryonyx.UI;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.UI.UiText;

namespace Baryonyx.Tavern
{
    /// <summary>One of the four party slots on the left, baked by the generator.</summary>
    [Serializable]
    public sealed class AdventurerSlotWidget
    {
        public GlassCard Card;

        // 立ち姿（周りの余白を切って4倍）と足元の影。空いた枠では隠す。
        public RawImage Figure;
        public GameObject Shadow;
        public TMP_Text Name;
        public GameObject LevelLine;
        public TMP_Text Level;
        public Image[] Elements = Array.Empty<Image>();

        // 空いているときだけ出す「空き」。
        public TMP_Text Empty;
    }

    /// <summary>The band at the bottom: the person chosen and the button that opens their tabs.</summary>
    [Serializable]
    public sealed class AdventurerChosenWidget
    {
        // 立ち姿（周りの余白を切って2倍）。選んでいないときは隠す。
        public RawImage Figure;
        public TMP_Text Name;
        public GameObject LevelLine;
        public TMP_Text Level;
        public Image[] Elements = Array.Empty<Image>();

        // その人の冒険者のタブ（ステータス・装備・スキル）を開く。選んでいないときは出さない。
        public Button Detail;
        public CanvasGroup DetailGroup;
    }

    /// <summary>One character's card in the list on the right, baked for every mock character.</summary>
    [Serializable]
    public sealed class AdventurerTileWidget
    {
        public string Id = "";
        public GlassCard Card;
        public TMP_Text Level;
        public Image[] Elements = Array.Empty<Image>();
    }

    /// <summary>
    /// The formation's adventurers inside the guide screen's frame: the party's four slots on
    /// the left and the owned characters outside the party on the right, as glass cards, the
    /// list ending with "remove from the party", and the band at the bottom with the person
    /// chosen and the button that opens their tabs (stats, equipment, card skills).
    /// AdventurerRosterAssets bakes the cards into the formation prefab, so they read in the
    /// editor; this view wires them up and drives the party's roster presenter. When the app has a server
    /// (<see cref="PartySession.Source"/>), it reads the party on opening and saves every change
    /// there; otherwise it uses the mock data. Changes are told on the guide screen's notice band.
    /// </summary>
    public sealed class AdventurerRosterView : GuidePanel, IPartyRosterView, IEditModePreview
    {
        public const string LoadFailedMessage = "編成を取得できませんでした";

        // 外せる人を選んでいないときの「パーティから外す」の濃さ。
        private const float RemoveIdleAlpha = 0.5f;

        // 読み込む間に右の一覧で伏せて並べるカードの数（2列×2段）。左のパーティの4枠もすべて伏せる。
        public const int CoverTiles = 4;

        public PartyMockData Party;

        public AdventurerSlotWidget[] Slots = Array.Empty<AdventurerSlotWidget>();
        public AdventurerTileWidget[] Tiles = Array.Empty<AdventurerTileWidget>();

        // 一覧の最後に置く「パーティから外す」。
        public GlassCard Remove;
        public ScrollRect List;

        // 読み込み中と、読めなかったときだけ一覧の面に出す文字。
        public TMP_Text Status;

        // 下の帯：選んでいる人と、その人の冒険者のタブを開く「装備・スキル」。
        public AdventurerChosenWidget Chosen = new();

        private PartyRosterPresenter presenter;
        private readonly List<(GlassCard Card, Action Click)> bindings = new();
        private readonly GlassCardCover cover = new();

        public event Action<int> SlotPressed;
        public event Action<string> BenchPressed;
        public event Action RemovePressed;
        public event Action DetailPressed;

        public PartyRosterPresenter Presenter => presenter;

        private void OnEnable()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                Bind(Slots[i].Card, () => SlotPressed?.Invoke(index));
            }
            foreach (var tile in Tiles)
            {
                string id = tile.Id;
                Bind(tile.Card, () => BenchPressed?.Invoke(id));
            }
            Bind(Remove, () => RemovePressed?.Invoke());
            if (Chosen.Detail != null)
                Chosen.Detail.onClick.AddListener(PressDetail);

            // 開くたびに、サーバーの編成（なければアプリを動かしている間のもの）を読み直す。
            presenter?.Dispose();
            presenter = null;
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            if (Party == null)
                return;
            var source = PartySession.Source;
            // 前に読んだ編成があれば、すぐ出して裏で読み直す。なければ読み終えるまで仮データを隠す。
            var known = PartySession.Known;
            if (known != null)
                Present(known, source);
            else if (source != null)
                ShowLoading();
            StartLoad(
                token => LoadAsync(source, known != null, token),
                LoadFailedMessage,
                () =>
                {
                    // 前の編成を出しているときは、そのまま残して通知だけにする。
                    if (known == null)
                        ShowLoadFailed();
                },
                readAgain: known != null
            );
        }

        private void Bind(GlassCard card, Action click)
        {
            if (card == null)
                return;
            card.Clicked += click;
            bindings.Add((card, click));
        }

        private void PressDetail() => DetailPressed?.Invoke();

        private async Task LoadAsync(IPartySource source, bool shown, CancellationToken token)
        {
            string shownVersion = PartySession.ReadVersion;
            var formation = await PartySession.LoadAsync(Party, source, token);
            if (token.IsCancellationRequested)
                return;
            if (shown && Unchanged(shownVersion, PartySession.ReadVersion))
                return;
            Present(formation, source);
        }

        private void Present(PartyFormation formation, IPartySource source)
        {
            presenter?.Dispose();
            Set(Status, "");
            presenter = new PartyRosterPresenter(
                this,
                formation,
                source != null
                    ? (slot, id) =>
                        PartySession.SetSlotAsync(slot, id, PartyRosterPresenter.SaveFailedMessage)
                    : null
            );
        }

        private void ShowLoadFailed()
        {
            cover.Clear();
            foreach (var tile in Tiles)
                if (tile.Card != null)
                    tile.Card.gameObject.SetActive(false);
            Set(Status, LoadFailedText);
        }

        // 読み込むまで、仮データの編成を本当の編成として見せない。パーティの枠と一覧の先頭はカードの裏で埋める。
        private void ShowLoading()
        {
            Set(Status, "");
            foreach (var slot in Slots)
            {
                ShowSlot(slot, null, 1, null, false);
                if (slot.Empty != null)
                    slot.Empty.gameObject.SetActive(false);
            }
            foreach (var tile in Tiles)
                if (tile.Card != null)
                    tile.Card.gameObject.SetActive(false);
            if (Remove != null)
                Remove.gameObject.SetActive(false);
            cover.Cover(
                Slots
                    .Select(slot => slot.Card)
                    .Concat(Tiles.Select(tile => tile.Card).Take(CoverTiles))
                    .Where(card => card != null)
                    .Select(card => card.gameObject)
            );
            ShowChosen(null);
        }

        // 停止中の表示の切り替え：サーバーから読み込む間と、読めなかったときの表示。
        public IReadOnlyList<string> PreviewStates => GuidePanelLoad.PreviewStates;

        public void ShowPreviewState(int index)
        {
            ShowLoading();
            if (index == 1)
                ShowLoadFailed();
        }

        private void OnDisable()
        {
            Load.Cancel();
            foreach (var (card, click) in bindings)
                if (card != null)
                    card.Clicked -= click;
            bindings.Clear();
            if (Chosen.Detail != null)
                Chosen.Detail.onClick.RemoveListener(PressDetail);
            presenter?.Dispose();
            presenter = null;
        }

        public void Render(PartyRosterState state)
        {
            for (int i = 0; i < Slots.Length && i < state.Party.Count; i++)
            {
                string id = state.Party[i];
                ShowSlot(
                    Slots[i],
                    Party.Find(id),
                    PartySession.LevelOf(Party, id),
                    ElementsOf(id),
                    i == state.SelectedSlot
                );
            }

            // 一覧はパーティにいない人を持っている順に並べ、最後に「パーティから外す」を置く。
            var bench = new Dictionary<string, int>();
            for (int i = 0; i < state.Bench.Count; i++)
                bench[state.Bench[i]] = i;
            foreach (var tile in Tiles)
            {
                if (tile.Card == null)
                    continue;
                bool shown = bench.TryGetValue(tile.Id, out int order);
                tile.Card.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                tile.Card.transform.SetSiblingIndex(order);
                tile.Card.Selected = tile.Id == state.SelectedBench;
                Set(tile.Level, PartySession.LevelOf(Party, tile.Id).ToString());
                ShowElements(tile.Elements, ElementsOf(tile.Id));
            }
            if (Remove != null)
            {
                Remove.gameObject.SetActive(true);
                Remove.transform.SetAsLastSibling();
                if (Remove.Group != null)
                    Remove.Group.alpha = state.CanRemove ? 1f : RemoveIdleAlpha;
            }
            ShowChosen(Party.Find(state.Chosen));
            // 読み込みを待っていたら、パーティの枠から一覧の順にめくる。一覧の伏せた場所のうち、
            // 人も「パーティから外す」も来ない場所のカードは、めくってから消す（外すの後ろへ回す）。
            var benchCards = Tiles
                .Select(tile => tile.Card)
                .Where(card => card != null && card.gameObject.activeSelf)
                .OrderBy(card => card.transform.GetSiblingIndex())
                .ToArray();
            int taken =
                benchCards.Length + (Remove != null && Remove.gameObject.activeSelf ? 1 : 0);
            var empty = cover.Covered
                ? Tiles
                    .Select(tile => tile.Card)
                    .Where(card => card != null && !card.gameObject.activeSelf)
                    .Take(CoverTiles - taken)
                    .Select(card => card.gameObject)
                    .ToArray()
                : Array.Empty<GameObject>();
            foreach (var card in empty)
                card.transform.SetAsLastSibling();
            cover.Reveal(
                Slots
                    .Select(slot => slot.Card)
                    .Concat(benchCards)
                    .Where(card => card != null)
                    .Select(card => card.gameObject),
                empty
            );
        }

        // 下の帯に選んでいる人を出す。選んでいなければ空けて、「装備・スキル」を隠す。
        private void ShowChosen(PartyMember member) =>
            ShowChosen(
                Chosen,
                member,
                member != null ? PartySession.LevelOf(Party, member.Id) : 0,
                member != null ? ElementsOf(member.Id) : null
            );

        /// <summary>Opens the person's tabs (stats, equipment, card skills); back there returns here.</summary>
        public void OpenDetail(string id) => TrainingSession.OpenPage(Guide, id, null);

        // その人が付けているカードの属性のアイコン（重ねずに、カードの順）。
        private IReadOnlyList<Sprite> ElementsOf(string id) =>
            PartySession.ElementIconsOf(Party, id);

        /// <summary>
        /// Shows a member (or an empty slot) on a party card. The generator calls it too, with the
        /// mock data's level and cards, so the prefab shows the mock party in the editor.
        /// </summary>
        public static void ShowSlot(
            AdventurerSlotWidget widget,
            PartyMember member,
            int level,
            IReadOnlyList<Sprite> elements,
            bool selected
        )
        {
            bool filled = member != null;
            if (widget.Card != null)
                widget.Card.Selected = selected;
            if (widget.Figure != null)
            {
                widget.Figure.gameObject.SetActive(filled && member.Art != null);
                if (filled)
                    PartyArt.Paint(widget.Figure, member, trim: true);
            }
            if (widget.Shadow != null)
                widget.Shadow.SetActive(filled);
            Set(widget.Name, filled ? member.Name : "");
            if (widget.LevelLine != null)
                widget.LevelLine.SetActive(filled);
            Set(widget.Level, filled ? level.ToString() : "");
            ShowElements(widget.Elements, filled ? elements : null);
            if (widget.Empty != null)
                widget.Empty.gameObject.SetActive(!filled);
        }

        /// <summary>
        /// Shows the person chosen (or nobody) on the band at the bottom. The generator calls it
        /// too, so the prefab shows the empty band.
        /// </summary>
        public static void ShowChosen(
            AdventurerChosenWidget widget,
            PartyMember member,
            int level,
            IReadOnlyList<Sprite> elements
        )
        {
            bool chosen = member != null;
            if (widget.Figure != null)
            {
                widget.Figure.gameObject.SetActive(chosen && member.Art != null);
                if (chosen)
                    PartyArt.Paint(widget.Figure, member, trim: true);
            }
            Set(widget.Name, chosen ? member.Name : "");
            if (widget.LevelLine != null)
                widget.LevelLine.SetActive(chosen);
            Set(widget.Level, chosen ? level.ToString() : "");
            ShowElements(widget.Elements, chosen ? elements : null);
            if (widget.Detail != null)
            {
                widget.Detail.interactable = chosen;
                widget.Detail.gameObject.SetActive(chosen);
            }
            if (widget.DetailGroup != null)
                widget.DetailGroup.alpha = 1f;
        }

        public static void ShowElements(Image[] icons, IReadOnlyList<Sprite> elements)
        {
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null)
                    continue;
                var icon = elements != null && i < elements.Count ? elements[i] : null;
                icons[i].sprite = icon;
                icons[i].gameObject.SetActive(icon != null);
            }
        }
    }
}
