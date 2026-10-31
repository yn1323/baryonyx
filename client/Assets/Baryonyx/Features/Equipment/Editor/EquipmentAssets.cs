using System.Linq;
using Baryonyx.Editor.UI;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.Training.Editor;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using ChangeScreen = Baryonyx.Party.Editor.PartyChangeScreenAssets;
using Frame = Baryonyx.UI.GuideMenu.Editor.GuideFrameAssets;
using GlassCardAssets = Baryonyx.UI.Editor.GlassCardAssets;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Equipment.Editor
{
    /// <summary>
    /// The adventurer's equipment tab of the formation. The formation's generator calls
    /// <see cref="BuildPanel"/> for its equipment item, so the people's column and the tabs, the
    /// weapon, armour and accessory slots, the stats and the item rows are baked into the
    /// formation prefab and read in the editor without Play Mode. The items are the mock catalog's
    /// (doc/features/equipment.md); the people are the party's mock data.
    /// </summary>
    public static class EquipmentAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Equipment";
        public const string IconWeaponPath = Folder + "/UI/Art/IconWeapon.aseprite";
        public const string IconArmorPath = Folder + "/UI/Art/IconArmor.aseprite";

        // 枠つきの表示（GuideFramedPanel）で、人の列とタブの間の左の面にステータスと枠、金の2本線の
        // 右の面に持っている装備を置く（PartyChangeScreenAssets）。右の行の効果と増減が入るよう、
        // 左の面を狭くする。設計座標、左上から。
        private const float Split = 760f;
        private static readonly Rect Left = ChangeScreen.LeftOf(Split);

        // 左は上からステータス・5つの枠。ステータスの下から面の下の余白までを、武器・防具・アクセサリー3つの
        // 5つの枠で等分する（2026-10-31、ユーザーの指示）。
        private const float SlotTop = ChangeScreen.StatsLineTop + 16f;
        private const float SlotGap = 12f;
        private const float SlotBottomMargin = 24f;
        private const float SlotHeight =
            (ChangeScreen.Height - SlotBottomMargin - SlotTop - SlotGap * 4) / 5f;
        private const float SlotStep = SlotHeight + SlotGap;

        // 枠の文字は、枠の名前・装備の名前と★・効果の3行。3行のまとまりを縦の中央に置く。
        private const float SlotTagHeight = 26f;
        private const float SlotNameHeight = 40f;
        private const float SlotDetailHeight = 32f;
        private const float SlotInset =
            (SlotHeight - SlotTagHeight - SlotNameHeight - 6f - SlotDetailHeight) / 2f;
        private const float SlotTextLeft = 112f;
        private const float RowHeight = 128f;
        private const float RowTextLeft = 132f;

        // 一覧は面の上から始める。武器・防具・アクセサリーは左の枠で選び、上にタブを置かない
        // （横に並んだタブは押しづらい。doc/rules/frontend-design.md）。
        private const float ListTop = 12f;

        // 持っている装備の行を、この数だけ作り込む。足りなければ、ビューが実行中に写して増やす。
        private const int BakedRows = 8;

        // 一覧の行の右に出すステータスの増減の幅。
        private const float ChangeWidth = 340f;

        /// <summary>
        /// Builds the equipment over the guide screen's safe area: the person, their slots and
        /// stats in the left frame, the owned items for the chosen slot in the right frame.
        /// Called while the guide screen's prefab is being built, so the shared frames and font
        /// are ready.
        /// </summary>
        public static GameObject BuildPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var training = TrainingAssets.CreateData();
            var (people, _) = PartyFormation.From(party).TabOrder();

            var (view, layer) = Frame.FramedPanel<EquipmentView>(safe, "Equipment");
            view.Party = party;
            view.Training = training;
            view.Guide = guide;
            view.WeaponIcon = Guide.Icon(IconWeaponPath);
            view.ArmorIcon = Guide.Icon(IconArmorPath);
            view.ItemArt = Baryonyx.Inventory.Editor.ItemArtAssets.Load();

            RectTransform left,
                right;
            (left, right, view.Swipe) = ChangeScreen.Build(layer, Split, "ItemsPane", "Items");
            view.Slots = new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor }
                .Select(slot => BuildSlot(left, slot))
                .ToArray();
            view.Accessories = Enumerable
                .Range(0, AccessorySlots.Count)
                .Select(i => BuildAccessory(left, i))
                .ToArray();
            // ステータスは左の上に置き、その下に線を引いて枠と分ける。
            view.Stats = ChangeScreen.Stats(left);
            BuildItems(right, view);
            view.Sheet = PartySheetAssets.Build(
                layer,
                guide,
                party,
                EquipmentSession.GuideItemKey,
                TrainingAssets.SheetTabs
            );

            // 仮データの最初の人と仮の装備で描き、Prefabを開くと停止中でも文字と絵が読めるようにする。
            new EquipmentPresenter(
                view,
                people,
                EquipmentLocalSource.Starter(),
                new EquipmentLocalSource(),
                levelOf: id => party.Find(id)?.Level ?? 1,
                statsOf: view.BaseStatsOf
            ).Dispose();
            return view.gameObject;
        }

        // --- Left: the chosen person's weapon, armour and the accessory slots ---------------

        private static EquipmentSlotWidget BuildSlot(RectTransform panel, EquipmentSlot slot)
        {
            int index = (int)slot;
            var (button, selected, row) = GlassCardAssets.Choice(
                SlotBox(panel, "Slot_" + slot, index),
                pattern: false
            );
            var widget = new EquipmentSlotWidget { Button = button, Selected = selected };

            // 24×24のアイコンを3倍で置き、右に枠の名前（武器・防具）、装備の名前と★、その効果。
            var icon = Layout.Box(row, "Icon", 18, (SlotHeight - 72) / 2f, 72, 72);
            widget.Icon = icon.gameObject.AddComponent<Image>();
            widget.Icon.raycastTarget = false;
            widget.Foil = CardFoilAssets.Add(widget.Icon);
            SlotTag(row, EquipmentCatalog.NameOf(slot));
            (widget.Name, widget.Stars, widget.Detail) = SlotTexts(row);
            return widget;
        }

        // アクセサリーの枠。武器・防具と同じ高さと並びで、付けていれば絵・名前・★・効果を出す。
        // 開いていなければ錠前と「Lv 20で解放」を出す。
        private static EquipmentAccessoryWidget BuildAccessory(RectTransform panel, int index)
        {
            var (button, selected, row) = GlassCardAssets.Choice(
                SlotBox(panel, "Accessory" + index, 2 + index),
                dashed: true,
                pattern: false
            );
            var widget = new EquipmentAccessoryWidget { Button = button, Selected = selected };
            var icon = Layout.Box(row, "Icon", 18, (SlotHeight - 72) / 2f, 72, 72);
            widget.Icon = icon.gameObject.AddComponent<Image>();
            widget.Icon.raycastTarget = false;
            widget.Foil = CardFoilAssets.Add(widget.Icon);
            var lockImage = Layout.Box(
                row,
                "Lock",
                18 + (72 - 36) / 2f,
                (SlotHeight - 40) / 2f,
                36,
                40
            );
            SpriteImage(lockImage, Frame.LockPath, Color.white);
            widget.Lock = lockImage.gameObject;
            SlotTag(row, EquipmentPresenter.AccessoryTitle);
            (widget.Name, widget.Stars, widget.Detail) = SlotTexts(row);
            widget.Name.text = AccessorySlots.UnlockText(index);
            return widget;
        }

        // 左の5つの枠の場所。ステータスの下から面の下までを5等分する。
        private static RectTransform SlotBox(RectTransform panel, string name, int index) =>
            Layout.Box(panel, name, 0, SlotTop + index * SlotStep, Left.width, SlotHeight);

        // 枠の上の行の、枠の名前（武器・防具・アクセサリー）。
        private static void SlotTag(RectTransform row, string tag) =>
            Layout.Text(
                row,
                "Tag",
                tag,
                20,
                UiPalette.Gold,
                TextAlignmentOptions.BottomLeft,
                SlotTextLeft,
                SlotInset,
                300,
                SlotTagHeight
            );

        // 枠の名前の下の、装備の名前と右端の★・品質、その下の効果（「属攻 +20%｜会心 +8%」）。
        private static (TMP_Text Name, TMP_Text Stars, TMP_Text Detail) SlotTexts(RectTransform row)
        {
            float top = SlotInset + SlotTagHeight;
            var name = Layout.Text(
                row,
                "Name",
                "",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft,
                SlotTextLeft,
                top,
                Left.width - SlotTextLeft - 170,
                SlotNameHeight
            );
            Guide.Shrink(name, 22);
            var stars = Layout.Text(
                row,
                "Stars",
                "",
                26,
                UiPalette.Gold,
                TextAlignmentOptions.BottomRight,
                Left.width - 170,
                top,
                150,
                SlotNameHeight
            );
            // 長い効果（「攻撃スキルが10%で2回発動」など）も1行に収まるよう縮めて入れる。
            var detail = Layout.Text(
                row,
                "Detail",
                "",
                24,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft,
                SlotTextLeft,
                top + SlotNameHeight + 6,
                Left.width - SlotTextLeft - 24,
                SlotDetailHeight
            );
            Guide.Shrink(detail, 16);
            return (name, stars, detail);
        }

        // --- Right: the owned items for the chosen slot, then "外す" -------------------------

        private static void BuildItems(RectTransform panel, EquipmentView view)
        {
            var (viewport, content) = ChangeScreen.List(panel, ListTop);
            view.Rows = Enumerable.Range(0, BakedRows).Select(i => BuildRow(content, i)).ToArray();
            (view.Remove, view.RemoveLabel) = BuildRemove(content);
            view.List = Layout.Scroll(panel, viewport, content);

            // 読み込み中と、読めなかったときだけ出す文字。
            view.Status = ChangeScreen.Status(panel);
        }

        private static EquipmentRow BuildRow(RectTransform content, int index)
        {
            var rect = Layout.ListItem(content, "Item" + index, RowHeight);
            var row = rect.gameObject.AddComponent<EquipmentRow>();
            var (button, _, card) = GlassCardAssets.Choice(rect, pattern: false);
            row.Button = button;

            var icon = Rect("Icon", card);
            Corner(icon, new Vector2(0, 0.5f), new Vector2(16, 0), Vector2.one * Guide.IconSize);
            row.Icon = icon.gameObject.AddComponent<Image>();
            row.Icon.raycastTarget = false;
            row.Foil = CardFoilAssets.Add(row.Icon);

            row.Name = Label(
                card,
                "Name",
                "",
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft
            );
            Guide.Band((RectTransform)row.Name.transform, top: true, 12, 44, RowTextLeft, 300);
            Guide.Shrink(row.Name, 22);
            row.Stars = Label(
                card,
                "Stars",
                "",
                26,
                UiPalette.Gold,
                TextAlignmentOptions.BottomLeft
            );
            Guide.Band(
                (RectTransform)row.Stars.transform,
                top: true,
                12,
                44,
                RowTextLeft + 290,
                140
            );
            row.Detail = Label(
                card,
                "Detail",
                "",
                24,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft
            );
            Guide.Band(
                (RectTransform)row.Detail.transform,
                top: true,
                64,
                40,
                RowTextLeft,
                ChangeWidth + 24
            );
            Guide.Shrink(row.Detail, 18);
            // 効果の右に、選んでいる枠に付けたときのステータスの増減。
            row.Change = Label(
                card,
                "Change",
                "",
                24,
                UiPalette.TextMain,
                TextAlignmentOptions.TopRight
            );
            row.Change.richText = true;
            Corner(
                (RectTransform)row.Change.transform,
                Vector2.one,
                new Vector2(-24, -64),
                new Vector2(ChangeWidth, 40)
            );
            Guide.Shrink(row.Change, 18);
            row.Mark = Label(card, "Mark", "", 24, UiPalette.Gold, TextAlignmentOptions.TopRight);
            Guide.Band((RectTransform)row.Mark.transform, top: true, 20, 32, RowTextLeft, 24);
            row.Mark.gameObject.SetActive(false);
            return row;
        }

        // 選んでいる枠の装備を外す行。リストの最後に置く。
        private static (Button Button, TMP_Text Label) BuildRemove(RectTransform content)
        {
            var rect = Layout.ListItem(content, "Remove", RowHeight);
            var (button, _, card) = GlassCardAssets.Choice(rect, dashed: true);
            var label = Label(
                card,
                "Label",
                EquipmentPresenter.RemoveLabel,
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)label.transform);
            return (button, label);
        }
    }
}
