using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Equipment;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Training.Editor
{
    // 限界突破（doc/features/monetization.md。コードとサーバーでは bond と呼ぶ）：突破のタブと、突破した瞬間の演出。
    public static partial class TrainingAssets
    {
        public const string BondLitPath = Folder + "/Art/BondStone.png";
        public const string BondDarkPath = Folder + "/Art/BondStoneDark.png";

        // 突破のタブの右の面（左上から）：見出しの下の6段の札。石は11×11ドットを6倍で出す。
        private const float StagesTop = 84f;
        private const float StageHeight = 134f;
        private const float StageGap = 10f;
        private const float StageStoneDot = 6f;
        private const float StageActionWidth = 220f;
        private const float StageButtonHeight = 96f;

        // 深めた瞬間：イラスト（2:3）と、それを囲む6つの石の弧、右の札。
        private static readonly Rect CelebrationArt = new(480, 70, 640, 960);
        private static readonly Vector2 ArcCenter = new(800, 650);
        private const float ArcRadius = 440f;
        private const float ArcFrom = 160f;
        private const float ArcTo = 20f;
        private const float CelebrationDot = 7f;
        private static readonly Rect CelebrationCard = new(1330, 330, 500, 420);

        private const int StoneSize = 11;

        private static readonly string[] StoneDots =
        {
            ".....D.....",
            "....DLD....",
            "...DLLGD...",
            "..DLLGGGD..",
            ".DLGGGGGGD.",
            "DGGGGGGGGSD",
            ".DGGGGGGSD.",
            "..DGGGGSD..",
            "...DGGSD...",
            "....DSD....",
            ".....D.....",
        };

        private static Color LitStone(char dot) =>
            dot switch
            {
                'D' => new Color32(0x7a, 0x3a, 0x0c, 0xff),
                'L' => new Color32(0xff, 0xf2, 0xb0, 0xff),
                'G' => new Color32(0xff, 0xc9, 0x4a, 0xff),
                'S' => new Color32(0xd9, 0x85, 0x1f, 0xff),
                _ => Color.clear,
            };

        private static Color DarkStone(char dot) =>
            dot switch
            {
                'D' => new Color32(0x7f, 0x88, 0xab, 0xff),
                'L' => new Color32(0x3a, 0x44, 0x70, 0xff),
                'G' => new Color32(0x23, 0x2a, 0x4a, 0xff),
                'S' => new Color32(0x17, 0x1c, 0x34, 0xff),
                _ => Color.clear,
            };

        private static (Sprite Lit, Sprite Dark) BondArt()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(BondLitPath));
            ArtAssets.WritePattern(BondLitPath, StoneDots, LitStone);
            ArtAssets.WritePattern(BondDarkPath, StoneDots, DarkStone);
            return (ArtAssets.LoadSprite(BondLitPath), ArtAssets.LoadSprite(BondDarkPath));
        }

        // --- The breakthrough tab ---------------------------------------------------------

        /// <summary>
        /// Builds the adventurer's breakthrough tab over the guide screen's safe area: the same
        /// illustration as the stats tab on the left, and on the right 「限界突破」 over the six
        /// stages, each a glass card with its stone, number and effect, 「突破済み」 once reached and
        /// 「限界突破」 on the next stage; the stage lighting up shows over everything after it.
        /// </summary>
        public static GameObject BuildBreakthroughPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var data = CreateData();

            var (view, layer) = GuideFrameAssets.FramedPanel<TrainingView>(safe, "Breakthrough");
            view.Party = party;
            view.Data = data;
            view.Guide = guide;
            (view.BondLit, view.BondDark) = BondArt();

            GuideFrameAssets.SplitPanes(
                layer,
                Split,
                "PortraitPane",
                "StagesPane",
                divider: false,
                from: Left,
                to: PartySheetAssets.ContentRight
            );
            view.Swipe = layer.gameObject.AddComponent<GuideSwipe>();
            BuildPortrait(layer, view, party);
            GuideFrameAssets.Divider(layer, Split, Portrait.y);
            BuildStages(layer, view);
            view.Sheet = PartySheetAssets.Build(
                layer,
                guide,
                party,
                TrainingSession.BreakthroughItemKey,
                SheetTabs
            );
            BuildCelebration((RectTransform)view.transform, view);

            TrainingView.Build(view, party, data, EquipmentLocalSource.Starter()).Dispose();
            BakeCelebration(view, party, data);
            return view.gameObject;
        }

        // 右の面：見出し「限界突破」の下に、6段を上から縦に並べる。効果の文が2〜3行になっても読めるよう、
        // 札は面の幅いっぱいに取り、文の右に「突破済み」か「限界突破」を置く。
        private static void BuildStages(RectTransform layer, TrainingView view)
        {
            var panel = Layout.Box(layer, "Stages", Panel.x, Panel.y, Panel.width, Panel.height);
            float width = Panel.width - PanelPad * 2;
            GuideFrameAssets.Heading(panel, "Title", "限界突破", PanelPad - 4, 18, 400, 48, 30);
            view.Stages = Enumerable
                .Range(0, TrainingBond.Max)
                .Select(i =>
                    BuildStage(
                        panel,
                        i,
                        PanelPad,
                        StagesTop + i * (StageHeight + StageGap),
                        width,
                        view.BondDark
                    )
                )
                .ToArray();
        }

        private static TrainingStageWidget BuildStage(
            RectTransform panel,
            int index,
            float x,
            float y,
            float width,
            Sprite dark
        )
        {
            var rect = Layout.Box(panel, "Stage" + (index + 1), x, y, width, StageHeight);
            var (card, inside) = GlassCardAssets.Build(rect, pattern: false);
            var widget = new TrainingStageWidget { Card = card };

            // 段の石（11×11ドットを6倍）と、その上の段の番号。
            float stone = StoneSize * StageStoneDot;
            var stoneRect = Layout.Box(
                inside,
                "Stone",
                30,
                (StageHeight - stone) / 2f,
                stone,
                stone
            );
            widget.Stone = stoneRect.gameObject.AddComponent<Image>();
            widget.Stone.sprite = dark;
            widget.Stone.raycastTarget = false;
            widget.Number = Layout.Text(
                stoneRect,
                "Number",
                (index + 1).ToString(),
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                0,
                0,
                stone,
                stone
            );

            const float textLeft = 130f;
            float actionLeft = width - StageActionWidth - 24;
            widget.Effect = Layout.Text(
                inside,
                "Effect",
                "",
                28,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                textLeft,
                0,
                actionLeft - textLeft - 16,
                StageHeight
            );
            // 効果は3行まで折り返し、入りきらない長い文だけ少し縮める。
            widget.Effect.textWrappingMode = TextWrappingModes.Normal;
            Guide.Shrink(widget.Effect, 22);

            widget.Done = Layout
                .Text(
                    inside,
                    "Done",
                    "突破済み",
                    24,
                    UiPalette.Gold,
                    TextAlignmentOptions.MidlineRight,
                    actionLeft,
                    0,
                    StageActionWidth,
                    StageHeight
                )
                .gameObject;

            var (button, _, label) = GlassCardAssets.Choice(
                Layout.Box(
                    inside,
                    "BreakThrough",
                    actionLeft,
                    (StageHeight - StageButtonHeight) / 2f,
                    StageActionWidth,
                    StageButtonHeight
                ),
                pattern: false
            );
            widget.BreakThrough = button;
            var text = Label(
                label,
                "Label",
                "限界突破",
                28,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)text.transform);
            return widget;
        }

        // --- The moment the bond deepens ---------------------------------------------------

        private static void BuildCelebration(RectTransform root, TrainingView view)
        {
            var rect = Rect("BondCelebration", root);
            Stretch(rect);
            var celebration = rect.gameObject.AddComponent<TrainingBondCelebration>();
            view.Celebration = celebration;
            celebration.Group = rect.gameObject.AddComponent<CanvasGroup>();
            celebration.Lit = view.BondLit;
            celebration.Dark = view.BondDark;

            // 画面全体を暗い紺で覆い、どこを押しても閉じる。
            var dim = Rect("Dim", rect);
            Stretch(dim);
            dim.offsetMin = new Vector2(-400, -400);
            dim.offsetMax = new Vector2(400, 400);
            var dimImage = AddImage(dim, new Color(0.012f, 0.018f, 0.05f, 1f), true);
            celebration.Close = AddButton(dim, dimImage);

            var layer = Layout.Layer(rect, "Layout");
            var art = Layout.Box(
                layer,
                "Illustration",
                CelebrationArt.x,
                CelebrationArt.y,
                CelebrationArt.width,
                CelebrationArt.height
            );
            celebration.Illustration = art.gameObject.AddComponent<RawImage>();
            celebration.Illustration.raycastTarget = false;
            // イラストの左右と下を背景の紺へなじませる。
            var navy = new Color(0.012f, 0.018f, 0.05f, 1f);
            Fade(art, "FadeLeft", new Rect(0, 0, 180, CelebrationArt.height), navy, flip: true);
            Fade(
                art,
                "FadeRight",
                new Rect(CelebrationArt.width - 180, 0, 180, CelebrationArt.height),
                navy,
                flip: false
            );
            FadeEdge(art, "FadeTop", top: true, 200, navy);
            FadeEdge(art, "FadeBottom", top: false, 260, navy);

            // 石の弧。隣の石を金の細い線でつなぐ（線は石の後ろ）。
            var points = Enumerable
                .Range(0, TrainingBond.Max)
                .Select(i =>
                {
                    float angle =
                        Mathf.Lerp(ArcFrom, ArcTo, i / (float)(TrainingBond.Max - 1))
                        * Mathf.Deg2Rad;
                    return ArcCenter + new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * ArcRadius;
                })
                .ToArray();
            celebration.Links = Enumerable
                .Range(0, TrainingBond.Max - 1)
                .Select(i =>
                {
                    var link = Rect("Link" + i, layer);
                    var from = Center(points[i]);
                    var to = Center(points[i + 1]);
                    Place(link, (from + to) / 2f, new Vector2(Vector2.Distance(from, to), 4f));
                    link.localRotation = Quaternion.Euler(
                        0,
                        0,
                        Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg
                    );
                    return AddImage(link, celebration.LinkDark, false);
                })
                .ToArray();
            var burst = Rect("Burst", layer);
            Place(burst, Vector2.zero, Vector2.one * 220);
            celebration.Burst = SpriteImage(burst, Guide.SoftSpotPath, Color.white);
            burst.gameObject.SetActive(false);
            float stone = StoneSize * CelebrationDot;
            celebration.Stones = points
                .Select(
                    (point, i) =>
                        StoneImage(layer, "Stone" + i, view.BondDark, point, Layout.Design, stone)
                )
                .ToArray();

            // 右の札：名前、突破の段、上がるステータス。
            var card = Layout.Box(
                layer,
                "Card",
                CelebrationCard.x,
                CelebrationCard.y,
                CelebrationCard.width,
                CelebrationCard.height
            );
            celebration.Card = card.gameObject.AddComponent<CanvasGroup>();
            celebration.Card.blocksRaycasts = false;
            GlassCardAssets.Plaque(card, pattern: false);
            float width = CelebrationCard.width;
            celebration.Name = Layout.Text(
                card,
                "Name",
                "",
                44,
                UiPalette.Gold,
                TextAlignmentOptions.Center,
                0,
                36,
                width,
                60
            );
            var stageRow = Layout.Row(
                card,
                "Stage",
                0,
                110,
                width,
                130,
                20,
                TextAnchor.MiddleCenter
            );
            Layout.Fit(
                Label(
                    stageRow,
                    "Tag",
                    "突破",
                    52,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Baseline
                ),
                0
            );
            celebration.Stage = Label(
                stageRow,
                "Value",
                "",
                120,
                UiPalette.Gold,
                TextAlignmentOptions.Baseline
            );
            Layout.Fit(celebration.Stage, 0);
            Layout.Line(card, "Line", 50, 270, width - 100, 2);
            celebration.Effect = Layout.Text(
                card,
                "Effect",
                "",
                40,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                0,
                300,
                width,
                70
            );

            rect.gameObject.SetActive(false);
        }

        // 停止中の表示の切り替え（「限界突破の演出」）で見られるよう、1人目が次の段を灯した姿で作り込む。
        private static void BakeCelebration(
            TrainingView view,
            PartyMockData party,
            TrainingMockData data
        )
        {
            var first = party.Members[0];
            int stage = Mathf.Clamp((data.Find(first.Id)?.Bond ?? 0) + 1, 1, TrainingBond.Max);
            view.Celebration.Show(first, stage);
            view.Celebration.Hide();
            view.Celebration.gameObject.SetActive(false);
        }

        // 設計座標の点（左上から）を、中央を原点にした位置にする。
        private static Vector2 Center(Vector2 point) =>
            new(point.x - Layout.Design.x / 2f, Layout.Design.y / 2f - point.y);

        // 段の石。center は parent の左上からの設計座標。
        private static Image StoneImage(
            RectTransform parent,
            string name,
            Sprite sprite,
            Vector2 center,
            Vector2 parentSize,
            float size
        )
        {
            var rect = Rect(name, parent);
            Place(
                rect,
                new Vector2(center.x - parentSize.x / 2f, parentSize.y / 2f - center.y),
                Vector2.one * size
            );
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        // 上端または下端が濃く、中へ向かって薄くなる帯。
        private static void FadeEdge(
            RectTransform parent,
            string name,
            bool top,
            float height,
            Color color
        )
        {
            var rect = Rect(name, parent);
            rect.anchorMin = new Vector2(0, top ? 1 : 0);
            rect.anchorMax = new Vector2(1, top ? 1 : 0);
            rect.pivot = new Vector2(0.5f, top ? 1 : 0);
            rect.sizeDelta = new Vector2(0, height);
            rect.anchoredPosition = Vector2.zero;
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = ArtAssets.LoadTexture(UiArt.ShadePath);
            image.color = color;
            image.uvRect = top ? new Rect(0, 0, 1, 1) : new Rect(0, 1, 1, -1);
            image.raycastTarget = false;
        }

        // 横に薄くなる帯。flip なら左端が濃い。
        private static void Fade(
            RectTransform parent,
            string name,
            Rect box,
            Color color,
            bool flip
        )
        {
            var rect = Layout.Box(parent, name, box.x, box.y, box.width, box.height);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Guide.ShadeHorizontalPath);
            image.color = color;
            image.uvRect = flip ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
            image.raycastTarget = false;
        }
    }
}
