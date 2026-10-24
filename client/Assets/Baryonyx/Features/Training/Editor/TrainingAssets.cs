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
using Baryonyx.SkillEvolution;
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
    /// <summary>
    /// The training mock data and the formation's adventurer page (the training, extended with
    /// the deck's four cards and the equipment). The formation's generator calls
    /// <see cref="BuildTrainingPanel"/> for its training item, so the page and the level-up
    /// dialog are baked into the formation prefab and read in the editor without Play Mode.
    /// Positions are design pixels from the top left of a 1920x1080 layer that shrinks on
    /// screens narrower than 16:9 (<see cref="GuidePanelLayout"/>). The growth, skills and
    /// costs are mock data until levels have data (doc/features/progression.md).
    /// </summary>
    public static partial class TrainingAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Training";
        public const string DataPath = Folder + "/Data/TrainingMockData.asset";

        private const float Dot = Guide.DotScale;
        private static readonly Vector2 Design = Layout.Design;

        // 枠つきの表示（GuideFramedPanel）で、左の面いっぱいにイラスト、その右下にドット絵、
        // 金の2本線の右の透ける面に詳細を置く（設計座標、左上から）。
        private const float Split = 640f;
        private static readonly Rect Portrait = new(
            0,
            GuideFrameAssets.HeaderHeight,
            Split,
            1080 - GuideFrameAssets.HeaderHeight
        );
        private static readonly Vector2 FigureFeet = new(520, 1028);
        private static readonly Rect Panel = new(
            Split + 14,
            GuideFrameAssets.HeaderHeight + 6,
            1210,
            956
        );
        private const float PortraitFade = 140f;
        private const float PanelPad = 34f;

        // 右の面の中（左上から）：ステータス（2列4行）とその右のパッシブ、デッキの6枚、装備の5枠。
        // 画面の下端（設計座標で1040）まで使い、段の間を空けて詰まって見えないようにする。
        private const float StatsTop = 148f;
        private const float StatsWidth = 520f;
        private const float StatRow = 42f;
        private static readonly Rect Passives = new(586, 140, 590, 200);
        private const float PassiveRow = 76f;
        private const float DeckTop = 400f;
        private const float CardGap = 12f;
        private const float PairGap = 40f;
        private const float GearTop = 680f;
        private const float GearGap = 13f;
        private const float GearHeight = 168f;

        // デッキのカードは、戦闘のカードを効果の文の上で切った小さなカード（70×66ドット）を2.5倍で出す。
        // 6枚が面の幅（1142）いっぱいに並ぶ大きさにした（2026-10-24、ユーザーの「もうちょい大きく」）。
        // 効果はカードを押して開くスキルの付け替えの画面で読む。
        private const float CardDot = 2.5f;

        // －＋の押せる範囲。
        private const float HitSize = Layout.HitSize;

        // 重ねて開くレベルアップ。上下左右の中央に置く1列の窓。
        private static readonly Rect Modal = new(460, 184, 1000, 712);
        private const float ModalPad = 48f;

        // レベルアップの下の段（「やめる」と、使うルーンを添えた「レベルアップ」）の高さと「やめる」の幅。
        private const float FootHeight = 132f;
        private const float CancelWidth = 300f;

        // レベルアップの、Lvと「－」「＋」「最大」を並べる上の段の上端・高さ。
        private const float TopY = 40f;
        private const float TopHeight = 92f;

        // Baselineで並べた64ptのLvの数字は、見た目の中心が段の中心より23上に来る。Lvの段をその分下げて
        // 数字をボタンの中心にそろえ、矢印も数字の中心に置く。
        private const float DigitLift = 23f;

        // Lvの間の矢印（5×9ドット）の倍率。
        private const float LevelArrowScale = 6f;

        // レベルアップの、ステータス8行の上端・1行の高さ・幅。
        private const float DiffTop = 162f;
        private const float DiffHeight = 42f;
        private static float DiffWidth => Modal.width - ModalPad * 2;

        // カードの挿絵（64×58）の中央24×24ドット。スキルの仮のアイコンに使う（スキルの付け替えと共有）。
        public static readonly Rect SkillIconCrop = new(20f / 64f, 17f / 58f, 24f / 64f, 24f / 58f);

        private static readonly Color Green = new(0.49f, 0.88f, 0.42f);
        private static readonly Color Before = new(0.72f, 0.75f, 0.83f);
        private static readonly Color Dim = new(0.01f, 0.015f, 0.04f, 0.74f);
        private static readonly Color FigureSpot = new(0.01f, 0.015f, 0.04f, 0.7f);

        [MenuItem("Baryonyx/Training/Create Mock Data")]
        public static TrainingMockData CreateData()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            AssetDatabase.Refresh();
            var data = AssetFolders.LoadOrCreate<TrainingMockData>(DataPath);
            data.CostPerLevel = 100;
            data.MaxLevel = 100;
            data.MockRunes = 8450;
            // ステータスは Lv 100 の値（doc/features/progression.md の自キャラのLv100の
            // ステータス）。絵のないキャラは、絵を借りたキャラと同じスキルにする。
            var mage = Mage();
            var archer = Archer();
            var knight = Knight();
            var healer = Healer();
            data.Characters = new[]
            {
                // トーマのマナバーストは、1本目の枝の2段目までと2本目の枝の1段目を取得し、
                // 1本目の枝の1段目をセットしている（固有スキルの進化の仮データ）。
                Character(
                    "toma",
                    new(2300, 700, 1100, 2100, 1850, 1400, 300, 500),
                    mage,
                    new UniqueSkillProgress { Set = 1, Acquired = new[] { 1, 2, 3 } }
                ),
                Character("luka", new(2850, 1800, 1200, 1750, 1200, 1650, 900, 600), archer),
                Character("aria", new(3900, 1950, 2050, 800, 1300, 1200, 500, 300), knight),
                Character("mina", new(2500, 800, 1450, 1350, 1950, 1350, 200, 900), healer),
                Character("anselm", new(3600, 1700, 1900, 1200, 1400, 1050, 300, 500), knight),
                Character("greta", new(2400, 1700, 1000, 1250, 1000, 1750, 1000, 800), archer),
                Character("lutz", new(3200, 1300, 1750, 1600, 1400, 1100, 300, 400), mage),
                Character("rita", new(3000, 2100, 1250, 500, 850, 1600, 800, 500), healer),
                Character("ritsu", new(2100, 500, 900, 2050, 1650, 1500, 400, 600), archer),
            };
            // 絆の仮データ：トーマは絆2でしるし1つ、ルカは絆0でしるし2つ、アリアは上限の一つ手前（doc/features/monetization.md）。
            Bond(data, "toma", 2, 1);
            Bond(data, "luka", 0, 2);
            Bond(data, "aria", 5, 1);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            return data;
        }

        private static void Bond(TrainingMockData data, string id, int stage, int tokens)
        {
            var character = data.Find(id);
            character.Bond = stage;
            character.BondTokens = tokens;
        }

        private static TrainingCharacter Character(
            string id,
            CharacterStats level100,
            TrainingCharacter skills,
            params UniqueSkillProgress[] progress
        ) =>
            new()
            {
                Id = id,
                Level100 = level100,
                Passives = CombatPassives.Of(id).Select(Passive).ToArray(),
                Uniques = skills.Uniques,
                UniqueProgress = progress,
            };

        // 固有スキル3つを解放するLv。サーバーの UNIQUE_UNLOCK_LEVELS（server/src/features/party/catalog.ts）と同じ値。
        public static readonly int[] UniqueUnlockLevels = { 1, 10, 30 };

        private static TrainingCharacter Mage() =>
            Skills(
                Unique(
                    "マナバースト",
                    "敵全体に 属攻の80% のダメージ",
                    "CardRevelation",
                    "ManaBurst"
                ),
                Unique("星降り", "ランダムな敵に5回ダメージ", "CardMeteor", "StarFall"),
                Unique("魔眼", "敵全体の弱点を見抜き、1枚引く", "CardScout", "ArcaneEye")
            );

        private static TrainingCharacter Archer() =>
            Skills(
                Unique(
                    "雷撃の矢",
                    "敵1体に雷のダメージ。まひさせる",
                    "CardThunderSpear",
                    "ThunderArrow"
                ),
                Unique("矢の雨", "敵全体に3回ダメージ", "CardArrowRain", "ArrowStorm"),
                Unique("速射", "敵1体に2回ダメージ", "CardInsightArrow", "QuickShot")
            );

        private static TrainingCharacter Knight() =>
            Skills(
                Unique("盾の構え", "次のターンまでブロック +45", "CardProtect", "ShieldStance"),
                Unique("一閃", "敵1体に 物攻の200% のダメージ", "CardIai", "Issen"),
                Unique("鼓舞", "味方全体にブロック、攻撃力アップ", "CardWarCry", "Rally")
            );

        private static TrainingCharacter Healer() =>
            Skills(
                Unique("癒しの風", "味方全体のHPを回復", "CardHeal", "HealingWind"),
                Unique("蘇生", "倒れた味方1人を復活させる", "CardResurrection", "Revive"),
                Unique("裁きの光", "敵全体にダメージ、味方全体を回復", "CardHolyLight", "Judgment")
            );

        // 固有スキルは、並びの順に UniqueUnlockLevels のLvで解放する。
        private static TrainingCharacter Skills(params TrainingSkill[] uniques)
        {
            for (int i = 0; i < uniques.Length; i++)
                uniques[i].UnlockLevel = UniqueUnlockLevels[i];
            return new() { Uniques = uniques };
        }

        // パッシブは戦闘の定義（CombatPassives）から作り、アイコンは24×24ドットの専用の絵を使う。
        private static TrainingSkill Passive(CombatPassive passive) =>
            new()
            {
                Name = passive.Name,
                Description = passive.PlainText,
                UnlockLevel = passive.UnlockLevel,
                Icon = ArtAssets.LoadTexture($"{PassiveArtFolder}/{passive.Icon}.aseprite"),
            };

        /// <summary>The folder of the passive skills' icons (24x24 dots).</summary>
        public const string PassiveArtFolder = BattleInspectAssets.ArtFolder + "/Passives";

        // 固有スキル。名前は進化ツリーの最初の段階と同じにし、エネルギーはその段階のコストから引く。
        // 解放するLvは Skills で入れる。
        private static TrainingSkill Unique(
            string name,
            string description,
            string art,
            string tree
        )
        {
            int energy = UniqueSkillTrees.Find(tree)?.Stages[0]?.Card.Cost ?? 0;
            var skill = Skill(name, description, 0, energy, art);
            skill.Tree = tree;
            return skill;
        }

        // 固有スキルの絵はまだないため、スキルの挿絵を仮のアイコンにする。
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
        /// level-up, the stats beside the passives, the six deck cards (three unique skills, three
        /// custom skills) and the five equipment slots; and the level-up dialog (hidden) over
        /// everything. A flick over the page changes the person. Called while the guide
        /// screen's prefab is being built, so the shared frames and font are ready.
        /// </summary>
        public static GameObject BuildTrainingPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var data = CreateData();

            var (view, layer) = GuideFrameAssets.FramedPanel<TrainingView>(safe, "Training");
            view.Party = party;
            view.Data = data;
            view.Guide = guide;
            view.WeaponIcon = Guide.Icon(EquipmentAssets.IconWeaponPath);
            view.ArmorIcon = Guide.Icon(EquipmentAssets.IconArmorPath);
            view.ItemArt = Baryonyx.Inventory.Editor.ItemArtAssets.Load();

            // 金の2本線は、左のイラストの上に重ねる。
            GuideFrameAssets.SplitPanes(layer, Split, "PortraitPane", "DetailPane", divider: false);
            // ページの上の左右のフリックで人を替える。枠とボタンの上から動かしても届くよう、両方を載せる層で受ける。
            view.Swipe = layer.gameObject.AddComponent<GuideSwipe>();
            BuildPortrait(layer, view, party);
            GuideFrameAssets.Divider(layer, Split, Portrait.y);
            BuildDetail(layer, view);
            BuildDialog((RectTransform)view.transform, view);
            BuildBondDialog((RectTransform)view.transform, view);
            BuildCelebration((RectTransform)view.transform, view);

            // 停止中のPrefabでも1人目が読めるよう、仮の装備で生成時に一度描く。
            TrainingView.Build(view, party, data, EquipmentLocalSource.Starter()).Dispose();
            BakeCelebration(view, party, data);
            return view.gameObject;
        }

        // --- Left: the illustration and the battle sprite in its corner ---------------------

        private static void BuildPortrait(
            RectTransform layer,
            TrainingView view,
            PartyMockData party
        )
        {
            // イラストは枠を付けず、左の面いっぱいに敷き、右端を面の色へなじませる。
            var card = Layout.Box(
                layer,
                "Portrait",
                Portrait.x,
                Portrait.y,
                Portrait.width,
                Portrait.height
            );
            var picture = Rect("Illustration", card);
            Stretch(picture);
            view.Illustration = picture.gameObject.AddComponent<RawImage>();
            view.Illustration.raycastTarget = false;
            var fade = Layout.Box(
                card,
                "Fade",
                Portrait.width - PortraitFade,
                0,
                PortraitFade,
                Portrait.height
            );
            var fadeImage = fade.gameObject.AddComponent<RawImage>();
            fadeImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Guide.ShadeHorizontalPath);
            var paneColor = GuideFrameAssets.PaneLight;
            fadeImage.color = new Color(paneColor.r, paneColor.g, paneColor.b, 0.9f);
            fadeImage.raycastTarget = false;

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
            var panel = Layout.Box(layer, "Detail", Panel.x, Panel.y, Panel.width, Panel.height);
            float width = Panel.width - PanelPad * 2;

            // 行は左の中央を起点にし、縮めても上下の中央が「絆」「レベルアップ」とそろう。
            float headerWidth = LevelUpX - BondGap - BondWidth - BondGap - PanelPad;
            var header = Layout.Row(panel, "Header", PanelPad, 18, headerWidth, 92, 14);
            header.pivot = new Vector2(0f, 0.5f);
            header.anchoredPosition = new Vector2(PanelPad, -18 - 92 / 2f);
            view.Header = header;
            view.HeaderWidth = headerWidth;
            view.Name = Label(
                header,
                "Name",
                "",
                60,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.Fit(view.Name, 0);
            Layout.Fit(
                Label(
                    header,
                    "LevelTag",
                    "Lv",
                    30,
                    UiPalette.TextSub,
                    TextAlignmentOptions.Midline
                ),
                0
            );
            view.Level = Label(
                header,
                "Level",
                "",
                60,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.Fit(view.Level, 0);
            view.Elements = Enumerable
                .Range(0, 3)
                .Select(i => Layout.RowIcon(header, "Element" + i, 12f * Dot))
                .ToArray();
            BuildLevelUp(panel, view);
            BuildBond(panel, view);

            // キャラ自身：ステータス（2列4行）と、その右にパッシブ2つ。
            view.Stats = StatGridAssets.Build(panel, PanelPad, StatsTop, StatsWidth, 2, StatRow);
            view.Passives = BuildPassives(panel);

            // デッキに入る6枚：固有スキル3枚と、カスタムスキル3枚。戦闘のカードと同じ組み方の小さなカード。
            var cardSize = DeckCardAssets.Size(CardDot, small: true);
            int half = TrainingPresenter.CustomSlots;
            float deckWidth = cardSize.x * half * 2 + CardGap * (half - 1) * 2 + PairGap;
            float deckLeft = (Panel.width - deckWidth) / 2f;
            var cardX = Enumerable
                .Range(0, half * 2)
                .Select(i =>
                    deckLeft + (cardSize.x + CardGap) * i - (i >= half ? CardGap - PairGap : 0)
                )
                .ToArray();
            GuideFrameAssets.Heading(
                panel,
                "UniquesTitle",
                "固有スキル",
                cardX[0] - 4,
                DeckTop - 40,
                300,
                36,
                26
            );
            var customTitle = Layout.Row(
                panel,
                "CustomTitle",
                cardX[half],
                DeckTop - 36,
                420,
                32,
                14,
                TextAnchor.LowerLeft
            );
            Layout.Fit(
                Label(
                    customTitle,
                    "Title",
                    "カスタムスキル",
                    24,
                    UiPalette.Gold,
                    TextAlignmentOptions.BottomLeft
                ),
                0
            );
            GuideFrameAssets.HeadingRow(customTitle, 26);
            Layout.Line(
                panel,
                "DeckLine",
                cardX[half - 1] + cardSize.x + PairGap / 2f - 1,
                DeckTop + 8,
                2,
                cardSize.y - 16
            );
            view.CardFrames = Enum.GetValues(typeof(CardElement))
                .Cast<CardElement>()
                .Select(element => (Texture)CardArt($"CardFrame{element}"))
                .ToArray();
            view.Uniques = Enumerable
                .Range(0, CardLoadout.CardLoadoutPresenter.UniqueCount)
                .Select(i => Card(panel, "Unique" + i, cardX[i]))
                .ToArray();
            view.CardSlots = Enumerable
                .Range(0, TrainingPresenter.CustomSlots)
                .Select(i => Card(panel, "Custom" + i, cardX[half + i]))
                .ToArray();

            // 武器・防具・アクセサリー3つを同じ大きさの枠で並べる。
            GuideFrameAssets.Heading(
                panel,
                "GearTitle",
                "装備",
                PanelPad - 4,
                GearTop - 40,
                300,
                36,
                26
            );
            float gear =
                (width - GearGap * (TrainingPresenter.GearSlots - 1)) / TrainingPresenter.GearSlots;
            view.Gear = Enumerable
                .Range(0, TrainingPresenter.GearSlots)
                .Select(i => BuildGear(panel, i, PanelPad + i * (gear + GearGap), GearTop, gear))
                .ToArray();
        }

        // 右上の「レベルアップ」（すりガラスのカード）。1レベル上げるのに要るルーンを添え、押すと重ねた画面を開く。
        private static void BuildLevelUp(RectTransform panel, TrainingView view)
        {
            // 金の縁と光（選んだ印）は出さず、背景の模様も付けない。
            var (button, _, levelUp) = GlassCardAssets.Choice(
                Layout.Box(panel, "LevelUp", LevelUpX, 18, LevelUpWidth, 96),
                pattern: false
            );
            view.LevelUp = button;
            view.LevelUpLabel = Label(
                levelUp,
                "Label",
                "レベルアップ",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.At(view.LevelUpLabel, 22, 0, 184, 96);
            Guide.Shrink(view.LevelUpLabel, 22);
            var cost = Layout.Box(levelUp, "Cost", 214, 0, LevelUpWidth - 218, 96);
            view.LevelUpCost = cost.gameObject;
            var icon = Icon(cost, "Icon", Guide.IconRunePath);
            Layout.At(icon, 0, 29, 38, 38);
            view.LevelUpCostLabel = Label(
                cost,
                "Value",
                "",
                28,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineRight
            );
            Layout.At(view.LevelUpCostLabel, 40, 0, LevelUpWidth - 262, 96);
            Guide.Shrink(view.LevelUpCostLabel, 20);
        }

        // パッシブ2つを、見出しの下に1行ずつ（アイコン、名前、効果）。未解放は名前の行の右に解放するLv。
        private static TrainingSkillWidget[] BuildPassives(RectTransform panel)
        {
            var box = Layout.Box(
                panel,
                "Passives",
                Passives.x,
                Passives.y,
                Passives.width,
                Passives.height
            );
            // カードにはせず、見出しと2行だけを面の上に置く。
            GuideFrameAssets.Heading(box, "Title", "パッシブ", 14, 6, 240, 34, 22);
            Layout.Line(box, "Line", 18, 42 + PassiveRow, Passives.width - 36, 2);
            return Enumerable
                .Range(0, 2)
                .Select(i =>
                {
                    var row = Layout.Box(
                        box,
                        "Passive" + i,
                        0,
                        44 + i * (PassiveRow + 4),
                        Passives.width,
                        PassiveRow
                    );
                    var widget = new TrainingSkillWidget();
                    (widget.Icon, widget.Lock) = SkillIcon(
                        Layout.Box(row, "Icon", 18, (PassiveRow - 48) / 2f, 48, 48)
                    );
                    // パッシブのアイコンは24×24ドットの絵そのもの（2倍）で、挿絵の切り抜きではない。
                    widget.Icon.uvRect = new Rect(0, 0, 1, 1);

                    const float textX = 82;
                    widget.Name = Label(
                        row,
                        "Name",
                        "",
                        24,
                        UiPalette.TextMain,
                        TextAlignmentOptions.TopLeft
                    );
                    Layout.At(widget.Name, textX, 4, 300, 30);
                    Guide.Shrink(widget.Name, 18);
                    widget.When = Label(
                        row,
                        "When",
                        "",
                        18,
                        UiPalette.Gold,
                        TextAlignmentOptions.TopRight
                    );
                    Layout.At(widget.When, Passives.width - 18 - 200, 8, 200, 24);
                    widget.Description = Label(
                        row,
                        "Description",
                        "",
                        18,
                        UiPalette.TextSub,
                        TextAlignmentOptions.TopLeft
                    );
                    // 説明は2行まで折り返す。
                    widget.Description.textWrappingMode = TextWrappingModes.Normal;
                    Layout.At(widget.Description, textX, 32, Passives.width - textX - 18, 42);
                    Guide.Shrink(widget.Description, 15);
                    return widget;
                })
                .ToArray();
        }

        // デッキの小さなカード1枚。押すと、その枠を選んだスキルの付け替えの画面を開く。
        private static TrainingCardWidget Card(RectTransform panel, string name, float x) =>
            DeckCardAssets.Build(panel, name, x, DeckTop, CardDot, button: true, small: true);

        private static Texture2D CardArt(string name) => DeckCardAssets.CardArt(name);

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
            // 装備の枠は小さいので、カードの模様は付けない。まだ付けられないアクセサリーは点線にする。
            var (button, _, cell) = GlassCardAssets.Choice(
                Layout.Box(panel, "Gear" + index, x, y, width, GearHeight),
                dashed: index >= TrainingPresenter.OpenGearSlots,
                pattern: false
            );
            var widget = new TrainingGearWidget { Button = button };
            var icon = Layout.Box(cell, "Icon", 14, 14, 48, 48);
            widget.Icon = icon.gameObject.AddComponent<Image>();
            widget.Icon.raycastTarget = false;
            // 武器・防具のアイコンには、付けている装備の品質のキラキラを重ねる。
            if (index < TrainingPresenter.OpenGearSlots)
                widget.Foil = CardFoilAssets.Add(widget.Icon);
            widget.Slot = Label(
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
                Layout.At(widget.Slot, 72, 14, width - 80, 24);
            else
            {
                widget.Slot.alignment = TextAlignmentOptions.Top;
                Layout.At(widget.Slot, 8, 18, width - 16, 24);
            }
            Guide.Shrink(widget.Slot, 14);
            widget.Stars = Label(
                cell,
                "Stars",
                "",
                20,
                UiPalette.Gold,
                TextAlignmentOptions.TopLeft
            );
            Layout.At(widget.Stars, 72, 40, width - 80, 24);
            widget.Name = Label(
                cell,
                "Name",
                "",
                22,
                UiPalette.TextMain,
                TextAlignmentOptions.TopLeft
            );
            Layout.At(widget.Name, 14, 74, width - 28, 30);
            Guide.Shrink(widget.Name, 16);
            widget.Detail = Label(
                cell,
                "Detail",
                "",
                18,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft
            );
            Layout.At(widget.Detail, 14, 106, width - 28, GearHeight - 118);
            widget.Detail.textWrappingMode = TextWrappingModes.Normal;

            var locked = Rect("Lock", cell);
            Stretch(locked);
            var lockImage = Layout.Box(locked, "Image", (width - 9 * 4) / 2f, 62, 9 * 4, 10 * 4);
            SpriteImage(lockImage, GuideFrameAssets.LockPath, Color.white);
            // 開いていないアクセサリーの枠は、解放するLv（「Lv 20で解放」）を出す。
            int accessory = index - TrainingPresenter.OpenGearSlots;
            widget.When = Label(
                locked,
                "When",
                accessory >= 0 ? AccessorySlots.UnlockText(accessory) : "",
                22,
                UiPalette.Gold,
                TextAlignmentOptions.Center
            );
            Layout.At(widget.When, 8, 112, width - 16, 30);
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
            // 枠つきの画面と同じ、濃い紺に金の細い線の窓。見出しや名前は出さず、Lvから始める。
            var modal = GuideFrameAssets.Window(
                layer,
                "Modal",
                Modal.x,
                Modal.y,
                Modal.width,
                Modal.height
            );
            float width = Modal.width;

            // 上の段は、左に今のLvと上げたあとのLv、右に「－」「＋」「最大」。
            float half = DiffWidth / 2f;
            var levels = Layout.Row(
                modal,
                "Levels",
                ModalPad,
                TopY + DigitLift,
                half,
                TopHeight,
                22,
                TextAnchor.MiddleLeft
            );
            Layout.Fit(
                Label(
                    levels,
                    "FromTag",
                    "Lv",
                    36,
                    UiPalette.TextSub,
                    TextAlignmentOptions.Baseline
                ),
                0
            );
            view.From = Label(levels, "From", "", 64, Before, TextAlignmentOptions.Baseline);
            Layout.Fit(view.From, 0);
            var arrowCell = Rect("Arrow", levels);
            Layout.Fit(arrowCell, 5 * LevelArrowScale);
            var arrow = Rect("Image", arrowCell);
            Place(arrow, new Vector2(0, DigitLift), new Vector2(5, 9) * LevelArrowScale);
            SpriteImage(arrow, Guide.ArrowPath, UiPalette.Gold);
            Layout.Fit(
                Label(levels, "ToTag", "Lv", 36, UiPalette.TextSub, TextAlignmentOptions.Baseline),
                0
            );
            view.To = Label(levels, "To", "", 64, UiPalette.Gold, TextAlignmentOptions.Baseline);
            Layout.Fit(view.To, 0);

            var step = Layout.Row(
                modal,
                "Step",
                ModalPad + half,
                TopY,
                half,
                TopHeight,
                16,
                TextAnchor.MiddleRight
            );
            view.Less = Square(step, "Less", "－", HitSize);
            view.More = Square(step, "More", "＋", HitSize);
            view.Max = Square(step, "Max", "最大", 140);

            view.Diffs = Enumerable
                .Range(0, CharacterStats.Count)
                .Select(i => BuildDiff(modal, i, ModalPad, DiffTop + i * DiffHeight))
                .ToArray();

            // 下の段は、左に「やめる」、右に使うルーンを添えた「レベルアップ」を窓の幅いっぱいに並べる。
            float footY = Modal.height - ModalPad - FootHeight;
            float confirmWidth = DiffWidth - CancelWidth - 24;
            float confirmX = width - ModalPad - confirmWidth;
            var (cancelButton, _, cancel) = GlassCardAssets.Choice(
                Layout.Box(modal, "Cancel", ModalPad, footY, CancelWidth, FootHeight),
                pattern: false
            );
            view.Cancel = cancelButton;
            var cancelLabel = Label(
                cancel,
                "Label",
                "やめる",
                40,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)cancelLabel.transform);

            // 決める方は、育成の画面の「レベルアップ」と同じ金の縁と光のカードにする。
            var (confirmButton, confirmRim, confirm) = GlassCardAssets.Choice(
                Layout.Box(modal, "Confirm", confirmX, footY, confirmWidth, FootHeight),
                pattern: false
            );
            confirmRim.SetActive(true);
            view.Confirm = confirmButton;
            var confirmLabel = Label(
                confirm,
                "Label",
                "レベルアップ",
                42,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Layout.At(confirmLabel, 0, 14, confirmWidth, 56);
            // 使うルーンは「レベルアップ」の下に、右上の所持ルーンと同じ大きさのアイコン（2倍）と
            // ステータスの数字の大きさで添える。
            const float runeSize = 24f * 2f;
            var costRow = Layout.Row(
                confirm,
                "Cost",
                0,
                72,
                confirmWidth,
                runeSize,
                10,
                TextAnchor.MiddleCenter
            );
            Layout.RowIcon(costRow, "RuneIcon", runeSize).sprite = ArtAssets.LoadSprite(
                Guide.IconRunePath
            );
            view.Cost = Label(
                costRow,
                "Value",
                "",
                34,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.Fit(view.Cost, 0);

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
            float w = DiffWidth;
            var row = Layout.Box(modal, "Diff" + index, x, y, w, DiffHeight);
            Layout.Line(row, "Line", 0, DiffHeight - 2, w, 2);
            var label = Label(
                row,
                "Label",
                CharacterStats.Labels[index],
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.BottomLeft
            );
            Layout.At(label, 0, 0, 220, text);
            var widget = new TrainingDiffWidget
            {
                Before = Label(row, "Before", "", 30, Before, TextAlignmentOptions.BottomRight),
                After = Label(
                    row,
                    "After",
                    "",
                    34,
                    UiPalette.TextMain,
                    TextAlignmentOptions.BottomRight
                ),
                Gain = Label(row, "Gain", "", 28, Green, TextAlignmentOptions.BottomRight),
            };
            Layout.At(widget.Before, 220, 0, 180, text);
            Arrow(row, "Arrow", 432, 9, 3);
            Layout.At(widget.After, 470, 0, 180, text);
            Layout.At(widget.Gain, 650, 0, w - 650, text);
            return widget;
        }

        private static Button Square(RectTransform row, string name, string label, float width)
        {
            var rect = Rect(name, row);
            Layout.Fit(rect, width);
            var (button, _, inside) = GlassCardAssets.Choice(rect, pattern: false);
            var text = Label(
                inside,
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

        /// <summary>
        /// A skill's icon on <paramref name="icon"/> (the middle of its card art, until skills
        /// have icons) with the lock over it for a skill not unlocked yet. The card skills' change
        /// screen shows the unique skills with it too.
        /// </summary>
        public static (RawImage Icon, GameObject Lock) SkillIcon(RectTransform icon)
        {
            var image = icon.gameObject.AddComponent<RawImage>();
            image.uvRect = SkillIconCrop;
            image.raycastTarget = false;
            return (image, GuideFrameAssets.IconLock(icon));
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
            var arrow = Layout.Box(parent, name, x, y, 5 * scale, 9 * scale);
            SpriteImage(arrow, Guide.ArrowPath, UiPalette.Gold);
        }
    }
}
