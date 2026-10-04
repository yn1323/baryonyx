using System.Linq;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Equipment.Editor
{
    /// <summary>
    /// The formation's equipment. The formation's generator calls <see cref="BuildPanel"/> for
    /// its equipment item, so the people's tabs, the weapon and armour slots, the figure and the
    /// item rows are baked into the formation prefab and read in the editor without Play Mode.
    /// The items are the mock catalog's (doc/features/equipment.md); the people are the party's
    /// mock data.
    /// </summary>
    public static class EquipmentAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Equipment";
        public const string IconChangeGearPath = Folder + "/UI/Art/IconChangeGear.aseprite";
        public const string IconWeaponPath = Folder + "/UI/Art/IconWeapon.aseprite";
        public const string IconArmorPath = Folder + "/UI/Art/IconArmor.aseprite";

        private const float DotScale = 4f;
        private const float LeftWidth = 780f;
        private const float DividerWidth = 4f;
        private const float TabTop = 24f;
        private const float SlotTop = 168f;
        private const float SlotHeight = 152f;
        private const float SlotGap = 12f;
        private const float SlotTextLeft = 160f;
        private const float FeetY = 56f;
        private const float RowHeight = 128f;
        private const float RowTextLeft = 132f;

        // 持っている装備の行を、この数だけ作り込む。足りなければ、ビューが実行中に写して増やす。
        private const int BakedRows = 8;
        private static readonly Color DividerColor = new(0.52f, 0.59f, 0.68f, 0.7f);
        private static readonly Color TagColor = new(0.02f, 0.03f, 0.06f, 0.85f);

        /// <summary>
        /// Builds the equipment over the guide screen's safe area: one large frame below the back
        /// button, split into the people and their equipment on the left and the owned items on
        /// the right. Called while the guide screen's prefab is being built, so the shared frames
        /// and font are ready.
        /// </summary>
        public static GameObject BuildPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var (people, partyCount) = PartyFormation.From(party).TabOrder();

            var root = Rect("Equipment", safe);
            Stretch(root);
            var view = root.gameObject.AddComponent<EquipmentView>();
            view.Party = party;
            view.Guide = guide;
            view.WeaponIcon = Guide.Icon(IconWeaponPath);
            view.ArmorIcon = Guide.Icon(IconArmorPath);

            var panel = Rect("Panel", root);
            Stretch(panel);
            panel.offsetMin = new Vector2(32, 24);
            panel.offsetMax = new Vector2(-32, -208);
            Guide.Frame(panel, Guide.FramePath, Color.white).raycastTarget = true;

            var left = Rect("Person", panel);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0, 1);
            left.pivot = new Vector2(0, 0.5f);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(LeftWidth, 0);
            var divider = Rect("Divider", panel);
            divider.anchorMin = Vector2.zero;
            divider.anchorMax = new Vector2(0, 1);
            divider.pivot = new Vector2(0, 0.5f);
            divider.offsetMin = new Vector2(LeftWidth, 28);
            divider.offsetMax = new Vector2(LeftWidth + DividerWidth, -28);
            AddImage(divider, DividerColor, false);
            var right = Rect("Items", panel);
            Stretch(right);
            right.offsetMin = new Vector2(LeftWidth + DividerWidth, 0);

            view.People = PartyTabAssets.Build(left, people, partyCount, TabTop);
            view.Slots = new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor }
                .Select(slot => BuildSlot(left, slot))
                .ToArray();
            view.Figure = BuildFigure(left, party);
            BuildItems(right, view);

            // 仮データの最初の人と仮の装備で描き、Prefabを開くと停止中でも文字と絵が読めるようにする。
            new EquipmentPresenter(
                view,
                people,
                partyCount,
                EquipmentLocalSource.Starter(),
                new EquipmentLocalSource()
            ).Dispose();
            return root.gameObject;
        }

        // --- Left: the chosen person's weapon and armour, and their figure ------------------

        private static EquipmentSlotWidget BuildSlot(RectTransform panel, EquipmentSlot slot)
        {
            int index = (int)slot;
            var row = Rect("Slot_" + slot, panel);
            row.anchorMin = new Vector2(0, 1);
            row.anchorMax = Vector2.one;
            row.pivot = new Vector2(0.5f, 1);
            float top = SlotTop + index * (SlotHeight + SlotGap);
            row.offsetMin = new Vector2(24, -(top + SlotHeight));
            row.offsetMax = new Vector2(-24, -top);
            var widget = new EquipmentSlotWidget
            {
                Button = Guide.AddButton(
                    row,
                    Guide.Frame(row, Guide.FramePath, new Color(1f, 1f, 1f, 0.9f))
                ),
            };
            var selected = Rect("Selected", row);
            Stretch(selected);
            Guide.Frame(selected, Guide.FrameSelectedPath, Color.white).raycastTarget = false;
            selected.gameObject.SetActive(false);
            widget.Selected = selected.gameObject;

            // 枠の名前（武器・防具）の札の上に、24×24のアイコンを4倍で置く。
            var icon = Rect("Icon", row);
            Corner(icon, new Vector2(0, 1), new Vector2(28, -12), Vector2.one * Guide.IconSize);
            widget.Icon = icon.gameObject.AddComponent<Image>();
            widget.Icon.raycastTarget = false;
            var tag = Rect("Tag", row);
            Corner(tag, new Vector2(0, 0), new Vector2(28, 10), new Vector2(Guide.IconSize, 34));
            AddImage(tag, TagColor, false);
            var label = Label(
                tag,
                "Label",
                EquipmentCatalog.NameOf(slot),
                24,
                UiPalette.Gold,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)label.transform);

            widget.Name = Label(
                row,
                "Name",
                "",
                34,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft
            );
            Guide.Band((RectTransform)widget.Name.transform, top: true, 16, 46, SlotTextLeft, 140);
            Guide.Shrink(widget.Name, 24);
            widget.Stars = Label(
                row,
                "Stars",
                "",
                28,
                UiPalette.Gold,
                TextAlignmentOptions.BottomRight
            );
            Guide.Band((RectTransform)widget.Stars.transform, top: true, 16, 46, SlotTextLeft, 24);
            widget.Detail = Label(
                row,
                "Detail",
                "",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft
            );
            widget.Detail.textWrappingMode = TextWrappingModes.Normal;
            Guide.Band((RectTransform)widget.Detail.transform, top: true, 76, 64, SlotTextLeft, 24);
            return widget;
        }

        // The chosen person's 64x64 figure at 4x, standing on a soft shadow below the slots.
        private static RawImage BuildFigure(RectTransform panel, PartyMockData party)
        {
            var shadow = Picture(
                panel,
                "Shadow",
                ArtAssets.LoadSprite(UiArt.ShadowPath),
                Vector2.zero,
                new Vector2(180, 28),
                0.5f
            );
            var foot = (RectTransform)shadow.transform;
            foot.anchorMin = foot.anchorMax = new Vector2(0.5f, 0f);
            foot.anchoredPosition = new Vector2(0, FeetY + 2);

            var art = party
                .Members.Select(member => member.Art)
                .FirstOrDefault(texture => texture != null);
            var figure = PixelActor(panel, "Figure", art, Vector2.zero, DotScale);
            var rect = figure.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0, FeetY);
            return figure;
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
