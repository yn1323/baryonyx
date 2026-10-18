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
    /// list ending with "remove from the party". AdventurerRosterAssets bakes the cards into the
    /// formation prefab, so they read in the editor; this view wires them up and drives the
    /// party's roster presenter. Holding a character opens their page. When the app has a server
    /// (<see cref="PartySession.Source"/>), it reads the party on opening and saves every change
    /// there; otherwise it uses the mock data. Changes are told on the guide screen's notice band.
    /// </summary>
    public sealed class AdventurerRosterView : MonoBehaviour, IPartyRosterView, IEditModePreview
    {
        public const string LoadingText = GuidePanelLoad.LoadingText;
        public const string LoadFailedText = GuidePanelLoad.LoadFailedText;
        public const string LoadFailedMessage = "編成を取得できませんでした";

        // 外せる人を選んでいないときの「パーティから外す」の濃さ。
        private const float RemoveIdleAlpha = 0.5f;

        public PartyMockData Party;

        // 通知は案内人の画面の通知の帯に出す。
        public GuideMenuView Guide;

        public AdventurerSlotWidget[] Slots = Array.Empty<AdventurerSlotWidget>();
        public AdventurerTileWidget[] Tiles = Array.Empty<AdventurerTileWidget>();

        // 一覧の最後に置く「パーティから外す」。
        public GlassCard Remove;
        public ScrollRect List;

        // 読み込み中と、読めなかったときだけ一覧の面に出す文字。
        public TMP_Text Status;

        private PartyRosterPresenter presenter;
        private PartyRosterState state;
        private readonly GuidePanelLoad load = new();
        private readonly List<(GlassCard Card, Action Click, Action Hold)> bindings = new();

        public event Action<int> SlotPressed;
        public event Action<string> BenchPressed;
        public event Action RemovePressed;
        public event Action<string> Held;

        public PartyRosterPresenter Presenter => presenter;
        public string LastNotice { get; private set; } = "";

        // 実行中または直前の読み込み。テストで完了を待つために公開する。
        public Task LoadTask => load.Task;

        private void OnEnable()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                Bind(
                    Slots[i].Card,
                    () => SlotPressed?.Invoke(index),
                    () =>
                    {
                        string id =
                            state != null && index < state.Party.Count ? state.Party[index] : null;
                        if (id != null)
                            Held?.Invoke(id);
                    }
                );
            }
            foreach (var tile in Tiles)
            {
                string id = tile.Id;
                Bind(tile.Card, () => BenchPressed?.Invoke(id), () => Held?.Invoke(id));
            }
            Bind(Remove, () => RemovePressed?.Invoke(), null);

            // 開くたびに、サーバーの編成（なければアプリを動かしている間のもの）で描き直す。
            presenter?.Dispose();
            presenter = null;
            state = null;
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            if (Party == null)
                return;
            var source = PartySession.Source;
            if (source != null)
                ShowLoading();
            load.Start(token => LoadAsync(source, token), LoadFailed);
        }

        private void Bind(GlassCard card, Action click, Action hold)
        {
            if (card == null)
                return;
            card.Clicked += click;
            if (hold != null)
                card.Held += hold;
            bindings.Add((card, click, hold));
        }

        private async Task LoadAsync(IPartySource source, CancellationToken token)
        {
            // 同じセッションを順に使う（ログインのやり直しを重ねない）。
            var formation =
                source != null
                    ? PartySession.Use(Party, await source.LoadAsync(token))
                    : PartySession.Formation(Party);
            if (token.IsCancellationRequested)
                return;
            Set(Status, "");
            presenter = new PartyRosterPresenter(
                this,
                formation,
                source != null ? (slot, id, cancel) => SaveAsync(source, slot, id, cancel) : null
            );
        }

        private void LoadFailed(Exception exception)
        {
            Debug.LogWarning("編成を取得できませんでした。" + exception.Message, this);
            ShowLoadFailed();
            ShowNotice(LoadFailedMessage);
        }

        private void ShowLoadFailed() => Set(Status, LoadFailedText);

        private async Task<PartyFormation> SaveAsync(
            IPartySource source,
            int slot,
            string id,
            CancellationToken token
        ) => PartySession.Use(Party, await source.SetSlotAsync(slot, id, token));

        // 読み込むまで、仮データの編成を本当の編成として見せない。
        private void ShowLoading()
        {
            Set(Status, LoadingText);
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
            load.Cancel();
            foreach (var (card, click, hold) in bindings)
            {
                if (card == null)
                    continue;
                card.Clicked -= click;
                if (hold != null)
                    card.Held -= hold;
            }
            bindings.Clear();
            presenter?.Dispose();
            presenter = null;
        }

        public void Render(PartyRosterState next)
        {
            state = next;
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
        }

        public void ShowNotice(string message)
        {
            LastNotice = message ?? "";
            if (Guide != null)
                Guide.ShowToast(message);
        }

        /// <summary>Opens the person's page (equipment, card skills, training).</summary>
        public void OpenDetail(string id)
        {
            if (Guide == null || !Guide.HasItem(TrainingSession.GuideItemKey))
                return;
            PartySession.Selected = id;
            // 個別の画面の「もどる」で、この一覧へ戻る。
            TrainingSession.ReturnKey = null;
            Guide.OpenItem(TrainingSession.GuideItemKey);
        }

        // その人が付けているカードの属性のアイコン（重ねずに、カードの順）。
        private IReadOnlyList<Sprite> ElementsOf(string id) =>
            Party == null || id == null
                ? Array.Empty<Sprite>()
                : PartySession
                    .CardsOf(Party, id)
                    .Select(Party.IconOf)
                    .Where(icon => icon != null)
                    .Distinct()
                    .ToArray();

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
