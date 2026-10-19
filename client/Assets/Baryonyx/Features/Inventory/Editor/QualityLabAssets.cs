using System;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.Equipment.Editor;
using Baryonyx.SkillEvolution;
using Baryonyx.StepBonus;
using Baryonyx.StepBonus.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Inventory.Editor
{
    /// <summary>
    /// The quality lab's screen (QualityLabScreen.prefab), a debugging room for quality
    /// (<see cref="QualityLab"/>): over a dark backdrop, the way back to home at the top left, the
    /// tabs of the kinds, the list of the kind picked on the left, the thing picked at its
    /// multiplier in the middle (a skill as the battle's card, the others as a picture) with
    /// the four qualities side by side under it, and on the right its quality letter, where the
    /// multiplier falls in the range, the sliders and buttons that move it, and its effect.
    /// </summary>
    public static class QualityLabAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/Inventory/UI/QualityLabScreen.prefab";

        private static readonly Color Gold = new(0.98f, 0.8f, 0.36f);
        private static readonly Color Backdrop = new(0.03f, 0.04f, 0.08f, 1f);

        // The battle's card is 70x98 dots at 3 px.
        private static readonly Vector2 CardSize = new(70 * 3, 98 * 3);
        private const float MainScale = 1.7f;
        private const float CompareScale = 0.52f;

        // The width everything is laid out in, centred: it fits a 16:9 screen (1920 wide) and
        // the Editor's narrower game view.
        private const float FrameWidth = 1700f;

        // Buttons are as tall as a finger needs (doc/rules/ui-design.md).
        private const float ButtonHeight = 128f;

        [MenuItem("Baryonyx/Inventory/Create Quality Lab Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();

            UiArt.EnsureAll();
            if (
                AssetDatabase.LoadAssetAtPath<GameObject>(BattleInspectAssets.CardPrefabPath)
                == null
            )
                BattleInspectAssets.CreateAssets();
            var bonuses = AssetDatabase.LoadAssetAtPath<StepBonusMockData>(
                StepBonusAssets.DataPath
            );
            if (bonuses == null)
                bonuses = StepBonusAssets.CreateData();
            var font = GameFontAssets.GetOrCreate();
            var shadowText = UiArt.EnsureTextShadow(font);
            using (UiBuild.Begin(font, shadowText))
            {
                var root = CanvasRoot("QualityLabScreen");
                var lab = root.gameObject.AddComponent<QualityLab>();
                AddImage(root, Backdrop, false);
                // 16:9（幅1920）の画面にも収まる幅の枠を、横長の画面では中央に置く。
                var safe = Rect("Frame", SafeArea(root));
                safe.anchorMin = safe.anchorMax = safe.pivot = new Vector2(0.5f, 1f);
                safe.anchoredPosition = Vector2.zero;
                safe.sizeDelta = new Vector2(FrameWidth, 1080f);

                BuildHeader(safe, lab);
                BuildList(safe, lab);
                BuildPreviews(safe, lab);
                BuildInfo(safe, lab);

                lab.Bonuses = bonuses;
                lab.WeaponIcon = GuideMenuAssets.Icon(EquipmentAssets.IconWeaponPath);
                lab.ArmorIcon = GuideMenuAssets.Icon(EquipmentAssets.IconArmorPath);
                lab.AccessoryIcon = GuideMenuAssets.Icon(UiArt.IconCompassPath);
                BakeCardArt(lab);

                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }

        // 左上のホームへ戻るボタン、画面の名前、種類のタブ。
        private static void BuildHeader(RectTransform safe, QualityLab lab)
        {
            var home = Layout.Box(safe, "Home", 20f, 8f, 200f, ButtonHeight);
            var (button, _, card) = LabCard(home);
            Stretch(
                Label(
                    card,
                    "Label",
                    "ホームへ",
                    32,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                ).rectTransform
            );
            lab.HomeButton = button;

            var title = Label(
                safe,
                "Heading",
                "品質のデバッグルーム",
                38,
                Gold,
                TextAlignmentOptions.Left
            );
            Layout.At(title, 260f, 46f, 900f, 52f);

            lab.Tabs = GuideFrameAssets.Tabs(
                safe,
                "Tabs",
                new[] { "スキル", "固有スキル", "武器", "防具", "アクセサリー", "ボーナス" },
                20f,
                148f,
                276f,
                72f,
                1660f
            );
        }

        // 左の一覧。行は選んだ種類に合わせてPlayの開始時に作るため、ここでは見本だけを置く。
        private static void BuildList(RectTransform safe, QualityLab lab)
        {
            var panel = Layout.Box(safe, "List", 20f, 236f, 400f, 830f);
            var viewport = Layout.Viewport(panel);
            var content = Layout.Column(viewport, "Rows", 10f, new RectOffset(0, 0, 4, 24));
            lab.ListContent = content;
            lab.ListScroll = Layout.Scroll(panel, viewport, content);

            var row = Rect("RowTemplate", panel);
            row.sizeDelta = new Vector2(400f, ButtonHeight);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = ButtonHeight;
            var (rowButton, _, rowCard) = LabCard(row);
            var selected = Rect(QualityLab.SelectedName, row);
            Stretch(selected);
            AddImage(selected, new Color(Gold.r, Gold.g, Gold.b, 0.28f), false);
            selected.gameObject.SetActive(false);
            var label = Label(
                rowCard,
                "Label",
                "",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(24f, 0f);
            row.gameObject.SetActive(false);
            lab.RowTemplate = rowButton;
        }

        // 真ん中に今の倍率のカード（か絵）、その下に品質ごとの見比べ。
        private static void BuildPreviews(RectTransform safe, QualityLab lab)
        {
            var main = Layout.Box(safe, "Main", 450f, 236f, 420f, 540f);
            lab.Main = Preview(main, MainScale, 34f);

            var compare = Layout.Box(safe, "Compare", 436f, 786f, 448f, 270f);
            var heading = Label(
                compare,
                "Heading",
                "品質ごとの見比べ",
                24,
                UiPalette.TextSub,
                TextAlignmentOptions.Left
            );
            Layout.At(heading, 12f, 0f, 400f, 32f);
            lab.Compare = Quality
                .All.Select(
                    (quality, i) =>
                        Preview(
                            Layout.Box(compare, "Compare" + quality, i * 112f, 34f, 112f, 236f),
                            CompareScale,
                            24f
                        )
                )
                .ToArray();
        }

        // 1つの見せ場：スキルなら戦闘のカード、ほかは絵の板。下に品質と倍率の見出し。
        private static QualityLabPreview Preview(RectTransform host, float scale, float captionSize)
        {
            var size = CardSize * scale;
            var preview = new QualityLabPreview();

            var instance = InstantiatePrefab(BattleInspectAssets.CardPrefabPath, "Card", host);
            var card = (RectTransform)instance.transform;
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = Vector2.zero;
            card.localScale = Vector3.one * scale;
            var input = instance.GetComponent<BattleInspectCardInput>();
            if (input != null)
                UnityEngine.Object.DestroyImmediate(input);
            preview.Card = instance.GetComponent<BattleInspectCardView>();

            preview.Item = ItemFace(host, size, scale);

            var caption = Label(
                host,
                "Caption",
                "",
                captionSize,
                Gold,
                TextAlignmentOptions.Center
            );
            Corner(
                caption.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -(size.y + 4f)),
                new Vector2(size.x + 40f, captionSize + 12f)
            );
            preview.Caption = caption;
            return preview;
        }

        // 装備・アクセサリー・ボーナスの絵の板：アイコンにキラキラを重ね、名前・★・品質の文字を添える。
        private static QualityLabItemFace ItemFace(RectTransform host, Vector2 size, float scale)
        {
            var root = Rect("Item", host);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = size;
            var content = GlassCardAssets.Plaque(
                root,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );

            var iconRect = Rect("Icon", content);
            Place(iconRect, new Vector2(0f, size.y * 0.06f), Vector2.one * (size.x * 0.72f));
            var icon = iconRect.gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            var foil = CardFoilAssets.Add(icon);

            float text = Mathf.Max(14f, 30f * scale / MainScale * 1.4f);
            var name = Label(
                content,
                "Name",
                "",
                text,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Corner(
                name.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0f, size.y * 0.1f),
                new Vector2(size.x - 16f, text + 10f)
            );
            var stars = Label(content, "Stars", "", text * 0.9f, Gold, TextAlignmentOptions.Center);
            Corner(
                stars.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0f, size.y * 0.02f),
                new Vector2(size.x - 16f, text + 6f)
            );
            var quality = Label(
                content,
                "Quality",
                "C",
                text * 1.8f,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Corner(
                quality.rectTransform,
                Vector2.one,
                new Vector2(-8f, -6f),
                new Vector2(text * 2.2f, text * 2.2f)
            );
            root.gameObject.SetActive(false);
            return new QualityLabItemFace
            {
                Root = root.gameObject,
                Icon = icon,
                Foil = foil,
                Name = name,
                Stars = stars,
                Quality = quality,
            };
        }

        // 右側：選んだ物の名前、品質の文字、倍率と幅、帯、つまみとボタン、効果。
        private static void BuildInfo(RectTransform safe, QualityLab lab)
        {
            const float left = 910f;
            const float width = 770f;
            lab.Title = Text(safe, "Title", 44, UiPalette.TextMain, left, 236f, width, 56f);
            lab.Detail = Text(safe, "Detail", 26, UiPalette.TextSub, left, 292f, width, 36f);

            lab.QualityLetter = Label(safe, "QualityLetter", "S", 150, Gold, TextAlignmentOptions.Center);
            Layout.At(lab.QualityLetter, left, 332f, 150f, 150f);
            const float barLeft = left + 160f;
            const float barWidth = 610f;
            lab.MultiplierText = Text(safe, "Multiplier", 40, UiPalette.TextMain, barLeft, 340f, barWidth, 52f);
            lab.RangeText = Text(safe, "Ranges", 21, UiPalette.TextSub, barLeft, 396f, barWidth, 32f);

            // 幅の帯：左端が×1.00、右端が上限。品質ごとの区間に色を付け、今の位置に白い印を立てる。
            var bar = Layout.Box(safe, "Bar", barLeft, 438f, barWidth, 34f);
            lab.Segments = Quality
                .All.Select(quality =>
                {
                    var segment = Rect("Segment" + quality, bar);
                    var color = QualityPalette.Of(quality);
                    AddImage(segment, new Color(color.r, color.g, color.b, 0.6f), false);
                    var letter = Label(segment, "Letter", Quality.Letter(quality), 22, Color.black, TextAlignmentOptions.Center, false);
                    Stretch(letter.rectTransform);
                    return segment;
                })
                .ToArray();
            var marker = Rect("Marker", bar);
            marker.sizeDelta = new Vector2(8f, 58f);
            AddImage(marker, Color.white, false);
            lab.Marker = marker;

            // 倍率のつまみを、1ずつ動かすボタンで挟む。ボタンは指で押せる128にする（doc/rules/ui-design.md）。
            lab.StepDownButton = Button(safe, "StepDown", "−0.01", left, 492f, ButtonHeight, UiPalette.TextMain);
            lab.MultiplierSlider = BuildSlider(safe, "MultiplierSlider", left + 140f, 528f, 490f, 56f);
            lab.StepUpButton = Button(safe, "StepUp", "+0.01", left + width - ButtonHeight, 492f, ButtonHeight, UiPalette.TextMain);

            var buttons = new[] { "C", "B", "A", "S" };
            lab.QualityButtons = buttons
                .Select(
                    (name, i) =>
                        Button(safe, "Jump" + name, name, left + i * 138f, 632f, ButtonHeight, QualityPalette.Of(Quality.All[i]))
                )
                .ToArray();
            lab.LimitResetButton = Button(safe, "LimitReset", "上限を戻す", left + 560f, 632f, 210f, UiPalette.TextMain);

            lab.LimitText = Text(safe, "Limit", 28, UiPalette.TextSub, left, 776f, 170f, 56f);
            lab.LimitSlider = BuildSlider(safe, "LimitSlider", left + 170f, 772f, 600f, 56f);

            // 効果の文は2行まで折り返す。倍率で変わる数字は1つの段落に続けて書く。
            lab.EffectText = Text(safe, "Effect", 26, UiPalette.TextMain, left, 840f, width, 70f);
            lab.BaseText = Text(safe, "Base", 21, UiPalette.TextSub, left, 912f, width, 30f);
            lab.NumbersText = Text(safe, "Numbers", 21, UiPalette.TextSub, left, 946f, width, 120f);
            lab.NumbersText.textWrappingMode = TextWrappingModes.Normal;
            lab.EffectText.textWrappingMode = TextWrappingModes.Normal;
        }

        private static TMP_Text Text(
            RectTransform parent,
            string name,
            float size,
            Color color,
            float x,
            float y,
            float width,
            float height
        )
        {
            var label = Label(parent, name, "", size, color, TextAlignmentOptions.TopLeft);
            Layout.At(label, x, y, width, height);
            return label;
        }

        private static Button Button(
            RectTransform parent,
            string name,
            string text,
            float x,
            float y,
            float width,
            Color color
        )
        {
            var rect = Layout.Box(parent, name, x, y, width, ButtonHeight);
            var (button, _, card) = LabCard(rect);
            Stretch(
                Label(card, "Label", text, 34, color, TextAlignmentOptions.Center).rectTransform
            );
            return button;
        }

        // 横に動かすつまみ。溝の上を押してもつまんでも倍率が変わる。
        private static Slider BuildSlider(
            RectTransform parent,
            string name,
            float x,
            float y,
            float width,
            float height
        )
        {
            var rect = Layout.Box(parent, name, x, y, width, height);
            float inset = height * 0.36f;
            var track = Rect("Background", rect);
            Stretch(track);
            track.offsetMin = new Vector2(0f, inset);
            track.offsetMax = new Vector2(0f, -inset);
            AddImage(track, new Color(1f, 1f, 1f, 0.16f), true);

            var fillArea = Rect("Fill Area", rect);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(0f, inset);
            fillArea.offsetMax = new Vector2(0f, -inset);
            var fill = Rect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.sizeDelta = Vector2.zero;
            AddImage(fill, new Color(Gold.r, Gold.g, Gold.b, 0.85f), false);

            var handleArea = Rect("Handle Slide Area", rect);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(height * 0.3f, 0f);
            handleArea.offsetMax = new Vector2(-height * 0.3f, 0f);
            var handle = Rect("Handle", handleArea);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.sizeDelta = new Vector2(height * 0.6f, 0f);
            var knob = AddImage(handle, Color.white, true);

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = knob;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 1f;
            slider.maxValue = 1.5f;
            slider.wholeNumbers = false;
            slider.value = 1f;
            return slider;
        }

        // 案内人の画面のカードと同じ、模様のないすりガラスのカードのボタン。
        private static (Button Button, GameObject Selected, RectTransform Content) LabCard(
            RectTransform rect
        ) => GlassCardAssets.Choice(rect, pattern: false, fill: GlassCardAssets.SceneFillColor);

        /// <summary>
        /// The art the cards show, which the lab cannot load in Play: each card's art in
        /// <see cref="QualityLabEntry.Cards"/>' order, the frames and element icons of every
        /// element, and the party members' names.
        /// </summary>
        private static void BakeCardArt(QualityLab lab)
        {
            lab.CardArts = QualityLabEntry
                .Cards.Select(skill =>
                    (Texture)
                        BattleInspectAssets.Art(
                            UniqueSkillTrees.FindStage(skill.Id)?.Art ?? skill.Art
                        )
                )
                .ToArray();
            var elements = (CardElement[])Enum.GetValues(typeof(CardElement));
            lab.CardFrames = elements
                .Select(element => (Texture)BattleInspectAssets.Art("CardFrame" + element))
                .ToArray();
            lab.ElementIcons = elements
                .Select(element =>
                    element == CardElement.None
                        ? null
                        : (Texture)BattleInspectAssets.Art("Element" + element)
                )
                .ToArray();
            var names = new string[Enum.GetValues(typeof(CardUser)).Length];
            foreach (var ally in BattleInspectAssets.Allies)
                names[(int)ally.User] = ally.Label;
            lab.PartyNames = names;
        }
    }
}
