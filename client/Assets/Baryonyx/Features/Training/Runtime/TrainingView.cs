using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Networking;
using Baryonyx.Party;
using Baryonyx.UI;
using Baryonyx.UI.Buttons;
using Baryonyx.UI.Cards;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.UI.UiText;

namespace Baryonyx.Training
{
    /// <summary>One passive on the page, baked by the generator.</summary>
    [Serializable]
    public sealed class TrainingSkillWidget
    {
        public RawImage Icon;

        // 未解放のときだけアイコンに重ねる錠前と、名前の行の右に出す解放するレベル。
        public GameObject Lock;
        public TMP_Text Name;
        public TMP_Text Description;
        public TMP_Text When;
    }

    /// <summary>
    /// One of the four deck cards on the page, laid out as the battle's card (70x98 dots at 3x):
    /// the element's frame, the illustration, the cost and element on its top corners, the type,
    /// name and kind on a dark band, and the effect below. A custom skill's card is a button.
    /// </summary>
    [Serializable]
    public sealed class TrainingCardWidget
    {
        // カスタムスキルだけ押せる。固有スキルはnull。
        public Button Button;
        public RawImage Frame;
        public RawImage Art;
        public GameObject Cost;

        // 0〜9の数字を横に並べた画像の、コストの数字の部分を出す。
        public RawImage CostDigit;
        public Image Element;
        public GameObject Band;
        public TMP_Text Type;
        public TMP_Text Name;
        public TMP_Text Kind;
        public TMP_Text Description;

        // 未解放の固有スキルに重ねる暗い幕・錠前と、解放するレベル（Lockの中）。
        public GameObject Lock;
        public TMP_Text When;
    }

    /// <summary>One equipment slot's tile on the page: the icon, slot, rarity, name and effect.</summary>
    [Serializable]
    public sealed class TrainingGearWidget
    {
        public Button Button;
        public Image Icon;

        // 「武器」「防具」「アクセサリー 1」。
        public TMP_Text Slot;
        public TMP_Text Name;
        public TMP_Text Stars;
        public TMP_Text Detail;

        // まだ付けられない枠（アクセサリー）に出す錠前と「準備中」。
        public GameObject Lock;
    }

    /// <summary>One stat's row in the level-up dialog: before, after and the gain.</summary>
    [Serializable]
    public sealed class TrainingDiffWidget
    {
        public TMP_Text Before;
        public TMP_Text After;
        public TMP_Text Gain;
    }

    /// <summary>
    /// The adventurer's page of the formation over the guide screen: the illustration with the
    /// battle sprite in its corner on the left, and on the right the name and level-up, the stats
    /// beside the passives, the four deck cards (two unique skills, two custom skills) and the
    /// five equipment slots, with the level-up dialog over them. A flick over the page changes
    /// the person; a custom skill or weapon or armour slot opens its change screen for the
    /// character, and back returns to the adventurers' list.
    /// TrainingAssets bakes every widget into the formation prefab, so the text reads in the
    /// editor; this view wires them up and drives its own presenter. When the app has a server
    /// (<see cref="PartySession.Source"/>), it reads the party and runes on opening and raises
    /// levels there; otherwise it uses the mock data. The equipment is read through
    /// <see cref="EquipmentSession.SourceOrLocal"/>. Back (the button and the device key)
    /// closes the dialog before going back.
    /// </summary>
    public sealed class TrainingView : MonoBehaviour, ITrainingView, IGuideBackHandler
    {
        public const string LoadingText = GuidePanelLoad.LoadingText;
        public const string LoadFailedText = GuidePanelLoad.LoadFailedText;
        public const string LoadFailedMessage = "キャラを取得できませんでした";

        // 足りないルーン・未解放のスキルの色。ほかの文字の色は UiPalette を使う。
        private static readonly Color Short = new(1f, 0.45f, 0.4f);
        private static readonly Color LockedIcon = new(0.25f, 0.25f, 0.3f, 1f);
        private static readonly Color LockedText = new(0.55f, 0.58f, 0.66f, 1f);

        // コストの数字の画像に、0〜9が横に並んでいる（戦闘のカードと同じ）。
        private const int CostDigits = 10;

        public PartyMockData Party;
        public TrainingMockData Data;

        // 通知は案内人の画面の通知の帯に出す。
        public GuideMenuView Guide;

        [Header("Detail")]
        // ページの上の左右のフリックで人を替える。
        public TrainingSwipe Swipe;
        public TMP_Text Name;
        public TMP_Text Level;
        public Image[] Elements = Array.Empty<Image>();

        // 左の縦長のイラストと、その右下に立たせる戦闘のドット絵（4倍）。
        public RawImage Illustration;
        public RawImage Figure;
        public Button LevelUp;
        public TMP_Text LevelUpLabel;
        public GameObject LevelUpCost;
        public TMP_Text LevelUpCostLabel;
        public TMP_Text[] Stats = Array.Empty<TMP_Text>();
        public TrainingSkillWidget[] Passives = Array.Empty<TrainingSkillWidget>();

        // 固有スキル2枚とカスタムスキル2枚。枠の画像は属性（CardElement の順）で選ぶ。
        public TrainingCardWidget[] Uniques = Array.Empty<TrainingCardWidget>();
        public TrainingCardWidget[] CardSlots = Array.Empty<TrainingCardWidget>();
        public Texture[] CardFrames = Array.Empty<Texture>();

        // 武器・防具・アクセサリー3つ。
        public TrainingGearWidget[] Gear = Array.Empty<TrainingGearWidget>();
        public Sprite WeaponIcon;
        public Sprite ArmorIcon;

        [Header("Level-up dialog")]
        public GameObject Dialog;
        public TMP_Text DialogName;
        public TMP_Text From;
        public TMP_Text To;
        public TMP_Text Count;
        public Button Less;
        public Button More;
        public Button Max;
        public Button Cancel;
        public Button Confirm;
        public TrainingDiffWidget[] Diffs = Array.Empty<TrainingDiffWidget>();
        public TMP_Text Learned;
        public GameObject NextBox;
        public RawImage NextIcon;
        public TMP_Text NextLevel;
        public TMP_Text NextName;
        public TMP_Text NextRemain;
        public TMP_Text Cost;
        public TMP_Text Balance;

        private TrainingPresenter presenter;
        private readonly GuidePanelLoad load = new();
        private readonly ButtonBindings bindings = new();

        public event Action PrevPressed;
        public event Action NextPressed;
        public event Action LevelUpPressed;
        public event Action LessPressed;
        public event Action MorePressed;
        public event Action MaxPressed;
        public event Action CancelPressed;
        public event Action ConfirmPressed;
        public event Action<int> GearPressed;
        public event Action<int> CardPressed;

        public TrainingPresenter Presenter => presenter;
        public string LastNotice { get; private set; } = "";

        // 実行中または直前の読み込み。テストで完了を待つために公開する。
        public Task LoadTask => load.Task;

        private void OnEnable()
        {
            if (Swipe != null)
                Swipe.Flicked += Flick;
            bindings.Bind(LevelUp, () => LevelUpPressed?.Invoke());
            bindings.Bind(Less, () => LessPressed?.Invoke());
            bindings.Bind(More, () => MorePressed?.Invoke());
            bindings.Bind(Max, () => MaxPressed?.Invoke());
            bindings.Bind(Cancel, () => CancelPressed?.Invoke());
            bindings.Bind(Confirm, () => ConfirmPressed?.Invoke());
            for (int i = 0; i < Gear.Length; i++)
            {
                int index = i;
                bindings.Bind(Gear[i].Button, () => GearPressed?.Invoke(index));
            }
            for (int i = 0; i < CardSlots.Length; i++)
            {
                int index = i;
                if (CardSlots[i].Button != null)
                    bindings.Bind(CardSlots[i].Button, () => CardPressed?.Invoke(index));
            }

            // 開くたびに、サーバーのレベル・所持ルーン・編成・装備（なければアプリを動かしている間のもの）で描き直す。
            presenter?.Dispose();
            presenter = null;
            if (Party == null || Data == null)
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
            IEquipmentSource equipment,
            CancellationToken token
        )
        {
            // 同じセッションを順に使う（ログインのやり直しを重ねない）。
            if (source != null)
                PartySession.Use(Party, await source.LoadAsync(token));
            var worn = await equipment.LoadAsync(token);
            if (token.IsCancellationRequested)
                return;
            presenter = Build(
                this,
                Party,
                Data,
                worn,
                source != null
                    ? (id, from, to, cancel) => LevelUpAsync(source, id, from, to, cancel)
                    : null
            );
        }

        // 左へのフリックで次の人、右へのフリックで前の人。
        private void Flick(int step)
        {
            if (step > 0)
                NextPressed?.Invoke();
            else if (step < 0)
                PrevPressed?.Invoke();
        }

        private void LoadFailed(Exception exception)
        {
            Debug.LogWarning("育成のキャラを取得できませんでした。" + exception.Message, this);
            Set(Name, LoadFailedText);
            ShowNotice(LoadFailedMessage);
        }

        // サーバーでレベルを上げる。断られたときは手元の表示がずれているので、読み直してから失敗を伝える。
        private async Task LevelUpAsync(
            IPartySource source,
            string id,
            int from,
            int to,
            CancellationToken token
        )
        {
            try
            {
                PartySession.Use(Party, await source.LevelUpAsync(id, from, to, token));
            }
            catch (ServerApiException exception) when (exception.StatusCode == 409)
            {
                try
                {
                    PartySession.Use(Party, await source.LoadAsync(token));
                }
                catch (Exception reload) when (reload is not OperationCanceledException)
                {
                    Debug.LogWarning("育成のキャラを読み直せませんでした。" + reload.Message, this);
                }
                throw;
            }
        }

        // 読み込むまで、仮データのキャラを本当のキャラとして見せない。
        private void ShowLoading()
        {
            Set(Name, LoadingText);
            Set(Level, "");
            foreach (var icon in Elements)
                if (icon != null)
                    icon.gameObject.SetActive(false);
            if (Illustration != null)
                Illustration.enabled = false;
            if (Figure != null)
                Figure.enabled = false;
            if (LevelUp != null)
                LevelUp.interactable = false;
            if (LevelUpCost != null)
                LevelUpCost.SetActive(false);
            foreach (var stat in Stats)
                Set(stat, "");
            foreach (var skill in Passives)
                ShowSkill(skill, null);
            foreach (var gear in Gear)
                ShowGear(gear, null, null);
            foreach (var card in Uniques.Concat(CardSlots))
                ShowCard(card, null, null);
            if (Dialog != null)
                Dialog.SetActive(false);
        }

        private void OnDisable()
        {
            if (Swipe != null)
                Swipe.Flicked -= Flick;
            load.Cancel();
            bindings.Clear();
            presenter?.Dispose();
            presenter = null;
        }

        /// <summary>
        /// A presenter over the running app's party, runes and <paramref name="equipment"/>,
        /// raising levels with <paramref name="levelUp"/> when the app has a server. The
        /// generator uses it too, so the prefab shows the first character in the editor.
        /// </summary>
        public static TrainingPresenter Build(
            ITrainingView view,
            PartyMockData party,
            TrainingMockData data,
            EquipmentState equipment,
            Func<string, int, int, CancellationToken, Task> levelUp = null
        ) =>
            new(
                view,
                TrainingRoster.From(PartySession.Formation(party), data),
                new TrainingSessionStore(party, data),
                party.IconOf,
                levelUp,
                id => GearOf(equipment, id),
                party.ArtOf,
                Describe(new CardLoadoutSessionStore(party, data))
            );

        // スキルの説明の数字は、その人の今のLvのステータスと今日のACTから出す（スキルの付け替えと同じ）。
        private static Func<string, CardSkill, string> Describe(ICardLoadoutStore cards) =>
            (id, card) => CardText.Description(card, stat => cards.StatOf(id, stat), cards.Act);

        // 武器と防具に付けている装備。
        public static IReadOnlyList<TrainingGearState> GearOf(
            EquipmentState equipment,
            string id
        ) =>
            new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor }
                .Select(slot =>
                {
                    var item = equipment?.Find(equipment.ItemIn(id, slot));
                    var definition = EquipmentCatalog.Find(item?.EquipmentId);
                    return TrainingGearState.Worn(
                        EquipmentCatalog.NameOf(slot),
                        definition?.Name,
                        definition != null ? EquipmentCatalog.Stars(definition.Rarity) : null,
                        definition?.Detail
                    );
                })
                .ToArray();

        // 個別の画面で戻ると、冒険者の一覧へ戻る（同じ人を選んだまま）。重ねた画面は先に閉じる。
        public bool HandleBack()
        {
            if (presenter != null && presenter.Back())
                return true;
            return Guide != null
                && Guide.HasItem(PartySession.GuideItemKey)
                && Guide.OpenItem(PartySession.GuideItemKey);
        }

        public void Render(TrainingState state)
        {
            RenderDetail(state);
            RenderDialog(state);
        }

        private void RenderDetail(TrainingState state)
        {
            Set(Name, state.Name);
            Set(
                Level,
                state.Member != null ? state.Level.ToString(CultureInfo.InvariantCulture) : ""
            );
            for (int i = 0; i < Elements.Length; i++)
            {
                var icon = i < state.Elements.Count ? state.Elements[i] : null;
                Elements[i].sprite = icon;
                Elements[i].gameObject.SetActive(icon != null);
            }
            if (Illustration != null)
                PartyArt.Cover(Illustration, state.Member?.Illustration);
            if (Figure != null)
            {
                Figure.enabled = state.Member != null;
                if (state.Member != null)
                    PartyArt.Paint(Figure, state.Member, trim: false);
            }

            if (LevelUp != null)
                LevelUp.interactable = state.Member != null && !state.Maxed;
            Set(LevelUpLabel, state.Maxed && state.Member != null ? "レベル上限" : "レベルアップ");
            if (LevelUpCost != null)
                LevelUpCost.SetActive(!state.Maxed);
            Set(LevelUpCostLabel, Number(state.NextCost));

            ShowStats(Stats, state.Member != null ? state.Stats : (CharacterStats?)null);
            for (int i = 0; i < Passives.Length; i++)
                ShowSkill(Passives[i], i < state.Passives.Count ? state.Passives[i] : null);
            ShowCards(Uniques, state.Uniques);
            ShowCards(CardSlots, state.Cards);
            for (int i = 0; i < Gear.Length; i++)
                ShowGear(
                    Gear[i],
                    i < state.Gear.Count ? state.Gear[i] : null,
                    i == 0 ? WeaponIcon
                        : i == 1 ? ArmorIcon
                        : null
                );
        }

        private void ShowCards(TrainingCardWidget[] widgets, IReadOnlyList<TrainingCardState> cards)
        {
            for (int i = 0; i < widgets.Length; i++)
            {
                var card = i < cards.Count ? cards[i] : null;
                ShowCard(widgets[i], card, FrameOf(card));
            }
        }

        private Texture FrameOf(TrainingCardState card)
        {
            int element = (int)(card?.Element ?? CardElement.None);
            return element < CardFrames.Length ? CardFrames[element] : null;
        }

        /// <summary>
        /// The eight stats in <see cref="CharacterStats"/> order, or empty while nothing is
        /// shown. The other formation screens show the stats with it too.
        /// </summary>
        public static void ShowStats(IReadOnlyList<TMP_Text> labels, CharacterStats? stats)
        {
            for (int i = 0; i < labels.Count && i < CharacterStats.Count; i++)
                Set(
                    labels[i],
                    stats.HasValue ? stats.Value[i].ToString(CultureInfo.InvariantCulture) : ""
                );
        }

        private void RenderDialog(TrainingState state)
        {
            if (Dialog != null)
                Dialog.SetActive(state.DialogOpen);
            Set(DialogName, state.Name);
            Set(From, state.Level.ToString(CultureInfo.InvariantCulture));
            Set(To, state.Target.ToString(CultureInfo.InvariantCulture));
            Set(Count, state.Count.ToString(CultureInfo.InvariantCulture));
            if (Less != null)
                Less.interactable = state.CanLess;
            if (More != null)
                More.interactable = state.CanMore;
            if (Confirm != null)
                Confirm.interactable = state.CanConfirm;
            for (int i = 0; i < Diffs.Length && i < CharacterStats.Count; i++)
            {
                int before = state.Stats[i];
                int after = state.TargetStats[i];
                Set(Diffs[i].Before, before.ToString(CultureInfo.InvariantCulture));
                Set(Diffs[i].After, after.ToString(CultureInfo.InvariantCulture));
                Set(Diffs[i].Gain, $"+{after - before}");
            }
            Set(
                Learned,
                state.Learned.Count == 0
                    ? "なし"
                    : string.Join("　", state.Learned.Select(skill => $"「{skill.Name}」"))
            );
            var next = state.NextUnlock;
            if (NextBox != null)
                NextBox.SetActive(next != null);
            if (next != null)
            {
                if (NextIcon != null)
                {
                    NextIcon.texture = next.Icon;
                    NextIcon.enabled = next.Icon != null;
                }
                Set(NextLevel, $"Lv {next.UnlockLevel}・{next.Type}");
                Set(NextName, next.Name);
                Set(NextRemain, $"あと{next.UnlockLevel - state.Target}レベル");
            }
            Set(Cost, Number(state.Cost));
            if (Cost != null)
                Cost.color = state.CanAfford ? UiPalette.TextMain : Short;
            Set(
                Balance,
                state.CanAfford
                    ? $"所持 {Number(state.Runes)} → のこり {Number(state.Remaining)}"
                    : $"ルーンが {Number(-state.Remaining)} 足りません"
            );
            if (Balance != null)
                Balance.color = state.CanAfford ? UiPalette.TextSub : Short;
        }

        private static void ShowSkill(TrainingSkillWidget widget, TrainingSkillState skill)
        {
            bool open = skill == null || skill.Unlocked;
            if (widget.Icon != null)
            {
                widget.Icon.texture = skill?.Icon;
                widget.Icon.enabled = skill?.Icon != null;
                widget.Icon.color = open ? Color.white : LockedIcon;
            }
            if (widget.Lock != null)
                widget.Lock.SetActive(!open);
            Set(widget.Name, skill?.Name);
            Set(widget.Description, skill?.Description);
            // 未解放のパッシブは、名前の行の右に解放するレベルを出す。
            Set(widget.When, open ? "" : $"Lv {skill.UnlockLevel}で解放");
            if (widget.Name != null)
                widget.Name.color = open ? UiPalette.TextMain : LockedText;
            if (widget.Description != null)
                widget.Description.color = open ? UiPalette.TextSub : LockedText;
        }

        /// <summary>
        /// One deck card: <paramref name="frame"/> is the element's frame. An empty custom slot
        /// shows only its frame and "空き"; a unique skill not unlocked yet is veiled with its level.
        /// </summary>
        public static void ShowCard(
            TrainingCardWidget widget,
            TrainingCardState card,
            Texture frame
        )
        {
            bool filled = card != null && card.Filled;
            if (widget.Frame != null)
            {
                widget.Frame.texture = frame;
                widget.Frame.enabled = frame != null;
                widget.Frame.color = filled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }
            if (widget.Art != null)
            {
                widget.Art.texture = card?.Art;
                widget.Art.enabled = filled && card.Art != null;
            }
            if (widget.Cost != null)
                widget.Cost.SetActive(filled && card.Cost >= 0 && card.Cost < CostDigits);
            if (widget.CostDigit != null && filled)
                widget.CostDigit.uvRect = new Rect(
                    card.Cost / (float)CostDigits,
                    0f,
                    1f / CostDigits,
                    1f
                );
            if (widget.Element != null)
            {
                widget.Element.sprite = card?.Icon;
                widget.Element.gameObject.SetActive(filled && card.Icon != null);
            }
            if (widget.Band != null)
                widget.Band.SetActive(filled);
            Set(widget.Type, filled ? card.Type : "");
            Set(widget.Name, card?.Name);
            Set(widget.Kind, filled ? card.Kind : "");
            Set(widget.Description, filled ? card.Description : "");
            bool locked = filled && card.Locked;
            if (widget.Lock != null)
                widget.Lock.SetActive(locked);
            Set(widget.When, locked ? $"Lv {card.UnlockLevel}で解放" : "");
        }

        /// <summary>One equipment slot's tile; the generator calls it too.</summary>
        public static void ShowGear(TrainingGearWidget widget, TrainingGearState gear, Sprite icon)
        {
            bool locked = gear != null && gear.Locked;
            if (widget.Icon != null)
            {
                widget.Icon.sprite = icon;
                widget.Icon.enabled = icon != null && !locked;
                widget.Icon.color =
                    gear != null && gear.Filled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
            Set(
                widget.Name,
                locked ? ""
                    : gear == null ? ""
                    : gear.Filled ? gear.Name
                    : "なし"
            );
            Set(widget.Stars, gear != null && gear.Filled ? gear.Stars : "");
            Set(widget.Detail, gear != null && gear.Filled ? gear.Detail : "");
            if (widget.Lock != null)
                widget.Lock.SetActive(locked);
            if (widget.Button != null)
                widget.Button.interactable = gear != null && !locked;
        }

        public void ShowNotice(string message)
        {
            LastNotice = message ?? "";
            if (Guide != null)
                Guide.ShowToast(message);
        }

        /// <summary>
        /// Moves to the equipment's change screen with this character and slot chosen. Its back
        /// comes back here, where the same character is still on screen.
        /// </summary>
        public void OpenGear(string id, int slot)
        {
            if (Guide == null || !Guide.HasItem(EquipmentSession.GuideItemKey))
                return;
            EquipmentSession.Selected = id;
            EquipmentSession.OpenSlot = (EquipmentSlot)slot;
            Guide.OpenItem(EquipmentSession.GuideItemKey);
        }

        /// <summary>Moves to the card skills' change screen with this character and slot chosen.</summary>
        public void OpenCards(string id, int slot)
        {
            if (Guide == null || !Guide.HasItem(CardLoadoutSession.GuideItemKey))
                return;
            CardLoadoutSession.Selected = id;
            CardLoadoutSession.OpenSlot = slot;
            Guide.OpenItem(CardLoadoutSession.GuideItemKey);
        }

        private static string Number(long value) =>
            value.ToString("#,0", CultureInfo.InvariantCulture);
    }
}
