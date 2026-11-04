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
using ElementIconRowAssets = Baryonyx.UI.Editor.ElementIconRowAssets;
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
        private const float Split = UiSpace.SheetSplit;
        private static readonly Rect Left = ChangeScreen.LeftOf(Split);

        // 左は上からステータス・5つの枠。ステータスの下から面の下の余白までを、武器・防具・アクセサリー3つの
        // 5つの枠で等分する（2026-10-31、ユーザーの指示）。
        private const float SlotTop = ChangeScreen.StatsLineTop + 16f;
        private const float SlotGap = 12f;
        private const float SlotBottomMargin = 24f;
        private const float SlotHeight =
            (ChangeScreen.Height - SlotBottomMargin - SlotTop - SlotGap * 4) / 5f;
        private const float SlotStep = SlotHeight + SlotGap;

        // 枠の文字は、枠の名前（空きのときだけ）・装備の名前と☆と品質・効果の行。まとまりを縦の中央に置く。
        private const float SlotTagHeight = 26f;
        private const float SlotNameHeight = 40f;
        private const float SlotDetailHeight = 32f;
        private const float SlotTextLeft = 112f;
        private const float SlotRight = 20f;
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
                pattern: false,
                bare: UiLook.Lineless
            );
            var widget = new EquipmentSlotWidget { Button = button, Selected = selected };

            // 24×24のアイコンを3倍で置き、右に枠の名前（武器・防具）、装備の名前と★、その効果。
            var icon = Layout.Box(row, "Icon", 18, (SlotHeight - 72) / 2f, 72, 72);
            widget.Icon = icon.gameObject.AddComponent<Image>();
            widget.Icon.raycastTarget = false;
            widget.Foil = CardFoilAssets.Add(widget.Icon);
            (widget.Tag, widget.Name, widget.Detail, widget.Elements) = SlotTexts(
                row,
                EquipmentCatalog.NameOf(slot)
            );
            return widget;
        }

        // アクセサリーの枠。武器・防具と同じ高さと並びで、付けていれば絵・名前・★・効果を出す。
        // 開いていなければ錠前と「Lv 20で解放」を出す。
        private static EquipmentAccessoryWidget BuildAccessory(RectTransform panel, int index)
        {
            var (button, selected, row) = GlassCardAssets.Choice(
                SlotBox(panel, "Accessory" + index, 2 + index),
                dashed: true,
                pattern: false,
                bare: UiLook.Lineless
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
            (widget.Tag, widget.Name, widget.Detail, widget.Elements) = SlotTexts(
                row,
                EquipmentPresenter.AccessoryTitle
            );
            widget.Name.text = AccessorySlots.UnlockText(index);
            return widget;
        }

        // 左の5つの枠の場所。ステータスの下から面の下までを5等分する。
        private static RectTransform SlotBox(RectTransform panel, string name, int index) =>
            Layout.Box(panel, name, 0, SlotTop + index * SlotStep, Left.width, SlotHeight);

        // 枠の文字（行の標準の形）：装備の名前のすぐ右に☆と品質・倍率、右上に属性、2行目に効果。
        // 空き・開いていない枠だけは、どの枠か分かるよう上に枠の名前（武器・防具・アクセサリー）を出す。
        // 縦に並べ、隠した行を詰めて、残りを枠の上下の中央に置く。
        private static (TMP_Text Tag, TMP_Text Name, TMP_Text Detail, ElementIconRow Elements) SlotTexts(
            RectTransform row,
            string tag
        )
        {
            var box = Layout.Box(
                row,
                "Texts",
                SlotTextLeft,
                0f,
                Left.width - SlotTextLeft - SlotRight,
                SlotHeight
            );
            var stack = box.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.childAlignment = TextAnchor.MiddleLeft;
            stack.spacing = 4f;
            stack.childControlWidth = stack.childForceExpandWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandHeight = false;

            var label = Label(box, "Tag", tag, UiType.Tiny, UiPalette.Gold, TextAlignmentOptions.BottomLeft);
            Fixed(label.transform, SlotTagHeight);

            var line = Rect("NameLine", box);
            Fixed(line, SlotNameHeight);
            float width = ElementIconRowAssets.Width();
            var name = Label(line, "Name", "", UiType.Small, UiPalette.TextMain, TextAlignmentOptions.MidlineLeft);
            name.richText = true;
            Stretch((RectTransform)name.transform);
            ((RectTransform)name.transform).offsetMax = new Vector2(-width - 8f, 0f);
            Guide.Shrink(name, 18);
            var icons = Rect("Elements", line);
            Corner(icons, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(width, ElementIconRowAssets.Size));
            var elements = ElementIconRowAssets.Add(icons, TextAnchor.MiddleRight);

            // 長い効果（「攻撃スキルが10%で2回発動」など）も1行に収まるよう縮めて入れる。
            var detail = Label(box, "Detail", "", UiType.Tiny, UiPalette.TextSub, TextAlignmentOptions.MidlineLeft);
            Fixed(detail.transform, SlotDetailHeight);
            Guide.Shrink(detail, 16);
            return (label, name, detail, elements);
        }

        private static void Fixed(Transform part, float height) =>
            part.gameObject.AddComponent<LayoutElement>().preferredHeight = height;

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
            var (button, _, card) = GlassCardAssets.Choice(rect, pattern: false, bare: UiLook.Lineless);
            row.Button = button;

            var icon = Rect("Icon", card);
            Corner(icon, new Vector2(0, 0.5f), new Vector2(16, 0), Vector2.one * Guide.IconSize);
            row.Icon = icon.gameObject.AddComponent<Image>();
            row.Icon.raycastTarget = false;
            row.Foil = CardFoilAssets.Add(row.Icon);

            // 横長の行：名前のすぐ右に☆と品質・倍率、2行目に効果の全文、右下に増減、右上に属性と印。
            row.Name = Label(
                card,
                "Name",
                "",
                UiType.Small,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft
            );
            row.Name.richText = true;
            Guide.Band((RectTransform)row.Name.transform, top: true, 12, 44, RowTextLeft, 300);
            Guide.Shrink(row.Name, 22);
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
            // 名前の行の高さの右上に属性のアイコン（増減の真上）。印があれば、ビューがその左へずらす。
            var elements = Rect("Elements", card);
            Corner(
                elements,
                Vector2.one,
                new Vector2(
                    -EquipmentRow.ElementsRight,
                    -(12 + 44 / 2f - ElementIconRowAssets.Size / 2f)
                ),
                new Vector2(ElementIconRowAssets.Width(), ElementIconRowAssets.Size)
            );
            row.Elements = ElementIconRowAssets.Add(elements, TextAnchor.MiddleRight);
            return row;
        }

        // 選んでいる枠の装備を外す行。リストの最後に置く。
        private static (Button Button, TMP_Text Label) BuildRemove(RectTransform content)
        {
            var rect = Layout.ListItem(content, "Remove", RowHeight);
            var (button, _, card) = GlassCardAssets.Choice(rect, dashed: true, bare: UiLook.Lineless);
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
