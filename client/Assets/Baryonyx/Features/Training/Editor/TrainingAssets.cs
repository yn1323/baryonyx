using System.IO;
using System.Linq;
using Baryonyx.Combat.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Training.Editor
{
    /// <summary>
    /// The training mock data and the tavern's training. The tavern's generator calls
    /// <see cref="BuildTrainingPanel"/> for its training item, so the detail and the level-up
    /// dialog are baked into the tavern prefab and read in the editor without Play Mode.
    /// Positions are design pixels from the top left of a 1920x1080 layer that shrinks on
    /// screens narrower than 16:9. The growth, skills and costs are mock data until levels
    /// have data (doc/features/progression.md).
    /// </summary>
    public static class TrainingAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Training";
        public const string DataPath = Folder + "/Data/TrainingMockData.asset";
        public const string LockPath = Folder + "/UI/Art/TrainingLock.png";
        public const string IconRunePath =
            "Assets/Baryonyx/Shared/Art/GameResources/IconRune.aseprite";

        private const float Dot = Guide.DotScale;
        private static readonly Vector2 Design = new(1920, 1080);

        // 左の列（キャラ）と右の枠（ステータス・スキル・カード）。
        private const float LeftX = 60f;
        private const float LeftWidth = 680f;

        // 左上の「もどる」（下端が192）と重ならないよう、列はこの高さから下に置く。
        private const float ColumnTop = 200f;
        private const float FeetY = 660f;
        private const float HitSize = 128f;
        private static readonly Rect Panel = new(780, 168, 1100, 880);
        private const float PanelPad = 36f;
        private const float CellGap = 12f;

        // 重ねて開くレベルアップ。
        private static readonly Rect Modal = new(280, 150, 1360, 890);
        private const float ModalPad = 48f;

        // カードの挿絵（64×58）の中央24×24ドット。スキルの仮のアイコンに使う。
        private static readonly Rect IconCrop = new(20f / 64f, 17f / 58f, 24f / 64f, 24f / 58f);

        private static readonly Color Green = new(0.49f, 0.88f, 0.42f);
        private static readonly Color Before = new(0.72f, 0.75f, 0.83f);
        private static readonly Color Line = new(0.27f, 0.32f, 0.48f, 0.55f);
        private static readonly Color Inset = new(0.03f, 0.05f, 0.12f, 0.75f);
        private static readonly Color Dim = new(0.01f, 0.015f, 0.04f, 0.74f);
        private static readonly Color Warm = new(1f, 0.84f, 0.55f, 0.3f);

        private static readonly Color Backdrop = new(0.02f, 0.03f, 0.07f, 0.62f);

        [MenuItem("Baryonyx/Training/Create Mock Data")]
        public static TrainingMockData CreateData()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            AssetDatabase.Refresh();
            var data = AssetDatabase.LoadAssetAtPath<TrainingMockData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<TrainingMockData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            data.CostPerLevel = 100;
            data.MaxLevel = 30;
            data.MockRunes = 8450;
            // 4人のステータスは、戦闘画面のモックの値を今のレベルでの値にした。絵のないキャラは、
            // 絵を借りたキャラと同じ伸び方にする。
            var toma = Mage();
            var luka = Archer();
            var aria = Knight();
            var mina = Healer();
            data.Characters = new[]
            {
                Character("toma", toma),
                Character("luka", luka),
                Character("aria", aria),
                Character("mina", mina),
                Character("anselm", aria),
                Character("greta", luka),
                Character("lutz", toma),
                Character("rita", mina),
                Character("ritsu", luka),
            };
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            return data;
        }

        private static TrainingCharacter Character(string id, TrainingCharacter growth) =>
            new()
            {
                Id = id,
                Base = growth.Base,
                Growth = growth.Growth,
                Passives = growth.Passives,
                Uniques = growth.Uniques,
            };

        // トーマ（Lv 12で HP 300・まりょく 318）。
        private static TrainingCharacter Mage() =>
            Growth(
                at: 12,
                new TrainingStats(300, 90, 318, 110, 72),
                new TrainingStats(18, 3, 18, 5, 2),
                Skill("魔力の泉", "戦闘の始めにエネルギー +1", 1, 0, "CardManaPrayer"),
                Skill("炎の心得", "炎のカードの威力 +10%", 15, 0, "CardFlameEnchant"),
                Skill("マナバースト", "敵全体に まりょくの80% のダメージ", 5, 3, "CardRevelation"),
                Skill("星降り", "ランダムな敵に5回ダメージ", 20, 5, "CardMeteor")
            );

        private static TrainingCharacter Archer() =>
            Growth(
                at: 11,
                new TrainingStats(360, 230, 260, 120, 84),
                new TrainingStats(20, 12, 12, 5, 3),
                Skill("狩人の目", "敵の弱点を見つけやすくなる", 1, 0, "CardInsightArrow"),
                Skill("疾風", "すばやさ +10%", 15, 0, "CardGale"),
                Skill("雷撃の矢", "敵1体に雷のダメージ。まひさせる", 5, 3, "CardThunderSpear"),
                Skill("矢の雨", "敵全体に3回ダメージ", 20, 5, "CardArrowRain")
            );

        private static TrainingCharacter Knight() =>
            Growth(
                at: 10,
                new TrainingStats(480, 243, 120, 200, 60),
                new TrainingStats(30, 14, 5, 12, 2),
                Skill("守護の誓い", "味方が受けるダメージ -5%", 1, 0, "CardGuardianOath"),
                Skill("不屈", "倒れるダメージを1度だけ耐える", 15, 0, "CardDivineShield"),
                Skill("盾の構え", "次のターンまでブロック +80", 5, 2, "CardProtect"),
                Skill("一閃", "敵1体に ちからの200% のダメージ", 20, 5, "CardIai")
            );

        private static TrainingCharacter Healer() =>
            Growth(
                at: 10,
                new TrainingStats(310, 100, 200, 140, 66),
                new TrainingStats(18, 4, 12, 8, 2),
                Skill("祈り", "ターンの終わりに味方のHPを少し回復", 1, 0, "CardRegen"),
                Skill("浄化の光", "状態異常に1回かからない", 15, 0, "CardPurify"),
                Skill("癒しの風", "味方全体のHPを回復", 5, 3, "CardHeal"),
                Skill("蘇生", "倒れた味方1人を復活させる", 20, 6, "CardResurrection")
            );

        // 今のレベルでのステータスと伸びから、Lv 1 のステータスを求める。
        private static TrainingCharacter Growth(
            int at,
            TrainingStats now,
            TrainingStats growth,
            TrainingSkill passive1,
            TrainingSkill passive2,
            TrainingSkill unique1,
            TrainingSkill unique2
        ) =>
            new()
            {
                Base = now - growth * (at - 1),
                Growth = growth,
                Passives = new[] { passive1, passive2 },
                Uniques = new[] { unique1, unique2 },
            };

        // スキルの絵はまだないため、カードスキルの挿絵を仮のアイコンにする。
        private static TrainingSkill Skill(
            string name,
            string description,
            int level,
            int energy,
            string art
        ) =>
            new()
            {
                Name = name,
                Description = description,
                UnlockLevel = level,
                Energy = energy,
                Icon = ArtAssets.LoadTexture($"{BattleInspectAssets.ArtFolder}/{art}.aseprite"),
            };

        /// <summary>
        /// Builds the training over the guide screen's safe area: the character on the left,
        /// one frame of stats, skills and cards on the right, and the level-up dialog (hidden)
        /// over everything. Called while the guide screen's prefab is being built, so the shared
        /// frames and font are ready.
        /// </summary>
        public static GameObject BuildTrainingPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var data = CreateData();
            WriteLock();

            var root = Rect("Training", safe);
            Stretch(root);
            var view = root.gameObject.AddComponent<TrainingView>();
            view.Party = party;
            view.Data = data;
            view.Guide = guide;

            var layer = Layer(root, "Layout");
            BuildCharacter(layer, view, party);
            BuildDetail(layer, view);
            BuildDialog(root, view);

            // 停止中のPrefabでも1人目が読めるよう、生成時に一度描く。
            TrainingView.Build(view, party, data).Dispose();
            return root.gameObject;
        }

        // A 1920x1080 layer in the middle that shrinks on screens narrower than 16:9.
        private static RectTransform Layer(RectTransform parent, string name)
        {
            var layer = Rect(name, parent);
            Place(layer, Vector2.zero, Design);
            layer.gameObject.AddComponent<WorldLayerFit>().DesignSize = Design;
            return layer;
        }

        // --- Left: the character, the runes and the two buttons --------------------------

        private static void BuildCharacter(
            RectTransform layer,
            TrainingView view,
            PartyMockData party
        )
        {
            float center = LeftX + LeftWidth / 2f;
            // にぎやかな背景の上でも名前やルーンが読めるよう、列の後ろに半透明の暗い板を敷く。
            var plate = Box(
                layer,
                "Backdrop",
                LeftX - 20,
                ColumnTop,
                LeftWidth + 40,
                1044 - ColumnTop
            );
            Sliced(plate, UiArt.RoundedRectPath, Backdrop).raycastTarget = false;
            view.Name = Text(layer, "Name", "", 60, Guide.TextMain, TextAlignmentOptions.Center);
            At(view.Name, LeftX + 40, ColumnTop + 4, LeftWidth - 80, 88);
            Guide.Shrink(view.Name, 36);

            var level = Row(layer, "LevelLine", LeftX, 296, LeftWidth, 96, 14);
            var tag = Text(level, "Tag", "Lv", 32, Guide.TextSub, TextAlignmentOptions.Baseline);
            view.Level = Text(
                level,
                "Level",
                "",
                80,
                Guide.TextMain,
                TextAlignmentOptions.Baseline
            );
            Fit(tag, 0);
            Fit(view.Level, 0);
            view.Elements = Enumerable
                .Range(0, 3)
                .Select(i =>
                {
                    var cell = Rect("Element" + i, level);
                    Fit(cell, 56);
                    var icon = Rect("Icon", cell);
                    Place(icon, new Vector2(0, -6), Vector2.one * 12f * Dot);
                    var image = icon.gameObject.AddComponent<Image>();
                    image.raycastTarget = false;
                    return image;
                })
                .ToArray();

            // 足元の光と影の上に、64×64の戦闘のドット絵を4倍で立たせる。
            var feet = new Vector2(center - Design.x / 2f, Design.y / 2f - FeetY);
            var glow = Rect("Glow", layer);
            Place(glow, feet, new Vector2(320, 80));
            SpriteImage(glow, Guide.SoftSpotPath, Warm);
            Picture(
                layer,
                "Shadow",
                ArtAssets.LoadSprite(UiArt.ShadowPath),
                feet + new Vector2(0, 2),
                new Vector2(170, 28),
                0.55f
            );
            view.Figure = PixelActor(layer, "Figure", party.Members[0].Art, feet, Dot);
            // ◀▶はキャラの左右、体の高さの中ほどに置く。
            float arrowsY = FeetY - 128;
            view.Prev = ArrowButton(layer, "Prev", LeftX + 84, arrowsY, flip: true);
            view.Next = ArrowButton(layer, "Next", LeftX + LeftWidth - 84, arrowsY, flip: false);

            var runes = Row(layer, "Runes", LeftX, 668, LeftWidth, 96, 12);
            var runesTag = Text(
                runes,
                "Tag",
                "所持ルーン",
                28,
                Guide.TextSub,
                TextAlignmentOptions.Midline
            );
            Fit(runesTag, 0);
            Fit(Icon(runes, "Icon", IconRunePath), Guide.IconSize);
            view.Runes = Text(runes, "Value", "", 44, Guide.TextMain, TextAlignmentOptions.Midline);
            Fit(view.Runes, 0);

            var levelUp = Box(layer, "LevelUp", LeftX, 776, LeftWidth, 120);
            view.LevelUp = Guide.AddButton(
                levelUp,
                Guide.Frame(levelUp, Guide.FrameSelectedPath, Color.white)
            );
            view.LevelUpLabel = Text(
                levelUp,
                "Label",
                "レベルアップ",
                44,
                Guide.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            At(view.LevelUpLabel, 36, 0, 360, 120);
            var cost = Box(levelUp, "Cost", 330, 12, 320, 96);
            view.LevelUpCost = cost.gameObject;
            var costIcon = Icon(cost, "Icon", IconRunePath);
            At(costIcon, 0, 0, Guide.IconSize, Guide.IconSize);
            view.LevelUpCostLabel = Text(
                cost,
                "Value",
                "",
                36,
                Guide.TextMain,
                TextAlignmentOptions.MidlineRight
            );
            At(view.LevelUpCostLabel, 96, 0, 170, 96);
            Arrow(cost, "Arrow", 286, 30, 4);

            var cards = Box(layer, "Cards", LeftX, 908, LeftWidth, 120);
            view.Cards = Guide.AddButton(cards, Guide.Frame(cards, Guide.FramePath, Color.white));
            var cardsLabel = Text(
                cards,
                "Label",
                "カードを付け替える",
                44,
                Guide.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            At(cardsLabel, 36, 0, 560, 120);
            Arrow(cards, "Arrow", LeftWidth - 60, 42, 4);
        }

        // 中心が (x, y) の◀▶。見た目の枠は88だが、押せる範囲は指で押しやすい128四方にする。
        private static Button ArrowButton(
            RectTransform layer,
            string name,
            float x,
            float y,
            bool flip
        )
        {
            var rect = Box(layer, name, x - HitSize / 2f, y - HitSize / 2f, HitSize, HitSize);
            AddImage(rect, Color.clear, true);
            var frame = Rect("Frame", rect);
            Place(frame, Vector2.zero, Vector2.one * 88f);
            var image = Guide.Frame(frame, Guide.FramePath, Color.white);
            image.raycastTarget = false;
            var button = Guide.AddButton(rect, image);
            var arrow = Rect("Arrow", frame);
            Place(arrow, Vector2.zero, new Vector2(5, 9) * 6f);
            if (flip)
                arrow.localScale = new Vector3(-1, 1, 1);
            SpriteImage(arrow, Guide.ArrowPath, Guide.Gold);
            return button;
        }

        // --- Right: stats, skills and cards in one frame ---------------------------------

        private static void BuildDetail(RectTransform layer, TrainingView view)
        {
            var panel = Box(layer, "Detail", Panel.x, Panel.y, Panel.width, Panel.height);
            Guide.Frame(panel, Guide.FramePath, Color.white).raycastTarget = true;
            float width = Panel.width - PanelPad * 2;
            float cell = (width - 40) / 2f;

            Heading(panel, "StatsTitle", "ステータス", null, 28, width);
            view.Stats = Enumerable
                .Range(0, TrainingStats.Count)
                .Select(i =>
                {
                    float x = PanelPad + (i % 2) * (cell + 40);
                    float y = 84 + (i / 2) * 54;
                    var row = Box(panel, "Stat" + i, x, y, cell, 54);
                    var line = Box(row, "Line", 0, 52, cell, 2);
                    AddImage(line, Line, false);
                    var label = Text(
                        row,
                        "Label",
                        TrainingStats.Labels[i],
                        30,
                        Guide.TextSub,
                        TextAlignmentOptions.BottomLeft
                    );
                    At(label, 0, 0, cell / 2, 50);
                    var value = Text(
                        row,
                        "Value",
                        "",
                        40,
                        Guide.TextMain,
                        TextAlignmentOptions.BottomRight
                    );
                    At(value, cell / 2, 0, cell / 2, 50);
                    return value;
                })
                .ToArray();

            Heading(panel, "SkillsTitle", "スキル", null, 270, width);
            view.Skills = Enumerable
                .Range(0, 4)
                .Select(i =>
                    BuildSkill(
                        panel,
                        i,
                        PanelPad + (i % 2) * (cell + CellGap),
                        326 + (i / 2) * (124 + CellGap),
                        cell
                    )
                )
                .ToArray();

            Heading(panel, "CardsTitle", "カードスキル", "4枚", 608, width);
            view.CardSlots = Enumerable
                .Range(0, PartyFormation.Size)
                .Select(i =>
                    BuildCard(
                        panel,
                        i,
                        PanelPad + (i % 2) * (cell + CellGap),
                        664 + (i / 2) * (76 + CellGap),
                        cell
                    )
                )
                .ToArray();
        }

        private static void Heading(
            RectTransform panel,
            string name,
            string title,
            string note,
            float y,
            float width
        )
        {
            var label = Text(panel, name, title, 36, Guide.Gold, TextAlignmentOptions.BottomLeft);
            At(label, PanelPad, y, width, 48);
            if (note == null)
                return;
            var sub = Text(
                panel,
                name + "Note",
                note,
                24,
                Guide.TextSub,
                TextAlignmentOptions.BottomRight
            );
            At(sub, PanelPad, y, width, 44);
        }

        private static TrainingSkillWidget BuildSkill(
            RectTransform panel,
            int index,
            float x,
            float y,
            float width
        )
        {
            var cell = Box(panel, "Skill" + index, x, y, width, 124);
            var widget = new TrainingSkillWidget
            {
                Frame = Guide.Frame(cell, Guide.FramePath, Color.white),
            };
            widget.Frame.raycastTarget = false;
            var icon = Box(cell, "Icon", 14, 14, Guide.IconSize, Guide.IconSize);
            widget.Icon = icon.gameObject.AddComponent<RawImage>();
            widget.Icon.uvRect = IconCrop;
            widget.Icon.raycastTarget = false;
            // 未解放のスキルは、暗くしたアイコンの上に錠前と解放するレベルを出す。
            var locked = Rect("Lock", icon);
            Stretch(locked);
            var lockImage = Rect("Image", locked);
            Place(lockImage, new Vector2(0, 12), new Vector2(9, 10) * Dot);
            SpriteImage(lockImage, LockPath, Color.white);
            widget.When = Text(locked, "When", "", 22, Guide.Gold, TextAlignmentOptions.Center);
            Place((RectTransform)widget.When.transform, new Vector2(0, -30), new Vector2(96, 28));
            widget.Lock = locked.gameObject;

            float textX = 14 + Guide.IconSize + 16;
            float textWidth = width - textX - 16;
            widget.Type = Text(cell, "Type", "", 22, Guide.TextSub, TextAlignmentOptions.TopLeft);
            At(widget.Type, textX, 12, textWidth, 28);
            widget.Name = Text(cell, "Name", "", 32, Guide.TextMain, TextAlignmentOptions.TopLeft);
            At(widget.Name, textX, 40, textWidth, 42);
            Guide.Shrink(widget.Name, 24);
            widget.Description = Text(
                cell,
                "Description",
                "",
                22,
                Guide.TextSub,
                TextAlignmentOptions.TopLeft
            );
            At(widget.Description, textX, 84, textWidth, 30);
            Guide.Shrink(widget.Description, 18);
            return widget;
        }

        private static TrainingCardWidget BuildCard(
            RectTransform panel,
            int index,
            float x,
            float y,
            float width
        )
        {
            var cell = Box(panel, "Card" + index, x, y, width, 76);
            Guide.Frame(cell, Guide.FramePath, Color.white).raycastTarget = false;
            var badge = Box(cell, "CostBadge", 12, 12, 52, 52);
            Guide.Frame(badge, Guide.FrameSelectedPath, Color.white).raycastTarget = false;
            var widget = new TrainingCardWidget
            {
                Cost = Text(badge, "Cost", "", 30, Guide.Gold, TextAlignmentOptions.Center),
            };
            Stretch((RectTransform)widget.Cost.transform);
            widget.Name = Text(
                cell,
                "Name",
                "",
                30,
                Guide.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            At(widget.Name, 80, 0, width - 80 - 76, 76);
            Guide.Shrink(widget.Name, 22);
            var element = Box(cell, "Element", width - 16 - 12 * Dot, 14, 12 * Dot, 12 * Dot);
            widget.Element = element.gameObject.AddComponent<Image>();
            widget.Element.raycastTarget = false;
            var none = Text(cell, "None", "無", 30, Guide.TextSub, TextAlignmentOptions.Center);
            At(none, width - 16 - 12 * Dot, 14, 12 * Dot, 12 * Dot);
            widget.None = none.gameObject;
            return widget;
        }

        // --- The level-up dialog, over the whole screen ----------------------------------

        private static void BuildDialog(RectTransform root, TrainingView view)
        {
            var dialog = Rect("LevelUpDialog", root);
            Stretch(dialog);
            view.Dialog = dialog.gameObject;

            // 背面の「もどる」や詳細を押せないよう、Safe Areaの外まで暗くする。
            var dim = Rect("Dim", dialog);
            Stretch(dim);
            dim.offsetMin = new Vector2(-400, -400);
            dim.offsetMax = new Vector2(400, 400);
            AddImage(dim, Dim, true);

            var layer = Layer(dialog, "Layout");
            var modal = Box(layer, "Modal", Modal.x, Modal.y, Modal.width, Modal.height);
            Guide.Frame(modal, Guide.FramePath, Color.white).raycastTarget = true;
            float width = Modal.width;

            var title = Text(
                modal,
                "Title",
                "レベルアップ",
                48,
                Guide.Gold,
                TextAlignmentOptions.MidlineLeft
            );
            At(title, ModalPad, 30, 500, 64);
            view.DialogName = Text(
                modal,
                "Name",
                "",
                44,
                Guide.TextMain,
                TextAlignmentOptions.MidlineRight
            );
            At(view.DialogName, width - ModalPad - 500, 30, 500, 64);

            var levels = Row(modal, "Levels", 0, 104, width, 124, 22);
            Fit(Text(levels, "FromTag", "Lv", 36, Guide.TextSub, TextAlignmentOptions.Baseline), 0);
            view.From = Text(levels, "From", "", 64, Before, TextAlignmentOptions.Baseline);
            Fit(view.From, 0);
            var arrowCell = Rect("Arrow", levels);
            Fit(arrowCell, 5 * 8);
            var arrow = Rect("Image", arrowCell);
            Place(arrow, new Vector2(0, 8), new Vector2(5, 9) * 8f);
            SpriteImage(arrow, Guide.ArrowPath, Guide.Gold);
            Fit(Text(levels, "ToTag", "Lv", 36, Guide.TextSub, TextAlignmentOptions.Baseline), 0);
            view.To = Text(levels, "To", "", 104, Guide.Gold, TextAlignmentOptions.Baseline);
            Fit(view.To, 0);

            var step = Row(modal, "Step", 0, 238, width, 92, 16);
            view.Less = Square(step, "Less", "－", HitSize);
            var count = Rect("Count", step);
            Fit(count, 200);
            AddImage(count, Inset, false);
            var countTag = Text(
                count,
                "Tag",
                "上げる数",
                22,
                Guide.TextSub,
                TextAlignmentOptions.Top
            );
            At(countTag, 0, 8, 200, 28);
            view.Count = Text(count, "Value", "", 44, Guide.Gold, TextAlignmentOptions.Top);
            At(view.Count, 0, 34, 200, 54);
            view.More = Square(step, "More", "＋", HitSize);
            view.Max = Square(step, "Max", "最大", 140);

            view.Diffs = Enumerable
                .Range(0, TrainingStats.Count)
                .Select(i => BuildDiff(modal, i, ModalPad, 372 + i * 60))
                .ToArray();

            float side = ModalPad + 640 + 48;
            float sideWidth = width - side - ModalPad;
            var learned = Box(modal, "Learned", side, 372, sideWidth, 124);
            AddImage(learned, Inset, false);
            var learnedTag = Text(
                learned,
                "Tag",
                "このレベルアップで覚えるスキル",
                24,
                Guide.Gold,
                TextAlignmentOptions.TopLeft
            );
            At(learnedTag, 24, 16, sideWidth - 48, 32);
            view.Learned = Text(
                learned,
                "Value",
                "",
                32,
                Guide.TextMain,
                TextAlignmentOptions.TopLeft
            );
            At(view.Learned, 24, 56, sideWidth - 48, 56);
            view.Learned.textWrappingMode = TextWrappingModes.Normal;
            Guide.Shrink(view.Learned, 24);

            var next = Box(modal, "NextUnlock", side, 512, sideWidth, 160);
            Guide.Frame(next, Guide.FramePath, new Color(0.62f, 0.62f, 0.68f, 1f)).raycastTarget =
                false;
            view.NextBox = next.gameObject;
            var nextTag = Text(
                next,
                "Tag",
                "次の解放",
                24,
                Guide.Gold,
                TextAlignmentOptions.TopLeft
            );
            At(nextTag, 24, 14, 300, 32);
            var nextIcon = Box(next, "Icon", 24, 50, Guide.IconSize, Guide.IconSize);
            view.NextIcon = nextIcon.gameObject.AddComponent<RawImage>();
            view.NextIcon.uvRect = IconCrop;
            view.NextIcon.color = new Color(0.55f, 0.55f, 0.6f, 1f);
            view.NextIcon.raycastTarget = false;
            float nextX = 24 + Guide.IconSize + 18;
            view.NextLevel = Text(
                next,
                "Level",
                "",
                24,
                Guide.TextSub,
                TextAlignmentOptions.TopLeft
            );
            At(view.NextLevel, nextX, 48, sideWidth - nextX - 20, 30);
            view.NextName = Text(
                next,
                "Name",
                "",
                36,
                Guide.TextMain,
                TextAlignmentOptions.TopLeft
            );
            At(view.NextName, nextX, 78, sideWidth - nextX - 20, 44);
            Guide.Shrink(view.NextName, 24);
            view.NextRemain = Text(
                next,
                "Remain",
                "",
                24,
                Guide.TextSub,
                TextAlignmentOptions.TopLeft
            );
            At(view.NextRemain, nextX, 122, sideWidth - nextX - 20, 30);

            float footY = Modal.height - ModalPad - 110;
            var runeIcon = Icon(modal, "RuneIcon", IconRunePath);
            At(runeIcon, ModalPad, footY - 22, Guide.IconSize, Guide.IconSize);
            view.Cost = Text(
                modal,
                "Cost",
                "",
                56,
                Guide.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            At(view.Cost, ModalPad + Guide.IconSize + 12, footY - 22, 420, Guide.IconSize);
            view.Balance = Text(
                modal,
                "Balance",
                "",
                26,
                Guide.TextSub,
                TextAlignmentOptions.TopLeft
            );
            At(view.Balance, ModalPad, footY + 78, 600, 36);

            var confirm = Box(modal, "Confirm", width - ModalPad - 380, footY, 380, 110);
            view.Confirm = Guide.AddButton(
                confirm,
                Guide.Frame(confirm, Guide.FrameSelectedPath, Color.white)
            );
            var confirmLabel = Text(
                confirm,
                "Label",
                "レベルアップ",
                46,
                Guide.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)confirmLabel.transform);
            var cancel = Box(modal, "Cancel", width - ModalPad - 380 - 24 - 240, footY, 240, 110);
            view.Cancel = Guide.AddButton(
                cancel,
                Guide.Frame(cancel, Guide.FramePath, Color.white)
            );
            var cancelLabel = Text(
                cancel,
                "Label",
                "やめる",
                40,
                Guide.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)cancelLabel.transform);

            dialog.gameObject.SetActive(false);
        }

        private static TrainingDiffWidget BuildDiff(
            RectTransform modal,
            int index,
            float x,
            float y
        )
        {
            var row = Box(modal, "Diff" + index, x, y, 640, 60);
            var line = Box(row, "Line", 0, 58, 640, 2);
            AddImage(line, Line, false);
            var label = Text(
                row,
                "Label",
                TrainingStats.Labels[index],
                30,
                Guide.TextSub,
                TextAlignmentOptions.BottomLeft
            );
            At(label, 0, 0, 170, 54);
            var widget = new TrainingDiffWidget
            {
                Before = Text(row, "Before", "", 36, Before, TextAlignmentOptions.BottomRight),
                After = Text(
                    row,
                    "After",
                    "",
                    42,
                    Guide.TextMain,
                    TextAlignmentOptions.BottomRight
                ),
                Gain = Text(row, "Gain", "", 34, Green, TextAlignmentOptions.BottomRight),
            };
            At(widget.Before, 170, 0, 110, 54);
            Arrow(row, "Arrow", 300, 18, 3);
            At(widget.After, 330, 0, 110, 54);
            At(widget.Gain, 450, 0, 190, 54);
            return widget;
        }

        private static Button Square(RectTransform row, string name, string label, float width)
        {
            var rect = Rect(name, row);
            Fit(rect, width);
            var button = Guide.AddButton(rect, Guide.Frame(rect, Guide.FramePath, Color.white));
            var text = Text(
                rect,
                "Label",
                label,
                label.Length > 1 ? 34 : 48,
                Guide.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)text.transform);
            return button;
        }

        // --- Helpers -------------------------------------------------------------------

        // 左上からの設計座標で置く。
        private static RectTransform Box(
            RectTransform parent,
            string name,
            float x,
            float y,
            float width,
            float height
        )
        {
            var rect = Rect(name, parent);
            Corner(rect, new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height));
            return rect;
        }

        private static void At(Component item, float x, float y, float width, float height) =>
            Corner(
                (RectTransform)item.transform,
                new Vector2(0, 1),
                new Vector2(x, -y),
                new Vector2(width, height)
            );

        private static TMP_Text Text(
            RectTransform parent,
            string name,
            string text,
            float size,
            Color color,
            TextAlignmentOptions alignment
        ) => Label(parent, name, text, size, color, alignment);

        // 子を中央に横へ並べる行。子の幅は各自の推奨の幅（文字なら文の長さ）。
        private static RectTransform Row(
            RectTransform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            float spacing
        )
        {
            var row = Box(parent, name, x, y, width, height);
            var group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.childAlignment = TextAnchor.MiddleCenter;
            group.spacing = spacing;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            return row;
        }

        // 行の子の幅を決める。0なら文字の推奨の幅に任せる。
        private static void Fit(Component item, float width)
        {
            var element = item.gameObject.AddComponent<LayoutElement>();
            if (width > 0)
                element.minWidth = element.preferredWidth = width;
        }

        private static Image Icon(RectTransform parent, string name, string path)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            // 共有の画像なので取り込み設定は変えず、読み込むだけにする。
            image.sprite = ArtAssets.LoadSprite(path);
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static void Arrow(RectTransform parent, string name, float x, float y, float scale)
        {
            var arrow = Box(parent, name, x, y, 5 * scale, 9 * scale);
            SpriteImage(arrow, Guide.ArrowPath, Guide.Gold);
        }

        // 未解放のスキルのアイコンに重ねる、9×10ドットの錠前（4倍で表示する）。
        private static void WriteLock()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LockPath));
            ArtAssets.WritePattern(
                LockPath,
                new[]
                {
                    "..ooooo..",
                    ".ohhhhho.",
                    ".oh...ho.",
                    ".oh...ho.",
                    "ooooooooo",
                    "olllllllo",
                    "ogggkgggo",
                    "ogggkgggo",
                    "ossssssso",
                    ".ooooooo.",
                },
                c =>
                    c switch
                    {
                        'o' => new Color32(10, 13, 28, 255),
                        'h' => new Color32(195, 201, 220, 255),
                        'l' => new Color32(228, 232, 244, 255),
                        'g' => new Color32(140, 150, 178, 255),
                        's' => new Color32(92, 101, 132, 255),
                        'k' => new Color32(10, 13, 28, 255),
                        _ => Color.clear,
                    }
            );
        }
    }
}
