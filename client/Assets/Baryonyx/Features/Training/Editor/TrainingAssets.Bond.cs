using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Party;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
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
    // 絆（doc/features/monetization.md）：右上の「絆」のボタン、重ねて開く窓、深めた瞬間の演出。
    public static partial class TrainingAssets
    {
        public const string BondLitPath = Folder + "/Art/BondStone.png";
        public const string BondDarkPath = Folder + "/Art/BondStoneDark.png";

        // 右上の「レベルアップ」と、その左の「絆」（設計座標、右の面の左上から）。
        private const float LevelUpWidth = 330f;
        private static float LevelUpX => Panel.width - PanelPad - LevelUpWidth;
        private const float BondWidth = 220f;
        private const float BondGap = 12f;

        // 「絆」の中の6つのしるし（11×11ドットを2倍）。
        private const float PipDot = 2f;
        private const float PipGap = 4f;

        // 重ねて開く絆の窓。レベルアップの窓より、段の石の列の分だけ高い。
        private static readonly Rect BondModal = new(460, 144, 1000, 792);
        private const float StonesY = 150f;
        private const float StoneDot = 6f;
        private const float StoneGap = 60f;
        private const float BondDiffTop = 252f;

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

        // 右上の「絆」（すりガラスのカード）。灯った段を6つのしるしで見せ、押すと重ねた窓を開く。
        private static void BuildBond(RectTransform panel, TrainingView view)
        {
            var (lit, dark) = BondArt();
            view.BondLit = lit;
            view.BondDark = dark;
            var (button, _, bond) = GlassCardAssets.Choice(
                Layout.Box(panel, "Bond", LevelUpX - BondGap - BondWidth, 18, BondWidth, 96),
                pattern: false
            );
            view.Bond = button;
            var label = Label(
                bond,
                "Label",
                "絆",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.At(label, 20, 0, 40, 96);
            float pip = StoneSize * PipDot;
            view.BondPips = Enumerable
                .Range(0, TrainingBond.Max)
                .Select(i =>
                {
                    var rect = Layout.Box(
                        bond,
                        "Pip" + i,
                        60 + i * (pip + PipGap),
                        (96 - pip) / 2f,
                        pip,
                        pip
                    );
                    var image = rect.gameObject.AddComponent<Image>();
                    image.sprite = dark;
                    image.raycastTarget = false;
                    return image;
                })
                .ToArray();
        }

        // --- The bond dialog, over the whole screen ---------------------------------------

        private static void BuildBondDialog(RectTransform root, TrainingView view)
        {
            var dialog = Rect("BondDialog", root);
            Stretch(dialog);
            view.BondDialog = dialog.gameObject;
            var dim = Rect("Dim", dialog);
            Stretch(dim);
            dim.offsetMin = new Vector2(-400, -400);
            dim.offsetMax = new Vector2(400, 400);
            AddImage(dim, Dim, true);

            var layer = Layout.Layer(dialog, "Layout");
            var modal = GuideFrameAssets.Window(
                layer,
                "Modal",
                BondModal.x,
                BondModal.y,
                BondModal.width,
                BondModal.height
            );

            // 上の段：今の段と深めたあとの段（レベルアップの窓のLvと同じ組み方）。上限では次の段を隠す。
            var stages = Layout.Row(
                modal,
                "Stages",
                ModalPad,
                TopY + DigitLift,
                DiffWidth,
                TopHeight,
                22,
                TextAnchor.MiddleLeft
            );
            Layout.Fit(
                Label(
                    stages,
                    "FromTag",
                    "絆",
                    36,
                    UiPalette.TextSub,
                    TextAlignmentOptions.Baseline
                ),
                0
            );
            view.BondFrom = Label(stages, "From", "", 64, Before, TextAlignmentOptions.Baseline);
            Layout.Fit(view.BondFrom, 0);
            var next = Rect("Next", stages);
            view.BondNext = next.gameObject;
            var nextGroup = next.gameObject.AddComponent<HorizontalLayoutGroup>();
            nextGroup.spacing = 22;
            nextGroup.childAlignment = TextAnchor.MiddleLeft;
            nextGroup.childControlWidth = nextGroup.childControlHeight = true;
            nextGroup.childForceExpandWidth = false;
            nextGroup.childForceExpandHeight = true;
            var arrowCell = Rect("Arrow", next);
            Layout.Fit(arrowCell, 5 * LevelArrowScale);
            var arrow = Rect("Image", arrowCell);
            Place(arrow, new Vector2(0, DigitLift), new Vector2(5, 9) * LevelArrowScale);
            SpriteImage(arrow, Guide.ArrowPath, UiPalette.Gold);
            Layout.Fit(
                Label(next, "ToTag", "絆", 36, UiPalette.TextSub, TextAlignmentOptions.Baseline),
                0
            );
            view.BondTo = Label(next, "To", "", 64, UiPalette.Gold, TextAlignmentOptions.Baseline);
            Layout.Fit(view.BondTo, 0);

            // 6つの段の石。次に灯る石は、後ろの光で囲む。
            var glow = Rect("Glow", modal);
            Place(glow, Vector2.zero, Vector2.one * 150);
            SpriteImage(glow, Guide.SoftSpotPath, new Color(1f, 0.82f, 0.4f, 0.3f));
            view.BondGlow = glow;
            float stone = StoneSize * StoneDot;
            float row = stone * TrainingBond.Max + StoneGap * (TrainingBond.Max - 1);
            view.BondStones = Enumerable
                .Range(0, TrainingBond.Max)
                .Select(i =>
                    StoneImage(
                        modal,
                        "Stone" + i,
                        view.BondDark,
                        new Vector2(
                            (BondModal.width - row) / 2f + stone / 2f + i * (stone + StoneGap),
                            StonesY + stone / 2f
                        ),
                        BondModal.size,
                        stone
                    )
                )
                .ToArray();

            view.BondDiffs = Enumerable
                .Range(0, CharacterStats.Count)
                .Select(i => BuildDiff(modal, i, ModalPad, BondDiffTop + i * DiffHeight))
                .ToArray();

            // 下の段：左に「やめる」、右にその人の絆のしるしを添えた「絆を深める」。
            float footY = BondModal.height - ModalPad - FootHeight;
            float confirmWidth = DiffWidth - CancelWidth - 24;
            float confirmX = BondModal.width - ModalPad - confirmWidth;
            var (cancelButton, _, cancel) = GlassCardAssets.Choice(
                Layout.Box(modal, "Cancel", ModalPad, footY, CancelWidth, FootHeight),
                pattern: false
            );
            view.BondCancel = cancelButton;
            Stretch(
                (RectTransform)
                    Label(
                        cancel,
                        "Label",
                        "やめる",
                        40,
                        UiPalette.TextMain,
                        TextAlignmentOptions.Center
                    ).transform
            );
            var (confirmButton, confirmRim, confirm) = GlassCardAssets.Choice(
                Layout.Box(modal, "Confirm", confirmX, footY, confirmWidth, FootHeight),
                pattern: false
            );
            confirmRim.SetActive(true);
            view.BondConfirm = confirmButton;
            view.BondConfirmLabel = Label(
                confirm,
                "Label",
                "絆を深める",
                42,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Layout.At(view.BondConfirmLabel, 0, 14, confirmWidth, 56);
            const float tokenSize = 48f;
            var cost = Layout.Row(
                confirm,
                "Cost",
                0,
                72,
                confirmWidth,
                tokenSize,
                10,
                TextAnchor.MiddleCenter
            );
            view.BondCost = cost.gameObject;
            var tokenCell = Rect("Token", cost);
            Layout.Fit(tokenCell, tokenSize);
            var token = Rect("Image", tokenCell);
            Place(token, Vector2.zero, Vector2.one * tokenSize);
            view.BondToken = token.gameObject.AddComponent<RawImage>();
            view.BondToken.raycastTarget = false;
            view.BondTokens = Label(
                cost,
                "Value",
                "",
                34,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.Fit(view.BondTokens, 0);

            dialog.gameObject.SetActive(false);
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

            // 右の札：名前、絆の段、上がるステータス。
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
                Label(stageRow, "Tag", "絆", 52, UiPalette.TextMain, TextAlignmentOptions.Baseline),
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

        // 停止中の表示の切り替え（「絆の演出」）で見られるよう、1人目が次の段を灯した姿で作り込む。
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
