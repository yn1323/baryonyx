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
        private const float NearWidth = 640f;

        private readonly float near;
        private readonly float acrossNear;
        private readonly float farInverse = 1f / (1f + Depth);

        public ExplorationMapProjection(AdventureRouteMap map, string currentId)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            var here = map.PointOf(currentId) ?? map.PointOf(AdventureRouteMap.EntranceId);
            near = here.T - 0.05f;
            acrossNear = here.S;
        }

        public AdventureRouteMap Map { get; }

        /// <summary>Where a point of the route lies on the map, in dots (y down).</summary>
        public Vector2 ToDots(float t, float s)
        {
            float z = Mathf.Max(0.55f, 1f + (t - near) / (1f - near) * Depth);
            float f = (1f / z - farInverse) / (1f - farInverse);
            float across = 0.5f + (acrossNear - 0.5f) * Mathf.Clamp01(f);
            return new Vector2(
                Width / 2f + (s - across) * NearWidth / z,
                FarY + (NearY - FarY) * f
            );
        }

        public Vector2 ToDots(AdventureRoutePoint point) => ToDots(point.T, point.S);

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
        // 見せない（通った部屋、霧の奥）。
        Hidden,

        // 小さい印だけ。
        Mark,

        // 部屋の種類のアイコン。
        Detail,

        // 霧の奥でも分かる光（強い魔物の気配）。
        Glow,
    }

    /// <summary>
    /// How much of each room the party can see (doc/features/stage-progression.md): the rooms a
    /// few floors ahead in detail, a little further as small marks, and beyond that only the fog,
    /// through which strong monsters on the roads still ahead glow. The deepest room is always
    /// seen. ACT bonuses may see further later (<see cref="DetailFloors"/>, <see cref="MarkFloors"/>).
    /// </summary>
    public sealed class ExplorationMapSight
    {
        public const int DefaultDetailFloors = 2;
        public const int DefaultMarkFloors = 4;

        private readonly AdventureRouteMap map;
        private readonly AdventureRoom here;

        public ExplorationMapSight(
            AdventureRun run,
            int detailFloors = DefaultDetailFloors,
            int markFloors = DefaultMarkFloors
        )
        {
            if (run?.Map == null)
                throw new ArgumentException("The run has no route.", nameof(run));
            map = run.Map;
            here = run.Room ?? map.Find(AdventureRouteMap.EntranceId);
            DetailFloors = detailFloors;
            MarkFloors = Math.Max(detailFloors, markFloors);
            var exits = here != null ? here.Next : Array.Empty<string>();
            Reachable = map.Reachable(exits);
            Passed = new HashSet<string>(run.Route);
        }

        public int DetailFloors { get; }
        public int MarkFloors { get; }

        // 今いる部屋の先で、道をたどって行ける部屋。
        public HashSet<string> Reachable { get; }

        // この冒険で通った部屋（今いる部屋を含む）。
        public HashSet<string> Passed { get; }

        public ExplorationRoomSight Of(AdventureRoom room)
        {
            if (room == null || here == null)
                return ExplorationRoomSight.Hidden;
            if (room.Kind == AdventureRoomKind.Boss)
                return ExplorationRoomSight.Detail;
            int ahead = room.Floor - here.Floor;
            bool reachable = Reachable.Contains(room.Id);
            if (ahead <= 0)
                return ExplorationRoomSight.Hidden;
            if (ahead <= DetailFloors)
                return reachable ? ExplorationRoomSight.Detail : ExplorationRoomSight.Mark;
            if (ahead <= MarkFloors)
                return reachable ? ExplorationRoomSight.Mark : ExplorationRoomSight.Hidden;
            return reachable && room.Kind == AdventureRoomKind.Elite
                ? ExplorationRoomSight.Glow
                : ExplorationRoomSight.Hidden;
        }
    }
}
