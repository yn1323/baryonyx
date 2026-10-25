using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Economy;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.Party;
using Baryonyx.UI;
using Baryonyx.UI.Buttons;
using Baryonyx.UI.Cards;
using Baryonyx.UI.EditModePreview;
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

        // 効果の文を出さない小さなカードの、枠の下端（Frame は帯の下までを出す）。大きなカードはnull。
        public RawImage FrameEdge;

        // 枠の品質の光（Aは金、Sは虹）。
        public CardFrameFoil FrameFoil;
        public RawImage Art;

        // 挿絵の上に重ねるキラキラ（レア度で強くなる）。
        public CardFoil Foil;
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

        // アイコンに重ねる品質のキラキラ（S・Aだけ光る）。武器・防具の枠だけにある。
        public CardFoil Foil;

        // 「武器」「防具」「アクセサリー 1」。
        public TMP_Text Slot;
        public TMP_Text Name;
        public TMP_Text Stars;
        public TMP_Text Detail;

        // まだ開いていない枠（アクセサリー）に出す錠前と、解放するLv（「Lv 20で解放」）。
        public GameObject Lock;
        public TMP_Text When;
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
    /// five equipment slots, with the level-up and bond dialogs over them and the bond's stage
    /// lighting up after it deepens (<see cref="TrainingBondCelebration"/>). A flick over the page changes
    /// the person; a custom skill or weapon or armour slot opens its change screen for the
    /// character, and back returns to the adventurers' list.
    /// TrainingAssets bakes every widget into the formation prefab, so the text reads in the
    /// editor; this view wires them up and drives its own presenter. When the app has a server
    /// (<see cref="PartySession.Source"/>), it reads the party and runes on opening and raises
    /// levels there; otherwise it uses the mock data. The equipment is read through
    /// <see cref="EquipmentSession.SourceOrLocal"/>. Back (the button and the device key)
    /// closes the dialog before going back.
    /// </summary>
    public sealed class TrainingView
        : GuidePanel,
            ITrainingView,
            IGuideBackHandler,
            IGuideBreadcrumb,
            IEditModePreview
    {
        public const string LoadFailedMessage = "キャラを取得できませんでした";

        // 足りないルーン・未解放のスキルの色。ほかの文字の色は UiPalette を使う。
        private static readonly Color Short = new(1f, 0.45f, 0.4f);
        private static readonly Color LockedIcon = new(0.25f, 0.25f, 0.3f, 1f);
        private static readonly Color LockedText = new(0.55f, 0.58f, 0.66f, 1f);

        // コストの数字の画像に、0〜9が横に並んでいる（戦闘のカードと同じ）。
        private const int CostDigits = 10;

        public PartyMockData Party;
        public TrainingMockData Data;

        [Header("Detail")]
        // ページの上の左右のフリックで人を替える。
        public GuideSwipe Swipe;
        public TMP_Text Name;
        public TMP_Text Level;
        public Image[] Elements = Array.Empty<Image>();

        // 名前・Lv・属性の行と、その行に使える幅。長い名前で「絆」に届くときは、行ごと縮めて収める。
        public RectTransform Header;
        public float HeaderWidth;

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

        // 装備の種類ごとの絵。絵のない種類と空いた枠は、枠ごとのアイコンを出す。
        public ItemArt ItemArt;

        [Header("Level-up dialog")]
        public GameObject Dialog;
        public TMP_Text From;
        public TMP_Text To;
        public Button Less;
        public Button More;
        public Button Max;
        public Button Cancel;
        public Button Confirm;
        public TrainingDiffWidget[] Diffs = Array.Empty<TrainingDiffWidget>();

        // 使うルーン。足りないときは赤くする。
        public TMP_Text Cost;

        [Header("Bond")]
        // 右上の「レベルアップ」の左の「絆」と、その中の6つのしるし（灯った段は金）。
        public Button Bond;
        public Image[] BondPips = Array.Empty<Image>();
        public Sprite BondLit;
        public Sprite BondDark;

        [Header("Bond dialog")]
        public GameObject BondDialog;
        public TMP_Text BondFrom;
        public GameObject BondNext;
        public TMP_Text BondTo;

        // 6つの段の石と、次に灯る石を囲む光。
        public Image[] BondStones = Array.Empty<Image>();
        public RectTransform BondGlow;
        public TrainingDiffWidget[] BondDiffs = Array.Empty<TrainingDiffWidget>();
        public Button BondCancel;
        public Button BondConfirm;
        public TMP_Text BondConfirmLabel;

        // 「絆を深める」に添える、その人の絆のしるし（ドット絵）と「持っている数 / 使う数」。
        public GameObject BondCost;
        public RawImage BondToken;
        public TMP_Text BondTokens;

        [Header("Bond celebration")]
        public TrainingBondCelebration Celebration;

        private TrainingPresenter presenter;

        // パンくずに出す、表示している人の名前。
        private string person;
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
        public event Action<int> UniquePressed;
        public event Action BondPressed;
        public event Action BondCancelPressed;
        public event Action BondConfirmPressed;
        public event Action CelebrationClosed;

        public TrainingPresenter Presenter => presenter;

        private void OnEnable()
        {
            person = null;
            if (Swipe != null)
                Swipe.Flicked += Flick;
            bindings.Bind(LevelUp, () => LevelUpPressed?.Invoke());
            bindings.Bind(Less, () => LessPressed?.Invoke());
            bindings.Bind(More, () => MorePressed?.Invoke());
            bindings.Bind(Max, () => MaxPressed?.Invoke());
            bindings.Bind(Cancel, () => CancelPressed?.Invoke());
            bindings.Bind(Confirm, () => ConfirmPressed?.Invoke());
            bindings.Bind(Bond, () => BondPressed?.Invoke());
            bindings.Bind(BondCancel, () => BondCancelPressed?.Invoke());
            bindings.Bind(BondConfirm, () => BondConfirmPressed?.Invoke());
            if (Celebration != null)
                Celebration.Closed += CloseCelebration;
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
            for (int i = 0; i < Uniques.Length; i++)
            {
                int index = i;
                if (Uniques[i].Button != null)
                    bindings.Bind(Uniques[i].Button, () => UniquePressed?.Invoke(index));
            }

            // 開くたびに、サーバーのレベル・所持ルーン・編成・装備（なければアプリを動かしている間のもの）を読み直す。
            presenter?.Dispose();
            presenter = null;
            if (Party == null || Data == null)
                return;
            var source = PartySession.Source;
            // 前に読んだ編成と装備があれば、すぐ出して裏で読み直す。なければ読み終えるまで仮データを隠す。
            var knownGear = EquipmentSession.Known;
            bool shown = PartySession.Known != null && knownGear != null;
            if (shown)
                Present(knownGear, source);
            else if (source != null || EquipmentSession.Source != null)
                ShowLoading();
            StartLoad(
                token => LoadAsync(source, EquipmentSession.SourceOrLocal, shown, token),
                LoadFailedMessage,
                () =>
                {
                    // 前のキャラを出しているときは、そのまま残して通知だけにする。
                    if (!shown)
                        ShowLoadFailed();
                },
                "育成のキャラを取得できませんでした",
                readAgain: shown
            );
        }

        private async Task LoadAsync(
            IPartySource source,
            IEquipmentSource equipment,
            bool shown,
            CancellationToken token
        )
        {
            string shownParty = PartySession.ReadVersion;
            string shownGear = EquipmentSession.ReadVersion;
            // 編成と装備は同時に読む。ログインは AccountSessionRunner が1回にまとめる。
            // 保存を待っている変更があれば、編成は読み直さない（PartySession.LoadAsync）。
            var reading =
                source != null ? PartySession.LoadAsync(Party, source, token) : Task.CompletedTask;
            var wearing = equipment.LoadAsync(token);
            await Task.WhenAll(reading, wearing);
            if (token.IsCancellationRequested)
                return;
            if (
                shown
                && Unchanged(shownParty, PartySession.ReadVersion)
                && Unchanged(shownGear, EquipmentSession.ReadVersion)
            )
                return;
            Present(await wearing, source);
        }

        private void Present(EquipmentState worn, IPartySource source)
        {
            presenter?.Dispose();
            presenter = Build(
                this,
                Party,
                Data,
                worn,
                source != null ? LevelUpAsync : null,
                source is IPartyBondSource ? DeepenBondAsync : null,
                ItemArt
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

        private void ShowLoadFailed() => Set(Name, LoadFailedText);

        private void CloseCelebration() => CelebrationClosed?.Invoke();

        // すぐにレベルを上げてルーンを引き、サーバーで上げる。費用はサーバーが計算する。
        // 断られたとき（409）は手元の表示がずれているので、PartySession が戻したあとに読み直す。
        private static Task LevelUpAsync(string id, int from, int to)
        {
            var state = PartySession.State;
            long runes = TrainingRules.CostBetween(from, to, state?.CostPerLevel ?? 0);
            return PartySession.LevelUpAsync(
                id,
                from,
                to,
                runes,
                _ => TrainingPresenter.LevelUpFailedMessage
            );
        }

        // すぐに絆を深めてしるしを使い、サーバーで深める。失敗は PartySession が戻して通知の帯で知らせる。
        private static Task DeepenBondAsync(string id, int bond) =>
            PartySession.DeepenBondAsync(id, bond, TrainingPresenter.DeepenFailedMessage);

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
            if (Bond != null)
                Bond.interactable = false;
            foreach (var pip in BondPips)
                if (pip != null)
                    pip.sprite = BondDark;
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
            if (BondDialog != null)
                BondDialog.SetActive(false);
            if (Celebration != null)
                Celebration.Hide();
        }

        private void OnDisable()
        {
            if (Swipe != null)
                Swipe.Flicked -= Flick;
            if (Celebration != null)
            {
                Celebration.Closed -= CloseCelebration;
                Celebration.Hide();
            }
            Load.Cancel();
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
            Func<string, int, int, Task> levelUp = null,
            Func<string, int, Task> deepenBond = null,
            ItemArt itemArt = null
        ) =>
            new(
                view,
                TrainingRoster.From(PartySession.Formation(party), data),
                new TrainingSessionStore(party, data),
                party.IconOf,
                levelUp,
                id => GearOf(equipment, id),
                party.ArtOf,
                (_, card) => CardText.Description(card),
                new CardLoadoutSessionStore(party, data).UniqueCardOf,
                deepenBond,
                (id, stats) => EquipmentStats.With(stats, equipment, id),
                id => itemArt != null ? itemArt.TextureOf(id) : null
            );

        // 武器・防具と、アクセサリーの3つの枠に付けている装備。
        public static IReadOnlyList<TrainingGearState> GearOf(
            EquipmentState equipment,
            string id
        ) =>
            new[]
            {
                (EquipmentPlace)EquipmentSlot.Weapon,
                EquipmentSlot.Armor,
                EquipmentPlace.Accessory(0),
                EquipmentPlace.Accessory(1),
                EquipmentPlace.Accessory(2),
            }
                .Select(place =>
                {
                    var slot = place.Slot;
                    var item = equipment?.Find(equipment.ItemIn(id, place));
                    var definition = EquipmentCatalog.Find(item?.EquipmentId);
                    var quality = definition?.QualityOf(item.Multiplier) ?? ItemQuality.C;
                    // レア度の★に続けて、その1つの品質の文字を出す。説明の数字は倍率を掛けた値。
                    return TrainingGearState.Worn(
                        EquipmentCatalog.NameOf(slot),
                        definition?.Name,
                        definition != null
                            ? EquipmentCatalog.Stars(definition.Rarity)
                                + "　"
                                + QualityPalette.Painted(quality)
                            : null,
                        definition?.DetailAt(item.Multiplier),
                        quality,
                        definition != null ? EquipmentCatalog.FoilStyleOf(definition.Id) : 0,
                        definition?.Id
                    );
                })
                .ToArray();

        // 個別の画面で戻ると、開いた項目（冒険者の一覧か装備の人選び）へ戻る（同じ人を選んだまま）。重ねた画面は先に閉じる。
        public bool HandleBack()
        {
            if (presenter != null && presenter.Back())
                return true;
            return TrainingSession.LeavePage(Guide);
        }

        public void Render(TrainingState state)
        {
            RenderDetail(state);
            RenderDialog(state);
            RenderBond(state);
            // サーバーがないときは、右上の所持ルーンも、この画面で使ったあとの仮の残高にそろえる。
            // サーバーがあれば PartySession が確かめた残高と、保存を待っている分の取り置きで出す。
            if (state.Member != null && PartySession.State == null)
                RuneBalance.Set(state.Runes);
            person = state.Name;
            if (Guide != null)
                Guide.RefreshTrail();
        }

        // パンくずの「冒険者 › アリア」（装備から開いたら「装備 › アリア」）。読み込むまでは人の名前を出さない。
        public IReadOnlyList<string> Trail =>
            new[] { Guide != null ? Guide.LabelOf(TrainingSession.ReturnKeyOrRoster) : "", person };

        // 停止中の表示の切り替え：レベルアップと絆のダイアログ、深めた演出（生成時に1人目の値で作り込んである）と、
        // サーバーから読み込む間・読めなかったときの表示。
        public IReadOnlyList<string> PreviewStates =>
            new[] { "レベルアップ", "絆", "絆の演出" }
                .Concat(GuidePanelLoad.PreviewStates)
                .ToArray();

        public void ShowPreviewState(int index)
        {
            var overlay =
                index == 0 ? Dialog
                : index == 1 ? BondDialog
                : index == 2 && Celebration != null ? Celebration.gameObject
                : null;
            if (index < 3)
            {
                if (overlay != null)
                    overlay.SetActive(true);
                return;
            }
            ShowLoading();
            if (index == 4)
                ShowLoadFailed();
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
            FitHeader();
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

            StatGrid.Show(Stats, state.Member != null ? state.Stats : (CharacterStats?)null);
            for (int i = 0; i < Passives.Length; i++)
                ShowSkill(Passives[i], i < state.Passives.Count ? state.Passives[i] : null);
            ShowCards(Uniques, state.Uniques);
            ShowCards(CardSlots, state.Cards);
            for (int i = 0; i < Gear.Length; i++)
            {
                var gear = i < state.Gear.Count ? state.Gear[i] : null;
                var art =
                    ItemArt != null && gear?.EquipmentId != null
                        ? ItemArt.Find(gear.EquipmentId)
                        : null;
                ShowGear(
                    Gear[i],
                    gear,
                    art != null ? art
                        : i == 0 ? WeaponIcon
                        : i == 1 ? ArmorIcon
                        : null
                );
            }
        }

        private void FitHeader()
        {
            if (Header == null || HeaderWidth <= 0)
                return;
            Header.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(Header);
            // 行の幅を中身の幅まで広げてから縮める（狭いままだと、文字どうしが重なって詰まる）。
            float width = Mathf.Max(HeaderWidth, LayoutUtility.GetPreferredWidth(Header));
            Header.sizeDelta = new Vector2(width, Header.sizeDelta.y);
            LayoutRebuilder.ForceRebuildLayoutImmediate(Header);
            Header.localScale = Vector3.one * (HeaderWidth / width);
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

        private void RenderDialog(TrainingState state)
        {
            if (Dialog != null)
                Dialog.SetActive(state.DialogOpen);
            Set(From, state.Level.ToString(CultureInfo.InvariantCulture));
            Set(To, state.Target.ToString(CultureInfo.InvariantCulture));
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
            Set(Cost, Number(state.Cost));
            if (Cost != null)
                Cost.color = state.CanAfford ? UiPalette.TextMain : Short;
        }

        private void RenderBond(TrainingState state)
        {
            bool shown = state.Member != null;
            if (Bond != null)
                Bond.interactable = shown;
            for (int i = 0; i < BondPips.Length; i++)
                if (BondPips[i] != null)
                    BondPips[i].sprite = shown && i < state.Bond ? BondLit : BondDark;

            if (BondDialog != null)
                BondDialog.SetActive(state.BondOpen);
            Set(BondFrom, state.Bond.ToString(CultureInfo.InvariantCulture));
            // 上限では次の段を出さない。
            if (BondNext != null)
                BondNext.SetActive(!state.BondMaxed);
            Set(BondTo, state.BondTarget.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < BondStones.Length; i++)
                if (BondStones[i] != null)
                    BondStones[i].sprite = i < state.Bond ? BondLit : BondDark;
            if (BondGlow != null)
            {
                BondGlow.gameObject.SetActive(!state.BondMaxed && state.Bond < BondStones.Length);
                if (!state.BondMaxed && state.Bond < BondStones.Length)
                    BondGlow.anchoredPosition = BondStones[state.Bond]
                        .rectTransform
                        .anchoredPosition;
            }
            for (int i = 0; i < BondDiffs.Length && i < CharacterStats.Count; i++)
            {
                int before = state.Stats[i];
                int after = state.BondStats[i];
                Set(BondDiffs[i].Before, before.ToString(CultureInfo.InvariantCulture));
                Set(BondDiffs[i].After, after.ToString(CultureInfo.InvariantCulture));
                Set(BondDiffs[i].Gain, $"+{after - before}");
            }
            if (BondConfirm != null)
                BondConfirm.interactable = state.CanDeepen;
            Set(BondConfirmLabel, state.BondMaxed ? "絆は上限です" : "絆を深める");
            if (BondCost != null)
                BondCost.SetActive(!state.BondMaxed);
            if (BondToken != null && state.Member != null)
                PartyArt.Paint(BondToken, state.Member, trim: true);
            Set(BondTokens, $"{state.BondTokens} / {TrainingBond.TokensPerStage}");
            if (BondTokens != null)
                BondTokens.color =
                    state.BondTokens >= TrainingBond.TokensPerStage ? UiPalette.TextMain : Short;

            if (Celebration == null)
                return;
            if (state.Celebrated > 0 && state.Member != null)
                Celebration.Show(state.Member, state.Celebrated);
            else
                Celebration.Hide();
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
            if (widget.FrameEdge != null)
            {
                widget.FrameEdge.texture = frame;
                widget.FrameEdge.enabled = frame != null;
                widget.FrameEdge.color = filled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }
            if (widget.FrameFoil != null)
                widget.FrameFoil.Show(filled && !card.Locked ? card.Quality : ItemQuality.C);
            if (widget.Art != null)
            {
                widget.Art.texture = card?.Art;
                widget.Art.enabled = filled && card.Art != null;
            }
            if (widget.Foil != null)
                widget.Foil.Show(
                    card?.Art,
                    filled && !card.Locked ? card.Quality : ItemQuality.C,
                    filled ? card.FoilStyle : 0
                );
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
                PixelIcon.Show(widget.Icon, icon);
                widget.Icon.enabled = icon != null && !locked;
                widget.Icon.color =
                    gear != null && gear.Filled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
            // 付けている装備の品質のキラキラを、種類ごとの光り方で重ねる。空いた枠では出さない。
            if (widget.Foil != null)
            {
                bool filled = gear != null && gear.Filled && !locked;
                widget.Foil.Show(
                    filled ? icon : null,
                    filled ? gear.Quality : ItemQuality.C,
                    filled ? gear.FoilStyle : 0
                );
            }
            Set(
                widget.Name,
                locked ? ""
                    : gear == null ? ""
                    : gear.Filled ? gear.Name
                    : "なし"
            );
            SetRich(widget.Stars, gear != null && gear.Filled ? gear.Stars : "");
            Set(widget.Detail, gear != null && gear.Filled ? gear.Detail : "");
            if (widget.Lock != null)
                widget.Lock.SetActive(locked);
            Set(widget.When, gear?.UnlockText ?? "");
            // 開いていないアクセサリーの枠も押せ、付け替えの画面のアクセサリーのタブを開く。
            if (widget.Button != null)
                widget.Button.interactable = gear != null;
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
            // 0 武器、1 防具、2〜4 アクセサリー。
            bool accessory = slot >= TrainingPresenter.OpenGearSlots;
            EquipmentSession.OpenSlot = accessory ? EquipmentSlot.Weapon : (EquipmentSlot)slot;
            EquipmentSession.OpenAccessory = accessory
                ? slot - TrainingPresenter.OpenGearSlots
                : -1;
            Guide.OpenItem(EquipmentSession.GuideItemKey);
        }

        /// <summary>Moves to the card skills' change screen with this character and slot chosen.</summary>
        public void OpenCards(string id, int slot)
        {
            if (Guide == null || !Guide.HasItem(CardLoadoutSession.GuideItemKey))
                return;
            CardLoadoutSession.Selected = id;
            CardLoadoutSession.OpenSlot = slot;
            CardLoadoutSession.OpenUnique = -1;
            Guide.OpenItem(CardLoadoutSession.GuideItemKey);
        }

        /// <summary>Moves to the card skills' change screen with this character's unique skill and its tree shown.</summary>
        public void OpenUnique(string id, int index)
        {
            if (Guide == null || !Guide.HasItem(CardLoadoutSession.GuideItemKey))
                return;
            CardLoadoutSession.Selected = id;
            CardLoadoutSession.OpenSlot = -1;
            CardLoadoutSession.OpenUnique = index;
            Guide.OpenItem(CardLoadoutSession.GuideItemKey);
        }

        private static string Number(long value) =>
            value.ToString("#,0", CultureInfo.InvariantCulture);
    }
}
