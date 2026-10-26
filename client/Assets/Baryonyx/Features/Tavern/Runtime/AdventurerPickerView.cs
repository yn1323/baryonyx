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
    /// <summary>One character's card on the growth picker, baked by the generator for every mock character.</summary>
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
    /// The formation's growth item (育成): every owned character as a card (the party first, then
    /// the bench), with the weapon and armour they wear. Tapping one opens their page (the
    /// training item: levels, skills and equipment); back on the page returns here.
    /// AdventurerPickerAssets bakes the cards into the formation prefab, so they read in the
    /// editor; this view orders them and fills in the levels and equipment. With a server
    /// (<see cref="PartySession.Source"/>) it reads the party on opening; otherwise it uses the
    /// mock data and the equipment kept while the app runs.
    /// </summary>
    public sealed class AdventurerPickerView : GuidePanel, IEditModePreview
    {
        // 編成のメニューの「育成」の項目のキー。リストの代わりにこの人選びを開く（冒険中の探索からも開く）。
        public const string GuideItemKey = "gear";

        public const string LoadFailedMessage = "編成を取得できませんでした";
        public const string EmptyText = "なし";

        // 付けていない枠のアイコンの濃さ。
        private const float EmptyAlpha = 0.3f;

        // 読み込む間に伏せて並べるカードの数（3列×2段で一覧の見える所を埋める）。
        public const int CoverCards = 6;

        public PartyMockData Party;

        public TMP_Text Status;
        public AdventurerPickWidget[] Cards = Array.Empty<AdventurerPickWidget>();
        public ScrollRect List;

        private readonly List<(GlassCard Card, Action Pressed)> bindings = new();
        private readonly GlassCardCover cover = new();
        private bool ready;

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

            // 開くたびに、サーバーの編成と装備（なければアプリを動かしている間のもの）を読み直す。
            ready = false;
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            if (Party == null)
                return;
            var source = PartySession.Source;
            // 前に読んだ編成と装備があれば、すぐ出して裏で読み直す。なければ読み終えるまで仮データを隠す。
            var knownParty = PartySession.Known;
            var knownGear = EquipmentSession.Known;
            bool shown = knownParty != null && knownGear != null;
            if (shown)
            {
                Render(knownParty, knownGear);
                ready = true;
            }
            else if (source != null || EquipmentSession.Source != null)
                ShowLoading();
            StartLoad(
                token => LoadAsync(source, EquipmentSession.SourceOrLocal, shown, token),
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

        private void OnDisable()
        {
            Load.Cancel();
            foreach (var (card, pressed) in bindings)
                card.Clicked -= pressed;
            bindings.Clear();
        }

        private async Task LoadAsync(
            IPartySource source,
            IEquipmentSource gear,
            bool shown,
            CancellationToken token
        )
        {
            string shownParty = PartySession.ReadVersion;
            string shownGear = EquipmentSession.ReadVersion;
            // 編成と装備は同時に読む。ログインは AccountSessionRunner が1回にまとめる。
            var reading = PartySession.LoadAsync(Party, source, token);
            var wearing = gear.LoadAsync(token);
            await Task.WhenAll(reading, wearing);
            if (token.IsCancellationRequested)
                return;
            if (
                shown
                && Unchanged(shownParty, PartySession.ReadVersion)
                && Unchanged(shownGear, EquipmentSession.ReadVersion)
            )
                return;
            Render(await reading, await wearing);
            ready = true;
        }

        private void ShowLoadFailed()
        {
            cover.Clear();
            foreach (var widget in Cards)
                if (widget.Card != null)
                    widget.Card.gameObject.SetActive(false);
            Set(Status, LoadFailedText);
        }

        // 読み込むまで、仮データの編成と装備を本当のものとして見せない。一覧の見える所はカードの裏で埋める。
        private void ShowLoading()
        {
            Set(Status, "");
            foreach (var widget in Cards)
                if (widget.Card != null)
                    widget.Card.gameObject.SetActive(false);
            cover.Cover(
                Cards
                    .Where(widget => widget.Card != null)
                    .OrderBy(widget => widget.Card.transform.GetSiblingIndex())
                    .Take(CoverCards)
                    .Select(widget => widget.Card.gameObject)
            );
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
            // 読み込みを待っていたら、並べた順にめくる。人のいない場所の伏せたカードも、めくってから消す。
            var shownCards = Cards
                .Where(widget => widget.Card != null && widget.Card.gameObject.activeSelf)
                .OrderBy(widget => widget.Card.transform.GetSiblingIndex())
                .Select(widget => widget.Card.gameObject)
                .ToArray();
            var empty = cover.Covered
                ? Cards
                    .Where(widget => widget.Card != null && !widget.Card.gameObject.activeSelf)
                    .Take(CoverCards - shownCards.Length)
                    .Select(widget => widget.Card.gameObject)
                    .ToArray()
                : Array.Empty<GameObject>();
            foreach (var card in empty)
                card.transform.SetAsLastSibling();
            cover.Reveal(shownCards, empty);
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
            if (ready)
                TrainingSession.OpenPage(Guide, id, GuideItemKey);
        }
    }
}
