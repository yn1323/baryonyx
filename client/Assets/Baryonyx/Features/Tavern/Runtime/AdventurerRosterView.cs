using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Equipment;
using Baryonyx.Party;
using Baryonyx.Training;
using Baryonyx.UI.Buttons;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.UI.UiText;

namespace Baryonyx.Tavern
{
    /// <summary>One of the four party slots at the top of the list, baked by the generator.</summary>
    [Serializable]
    public sealed class AdventurerSlotWidget
    {
        public Button Button;
        public GameObject Selected;

        // 立ち姿（周りの余白を切って3倍）と足元の影。空いた枠では隠す。
        public RawImage Figure;
        public GameObject Shadow;
        public TMP_Text Name;
        public TMP_Text Level;
        public Image[] Elements = Array.Empty<Image>();

        // 空いているときだけ出す「空き」。
        public TMP_Text Empty;

        // 控えの人と入れ替える相手を選んでいる間だけ出す「⇄ 入れ替え」。
        public GameObject Swap;
    }

    /// <summary>One bench character's tile, baked by the generator for every mock character.</summary>
    [Serializable]
    public sealed class AdventurerTileWidget
    {
        public string Id = "";
        public Button Button;
        public GameObject Selected;
        public TMP_Text Level;
        public Image[] Elements = Array.Empty<Image>();

        // 入れ替える相手を選んでいる間、選んでいない控えを暗くする。
        public CanvasGroup Group;
    }

    /// <summary>
    /// The formation's adventurers over the guide screen: the chosen person's sample on the left
    /// (name, level, figure, stats, equipment and card skills) with the button to their page and
    /// the button that changes the party, and every owned character on the right, the party's
    /// four slots first and the bench below. AdventurerRosterAssets bakes the parts into the
    /// formation prefab, so they read in the editor; this view wires them up and drives the
    /// party's roster presenter. When the app has a server (<see cref="PartySession.Source"/>),
    /// it reads the party on opening and saves every change there; otherwise it uses the mock
    /// data. Changes are told on the guide screen's notice band.
    /// </summary>
    public sealed class AdventurerRosterView : MonoBehaviour, IPartyRosterView
    {
        public const string LoadingText = GuidePanelLoad.LoadingText;
        public const string LoadFailedText = GuidePanelLoad.LoadFailedText;
        public const string LoadFailedMessage = "編成を取得できませんでした";

        // まだ解放していない固有スキルの挿絵の色。
        private static readonly Color LockedArt = new(0.25f, 0.25f, 0.3f, 1f);

        // 入れ替える相手を選んでいる間の、選んでいない控えの濃さ。
        private const float DimAlpha = 0.45f;

        public PartyMockData Party;
        public TrainingMockData Training;

        // 通知は案内人の画面の通知の帯に出す。
        public GuideMenuView Guide;

        [Header("Chosen")]
        public TMP_Text Name;
        public TMP_Text Level;
        public Image[] Elements = Array.Empty<Image>();
        public RawImage Figure;
        public TMP_Text[] Stats = Array.Empty<TMP_Text>();

        // 武器・防具のアイコン。付けていなければ薄くする（アクセサリーは準備中で、押せない枠だけ置く）。
        public Image[] Gear = Array.Empty<Image>();
        public Sprite WeaponIcon;
        public Sprite ArmorIcon;

        // デッキに入る4枚の挿絵：固有スキル2枚（未解放は暗くする）と、カスタムスキル2枚。
        public RawImage[] Cards = Array.Empty<RawImage>();
        public Button Open;
        public Button Action;
        public TMP_Text ActionLabel;

        [Header("Roster")]
        public TMP_Text Owned;
        public AdventurerSlotWidget[] Slots = Array.Empty<AdventurerSlotWidget>();
        public AdventurerTileWidget[] Tiles = Array.Empty<AdventurerTileWidget>();
        public ScrollRect List;

        private PartyRosterPresenter presenter;
        private EquipmentState equipment;
        private readonly GuidePanelLoad load = new();
        private readonly ButtonBindings bindings = new();

        public event Action<int> SlotPressed;
        public event Action<string> BenchPressed;
        public event Action ActionPressed;
        public event Action OpenPressed;

        public PartyRosterPresenter Presenter => presenter;
        public string LastNotice { get; private set; } = "";

        // 実行中または直前の読み込み。テストで完了を待つために公開する。
        public Task LoadTask => load.Task;

        private void OnEnable()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                bindings.Bind(Slots[i].Button, () => SlotPressed?.Invoke(index));
            }
            foreach (var tile in Tiles)
            {
                string id = tile.Id;
                bindings.Bind(tile.Button, () => BenchPressed?.Invoke(id));
            }
            bindings.Bind(Action, () => ActionPressed?.Invoke());
            bindings.Bind(Open, () => OpenPressed?.Invoke());

            // 開くたびに、サーバーの編成と装備（なければアプリを動かしている間のもの）で描き直す。
            presenter?.Dispose();
            presenter = null;
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            if (Party == null)
                return;
            var source = PartySession.Source;
            if (source != null || EquipmentSession.Source != null)
                ShowLoading();
            load.Start(
                token => LoadAsync(source, EquipmentSession.SourceOrLocal, token),
                LoadFailed
            );
        }

        private async Task LoadAsync(
            IPartySource source,
            IEquipmentSource gear,
            CancellationToken token
        )
        {
            // 同じセッションを順に使う（ログインのやり直しを重ねない）。
            var formation =
                source != null
                    ? PartySession.Use(Party, await source.LoadAsync(token))
                    : PartySession.Formation(Party);
            equipment = await gear.LoadAsync(token);
            if (token.IsCancellationRequested)
                return;
            presenter = new PartyRosterPresenter(
                this,
                formation,
                PartySession.Selected,
                source != null ? (slot, id, cancel) => SaveAsync(source, slot, id, cancel) : null,
                id => PartySession.Selected = id
            );
        }

        private void LoadFailed(Exception exception)
        {
            Debug.LogWarning("編成を取得できませんでした。" + exception.Message, this);
            Set(Owned, LoadFailedText);
            ShowNotice(LoadFailedMessage);
        }

        private async Task<PartyFormation> SaveAsync(
            IPartySource source,
            int slot,
            string id,
            CancellationToken token
        ) => PartySession.Use(Party, await source.SetSlotAsync(slot, id, token));

        // 読み込むまで、仮データの編成を本当の編成として見せない。
        private void ShowLoading()
        {
            Set(Owned, LoadingText);
            foreach (var slot in Slots)
            {
                ShowSlot(slot, Party, null, 1, null, false, false);
                if (slot.Empty != null)
                    slot.Empty.gameObject.SetActive(false);
            }
            foreach (var tile in Tiles)
                if (tile.Button != null)
                    tile.Button.gameObject.SetActive(false);
            ShowChosen(null);
            foreach (var button in new[] { Open, Action })
                if (button != null)
                    button.interactable = false;
        }

        private void OnDisable()
        {
            load.Cancel();
            bindings.Clear();
            presenter?.Dispose();
            presenter = null;
        }

        public void Render(PartyRosterState state)
        {
            Set(Owned, state.OwnedText);
            for (int i = 0; i < Slots.Length && i < state.Party.Count; i++)
            {
                string id = state.Party[i];
                ShowSlot(
                    Slots[i],
                    Party,
                    Party.Find(id),
                    PartySession.LevelOf(Party, id),
                    ElementsOf(id),
                    id != null && id == state.Selected,
                    state.Swapping
                );
            }

            // 控えは持っている順に並べる。
            var bench = new Dictionary<string, int>();
            for (int i = 0; i < state.Bench.Count; i++)
                bench[state.Bench[i]] = i;
            foreach (var tile in Tiles)
            {
                if (tile.Button == null)
                    continue;
                bool shown = bench.TryGetValue(tile.Id, out int order);
                tile.Button.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                tile.Button.transform.SetSiblingIndex(order);
                bool chosen = tile.Id == state.Selected;
                if (tile.Selected != null)
                    tile.Selected.SetActive(chosen);
                Set(tile.Level, PartyArt.LevelText(PartySession.LevelOf(Party, tile.Id)));
                ShowElements(tile.Elements, ElementsOf(tile.Id));
                if (tile.Group != null)
                    tile.Group.alpha = state.Swapping && !chosen ? DimAlpha : 1f;
            }

            ShowChosen(state.Selected);
            if (Open != null)
                Open.interactable = state.CanOpen;
            if (Action != null)
            {
                Action.gameObject.SetActive(state.Action != PartyRosterAction.None);
                Action.interactable = state.CanAct;
            }
            Set(ActionLabel, state.ActionLabel);
        }

        public void ShowNotice(string message)
        {
            LastNotice = message ?? "";
            if (Guide != null)
                Guide.ShowToast(message);
        }

        /// <summary>Opens the chosen person's page (equipment, card skills, training).</summary>
        public void OpenDetail(string id)
        {
            if (Guide == null || !Guide.HasItem(TrainingSession.GuideItemKey))
                return;
            PartySession.Selected = id;
            Guide.OpenItem(TrainingSession.GuideItemKey);
        }

        // 左の見本：名前・Lv・属性・立ち姿・ステータス・装備とスキルのアイコン。
        private void ShowChosen(string id)
        {
            var member = Party != null ? Party.Find(id) : null;
            int level = PartySession.LevelOf(Party, id);
            Set(Name, member?.Name ?? (presenter == null ? LoadingText : ""));
            Set(Level, member != null ? level.ToString() : "");
            ShowElements(Elements, member != null ? ElementsOf(id) : Array.Empty<Sprite>());
            if (Figure != null)
            {
                Figure.enabled = member != null && member.Art != null;
                if (Figure.enabled)
                    PartyArt.Paint(Figure, member, trim: true);
            }
            var growth = member != null && Training != null ? Training.Find(id) : null;
            TrainingView.ShowStats(Stats, growth?.StatsAt(level));
            for (int i = 0; i < Gear.Length; i++)
            {
                if (Gear[i] == null)
                    continue;
                var slot = (EquipmentSlot)i;
                bool worn = member != null && equipment?.ItemIn(id, slot) != null;
                Gear[i].sprite = slot == EquipmentSlot.Weapon ? WeaponIcon : ArmorIcon;
                Gear[i].enabled = member != null;
                Gear[i].color = worn ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            }
            var uniques = growth != null ? growth.Uniques : Array.Empty<TrainingSkill>();
            var cards = member != null ? PartySession.CardsOf(Party, id) : Array.Empty<string>();
            for (int i = 0; i < Cards.Length; i++)
            {
                if (Cards[i] == null)
                    continue;
                int custom = i - uniques.Length;
                var art =
                    i < uniques.Length ? uniques[i].Icon
                    : custom < cards.Count ? Party.ArtOf(cards[custom])
                    : null;
                Cards[i].texture = art;
                Cards[i].enabled = art != null;
                bool locked = i < uniques.Length && uniques[i].UnlockLevel > level;
                Cards[i].color = locked ? LockedArt : Color.white;
            }
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
        /// Shows a member (or an empty slot) in a party slot. The generator calls it too, with
        /// the mock data's level and cards, so the prefab shows the mock party in the editor.
        /// </summary>
        public static void ShowSlot(
            AdventurerSlotWidget widget,
            PartyMockData data,
            PartyMember member,
            int level,
            IReadOnlyList<Sprite> elements,
            bool selected,
            bool swapping
        )
        {
            bool filled = member != null;
            if (widget.Selected != null)
                widget.Selected.SetActive(selected);
            if (widget.Figure != null)
            {
                widget.Figure.gameObject.SetActive(filled && member.Art != null);
                if (filled)
                    PartyArt.Paint(widget.Figure, member, trim: true);
            }
            if (widget.Shadow != null)
                widget.Shadow.SetActive(filled);
            Set(widget.Name, filled ? member.Name : "");
            Set(widget.Level, filled ? PartyArt.LevelText(level) : "");
            ShowElements(widget.Elements, filled ? elements : null);
            if (widget.Empty != null)
                widget.Empty.gameObject.SetActive(!filled);
            if (widget.Swap != null)
                widget.Swap.SetActive(swapping);
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
