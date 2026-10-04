using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Party;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    /// The formation's equipment over the whole guide screen: the people's tabs, the chosen
    /// person's weapon and armour and their figure on the left, the owned items for the chosen
    /// slot and "外す" on the right. EquipmentAssets bakes the parts into the formation prefab
    /// with the mock data, so they read in the editor; this view wires them up and drives its
    /// own presenter. It reads the party and the equipment on opening (the server's, or the
    /// app's memory without one) and saves every change through
    /// <see cref="EquipmentSession.SourceOrLocal"/>. Changes are told on the guide screen's
    /// notice band.
    /// </summary>
    public sealed class EquipmentView : MonoBehaviour, IEquipmentView
    {
        public const string LoadingText = "読み込み中…";
        public const string LoadFailedText = "取得できませんでした";
        public const string LoadFailedMessage = "装備を取得できませんでした";

        public PartyMockData Party;

        // 通知は案内人の画面の通知の帯に出す。
        public GuideMenuView Guide;

        // 左上の仲間のタブ。
        public PartyTabStrip People = new();

        // 武器・防具の順の2つの枠。
        public EquipmentSlotWidget[] Slots = Array.Empty<EquipmentSlotWidget>();

        // 選んでいる人の立ち姿（64×64を4倍）。
        public RawImage Figure;

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
        private CancellationTokenSource loading;
        private readonly List<(Button Button, UnityEngine.Events.UnityAction Action)> bindings =
            new();

        public event Action<string> PersonPressed;
        public event Action<int> SlotPressed;
        public event Action<string> ItemPressed;
        public event Action RemovePressed;

        public EquipmentPresenter Presenter => presenter;
        public string LastNotice { get; private set; } = "";

        // 実行中または直前の読み込み。テストで完了を待つために公開する。
        public Task LoadTask { get; private set; } = Task.CompletedTask;

        private void OnEnable()
        {
            foreach (var person in People.Tabs)
            {
                string id = person.Id;
                Bind(person.Button, () => PersonPressed?.Invoke(id));
            }
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                Bind(Slots[i].Button, () => SlotPressed?.Invoke(index));
            }
            foreach (var row in Rows)
                BindRow(row);
            Bind(Remove, () => RemovePressed?.Invoke());

            // 開くたびに、サーバー（なければアプリを動かしている間）の編成と装備で描き直す。
            presenter?.Dispose();
            presenter = null;
            People.Forget();
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            if (Party == null)
                return;
            var party = PartySession.Source;
            var equipment = EquipmentSession.SourceOrLocal;
            if (party != null || EquipmentSession.Source != null)
                ShowLoading();
            loading = new CancellationTokenSource();
            LoadTask = LoadAsync(party, equipment, loading.Token);
        }

        private async Task LoadAsync(
            IPartySource party,
            IEquipmentSource equipment,
            CancellationToken token
        )
        {
            try
            {
                // 同じセッションを順に使う（ログインのやり直しを重ねない）。
                var formation =
                    party != null
                        ? PartySession.Use(Party, await party.LoadAsync(token))
                        : PartySession.Formation(Party);
                var state = await equipment.LoadAsync(token);
                if (token.IsCancellationRequested)
                    return;
                var (people, partyCount) = formation.TabOrder();
                presenter = new EquipmentPresenter(
                    this,
                    people,
                    partyCount,
                    state,
                    equipment,
                    EquipmentSession.Selected,
                    id => EquipmentSession.Selected = id
                );
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (token.IsCancellationRequested)
                    return;
                Debug.LogWarning("装備を取得できませんでした。" + exception.Message, this);
                Set(Count, LoadFailedText);
                ShowNotice(LoadFailedMessage);
            }
        }

        // 読み込むまで、仮データの装備を本当の装備として見せない。
        private void ShowLoading()
        {
            Set(Count, LoadingText);
            People.Hide();
            foreach (var widget in Slots)
                ShowSlot(widget, null, null, false);
            if (Figure != null)
                Figure.enabled = false;
            foreach (var row in Rows)
                row.gameObject.SetActive(false);
            if (Remove != null)
                Remove.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            loading?.Cancel();
            loading?.Dispose();
            loading = null;
            foreach (var (button, action) in bindings)
                if (button != null)
                    button.onClick.RemoveListener(action);
            bindings.Clear();
            presenter?.Dispose();
            presenter = null;
        }

        public void Render(EquipmentViewState state)
        {
            People.Show(state.People, state.PartyCount, state.Person);
            for (int i = 0; i < Slots.Length; i++)
                ShowSlot(
                    Slots[i],
                    i < state.Slots.Count ? state.Slots[i] : null,
                    IconOf((EquipmentSlot)i),
                    i == (int)state.Slot
                );
            ShowFigure(Figure, Party != null ? Party.Find(state.PersonId) : null);
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

        /// <summary>The person's standing figure, with their tint and facing.</summary>
        public static void ShowFigure(RawImage figure, PartyMember member)
        {
            if (figure == null)
                return;
            figure.enabled = member != null && member.Art != null;
            if (!figure.enabled)
                return;
            figure.texture = member.Art;
            figure.color = member.Tint;
            figure.uvRect = member.Flip ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
        }

        private void BindRow(EquipmentRow row)
        {
            if (row == null)
                return;
            Bind(row.Button, () => ItemPressed?.Invoke(row.ItemId));
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;
            button.onClick.AddListener(action);
            bindings.Add((button, action));
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }
    }
}
