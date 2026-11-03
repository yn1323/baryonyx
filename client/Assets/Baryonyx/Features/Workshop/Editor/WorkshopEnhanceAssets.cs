using System;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.Enhancement;
using Baryonyx.Equipment;
using Baryonyx.Inventory.Editor;
using Baryonyx.Library;
using Baryonyx.Library.Editor;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.Training;
using Baryonyx.Training.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using static Baryonyx.Workshop.Editor.WorkshopPanelAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Workshop.Editor
{
    /// <summary>
    /// Builds the trading house's "enhance" panel (<see cref="WorkshopEnhanceView"/>), filling the
    /// screen: the tabs and the rows with the art on their left and the quality on their right in
    /// the left pane; in the right pane the chosen thing's art, name, quality now and after, the
    /// effect, the quality gauge, the materials held and how many stones to use, and the enhance
    /// button. The skills tab is baked from the mock party, so the prefab reads without Play
    /// Mode; the view draws what the player holds when it opens.
    /// </summary>
    public static class WorkshopEnhanceAssets
    {
        private const float RowHeight = 112f;
        private const float ArtBox = 80f;

        // 強化する前と後のカード（70×98ドットを4倍）と、間の矢印。
        private const float CardDot = 4f;
        private const float CardsTop = 232f;
        private const float ArrowWidth = 96f;
        private const float GearArt = 128f;

        // 行の右端の品質・費用の幅。
        private const float ValueWidth = 230f;

        // 素材の段：磨き石の札。その下に、費用と足りない物を1行で書く。
        private const float MaterialsTop = 776f;
        private const float BarHeight = 120f;
        private const float InfoHeight = 48f;
        private const float ChipGap = 12f;
        private const float ChipArt = 64f;

        // 磨き石の札の内側の右上に置く、赤い丸に白い×のボタン：押せる広さ、暗い縁と赤い丸の直径、×の一辺、
        // 札の角からの間。
        private const float CancelHit = 64f;
        private const float CancelRim = 46f;
        private const float CancelDisc = 40f;
        private const float CancelCross = 22f;
        private const float CancelInset = 6f;
        private static readonly Color CancelRed = new(0.86f, 0.2f, 0.2f);
        private static readonly Color CancelRimColor = new(0.18f, 0.05f, 0.05f, 0.9f);

        // 強化する前と後：スキル・固有スキルのデッキのカードと、装備の札を同じ位置に重ねて作り、選んだ物で出し分ける。
        // 間に金の矢印を置き、それぞれの下に品質と倍率を書く。
        private static void BuildCards(
            RectTransform layer,
            WorkshopEnhanceView enhance,
            float left,
            float right
        )
        {
            var size = DeckCardAssets.Size(CardDot, small: false);
            float x = left + (right - left - size.x * 2f - ArrowWidth) / 2f;
            float afterX = x + size.x + ArrowWidth;
            enhance.BeforeCard = DeckCardAssets.Build(
                layer,
                "BeforeCard",
                x,
                CardsTop,
                CardDot,
                button: false
            );
            enhance.AfterCard = DeckCardAssets.Build(
                layer,
                "AfterCard",
                afterX,
                CardsTop,
                CardDot,
                button: false
            );
            enhance.BeforeGear = GearCard(layer, "BeforeGear", x, size);
            enhance.AfterGear = GearCard(layer, "AfterGear", afterX, size);
            foreach (var card in new[] { enhance.BeforeCard, enhance.AfterCard })
                card.Frame.transform.parent.gameObject.SetActive(false);

            float arrow = 72f;
            var mark = Layout.Box(
                layer,
                "Arrow",
                x + size.x + (ArrowWidth - arrow) / 2f,
                CardsTop + (size.y - arrow) / 2f,
                arrow,
                arrow
            );
            SpriteImage(mark, ArrowPath, UiPalette.Gold).preserveAspect = true;
            mark.gameObject.SetActive(false);
            enhance.Arrow = mark.gameObject;

            float qualityTop = CardsTop + size.y + 8f;
            enhance.BeforeQuality = QualityLabel(
                layer,
                "BeforeQuality",
                x - 40f,
                qualityTop,
                size.x + 80f
            );
            enhance.AfterQuality = QualityLabel(
                layer,
                "AfterQuality",
                afterX - 40f,
                qualityTop,
                size.x + 80f
            );
        }

        private static TMP_Text QualityLabel(
            RectTransform layer,
            string name,
            float x,
            float y,
            float width
        )
        {
            var label = Layout.Text(
                layer,
                name,
                "",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                x,
                y,
                width,
                44f
            );
            label.richText = true;
            Shrink(label, 22);
            return label;
        }

        // 装備の札：すりガラスの板の縁を品質の色にし、上に絵とキラキラ、下に名前・品質・効果を書く。
        private static WorkshopGearCard GearCard(
            RectTransform layer,
            string name,
            float x,
            Vector2 size
        )
        {
            var rect = Layout.Box(layer, name, x, CardsTop, size.x, size.y);
            var inside = GlassCardAssets.Plaque(rect, pattern: false);
            var card = new WorkshopGearCard
            {
                Root = rect.gameObject,
                Rim = GlassCardAssets.RimOf(rect),
            };
            var box = Layout.Box(inside, "ArtBox", (size.x - GearArt) / 2f, 36f, GearArt, GearArt);
            var art = Rect("Art", box);
            Place(art, Vector2.zero, Vector2.one * GearArt);
            card.Art = art.gameObject.AddComponent<RawImage>();
            card.Art.raycastTarget = false;
            card.Foil = CardFoilAssets.Add(card.Art);
            card.Name = Layout.Text(
                inside,
                "Name",
                "",
                36,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                12f,
                188f,
                size.x - 24f,
                48f
            );
            Shrink(card.Name, 24);
            card.Quality = Layout.Text(
                inside,
                "Quality",
                "",
                26,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                12f,
                238f,
                size.x - 24f,
                36f
            );
            card.Quality.richText = true;
            card.Effect = Layout.Text(
                inside,
                "Effect",
                "",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.Top,
                16f,
                286f,
                size.x - 32f,
                92f
            );
            card.Effect.richText = true;
            card.Effect.textWrappingMode = TextWrappingModes.Normal;
            Shrink(card.Effect, 18);
            rect.gameObject.SetActive(false);
            return card;
        }

        // 品質のゲージ：境目の文字と棒。
        private const float GaugeTop = 690f;
        private const float GaugeBar = 24f;
        private static readonly Color GaugeBack = new(0.02f, 0.03f, 0.08f, 0.8f);
        private static readonly Color GaugeGain = UiPalette.Rise;

        /// <summary>
        /// Builds the polishing panel: skills, equipment and unique skills in the tabs. The skill
        /// awakening is a panel of its own (<see cref="WorkshopAwakenAssets"/>).
        /// </summary>
        public static GameObject BuildPanel(RectTransform safe, GuideMenuView view)
        {
            var tabLabels = WorkshopEnhanceView
                .PolishTabs.Select(tab => WorkshopEnhanceView.TabLabels[(int)tab])
                .ToArray();
            var (enhance, panel) = GuidePanelTemplate.RowsDetail<WorkshopEnhanceView>(
                safe,
                "WorkshopEnhance",
                tabLabels[0],
                tabLabels
            );
            var layer = panel.Layer;
            enhance.TabOrder = (WorkshopEnhanceView.Tab[])WorkshopEnhanceView.PolishTabs.Clone();
            enhance.Guide = view;
            enhance.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            enhance.Party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            enhance.Training = AssetDatabase.LoadAssetAtPath<TrainingMockData>(
                TrainingAssets.DataPath
            );
            enhance.ArtBox = ArtBox;
            enhance.GearArtBox = GearArt;
            enhance.CardFrames = DeckCardFrames();
            enhance.ElementIcons = ElementIcons();
            enhance.Tabs = panel.Tabs;
            enhance.ListTitle = panel.Title;
            enhance.ListCount = panel.Count;
            var content = panel.Content;
            enhance.Content = content;
            enhance.List = panel.Scroll;
            enhance.Template = ItemRowAssets.Row(
                content,
                "Template",
                RowHeight,
                ArtBox,
                ValueWidth
            );
            // 読み込む間は、この写しを伏せて一覧を埋める（GlassCardCover）。
            GlassCardAssets.AddBack(enhance.Template.Card, vertical: true);
            enhance.Template.gameObject.SetActive(false);
            // 最初のタブ（装備）の仮の持ち物で描き、Prefabを開くと停止中でも一覧が読めるようにする。覚醒は実行中に読む。
            var mock = EnhancementTargets.Equipment(EquipmentLocalSource.Starter());
            for (int i = 0; i < mock.Count; i++)
            {
                var row = ItemRowAssets.Row(content, "Row" + i, RowHeight, ArtBox, ValueWidth);
                GlassCardAssets.AddBack(row.Card, vertical: true);
                var kind = mock[i].Category switch
                {
                    EnhancementCategory.Weapon => LibraryKind.Weapon,
                    EnhancementCategory.Armor => LibraryKind.Armor,
                    _ => LibraryKind.Accessory,
                };
                var entry =
                    enhance.Data != null
                        ? enhance.Data.Entries.FirstOrDefault(e =>
                            e != null && e.Kind == kind && e.Id == mock[i].KindId
                        )
                        : null;
                WorkshopEnhanceView.PaintTarget(row, mock[i], "", entry, ArtBox);
                enhance.Rows.Add(row);
            }
            enhance.Status = panel.Status;
            enhance.ListCount.text = UiText.Count(mock.Count);

            BuildDetail(
                layer,
                enhance,
                GuidePanelTemplate.DetailLeft + 16f,
                GuidePanelTemplate.DetailRight
            );
            (enhance.Confirm, enhance.ConfirmLabel) = ConfirmButton(
                layer,
                WorkshopEnhanceView.PolishLabel
            );
            enhance.Flip = BuildFlip((RectTransform)enhance.transform);
            return enhance.gameObject;
        }

        // 冒険者の強化のタブ：人の列とタブの間を2つに分け、左の面に一覧、金の2本線の右の面に選んだ物を
        // 商会と同じ並びで出す。左の行に名前と効果が入るよう、スキルのタブと同じく左の面を広くする。
        private const float SheetSplit = UiSpace.SheetSplitWide;
        private const float SheetHeadingHeight = 52f;

        /// <summary>
        /// The adventurer's 「強化」 tab (doc/features/screens/loadout.md#強化): what the person
        /// holds under the headings 装備・固有スキル・カスタムスキル in the left pane, the chosen
        /// thing in the right pane as the trading house shows it, between the people's column and
        /// the tabs. The first mock person is baked, so the prefab reads without Play Mode.
        /// </summary>
        public static GameObject BuildSheetPanel(RectTransform safe, GuideMenuView guide)
        {
            var (enhance, layer) = GuideFrameAssets.FramedPanel<WorkshopEnhanceView>(
                safe,
                "PartyEnhance"
            );
            enhance.Guide = guide;
            enhance.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            enhance.Party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            enhance.Training = AssetDatabase.LoadAssetAtPath<TrainingMockData>(
                TrainingAssets.DataPath
            );
            enhance.ArtBox = ArtBox;
            enhance.GearArtBox = GearArt;
            enhance.CardFrames = DeckCardFrames();
            enhance.ElementIcons = ElementIcons();

            var (left, right, swipe) = PartyChangeScreenAssets.Build(
                layer,
                SheetSplit,
                "EnhancePane",
                "Enhance"
            );
            // 一覧は左の面いっぱいで、上下の縁で行を薄く消す。人を替えるフリックは、一覧の上でも受ける
            // （一覧のスクロールと同じ所に載る）。
            var viewport = Layout.Viewport(left);
            viewport.GetComponent<RectMask2D>().softness = new Vector2Int(
                0,
                PartyChangeScreenAssets.ListFade
            );
            var content = Layout.Column(
                viewport,
                "Rows",
                10,
                new RectOffset(
                    0,
                    0,
                    PartyChangeScreenAssets.ListFade,
                    PartyChangeScreenAssets.ListFade
                )
            );
            enhance.Content = content;
            enhance.List = Layout.Scroll(left, viewport, content);
            enhance.Swipes = new[] { swipe };
            enhance.Template = ItemRowAssets.Row(content, "Template", RowHeight, ArtBox);
            // 読み込む間は、この写しを伏せて一覧を埋める（GlassCardCover）。
            GlassCardAssets.AddBack(enhance.Template.Card, vertical: true);
            enhance.Template.gameObject.SetActive(false);
            var heading = Layout.ListItem(content, "HeadingTemplate", SheetHeadingHeight);
            GuideFrameAssets.Heading(
                heading,
                "Title",
                "",
                0f,
                8f,
                left.rect.width,
                SheetHeadingHeight - 8f,
                28f
            );
            heading.gameObject.SetActive(false);
            enhance.HeadingTemplate = heading;
            enhance.Status = GuideFrameAssets.ListStatus(left, 0f, left.rect.width, 100f);

            // 品質のゲージの左端の「C」が金の2本線にかからないよう、右の面の左から少し空ける。
            BuildDetail(layer, enhance, SheetSplit + 40f, PartySheetAssets.ContentRight - 24f);
            (enhance.Confirm, enhance.ConfirmLabel) = ConfirmButton(
                layer,
                WorkshopEnhanceView.PolishLabel,
                PartySheetAssets.ContentRight - 8f
            );
            enhance.Sheet = PartySheetAssets.Build(
                layer,
                guide,
                enhance.Party,
                EnhancementSession.SheetItemKey,
                TrainingAssets.SheetTabs
            );
            enhance.Trail = enhance.gameObject.AddComponent<PartySheetTrail>();
            enhance.Trail.Guide = guide;
            enhance.Flip = BuildFlip((RectTransform)enhance.transform);

            // 仮データの最初の人で描き、Prefabを開くと停止中でも一覧と右の面が読めるようにする。
            enhance.Use(PartyFormation.From(enhance.Party), null, EquipmentLocalSource.Starter());
            enhance.ShowPerson(null, null);
            return enhance.gameObject;
        }

        // 右の面：選んだ物の名前・分類、強化する前と後のカード、品質のゲージ、磨き石の札と費用。
        private static void BuildDetail(
            RectTransform layer,
            WorkshopEnhanceView enhance,
            float left,
            float right
        )
        {
            enhance.DetailName = Layout.Text(
                layer,
                "DetailName",
                "",
                46,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                left,
                128f,
                right - left,
                56f
            );
            Shrink(enhance.DetailName, 30);
            enhance.DetailSub = Layout.Text(
                layer,
                "DetailSub",
                "",
                28,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineLeft,
                left,
                184f,
                right - left,
                36f
            );
            enhance.DetailSub.richText = true;
            Shrink(enhance.DetailSub, 20);
            BuildCards(layer, enhance, left, right);
            BuildGauge(layer, enhance, left, right);
            BuildMaterials(layer, enhance, left, right, MaterialsTop);
            var info = Layout.Text(
                layer,
                "Info",
                "",
                28,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineLeft,
                left,
                MaterialsTop + BarHeight + 4f,
                right - left,
                InfoHeight
            );
            info.richText = true;
            info.textWrappingMode = TextWrappingModes.NoWrap;
            Shrink(info, 20);
            enhance.Info = info;
        }

        // 品質強化の演出（裏返して現れる）：画面全体の暗転、中央で回るカード、計算で描く光の板
        // （doc/features/enhancement.md 品質強化の演出）。光は戦闘の形のマテリアルを使う。押すまで隠しておく。
        private const string OrbitShaderPath =
            WorkshopScreenAssets.Folder + "/Vfx/Shaders/WorkshopEnhanceOrbit.shader";
        private const string OrbitMaterialFolder = WorkshopScreenAssets.Folder + "/Vfx/Materials";
        private const int Orbits = 10;

        // 光の軌跡の板（奥の半分・手前の半分のどちらかを描くマテリアル）。
        private static Baryonyx.Combat.Presentation.BattleVfxImage[] OrbitBoards(
            RectTransform screen,
            string name,
            bool front
        ) =>
            WorkshopCraftAssets.Boards(
                screen,
                name,
                Orbits,
                OrbitMaterial(front ? "EnhanceOrbitFront" : "EnhanceOrbitBack", front ? 1f : 0f)
            );

        private static Material OrbitMaterial(string name, float half)
        {
            AssetFolders.Ensure(OrbitMaterialFolder);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(OrbitShaderPath);
            if (shader == null)
                throw new InvalidOperationException("Missing shader: " + OrbitShaderPath);
            var path = $"{OrbitMaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetFloat("_Half", half);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static WorkshopEnhanceFlip BuildFlip(RectTransform root)
        {
            var screen = Rect("EnhanceFlip", root);
            Stretch(screen);
            var flip = screen.gameObject.AddComponent<WorkshopEnhanceFlip>();

            // Safe Areaの外まで暗くし、押すと演出を飛ばす。
            var dim = Rect("Dim", screen);
            Stretch(dim);
            dim.offsetMin = new Vector2(-400, -400);
            dim.offsetMax = new Vector2(400, 400);
            flip.Dim = AddImage(dim, new Color(0.02f, 0.03f, 0.08f, 0f), true);
            flip.Skip = GuideMenuAssets.AddButton(dim, flip.Dim);
            flip.Skip.transition = Selectable.Transition.None;

            flip.CardGlow = WorkshopCraftAssets.Board(
                screen,
                "CardGlow",
                WorkshopCraftAssets.Shape("Glow")
            );
            // 表を向いたときの光線と輪は、カードの文字が読めるようカードの奥に置く。
            flip.Rays = WorkshopCraftAssets.Board(
                screen,
                "Rays",
                WorkshopCraftAssets.Shape("Star")
            );
            flip.MoreRays = WorkshopCraftAssets.Board(
                screen,
                "MoreRays",
                WorkshopCraftAssets.Shape("Star")
            );
            flip.Ring = WorkshopCraftAssets.Board(
                screen,
                "Ring",
                WorkshopCraftAssets.Shape("Ring")
            );
            flip.MoreRings = WorkshopCraftAssets.Boards(
                screen,
                "MoreRing",
                3,
                WorkshopCraftAssets.Shape("Ring")
            );
            var rim = Rect("Rim", screen);
            WorkshopCraftAssets.Center(rim);
            flip.Rim = Sliced(rim, UiArt.FeatherPath, Color.white);
            flip.Rim.raycastTarget = false;
            rim.gameObject.SetActive(false);

            // 奥の半分の光の軌跡は、カードの奥を通る。
            flip.OrbitBacks = OrbitBoards(screen, "OrbitBack", front: false);

            // カード：表は押したときに強化する前と後の札を複製して入れ、裏は戦闘のカードの裏の絵。
            var card = Rect("Card", screen);
            WorkshopCraftAssets.Center(card);
            flip.Card = card;
            flip.BeforeFace = Rect("BeforeFace", card);
            Stretch(flip.BeforeFace);
            flip.AfterFace = Rect("AfterFace", card);
            Stretch(flip.AfterFace);
            var back = Rect("Back", card);
            WorkshopCraftAssets.Center(back);
            flip.Back = back.gameObject.AddComponent<RawImage>();
            flip.Back.texture = Baryonyx.Combat.Editor.BattleInspectAssets.Art("CardBack");
            flip.Back.raycastTarget = false;
            back.gameObject.SetActive(false);
            // 回る面の影：カードの裏の絵の形で、いちばん上に重ねる。
            var shade = Rect("Shade", card);
            Stretch(shade);
            flip.Shade = shade.gameObject.AddComponent<RawImage>();
            flip.Shade.texture = flip.Back.texture;
            flip.Shade.color = new Color(0f, 0f, 0f, 0f);
            flip.Shade.raycastTarget = false;
            shade.gameObject.SetActive(false);

            // 手前の半分の光の軌跡は、カードの手前を通る。
            flip.OrbitFronts = OrbitBoards(screen, "OrbitFront", front: true);
            flip.Absorb = WorkshopCraftAssets.Board(
                screen,
                "Absorb",
                WorkshopCraftAssets.Shape("Glow")
            );
            const int stones = 5;
            flip.StoneTrails = WorkshopCraftAssets.Boards(
                screen,
                "StoneTrail",
                stones,
                WorkshopCraftAssets.Shape("Streak")
            );
            flip.StoneGlows = WorkshopCraftAssets.Boards(
                screen,
                "StoneGlow",
                stones,
                WorkshopCraftAssets.Shape("Glow")
            );
            flip.Stones = new RawImage[stones];
            for (int i = 0; i < stones; i++)
            {
                var stone = Rect("Stone" + i, screen);
                WorkshopCraftAssets.Center(stone);
                var art = stone.gameObject.AddComponent<RawImage>();
                art.raycastTarget = false;
                stone.gameObject.SetActive(false);
                flip.Stones[i] = art;
            }
            flip.Embers = WorkshopCraftAssets.Boards(
                screen,
                "Ember",
                18,
                WorkshopCraftAssets.Shape("Spark")
            );
            flip.Converge = WorkshopCraftAssets.Boards(
                screen,
                "Converge",
                20,
                WorkshopCraftAssets.Shape("Streak")
            );
            flip.Bursts = WorkshopCraftAssets.Boards(
                screen,
                "Burst",
                28,
                WorkshopCraftAssets.Shape("Spark")
            );
            flip.Corners = WorkshopCraftAssets.Boards(
                screen,
                "Corner",
                4,
                WorkshopCraftAssets.Shape("Sparkle")
            );
            flip.Twinkles = WorkshopCraftAssets.Boards(
                screen,
                "Twinkle",
                6,
                WorkshopCraftAssets.Shape("Sparkle")
            );
            flip.Sheen = WorkshopCraftAssets.Board(
                screen,
                "Sheen",
                WorkshopCraftAssets.Shape("Sparkle")
            );
            flip.Flakes = WorkshopCraftAssets.Boards(
                screen,
                "Flake",
                16,
                WorkshopCraftAssets.Shape("Shard")
            );
            flip.Prisms = WorkshopCraftAssets.Boards(
                screen,
                "Prism",
                18,
                WorkshopCraftAssets.Shape("Sparkle")
            );

            Baryonyx.Combat.Presentation.BattleVfxImage.EnableShapeChannel(screen);
            screen.gameObject.SetActive(false);
            return flip;
        }

        // 品質のゲージ：上にC・B・A・Sの境目の文字、下に暗い棒。棒には水色（上がった後）と白（今）を重ね、境目に細い線を引く。
        private static void BuildGauge(
            RectTransform layer,
            WorkshopEnhanceView enhance,
            float left,
            float right
        )
        {
            var gauge = Rect("Gauge", layer);
            Stretch(gauge);
            float width = right - left;
            float barTop = GaugeTop + 44f;
            var bar = Layout.Box(gauge, "Bar", left, barTop, width, GaugeBar);
            AddImage(bar, GaugeBack, false);
            enhance.GaugeAfter = FillBar(bar, "After", GaugeGain);
            enhance.GaugeNow = FillBar(bar, "Now", UiPalette.TextMain);
            foreach (
                var (letter, place) in new[] { ("C", 0f), ("B", 0.4f), ("A", 0.7f), ("S", 0.9f) }
            )
            {
                float x = left + width * place;
                if (place > 0f)
                    AddImage(
                        Layout.Box(gauge, "Mark" + letter, x - 1f, barTop - 6f, 2f, GaugeBar + 12f),
                        GuideFrameAssets.DividerColor,
                        false
                    );
                Inventory.Quality.TryParse(letter, out var quality);
                Layout.Text(
                    gauge,
                    "Letter" + letter,
                    letter,
                    30,
                    Inventory.QualityPalette.Of(quality),
                    TextAlignmentOptions.Bottom,
                    x - 30f,
                    GaugeTop,
                    60f,
                    40f
                );
            }
            gauge.gameObject.SetActive(false);
            enhance.Gauge = gauge.gameObject;
        }

        private static RectTransform FillBar(RectTransform bar, string name, Color color)
        {
            var fill = Rect(name, bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            AddImage(fill, color, false);
            return fill;
        }

        // 素材の段：磨き石の札1枚を左に置く。幅は右の面の右端までを4等分した1つ分にする。
        private static void BuildMaterials(
            RectTransform layer,
            WorkshopEnhanceView enhance,
            float left,
            float right,
            float top
        )
        {
            var art = ItemArtAssets.Load();
            var materials = new[] { EnhancementMaterials.Stone };
            enhance.Chips = new WorkshopMaterialChip[materials.Length];
            const int columns = 4;
            float width = (right - left - ChipGap * (columns - 1)) / columns;
            for (int i = 0; i < materials.Length; i++)
            {
                var rect = Layout.Box(
                    layer,
                    "Chip" + i,
                    left + i * (width + ChipGap),
                    top,
                    width,
                    BarHeight
                );
                var (card, inside) = GlassCardAssets.Build(rect, pattern: false);
                var chip = rect.gameObject.AddComponent<WorkshopMaterialChip>();
                chip.Card = card;
                // 絵を上に、持っている数をその下に置く。
                var box = Layout.Box(inside, "Art", (width - ChipArt) / 2f, 12f, ChipArt, ChipArt);
                chip.Art = box.gameObject.AddComponent<RawImage>();
                chip.Art.raycastTarget = false;
                chip.Art.texture = art.TextureOf(materials[i].Id);
                chip.Art.enabled = chip.Art.texture != null;
                chip.Name = Layout.Text(
                    inside,
                    "Name",
                    chip.Art.enabled ? "" : materials[i].ShortName,
                    34,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center,
                    0f,
                    12f,
                    width,
                    ChipArt
                );
                chip.Count = Layout.Text(
                    inside,
                    "Count",
                    "×0",
                    32,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center,
                    0f,
                    12f + ChipArt + 4f,
                    width,
                    BarHeight - ChipArt - 22f
                );
                // 使う数を水色で「3/8」と書くため、色の指定を読む。
                chip.Count.richText = true;
                chip.Cancel = CancelMark(rect, width);
                enhance.Chips[i] = chip;
            }
        }

        // 使っている色の札の内側の右上に出す、選ぶのをやめるボタン。暗い縁の赤い丸に白い×を描き、
        // 札の上でもボタンと分かる形にする。押せる広さは札の角に収め、指で押せる大きさにする。
        private static Button CancelMark(RectTransform chip, float width)
        {
            var hit = Layout.Box(chip, "Cancel", width - CancelHit, 0f, CancelHit, CancelHit);
            AddImage(hit, Color.clear, true);
            // 丸の中心を札の角から CancelInset だけ内側に寄せる。押せる広さの中心からのずれ。
            float shift = CancelHit / 2f - CancelInset - CancelRim / 2f;
            var center = new Vector2(shift, shift);
            var rim = Rect("Rim", hit);
            Place(rim, center, Vector2.one * CancelRim);
            SpriteImage(rim, UiArt.CirclePath, CancelRimColor);
            var disc = Rect("Disc", hit);
            Place(disc, center, Vector2.one * CancelDisc);
            var button = Baryonyx.Editor.UI.UiBuild.AddButton(
                hit,
                SpriteImage(disc, UiArt.CirclePath, CancelRed)
            );
            GuideFrameAssets.CrossIcon(hit, center, CancelCross);
            hit.gameObject.SetActive(false);
            return button;
        }
    }
}
