using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// How the exploration map looks down on the route (doc/features/stage-progression.md): the
    /// room the party is in near the bottom, the deepest room far away at the top, the rows
    /// narrowing with distance. The camera follows the party across near the bottom and keeps the
    /// deepest room in the middle far away. Positions are in map dots (800x360, y down), drawn
    /// three pixels a dot: 2400x1080, wide enough for a 20:9 screen; a 16:9 screen shows the
    /// middle 640 dots, and the map is never scaled, so every dot stays three pixels.
    /// The camera can stand anywhere on the route, so it can follow the party along a road.
    /// </summary>
    public sealed class ExplorationMapProjection
    {
        public const int Width = 800;
        public const int Height = 360;
        public const int Dot = 3;

        // 遠近の強さと、手前と奥の行の高さ（ドット）。
        private const float Depth = 2.6f;
        private const float NearY = 330f;
        public const float FarY = 96f;
        public const float NearWidth = 640f;

        // カメラの手前の端を、パーティの立つ所からどれだけ後ろに置くか（道の長さの割合）。
        private const float Behind = 0.05f;

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

        // 道の t の所の奥行き（パーティの少し後ろが1、最奥の間が 1+Depth）。
        public float DepthOf(float t) => Mathf.Max(0.55f, 1f + (t - near) / (1f - near) * Depth);

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
            return float.IsInfinity(z) ? z : near + (z - 1f) / Depth * (1f - near);
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
            float rowsPerT = (NearY - FarY) * Depth / ((1f - near) * (1f - farInverse)) / (z * z);
            return NearWidth / z * rowsPerT;
        }

        /// <summary>How big things are drawn at a row: 1 at the party's row, smaller far away.</summary>
        public static float ScaleAt(float y) => 0.32f + 0.68f * (y - FarY) / (NearY - FarY);

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
