using System;
using System.IO;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Combat;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Equipment;
using Baryonyx.Equipment.Editor;
using Baryonyx.Items;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.Training;
using Baryonyx.Training.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Cards;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Frame = Baryonyx.UI.GuideMenu.Editor.GuideFrameAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Library.Editor
{
    /// <summary>
    /// The library (doc/features/library.md): a guide screen whose menu lists the kinds, each
    /// opening a panel of every entry's card with the chosen one beside it, and the daily steps.
    /// The entries are written into <see cref="DataPath"/> from each feature's own definitions,
    /// so run this again when a definition changes. The guide is the elf librarian in the great
    /// library's stacks.
    /// </summary>
    public static class LibraryScreenAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Library";
        public const string DefinitionPath = Folder + "/Data/LibraryGuideMenu.asset";
        public const string DataPath = Folder + "/Data/LibraryData.asset";
        public const string PrefabPath = Folder + "/UI/LibraryScreen.prefab";

        // 案内人はエルフの司書（Codexで高解像度に生成し、32色・200×262ドット以内に縮めた）。
        // 背景は大図書館の書庫で、ほかの案内人の画面と同じく生成画像をそのまま使う。
        public const string GuideArtPath = Folder + "/UI/Art/LibraryGuide.aseprite";
        public const string BackgroundPath = Folder + "/UI/Art/LibraryBackground.png";

        // メニューのアイコン。ほかの案内人の画面と同じく、縁取りのない白一色の形に文字の色を焼き込む。
        public const string IconCharacterPath = Folder + "/UI/Art/IconCharacter.png";
        public const string IconGearPath = Folder + "/UI/Art/IconGear.png";
        public const string IconSkillPath = Folder + "/UI/Art/IconSkill.png";
        public const string IconItemPath = Folder + "/UI/Art/IconItem.png";
        public const string IconEnemyPath = Folder + "/UI/Art/IconEnemy.png";
        public const string IconStepsPath = Folder + "/UI/Art/IconSteps.png";

        // 手前の形と、その後ろの形のあいだに空ける透明な隙間（設計座標）。
        private const float PartGap = 2.6f;

        private const string EnemyArtFolder = "Assets/Baryonyx/Features/Combat/UI/Art/Enemies";

        // 一覧（左）と個別（右）の境目と、カードの大きさ。設計座標、左上から。
        private const float Gap = 24f;
        private const float HeadingHeight = GuidePanelTemplate.HeadingHeight;
        private const int Columns = 4;
        private const float ListTop = GuidePanelTemplate.ListTop;

        // 装備の一覧は左の縦の分類のぶん狭いため、3列にする。
        private const int GearColumns = 3;
        private const float CardHeight = 300f;
        private const float TileArtBox = 168f;
        private const float DetailLeft = GuidePanelTemplate.DetailLeft;
        private const float DetailWidth = GuidePanelTemplate.DetailWidth;
        private const float DetailArtBox = 224f;

        private const string GearLabel = "装備";

        // 装備のパネルの左に並べる分類。武器・防具・アクセサリーのどれも開いている（アクセサリーは2026-11-04に
        // 錠前を外した。ユーザーの指示）。
        private static readonly LibraryKind[] GearTabs =
        {
            LibraryKind.Weapon,
            LibraryKind.Armor,
            LibraryKind.Accessory,
        };

        [MenuItem("Baryonyx/Library/Create Screen Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();
            var data = CreateData();
            var training = TrainingAssets.CreateData();
            WriteMenuIcons();
            CreateScreen(
                DefinitionPath,
                PrefabPath,
                GuideArtPath,
                BackgroundPath,
                Fill,
                itemPanel: (item, safe, view) =>
                {
                    var kind = KindOf(item.Key);
                    if (kind == null)
                        return null;
                    return kind == LibraryKind.Steps
                        ? BuildStepsPanel(safe, view)
                        : BuildCatalogPanel(safe, view, kind.Value, data, training);
                },
                overlay: BuildDialog
            );
        }

        // 入手先のダンジョン名を押したときに、入るかを確かめる窓。画面全体を覆うため、パネルの外に1つだけ置き、
        // 各分類から使う。端末の戻るキーは開いているパネル（LibraryCatalogView）が窓へ渡す。
        private static GameObject BuildDialog(RectTransform root, GuideMenuView guide)
        {
            var dialog = GameDialogAssets.Build(root);
            foreach (var view in root.GetComponentsInChildren<LibraryCatalogView>(true))
                view.Dialog = dialog;
            return null;
        }

        private static LibraryKind? KindOf(string key)
        {
            foreach (LibraryKind kind in Enum.GetValues(typeof(LibraryKind)))
                if (LibraryKinds.GuideItemKey(kind) == key)
                    return kind;
            return null;
        }

        private static void Fill(GuideMenuDefinition d)
        {
            d.Title = "ライブラリ";
            d.Layout = GuideMenuLayout.List;
            // メニューに収まる6行にするため、武器・防具・アクセサリーは「装備」1行にまとめ、
            // 装備のパネルの左の分類で切り替える。防具・アクセサリーは武器のパネルから開く隠れた項目。
            var gear = Kind(LibraryKind.Weapon, IconGearPath);
            gear.Label = GearLabel;
            var armor = Kind(LibraryKind.Armor, null);
            armor.Hidden = true;
            var accessory = Kind(LibraryKind.Accessory, null);
            accessory.Hidden = true;
            d.Items = new[]
            {
                Kind(LibraryKind.Character, IconCharacterPath),
                gear,
                Kind(LibraryKind.Skill, IconSkillPath),
                Kind(LibraryKind.Item, IconItemPath),
                Kind(LibraryKind.Enemy, IconEnemyPath),
                Kind(LibraryKind.Steps, IconStepsPath),
                armor,
                accessory,
            };
        }

        private static GuideMenuItem Kind(LibraryKind kind, string iconPath)
        {
            var item = Item(
                LibraryKinds.Label(kind),
                "見る",
                iconPath != null ? Icon(iconPath) : null
            );
            item.Key = LibraryKinds.GuideItemKey(kind);
            return item;
        }

        // --- The menu's icons ---------------------------------------------------------------

        private static void WriteMenuIcons()
        {
            Directory.CreateDirectory(Folder + "/UI/Art");
            Companions().Save(IconCharacterPath, SmoothIconTextureSize, UiPalette.TextMain);
            Helmet().Save(IconGearPath, SmoothIconTextureSize, UiPalette.TextMain);
            Cards().Save(IconSkillPath, SmoothIconTextureSize, UiPalette.TextMain);
            Potion().Save(IconItemPath, SmoothIconTextureSize, UiPalette.TextMain);
            Horned().Save(IconEnemyPath, SmoothIconTextureSize, UiPalette.TextMain);
            Footprints().Save(IconStepsPath, SmoothIconTextureSize, UiPalette.TextMain);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        // Two companions, one in front of the other.
        private static VectorIcon Companions() =>
            new VectorIcon()
                .Fill(IconShape.Circle(42f, 17f, 8f))
                .Fill(IconShape.Path("M28 47 Q28 29 42 29 Q56 29 56 47Z"))
                .Fill(IconShape.Circle(23f, 25f, 10f), PartGap)
                .Fill(IconShape.Path("M5 60 Q5 39 23 39 Q41 39 41 60Z"), PartGap);

        // A knight's helmet with a visor slit and a plume.
        private static VectorIcon Helmet() =>
            new VectorIcon()
                .Fill(IconShape.Path("M30 10 Q40 0 52 4 Q44 6 38 12Z"))
                .Fill(IconShape.Path("M12 42 Q12 12 32 12 Q52 12 52 42 L52 58 L12 58Z"), PartGap)
                .Cut(IconShape.Rect(18f, 30f, 28f, 5f))
                .CutStroke(IconShape.Path("M32 37 L32 54"), 3f);

        // Two cards fanned out, the front one with a star.
        private static VectorIcon Cards() =>
            new VectorIcon()
                .Fill(IconShape.Rect(14f, 8f, 28f, 42f, 3f).Rotated(-14f, 28f, 30f))
                .Fill(IconShape.Rect(24f, 12f, 28f, 42f, 3f).Rotated(10f, 38f, 34f), PartGap)
                .Cut(
                    IconShape
                        .Path(
                            "M38 22 L40.6 29 L48 29.3 L42.2 33.9 L44.2 41 L38 37 L31.8 41 L33.8 33.9 L28 29.3 L35.4 29Z"
                        )
                        .Rotated(10f, 38f, 34f)
                );

        // A round potion flask with a cork and the liquid's line.
        private static VectorIcon Potion() =>
            new VectorIcon()
                .Fill(IconShape.Rect(24f, 4f, 16f, 7f, 2f))
                .Fill(IconShape.Rect(27f, 13f, 10f, 14f, 1f), PartGap)
                .Fill(IconShape.Circle(32f, 42f, 18f))
                .CutStroke(IconShape.Path("M17 40 Q24 35 32 40 Q40 45 47 40"), 2.6f)
                .Cut(IconShape.Circle(24f, 48f, 3f));

        // A horned monster's head with fierce eyes and fangs.
        private static VectorIcon Horned() =>
            new VectorIcon()
                .Fill(IconShape.Path("M15 24 Q4 16 6 2 Q12 14 22 18Z"))
                .Fill(IconShape.Path("M49 24 Q60 16 58 2 Q52 14 42 18Z"))
                .Fill(
                    IconShape.Path(
                        "M12 34 Q12 14 32 14 Q52 14 52 34 Q52 46 44 52 L44 60 L20 60 L20 52 Q12 46 12 34Z"
                    ),
                    PartGap
                )
                .Cut(IconShape.Path("M19 30 L29 34 L27 40 L20 38Z"))
                .Cut(IconShape.Path("M45 30 L35 34 L37 40 L44 38Z"))
                .Cut(IconShape.Path("M24 50 L28 57 L32 50 L36 57 L40 50Z"));

        // Two footprints walking up to the right.
        private static VectorIcon Footprints()
        {
            VectorIcon Foot(VectorIcon icon, float dx, float dy) =>
                icon.Fill(
                        IconShape
                            .Path("M10 30 Q8 44 14 54 Q20 60 23 52 Q25 40 21 30 Q16 23 10 30Z")
                            .Translated(dx, dy)
                    )
                    .Fill(IconShape.Circle(10f + dx, 21f + dy, 3.2f))
                    .Fill(IconShape.Circle(16.5f + dx, 18.5f + dy, 2.8f))
                    .Fill(IconShape.Circle(22f + dx, 21f + dy, 2.5f));
            return Foot(Foot(new VectorIcon(), 4f, 6f), 30f, -12f);
        }

        // --- The entries, from each feature's own definitions -------------------------------

        /// <summary>The kind line of a card's entry (a skill's or an item's): its kind and target, and its cost.</summary>
        public static string KindLineOf(CardSkill card, int cost) =>
            $"{CardText.KindLine(card)}　コスト {cost}";

        public static LibraryData CreateData()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            AssetDatabase.Refresh();
            var party = PartyAssets.CreateData();
            var training = TrainingAssets.CreateData();
            var data = AssetFolders.LoadOrCreate<LibraryData>(DataPath);
            var weaponIcon = ArtAssets.ImportTexture(
                EquipmentAssets.IconWeaponPath,
                FilterMode.Point
            );
            var armorIcon = ArtAssets.ImportTexture(
                EquipmentAssets.IconArmorPath,
                FilterMode.Point
            );

            var characters = party.Members.Select(
                (member, index) =>
                {
                    // パッシブは右の面のパッシブの欄に出すため、説明には書かない（2026-11-04）。
                    return new LibraryEntryData
                    {
                        Kind = LibraryKind.Character,
                        Id = member.Id,
                        Number = index + 1,
                        Name = member.Name,
                        Description = "",
                        Art = member.Art,
                        ArtUv = FigureUv(member),
                        ArtTint = member.Tint,
                    };
                }
            );
            // 装備・アイテムは種類ごとの絵（ItemArt）を出す。絵のない種類は、武器・防具のアイコンにする。
            var itemArt = Baryonyx.Inventory.Editor.ItemArtAssets.Load();
            var equipment = new[]
            {
                EquipmentSlot.Weapon,
                EquipmentSlot.Armor,
                EquipmentSlot.Accessory,
            }.SelectMany(slot =>
                EquipmentCatalog
                    .All.Where(entry => entry.Slot == slot)
                    .Select(
                        (entry, index) =>
                            new LibraryEntryData
                            {
                                Kind = slot switch
                                {
                                    EquipmentSlot.Weapon => LibraryKind.Weapon,
                                    EquipmentSlot.Armor => LibraryKind.Armor,
                                    _ => LibraryKind.Accessory,
                                },
                                Id = entry.Id,
                                Number = index + 1,
                                Name = entry.Name,
                                Rarity = entry.Rarity,
                                KindLine = EquipmentCatalog.NameOf(slot),
                                Description = entry.Detail,
                                Art =
                                    itemArt.Find(entry.Id) != null ? itemArt.TextureOf(entry.Id)
                                    : slot == EquipmentSlot.Weapon ? weaponIcon
                                    : armorIcon,
                            }
                    )
            );
            var items = ItemCards.All.Select(
                (item, index) =>
                    new LibraryEntryData
                    {
                        Kind = LibraryKind.Item,
                        Id = item.Id,
                        Number = index + 1,
                        Name = item.Name,
                        Rarity = item.Rarity,
                        KindLine = KindLineOf(item.Card, item.Cost),
                        Description = CardText.Description(item.Card),
                        Art = itemArt.TextureOf(item.Id),
                    }
            );
            var skills = CardSkills.All.Select(
                (card, index) =>
                    new LibraryEntryData
                    {
                        Kind = LibraryKind.Skill,
                        Id = card.Id,
                        Number = index + 1,
                        Name = card.Name,
                        // ☆は、冒険のドロップの一覧が持つ（doc/features/drops.md）。
                        Rarity = AdventureDrops.StarsOf(AdventureDropCategory.Skill, card.Id),
                        KindLine = KindLineOf(card, card.Cost),
                        Description = CardText.Description(card),
                        Art = party.ArtOf(card.Id),
                    }
            );
            var enemies = CombatEnemies.All.Select(
                (enemy, index) =>
                    new LibraryEntryData
                    {
                        Kind = LibraryKind.Enemy,
                        Id = enemy.Id,
                        Number = index + 1,
                        Name = enemy.Name,
                        Art = ArtAssets.ImportTexture(
                            $"{EnemyArtFolder}/{ArtName(enemy.Id)}.aseprite",
                            FilterMode.Point
                        ),
                    }
            );

            data.Entries = characters
                .Concat(equipment)
                .Concat(skills)
                .Concat(items)
                .Concat(enemies)
                .ToArray();
            data.ElementIcons = party.ElementIcons;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            return data;
        }

        // 「moss-slime」→「MossSlime」。敵の絵のファイル名。
        private static string ArtName(string id) =>
            string.Concat(
                id.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1))
            );

        // 立ち姿の範囲（周りの透明な余白を切る）。左右を反転するキャラは反転する。
        private static Rect FigureUv(PartyMember member)
        {
            if (member.Art == null)
                return new Rect(0f, 0f, 1f, 1f);
            float width = member.Art.width;
            float height = member.Art.height;
            var figure = PartyArt.Figure;
            var uv = new Rect(
                figure.x / width,
                1f - figure.yMax / height,
                figure.width / width,
                figure.height / height
            );
            return member.Flip ? new Rect(uv.xMax, uv.y, -uv.width, uv.height) : uv;
        }

        // --- One kind: the cards on the left and the chosen entry on the right ---------------

        private static GameObject BuildCatalogPanel(
            RectTransform safe,
            GuideMenuView guide,
            LibraryKind kind,
            LibraryData data,
            Baryonyx.Training.TrainingMockData training
        )
        {
            GlassCardAssets.EnsureArt();
            // 装備（武器・防具・アクセサリー）は、左の縦の分類で切り替え、一覧を分類の右に置く（横のタブは置かない）。
            bool gear = Array.IndexOf(GearTabs, kind) >= 0;
            var (view, panel) = GuidePanelTemplate.GridDetail<LibraryCatalogView>(
                safe,
                "Library" + kind,
                LibraryKinds.Label(kind),
                gear ? GearColumns : Columns,
                CardHeight,
                gear ? GearTabs.Select(LibraryKinds.Label).ToArray() : null
            );
            var layer = panel.Layer;
            view.Guide = guide;
            view.Kind = kind;
            view.Data = data;
            view.Training = training;
            view.TileArtBox = TileArtBox;
            view.DetailArtBox = DetailArtBox;
            if (gear)
            {
                view.KindTabs = panel.Tabs;
                view.KindTabKeys = GearTabs.Select(LibraryKinds.GuideItemKey).ToArray();
                for (int i = 0; i < view.KindTabs.Length; i++)
                    if (view.KindTabs[i].Lock == null)
                        view.KindTabs[i].Chosen = GearTabs[i] == kind;
            }
            view.Count = panel.Count;

            var entries = data.Entries.Where(entry => entry.Kind == kind).ToArray();
            float cardWidth = panel.Cell.x;
            view.Tiles = entries
                .Select(entry => BuildTile(panel.Content, entry, cardWidth))
                .ToArray();
            // 読み込む間に伏せるのは、開いたときに見える先頭のカードだけ（LibraryCatalogView.CoverCards）。
            foreach (var tile in view.Tiles.Take(LibraryCatalogView.CoverCards))
                GlassCardAssets.AddBack(tile.Card);
            view.List = panel.Scroll;
            view.Status = panel.Status;
            view.Status.text = entries.Length == 0 ? LibraryCatalogView.PreparingText : "";

            BuildDetail(layer, view);
            // 仮の記録で描き、Prefabを開くと停止中でも一覧が読めるようにする。
            var mock = LibraryMemorySource.Mock();
            foreach (var tile in view.Tiles)
            {
                var entry = entries.First(e => e.Id == tile.Id);
                bool found = mock.IsFound(kind, entry.Id);
                LibraryCatalogView.PaintArt(tile.Art, entry, found, TileArtBox);
                tile.Name.gameObject.SetActive(found);
            }
            return view.gameObject;
        }

        private static ItemTile BuildTile(
            RectTransform content,
            LibraryEntryData entry,
            float cardWidth
        )
        {
            var rect = Rect("Entry_" + entry.Id, content);
            var (widget, inside) = ItemTileAssets.Build(rect, pattern: true);
            widget.Id = entry.Id;

            // 札の形（doc/rules/ui-design.md の装備の見せ方）：左上に番号（品質の位置。図鑑の物は品質を持たない）、
            // 中央に絵、下に名前と☆。
            widget.Number = Layout.Text(
                inside,
                "Number",
                LibraryCatalogView.NumberText(entry.Number),
                UiType.Caption,
                UiPalette.Gold,
                TextAlignmentOptions.MidlineLeft,
                ItemTileAssets.CornerInset,
                6f,
                cardWidth - 40f,
                30f
            );
            var box = Layout.Box(
                inside,
                "ArtBox",
                (cardWidth - TileArtBox) / 2f,
                36f,
                TileArtBox,
                TileArtBox
            );
            widget.Art = Art(box);
            widget.Name = Layout.Text(
                inside,
                "Name",
                entry.Name,
                UiType.Small,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                12f,
                CardHeight - 90f,
                cardWidth - 24f,
                40f
            );
            Shrink(widget.Name, 20);
            widget.Stars = Layout.Text(
                inside,
                "Stars",
                ItemText.Stars(entry.Rarity),
                UiType.Caption,
                UiPalette.Gold,
                TextAlignmentOptions.Center,
                12f,
                CardHeight - 50f,
                cardWidth - 24f,
                32f
            );
            // 新しく載った印は、ほかの札の印と同じく右上に置く。
            ItemTileAssets.CornerTag(
                widget,
                inside,
                cardWidth,
                "NEW",
                UiPalette.Gold,
                plate: false,
                "New"
            );
            return widget;
        }

        // ドット絵を置く枠。絵は枠の中央に、収まる整数倍で置く（大きさは実行時に合わせる）。
        private static RawImage Art(RectTransform box)
        {
            var rect = Rect("Art", box);
            Place(rect, Vector2.zero, box.sizeDelta);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        // キャラクターの固有スキルのカード（戦闘のカードを効果の文まで3倍）と、パッシブの行。
        private const float UniqueDot = 3f;
        private const float PassiveRow = 100f;
        private const float PassiveIcon = 72f;

        // キャラクターの右の面の名前の下：見出し「固有スキル」の下に今セットしている3枚のカードと覚醒の進み具合、
        // 見出し「パッシブ」の下にアイコン・名前・解放するLv・効果の行（ステータスのタブのパッシブと同じ並び）。
        // カードを効果の文まで読める大きさにするため、この欄は縦にスクロールする（2026-11-04、ユーザーの指示）。
        private static void BuildCharacterSkills(
            RectTransform detail,
            LibraryCatalogView view,
            float x,
            float top
        )
        {
            var area = Layout.Box(detail, "CharacterSkills", x, top, DetailWidth, 1080f - top);
            view.CharacterSkills = area.gameObject;
            var viewport = Layout.Viewport(area);
            var card = DeckCardAssets.Size(UniqueDot, small: false);
            float cardsTop = 48f;
            float passivesTop = cardsTop + card.y + 44f;
            var root = Layout.Box(
                viewport,
                "Content",
                0f,
                0f,
                DetailWidth,
                passivesTop + 44f + 2f * PassiveRow + UiSpace.Edge
            );
            view.CharacterScroll = Layout.Scroll(area, viewport, root);
            view.Party = PartyAssets.CreateData();
            view.CardFrames = Workshop.Editor.WorkshopPanelAssets.DeckCardFrames();
            view.ElementIcons = Workshop.Editor.WorkshopPanelAssets.ElementIcons();

            Frame.Heading(root, "UniqueTitle", "固有スキル", 0f, 0f, 300f, 40f, 26f);
            float gap = (DetailWidth - card.x * 3f) / 2f;
            view.UniqueCards = new TrainingCardWidget[3];
            view.UniqueSteps = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                float left = i * (card.x + gap);
                view.UniqueCards[i] = DeckCardAssets.Build(
                    root,
                    "Unique" + i,
                    left,
                    cardsTop,
                    UniqueDot,
                    button: false
                );
                view.UniqueSteps[i] = Layout.Text(
                    root,
                    "UniqueSteps" + i,
                    "",
                    UiType.Caption,
                    UiPalette.GoldLight,
                    TextAlignmentOptions.Top,
                    left,
                    cardsTop + card.y + 4f,
                    card.x,
                    32f
                );
            }

            Frame.Heading(root, "PassiveTitle", "パッシブ", 0f, passivesTop, 300f, 40f, 26f);
            view.PassiveRows = Enumerable
                .Range(0, 2)
                .Select(i =>
                {
                    var row = Layout.Box(
                        root,
                        "Passive" + i,
                        0f,
                        passivesTop + 44f + i * PassiveRow,
                        DetailWidth,
                        PassiveRow
                    );
                    var widget = new TrainingSkillWidget();
                    var icon = Layout.Box(row, "Icon", 4f, 4f, PassiveIcon, PassiveIcon);
                    widget.Icon = icon.gameObject.AddComponent<RawImage>();
                    widget.Icon.raycastTarget = false;
                    const float textX = PassiveIcon + 20f;
                    const float whenWidth = 200f;
                    widget.Name = Layout.Text(
                        row,
                        "Name",
                        "",
                        UiType.Body,
                        UiPalette.TextMain,
                        TextAlignmentOptions.TopLeft,
                        textX,
                        0f,
                        DetailWidth - textX - whenWidth,
                        40f
                    );
                    Shrink(widget.Name, 22);
                    widget.When = Layout.Text(
                        row,
                        "When",
                        "",
                        UiType.Caption,
                        UiPalette.Gold,
                        TextAlignmentOptions.TopRight,
                        DetailWidth - whenWidth,
                        4f,
                        whenWidth,
                        32f
                    );
                    widget.Description = Layout.Text(
                        row,
                        "Description",
                        "",
                        UiType.Caption,
                        UiPalette.TextSub,
                        TextAlignmentOptions.TopLeft,
                        textX,
                        42f,
                        DetailWidth - textX,
                        PassiveRow - 46f
                    );
                    widget.Description.textWrappingMode = TextWrappingModes.Normal;
                    Shrink(widget.Description, 18);
                    return widget;
                })
                .ToArray();
            area.gameObject.SetActive(false);
        }

        private static void BuildDetail(RectTransform layer, LibraryCatalogView view)
        {
            var detail = Layout.Box(layer, "Detail", 0f, 0f, Layout.Design.x, Layout.Design.y);
            view.Detail = detail.gameObject;
            // 詳しい面の順（doc/rules/ui-design.md の装備の見せ方）：絵、番号、名前、☆、種類、説明、属性。
            // 図鑑の物は品質を持たないため、品質の行は置かない。その下に分類ごとの欄（レシピ・倒した数など）。
            float x = DetailLeft;
            float y = ListTop + 8f;
            var box = Layout.Box(
                detail,
                "ArtBox",
                x + (DetailWidth - DetailArtBox) / 2f,
                y,
                DetailArtBox,
                DetailArtBox
            );
            view.DetailArt = Art(box);
            y += DetailArtBox + 12f;
            view.DetailNumber = Centered(
                detail,
                "Number",
                UiType.Caption,
                UiPalette.Gold,
                x,
                y,
                32f
            );
            y += 32f;
            view.DetailName = Centered(
                detail,
                "Name",
                UiType.Heading,
                UiPalette.TextMain,
                x,
                y,
                60f
            );
            Shrink(view.DetailName, 28);
            y += 60f;
            view.DetailRarity = Centered(detail, "Rarity", UiType.Body, UiPalette.Gold, x, y, 44f);
            y += 44f;
            view.DetailKind = Centered(
                detail,
                "Kind",
                UiType.Caption,
                UiPalette.TextSub,
                x,
                y,
                36f
            );
            view.DetailKind.richText = true;
            y += 40f;
            view.DetailBody = Layout.Text(
                detail,
                "Body",
                "",
                UiType.Small,
                UiPalette.TextMain,
                TextAlignmentOptions.Top,
                x,
                y,
                DetailWidth,
                100f
            );
            view.DetailBody.richText = true;
            view.DetailBody.textWrappingMode = TextWrappingModes.Normal;
            Shrink(view.DetailBody, 20);
            y += 104f;
            var elements = Layout.Box(
                detail,
                "Elements",
                x,
                y,
                DetailWidth,
                ElementIconRowAssets.DetailSize
            );
            view.DetailElements = ElementIconRowAssets.Add(
                elements,
                TextAnchor.MiddleCenter,
                ElementIconRowAssets.DetailSize
            );
            y += ElementIconRowAssets.DetailSize + 8f;
            view.DetailExtra = Layout.Text(
                detail,
                "Extra",
                "",
                UiType.Caption,
                UiPalette.TextMain,
                TextAlignmentOptions.Top,
                x,
                y,
                DetailWidth,
                140f
            );
            view.DetailExtra.richText = true;
            view.DetailExtra.textWrappingMode = TextWrappingModes.Normal;
            BuildRecipe(detail, view, x, y);
            if (view.Kind == LibraryKind.Character)
                BuildCharacterSkills(
                    detail,
                    view,
                    x,
                    ListTop + 8f + DetailArtBox + 12f + 32f + 60f + 8f
                );

            // 入手先：金の「入手先」と、押すとそのダンジョンへ入るかを確かめるダンジョン名の札。
            // 合成のレシピを出す物では、実行時にレシピの下へ動かす。
            var sources = Layout.Row(
                detail,
                "Sources",
                x,
                y,
                DetailWidth,
                SourcesTouch,
                16f,
                TextAnchor.MiddleCenter
            );
            Layout.Fit(
                Label(
                    sources,
                    "SourcesLabel",
                    "入手先",
                    UiType.Caption,
                    UiPalette.Gold,
                    TextAlignmentOptions.Midline
                ),
                0
            );
            var (place, placeName, _) = ActionButtonAssets.InLayout(
                sources,
                "Place",
                "",
                ButtonRole.Secondary,
                SourcePlaceWidth,
                SourcesTouch,
                UiType.Small
            );
            Shrink(placeName, 20);
            // 札は細く描き、押せる範囲（見えない板）だけを上下に広げる。
            const float inset = (SourcesTouch - SourcesHeight) / 2f;
            foreach (RectTransform part in place.transform)
            {
                part.offsetMin += new Vector2(0f, inset);
                part.offsetMax -= new Vector2(0f, inset);
            }
            view.Sources = sources;
            view.SourcePlace = place;
            view.SourcePlaceName = placeName;
            sources.gameObject.SetActive(false);
            view.SourcesTop = y - (SourcesTouch - SourcesHeight) / 2f;
            view.SourcesUnderRecipe = view.SourcesTop + RecipeHeight + SourcesGap;

            // 敵の開示した弱点。倒した数の下に、「弱点」と属性のアイコン（12×12を4倍）。
            var row = Layout.Row(
                detail,
                "Weakness",
                x,
                y + 48f,
                DetailWidth,
                56f,
                12f,
                TextAnchor.MiddleCenter
            );
            Layout.Fit(
                Label(
                    row,
                    "WeaknessLabel",
                    "弱点",
                    26,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Midline
                ),
                0
            );
            view.DetailIcons = Enumerable
                .Range(0, 3)
                .Select(i => Layout.RowIcon(row, "Element" + i, 48f))
                .ToArray();
            view.WeaknessNote = Label(
                row,
                "WeaknessNote",
                "",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.Midline
            );
            Layout.Fit(view.WeaknessNote, 0);
            view.WeaknessRow = row.gameObject;
            row.gameObject.SetActive(false);
            detail.gameObject.SetActive(false);
        }

        // 詳しい面の中央にそろえた1行。
        private static TMP_Text Centered(
            RectTransform detail,
            string name,
            float size,
            Color color,
            float x,
            float y,
            float height
        ) =>
            Layout.Text(
                detail,
                name,
                "",
                size,
                color,
                TextAlignmentOptions.Center,
                x,
                y,
                DetailWidth,
                height
            );

        // 合成のレシピ：見出しと、材料の札（絵と要る数、下に名前）を横に並べる。武器・防具・アクセサリー・スキルの
        // 説明の下に出し、キャラ・敵の「倒した数」などの欄と同じ場所を使う（その分類では空いている）。
        private const int RecipeSlots = 4;
        private const float RecipeTile = 120f;
        private const float RecipeArt = 64f;

        // 合成のレシピの欄の高さ（見出しと材料の札）と、その下に置く入手先の行の高さ（押せる札の高さ）。
        private const float RecipeHeight =
            56f + RecipeTile + Workshop.Editor.WorkshopPanelAssets.TileNameHeight + 4f;
        private const float SourcesHeight = 64f;

        // 札の押せる範囲の高さ（doc/rules/ui-design.md のタップ領域）。
        private const float SourcesTouch = 128f;

        // ダンジョン名の札の幅。いちばん長い「グラシエラ氷窟の最奥」が収まる幅。
        private const float SourcePlaceWidth = 400f;

        // 材料の名前と入手先の行がくっつかないよう、その間を空ける。
        private const float SourcesGap = 12f;

        private static void BuildRecipe(
            RectTransform detail,
            LibraryCatalogView view,
            float x,
            float y
        )
        {
            var group = Rect("Recipe", detail);
            Stretch(group);
            Frame.Heading(group, "Title", "合成のレシピ", x, y, DetailWidth, 44f, 30f);
            float gap = (DetailWidth - RecipeTile * RecipeSlots) / (RecipeSlots - 1);
            for (int i = 0; i < RecipeSlots; i++)
            {
                var tile = Workshop.Editor.WorkshopPanelAssets.MaterialTile(
                    group,
                    "Material" + i,
                    x + i * (RecipeTile + gap),
                    y + 56f,
                    RecipeTile,
                    RecipeArt
                );
                tile.Card.Interactable = true;
                view.RecipeRows.Add(tile);
            }
            group.gameObject.SetActive(false);
            view.Recipe = group.gameObject;
        }

        // --- The daily steps -------------------------------------------------------------

        private const float StepRowHeight = 96f;
        private const float StepsWidth = 1920f - UiSpace.Edge * 2f;

        private static GameObject BuildStepsPanel(RectTransform safe, GuideMenuView guide)
        {
            var (view, panel) = GuidePanelTemplate.FullList<LibraryStepsView>(
                safe,
                "LibrarySteps",
                LibraryKinds.Label(LibraryKind.Steps)
            );
            view.Guide = guide;
            view.Count = panel.Count;
            var rows = panel.Content;
            view.Rows = rows;
            view.List = panel.Scroll;

            var row = Layout.ListItem(rows, "Template", StepRowHeight);
            GlassCardAssets.Plaque(row, pattern: false);
            var template = new LibraryStepRow { Root = row.gameObject };
            template.Day = Layout.Text(
                row,
                "Day",
                "",
                36,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                40f,
                0f,
                320f,
                StepRowHeight
            );
            template.Steps = Layout.Text(
                row,
                "Steps",
                "",
                40,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineRight,
                StepsWidth / 2f - 300f,
                0f,
                300f,
                StepRowHeight
            );
            template.Steps.richText = true;
            template.Act = Layout.Text(
                row,
                "Act",
                "",
                40,
                UiPalette.Gold,
                TextAlignmentOptions.MidlineRight,
                StepsWidth - 340f,
                0f,
                300f,
                StepRowHeight
            );
            template.Act.richText = true;
            view.Template = template;
            LibraryStepsView.Paint(template, LibraryMemorySource.Mock().Steps[0]);

            view.Status = panel.Status;
            return view.gameObject;
        }
    }
}
