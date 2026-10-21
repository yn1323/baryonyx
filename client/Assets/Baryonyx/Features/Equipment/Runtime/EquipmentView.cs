using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        public TMP_Text Name;
        public TMP_Text Stars;
        public TMP_Text Detail;
    }

    /// <summary>One of the three accessory slots on the left, baked by the generator.</summary>
    [Serializable]
    public sealed class EquipmentAccessoryWidget
    {
        public Button Button;
        public GameObject Selected;

        // まだ開いていない枠の錠前。開いた枠では隠す。
        public GameObject Lock;

        // 「Lv 15で解放」、開いた枠では「なし」。
        public TMP_Text Name;
    }

    /// <summary>
    /// The formation's equipment change screen over the guide screen: the person (◀ ▶, figure,
    /// name and level), their weapon, armour and the accessory slots that are not ready yet,
    /// and their stats on the left; the owned items for the chosen slot and "外す" on the
    /// right. EquipmentAssets bakes the parts into the formation prefab with the mock data, so
    /// they read in the editor; this view wires them up and drives its own presenter. It reads
    /// the party and the equipment on opening (the server's, or the app's memory without one)
    /// and saves every change through <see cref="EquipmentSession.SourceOrLocal"/>. Back
    /// returns to the adventurer's page. Changes are told on the guide screen's notice band.
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

        // 左上の人（立ち姿・名前・Lv）。
        public PartyPersonHeader Person = new();

        // 左の面の上の左右のフリックで人を替える（個別の画面と同じ）。
        public GuideSwipe Swipe;

        // 武器・防具の順の2つの枠。
        public EquipmentSlotWidget[] Slots = Array.Empty<EquipmentSlotWidget>();

        // アクセサリーの3つの枠。押すとアクセサリーのタブに移る（アクセサリーはまだ持てない）。
        public EquipmentAccessoryWidget[] Accessories = Array.Empty<EquipmentAccessoryWidget>();

        // 選んでいる人のステータス（CharacterStats の順）。
        public TMP_Text[] Stats = Array.Empty<TMP_Text>();

        // 右の上の武器・防具・アクセサリーのタブ（枠と同じ順）。左の枠と連動し、アクセサリーは準備中で押せない。
        public GuideTab[] Tabs = Array.Empty<GuideTab>();

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

        public event Action PrevPressed;
        public event Action NextPressed;
        public event Action<int> SlotPressed;
        public event Action<string> ItemPressed;
        public event Action RemovePressed;

        public EquipmentPresenter Presenter => presenter;

        private void OnEnable()
        {
            person = null;
            if (Swipe != null)
                Swipe.Flicked += Flick;
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
            // タブは左の枠と連動する。アクセサリーのタブは、選んでいたアクセサリーの枠（なければ1つ目）を選ぶ。
            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                if (Tabs[i] != null)
                    bindings.Bind(Tabs[i].Button, () => SlotPressed?.Invoke(TabTarget(index)));
            }
            foreach (var row in Rows)
                BindRow(row);
            bindings.Bind(Remove, () => RemovePressed?.Invoke());

            // 開くたびに、サーバー（なければアプリを動かしている間）の編成と装備で描き直す。
            presenter?.Dispose();
            presenter = null;
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            if (Party == null)
                return;
            var party = PartySession.Source;
            var equipment = EquipmentSession.SourceOrLocal;
            if (party != null || EquipmentSession.Source != null)
                ShowLoading();
            StartLoad(
                token => LoadAsync(party, equipment, token),
                LoadFailedMessage,
                ShowLoadFailed
            );
        }

        private async Task LoadAsync(
            IPartySource party,
            IEquipmentSource equipment,
            CancellationToken token
        )
        {
            // 同じセッションを順に使う（ログインのやり直しを重ねない）。
            var formation = await PartySession.LoadAsync(Party, party, token);
            var state = await equipment.LoadAsync(token);
            if (token.IsCancellationRequested)
                return;
            var (people, _) = formation.TabOrder();
            presenter = new EquipmentPresenter(
                this,
                people,
                state,
                equipment,
                EquipmentSession.Selected,
                id => EquipmentSession.Selected = id,
                EquipmentSession.OpenSlot,
                EquipmentSession.OpenAccessory
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
                    accessory: index == 1 ? 0 : -1
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

        // 読み込むまで、仮データの装備を本当の装備として見せない。
        private void ShowLoading()
        {
            Set(Status, LoadingText);
            Person.ShowLoading(LoadingText);
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
            presenter?.Dispose();
            presenter = null;
        }

        // 付け替えの画面で戻ると、冒険者の個別の画面へ戻る（同じ人を選んだまま）。
        // パンくずの「冒険者 › アリア › 装備」（個別の画面を装備から開いたら「装備 › アリア › 装備」）。
        // 読み込むまでは人の名前を出さない。
        public IReadOnlyList<string> Trail =>
            TrainingSession.ChangeTrail(Guide, person, EquipmentSession.GuideItemKey);

        // パンくずに出す、表示している人の名前。
        private string person;

        public bool HandleBack() => TrainingSession.BackToPage(Guide);

        public void Render(EquipmentViewState state)
        {
            var member = Party != null ? Party.Find(state.PersonId) : null;
            person = member?.Name;
            if (Guide != null && !previewing)
                Guide.RefreshTrail();
            int level = LevelOf(state.PersonId);
            Person.Show(member, level, Elements(state.PersonId));
            bool accessory = state.Accessory >= 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                var item = i < state.Slots.Count ? state.Slots[i] : null;
                ShowSlot(
                    Slots[i],
                    item,
                    IconOf(item, (EquipmentSlot)i),
                    !accessory && i == (int)state.Slot
                );
            }
            for (int i = 0; i < Accessories.Length; i++)
                ShowAccessory(Accessories[i], i, member != null ? level : 0, i == state.Accessory);
            var growth = Training != null ? Training.Find(state.PersonId) : null;
            StatGrid.Show(Stats, growth?.StatsAt(level));
            // 一覧の面の文字は、アクセサリーの枠を選んでいるときだけ出す（開いていなければ解放するLv）。
            Set(
                Status,
                !accessory ? ""
                    : AccessorySlots.IsOpen(state.Accessory, level) ? NoAccessoryText
                    : AccessorySlots.UnlockText(state.Accessory)
            );
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i == (accessory ? AccessoryTab : (int)state.Slot);
            ShowRows(state);
        }

        // アクセサリーの枠を選んでいて、開いているが持っていないときの一覧の面の文字。
        public const string NoAccessoryText = "持っているアクセサリーはありません";
        private const int AccessoryTab = 2;

        // タブを押したときに選ぶ左の枠（0 武器、1 防具、2〜4 アクセサリー）。
        private int TabTarget(int tab)
        {
            if (tab < AccessoryTab)
                return tab;
            int current = presenter?.State?.Accessory ?? -1;
            return Slots.Length + Math.Max(0, current);
        }

        /// <summary>One accessory slot: locked with the level it opens at, or empty once open.</summary>
        public static void ShowAccessory(
            EquipmentAccessoryWidget widget,
            int index,
            int level,
            bool selected
        )
        {
            bool open = AccessorySlots.IsOpen(index, level);
            if (widget.Selected != null)
                widget.Selected.SetActive(selected);
            if (widget.Lock != null)
                widget.Lock.SetActive(!open);
            Set(widget.Name, open ? "なし" : AccessorySlots.UnlockText(index));
        }

        public Sprite IconOf(EquipmentSlot slot) =>
            slot == EquipmentSlot.Weapon ? WeaponIcon : ArmorIcon;

        /// <summary>The item's own picture, or the slot's icon for an empty slot or a kind without art.</summary>
        public Sprite IconOf(EquipmentItemState item, EquipmentSlot slot)
        {
            var art =
                ItemArt != null && item?.ItemId != null ? ItemArt.Find(item.EquipmentId) : null;
            return art != null ? art : IconOf(slot);
        }

        // その人が付けているカードの属性のアイコン（重ねずに、カードの順）。
        private IReadOnlyList<Sprite> Elements(string id) =>
            Party == null ? Array.Empty<Sprite>()
            : previewing
                ? Party.IconsOf(
                    Party.Find(id)?.Cards.Select(card => card.Skill) ?? Array.Empty<string>()
                )
            : PartySession.ElementIconsOf(Party, id);

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
                // アクセサリーはまだ持てないので、外す行も出さない。
                Remove.gameObject.SetActive(state.Accessory < 0);
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
            Set(widget.Name, item == null ? "" : item.Name);
            SetRich(widget.Stars, filled ? Grade(item) : "");
            Set(widget.Detail, filled ? item.Detail : "");
        }

        /// <summary>
        /// The kind's rarity in stars, then this one item's quality letter in its colour
        /// ("★★★　S", rich text; doc/features/inventory.md).
        /// </summary>
        public static string Grade(EquipmentItemState item) =>
            item == null || string.IsNullOrEmpty(item.Name) || item.ItemId == null
                ? item?.Stars ?? ""
                : $"{item.Stars}　{QualityPalette.Painted(item.Quality)}";

        /// <summary>One owned item: its name, rarity, quality, effect and who wears it.</summary>
        public static void ShowRow(EquipmentRow row, EquipmentItemState item, Sprite icon)
        {
            row.ItemId = item.ItemId;
            PixelIcon.Show(row.Icon, icon);
            ShowFoil(row.Foil, item.ItemId != null ? item : null, icon);
            Set(row.Name, item.Name);
            SetRich(row.Stars, Grade(item));
            Set(row.Detail, item.Detail);
            if (row.Mark != null)
            {
                row.Mark.gameObject.SetActive(item.Mark.Length > 0);
                row.Mark.text = item.Mark;
                row.Mark.alpha = item.Worn ? 1f : 0.75f;
            }
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
