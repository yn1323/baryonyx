using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Party;
using Baryonyx.Training;
using Baryonyx.UI.Buttons;
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
        public TMP_Text Name;
        public TMP_Text Stars;
        public TMP_Text Detail;
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
    public sealed class EquipmentView : MonoBehaviour, IEquipmentView, IGuideBackHandler
    {
        public const string LoadingText = GuidePanelLoad.LoadingText;
        public const string LoadFailedText = GuidePanelLoad.LoadFailedText;
        public const string LoadFailedMessage = "装備を取得できませんでした";

        public PartyMockData Party;

        // ステータスは育成の仮データの成長から、今のレベルの値を出す。
        public TrainingMockData Training;

        // 通知は案内人の画面の通知の帯に出す。
        public GuideMenuView Guide;

        // 左上の人（◀▶・立ち姿・名前・Lv）。
        public PartyPersonHeader Person = new();

        // 武器・防具の順の2つの枠。
        public EquipmentSlotWidget[] Slots = Array.Empty<EquipmentSlotWidget>();

        // まだ付けられないアクセサリーの3つの枠（押せない）。
        public GameObject[] Accessories = Array.Empty<GameObject>();

        // 選んでいる人のステータス（CharacterStats の順）。
        public TMP_Text[] Stats = Array.Empty<TMP_Text>();

        public TMP_Text Title;
        public TMP_Text Count;
        public EquipmentRow[] Rows = Array.Empty<EquipmentRow>();

        // リストの最後の「外す」。選んでいる枠が空いている間は押せない。
        public Button Remove;
        public TMP_Text RemoveLabel;
        public ScrollRect List;

        public Sprite WeaponIcon;
        public Sprite ArmorIcon;

        private EquipmentPresenter presenter;
        private readonly GuidePanelLoad load = new();
        private readonly ButtonBindings bindings = new();

        public event Action PrevPressed;
        public event Action NextPressed;
        public event Action<int> SlotPressed;
        public event Action<string> ItemPressed;
        public event Action RemovePressed;

        public EquipmentPresenter Presenter => presenter;
        public string LastNotice { get; private set; } = "";

        // 実行中または直前の読み込み。テストで完了を待つために公開する。
        public Task LoadTask => load.Task;

        private void OnEnable()
        {
            bindings.Bind(Person.Prev, () => PrevPressed?.Invoke());
            bindings.Bind(Person.Next, () => NextPressed?.Invoke());
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                bindings.Bind(Slots[i].Button, () => SlotPressed?.Invoke(index));
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
            load.Start(token => LoadAsync(party, equipment, token), LoadFailed);
        }

        private async Task LoadAsync(
            IPartySource party,
            IEquipmentSource equipment,
            CancellationToken token
        )
        {
            // 同じセッションを順に使う（ログインのやり直しを重ねない）。
            var formation =
                party != null
                    ? PartySession.Use(Party, await party.LoadAsync(token))
                    : PartySession.Formation(Party);
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
                EquipmentSession.OpenSlot
            );
        }

        private void LoadFailed(Exception exception)
        {
            Debug.LogWarning("装備を取得できませんでした。" + exception.Message, this);
            Set(Count, LoadFailedText);
            ShowNotice(LoadFailedMessage);
        }

        // 読み込むまで、仮データの装備を本当の装備として見せない。
        private void ShowLoading()
        {
            Set(Count, LoadingText);
            Person.ShowLoading(LoadingText);
            foreach (var widget in Slots)
                ShowSlot(widget, null, null, false);
            TrainingView.ShowStats(Stats, null);
            foreach (var row in Rows)
                row.gameObject.SetActive(false);
            if (Remove != null)
                Remove.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            load.Cancel();
            bindings.Clear();
            presenter?.Dispose();
            presenter = null;
        }

        // 付け替えの画面で戻ると、冒険者の個別の画面へ戻る（同じ人を選んだまま）。
        public bool HandleBack() =>
            Guide != null
            && Guide.HasItem(TrainingSession.GuideItemKey)
            && Guide.OpenItem(TrainingSession.GuideItemKey);

        public void Render(EquipmentViewState state)
        {
            var member = Party != null ? Party.Find(state.PersonId) : null;
            int level = PartySession.LevelOf(Party, state.PersonId);
            Person.Show(member, level, Elements(state.PersonId), state.CanSwitch);
            for (int i = 0; i < Slots.Length; i++)
                ShowSlot(
                    Slots[i],
                    i < state.Slots.Count ? state.Slots[i] : null,
                    IconOf((EquipmentSlot)i),
                    i == (int)state.Slot
                );
            var growth = Training != null ? Training.Find(state.PersonId) : null;
            TrainingView.ShowStats(Stats, growth?.StatsAt(level));
            Set(Title, state.Title);
            Set(Count, state.CountText);
            ShowRows(state);
        }

        public void ShowNotice(string message)
        {
            LastNotice = message ?? "";
            if (Guide != null)
                Guide.ShowToast(message);
        }

        public Sprite IconOf(EquipmentSlot slot) =>
            slot == EquipmentSlot.Weapon ? WeaponIcon : ArmorIcon;

        // その人が付けているカードの属性のアイコン（重ねずに、カードの順）。
        private IReadOnlyList<Sprite> Elements(string id) =>
            Party == null
                ? Array.Empty<Sprite>()
                : PartySession
                    .CardsOf(Party, id)
                    .Select(Party.IconOf)
                    .Where(icon => icon != null)
                    .Distinct()
                    .ToArray();

        // 持っている装備の行を並べ、足りなければ行を写して増やす。最後に「外す」。
        private void ShowRows(EquipmentViewState state)
        {
            EnsureRows(state.Choices.Count);
            var icon = IconOf(state.Slot);
            for (int i = 0; i < Rows.Length; i++)
            {
                bool on = i < state.Choices.Count;
                Rows[i].gameObject.SetActive(on);
                if (!on)
                    continue;
                Rows[i].transform.SetSiblingIndex(i);
                ShowRow(Rows[i], state.Choices[i], icon);
            }
            if (Remove != null)
            {
                Remove.gameObject.SetActive(true);
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
                widget.Icon.sprite = icon;
                widget.Icon.enabled = icon != null;
                widget.Icon.color = filled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
            Set(widget.Name, item == null ? "" : item.Name);
            Set(widget.Stars, filled ? item.Stars : "");
            Set(widget.Detail, filled ? item.Detail : "");
        }

        /// <summary>One owned item: its name, rarity, effect and who wears it.</summary>
        public static void ShowRow(EquipmentRow row, EquipmentItemState item, Sprite icon)
        {
            row.ItemId = item.ItemId;
            if (row.Icon != null)
            {
                row.Icon.sprite = icon;
                row.Icon.enabled = icon != null;
            }
            Set(row.Name, item.Name);
            Set(row.Stars, item.Stars);
            Set(row.Detail, item.Detail);
            if (row.Mark != null)
            {
                row.Mark.gameObject.SetActive(item.Mark.Length > 0);
                row.Mark.text = item.Mark;
                row.Mark.alpha = item.Worn ? 1f : 0.75f;
            }
        }

        private void BindRow(EquipmentRow row)
        {
            if (row == null)
                return;
            bindings.Bind(row.Button, () => ItemPressed?.Invoke(row.ItemId));
        }
    }
}
