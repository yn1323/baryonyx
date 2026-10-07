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
        public const string IconChangeGearPath = Folder + "/UI/Art/IconChangeGear.aseprite";
        public const string IconWeaponPath = Folder + "/UI/Art/IconWeapon.aseprite";
        public const string IconArmorPath = Folder + "/UI/Art/IconArmor.aseprite";

        // 左の枠（人・枠・ステータス）と右の枠（持っている装備）。設計座標、左上から。
        private static readonly Rect Left = new(40, 200, 740, 850);
        private static readonly Rect Right = new(800, 200, 1080, 850);
        private const float SlotTop = 154f;
        private const float SlotHeight = 100f;
        private const float SlotStep = 110f;
        private const float SlotTextLeft = 112f;
        private const int AccessorySlots = 3;
        private const float RowHeight = 128f;
        private const float RowTextLeft = 132f;

        // 持っている装備の行を、この数だけ作り込む。足りなければ、ビューが実行中に写して増やす。
        private const int BakedRows = 8;
        private static readonly Color Locked = new(0.62f, 0.62f, 0.68f, 0.85f);

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

            var root = Rect("Equipment", safe);
            Stretch(root);
            var view = root.gameObject.AddComponent<EquipmentView>();
            view.Party = party;
            view.Training = training;
            view.Guide = guide;
            view.WeaponIcon = Guide.Icon(IconWeaponPath);
            view.ArmorIcon = Guide.Icon(IconArmorPath);

            var layer = Layout.Layer(root, "Layout");
            var left = Layout.FrameBox(layer, "Person", Left.x, Left.y, Left.width, Left.height);
            var right = Layout.FrameBox(
                layer,
                "Items",
                Right.x,
                Right.y,
                Right.width,
                Right.height
            );

            view.Person = PartyHeaderAssets.Build(left, 10, Left.width, people.FirstOrDefault());
            Layout.Line(left, "HeaderLine", 24, 142, Left.width - 48, 2);
            view.Slots = new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor }
                .Select(slot => BuildSlot(left, slot))
                .ToArray();
            view.Accessories = Enumerable
                .Range(0, AccessorySlots)
                .Select(i => BuildAccessory(left, i))
                .ToArray();
            float statsTop = SlotTop + (2 + AccessorySlots) * SlotStep + 6;
            Layout.Line(left, "StatsLine", 24, statsTop, Left.width - 48, 2);
            view.Stats = TrainingAssets.BuildStats(left, 30, statsTop + 14, Left.width - 60, 4);
            BuildItems(right, view);

            // 仮データの最初の人と仮の装備で描き、Prefabを開くと停止中でも文字と絵が読めるようにする。
            new EquipmentPresenter(
                view,
                people,
                EquipmentLocalSource.Starter(),
                new EquipmentLocalSource()
            ).Dispose();
            return root.gameObject;
        }

        // --- Left: the chosen person's weapon, armour and the accessory slots ---------------

        private static EquipmentSlotWidget BuildSlot(RectTransform panel, EquipmentSlot slot)
        {
            int index = (int)slot;
            var (button, selected) = Layout.Choice(
                panel,
                "Slot_" + slot,
                24,
                SlotTop + index * SlotStep,
                Left.width - 48,
                SlotHeight
            );
            var row = (RectTransform)button.transform;
            var widget = new EquipmentSlotWidget { Button = button, Selected = selected };

            // 24×24のアイコンを3倍で置き、右に枠の名前（武器・防具）と装備の名前・★。
            var icon = Layout.Box(row, "Icon", 18, (SlotHeight - 72) / 2f, 72, 72);
            widget.Icon = icon.gameObject.AddComponent<Image>();
            widget.Icon.raycastTarget = false;
            Layout.Text(
                row,
                "Tag",
                EquipmentCatalog.NameOf(slot),
                20,
                UiPalette.Gold,
                TextAlignmentOptions.TopLeft,
                SlotTextLeft,
                10,
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
                38,
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
                38,
                150,
                46
            );
            return widget;
        }

        // まだ付けられないアクセサリーの枠。押せず、錠前と「準備中」だけを出す。
        private static GameObject BuildAccessory(RectTransform panel, int index)
        {
            var row = Layout.Box(
                panel,
                "Accessory" + index,
                24,
                SlotTop + (2 + index) * SlotStep,
                Left.width - 48,
                SlotHeight
            );
            Guide.Frame(row, Guide.FramePath, Locked).raycastTarget = false;
            var lockImage = Layout.Box(
                row,
                "Lock",
                18 + (72 - 36) / 2f,
                (SlotHeight - 40) / 2f,
                36,
                40
            );
            SpriteImage(lockImage, TrainingAssets.LockPath, Color.white);
            Layout.Text(
                row,
                "Tag",
                "アクセサリー",
                20,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft,
                SlotTextLeft,
                10,
                300,
                26
            );
            Layout.Text(
                row,
                "Soon",
                "準備中",
                28,
                UiPalette.TextSub,
                TextAlignmentOptions.BottomLeft,
                SlotTextLeft,
                38,
                300,
                46
            );
            return row.gameObject;
        }

        // --- Right: the owned items for the chosen slot, then "外す" -------------------------

        private static void BuildItems(RectTransform panel, EquipmentView view)
        {
            view.Title = Label(
                panel,
                "Title",
                "",
                44,
                UiPalette.Gold,
                TextAlignmentOptions.TopLeft
            );
            Guide.Fill(
                (RectTransform)view.Title.transform,
                new Vector2(40, 0),
                new Vector2(-40, -24)
            );
            view.Count = Label(
                panel,
                "Count",
                "",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.TopRight
            );
            Guide.Fill(
                (RectTransform)view.Count.transform,
                new Vector2(40, 0),
                new Vector2(-40, -36)
            );

            var viewport = Rect("Viewport", panel);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(24, 24);
            viewport.offsetMax = new Vector2(-24, -100);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear, true);

            var content = Rect("Rows", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var rows = content.gameObject.AddComponent<VerticalLayoutGroup>();
            rows.spacing = 10;
            rows.childControlWidth = rows.childControlHeight = true;
            rows.childForceExpandWidth = true;
            rows.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            view.Rows = Enumerable.Range(0, BakedRows).Select(i => BuildRow(content, i)).ToArray();
            (view.Remove, view.RemoveLabel) = BuildRemove(content);

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            view.List = scroll;
        }

        private static EquipmentRow BuildRow(RectTransform content, int index)
        {
            var rect = Rect("Item" + index, content);
            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = RowHeight;
            var row = rect.gameObject.AddComponent<EquipmentRow>();
            row.Button = Guide.AddButton(
                rect,
                Guide.Frame(rect, Guide.FramePath, new Color(1f, 1f, 1f, 0.9f))
            );

            var icon = Rect("Icon", rect);
            Corner(icon, new Vector2(0, 0.5f), new Vector2(16, 0), Vector2.one * Guide.IconSize);
            row.Icon = icon.gameObject.AddComponent<Image>();
            row.Icon.raycastTarget = false;

            row.Name = Label(
                rect,
                "Name",
                "",
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft
            );
            Guide.Band((RectTransform)row.Name.transform, top: true, 12, 44, RowTextLeft, 300);
            Guide.Shrink(row.Name, 22);
            row.Stars = Label(
                rect,
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
                rect,
                "Detail",
                "",
                24,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft
            );
            Guide.Band((RectTransform)row.Detail.transform, top: true, 64, 40, RowTextLeft, 24);
            Guide.Shrink(row.Detail, 18);
            row.Mark = Label(rect, "Mark", "", 24, UiPalette.Gold, TextAlignmentOptions.TopRight);
            Guide.Band((RectTransform)row.Mark.transform, top: true, 20, 32, RowTextLeft, 24);
            row.Mark.gameObject.SetActive(false);
            return row;
        }

        // 選んでいる枠の装備を外す行。リストの最後に置く。
        private static (Button Button, TMP_Text Label) BuildRemove(RectTransform content)
        {
            var rect = Rect("Remove", content);
            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = RowHeight;
            var button = Guide.AddButton(
                rect,
                Guide.Frame(rect, Guide.FramePath, new Color(1f, 1f, 1f, 0.9f))
            );
            var label = Label(
                rect,
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
