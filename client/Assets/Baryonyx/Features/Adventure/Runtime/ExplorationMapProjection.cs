using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// How the exploration map looks down on the route (doc/features/stage-progression.md): the
    /// room the party is in near the bottom and the road ahead running up the screen, the rows
    /// narrowing with distance. The screen shows a fixed length of the road, about five floors
    /// ahead of the party, so the deepest room comes into sight as the party nears it or the map
    /// is dragged. The camera follows the party across near the bottom and keeps the middle of
    /// the route in the middle far away. Positions are in map dots (800x360, y down), drawn
    /// three pixels a dot: 2400x1080, wide enough for a 20:9 screen; a 16:9 screen shows the
    /// middle 640 dots, and the map is never scaled, so every dot stays three pixels.
    /// The camera can stand anywhere on the route, so it can follow the party along a road.
    /// It looks down like a camera tilted about 41 degrees at the middle of the screen: about
    /// 52 degrees at the party and 25 at the top of the screen, the horizon well above it.
    /// </summary>
    public sealed class ExplorationMapProjection
    {
        public const int Width = 800;
        public const int Height = 360;
        public const int Dot = 3;

        // 遠近の強さ（奥の行がカメラの手前の端の何倍遠いかから1を引いた値）と、手前と奥の行の
        // 高さ（ドット）。遠近を弱めるほど、上から見下ろす角度になる。
        public const float Depth = 0.5f;
        public const float NearY = 330f;
        public const float FarY = 96f;
        public const float NearWidth = 640f;

        // カメラの手前の端から奥の行までの道の長さ（入口から最奥の間までを1とする割合）。
        // 小さいほど道が縦に長く伸び、画面に入る階が減る。
        public const float Span = 0.5f;

        // 地平線の行（ドット）。画面より上にあり、画面の中はどの行も地面になる。
        public const float HorizonY = FarY - (NearY - FarY) / Depth;

        // 地面の上の丸い物（部屋の空き地、木の影）を縦に縮める割合。見下ろす角度に合わせる。
        public const float Squash = 0.8f;

        // 奥の行で、物を描く大きさ（カメラの手前の端を1とする）。
        private const float FarScale = 1f / (1f + Depth);

        // カメラの手前の端を、パーティの立つ所からどれだけ後ろに置くか（道の長さの割合）。
        // パーティの足元が、下の案内に掛からない行（300ドット）になる。
        private const float Behind = 0.045f;

        private readonly float near;
        private readonly float acrossNear;
        private readonly float farInverse = 1f / (1f + Depth);

        public ExplorationMapProjection(AdventureRouteMap map, string currentId)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            var here = map.PointOf(currentId) ?? map.PointOf(AdventureRouteMap.EntranceId);
            near = here.T - Behind;
            acrossNear = here.S;
        }

        /// <summary>The map seen with the party standing at the route point (t, s).</summary>
        public ExplorationMapProjection(AdventureRouteMap map, float t, float s)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            near = t - Behind;
            acrossNear = s;
        }

        public AdventureRouteMap Map { get; }

        /// <summary>Where a point of the route lies on the map, in dots (y down).</summary>
        public Vector2 ToDots(float t, float s)
        {
            float z = DepthOf(t);
            float f = (1f / z - farInverse) / (1f - farInverse);
            float across = 0.5f + (acrossNear - 0.5f) * Mathf.Clamp01(f);
            return new Vector2(
                Width / 2f + (s - across) * NearWidth / z,
                FarY + (NearY - FarY) * f
            );
        }

        public Vector2 ToDots(AdventureRoutePoint point) => ToDots(point.T, point.S);

        // 道の t の所の奥行き（パーティの少し後ろが1、そこから Span 先が 1+Depth）。
        public float DepthOf(float t) => Mathf.Max(0.55f, 1f + (t - near) / Span * Depth);

        /// <summary>The row (dots, y down) the route's <paramref name="t"/> lies on.</summary>
        public float RowOf(float t) =>
            FarY + (NearY - FarY) * (1f / DepthOf(t) - farInverse) / (1f - farInverse);

        /// <summary>
        /// The depth of the ground seen at a row, or infinity above the horizon, where there is
        /// no ground. It undoes <see cref="RowOf"/> on the rows the map shows.
        /// </summary>
        public float DepthAtRow(float y)
        {
            float inverse = (y - FarY) / (NearY - FarY) * (1f - farInverse) + farInverse;
            return inverse <= 0f ? float.PositiveInfinity : Mathf.Max(0.55f, 1f / inverse);
        }

        /// <summary>The route's t seen at a row (infinity above the horizon).</summary>
        public float TAtRow(float y)
        {
            float z = DepthAtRow(y);
            return float.IsInfinity(z) ? z : near + (z - 1f) / Depth * Span;
        }

        /// <summary>The route's s seen at the middle of the map on a row.</summary>
        public float AcrossAtRow(float y) =>
            0.5f + (acrossNear - 0.5f) * Mathf.Clamp01((y - FarY) / (NearY - FarY));

        /// <summary>
        /// How many square dots a unit of route area (t times s) covers at <paramref name="t"/>:
        /// large near the party, small far away.
        /// </summary>
        public float DotsPerArea(float t)
        {
            float z = DepthOf(t);
            float rowsPerT = (NearY - FarY) * Depth / (Span * (1f - farInverse)) / (z * z);
            return NearWidth / z * rowsPerT;
        }

        /// <summary>How big things are drawn at a row: 1 at the party's row, smaller far away.</summary>
        public static float ScaleAt(float y) =>
            FarScale + (1f - FarScale) * (y - FarY) / (NearY - FarY);

        /// <summary>The design position (centred, y up) of a map dot.</summary>
        public static Vector2 ToDesign(Vector2 dots) =>
            new((dots.x - Width / 2f) * Dot, (Height / 2f - dots.y) * Dot);

        public Vector2 DesignOf(string roomId)
        {
            var point = Map.PointOf(roomId);
            return point == null ? Vector2.zero : ToDesign(ToDots(point));
        }
    }

    public enum ExplorationRoomSight
    {
        // 見せない（通った部屋、今いる部屋）。
        Hidden,

        // 小さい札とアイコン。
        Mark,

        // 大きい札とアイコン。
        Detail,

        // 遠くの、いちばん小さい札とアイコン。強い魔物は赤い光でも分かる。
        Far,
    }

    /// <summary>
    /// How the rooms ahead are shown (doc/features/stage-progression.md): every room on the
    /// route ahead, with its kind, so the party can plan its road to the chests and around the
    /// strong monsters; the fog only shows how far away they are. Rooms near the floor the
    /// camera looks from are large, further ones smaller, and the rooms the party can no longer
    /// reach are dimmed (<see cref="Reachable"/>). The deepest room is always large.
    /// </summary>
    public sealed class ExplorationMapSight
    {
        public const int DefaultDetailFloors = 2;
        public const int DefaultMarkFloors = 4;

        private readonly AdventureRoom here;

        public ExplorationMapSight(
            AdventureRun run,
            int detailFloors = DefaultDetailFloors,
            int markFloors = DefaultMarkFloors
        )
        {
            if (run?.Map == null)
                throw new ArgumentException("The run has no route.", nameof(run));
            var map = run.Map;
            here = run.Room ?? map.Find(AdventureRouteMap.EntranceId);
            DetailFloors = detailFloors;
            MarkFloors = Math.Max(detailFloors, markFloors);
            var exits = here != null ? here.Next : Array.Empty<string>();
            Reachable = map.Reachable(exits);
            Passed = new HashSet<string>(run.Route);
        }

        // カメラの立つ階から、大きく見せる階と小さく見せる階の数。
        public int DetailFloors { get; }
        public int MarkFloors { get; }

        // 今いる部屋の先で、道をたどって行ける部屋。
        public HashSet<string> Reachable { get; }

        // この冒険で通った部屋（今いる部屋を含む）。
        public HashSet<string> Passed { get; }

        /// <summary>How the room looks with the camera at the party's floor.</summary>
        public ExplorationRoomSight Of(AdventureRoom room) => Of(room, here?.Floor ?? 0);

        /// <summary>How the room looks with the camera looking from <paramref name="viewFloor"/>.</summary>
        public ExplorationRoomSight Of(AdventureRoom room, int viewFloor)
        {
            if (
                room == null
                || here == null
                || room.Floor <= here.Floor
                || Passed.Contains(room.Id)
            )
                return ExplorationRoomSight.Hidden;
            if (room.Kind == AdventureRoomKind.Boss)
                return ExplorationRoomSight.Detail;
            int ahead = room.Floor - viewFloor;
            if (ahead <= DetailFloors)
                return ExplorationRoomSight.Detail;
            return ahead <= MarkFloors ? ExplorationRoomSight.Mark : ExplorationRoomSight.Far;
        }
    }
}
