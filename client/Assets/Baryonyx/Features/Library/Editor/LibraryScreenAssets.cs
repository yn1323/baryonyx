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
using Baryonyx.Training.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Cards;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
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
        private const float Split = 1180f;
        private const float Gap = 24f;
        private const float HeadingHeight = 76f;
        private const float CardGap = 16f;
        private const int Columns = 4;
        private const float ListTop = Frame.HeaderHeight + HeadingHeight;
        private static readonly float CardWidth =
            (Split - Frame.Margin - Gap - CardGap * (Columns - 1)) / Columns;
        private const float CardHeight = 300f;
        private const float TileArtBox = 176f;
        private const float DetailLeft = Split + Gap + 8f;
        private const float DetailWidth = 1920f - Frame.Margin - DetailLeft;
        private const float DetailArtBox = 300f;

        private static readonly Color NewColor = new(1f, 0.843f, 0.4f);

        private const string GearLabel = "装備";

        // 装備のパネルの見出しに並べるタブ。定義が1つもない分類は、準備中のタブにする。
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
                }
            );
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
            // 装備のパネルの見出しのタブで切り替える。防具は武器のパネルから開く隠れた項目。
            var gear = Kind(LibraryKind.Weapon, "手に入れた武器・防具・アクセサリー", IconGearPath);
            gear.Label = GearLabel;
            var armor = Kind(LibraryKind.Armor, "", null);
            armor.Hidden = true;
            d.Items = new[]
            {
                Kind(
                    LibraryKind.Character,
                    "仲間にした冒険者と、その固有スキル",
                    IconCharacterPath
                ),
                gear,
                Kind(LibraryKind.Skill, "手に入れたスキル", IconSkillPath),
                Kind(LibraryKind.Item, "手に入れた道具と素材", IconItemPath),
                Kind(LibraryKind.Enemy, "戦った敵と、開示した弱点", IconEnemyPath),
                Kind(LibraryKind.Steps, "日ごとに歩いた記録", IconStepsPath),
                armor,
            };
        }

        private static GuideMenuItem Kind(LibraryKind kind, string caption, string iconPath)
        {
            var item = Item(
                LibraryKinds.Label(kind),
                caption,
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
                    var passives = training.Find(member.Id)?.Passives;
                    return new LibraryEntryData
                    {
                        Kind = LibraryKind.Character,
                        Id = member.Id,
                        Number = index + 1,
                        Name = member.Name,
                        KindLine = "冒険者",
                        Description =
                            passives != null && passives.Length > 0
                                ? "パッシブ　" + string.Join("・", passives.Select(p => p.Name))
                                : "",
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
                        KindLine = $"{CardText.KindLine(item.Card)}　コスト {item.Cost}",
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
                        KindLine = $"{CardText.KindLine(card)}　コスト {card.Cost}",
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
                        KindLine = "敵",
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
            var (view, layer) = Frame.FramedPanel<LibraryCatalogView>(safe, "Library" + kind);
            view.Guide = guide;
            view.Kind = kind;
            view.Data = data;
            view.Training = training;
            view.TileArtBox = TileArtBox;
            view.DetailArtBox = DetailArtBox;

            Frame.SplitPanes(layer, Split, "ListPane", "DetailPane");
            if (Array.IndexOf(GearTabs, kind) >= 0)
            {
                view.KindTabs = Frame.Tabs(
                    layer,
                    "KindTabs",
                    GearTabs.Select(LibraryKinds.Label).ToArray(),
                    Frame.Margin,
                    Frame.HeaderHeight + 8f,
                    240f,
                    HeadingHeight - 16f,
                    Split - Frame.Margin - Gap - 320f,
                    Array.IndexOf(GearTabs, LibraryKind.Accessory)
                );
                view.KindTabKeys = GearTabs.Select(LibraryKinds.GuideItemKey).ToArray();
                for (int i = 0; i < view.KindTabs.Length; i++)
                    view.KindTabs[i].Chosen = GearTabs[i] == kind;
            }
            else
                Frame.Heading(
                    layer,
                    "ListTitle",
                    LibraryKinds.Label(kind),
                    Frame.Margin,
                    Frame.HeaderHeight,
                    600f,
                    HeadingHeight
                );
            view.Count = Layout.Text(
                layer,
                "Count",
                "",
                34,
                UiPalette.GoldLight,
                TextAlignmentOptions.MidlineRight,
                Split - Gap - 300f,
                Frame.HeaderHeight,
                300f,
                HeadingHeight
            );

            var entries = data.Entries.Where(entry => entry.Kind == kind).ToArray();
            var list = Layout.Box(layer, "List", 0f, ListTop, Split, Layout.Design.y - ListTop);
            var viewport = Layout.Viewport(list);
            var cards = Layout.Grid(
                viewport,
                "Cards",
                new Vector2(CardWidth, CardHeight),
                CardGap,
                new RectOffset(
                    (int)Frame.Margin,
                    (int)Gap,
                    (int)CardGap,
                    (int)(CardGap + Frame.Margin)
                ),
                Columns
            );
            view.Tiles = entries.Select(entry => BuildTile(cards, entry)).ToArray();
            view.List = Layout.Scroll(list, viewport, cards);
            view.Status = Layout.Text(
                layer,
                "Status",
                entries.Length == 0 ? LibraryCatalogView.PreparingText : "",
                36,
                UiPalette.TextSub,
                TextAlignmentOptions.Center,
                Frame.Margin,
                ListTop + CardGap,
                Split - Frame.Margin - Gap,
                CardHeight
            );

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

        private static LibraryTileWidget BuildTile(RectTransform content, LibraryEntryData entry)
        {
            var rect = Rect("Entry_" + entry.Id, content);
            var (card, inside) = GlassCardAssets.Build(rect);
            var widget = new LibraryTileWidget { Id = entry.Id, Card = card };

            var box = Layout.Box(
                inside,
                "ArtBox",
                (CardWidth - TileArtBox) / 2f,
                20f,
                TileArtBox,
                TileArtBox
            );
            widget.Art = Art(box);
            widget.Number = Layout.Text(
                inside,
                "Number",
                LibraryCatalogView.NumberText(entry.Number),
                26,
                UiPalette.Gold,
                TextAlignmentOptions.MidlineLeft,
                20f,
                CardHeight - 96f,
                CardWidth - 40f,
                36f
            );
            widget.Name = Layout.Text(
                inside,
                "Name",
                entry.Name,
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                20f,
                CardHeight - 60f,
                CardWidth - 40f,
                44f
            );
            Shrink(widget.Name, 20);
            var badge = Layout.Text(
                inside,
                "New",
                "NEW",
                26,
                NewColor,
                TextAlignmentOptions.MidlineRight,
                CardWidth - 120f,
                CardHeight - 96f,
                100f,
                36f
            );
            badge.gameObject.SetActive(false);
            widget.New = badge.gameObject;
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

        private static void BuildDetail(RectTransform layer, LibraryCatalogView view)
        {
            var detail = Layout.Box(layer, "Detail", 0f, 0f, Layout.Design.x, Layout.Design.y);
            view.Detail = detail.gameObject;
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
            y += DetailArtBox + 16f;
            view.DetailNumber = Layout.Text(
                detail,
                "Number",
                "",
                30,
                UiPalette.Gold,
                TextAlignmentOptions.MidlineLeft,
                x,
                y,
                DetailWidth,
                40f
            );
            y += 40f;
            view.DetailName = Layout.Text(
                detail,
                "Name",
                "",
                48,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                x,
                y,
                DetailWidth - 180f,
                60f
            );
            Shrink(view.DetailName, 28);
            view.DetailRarity = Layout.Text(
                detail,
                "Rarity",
                "",
                36,
                UiPalette.Gold,
                TextAlignmentOptions.MidlineRight,
                x + DetailWidth - 180f,
                y,
                180f,
                60f
            );
            y += 64f;
            view.DetailKind = Layout.Text(
                detail,
                "Kind",
                "",
                28,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineLeft,
                x,
                y,
                DetailWidth,
                40f
            );
            view.DetailKind.richText = true;
            y += 48f;
            view.DetailBody = Layout.Text(
                detail,
                "Body",
                "",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.TopLeft,
                x,
                y,
                DetailWidth,
                120f
            );
            view.DetailBody.richText = true;
            view.DetailBody.textWrappingMode = TextWrappingModes.Normal;
            y += 124f;
            view.DetailExtra = Layout.Text(
                detail,
                "Extra",
                "",
                26,
                UiPalette.TextMain,
                TextAlignmentOptions.TopLeft,
                x,
                y,
                DetailWidth,
                140f
            );
            view.DetailExtra.richText = true;
            view.DetailExtra.textWrappingMode = TextWrappingModes.Normal;

            // 敵の開示した弱点。倒した数の下に、「弱点」と属性のアイコン（12×12を4倍）。
            var row = Layout.Row(detail, "Weakness", x, y + 48f, DetailWidth, 56f, 12f);
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

        // --- The daily steps -------------------------------------------------------------

        private const float StepRowHeight = 96f;
        private const float StepsLeft = 520f;
        private const float StepsWidth = 1920f - StepsLeft * 2f;

        private static GameObject BuildStepsPanel(RectTransform safe, GuideMenuView guide)
        {
            var (view, layer) = Frame.FramedPanel<LibraryStepsView>(safe, "LibrarySteps");
            view.Guide = guide;
            Frame.Pane(
                layer,
                "Pane",
                0f,
                Layout.Design.x,
                Frame.HeaderHeight,
                Frame.PaneLight,
                true,
                true
            );
            Frame.Heading(
                layer,
                "Title",
                LibraryKinds.Label(LibraryKind.Steps),
                StepsLeft,
                Frame.HeaderHeight,
                600f,
                HeadingHeight
            );
            view.Count = Layout.Text(
                layer,
                "Count",
                "",
                34,
                UiPalette.GoldLight,
                TextAlignmentOptions.MidlineRight,
                StepsLeft + StepsWidth - 300f,
                Frame.HeaderHeight,
                300f,
                HeadingHeight
            );

            var list = Layout.Box(
                layer,
                "List",
                StepsLeft,
                ListTop,
                StepsWidth,
                Layout.Design.y - ListTop
            );
            var viewport = Layout.Viewport(list);
            var rows = Layout.Column(
                viewport,
                "Rows",
                8f,
                new RectOffset(0, 0, 8, (int)Frame.Margin)
            );
            view.Rows = rows;
            view.List = Layout.Scroll(list, viewport, rows);

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
                360f,
                0f,
                260f,
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

            view.Status = Layout.Text(
                layer,
                "Status",
                "",
                36,
                UiPalette.TextSub,
                TextAlignmentOptions.Center,
                StepsLeft,
                ListTop + 16f,
                StepsWidth,
                StepRowHeight
            );
            return view.gameObject;
        }
    }
}
