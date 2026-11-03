using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Inventory;
using Baryonyx.Party;
using Baryonyx.Training;
using Baryonyx.UI;
using Baryonyx.UI.Buttons;
using Baryonyx.UI.Cards;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.UI.UiText;

namespace Baryonyx.Equipment
{
    /// <summary>The weapon or armour slot on the left, baked by the generator.</summary>
    [Serializable]
    public sealed class EquipmentSlotWidget
    {
        public Button Button;
        public GameObject Selected;
        public Image Icon;

        // アイコンに重ねる品質のキラキラ（S・Aだけ光る）。
        public CardFoil Foil;

        // 空いている枠だけに出す枠の名前（武器・防具）。
        public TMP_Text Tag;

        // 装備の名前と、そのすぐ右の☆・品質・倍率。
        public TMP_Text Name;
        public TMP_Text Detail;

        // 名前の行の右端の属性のアイコン。
        public ElementIconRow Elements;
    }

    /// <summary>One of the three accessory slots on the left, baked by the generator.</summary>
    [Serializable]
    public sealed class EquipmentAccessoryWidget
    {
        public Button Button;
        public GameObject Selected;

        // まだ開いていない枠の錠前。開いた枠では隠す。
        public GameObject Lock;

        // 付けているアクセサリーの絵と、品質のキラキラ。付けていなければ隠す。
        public Image Icon;
        public CardFoil Foil;

        // 空いている・開いていない枠だけに出す枠の名前（アクセサリー）。
        public TMP_Text Tag;

        // 「Lv 20で解放」、開いた枠では付けている物の名前と☆・品質・倍率か「なし」。
        public TMP_Text Name;

        // 付けているアクセサリーの効果。
        public TMP_Text Detail;

        // 右上の属性のアイコン。
        public ElementIconRow Elements;
    }

    /// <summary>
    /// The adventurer's equipment tab of the formation over the guide screen: the person's stats,
    /// weapon, armour and accessory slots on the left; the owned items for the chosen slot and
    /// "外す" on the right, between the people's column and the tabs (<see cref="PartySheet"/>).
    /// EquipmentAssets bakes the parts into the formation prefab with the mock data, so
    /// they read in the editor; this view wires them up and drives its own presenter. It reads
    /// the party and the equipment on opening (the server's, or the app's memory without one)
    /// and saves every change through <see cref="EquipmentSession.SourceOrLocal"/>. Back
    /// returns to the adventurers' list. Changes are told on the guide screen's notice band.
    /// </summary>
    public sealed class EquipmentView
        : GuidePanel,
            IEquipmentView,
            IGuideBackHandler,
            IGuideBreadcrumb,
            IEditModePreview
    {
        public const string LoadFailedMessage = "装備を取得できませんでした";

        public PartyMockData Party;

        // ステータスは育成の仮データの成長から、今のレベルの値を出す。
        public TrainingMockData Training;

        // 左の人の列と右のタブ。
        public PartySheet Sheet;

        // 左の面の上の左右のフリックで人を替える（ステータスのタブと同じ）。
        public GuideSwipe Swipe;

        // 武器・防具の順の2つの枠。
        public EquipmentSlotWidget[] Slots = Array.Empty<EquipmentSlotWidget>();

        // アクセサリーの3つの枠。押すと右の一覧がアクセサリーに替わる。
        public EquipmentAccessoryWidget[] Accessories = Array.Empty<EquipmentAccessoryWidget>();

        // 選んでいる人のステータス（CharacterStats の順）。
        public TMP_Text[] Stats = Array.Empty<TMP_Text>();

        // 読み込み中と、読めなかったときだけ一覧の面に出す文字。
        public TMP_Text Status;
        public EquipmentRow[] Rows = Array.Empty<EquipmentRow>();

        // リストの最後の「外す」。選んでいる枠が空いている間は押せない。
        public Button Remove;
        public TMP_Text RemoveLabel;
        public ScrollRect List;

        public Sprite WeaponIcon;
        public Sprite ArmorIcon;

        // 装備の種類ごとの絵。絵のない種類は、枠ごとのアイコン（WeaponIcon・ArmorIcon）を出す。
        public ItemArt ItemArt;

        private EquipmentPresenter presenter;
        private readonly ButtonBindings bindings = new();

        // 付け替えた枠のカードを縦に回す（武器・防具、続けてアクセサリーの枠の順）。
        private GlassCardTurns turns;

        public event Action PrevPressed;
        public event Action NextPressed;
        public event Action<int> SlotPressed;
        public event Action<string> ItemPressed;
        public event Action RemovePressed;

        public EquipmentPresenter Presenter => presenter;

        private void OnEnable()
        {
            person = null;
            Turns().Forget();
            if (Swipe != null)
                Swipe.Flicked += Flick;
            if (Sheet != null)
                Sheet.PersonPressed += Choose;
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                bindings.Bind(Slots[i].Button, () => SlotPressed?.Invoke(index));
            }
            for (int i = 0; i < Accessories.Length; i++)
            {
                int index = Slots.Length + i;
                bindings.Bind(Accessories[i].Button, () => SlotPressed?.Invoke(index));
            }
            foreach (var row in Rows)
                BindRow(row);
            bindings.Bind(Remove, () => RemovePressed?.Invoke());

            // 開くたびに、サーバー（なければアプリを動かしている間）の編成と装備を読み直す。
            presenter?.Dispose();
            presenter = null;
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            if (Party == null)
                return;
            var party = PartySession.Source;
            var equipment = EquipmentSession.SourceOrLocal;
            // 前に読んだ編成と装備があれば、すぐ出して裏で読み直す。なければ読み終えるまで仮データを隠す。
            var knownParty = PartySession.Known;
            var knownGear = EquipmentSession.Known;
            bool shown = knownParty != null && knownGear != null;
            if (shown)
                Present(knownParty, knownGear, equipment);
            else if (party != null || EquipmentSession.Source != null)
                ShowLoading();
            StartLoad(
                token => LoadAsync(party, equipment, shown, token),
                LoadFailedMessage,
                () =>
                {
                    // 前の編成と装備を出しているときは、そのまま残して通知だけにする。
                    if (!shown)
                        ShowLoadFailed();
                },
                readAgain: shown
            );
        }

        private async Task LoadAsync(
            IPartySource party,
            IEquipmentSource equipment,
            bool shown,
            CancellationToken token
        )
        {
            string shownParty = PartySession.ReadVersion;
            string shownGear = EquipmentSession.ReadVersion;
            // 編成と装備は同時に読む。ログインは AccountSessionRunner が1回にまとめる。
            var reading = PartySession.LoadAsync(Party, party, token);
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
            Present(await reading, await wearing, equipment);
        }

        private void Present(
            PartyFormation formation,
            EquipmentState state,
            IEquipmentSource equipment
        )
        {
            presenter?.Dispose();
            var (people, _) = formation.TabOrder();
            presenter = new EquipmentPresenter(
                this,
                people,
                state,
                equipment,
                EquipmentSession.Selected,
                id => EquipmentSession.Selected = id,
                EquipmentSession.OpenSlot,
                EquipmentSession.OpenAccessory,
                LevelOf,
                BaseStatsOf
            );
        }

        private void ShowLoadFailed() => Set(Status, LoadFailedText);

        // 停止中の表示の切り替え（doc/rules/ui-design.md#停止中の表示の切り替え）。最初の表示は武器のタブ。
        public IReadOnlyList<string> PreviewStates =>
            new[] { "防具", "アクセサリー" }.Concat(GuidePanelLoad.PreviewStates).ToArray();

        public void ShowPreviewState(int index)
        {
            if (index >= 2)
            {
                ShowLoading();
                if (index == 3)
                    ShowLoadFailed();
                return;
            }
            // 仮データの人と仮の装備で、Play中と同じ表示の切り替えを1度だけ通す。静的な状態は読まない。
            previewing = true;
            try
            {
                var (people, _) = PartyFormation.From(Party).TabOrder();
                new EquipmentPresenter(
                    this,
                    people,
                    EquipmentLocalSource.Starter(),
                    new EquipmentLocalSource(),
                    slot: index == 0 ? EquipmentSlot.Armor : EquipmentSlot.Weapon,
                    accessory: index == 1 ? 0 : -1,
                    levelOf: LevelOf,
                    statsOf: BaseStatsOf
                ).Dispose();
            }
            finally
            {
                previewing = false;
            }
        }

        // 停止中の表示を作っている間。Lvとスキルは仮データから読み、自分の外（パンくず）は変えない。
        private bool previewing;

        private int LevelOf(string id) =>
            !previewing ? PartySession.LevelOf(Party, id)
            : Party != null ? Party.Find(id)?.Level ?? 1
            : 1;

        /// <summary>
        /// The person's stats at their level with the bond, before the equipment raises them;
        /// null without their growth.
        /// </summary>
        public CharacterStats? BaseStatsOf(string id)
        {
            var growth = Training != null ? Training.Find(id) : null;
            return growth != null ? TrainingBond.StatsOf(growth, LevelOf(id)) : null;
        }

        // 読み込むまで、仮データの装備を本当の装備として見せない。
        private void ShowLoading()
        {
            Turns().Forget();
            Set(Status, LoadingText);
            foreach (var widget in Slots)
                ShowSlot(widget, null, null, false);
            StatGrid.Show(Stats, null);
            foreach (var row in Rows)
                row.gameObject.SetActive(false);
            if (Remove != null)
                Remove.gameObject.SetActive(false);
        }

        // 左へのフリックで次の人、右へのフリックで前の人。
        private void Flick(int step)
        {
            if (step > 0)
                NextPressed?.Invoke();
            else if (step < 0)
                PrevPressed?.Invoke();
        }

        private void OnDisable()
        {
            Load.Cancel();
            bindings.Clear();
            if (Swipe != null)
                Swipe.Flicked -= Flick;
            if (Sheet != null)
                Sheet.PersonPressed -= Choose;
            presenter?.Dispose();
            presenter = null;
        }

        // 左の列で押した人を出す。選んでいる枠はそのまま。
        private void Choose(string id) => presenter?.Select(id);

        // パンくずの「冒険者 › アリア」（育成の人選びから開いたら「育成 › アリア」）。どのタブかはタブでわかる。
        // 読み込むまでは人の名前を出さない。
        public IReadOnlyList<string> Trail => TrainingSession.Trail(Guide, person);

        // パンくずに出す、表示している人の名前。
        private string person;

        // 戻ると、タブを開いた項目（冒険者の一覧か育成の人選び）へ戻る。
        public bool HandleBack() => TrainingSession.LeavePage(Guide);

        public void Render(EquipmentViewState state)
        {
            var member = Party != null ? Party.Find(state.PersonId) : null;
            person = member?.Name;
            if (Guide != null && !previewing)
                Guide.RefreshTrail();
            int level = LevelOf(state.PersonId);
            if (Sheet != null && Party != null)
            {
                var (people, partyCount) = (
                    previewing ? PartyFormation.From(Party) : PartySession.Formation(Party)
                ).TabOrder();
                Sheet.Show(people, partyCount, state.PersonId);
            }
            bool accessory = state.Accessory >= 0;
            if (!previewing)
                EquipmentSession.LastPlace = accessory
                    ? EquipmentPlace.Accessory(state.Accessory)
                    : state.Slot;
            // 同じ人の枠の中身が変わったら、描き替える前のカードを写して縦に回す。
            var turning = Turns();
            turning.Begin(state.PersonId);
            for (int i = 0; i < Slots.Length; i++)
            {
                var item = i < state.Slots.Count ? state.Slots[i] : null;
                var icon = IconOf(item, (EquipmentSlot)i);
                turning.Show(
                    i,
                    CardOf(Slots[i].Button),
                    item?.ItemId ?? "",
                    icon != null ? icon.texture : null
                );
                ShowSlot(Slots[i], item, icon, !accessory && i == (int)state.Slot);
            }
            for (int i = 0; i < Accessories.Length; i++)
            {
                var item = i < state.Accessories.Count ? state.Accessories[i] : null;
                var art = item != null ? ArtOf(item) : null;
                int shownLevel = member != null ? level : 0;
                turning.Show(
                    Slots.Length + i,
                    CardOf(Accessories[i].Button),
                    AccessorySlots.IsOpen(i, shownLevel) ? item?.ItemId ?? "" : "locked",
                    art != null ? art.texture : null
                );
                ShowAccessory(Accessories[i], i, shownLevel, i == state.Accessory, item, art);
            }
            // ステータスは装備を入れた値だけを出す。上昇分は添えない。
            StatGrid.Show(Stats, state.Stats);
            // 一覧の面の文字は、アクセサリーの枠を選んでいて、付けられる物がないときだけ出す
            // （開いていなければ解放するLv）。
            Set(
                Status,
                !accessory || state.Choices.Count > 0 ? ""
                    : AccessorySlots.IsOpen(state.Accessory, level) ? NoAccessoryText
                    : AccessorySlots.UnlockText(state.Accessory)
            );
            ShowRows(state);
        }

        // アクセサリーの枠を選んでいて、開いているが持っていないときの一覧の面の文字。
        public const string NoAccessoryText = "持っているアクセサリーはありません";

        /// <summary>
        /// One accessory slot: locked with the level it opens at, or once open, the accessory
        /// worn there with its picture, rarity and effect ("なし" when empty).
        /// </summary>
        public static void ShowAccessory(
            EquipmentAccessoryWidget widget,
            int index,
            int level,
            bool selected,
            EquipmentItemState worn = null,
            Sprite art = null
        )
        {
            bool open = AccessorySlots.IsOpen(index, level);
            bool filled = open && worn?.ItemId != null;
            if (widget.Selected != null)
                widget.Selected.SetActive(selected);
            if (widget.Lock != null)
                widget.Lock.SetActive(!open);
            if (widget.Icon != null)
            {
                widget.Icon.gameObject.SetActive(filled && art != null);
                if (filled && art != null)
                    PixelIcon.Show(widget.Icon, art);
            }
            ShowFoil(widget.Foil, filled && art != null ? worn : null, art);
            SetRich(
                widget.Name,
                !open ? AccessorySlots.UnlockText(index)
                    : filled ? Heading(worn)
                    : "なし"
            );
            if (widget.Tag != null)
                widget.Tag.gameObject.SetActive(!filled);
            Set(widget.Detail, filled ? worn.Detail : "");
            if (widget.Elements != null)
                widget.Elements.Show(filled ? worn.Elements : null);
        }

        // 付け替えた枠を回す部品。線の光は、新しく付けた物の絵の色から取る。
        private GlassCardTurns Turns() =>
            turns ??= new GlassCardTurns(
                transform,
                art => Combat.Presentation.CardArtColor.Of(art, Color.white)
            );

        private static GlassCard CardOf(Button button) =>
            button != null && button.TryGetComponent(out GlassCard card) ? card : null;

        // 装備の種類ごとの絵。絵がなければ null。
        private Sprite ArtOf(EquipmentItemState item) =>
            ItemArt != null && item?.ItemId != null ? ItemArt.Find(item.EquipmentId) : null;

        public Sprite IconOf(EquipmentSlot slot) =>
            slot == EquipmentSlot.Weapon ? WeaponIcon : ArmorIcon;

        /// <summary>The item's own picture, or the slot's icon for an empty slot or a kind without art.</summary>
        public Sprite IconOf(EquipmentItemState item, EquipmentSlot slot)
        {
            var art = ArtOf(item);
            return art != null ? art : IconOf(slot);
        }

        // その人が付けているカードの属性のアイコン（重ねずに、カードの順）。
        // 持っている装備の行を並べ、足りなければ行を写して増やす。最後に「外す」。
        private void ShowRows(EquipmentViewState state)
        {
            EnsureRows(state.Choices.Count);
            for (int i = 0; i < Rows.Length; i++)
            {
                bool on = i < state.Choices.Count;
                Rows[i].gameObject.SetActive(on);
                if (!on)
                    continue;
                Rows[i].transform.SetSiblingIndex(i);
                ShowRow(Rows[i], state.Choices[i], IconOf(state.Choices[i], state.Slot));
            }
            if (Remove != null)
            {
                // 開いていないアクセサリーの枠では、外す行も出さない。
                Remove.gameObject.SetActive(
                    state.Accessory < 0 || state.Choices.Count > 0 || state.CanRemove
                );
                Remove.transform.SetAsLastSibling();
                Remove.interactable = state.CanRemove;
                if (RemoveLabel != null)
                    RemoveLabel.alpha = state.CanRemove ? 1f : 0.4f;
            }
        }

        private void EnsureRows(int count)
        {
            if (Rows.Length == 0 || Rows.Length >= count)
                return;
            var rows = new List<EquipmentRow>(Rows);
            var parent = Rows[0].transform.parent;
            while (rows.Count < count)
            {
                var copy = Instantiate(Rows[0], parent);
                copy.name = "Item" + rows.Count;
                rows.Add(copy);
                BindRow(copy);
            }
            Rows = rows.ToArray();
        }

        /// <summary>
        /// Shows an item (or an empty slot) in the weapon or armour slot. The generator calls it
        /// too, so the prefab shows the first person's equipment in the editor.
        /// </summary>
        public static void ShowSlot(
            EquipmentSlotWidget widget,
            EquipmentItemState item,
            Sprite icon,
            bool selected
        )
        {
            bool filled = item != null && item.ItemId != null;
            if (widget.Selected != null)
                widget.Selected.SetActive(selected);
            if (widget.Icon != null)
            {
                PixelIcon.Show(widget.Icon, icon);
                widget.Icon.color = filled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
            ShowFoil(widget.Foil, filled ? item : null, icon);
            SetRich(
                widget.Name,
                item == null ? ""
                    : filled ? Heading(item)
                    : item.Name
            );
            if (widget.Tag != null)
                widget.Tag.gameObject.SetActive(!filled);
            Set(widget.Detail, filled ? item.Detail : "");
            if (widget.Elements != null)
                widget.Elements.Show(filled ? item.Elements : null);
        }

        /// <summary>
        /// The item's name with the kind's rarity in stars and this one item's quality and
        /// multiplier right after it ("炎のダガー ★★★★ C ×1.00", rich text;
        /// doc/rules/ui-design.md の装備の見せ方). An empty slot shows its name alone.
        /// </summary>
        public static string Heading(EquipmentItemState item) =>
            item == null ? ""
            : string.IsNullOrEmpty(item.Name) || item.ItemId == null ? item.Name ?? ""
            : ItemText.Heading(
                item.Name,
                item.Stars?.Length ?? 0,
                true,
                item.Quality,
                item.Multiplier
            );

        /// <summary>
        /// One owned item: its name, rarity, quality, effect, how the stats change when it is put
        /// on, and who wears it.
        /// </summary>
        public static void ShowRow(EquipmentRow row, EquipmentItemState item, Sprite icon)
        {
            row.ItemId = item.ItemId;
            PixelIcon.Show(row.Icon, icon);
            ShowFoil(row.Foil, item.ItemId != null ? item : null, icon);
            SetRich(row.Name, Heading(item));
            Set(row.Detail, item.Detail);
            SetRich(row.Change, ChangeText(item.Changes));
            // 属性が合わず付けられない装備は、薄くして押せなくする。
            if (row.Button != null)
            {
                row.Button.interactable = item.Usable;
                if (row.Button.TryGetComponent(out GlassCard card))
                    card.Interactable = item.Usable;
            }
            if (row.Mark != null)
            {
                row.Mark.gameObject.SetActive(item.Mark.Length > 0);
                row.Mark.text = item.Mark;
                row.Mark.alpha = item.Worn ? 1f : 0.75f;
            }
            ShowElements(row, item);
        }

        // 行の右上の属性のアイコン。誰かが付けている印（右上）があれば、その左に並べる。
        private static void ShowElements(EquipmentRow row, EquipmentItemState item)
        {
            if (row.Elements == null)
                return;
            row.Elements.Show(item.Elements);
            var rect = (RectTransform)row.Elements.transform;
            float right = EquipmentRow.ElementsRight;
            if (row.Mark != null && item.Mark.Length > 0)
                right += row.Mark.GetPreferredValues(item.Mark).x + EquipmentRow.ElementsGap;
            rect.anchoredPosition = new Vector2(-right, rect.anchoredPosition.y);
        }

        /// <summary>
        /// The stat changes as "属攻 ▲43　会心 ▼2": the amount green when it rises and red when it
        /// falls (rich text).
        /// </summary>
        public static string ChangeText(IReadOnlyList<(int Stat, int Amount)> changes)
        {
            if (changes == null || changes.Count == 0)
                return "";
            string rise = ColorUtility.ToHtmlStringRGB(UiPalette.Rise);
            string fall = ColorUtility.ToHtmlStringRGB(UiPalette.Fall);
            return string.Join(
                "　",
                changes.Select(change =>
                    change.Amount > 0
                        ? $"{CharacterStats.Labels[change.Stat]} <color=#{rise}>▲{change.Amount}</color>"
                        : $"{CharacterStats.Labels[change.Stat]} <color=#{fall}>▼{-change.Amount}</color>"
                )
            );
        }

        // 装備の品質のキラキラを、種類ごとの光り方でアイコンに重ねる。空いている枠では出さない。
        private static void ShowFoil(CardFoil foil, EquipmentItemState item, Sprite icon)
        {
            if (foil != null)
                foil.Show(icon, item?.Quality ?? ItemQuality.C, item?.FoilStyle ?? 0);
        }

        private void BindRow(EquipmentRow row)
        {
            if (row == null)
                return;
            bindings.Bind(row.Button, () => ItemPressed?.Invoke(row.ItemId));
        }
    }
}
