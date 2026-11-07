using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.WorldMap;
using UnityEngine;

namespace Baryonyx.Library
{
    /// <summary>
    /// Where a kind can be found (doc/features/library.md の入手先): the open destinations whose
    /// drop list (<see cref="AdventureDrops.Pools"/>) has it, in the destinations' order. A
    /// destination is open once enough towns are reached (<see cref="DungeonUnlocks.IsOpen"/>, the
    /// same as the travel office and the server); the ones still closed are left out.
    /// </summary>
    public static class LibraryDropPlaces
    {
        /// <summary>One destination the kind falls in.</summary>
        public readonly struct Place
        {
            public Place(AdventureArea area, bool bossOnly)
            {
                Destination = area.Id;
                Name = area.Name;
                BossOnly = bossOnly;
            }

            public string Destination { get; }
            public string Name { get; }

            // 最奥の間でだけ落ちる物（主の物）。
            public bool BossOnly { get; }

            public string Text => BossOnly ? $"{Name}の最奥" : Name;
        }

        /// <summary>
        /// The open destinations where <paramref name="id"/> of <paramref name="kind"/> falls;
        /// empty for a kind that falls in none of them.
        /// </summary>
        public static IReadOnlyList<Place> Of(
            LibraryKind kind,
            string id,
            WorldMapProgress highway
        )
        {
            if (string.IsNullOrEmpty(id))
                return Array.Empty<Place>();
            var places = new List<Place>();
            foreach (var area in AdventureCatalog.Areas)
            {
                if (
                    !IsOpen(area, highway)
                    || !AdventureDrops.Pools.TryGetValue(area.Id, out var pool)
                )
                    continue;
                foreach (var entry in pool)
                    if (
                        entry.Category != AdventureDropCategory.Material
                        && KindOf(entry.Category) == kind
                        && entry.Id == id
                    )
                    {
                        places.Add(new Place(area, entry.BossOnly));
                        break;
                    }
            }
            return places;
        }

        /// <summary>Whether the destination can be set out for (DungeonUnlocks.IsOpen).</summary>
        public static bool IsOpen(AdventureArea area, WorldMapProgress highway) =>
            DungeonUnlocks.IsOpen(area, highway);

        /// <summary>
        /// Reads again what decides the open destinations: the highway's progress and the squares
        /// paid for on the world map. Null when the progress cannot be read, so the caller keeps
        /// what it had.
        /// </summary>
        public static async Task<WorldMapProgress> LoadReachAsync(CancellationToken token)
        {
            try
            {
                var atlas = WorldAtlasSession.LoadAsync(token);
                var progress = await WorldMapSession.LoadAsync(token);
                try
                {
                    await atlas;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // 今の世界地図が読めなくても、街道の進み具合だけで出す。
                    Debug.LogWarning("世界地図の開けたマスを取得できませんでした。" + exception.Message);
                }
                return progress;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("街道の進み具合を取得できませんでした。" + exception.Message);
                return null;
            }
        }

        /// <summary>The library's kind of a drop. The enhancement materials are not in the library.</summary>
        public static LibraryKind KindOf(AdventureDropCategory category) =>
            category switch
            {
                AdventureDropCategory.Item => LibraryKind.Item,
                AdventureDropCategory.Accessory => LibraryKind.Accessory,
                AdventureDropCategory.Weapon => LibraryKind.Weapon,
                AdventureDropCategory.Armor => LibraryKind.Armor,
                AdventureDropCategory.Skill => LibraryKind.Skill,
                _ => throw new ArgumentOutOfRangeException(nameof(category)),
            };
    }
}
