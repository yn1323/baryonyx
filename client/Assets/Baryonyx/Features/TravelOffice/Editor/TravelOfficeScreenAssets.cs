using System.Collections.Generic;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Adventure.Editor;
using Baryonyx.Combat.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.Equipment;
using Baryonyx.Inventory.Editor;
using Baryonyx.Library;
using Baryonyx.Library.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using Baryonyx.UI.GuideMenu.Editor;
using Baryonyx.WorldMap;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Frame = Baryonyx.UI.GuideMenu.Editor.GuideFrameAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.TravelOffice.Editor
{
    /// <summary>
    /// The travel office (旅の案内所), opened from the world map as its list of dungeons: the
    /// destinations across the screen without a guide, each row
    /// with a treasure button that shows how many of the destination's treasures were found and
    /// opens them in a window in the middle of the screen (<see cref="TravelTreasureView"/>).
    /// Place names are provisional mock values, not settled world settings.
    /// </summary>
    public static class TravelOfficeScreenAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/TravelOffice";
        public const string DefinitionPath = Folder + "/Data/TravelOfficeGuideMenu.asset";
        public const string PrefabPath = Folder + "/UI/TravelOfficeScreen.prefab";
        public const string BackgroundPath = Folder + "/UI/Art/TravelOfficeBackground.png";

        // 最奥の主だけが落とす物の印。12×12の王冠を4倍で出す（UIアイコンの24×24からの例外）。
        public const string IconBossOnlyPath = Folder + "/UI/Art/IconBossOnly.aseprite";

        // 行の右端のお宝のボタン（宝箱の絵と「9/23」）。設計座標。押せる範囲は行の高さいっぱいの
        // 幅176pxで、その内側にすりガラスの札を描く。推奨Lvと説明文は、ボタンの左で切る。
        private const float TreasureWidth = 176f;
        private const float TreasureRight = 8f;
        private const float TreasureInset = 12f;
        private const float TreasureIconSize = 24f * 2f;

        // 画面の中央の窓。左に分類の名前、右にお宝の札を並べ、下に選んだお宝の名前と説明を出す。
        // 左にクリア回数の報酬の列、右にお宝の段を並べるため、窓を画面の幅いっぱいに近くする（2026-11-01、T2の案）。
        private const float WindowWidth = UiSpace.WindowLarge;
        private const float WindowHeight = 1000f;
        private const float Pad = Frame.WindowPad;
        private const float HeadHeight = Frame.WindowHeadHeight;
        private const float InfoHeight = 136f;
        private const float SectionLabelWidth = 160f;
        private const int Columns = 7;

        // 左の列：「クリア N回」と、縦の線でつないだ5つの印と報酬の札。
        private const float RewardsWidth = 560f;
        private const float RewardsTop = HeadHeight + 16f;
        private const float RewardRowsTop = HeadHeight + 96f;
        private const float RewardRowHeight = 112f;
        private const float RewardRowGap = 12f;
        private const float RewardMarkX = 24f;
        private const float RewardMarkSize = 30f;
        private const float RewardCardX = 64f;
        private const float RewardArtSize = 80f;
        private static readonly Color RewardMarkDim = new(0.2f, 0.24f, 0.33f, 1f);

        // 右のお宝の段の左端（左の列と縦の区切り線のあと）。
        private const float TreasuresLeft = Pad + RewardsWidth + 48f;
        private static readonly Vector2 Tile = new(Layout.HitSize, 144f);
        private const float TileGap = 12f;

        // 絵は32×32ドットを3倍で出す。主の物の王冠は12×12ドットを4倍で出す。
        private const float TileArtBox = 96f;
        private const float BossIconSize = 12f * DotScale;

        // まだ見つけていないスキルの「？」。影と同じく目立たせず、札の地より少し明るくする。
        private static readonly Color UnknownColor = ItemTileAssets.UnknownColor;

        [MenuItem("Baryonyx/Travel Office/Create Screen Assets")]
        public static void CreateAssets() =>
            CreateScreen(
                DefinitionPath,
                PrefabPath,
                null,
                BackgroundPath,
                Fill,
                overlay: BuildTreasures
            );

        private static void Fill(GuideMenuDefinition d)
        {
            d.Title = "ダンジョン一覧";
            // 世界地図のダンジョン一覧として開くため、案内人は置かない（2026-10-24）。
            d.NoGuide = true;
            d.Layout = GuideMenuLayout.Destinations;
            d.DepartLabel = "出発";
            // 説明は仮の値。推奨Lvは、行き先の敵のLv（AdventureCatalog.Areas）と同じにする。
            // 行き先は街ごとに、街の近い順に並べる。街道でその街に着くまで、行は出さない
            // （doc/features/stage-progression.md の街とダンジョン、server/src/features/adventure/catalog.ts）。
            // 名前は探索・ホームと同じ冒険の定義から取る。中身をまだ作っていないダンジョンは、名前と推奨Lvと
            // 街の名前だけを出し、出発すると準備中と知らせる。最後の未踏の地は、行では地名を「？？？」に伏せる。
            var details = new Dictionary<string, string>
            {
                [AdventureCatalog.ForestRuins] = "苔の下で、千年前の門番がいまも見張りを続けている",
                ["port-portoria"] = "潮と魚と酒の匂い。船乗りの噂話は半分ほど本当",
                ["glaciera-cave"] = "吐く息まで凍る洞窟。奥で何かが寝返りを打つ",
                ["hush-mire"] = "鳥も虫も鳴かない沼。泡の音だけが返事をする",
                ["almaja-tomb"] = "砂に沈んだ王が、いまも財宝の数を数えている",
                ["moonshade-graveyard"] = "月の夜だけ、墓守の灯りがひとりでに灯る",
                ["volgan-volcano"] = "山が怒るたびに、ふもとの鍛冶屋が忙しくなる",
                ["neve-peaks"] = "白い峰の上を、霧をまとった影が渡っていく",
            };
            string TownName(string town) =>
                WorldMapCatalog.Towns.Values.FirstOrDefault(entry => entry.Id == town).Name ?? "";
            GuideDestination Made(string id, string town)
            {
                var area = AdventureCatalog.Area(id);
                return Destination(
                    area.Name,
                    details.TryGetValue(id, out var detail) ? detail : "",
                    area.Level,
                    id: id,
                    town: town
                );
            }
            GuideDestination Planned(string name, int level, string town) =>
                Destination(name, TownName(town), level, town: town);
            d.Destinations = new[]
            {
                // 門前町ポルタ（始まりの街。いつも着いている）
                Made(AdventureCatalog.ForestRuins, "porta"),
                Planned("受け石の高原", 10, "porta"),
                Planned("苔むす水路", 11, "porta"),
                Planned("どんぐりの森", 12, "porta"),
                // 宿場町ブリーザ
                Planned("風鳴りの丘", 12, "brezza"),
                Planned("古井戸の坑道", 13, "brezza"),
                Planned("風車小屋の地下", 13, "brezza"),
                Planned("案山子の畑", 14, "brezza"),
                // 港町ポルトリア
                Made("port-portoria", "portoria"),
                Planned("沈船の入り江", 15, "portoria"),
                Planned("学院の地下書庫", 15, "portoria"),
                Planned("灯台の螺旋", 16, "portoria"),
                Planned("沖の小島", 17, "portoria"),
                // 宿場町ピーノ
                Planned("松明の坑道", 16, "pino"),
                Planned("針葉の霧森", 17, "pino"),
                Planned("樵の廃村", 17, "pino"),
                Planned("狼の岩棚", 18, "pino"),
                // 氷室村フレッダ
                Made("glaciera-cave", "fredda"),
                Planned("水晶の裂け目", 19, "fredda"),
                Planned("凍った滝裏", 20, "fredda"),
                Planned("氷室の蔵", 20, "fredda"),
                Planned("白狐の雪原", 21, "fredda"),
                // 渡し町パルーデ
                Made("hush-mire", "palude"),
                Planned("沈んだ礼拝堂", 23, "palude"),
                Planned("蛍の入り江", 23, "palude"),
                Planned("茸の朽ち森", 24, "palude"),
                // 宿場町サッビア
                Planned("涸れ谷", 24, "sabbia"),
                Planned("蜃気楼の市跡", 25, "sabbia"),
                Planned("砂嵐の石柱", 25, "sabbia"),
                Planned("隊商の墓場", 26, "sabbia"),
                // 砂の町ドゥーナ
                Made("almaja-tomb", "duna"),
                Planned("砂鮫の巣", 27, "duna"),
                Planned("星見の神殿", 27, "duna"),
                Planned("砂時計の塔", 28, "duna"),
                Planned("地底湖の宮", 29, "duna"),
                // 鐘の町カンパネラ
                Made("moonshade-graveyard", "campanella"),
                Planned("鳴らずの鐘楼", 31, "campanella"),
                Planned("燭台の修道院", 31, "campanella"),
                Planned("鴉の森", 32, "campanella"),
                Planned("魔導書の書庫", 33, "campanella"),
                // 鍛冶町フォルジャ
                Made("volgan-volcano", "forgia"),
                Planned("溶岩の坑道", 36, "forgia"),
                Planned("帝国の廃砦", 36, "forgia"),
                Planned("黒曜の採石場", 37, "forgia"),
                Planned("火喰い鳥の巣", 38, "forgia"),
                // 宿場町アルバ
                Planned("霜枯れの森", 38, "alba"),
                Planned("凍て川の洞", 39, "alba"),
                Planned("吹雪の峠", 39, "alba"),
                Planned("雪埋もれの砦", 40, "alba"),
                // 山小屋チーマ
                Made("neve-peaks", "cima"),
                Planned("マンモスの墓場", 41, "cima"),
                Planned("霧竜の巣", 42, "cima"),
                Planned("稜線の古塔", 43, "cima"),
                Planned("天空の祭壇", 45, "cima"),
            }.Append(Destination("天空の迷い路", "雲の上に、だれも帰ってこない道があるという", 50, locked: true)).ToArray();
        }

        // --- The treasures ---------------------------------------------------------------

        private static GameObject BuildTreasures(RectTransform root, GuideMenuView guide)
        {
            var data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            // 仮の記録で描き、Prefabを開くと停止中でも見つけたお宝と影が読めるようにする。
            var mock = LibraryMemorySource.Mock();

            var treasures = Rect("Treasures", root);
            Stretch(treasures);
            var view = treasures.gameObject.AddComponent<TravelTreasureView>();
            view.Guide = guide;
            view.Data = data;
            view.TileArtBox = TileArtBox;

            var destinations = guide
                .Definition.Destinations.Select((destination, index) => (destination, index))
                .Where(pair =>
                    !pair.destination.Locked
                    && AdventureDrops.Pools.ContainsKey(pair.destination.Id)
                )
                .ToArray();
            view.Rows = destinations
                .Select(pair =>
                    BuildRowButton(guide.DestinationRows[pair.index], pair.destination.Id, mock)
                )
                .ToArray();

            BuildWindow(treasures, view, destinations.Select(pair => pair.destination), mock);
            // 受け取ったクリアの報酬を押すと出す小さな窓。お宝の窓の上に重ねる。
            view.DetailWindow = ItemDetailAssets.Window(treasures, "ItemDetail");
            // 停止中の表示で開いた形を確かめられるよう、最初の武器を品質Bの見本として焼いておく。
            var sample = data != null
                ? data.Entries.FirstOrDefault(entry => entry.Kind == LibraryKind.Weapon)
                : null;
            if (sample != null)
                view.DetailWindow.Face.Show(
                    LibraryFaces.Of(
                        sample,
                        Baryonyx.Inventory.ItemQuality.B,
                        EquipmentCatalog.Find(sample.Id) is { } gear
                            ? Baryonyx.Inventory.Quality.Middle(Baryonyx.Inventory.ItemQuality.B, gear.MaxMultiplier)
                            : 1f
                    )
                );
            return treasures.gameObject;
        }

        // 行の右端に、宝箱の絵と見つけた数のボタンを置く。推奨Lvと説明文はボタンの左で切る。
        private static TravelTreasureRow BuildRowButton(
            Button row,
            string destinationId,
            LibraryRecords mock
        )
        {
            var content = (RectTransform)row.transform.Find("Body/Content");
            foreach (var name in new[] { "Badge", "Detail" })
            {
                var label = (RectTransform)content.Find(name);
                label.offsetMax = new Vector2(
                    -(TreasureRight + TreasureWidth + 8f),
                    label.offsetMax.y
                );
            }

            // 札の外側も押せるよう、押せる範囲の板にボタンを付け、札は押せない飾りにする。
            var hit = Rect("Treasure", content);
            hit.anchorMin = new Vector2(1, 0);
            hit.anchorMax = Vector2.one;
            hit.pivot = new Vector2(1, 0.5f);
            hit.offsetMin = new Vector2(-(TreasureRight + TreasureWidth), 0f);
            hit.offsetMax = new Vector2(-TreasureRight, 0f);
            AddImage(hit, Color.clear, true);
            var plate = Rect("Card", hit);
            Stretch(plate);
            plate.offsetMin = Vector2.one * TreasureInset;
            plate.offsetMax = -Vector2.one * TreasureInset;
            var inside = GlassCardAssets.Plaque(plate, pattern: false);
            var button = UiBuild.AddButton(hit, GlassCardAssets.FillOf(plate));

            var icon = Rect("Icon", inside);
            Corner(
                icon,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -10f),
                Vector2.one * TreasureIconSize
            );
            var image = icon.gameObject.AddComponent<Image>();
            image.sprite = Icon(AdventureAssets.IconTreasurePath);
            image.raycastTarget = false;
            var count = Label(
                inside,
                "Count",
                TravelTreasures.CountText(destinationId, mock),
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Band((RectTransform)count.transform, top: false, 4f, 38f, 4f, 4f);
            return new TravelTreasureRow
            {
                DestinationId = destinationId,
                Open = button,
                Count = count,
            };
        }

        // 画面全体を暗くする幕と、Safe Areaの中央の窓。窓は閉じた状態で保存する。
        private static void BuildWindow(
            RectTransform treasures,
            TravelTreasureView view,
            IEnumerable<GuideDestination> destinations,
            LibraryRecords mock
        )
        {
            var parts = Frame.ListWindow(treasures, "Window", Frame.WindowSize.Large, WindowHeight);
            var root = parts.Root;
            view.Window = root.gameObject;
            view.Veil = parts.Veil;
            view.Close = parts.Close;
            view.Title = parts.Title;
            var window = parts.Window;

            BuildRewards(window, view);
            // 見つけた数は、見出しの段の右（×の左）に置く。
            view.Count = Layout.Text(
                window,
                "Count",
                "",
                40,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineRight,
                WindowWidth - Layout.HitSize - 8f - 24f - 200f,
                12f,
                200f,
                96f
            );

            float viewportHeight = WindowHeight - HeadHeight - InfoHeight;
            var divider = Layout.Box(
                window,
                "Divider",
                TreasuresLeft - 24f,
                HeadHeight + 24f,
                2f,
                viewportHeight - 48f
            );
            AddImage(divider, Frame.LineColor, false);
            var viewport = Layout.Box(
                window,
                "Viewport",
                TreasuresLeft - Pad,
                HeadHeight,
                WindowWidth - TreasuresLeft + Pad,
                viewportHeight
            );
            Layout.Mask(viewport);
            var lists = new List<TravelTreasureList>();
            foreach (var destination in destinations)
                lists.Add(BuildList(viewport, view, destination, mock));
            view.Lists = lists.ToArray();
            view.Scroll = Layout.Scroll(
                viewport,
                viewport,
                lists.Count > 0 ? lists[0].Content : null
            );
            if (lists.Count > 0)
            {
                view.Clears.text = $"クリア {TravelTreasureView.PreviewClears}回";
                view.ShowRewards(lists[0].DestinationId, TravelTreasureView.PreviewClears);
                view.Title.text = lists[0].Title;
                view.Count.text = TravelTreasures.CountText(lists[0].DestinationId, mock);
            }

            Rule(window, "InfoLine", WindowHeight - InfoHeight);
            view.Info = Layout.Text(
                window,
                "Info",
                "",
                UiType.Body,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                Pad,
                WindowHeight - InfoHeight + 16f,
                WindowWidth - Pad * 2f - ElementIconRowAssets.Width() - 16f,
                52f
            );
            view.InfoBody = Layout.Text(
                window,
                "InfoBody",
                "",
                UiType.Caption,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineLeft,
                Pad,
                WindowHeight - InfoHeight + 72f,
                WindowWidth - Pad * 2f,
                44f
            );
            Shrink(view.InfoBody, 20);
            // ☆の色と、説明文のキーワードの色はリッチテキストで書く。
            view.Info.richText = view.InfoBody.richText = true;
            // 効果の行の右に、属性のアイコン。
            var elements = Layout.Box(
                window,
                "InfoElements",
                WindowWidth - Pad - ElementIconRowAssets.Width(),
                WindowHeight - InfoHeight + 16f + (52f - ElementIconRowAssets.Size) / 2f,
                ElementIconRowAssets.Width(),
                ElementIconRowAssets.Size
            );
            view.InfoElements = ElementIconRowAssets.Add(elements, TextAnchor.MiddleRight);

            root.gameObject.SetActive(false);
        }

        // 左の列：上に「クリア N回」、その下に1〜5回目の報酬を縦に並べ、左の縦の線の印でクリアした回数を見せる。
        private static void BuildRewards(RectTransform window, TravelTreasureView view)
        {
            view.ItemArt = ItemArtAssets.Load();
            view.Clears = Layout.Text(
                window,
                "Clears",
                "",
                44,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                Pad,
                RewardsTop,
                RewardsWidth,
                72f
            );
            var rows = new TravelClearRewardRow[AdventureClearRewards.Count];
            float centerOf(int i) =>
                RewardRowsTop + i * (RewardRowHeight + RewardRowGap) + RewardRowHeight / 2f;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = new TravelClearRewardRow();
                if (i + 1 < rows.Length)
                {
                    var link = Layout.Box(
                        window,
                        $"RewardLink{i + 1}",
                        Pad + RewardMarkX - 3f,
                        centerOf(i),
                        6f,
                        centerOf(i + 1) - centerOf(i)
                    );
                    row.Link = AddImage(link, RewardMarkDim, false);
                }
                var mark = Layout.Box(
                    window,
                    $"RewardMark{i + 1}",
                    Pad + RewardMarkX - RewardMarkSize / 2f,
                    centerOf(i) - RewardMarkSize / 2f,
                    RewardMarkSize,
                    RewardMarkSize
                );
                row.Mark = SpriteImage(mark, UiArt.CirclePath, RewardMarkDim);
                rows[i] = row;
            }
            // 線の上に印、印の右に札を重ねる（線と印を先に置く）。
            for (int i = 0; i < rows.Length; i++)
                BuildRewardCard(window, rows[i], i, centerOf(i) - RewardRowHeight / 2f);
            view.Rewards = rows;
        }

        // 報酬の札：左に絵、右に名前（数）と☆・品質、右端にルーン。受け取った回はチェック、次の回は金の縁。
        private static void BuildRewardCard(
            RectTransform window,
            TravelClearRewardRow row,
            int index,
            float top
        )
        {
            float width = RewardsWidth - RewardCardX;
            var rect = Layout.Box(
                window,
                $"Reward{index + 1}",
                Pad + RewardCardX,
                top,
                width,
                RewardRowHeight
            );
            var (card, inside) = GlassCardAssets.Build(rect, pattern: false);
            // 受け取った回だけ押せ、詳しい窓を出す（ビューが押せる回を決める）。
            card.Interactable = false;
            row.Card = card.Group;
            row.Tap = card;

            var next = Rect("Next", rect);
            Stretch(next);
            next.offsetMin = Vector2.one * -3f;
            next.offsetMax = Vector2.one * 3f;
            Sliced(next, GlassCardAssets.RimPath, GlassCardAssets.GoldColor).raycastTarget = false;
            row.Next = next.gameObject;

            var art = Layout.Box(
                inside,
                "Art",
                16f,
                (RewardRowHeight - RewardArtSize) / 2f,
                RewardArtSize,
                RewardArtSize
            );
            row.Art = art.gameObject.AddComponent<Image>();
            row.Art.preserveAspect = true;
            row.Art.raycastTarget = false;

            var unknown = Label(
                inside,
                "Unknown",
                "？",
                60,
                UnknownColor,
                TextAlignmentOptions.Center
            );
            var unknownRect = (RectTransform)unknown.transform;
            unknownRect.anchorMin = unknownRect.anchorMax = art.anchorMin;
            unknownRect.pivot = art.pivot;
            unknownRect.anchoredPosition = art.anchoredPosition;
            unknownRect.sizeDelta = art.sizeDelta;
            row.Unknown = unknown.gameObject;
            row.Unknown.SetActive(false);

            // 行の標準の形：名前のすぐ右に☆と品質・倍率、2行目に効果、右下にルーン。
            float textX = 16f + RewardArtSize + 16f;
            row.Name = Layout.Text(
                inside,
                "Name",
                "",
                UiType.Small,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                textX,
                12f,
                width - textX - 72f,
                46f
            );
            row.Name.richText = true;
            Shrink(row.Name, 20);
            row.Effect = Layout.Text(
                inside,
                "Effect",
                "",
                UiType.Tiny,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineLeft,
                textX,
                58f,
                width - textX - 200f - 8f,
                40f
            );
            row.Effect.richText = true;
            Shrink(row.Effect, 16);

            row.Runes = Frame.RuneAmount(
                inside,
                "Runes",
                width - 200f,
                58f,
                180f,
                40f,
                28f,
                TextAnchor.MiddleRight,
                32f,
                UiPalette.TextMain
            );

            var check = Layout.Box(inside, "Received", width - 52f, 10f, 36f, 33f);
            var checkArt = check.gameObject.AddComponent<RawImage>();
            checkArt.texture = BattleInspectAssets.Art("ConfirmCheck");
            checkArt.raycastTarget = false;
            row.Received = check.gameObject;
            row.Received.SetActive(false);
            row.Next.SetActive(false);
        }

        // 1つの行き先のお宝：分類ごとに、左に分類の名前、右に札の格子を並べる。落ちる物のない分類は出さない。
        private static TravelTreasureList BuildList(
            RectTransform viewport,
            TravelTreasureView view,
            GuideDestination destination,
            LibraryRecords mock
        )
        {
            var content = Layout.Column(
                viewport,
                "Treasures_" + destination.Id,
                TileGap,
                new RectOffset((int)Pad, (int)Pad, 24, 32)
            );
            var tiles = new List<TravelTreasureTile>();
            for (int section = 0; section < TravelTreasures.Sections.Count; section++)
            {
                var treasures = TravelTreasures.Of(destination.Id, section);
                if (treasures.Count == 0)
                    continue;
                int rows = (treasures.Count + Columns - 1) / Columns;
                var item = Layout.ListItem(
                    content,
                    "Section" + section,
                    rows * Tile.y + (rows - 1) * TileGap
                );
                var label = Layout.Text(
                    item,
                    "Label",
                    TravelTreasures.Sections[section].Title,
                    30,
                    UiPalette.TextSub,
                    TextAlignmentOptions.MidlineLeft,
                    0f,
                    0f,
                    SectionLabelWidth - TileGap,
                    Tile.y
                );
                Shrink(label, 22);
                var grid = Rect("Grid", item);
                Stretch(grid);
                grid.offsetMin = new Vector2(SectionLabelWidth, 0f);
                var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
                layout.cellSize = Tile;
                layout.spacing = Vector2.one * TileGap;
                layout.startAxis = GridLayoutGroup.Axis.Horizontal;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = Columns;
                foreach (var treasure in treasures)
                    tiles.Add(BuildTile(grid, view.Data, treasure, mock));
            }
            content.gameObject.SetActive(false);
            return new TravelTreasureList
            {
                DestinationId = destination.Id,
                Title = destination.Name + "のお宝",
                Content = content,
                Tiles = tiles.ToArray(),
            };
        }

        private static TravelTreasureTile BuildTile(
            RectTransform grid,
            LibraryData data,
            TravelTreasures.Treasure treasure,
            LibraryRecords mock
        )
        {
            var rect = Rect("Treasure_" + treasure.Id, grid);
            // 小さな札のため、左上の模様は付けない。
            var (face, inside) = ItemTileAssets.Build(rect);
            var art = Rect("Art", inside);
            Place(art, new Vector2(0f, 12f), Vector2.one * TileArtBox);
            var image = art.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            var stars = Label(inside, "Stars", "", 22, UiPalette.Gold, TextAlignmentOptions.Center);
            Band((RectTransform)stars.transform, top: false, 4f, 30f, 4f, 4f);
            face.Id = treasure.Id;
            face.Art = image;
            face.Stars = stars;
            GameObject unknown = null;
            if (treasure.Kind == LibraryKind.Skill)
            {
                // スキルの挿絵は四角いため、まだ見つけていない札は影の代わりに「？」を出す。
                unknown = ItemTileAssets.UnknownMark(
                    face,
                    inside,
                    new Vector2(0f, 12f),
                    TileArtBox,
                    72f
                );
            }
            if (treasure.BossOnly)
            {
                // 最奥の主だけが落とす物の印。シンプルな王冠を右上に置く。
                var boss = Rect("Boss", inside);
                Corner(boss, Vector2.one, new Vector2(-2f, -2f), Vector2.one * BossIconSize);
                var crown = boss.gameObject.AddComponent<Image>();
                crown.sprite = Icon(IconBossOnlyPath);
                crown.raycastTarget = false;
                face.Mark = boss.gameObject;
            }
            var tile = new TravelTreasureTile
            {
                Id = treasure.Id,
                Kind = treasure.Kind,
                BossOnly = treasure.BossOnly,
                Tile = face,
            };
            var entry =
                data != null
                    ? System.Array.Find(
                        data.Entries,
                        e => e.Kind == treasure.Kind && e.Id == treasure.Id
                    )
                    : null;
            bool found = TravelTreasures.IsFound(treasure, mock);
            LibraryCatalogView.PaintArt(image, entry, found, TileArtBox);
            if (unknown != null)
            {
                unknown.SetActive(!found);
                image.enabled &= found;
            }
            stars.text = entry != null ? TravelTreasureView.StarsText(entry.Rarity) : "";
            return tile;
        }

        // 窓の中の、左右に余白を空けた金の細い線。
        private static void Rule(RectTransform window, string name, float y)
        {
            var line = Layout.Box(window, name, Pad, y, WindowWidth - Pad * 2f, 2f);
            AddImage(line, Frame.LineColor, false);
        }
    }
}
