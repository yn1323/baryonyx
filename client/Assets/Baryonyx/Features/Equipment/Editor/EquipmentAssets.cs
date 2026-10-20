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
    /// The formation's equipment change screen. The formation's generator calls
    /// <see cref="BuildPanel"/> for its equipment item, so the person, the weapon, armour and
    /// accessory slots, the stats and the item rows are baked into the formation prefab and
    /// read in the editor without Play Mode. The items are the mock catalog's
    /// (doc/features/equipment.md); the people are the party's mock data.
    /// </summary>
    public static class EquipmentAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Equipment";
        public const string IconWeaponPath = Folder + "/UI/Art/IconWeapon.aseprite";
        public const string IconArmorPath = Folder + "/UI/Art/IconArmor.aseprite";

        // 枠つきの表示（GuideFramedPanel）で、左の面に人・枠・ステータス、金の2本線の右の面に
        // 持っている装備を置く（PartyChangeScreenAssets）。設計座標、左上から。
        private static readonly Rect Left = ChangeScreen.Left;
        private static readonly Rect Right = ChangeScreen.Right;

        // 左は上から人・ステータス・5つの枠。枠は面の下端（設計座標で1036）まで並べる。
        private const float SlotTop = 280f;
        private const float SlotHeight = 118f;
        private const float SlotStep = 130f;

        // 枠の文字は、高さ100の枠のときの位置から、縦の中央へずらして置く。
        private const float SlotInset = (SlotHeight - 100f) / 2f;
        private const float SlotTextLeft = 112f;
        private const float RowHeight = 128f;
        private const float RowTextLeft = 132f;

        // タブの下から一覧を始める。
        private const float ListTop = 100f;

        // 持っている装備の行を、この数だけ作り込む。足りなければ、ビューが実行中に写して増やす。
        private const int BakedRows = 8;

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

            RectTransform left,
                right;
            (left, right, view.Swipe, view.Person) = ChangeScreen.Build(
                layer,
                "ItemsPane",
                "Items",
                people.FirstOrDefault()
            );
            view.Slots = new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor }
                .Select(slot => BuildSlot(left, slot))
                .ToArray();
            view.Accessories = Enumerable
                .Range(0, AccessorySlots.Count)
                .Select(i => BuildAccessory(left, i))
                .ToArray();
            // ステータスは人のすぐ下に置き、その下に線を引いて枠と分ける。
            view.Stats = ChangeScreen.Stats(left);
            BuildItems(right, view);

            // 仮データの最初の人と仮の装備で描き、Prefabを開くと停止中でも文字と絵が読めるようにする。
            new EquipmentPresenter(
                view,
                people,
                EquipmentLocalSource.Starter(),
                new EquipmentLocalSource()
            ).Dispose();
            return view.gameObject;
        }

        // --- Left: the chosen person's weapon, armour and the accessory slots ---------------

        private static EquipmentSlotWidget BuildSlot(RectTransform panel, EquipmentSlot slot)
        {
            int index = (int)slot;
            var (button, selected, row) = GlassCardAssets.Choice(
                Layout.Box(
                    panel,
                    "Slot_" + slot,
                    24,
                    SlotTop + index * SlotStep,
                    Left.width - 48,
                    SlotHeight
                ),
                pattern: false
            );
            var widget = new EquipmentSlotWidget { Button = button, Selected = selected };

            // 24×24のアイコンを3倍で置き、右に枠の名前（武器・防具）と装備の名前・★。
            var icon = Layout.Box(row, "Icon", 18, (SlotHeight - 72) / 2f, 72, 72);
            widget.Icon = icon.gameObject.AddComponent<Image>();
            widget.Icon.raycastTarget = false;
            widget.Foil = CardFoilAssets.Add(widget.Icon);
            Layout.Text(
                row,
                "Tag",
                EquipmentCatalog.NameOf(slot),
                20,
                UiPalette.Gold,
                TextAlignmentOptions.TopLeft,
                SlotTextLeft,
                10 + SlotInset,
                200,
                26
            );
            widget.Name = Layout.Text(
                row,
                "Name",
                "",
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft,
                SlotTextLeft,
                38 + SlotInset,
                Left.width - 48 - SlotTextLeft - 150,
                46
            );
            Guide.Shrink(widget.Name, 22);
            widget.Stars = Layout.Text(
                row,
                "Stars",
                "",
                26,
                UiPalette.Gold,
                TextAlignmentOptions.BottomRight,
                Left.width - 48 - 170,
                38 + SlotInset,
                150,
                46
            );
            return widget;
        }

        // アクセサリーの枠。押すとアクセサリーのタブに移る。開いていなければ錠前と「Lv 15で解放」を出す。
        private static EquipmentAccessoryWidget BuildAccessory(RectTransform panel, int index)
        {
            var (button, selected, row) = GlassCardAssets.Choice(
                Layout.Box(
                    panel,
                    "Accessory" + index,
                    24,
                    SlotTop + (2 + index) * SlotStep,
                    Left.width - 48,
                    SlotHeight
                ),
                dashed: true,
                pattern: false
            );
            var widget = new EquipmentAccessoryWidget { Button = button, Selected = selected };
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
            Layout.Text(
                row,
                "Tag",
                EquipmentPresenter.AccessoryTitle,
                20,
                UiPalette.Gold,
                TextAlignmentOptions.TopLeft,
                SlotTextLeft,
                10 + SlotInset,
                300,
                26
            );
            widget.Name = Layout.Text(
                row,
                "Name",
                AccessorySlots.UnlockText(index),
                28,
                UiPalette.TextSub,
                TextAlignmentOptions.BottomLeft,
                SlotTextLeft,
                38 + SlotInset,
                Left.width - 48 - SlotTextLeft - 24,
                46
            );
            return widget;
        }

        // --- Right: the owned items for the chosen slot, then "外す" -------------------------

        private static void BuildItems(RectTransform panel, EquipmentView view)
        {
            // 上に、面の幅いっぱいの武器・防具・アクセサリーのタブ。左の枠と連動する。
            view.Tabs = ChangeScreen.Tabs(
                panel,
                EquipmentCatalog.NameOf(EquipmentSlot.Weapon),
                EquipmentCatalog.NameOf(EquipmentSlot.Armor),
                "アクセサリー"
            );

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
            Guide.Band((RectTransform)row.Detail.transform, top: true, 64, 40, RowTextLeft, 24);
            Guide.Shrink(row.Detail, 18);
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
