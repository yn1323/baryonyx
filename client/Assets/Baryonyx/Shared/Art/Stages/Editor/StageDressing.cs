using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static Baryonyx.Vfx.Hd2d.Editor.Hd2dStageKit;

namespace Baryonyx.Stages.Editor
{
    /// <summary>
    /// The small things that make a stage look lived in, as HD-2D's maps are drawn as densely as
    /// pixel art allows: props round the characters, tufts of grass bedding the scenery into the
    /// ground, litter laid over the repeating tiles, and soft shade under everything that stands.
    /// Each stage's builder in <see cref="StageSetAssets"/> calls its dressing last. The props keep
    /// clear of where the screen's characters stand (their feet as the scenes' UI places them).
    /// </summary>
    internal static class StageDressing
    {
        // Above the highest floor of the stage, under the characters' contact shadows (1 cm).
        internal const float DecalHeight = 0.004f;
        internal const float ShadeHeight = 0.006f;

        // Merged boards and decals share a mesh for every few metres of ground, so each part
        // still picks the lights near it (the phone lights an object with four lights at most).
        internal const float Chunk = 6f;

        /// <summary>A prop's picture as a lit cut-out with its relief, and its shape.</summary>
        internal readonly struct Prop
        {
            public readonly Material Material;
            private readonly float aspect;

            public Prop(Material material, float aspect)
            {
                Material = material;
                this.aspect = aspect;
            }

            public Vector2 Wide(float width) => new(width, width * aspect);

            public Vector2 Tall(float height) => new(height / aspect, height);
        }

        internal static Prop Load(
            string directory,
            string name,
            float relief = 2f,
            Color? tint = null
        )
        {
            var texture = PixelTexture($"{directory}/{name}.aseprite", false);
            var material = LitMaterial(
                $"{directory}/{name}.mat",
                texture,
                tint ?? Color.white,
                true,
                null,
                ReliefMap(texture, relief, 1.2f)
            );
            return new Prop(material, texture.height / (float)texture.width);
        }

        // Grass and leaves let the light from behind through and sway at their tips.
        internal static Prop LoadFoliage(string directory, string name, float sway)
        {
            var texture = PixelTexture($"{directory}/{name}.aseprite", false);
            var material = FoliageMaterial($"{directory}/{name}.mat", texture, Color.white, sway);
            return new Prop(material, texture.height / (float)texture.width);
        }

        /// <summary>
        /// Home's camp: crates, the packs and a barrel by the tent, firewood behind the log, a
        /// stump and mossy rocks at the edges of the clearing, ferns at the trees' feet, a ring of
        /// stones under the campfire, fallen leaves over the ground and tufts of grass all round.
        /// </summary>
        public static void ForestGlade(Transform root, MeshStore store, string directory)
        {
            var dressing = Group(root, "Dressing", Vector3.zero).transform;
            var crateTexture = PixelTexture(directory + "/GladeCrate.aseprite", false);
            var crate = LitMaterial(
                directory + "/GladeCrate.mat",
                crateTexture,
                // The weathered boards are painted dark; lifted, they read as pale wood in the sun.
                new Color(1.75f, 1.58f, 1.35f),
                false,
                null,
                ReliefMap(crateTexture, 2.5f)
            );
            // The packs stand in the big tree's shade; lifted a little, the sacking still reads.
            var supplies = Load(directory, "GladeSupplies", 2f, new Color(1.3f, 1.24f, 1.12f));
            var firewood = Load(directory, "GladeFirewood");
            var ferns = LoadFoliage(directory, "GladeFerns", 0.03f);
            var tuft = LoadFoliage(directory, "GladeTuft", 0.035f);
            var rocks = Load(directory, "GladeRocks", 2.5f);
            var stump = Load(directory, "GladeStump");
            var litter = Load(directory, "GladeLitter", 1.5f);
            var firePit = Load(directory, "GladeFirePit", 2.5f);

            // The party's feet and the fire (Home's layout), and the trodden clearing round them.
            var party = new[]
            {
                new Vector2(-1.71f, 5.71f),
                new Vector2(-0.96f, 6.3f),
                new Vector2(0f, 6.16f),
                new Vector2(0.96f, 6.3f),
                new Vector2(1.71f, 5.71f),
            };
            static bool InClearing(Vector2 p) => Sq(p.x / 4.4f) + Sq((p.y - 8.2f) / 4f) < 1f;
            static bool InView(Vector2 p) => Mathf.Abs(p.x) < 0.54f * (p.y + 3.1f) + 0.4f;

            // Supplies beside the tent: three crates in front of its side, the top one set askew,
            // out in the sun before the shade of the big tree on the left, and the packs and a
            // barrel in front of the back log (to the right, so their shadow misses the crates).
            (Vector3 foot, float size, float yaw)[] crates =
            {
                (new Vector3(-1.75f, 0f, 11.15f), 0.6f, 14f),
                (new Vector3(-1.1f, 0f, 11.35f), 0.5f, -8f),
                (new Vector3(-1.72f, 0.6f, 11.17f), 0.42f, 32f),
            };
            for (int i = 0; i < crates.Length; i++)
            {
                var (foot, size, yaw) = crates[i];
                var box = Box(
                    dressing,
                    store,
                    "Crate" + i,
                    crate,
                    Vector3.zero,
                    Vector3.one * size,
                    size
                );
                box.transform.SetPositionAndRotation(foot, Quaternion.Euler(0f, yaw, 0f));
            }
            Flat(
                dressing,
                store,
                "Supplies",
                supplies.Material,
                new Vector3(0.35f, 0f, 12.3f),
                supplies.Wide(2f),
                Vector3.back
            );
            Flat(
                dressing,
                store,
                "Firewood",
                firewood.Material,
                new Vector3(4.4f, 0f, 13.4f),
                firewood.Wide(2f),
                Vector3.back
            );
            Flat(
                dressing,
                store,
                "Stump",
                stump.Material,
                new Vector3(3.7f, -0.02f, 8.4f),
                stump.Wide(0.95f),
                Vector3.back
            );
            Flat(
                dressing,
                store,
                "Rocks",
                rocks.Material,
                new Vector3(-3.6f, -0.02f, 8.1f),
                rocks.Wide(1.5f),
                Vector3.back
            );
            Flat(
                dressing,
                store,
                "RocksSmall",
                rocks.Material,
                new Vector3(4.5f, -0.02f, 6.5f),
                rocks.Wide(0.95f),
                Vector3.back,
                true,
                true
            );
            (string name, float x, float z, float width, bool mirror)[] fernSpots =
            {
                ("FernsTreeLeft", -4.6f, 10.6f, 1.8f, false),
                ("FernsTent", -5.3f, 12.9f, 1.6f, true),
                ("FernsLog", 1.4f, 13.3f, 1.6f, false),
                ("FernsTreeRight", 5.6f, 11f, 1.7f, true),
            };
            foreach (var (name, x, z, width, mirror) in fernSpots)
                Flat(
                    dressing,
                    store,
                    name,
                    ferns.Material,
                    new Vector3(x, -0.03f, z),
                    ferns.Wide(width),
                    Vector3.back,
                    true,
                    mirror
                );

            // The ring of sooty stones the fire burns in, and leaves fallen round the clearing.
            var decals = new List<Piece>
            {
                new(new Vector3(0f, DecalHeight, 6.16f), new Vector2(1f, 1f * 42f / 45f)),
            };
            var leafSizes = new System.Random(311);
            foreach (
                var p in Scatter(
                    310,
                    12,
                    new Rect(-7f, 3.8f, 14f, 11f),
                    2f,
                    p => InView(p) && !NearAny(p, party, 1.4f)
                )
            )
            {
                float size = Range(leafSizes, 1.1f, 1.8f);
                decals.Add(
                    new Piece(
                        new Vector3(p.x, DecalHeight, p.y),
                        new Vector2(size, size * 61f / 72f),
                        leafSizes.Next(2) == 0,
                        leafSizes.Next(4)
                    )
                );
            }
            Decals(dressing, store, "FirePit", firePit.Material, decals.GetRange(0, 1), Chunk);
            Decals(
                dressing,
                store,
                "Litter",
                litter.Material,
                decals.GetRange(1, decals.Count - 1),
                Chunk
            );

            // Tufts over the grass round the clearing, and a few bedding each prop and tree.
            var tufts = ScatterTufts(
                320,
                54,
                new Rect(-9f, 4.6f, 18f, 14f),
                0.85f,
                p => !InClearing(p) && InView(p),
                -0.04f,
                0.5f,
                0.85f
            );
            AddBedding(
                tufts,
                330,
                -0.04f,
                new[]
                {
                    new Vector3(-3.4f, 4.2f, 12.3f), // the tent
                    new Vector3(3.3f, 3f, 10.5f), // the log by the fire
                    new Vector3(0.6f, 2.6f, 13.5f), // the log behind
                    new Vector3(-1.45f, 1.3f, 11.15f), // the crates
                    new Vector3(0.35f, 2f, 12.2f), // the packs
                    new Vector3(4.4f, 2f, 13.3f), // the firewood
                    new Vector3(3.7f, 1f, 8.3f), // the stump
                    new Vector3(-3.6f, 1.5f, 8f), // the rocks
                    new Vector3(-6.8f, 2.4f, 8.4f), // the trees
                    new Vector3(7.2f, 2.4f, 9.4f),
                    new Vector3(-9.5f, 2.4f, 14.9f),
                    new Vector3(9.2f, 2.4f, 16.4f),
                },
                0.45f,
                0.75f
            );
            // The shade goes under the boards standing so far, before the tufts are merged in;
            // not under the far trees, where the lens blurs the ground but not the shade.
            ShadeStanding(root, dressing, store, 13f);
            Boards(dressing, store, "Tufts", tuft.Material, tufts, Chunk);
            Shades(
                dressing,
                store,
                "CrateShade",
                LoadShade(),
                new[]
                {
                    new Piece(new Vector3(-1.45f, ShadeHeight, 11.25f), new Vector2(1.7f, 1f)),
                },
                Chunk
            );
        }

        /// <summary>
        /// Top's mountain gate: weathered knights flanking the gate on its platform, tattered
        /// banners lit by the near torches, broken walls, rubble and old standing stones in the grass either
        /// side of the path, moss creeping over its edges and tufts of grass all along it.
        /// </summary>
        public static void StarlitGate(
            Transform root,
            MeshStore store,
            string directory,
            float platformHeight
        )
        {
            var dressing = Group(root, "Dressing", Vector3.zero).transform;
            var statue = Load(directory, "StarlitStatue", 2.5f);
            var banner = Load(directory, "StarlitBanner", 1.5f);
            var wall = Load(directory, "StarlitWall", 2.5f);
            var rubble = Load(directory, "StarlitRubble", 2.5f);
            var stele = Load(directory, "StarlitStele", 2.5f);
            var tuft = LoadFoliage(directory, "StarlitTuft", 0.035f);
            var moss = Load(directory, "StarlitMoss", 1.5f);

            foreach (float side in new[] { -1f, 1f })
            {
                var label = side < 0f ? "Left" : "Right";
                Flat(
                    dressing,
                    store,
                    "Statue" + label,
                    statue.Material,
                    new Vector3(side * 4.3f, platformHeight, 16.8f),
                    statue.Tall(2.6f),
                    Vector3.back,
                    true,
                    side > 0f
                );
                Flat(
                    dressing,
                    store,
                    "Banner" + label,
                    banner.Material,
                    new Vector3(side * 3.05f, -0.02f, 4.5f),
                    banner.Tall(2.2f),
                    Vector3.back,
                    true,
                    side > 0f
                );
            }
            Flat(
                dressing,
                store,
                "WallLeft",
                wall.Material,
                new Vector3(-4.6f, -0.03f, 8f),
                wall.Wide(3.4f),
                Vector3.back
            );
            Flat(
                dressing,
                store,
                "WallRight",
                wall.Material,
                new Vector3(5f, -0.03f, 10.6f),
                wall.Wide(3.2f),
                Vector3.back,
                true,
                true
            );
            Flat(
                dressing,
                store,
                "RubbleRight",
                rubble.Material,
                new Vector3(5.8f, -0.03f, 6.6f),
                rubble.Wide(2.8f),
                Vector3.back
            );
            Flat(
                dressing,
                store,
                "RubbleLeft",
                rubble.Material,
                new Vector3(-6.6f, -0.03f, 10.4f),
                rubble.Wide(2.2f),
                Vector3.back,
                true,
                true
            );
            Flat(
                dressing,
                store,
                "Steles",
                stele.Material,
                new Vector3(-3.3f, -0.03f, 11.6f),
                stele.Wide(1.6f),
                Vector3.back
            );

            // Moss creeping over both edges of the path, a little onto it, and patches in the grass.
            var decals = new List<Piece>();
            var mossSizes = new System.Random(411);
            for (float z = 1.4f; z < 13f; z += 1.45f)
                foreach (float side in new[] { -1f, 1f })
                {
                    float size = Range(mossSizes, 0.9f, 1.4f);
                    decals.Add(
                        new Piece(
                            new Vector3(
                                side * (1.7f + Range(mossSizes, -0.25f, 0.2f)),
                                DecalHeight,
                                z + Range(mossSizes, -0.5f, 0.5f)
                            ),
                            new Vector2(size, size),
                            mossSizes.Next(2) == 0,
                            mossSizes.Next(4)
                        )
                    );
                }
            decals.Add(new Piece(new Vector3(0.45f, DecalHeight, 4.8f), new Vector2(0.8f, 0.8f)));
            decals.Add(
                new Piece(new Vector3(-0.5f, DecalHeight, 10.9f), new Vector2(0.9f, 0.9f), true, 1)
            );
            foreach (
                var p in Scatter(
                    410,
                    10,
                    new Rect(-9f, 2f, 18f, 11f),
                    2.2f,
                    p => Mathf.Abs(p.x) > 2.8f && Mathf.Abs(p.x) < 0.7f * (p.y + 4f)
                )
            )
            {
                float size = Range(mossSizes, 1.2f, 1.8f);
                decals.Add(
                    new Piece(
                        new Vector3(p.x, DecalHeight, p.y),
                        new Vector2(size, size),
                        mossSizes.Next(2) == 0,
                        mossSizes.Next(4)
                    )
                );
            }
            Decals(dressing, store, "Moss", moss.Material, decals, Chunk);

            // Tufts thick along the path's edges and scattered over the grass, clear of the
            // torch posts and the steps.
            var posts = new[]
            {
                new Vector2(-2.2f, 3.8f),
                new Vector2(2.2f, 3.8f),
                new Vector2(-2.2f, 9.2f),
                new Vector2(2.2f, 9.2f),
            };
            bool OffPath(Vector2 p) =>
                !NearAny(p, posts, 0.35f) && !(p.y > 13f && Mathf.Abs(p.x) < 2.9f);
            var tufts = ScatterTufts(
                420,
                34,
                new Rect(-2.7f, 1.4f, 5.4f, 12.2f),
                0.6f,
                p => Mathf.Abs(p.x) > 1.55f && OffPath(p),
                -0.04f,
                0.4f,
                0.65f
            );
            tufts.AddRange(
                ScatterTufts(
                    430,
                    46,
                    new Rect(-10f, 1.4f, 20f, 12.6f),
                    1f,
                    p =>
                        Mathf.Abs(p.x) > 2.6f
                        && Mathf.Abs(p.x) < 0.7f * (p.y + 4f) + 0.5f
                        && OffPath(p),
                    -0.04f,
                    0.5f,
                    0.9f
                )
            );
            AddBedding(
                tufts,
                440,
                -0.04f,
                new[]
                {
                    new Vector3(-4.6f, 3.4f, 7.9f), // the walls
                    new Vector3(5f, 3.2f, 10.5f),
                    new Vector3(5.8f, 2.8f, 6.5f), // the rubble
                    new Vector3(-6.6f, 2.2f, 10.3f),
                    new Vector3(-3.3f, 1.6f, 11.5f), // the steles
                    new Vector3(-3.05f, 0.6f, 4.4f), // the banners
                    new Vector3(3.05f, 0.6f, 4.4f),
                },
                0.45f,
                0.75f
            );
            ShadeStanding(root, dressing, store, 20f, platformHeight);
            Boards(dressing, store, "Tufts", tuft.Material, tufts, Chunk);
        }

        /// <summary>
        /// The battle's stone circle: a beast's bones, the rusted blades of an old fight and a
        /// cairn on the far rim, broken blocks and a dry bush on the near rim, sand drifted over
        /// the stones, and dry grass round the circle and in its cracks. Everything keeps clear of
        /// both parties so they and the effects stay easy to read.
        /// </summary>
        public static void DuskHighland(Transform root, MeshStore store, string directory)
        {
            var dressing = Group(root, "Dressing", Vector3.zero).transform;
            var bones = Load(directory, "DuskBones", 2.5f);
            var weapons = Load(directory, "DuskWeapons");
            var cairn = Load(directory, "DuskCairn", 2.5f);
            var rubble = Load(directory, "DuskRubble", 2.5f);
            var shrub = LoadFoliage(directory, "DuskShrub", 0.025f);
            var tuft = LoadFoliage(directory, "DuskTuft", 0.04f);
            // The drifted sand is greyed towards the weathered stone it lies on; bright orange
            // patches in the open middle read as stains, so it lies only against the rim.
            var sand = Load(directory, "DuskSand", 1.5f, new Color(0.72f, 0.64f, 0.62f));

            // Both parties' feet (BattleInspect's layout) and the circle (centre 0, 9; radius 8.5).
            var feet = new[]
            {
                new Vector2(-2.83f, 4.21f),
                new Vector2(-5.31f, 6.29f),
                new Vector2(-3.89f, 9.62f),
                new Vector2(-7.87f, 14.21f),
                new Vector2(2.85f, 5.04f),
                new Vector2(6.58f, 7.62f),
                new Vector2(4.09f, 11.58f),
            };
            var centre = new Vector2(0f, 9f);
            static bool InView(Vector2 p) => Mathf.Abs(p.x) < 0.5f * (p.y + 8f) + 0.3f;

            Flat(
                dressing,
                store,
                "Bones",
                bones.Material,
                new Vector3(-0.6f, -0.02f, 17f),
                bones.Wide(2.2f),
                Vector3.back
            );
            Flat(
                dressing,
                store,
                "Weapons",
                weapons.Material,
                new Vector3(-2.6f, -0.02f, 16.6f),
                weapons.Wide(1.3f),
                Vector3.back
            );
            Flat(
                dressing,
                store,
                "Cairn",
                cairn.Material,
                new Vector3(1.8f, -0.02f, 17f),
                cairn.Tall(1.2f),
                Vector3.back
            );
            Flat(
                dressing,
                store,
                "RubbleRight",
                rubble.Material,
                new Vector3(5.4f, -0.02f, 4f),
                rubble.Wide(1.4f),
                Vector3.back
            );
            Flat(
                dressing,
                store,
                "RubbleLeft",
                rubble.Material,
                new Vector3(-5.6f, -0.02f, 4f),
                rubble.Wide(1.3f),
                Vector3.back,
                true,
                true
            );
            Flat(
                dressing,
                store,
                "Shrub",
                shrub.Material,
                new Vector3(4.7f, -0.02f, 3.3f),
                shrub.Wide(1.1f),
                Vector3.back
            );

            // Sand drifted against the rim of the circle and round the standing stones, where the
            // wind drops it.
            var drifts = new List<Piece>();
            var sandSizes = new System.Random(511);
            foreach (
                var p in Scatter(
                    510,
                    10,
                    new Rect(-11f, 1f, 22f, 16.4f),
                    2.3f,
                    p =>
                        InView(p)
                        && (p - centre).magnitude > 5.5f
                        && (p - centre).magnitude < 10f
                        && !NearAny(p, feet, 1.9f)
                )
            )
            {
                float size = Range(sandSizes, 1.3f, 2.1f);
                drifts.Add(
                    new Piece(
                        new Vector3(p.x, DecalHeight, p.y),
                        new Vector2(size, size),
                        sandSizes.Next(2) == 0,
                        sandSizes.Next(4)
                    )
                );
            }
            Decals(dressing, store, "Sand", sand.Material, drifts, Chunk);

            // Dry grass round the circle, and short tufts in the cracks between its stones.
            var tufts = ScatterTufts(
                520,
                34,
                new Rect(-12f, 8f, 24f, 9.6f),
                0.9f,
                p => InView(p) && (p - centre).magnitude > 8.3f && !NearAny(p, feet, 1.3f),
                -0.05f,
                0.5f,
                0.9f
            );
            tufts.AddRange(
                ScatterTufts(
                    530,
                    30,
                    new Rect(-9f, 1f, 18f, 16.5f),
                    1f,
                    p =>
                        InView(p)
                        && (p - centre).magnitude > 2.5f
                        && (p - centre).magnitude < 8.4f
                        && !NearAny(p, feet, 1.4f),
                    -0.01f,
                    0.35f,
                    0.55f
                )
            );
            AddBedding(
                tufts,
                540,
                -0.04f,
                new[]
                {
                    new Vector3(-0.6f, 2.2f, 16.9f), // the bones
                    new Vector3(1.8f, 0.8f, 16.9f), // the cairn
                    new Vector3(5.4f, 1.4f, 3.9f), // the rubble
                    new Vector3(-5.6f, 1.3f, 3.9f),
                    new Vector3(-4.6f, 1f, 16.7f), // the standing stones at the back
                    new Vector3(4.9f, 1f, 16.9f),
                },
                0.4f,
                0.7f
            );
            ShadeStanding(root, dressing, store, 18f);
            Boards(dressing, store, "Tufts", tuft.Material, tufts, Chunk);
        }

        private static List<Piece> ScatterTufts(
            int seed,
            int count,
            Rect area,
            float spacing,
            Func<Vector2, bool> allowed,
            float height,
            float minWidth,
            float maxWidth
        )
        {
            var sizes = new System.Random(seed + 1);
            var tufts = new List<Piece>();
            foreach (var p in Scatter(seed, count, area, spacing, allowed))
                tufts.Add(
                    new Piece(
                        new Vector3(p.x, height, p.y),
                        TuftSize(Range(sizes, minWidth, maxWidth)),
                        sizes.Next(2) == 0
                    )
                );
            return tufts;
        }

        /// <summary>
        /// Tufts at the foot of a prop (x, width, z of each <paramref name="props"/>): at both
        /// ends and one in front, so it grows out of the grass instead of standing on it.
        /// </summary>
        private static void AddBedding(
            List<Piece> tufts,
            int seed,
            float height,
            IEnumerable<Vector3> props,
            float minWidth,
            float maxWidth
        )
        {
            var random = new System.Random(seed);
            foreach (var prop in props)
            {
                float half = prop.y * 0.5f;
                foreach (
                    var (dx, dz) in new[]
                    {
                        (-half + Range(random, -0.1f, 0.25f), -0.12f),
                        (half + Range(random, -0.25f, 0.1f), -0.1f),
                        (Range(random, -half * 0.6f, half * 0.6f), -0.22f),
                    }
                )
                    tufts.Add(
                        new Piece(
                            new Vector3(prop.x + dx, height, prop.z + dz),
                            TuftSize(Range(random, minWidth, maxWidth)),
                            random.Next(2) == 0
                        )
                    );
            }
        }

        // Every tuft picture is about twice as wide as tall.
        private static Vector2 TuftSize(float width) => new(width, width * 0.5f);

        /// <summary>
        /// Lays a soft shade under every cut-out board of the stage that stands on the ground
        /// (or on the raised <paramref name="platformHeight"/>) and casts a shadow: the scenery
        /// from the stage's builder and the props of its dressing alike, up to
        /// <paramref name="farthest"/> m back. The shades are see-through, drawn after the lens
        /// blurs the stage, so farther away they would stay sharp lines on the blurred ground.
        /// </summary>
        internal static void ShadeStanding(
            Transform root,
            Transform dressing,
            MeshStore store,
            float farthest,
            float platformHeight = float.NaN
        )
        {
            var shades = new List<Piece>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var material = renderer.sharedMaterial;
                if (
                    mesh == null
                    || mesh.vertexCount != 4
                    || renderer.shadowCastingMode == ShadowCastingMode.Off
                    || material == null
                    || material.renderQueue != (int)RenderQueue.AlphaTest
                    || Mathf.Abs(mesh.normals[0].y) > 0.1f
                )
                    continue;
                var bounds = renderer.bounds;
                if (bounds.center.z > farthest)
                    continue;
                float ground =
                    !float.IsNaN(platformHeight) && bounds.min.y > platformHeight - 0.1f
                        ? platformHeight
                        : 0f;
                if (bounds.min.y > ground + 0.1f)
                    continue;
                float width = bounds.size.x * 0.75f;
                shades.Add(
                    new Piece(
                        new Vector3(bounds.center.x, ground + ShadeHeight, bounds.center.z),
                        new Vector2(width, Mathf.Clamp(width * 0.35f, 0.5f, 1.6f))
                    )
                );
            }
            Shades(dressing, store, "Shades", LoadShade(), shades, Chunk);
        }

        private static Material LoadShade() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(PropShadeMaterialPath);

        internal static bool NearAny(Vector2 point, IEnumerable<Vector2> spots, float distance)
        {
            foreach (var spot in spots)
                if ((spot - point).sqrMagnitude < distance * distance)
                    return true;
            return false;
        }

        internal static float Range(System.Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);

        private static float Sq(float value) => value * value;
    }
}
