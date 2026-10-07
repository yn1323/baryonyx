using System;
using System.IO;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Editor;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Equipment;
using Baryonyx.Equipment.Editor;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.UI;
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
    /// <summary>
    /// The training mock data and the formation's adventurer page (the training, extended with
    /// the deck's four cards and the equipment). The formation's generator calls
    /// <see cref="BuildTrainingPanel"/> for its training item, so the page and the level-up
    /// dialog are baked into the formation prefab and read in the editor without Play Mode.
    /// Positions are design pixels from the top left of a 1920x1080 layer that shrinks on
    /// screens narrower than 16:9 (<see cref="GuidePanelLayout"/>). The growth, skills and
    /// costs are mock data until levels have data (doc/features/progression.md).
    /// </summary>
    public static class TrainingAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Training";
        public const string DataPath = Folder + "/Data/TrainingMockData.asset";
        public const string LockPath = Folder + "/UI/Art/TrainingLock.png";
        public const string IconRunePath =
            "Assets/Baryonyx/Shared/Art/GameResources/IconRune.aseprite";

        private const float Dot = Guide.DotScale;
        private static readonly Vector2 Design = Layout.Design;

        // 左のイラストの枠、その右下に立たせるドット絵の足元、右の枠（設計座標、左上から）。
        // 左上の「もどる」（下端が192）と重ならないよう、どちらも200から下に置く。
        private static readonly Rect Portrait = new(60, 200, 576, 850);
        private static readonly Vector2 FigureFeet = new(520, 1028);
        private static readonly Rect Panel = new(670, 200, 1210, 850);
        private const float PanelPad = 34f;

        // 右の枠の中（枠の左上から）：ステータス（2列4行）とその右のパッシブの箱、デッキの4枚、装備の5枠。
        private const float StatsTop = 122f;
        private const float StatsWidth = 520f;
        private const float StatRow = 42f;
        private static readonly Rect Passives = new(586, 116, 590, 178);
        private const float PassiveRow = 64f;
        private const float DeckTop = 334f;
        private const float CardGap = 16f;
        private const float PairGap = 60f;
        private const float GearTop = 666f;
        private const float GearGap = 13f;
        private const float GearHeight = 168f;

        // デッキのカードは戦闘と同じ70×98ドットを3倍で出す。
        private static readonly Vector2 CardDots = new(70, 98);
        private const float CardDot = 3f;

        // －＋の押せる範囲。
        private const float HitSize = Layout.HitSize;

        // ステータスの8項目を置く枠（列, 行）。4列2行で、物攻・属攻の下に物防・属防を置く。
        private static readonly Vector2Int[] StatCells =
        {
            new(0, 0), // HP
            new(1, 0), // 物攻
            new(1, 1), // 物防
            new(2, 0), // 属攻
            new(2, 1), // 属防
            new(0, 1), // 速度
            new(3, 0), // 会心
            new(3, 1), // 幸運
        };
        private const float StatGap = 24f;

        // 2列のときの枠（列, 行）。HP・物攻、属攻・速度、会心・物防、属防・幸運の順に並べる。
        private static readonly Vector2Int[] StatCellsNarrow =
        {
            new(0, 0), // HP
            new(1, 0), // 物攻
            new(1, 2), // 物防
            new(0, 1), // 属攻
            new(0, 3), // 属防
            new(1, 1), // 速度
            new(0, 2), // 会心
            new(1, 3), // 幸運
        };

        // 重ねて開くレベルアップ。
        private static readonly Rect Modal = new(280, 150, 1360, 890);
        private const float ModalPad = 48f;

        // レベルアップの、ステータス8行の上端と1行の高さ。
        private const float DiffTop = 350f;
        private const float DiffHeight = 42f;

        // カードの挿絵（64×58）の中央24×24ドット。スキルの仮のアイコンに使う。
        private static readonly Rect IconCrop = new(20f / 64f, 17f / 58f, 24f / 64f, 24f / 58f);

        private static readonly Color Green = new(0.49f, 0.88f, 0.42f);
        private static readonly Color Before = new(0.72f, 0.75f, 0.83f);
        private static readonly Color Line = new(0.27f, 0.32f, 0.48f, 0.55f);
        private static readonly Color Inset = new(0.03f, 0.05f, 0.12f, 0.75f);
        private static readonly Color Dim = new(0.01f, 0.015f, 0.04f, 0.74f);
        private static readonly Color FigureSpot = new(0.01f, 0.015f, 0.04f, 0.7f);
        private static readonly Color CardBand = new(0.02f, 0.03f, 0.07f, 0.68f);
        private static readonly Color CardOwner = new(1f, 0.93f, 0.76f);
        private static readonly Color CardVeil = new(0.01f, 0.015f, 0.04f, 0.72f);

        [MenuItem("Baryonyx/Training/Create Mock Data")]
        public static TrainingMockData CreateData()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            AssetDatabase.Refresh();
            var data = AssetFolders.LoadOrCreate<TrainingMockData>(DataPath);
            data.CostPerLevel = 100;
            data.MaxLevel = 30;
            data.MockRunes = 8450;
            // ステータスは Lv 100 の値（doc/features/progression.md の自キャラのLv100の
            // ステータス）。絵のないキャラは、絵を借りたキャラと同じスキルにする。
            var mage = Mage();
            var archer = Archer();
            var knight = Knight();
            var healer = Healer();
            data.Characters = new[]
            {
                Character("toma", new(2300, 700, 850, 2450, 1700, 560, 300, 500), mage),
                Character("luka", new(2850, 1800, 950, 2050, 1100, 670, 900, 600), archer),
                Character("aria", new(3900, 1950, 1600, 950, 1200, 490, 500, 300), knight),
                Character("mina", new(2500, 800, 1150, 1600, 1800, 540, 200, 900), healer),
                Character("anselm", new(3600, 1700, 1500, 1400, 1300, 430, 300, 500), knight),
                Character("greta", new(2400, 1700, 800, 1500, 900, 700, 1000, 800), archer),
                Character("lutz", new(3200, 1300, 1400, 1900, 1300, 450, 300, 400), mage),
                Character("rita", new(3000, 2100, 1000, 600, 800, 650, 800, 500), healer),
                Character("ritsu", new(2100, 500, 700, 2400, 1500, 600, 400, 600), archer),
            };
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            return data;
        }

        private static TrainingCharacter Character(
            string id,
            CharacterStats level100,
            TrainingCharacter skills
        ) =>
            new()
            {
                Id = id,
                Level100 = level100,
                Passives = skills.Passives,
                Uniques = skills.Uniques,
            };

        private static TrainingCharacter Mage() =>
            Skills(
                Skill("魔力の泉", "戦闘の始めにエネルギー +1", 1, 0, "CardManaPrayer"),
                Skill("炎の心得", "炎のカードの威力 +10%", 15, 0, "CardFlameEnchant"),
                Skill("マナバースト", "敵全体に 属攻の80% のダメージ", 5, 3, "CardRevelation"),
                Skill("星降り", "ランダムな敵に5回ダメージ", 20, 5, "CardMeteor")
            );

        private static TrainingCharacter Archer() =>
            Skills(
                Skill("狩人の目", "敵の弱点を見つけやすくなる", 1, 0, "CardInsightArrow"),
                Skill("疾風", "速度 +10%", 15, 0, "CardGale"),
                Skill("雷撃の矢", "敵1体に雷のダメージ。まひさせる", 5, 3, "CardThunderSpear"),
                Skill("矢の雨", "敵全体に3回ダメージ", 20, 5, "CardArrowRain")
            );

        private static TrainingCharacter Knight() =>
            Skills(
                Skill("守護の誓い", "味方が受けるダメージ -5%", 1, 0, "CardGuardianOath"),
                Skill("不屈", "倒れるダメージを1度だけ耐える", 15, 0, "CardDivineShield"),
                Skill("盾の構え", "次のターンまでブロック +80", 5, 2, "CardProtect"),
                Skill("一閃", "敵1体に 物攻の200% のダメージ", 20, 5, "CardIai")
            );

        private static TrainingCharacter Healer() =>
            Skills(
                Skill("祈り", "ターンの終わりに味方のHPを少し回復", 1, 0, "CardRegen"),
                Skill("浄化の光", "状態異常に1回かからない", 15, 0, "CardPurify"),
                Skill("癒しの風", "味方全体のHPを回復", 5, 3, "CardHeal"),
                Skill("蘇生", "倒れた味方1人を復活させる", 20, 6, "CardResurrection")
            );

        private static TrainingCharacter Skills(
            TrainingSkill passive1,
            TrainingSkill passive2,
            TrainingSkill unique1,
            TrainingSkill unique2
        ) =>
            new() { Passives = new[] { passive1, passive2 }, Uniques = new[] { unique1, unique2 } };

        // 固有スキル・パッシブの絵はまだないため、スキルの挿絵を仮のアイコンにする。
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
        /// Builds the adventurer's page over the guide screen's safe area: the illustration with
        /// the battle sprite in its corner on the left; on the right one frame of the name and
        /// level-up, the stats beside the passives, the four deck cards (two unique skills, two
        /// custom skills) and the five equipment slots; and the level-up dialog (hidden) over
        /// everything. A flick over the page changes the person. Called while the guide
        /// screen's prefab is being built, so the shared frames and font are ready.
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
            view.WeaponIcon = Guide.Icon(EquipmentAssets.IconWeaponPath);
            view.ArmorIcon = Guide.Icon(EquipmentAssets.IconArmorPath);

            var layer = Layout.Layer(root, "Layout");
            // ページの上の左右のフリックで人を替える。枠とボタンの上から動かしても届くよう、両方を載せる層で受ける。
            view.Swipe = layer.gameObject.AddComponent<TrainingSwipe>();
            BuildPortrait(layer, view, party);
            BuildDetail(layer, view);
            BuildDialog(root, view);

            // 停止中のPrefabでも1人目が読めるよう、仮の装備で生成時に一度描く。
            TrainingView.Build(view, party, data, EquipmentLocalSource.Starter()).Dispose();
            return root.gameObject;
        }

        // --- Left: the illustration and the battle sprite in its corner ---------------------

        private static void BuildPortrait(
            RectTransform layer,
            TrainingView view,
            PartyMockData party
        )
        {
            var card = Layout.FrameBox(
                layer,
                "Portrait",
                Portrait.x,
                Portrait.y,
                Portrait.width,
                Portrait.height
            );
            var picture = Box(
                card,
                "Illustration",
                12,
                12,
                Portrait.width - 24,
                Portrait.height - 24
            );
            view.Illustration = picture.gameObject.AddComponent<RawImage>();
            view.Illustration.raycastTarget = false;

            // 右下の足元を暗くして、イラストの上でもドット絵が見えるようにする。
            var feet = new Vector2(FigureFeet.x - Design.x / 2f, Design.y / 2f - FigureFeet.y);
            var spot = Rect("FigureSpot", layer);
            Place(spot, feet + new Vector2(0, 90), new Vector2(340, 300));
            SpriteImage(spot, Guide.SoftSpotPath, FigureSpot);
            Picture(
                layer,
                "Shadow",
                ArtAssets.LoadSprite(UiArt.ShadowPath),
                feet + new Vector2(0, 2),
                new Vector2(170, 28),
                0.6f
            );
            view.Figure = PixelActor(layer, "Figure", party.Members[0].Art, feet, Dot);
        }

        // --- Right: the person, the deck's four cards and the equipment in one frame -------

        private static void BuildDetail(RectTransform layer, TrainingView view)
        {
            var panel = Layout.FrameBox(
                layer,
                "Detail",
                Panel.x,
                Panel.y,
                Panel.width,
                Panel.height
            );
            float width = Panel.width - PanelPad * 2;

            var header = Layout.Row(panel, "Header", PanelPad, 18, 760, 92, 14);
            view.Name = Text(
                header,
                "Name",
                "",
                60,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Fit(view.Name, 0);
            Fit(
                Text(header, "LevelTag", "Lv", 30, UiPalette.TextSub, TextAlignmentOptions.Midline),
                0
            );
            view.Level = Text(
                header,
                "Level",
                "",
                60,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Fit(view.Level, 0);
            view.Elements = Enumerable
                .Range(0, 3)
                .Select(i => Layout.RowIcon(header, "Element" + i, 12f * Dot))
                .ToArray();
            BuildLevelUp(panel, view);

            // キャラ自身：ステータス（2列4行）と、その右にパッシブ2つ。
            view.Stats = BuildStats(panel, PanelPad, StatsTop, StatsWidth, 2, StatRow);
            view.Passives = BuildPassives(panel);

            // デッキに入る4枚：固有スキル2枚と、カスタムスキル2枚。戦闘のカードと同じ組み方。
            var cardSize = CardDots * CardDot;
            float deckWidth = cardSize.x * 4 + CardGap * 2 + PairGap;
            float deckLeft = (Panel.width - deckWidth) / 2f;
            float[] cardX =
            {
                deckLeft,
                deckLeft + cardSize.x + CardGap,
                deckLeft + cardSize.x * 2 + CardGap + PairGap,
                deckLeft + cardSize.x * 3 + CardGap * 2 + PairGap,
            };
            Layout.Text(
                panel,
                "UniquesTitle",
                "固有スキル",
                24,
                UiPalette.Gold,
                TextAlignmentOptions.BottomLeft,
                cardX[0],
                DeckTop - 36,
                300,
                32
            );
            var customTitle = Row(panel, "CustomTitle", cardX[2], DeckTop - 36, 420, 32, 14);
            customTitle.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
            Fit(
                Text(
                    customTitle,
                    "Title",
                    "カスタムスキル",
                    24,
                    UiPalette.Gold,
                    TextAlignmentOptions.BottomLeft
                ),
                0
            );
            Fit(
                Text(
                    customTitle,
                    "Note",
                    "スキルかアイテム",
                    18,
                    UiPalette.TextSub,
                    TextAlignmentOptions.BottomLeft
                ),
                0
            );
            Layout.Line(
                panel,
                "DeckLine",
                cardX[1] + cardSize.x + PairGap / 2f - 1,
                DeckTop + 8,
                2,
                cardSize.y - 16
            );
            view.CardFrames = Enum.GetValues(typeof(CardElement))
                .Cast<CardElement>()
                .Select(element => (Texture)CardArt($"CardFrame{element}"))
                .ToArray();
            view.Uniques = Enumerable
                .Range(0, 2)
                .Select(i => BuildCard(panel, "Unique" + i, cardX[i], DeckTop, button: false))
                .ToArray();
            view.CardSlots = Enumerable
                .Range(0, TrainingPresenter.CustomSlots)
                .Select(i => BuildCard(panel, "Custom" + i, cardX[2 + i], DeckTop, button: true))
                .ToArray();

            // 武器・防具・アクセサリー3つを同じ大きさの枠で並べる。
            Layout.Text(
                panel,
                "GearTitle",
                "装備",
                24,
                UiPalette.Gold,
                TextAlignmentOptions.BottomLeft,
                PanelPad,
                GearTop - 34,
                300,
                32
            );
            float gear =
                (width - GearGap * (TrainingPresenter.GearSlots - 1)) / TrainingPresenter.GearSlots;
            view.Gear = Enumerable
                .Range(0, TrainingPresenter.GearSlots)
                .Select(i => BuildGear(panel, i, PanelPad + i * (gear + GearGap), GearTop, gear))
                .ToArray();
        }

        // 右上の「レベルアップ」（金の枠）。1レベル上げるのに要るルーンを添え、押すと重ねた画面を開く。
        private static void BuildLevelUp(RectTransform panel, TrainingView view)
        {
            var levelUp = Box(panel, "LevelUp", Panel.width - PanelPad - 360, 18, 360, 96);
            view.LevelUp = Guide.AddButton(
                levelUp,
                Guide.Frame(levelUp, Guide.FrameSelectedPath, Color.white)
            );
            view.LevelUpLabel = Text(
                levelUp,
                "Label",
                "レベルアップ",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            At(view.LevelUpLabel, 26, 0, 200, 96);
            Guide.Shrink(view.LevelUpLabel, 22);
            var cost = Box(levelUp, "Cost", 216, 0, 130, 96);
            view.LevelUpCost = cost.gameObject;
            var icon = Icon(cost, "Icon", IconRunePath);
            At(icon, 0, 26, 44, 44);
            view.LevelUpCostLabel = Text(
                cost,
                "Value",
                "",
                28,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineRight
            );
            At(view.LevelUpCostLabel, 44, 0, 80, 96);
            Guide.Shrink(view.LevelUpCostLabel, 20);
        }

        /// <summary>
        /// The eight stats as label and value cells with a line under each, in
        /// <see cref="CharacterStats"/> order: four columns (HP・物攻・属攻・会心 over 速度・物防・属防・幸運)
        /// or two (HP・物攻, 属攻・速度, 会心・物防, 属防・幸運). The formation's other screens
        /// build their stats with it too.
        /// </summary>
        public static TMP_Text[] BuildStats(
            RectTransform parent,
            float x,
            float y,
            float width,
            int columns,
            float rowHeight = 46f
        )
        {
            var cells = columns >= 4 ? StatCells : StatCellsNarrow;
            int count = columns >= 4 ? 4 : 2;
            float cell = (width - StatGap * (count - 1)) / count;
            var stats = Box(parent, "Stats", x, y, width, rowHeight * (8 / count));
            return Enumerable
                .Range(0, CharacterStats.Count)
                .Select(i =>
                {
                    var row = Box(
                        stats,
                        "Stat" + i,
                        cells[i].x * (cell + StatGap),
                        cells[i].y * rowHeight,
                        cell,
                        rowHeight
                    );
                    Layout.Line(row, "Line", 0, rowHeight - 2, cell, 2);
                    var label = Text(
                        row,
                        "Label",
                        CharacterStats.Labels[i],
                        24,
                        UiPalette.TextSub,
                        TextAlignmentOptions.BottomLeft
                    );
                    At(label, 0, 0, 80, rowHeight - 4);
                    var value = Text(
                        row,
                        "Value",
                        "",
                        32,
                        UiPalette.TextMain,
                        TextAlignmentOptions.BottomRight
                    );
                    At(value, 70, 0, cell - 70, rowHeight - 4);
                    return value;
                })
                .ToArray();
        }

        // パッシブ2つを、見出し付きの暗い箱に1行ずつ（アイコン、名前、効果）。未解放は名前の行の右に解放するLv。
        private static TrainingSkillWidget[] BuildPassives(RectTransform panel)
        {
            var box = Box(
                panel,
                "Passives",
                Passives.x,
                Passives.y,
                Passives.width,
                Passives.height
            );
            AddImage(box, Inset, false);
            var title = Text(
                box,
                "Title",
                "パッシブ",
                22,
                UiPalette.Gold,
                TextAlignmentOptions.TopLeft
            );
            At(title, 18, 10, 200, 28);
            Layout.Line(box, "Line", 18, 42 + PassiveRow, Passives.width - 36, 2);
            return Enumerable
                .Range(0, 2)
                .Select(i =>
                {
                    var row = Box(
                        box,
                        "Passive" + i,
                        0,
                        44 + i * (PassiveRow + 4),
                        Passives.width,
                        PassiveRow
                    );
                    var icon = Box(row, "Icon", 18, (PassiveRow - 48) / 2f, 48, 48);
                    var widget = new TrainingSkillWidget
                    {
                        Icon = icon.gameObject.AddComponent<RawImage>(),
                    };
                    widget.Icon.uvRect = IconCrop;
                    widget.Icon.raycastTarget = false;
                    var locked = Rect("Lock", icon);
                    Stretch(locked);
                    var lockImage = Rect("Image", locked);
                    Place(lockImage, Vector2.zero, new Vector2(9, 10) * 3f);
                    SpriteImage(lockImage, LockPath, Color.white);
                    widget.Lock = locked.gameObject;

                    const float textX = 82;
                    widget.Name = Text(
                        row,
                        "Name",
                        "",
                        24,
                        UiPalette.TextMain,
                        TextAlignmentOptions.TopLeft
                    );
                    At(widget.Name, textX, 4, 300, 30);
                    Guide.Shrink(widget.Name, 18);
                    widget.When = Text(
                        row,
                        "When",
                        "",
                        18,
                        UiPalette.Gold,
                        TextAlignmentOptions.TopRight
                    );
                    At(widget.When, Passives.width - 18 - 200, 8, 200, 24);
                    widget.Description = Text(
                        row,
                        "Description",
                        "",
                        20,
                        UiPalette.TextSub,
                        TextAlignmentOptions.TopLeft
                    );
                    At(widget.Description, textX, 34, Passives.width - textX - 18, 28);
                    Guide.Shrink(widget.Description, 15);
                    return widget;
                })
                .ToArray();
        }

        /// <summary>
        /// One deck card, laid out as the battle's card (70x98 dots at 3x, rows in dots as in
        /// BattleInspectAssets): the frame, the art over its top, the cost and element on the top
        /// corners, the type, name and kind on a dark band, the effect below, and a veil with the
        /// level for a unique skill not unlocked yet. A custom skill's card is a button.
        /// </summary>
        private static TrainingCardWidget BuildCard(
            RectTransform panel,
            string name,
            float x,
            float y,
            bool button
        )
        {
            var card = Box(panel, name, x, y, CardDots.x * CardDot, CardDots.y * CardDot);
            var widget = new TrainingCardWidget
            {
                Frame = Dots(card, "Frame", 0, 0, CardDots, null),
            };
            widget.Frame.raycastTarget = button;
            if (button)
                widget.Button = Guide.AddButton(card, widget.Frame);
            widget.Art = Dots(card, "Art", 3, 3, new Vector2(64, 58), null);

            var band = Box(card, "Band", 3 * CardDot, 39 * CardDot, 64 * CardDot, 26 * CardDot);
            AddImage(band, CardBand, false);
            widget.Band = band.gameObject;
            widget.Type = Text(card, "Type", "", 15, CardOwner, TextAlignmentOptions.MidlineLeft);
            At(widget.Type, 6 * CardDot, 42.4f * CardDot, 58 * CardDot, 6.4f * CardDot);
            widget.Name = Text(
                card,
                "Name",
                "",
                22,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            At(widget.Name, 6 * CardDot, 48.6f * CardDot, 58 * CardDot, 8.4f * CardDot);
            Guide.Shrink(widget.Name, 15);
            widget.Kind = Text(
                card,
                "Kind",
                "",
                15,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            At(widget.Kind, 7 * CardDot, 56.8f * CardDot, 56 * CardDot, 6 * CardDot);
            widget.Kind.richText = true;
            widget.Description = Label(
                card,
                "Description",
                "",
                15,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft,
                shadow: false
            );
            At(widget.Description, 7.5f * CardDot, 66.6f * CardDot, 55 * CardDot, 26 * CardDot);
            widget.Description.richText = true;
            widget.Description.textWrappingMode = TextWrappingModes.Normal;

            var costFrame = CardArt("CardCostFrame");
            var costDigits = CardArt("CardCostDigits");
            var cost = Dots(
                card,
                "Cost",
                5,
                5,
                new Vector2(costFrame.width, costFrame.height),
                costFrame
            );
            widget.Cost = cost.gameObject;
            var digit = Rect("Digit", cost.rectTransform);
            Place(
                digit,
                Vector2.zero,
                new Vector2(costDigits.width / 10f, costDigits.height) * CardDot
            );
            widget.CostDigit = digit.gameObject.AddComponent<RawImage>();
            widget.CostDigit.texture = costDigits;
            widget.CostDigit.raycastTarget = false;
            var element = Box(
                card,
                "Element",
                (70 - 5 - 12) * CardDot,
                5 * CardDot,
                12 * CardDot,
                12 * CardDot
            );
            widget.Element = element.gameObject.AddComponent<Image>();
            widget.Element.raycastTarget = false;

            // 未解放の固有スキル：カード全体を暗くして、錠前と解放するLvを重ねる。
            var locked = Rect("Lock", card);
            Stretch(locked);
            AddImage(locked, CardVeil, false);
            var lockImage = Box(
                locked,
                "Image",
                (CardDots.x * CardDot - 9 * 4) / 2f,
                54,
                9 * 4,
                10 * 4
            );
            SpriteImage(lockImage, LockPath, Color.white);
            widget.When = Text(locked, "When", "", 22, UiPalette.Gold, TextAlignmentOptions.Center);
            At(widget.When, 0, 100, CardDots.x * CardDot, 30);
            widget.Lock = locked.gameObject;
            locked.gameObject.SetActive(false);
            return widget;
        }

        // ドット絵の画像を、左上からのドットの位置と大きさで3倍に置く。
        private static RawImage Dots(
            RectTransform parent,
            string name,
            float x,
            float y,
            Vector2 size,
            Texture texture
        )
        {
            var rect = Box(
                parent,
                name,
                x * CardDot,
                y * CardDot,
                size.x * CardDot,
                size.y * CardDot
            );
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        // 戦闘のカードの画像（枠・コスト）。スキルの挿絵と同じく、戦闘画面のモックの画像を借りる。
        private static Texture2D CardArt(string name) =>
            ArtAssets.LoadTexture($"{BattleInspectAssets.ArtFolder}/{name}.aseprite");

        // 装備の枠1つ。上にアイコン・枠の名前・星、下に名前と効果。武器・防具は押すと付け替えの画面を開く。
        // アクセサリーは決まりがないため、枠の名前と錠前・「準備中」だけを出して押せなくする。
        private static TrainingGearWidget BuildGear(
            RectTransform panel,
            int index,
            float x,
            float y,
            float width
        )
        {
            var (button, _) = Layout.Choice(panel, "Gear" + index, x, y, width, GearHeight);
            var cell = (RectTransform)button.transform;
            var widget = new TrainingGearWidget { Button = button };
            var icon = Box(cell, "Icon", 14, 14, 48, 48);
            widget.Icon = icon.gameObject.AddComponent<Image>();
            widget.Icon.raycastTarget = false;
            widget.Slot = Text(
                cell,
                "Slot",
                index < TrainingPresenter.OpenGearSlots
                    ? TrainingPresenter.GearLabels[index]
                    : $"{TrainingPresenter.GearLabels[2]} {index - TrainingPresenter.OpenGearSlots + 1}",
                18,
                UiPalette.Gold,
                TextAlignmentOptions.TopLeft
            );
            // アクセサリーはアイコンがまだないため、枠の名前を中央に置く。
            if (index < TrainingPresenter.OpenGearSlots)
                At(widget.Slot, 72, 14, width - 80, 24);
            else
            {
                widget.Slot.alignment = TextAlignmentOptions.Top;
                At(widget.Slot, 8, 18, width - 16, 24);
            }
            Guide.Shrink(widget.Slot, 14);
            widget.Stars = Text(
                cell,
                "Stars",
                "",
                20,
                UiPalette.Gold,
                TextAlignmentOptions.TopLeft
            );
            At(widget.Stars, 72, 40, width - 80, 24);
            widget.Name = Text(
                cell,
                "Name",
                "",
                22,
                UiPalette.TextMain,
                TextAlignmentOptions.TopLeft
            );
            At(widget.Name, 14, 74, width - 28, 30);
            Guide.Shrink(widget.Name, 16);
            widget.Detail = Text(
                cell,
                "Detail",
                "",
                18,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft
            );
            At(widget.Detail, 14, 106, width - 28, GearHeight - 118);
            widget.Detail.textWrappingMode = TextWrappingModes.Normal;

            var locked = Rect("Lock", cell);
            Stretch(locked);
            var lockImage = Box(locked, "Image", (width - 9 * 4) / 2f, 62, 9 * 4, 10 * 4);
            SpriteImage(lockImage, LockPath, Color.white);
            var soon = Text(
                locked,
                "Soon",
                "準備中",
                22,
                UiPalette.Gold,
                TextAlignmentOptions.Center
            );
            At(soon, 8, 112, width - 16, 30);
            widget.Lock = locked.gameObject;
            locked.gameObject.SetActive(false);
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

            var layer = Layout.Layer(dialog, "Layout");
            var modal = Box(layer, "Modal", Modal.x, Modal.y, Modal.width, Modal.height);
            Guide.Frame(modal, Guide.FramePath, Color.white).raycastTarget = true;
            float width = Modal.width;

            var title = Text(
                modal,
                "Title",
                "レベルアップ",
                48,
                UiPalette.Gold,
                TextAlignmentOptions.MidlineLeft
            );
            At(title, ModalPad, 30, 500, 64);
            view.DialogName = Text(
                modal,
                "Name",
                "",
                44,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineRight
            );
            At(view.DialogName, width - ModalPad - 500, 30, 500, 64);

            var levels = Row(modal, "Levels", 0, 104, width, 124, 22);
            Fit(
                Text(levels, "FromTag", "Lv", 36, UiPalette.TextSub, TextAlignmentOptions.Baseline),
                0
            );
            view.From = Text(levels, "From", "", 64, Before, TextAlignmentOptions.Baseline);
            Fit(view.From, 0);
            var arrowCell = Rect("Arrow", levels);
            Fit(arrowCell, 5 * 8);
            var arrow = Rect("Image", arrowCell);
            Place(arrow, new Vector2(0, 8), new Vector2(5, 9) * 8f);
            SpriteImage(arrow, Guide.ArrowPath, UiPalette.Gold);
            Fit(
                Text(levels, "ToTag", "Lv", 36, UiPalette.TextSub, TextAlignmentOptions.Baseline),
                0
            );
            view.To = Text(levels, "To", "", 104, UiPalette.Gold, TextAlignmentOptions.Baseline);
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
                UiPalette.TextSub,
                TextAlignmentOptions.Top
            );
            At(countTag, 0, 8, 200, 28);
            view.Count = Text(count, "Value", "", 44, UiPalette.Gold, TextAlignmentOptions.Top);
            At(view.Count, 0, 34, 200, 54);
            view.More = Square(step, "More", "＋", HitSize);
            view.Max = Square(step, "Max", "最大", 140);

            view.Diffs = Enumerable
                .Range(0, CharacterStats.Count)
                .Select(i => BuildDiff(modal, i, ModalPad, DiffTop + i * DiffHeight))
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
                UiPalette.Gold,
                TextAlignmentOptions.TopLeft
            );
            At(learnedTag, 24, 16, sideWidth - 48, 32);
            view.Learned = Text(
                learned,
                "Value",
                "",
                32,
                UiPalette.TextMain,
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
                UiPalette.Gold,
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
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft
            );
            At(view.NextLevel, nextX, 48, sideWidth - nextX - 20, 30);
            view.NextName = Text(
                next,
                "Name",
                "",
                36,
                UiPalette.TextMain,
                TextAlignmentOptions.TopLeft
            );
            At(view.NextName, nextX, 78, sideWidth - nextX - 20, 44);
            Guide.Shrink(view.NextName, 24);
            view.NextRemain = Text(
                next,
                "Remain",
                "",
                24,
                UiPalette.TextSub,
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
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            At(view.Cost, ModalPad + Guide.IconSize + 12, footY - 22, 420, Guide.IconSize);
            view.Balance = Text(
                modal,
                "Balance",
                "",
                26,
                UiPalette.TextSub,
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
                UiPalette.TextMain,
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
                UiPalette.TextMain,
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
            const float text = DiffHeight - 4;
            var row = Box(modal, "Diff" + index, x, y, 640, DiffHeight);
            var line = Box(row, "Line", 0, DiffHeight - 2, 640, 2);
            AddImage(line, Line, false);
            var label = Text(
                row,
                "Label",
                CharacterStats.Labels[index],
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.BottomLeft
            );
            At(label, 0, 0, 170, text);
            var widget = new TrainingDiffWidget
            {
                Before = Text(row, "Before", "", 30, Before, TextAlignmentOptions.BottomRight),
                After = Text(
                    row,
                    "After",
                    "",
                    34,
                    UiPalette.TextMain,
                    TextAlignmentOptions.BottomRight
                ),
                Gain = Text(row, "Gain", "", 28, Green, TextAlignmentOptions.BottomRight),
            };
            At(widget.Before, 170, 0, 110, text);
            Arrow(row, "Arrow", 300, 9, 3);
            At(widget.After, 330, 0, 110, text);
            At(widget.Gain, 450, 0, 190, text);
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
                UiPalette.TextMain,
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
            SpriteImage(arrow, Guide.ArrowPath, UiPalette.Gold);
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
