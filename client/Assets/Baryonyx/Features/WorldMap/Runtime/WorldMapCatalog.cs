using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.WorldMap
{
    /// <summary>The kinds of a highway square (doc/features/stage-progression.md).</summary>
    public enum WorldNodeKind
    {
        Empty,
        Battle,
        Treasure,
        Story,
        Boss,
        Town,

        // 入れ子の地図の試作（WorldAtlasLayout）で使う。世界地図の地域と、地域どうしの入口。
        Region,
        Gate,
    }

    /// <summary>
    /// The highway's world map (街道, doc/features/stage-progression.md#街とダンジョン仮), drawn as a
    /// grid of characters like a sugoroku board (<see cref="WorldMapGrid"/>). The web has two
    /// rings around the first town, roads out to the other towns behind a boss, and dead ends to
    /// wander into. All names, levels and squares are provisional.
    /// </summary>
    public static class WorldMapCatalog
    {
        // 背景の絵の大きさ（画素）と、方眼の1マスの大きさ・左上のマスの中心（画素）。
        public static readonly Vector2 PictureSize = new(1536f, 1024f);
        public const float Cell = 48f;
        public static readonly Vector2 Origin = new(20f, 14f);

        public const string Start = "porta";

        // 大文字は街、小文字はマスの種類（o 何もない・b 戦闘・t 宝箱・s 物語・x 関所のボス）。
        public static readonly string[] Rows =
        {
            "................................",
            ".....o-x-A-b-o..................",
            ".....C.......F........s.........",
            ".............x........|.........",
            "......o-o-booos-b-o-x-K-b-oo....",
            "......|.to-N......|........|....",
            "......b....|......b........x....",
            "...R..|..o-ooo-o-t|........|....",
            "...ox-o-Bo..b..|..o-o-o-b-xG....",
            "......|..o..|..b..|.............",
            "......s..ob-P-bo-bo.............",
            "......|..b..|..o..|.............",
            "......b..|..b..|..o-o...........",
            "......|..o-oob-o..|.|...........",
            ".....xo.....|..|..b.t...........",
            ".....M|.....o..t..|.............",
            "......o-b-s-o-S-b-o-o-x.........",
            "..........|...|.......D.........",
            "..........s...o-t...............",
            "................................",
            "................................",
        };

        public static readonly IReadOnlyDictionary<char, (string Id, string Name)> Towns =
            new Dictionary<char, (string, string)>
            {
                ['P'] = ("porta", "門前町ポルタ"),
                ['B'] = ("brezza", "宿場町ブリーザ"),
                ['R'] = ("portoria", "港町ポルトリア"),
                ['N'] = ("pino", "宿場町ピーノ"),
                ['F'] = ("fredda", "氷室村フレッダ"),
                ['A'] = ("alba", "宿場町アルバ"),
                ['C'] = ("cima", "山小屋チーマ"),
                ['M'] = ("palude", "渡し町パルーデ"),
                ['S'] = ("sabbia", "宿場町サッビア"),
                ['D'] = ("duna", "砂の町ドゥーナ"),
                ['K'] = ("campanella", "鐘の町カンパネラ"),
                ['G'] = ("forgia", "鍛冶町フォルジャ"),
            };

        internal static readonly IReadOnlyDictionary<char, WorldNodeKind> Kinds = new Dictionary<
            char,
            WorldNodeKind
        >
        {
            ['o'] = WorldNodeKind.Empty,
            ['b'] = WorldNodeKind.Battle,
            ['t'] = WorldNodeKind.Treasure,
            ['s'] = WorldNodeKind.Story,
            ['x'] = WorldNodeKind.Boss,
        };

        // 区間の推奨Lvの札（方眼の位置）。道の脇に置く。
        public static readonly IReadOnlyList<(int Level, Vector2 Cell)> Levels = new[]
        {
            (12, new Vector2(7f, 7.4f)),
            (14, new Vector2(5f, 7.4f)),
            (16, new Vector2(11.7f, 6f)),
            (18, new Vector2(13.7f, 3f)),
            (38, new Vector2(12f, 0.45f)),
            (40, new Vector2(6f, 0.45f)),
            (22, new Vector2(4.3f, 14.5f)),
            (24, new Vector2(13f, 16.6f)),
            (26, new Vector2(21f, 15.4f)),
            (30, new Vector2(19f, 3.4f)),
            (35, new Vector2(25f, 7.4f)),
        };

        // 試作の進み具合：ポルタから、西のブリーザと北のピーノまで進んだところ。
        public static readonly IReadOnlyList<string> MockCleared = new[]
        {
            "porta",
            "n10-10",
            "n9-10",
            "n9-9",
            "n9-8",
            "brezza",
            "n6-8",
            "n12-8",
            "n12-7",
            "n11-7",
            "pino",
            "n11-4",
        };

        private static readonly WorldMapGrid Grid = new(Rows, Cell, Origin, "n", Kinds);

        public static Vector2 PointOf(Vector2 cell) => Grid.PointOf(cell);

        public static string IdAt(int column, int row) => Grid.IdAt(column, row);

        public static WorldMapGraph Build() => Grid.Build(Levels);
    }
}
