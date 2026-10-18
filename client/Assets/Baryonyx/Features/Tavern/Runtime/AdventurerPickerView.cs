using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Equipment;
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
    /// <summary>One character's card on the equipment's picker, baked by the generator for every mock character.</summary>
    [Serializable]
    public sealed class AdventurerPickWidget
    {
        public string Id = "";
        public GlassCard Card;
        public TMP_Text Level;

        // パーティにいる人だけに出す「パーティ」の札。
        public GameObject PartyTag;

        // 付けている武器・防具の名前。空いていれば「なし」と書き、アイコンを薄くする。
        public Image WeaponIcon;
        public TMP_Text Weapon;
        public Image ArmorIcon;
        public TMP_Text Armor;
    }

    /// <summary>
    /// The formation's equipment item: every owned character as a card (the party first, then
    /// the bench), with the weapon and armour they wear. Tapping one opens their page (the
    /// training item), where the equipment slots change it; back on the page returns here.
    /// AdventurerPickerAssets bakes the cards into the formation prefab, so they read in the
    /// editor; this view orders them and fills in the levels and equipment. With a server
    /// (<see cref="PartySession.Source"/>) it reads the party on opening; otherwise it uses the
    /// mock data and the equipment kept while the app runs.
    /// </summary>
    public sealed class AdventurerPickerView : MonoBehaviour, IEditModePreview
    {
        // 編成のメニューの「装備」の項目のキー。リストの代わりにこの人選びを開く。
        public const string GuideItemKey = "gear";

        public const string LoadingText = GuidePanelLoad.LoadingText;
        public const string LoadFailedText = GuidePanelLoad.LoadFailedText;
        public const string LoadFailedMessage = "編成を取得できませんでした";
        public const string EmptyText = "なし";

        // 付けていない枠のアイコンの濃さ。
        private const float EmptyAlpha = 0.3f;

        public PartyMockData Party;

        // 通知は案内人の画面の通知の帯に出し、人を押すと案内人の画面で個別の画面を開く。
        public GuideMenuView Guide;

        public TMP_Text Status;
        public AdventurerPickWidget[] Cards = Array.Empty<AdventurerPickWidget>();
        public ScrollRect List;

        private readonly GuidePanelLoad load = new();
        private readonly List<(GlassCard Card, Action Pressed)> bindings = new();
        private bool ready;

        public string LastNotice { get; private set; } = "";

        // 実行中または直前の読み込み。テストで完了を待つために公開する。
        public Task LoadTask => load.Task;

        private void OnEnable()
        {
            foreach (var widget in Cards)
            {
                if (widget.Card == null)
                    continue;
                string id = widget.Id;
                Action pressed = () => Pick(id);
                widget.Card.Clicked += pressed;
                bindings.Add((widget.Card, pressed));
            }

            // 開くたびに、サーバーの編成と装備（なければアプリを動かしている間のもの）で描き直す。
            ready = false;
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

        private void OnDisable()
        {
            load.Cancel();
            foreach (var (card, pressed) in bindings)
                card.Clicked -= pressed;
            bindings.Clear();
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
            var equipment = await gear.LoadAsync(token);
            if (token.IsCancellationRequested)
                return;
            Render(formation, equipment);
            ready = true;
        }

        private void LoadFailed(Exception exception)
        {
            Debug.LogWarning("編成を取得できませんでした。" + exception.Message, this);
            ShowLoadFailed();
            ShowNotice(LoadFailedMessage);
        }

        private void ShowLoadFailed() => Set(Status, LoadFailedText);

        // 読み込むまで、仮データの編成と装備を本当のものとして見せない。
        private void ShowLoading()
        {
            Set(Status, LoadingText);
            foreach (var widget in Cards)
                if (widget.Card != null)
                    widget.Card.gameObject.SetActive(false);
        }

        // 停止中の表示の切り替え：サーバーから読み込む間と、読めなかったときの表示。
        public IReadOnlyList<string> PreviewStates => GuidePanelLoad.PreviewStates;

        public void ShowPreviewState(int index)
        {
            ShowLoading();
            if (index == 1)
                ShowLoadFailed();
        }

        /// <summary>
        /// Shows the party first, in slot order, then the bench, each with their level and what
        /// they wear. The generator calls it too, with the mock data, so the prefab reads.
        /// </summary>
        public void Render(PartyFormation formation, EquipmentState equipment)
        {
            Set(Status, "");
            var order = Order(formation);
            var place = new Dictionary<string, int>();
            for (int i = 0; i < order.Count; i++)
                place[order[i]] = i;
            foreach (var widget in Cards)
            {
                if (widget.Card == null)
                    continue;
                bool shown = place.TryGetValue(widget.Id, out int index);
                widget.Card.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                widget.Card.transform.SetSiblingIndex(index);
                widget.Card.Selected = false;
                if (widget.PartyTag != null)
                    widget.PartyTag.SetActive(formation.SlotOf(widget.Id) >= 0);
                Set(widget.Level, PartySession.LevelOf(Party, widget.Id).ToString());
                ShowGear(
                    widget.WeaponIcon,
                    widget.Weapon,
                    equipment,
                    widget.Id,
                    EquipmentSlot.Weapon
                );
                ShowGear(widget.ArmorIcon, widget.Armor, equipment, widget.Id, EquipmentSlot.Armor);
            }
        }

        /// <summary>The characters' ids in the order they are shown: the party, then the bench.</summary>
        public static IReadOnlyList<string> Order(PartyFormation formation) =>
            Enumerable
                .Range(0, formation.SlotCount)
                .Select(formation.Member)
                .Where(id => id != null)
                .Concat(formation.Bench.Select(member => member.Id))
                .ToArray();

        /// <summary>The name of what the character wears in the slot, or <see cref="EmptyText"/>.</summary>
        public static string GearName(EquipmentState equipment, string id, EquipmentSlot slot)
        {
            var item = equipment?.Find(equipment.ItemIn(id, slot));
            return EquipmentCatalog.Find(item?.EquipmentId)?.Name ?? EmptyText;
        }

        private static void ShowGear(
            Image icon,
            TMP_Text label,
            EquipmentState equipment,
            string id,
            EquipmentSlot slot
        )
        {
            string name = GearName(equipment, id, slot);
            Set(label, name);
            if (icon != null)
                icon.color = new Color(1f, 1f, 1f, name == EmptyText ? EmptyAlpha : 1f);
        }

        /// <summary>Opens the character's page; back on the page returns to this picker.</summary>
        public void Pick(string id)
        {
            if (!ready || Guide == null || !Guide.HasItem(TrainingSession.GuideItemKey))
                return;
            PartySession.Selected = id;
            TrainingSession.ReturnKey = GuideItemKey;
            Guide.OpenItem(TrainingSession.GuideItemKey);
        }

        public void ShowNotice(string message)
        {
            LastNotice = message ?? "";
            if (Guide != null)
                Guide.ShowToast(message);
        }
    }
}
