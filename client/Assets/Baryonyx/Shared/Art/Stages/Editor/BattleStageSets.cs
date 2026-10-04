using System;
using System.Collections.Generic;
using Baryonyx.Editor;
using Baryonyx.Vfx.Hd2d;
using Baryonyx.Vfx.Hd2d.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Baryonyx.Vfx.Hd2d.Editor.Hd2dStageKit;

namespace Baryonyx.Stages.Editor
{
    /// <summary>
    /// Builds the battle's backgrounds beside the dusk highland: ten 3D stages (HD-2D) the battle
    /// can be fought on, from the meadow road to the castle hall, each at its own time of day.
    /// They all share the battle's camera (<see cref="StageSetAssets.BattleView"/>) and its
    /// layout: a floor of about 17 m where both parties stand, the ground round it, a painted
    /// distance behind, large scenery framing the corners, middle-sized things along the back,
    /// the place's own story props at the back of the floor, and small things, tufts and litter
    /// round the rim, away from the characters' feet. The pictures are .aseprite made from
    /// Codex CLI's (client/ArtSource/Stages/prompts.json); what differs from stage to stage is
    /// the pictures, the light, the air and the extras (lanterns, fire, beams, snow).
    /// </summary>
    public static class BattleStageSets
    {
        private const string Root = "Assets/Baryonyx/Shared/Art/Stages";

        // Both parties' feet in the battle screen's layout, and the middle of the floor; the
        // same as the dusk highland's (StageDressing.DuskHighland).
        internal static readonly Vector2[] Feet =
        {
            new(-2.83f, 4.21f),
            new(-5.31f, 6.29f),
            new(-3.89f, 9.62f),
            new(-7.87f, 14.21f),
            new(2.85f, 5.04f),
            new(6.58f, 7.62f),
            new(4.09f, 11.58f),
        };
        private static readonly Vector2 Centre = new(0f, 9f);

        /// <summary>One battle stage: where it is kept, its air and its look.</summary>
        public sealed class Set
        {
            public readonly string Name;
            public readonly string Prefix;
            public readonly StageSetAssets.StageEnvironment Environment;
            internal readonly Action<VolumeProfile> Look;
            internal readonly Action<Builder> Build;

            internal Set(
                string name,
                string prefix,
                StageSetAssets.StageEnvironment environment,
                Action<VolumeProfile> look,
                Action<Builder> build
            )
            {
                Name = name;
                Prefix = prefix;
                Environment = environment;
                Look = look;
                Build = build;
            }

            public string Directory => Root + "/" + Name;
            public string PrefabPath => Directory + "/" + Name + "Stage.prefab";
            public string LookPath => Directory + "/" + Name + "Look.asset";
        }

        /// <summary>The ten battle stages, in the order of the battle scene's choice.</summary>
        public static IReadOnlyList<Set> All => Sets;

        /// <summary>The stage of a name (<see cref="Set.Name"/>), or null.</summary>
        public static Set Find(string name)
        {
            foreach (var set in Sets)
                if (set.Name == name)
                    return set;
            return null;
        }

        [MenuItem("Baryonyx/Stages/Create Battle Stage Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();
            EnsureSharedMaterials();
            foreach (var set in Sets)
                BuildSet(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>Builds the battle stages that have not been built yet.</summary>
        public static void EnsureAssets()
        {
            EnsureSharedMaterials();
            foreach (var set in Sets)
                if (
                    AssetDatabase.LoadAssetAtPath<GameObject>(set.PrefabPath) == null
                    || AssetDatabase.LoadAssetAtPath<VolumeProfile>(set.LookPath) == null
                )
                    BuildSet(set);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Builds one stage's look (post-processing) and prefab again.</summary>
        public static void BuildSet(Set set)
        {
            AssetFolders.Ensure(set.Directory);
            Profile(
                set.LookPath,
                profile =>
                {
                    // The same lens as the dusk highland: sharp from the front row's feet to the
                    // back row's, the ground under the hand of cards blurring towards the bottom.
                    StageSetAssets.AddFocus(profile, 18.2f, 4.7f, 3.8f, 9f, 12f, 5f);
                    set.Look(profile);
                }
            );
            var builder = new Builder(set);
            try
            {
                set.Build(builder);
                builder.Save();
            }
            finally
            {
                builder.Dispose();
            }
        }

        // The battle keeps the bloom of its effects (BattlePostProcess); each stage adds its lens
        // and its grade: the shadows' and lights' tints, and a vignette.
        private static Action<VolumeProfile> Grade(
            float exposure,
            float contrast,
            float saturation,
            float temperature,
            Color shadows,
            Color highlights,
            float vignette,
            Color vignetteColor
        ) =>
            profile =>
            {
                StageSetAssets.AddGrade(
                    profile,
                    exposure,
                    contrast,
                    saturation,
                    temperature,
                    shadows,
                    highlights,
                    0f
                );
                StageSetAssets.AddVignette(profile, vignette, vignetteColor);
            };

        private static readonly Set[] Sets =
        {
            // A spring meadow at noon on the road out of town: a high warm sun, a blue sky with
            // a windmill on the hills, an old oak at the corners and a broken cart by the road.
            new(
                "MeadowRoad",
                "Meadow",
                new StageSetAssets.StageEnvironment(
                    new Color(0.56f, 0.66f, 0.82f),
                    new Color(0.72f, 0.82f, 0.9f),
                    26f,
                    85f,
                    new Color(0.46f, 0.54f, 0.42f),
                    new Color(0.26f, 0.25f, 0.16f)
                ),
                Grade(
                    0.05f,
                    12f,
                    12f,
                    4f,
                    new Color(0.42f, 0.48f, 0.6f),
                    new Color(0.62f, 0.56f, 0.44f),
                    0.26f,
                    new Color(0.03f, 0.06f, 0.03f)
                ),
                b =>
                {
                    b.Ground(Color.white);
                    b.Floor(Color.white);
                    b.Backdrop(Color.white);
                    b.Frames(b.Cutout("Tree", Color.white, 2f), 7.2f);
                    b.Mids(b.Cutout("Fence", Color.white), 2.8f, true);
                    b.Story(b.Cutout("Cart", Color.white), 3f);
                    b.Smalls(b.Cutout("Stones", Color.white), 1.1f);
                    var tuft = b.Foliage("Tuft", 0.04f);
                    b.Tufts(tuft, 40, 0.5f, 0.85f, outside: true);
                    b.Tufts(tuft, 18, 0.4f, 0.6f, outside: false);
                    b.Litter(b.Decal("Litter", Color.white), 10, 1.4f, 2f);
                    var sun = b.Sun(
                        new Vector3(48f, 62f, 0f),
                        new Color(1f, 0.95f, 0.84f),
                        1.5f,
                        0.85f
                    );
                    // Shadows of the clouds drift over the field (the leaves' mask laid large), so
                    // sun and shade alternate on the ground instead of lighting it evenly.
                    LeafCookie(sun, 34f);
                    // Sunbeams through the oaks' leaves at the corners and a broad one through a
                    // gap in the clouds, each landing in a pool of light.
                    var beam = new Color(1f, 0.93f, 0.72f);
                    b.Shafts(
                        "Sunbeams",
                        beam,
                        0.85f,
                        sun.transform.forward,
                        11f,
                        (-9.8f, 12.6f, 1.6f),
                        (10.4f, 13f, 1.4f),
                        (-1.2f, 16.4f, 2.8f)
                    );
                    b.Pools(
                        "SunPools",
                        beam,
                        0.3f,
                        (-9.8f, 12.6f, 3.4f, 1f),
                        (10.4f, 13f, 3f, 1f),
                        (-1.2f, 16.4f, 5f, 0.8f)
                    );
                    b.Motes(new Color(1f, 0.96f, 0.78f), 44);
                }
            ),
            // A deep forest in the morning mist: giant mossy trunks, pale beams slanting between
            // them, an old stone shrine and mushrooms among the ferns.
            new(
                "MistyWoods",
                "Woods",
                new StageSetAssets.StageEnvironment(
                    new Color(0.44f, 0.56f, 0.6f),
                    new Color(0.56f, 0.68f, 0.66f),
                    12f,
                    52f,
                    new Color(0.3f, 0.42f, 0.36f),
                    new Color(0.14f, 0.16f, 0.12f)
                ),
                Grade(
                    0f,
                    10f,
                    6f,
                    -2f,
                    new Color(0.38f, 0.5f, 0.55f),
                    new Color(0.62f, 0.57f, 0.45f),
                    0.32f,
                    new Color(0.02f, 0.05f, 0.04f)
                ),
                b =>
                {
                    b.Ground(Color.white);
                    b.Floor(Color.white);
                    b.Backdrop(Color.white);
                    b.Frames(b.Cutout("Trunk", Color.white, 2.5f), 9f);
                    b.Mids(b.Cutout("Boulder", Color.white), 3.1f, true);
                    b.Story(b.Cutout("Shrine", Color.white), 1.6f);
                    b.Smalls(b.Cutout("Mushrooms", Color.white), 1.1f);
                    var fern = b.Foliage("Fern", 0.035f);
                    b.Tufts(fern, 36, 0.6f, 1.1f, outside: true);
                    b.Tufts(fern, 10, 0.45f, 0.7f, outside: false);
                    b.Litter(b.Decal("Litter", Color.white), 12, 1.4f, 2.1f);
                    var sun = b.Sun(
                        new Vector3(34f, 58f, 0f),
                        new Color(1f, 0.88f, 0.7f),
                        1.15f,
                        0.75f
                    );
                    LeafCookie(sun, 12f);
                    // Morning light slanting through the mist between the trunks: many beams,
                    // brighter than the meadow's, each landing in a pool of light.
                    var beam = new Color(1f, 0.9f, 0.68f);
                    b.Shafts(
                        "Sunbeams",
                        beam,
                        0.9f,
                        sun.transform.forward,
                        10f,
                        (-8.6f, 11.6f, 1.5f),
                        (-4.6f, 15.8f, 1.9f),
                        (1.4f, 14.4f, 1.3f),
                        (6.4f, 13.4f, 1.7f),
                        (10f, 16.4f, 1.2f)
                    );
                    b.Pools(
                        "SunPools",
                        beam,
                        0.4f,
                        (-8.6f, 11.6f, 3.2f, 1f),
                        (-4.6f, 15.8f, 3.8f, 1f),
                        (1.4f, 14.4f, 2.8f, 1f),
                        (6.4f, 13.4f, 3.4f, 1f),
                        (10f, 16.4f, 2.6f, 1f)
                    );
                    b.Motes(new Color(1f, 0.92f, 0.7f), 60);
                }
            ),
            // A mine underground: timber props holding up the rock, a cart of ore, and lanterns
            // giving warm pools of light in the dark.
            new(
                "MineTunnel",
                "Mine",
                new StageSetAssets.StageEnvironment(
                    new Color(0.2f, 0.18f, 0.2f),
                    new Color(0.06f, 0.05f, 0.05f),
                    18f,
                    52f,
                    new Color(0.24f, 0.18f, 0.13f),
                    new Color(0.08f, 0.06f, 0.05f)
                ),
                Grade(
                    0f,
                    14f,
                    4f,
                    0f,
                    new Color(0.38f, 0.4f, 0.5f),
                    new Color(0.66f, 0.52f, 0.38f),
                    0.34f,
                    new Color(0.02f, 0.01f, 0.01f)
                ),
                b =>
                {
                    b.Ground(Color.white);
                    // The old boards are dark; lighter, the battle reads on them.
                    b.Floor(new Color(1.3f, 1.25f, 1.2f));
                    b.Backdrop(Color.white);
                    b.Frames(b.Cutout("Wall", Color.white, 2.5f), 7f);
                    b.Mids(b.Cutout("Support", Color.white, 2.5f), 3.4f, false);
                    b.Story(b.Cutout("Cart", Color.white), 2.8f);
                    b.Smalls(b.Cutout("Ore", Color.white), 1.1f);
                    b.Litter(b.Decal("Litter", Color.white), 12, 1.4f, 2.1f);
                    b.Sun(new Vector3(55f, 68f, 0f), new Color(0.62f, 0.68f, 0.86f), 0.55f, 0.75f);
                    b.Fill(new Color(0.5f, 0.45f, 0.42f), 0.28f);
                    b.Lanterns(
                        b.Cutout("Lantern", Color.white),
                        2.6f,
                        Lantern.MineLamp,
                        new Color(1f, 0.66f, 0.32f)
                    );
                    b.Warm(new Color(1f, 0.62f, 0.3f), 3f);
                    // Daylight falls through cracks in the roof onto the boards: cool shafts
                    // against the lanterns' warmth, the dust floating in them.
                    var daylight = new Color(0.72f, 0.84f, 1f);
                    b.Shafts(
                        "RoofShafts",
                        daylight,
                        0.75f,
                        new Vector3(0.4f, -1f, -0.3f),
                        9f,
                        (-2.4f, 13.8f, 1.5f),
                        (3.4f, 14.2f, 1.1f)
                    );
                    b.Pools(
                        "RoofPools",
                        daylight,
                        0.3f,
                        (-2.4f, 13.8f, 3.2f, 1f),
                        (3.4f, 14.2f, 2.4f, 1f)
                    );
                    PointLight(
                        b.Root,
                        "RoofLight",
                        new Vector3(-2.4f, 2.6f, 13.8f),
                        daylight,
                        1.4f,
                        6f
                    );
                    b.Motes(new Color(1f, 0.76f, 0.48f), 36);
                }
            ),
            // A cavern of crystals: huge blue and violet columns glowing in the dark, a still
            // lake behind, an old statue swallowed by the crystals.
            new(
                "CrystalCavern",
                "Crystal",
                new StageSetAssets.StageEnvironment(
                    new Color(0.16f, 0.18f, 0.34f),
                    new Color(0.05f, 0.06f, 0.13f),
                    18f,
                    60f,
                    new Color(0.14f, 0.13f, 0.28f),
                    new Color(0.05f, 0.05f, 0.1f)
                ),
                Grade(
                    0f,
                    12f,
                    10f,
                    0f,
                    new Color(0.38f, 0.4f, 0.62f),
                    new Color(0.5f, 0.56f, 0.66f),
                    0.38f,
                    new Color(0.02f, 0.02f, 0.08f)
                ),
                b =>
                {
                    // The crystals light themselves; their brightest faces pass white and bloom.
                    var crystalGlow = new Color(0.8f, 0.9f, 1.25f);
                    b.Ground(Color.white);
                    b.Floor(Color.white);
                    b.Backdrop(Color.white, glowBoost: 1.6f);
                    b.Frames(b.Cutout("Spire", Color.white, 2f, crystalGlow), 6.5f);
                    b.Mids(b.Cutout("Cluster", Color.white, 2f, crystalGlow), 2.4f, false);
                    b.Story(
                        b.Cutout("Statue", Color.white, 2f, crystalGlow * 0.5f),
                        2.2f,
                        tall: true
                    );
                    b.Smalls(b.Cutout("Shards", Color.white, 2f, crystalGlow), 1f);
                    b.Litter(b.Decal("Litter", Color.white, crystalGlow * 0.6f), 12, 1.4f, 2f);
                    b.Sun(new Vector3(50f, 70f, 0f), new Color(0.55f, 0.65f, 1f), 0.6f, 0.8f);
                    b.Fill(new Color(0.45f, 0.5f, 0.9f), 0.3f);
                    // Light from the crystals themselves: cyan at the corners, violet at the back.
                    var glow = Group(b.Root, "CrystalLights", Vector3.zero).transform;
                    PointLight(
                        glow,
                        "CrystalLeft",
                        new Vector3(-11f, 2.2f, 14.6f),
                        new Color(0.4f, 0.8f, 1f),
                        2.4f,
                        9f,
                        false,
                        0.06f,
                        1f
                    );
                    PointLight(
                        glow,
                        "CrystalRight",
                        new Vector3(11.2f, 2.2f, 14.4f),
                        new Color(0.65f, 0.5f, 1f),
                        2.4f,
                        9f,
                        false,
                        0.06f,
                        3f
                    );
                    PointLight(
                        glow,
                        "CrystalBack",
                        new Vector3(0.5f, 2f, 16.2f),
                        new Color(0.55f, 0.6f, 1f),
                        1.8f,
                        8f,
                        false,
                        0.05f,
                        5f
                    );
                    // Pale light falls through cracks in the cavern's roof, and the crystals light the
                    // floor round them, cyan at the corners and violet at the back.
                    var shaft = new Color(0.62f, 0.8f, 1f);
                    b.Shafts(
                        "RoofShafts",
                        shaft,
                        1f,
                        new Vector3(-0.45f, -1f, -0.25f),
                        10f,
                        (-1.2f, 14.6f, 2.2f),
                        (6.6f, 14.4f, 1.5f),
                        (-7.4f, 14.6f, 1.5f)
                    );
                    b.Pools(
                        "RoofPools",
                        shaft,
                        0.45f,
                        (-1.2f, 14.6f, 3.8f, 1f),
                        (6.6f, 14.4f, 2.4f, 1f),
                        (-7.4f, 14.6f, 2.4f, 1f)
                    );
                    b.Pools(
                        "CyanPools",
                        new Color(0.35f, 0.75f, 1f),
                        0.55f,
                        (-11f, 14.2f, 5.5f, 1f),
                        (-9.4f, 10.4f, 2.6f, 0.8f),
                        (5.5f, 4.1f, 2.4f, 0.7f),
                        (-5.7f, 4f, 2.4f, 0.7f)
                    );
                    b.Pools(
                        "VioletPools",
                        new Color(0.65f, 0.45f, 1f),
                        0.55f,
                        (11.2f, 14.2f, 5.5f, 1f),
                        (9.1f, 9.6f, 2.6f, 0.8f),
                        (5.8f, 17f, 3.2f, 0.8f),
                        (-5.6f, 17.1f, 3.2f, 0.8f)
                    );
                    b.Motes(new Color(0.6f, 0.85f, 1f), 48);
                }
            ),
            // A rocky shore at high noon: the deep blue sea and a towering cloud, a sea stack at
            // the corners, a wreck washed up on the sand.
            new(
                "RockyShore",
                "Shore",
                new StageSetAssets.StageEnvironment(
                    new Color(0.6f, 0.72f, 0.88f),
                    new Color(0.76f, 0.86f, 0.93f),
                    30f,
                    95f,
                    new Color(0.55f, 0.62f, 0.66f),
                    new Color(0.34f, 0.31f, 0.24f)
                ),
                Grade(
                    0.05f,
                    12f,
                    14f,
                    -2f,
                    new Color(0.4f, 0.5f, 0.62f),
                    new Color(0.62f, 0.56f, 0.46f),
                    0.24f,
                    new Color(0.02f, 0.05f, 0.08f)
                ),
                b =>
                {
                    b.Ground(Color.white, 1.5f);
                    b.Floor(Color.white);
                    b.Backdrop(Color.white);
                    b.Frames(b.Cutout("Stack", Color.white, 2.5f), 7.5f);
                    b.Mids(b.Cutout("Rocks", Color.white), 3.2f, true);
                    b.Story(b.Cutout("Wreck", Color.white), 3.6f);
                    b.Smalls(b.Cutout("Pebbles", Color.white), 1f);
                    var grass = b.Foliage("Grass", 0.05f);
                    b.Tufts(grass, 30, 0.5f, 0.9f, outside: true);
                    b.Litter(b.Decal("Litter", Color.white), 12, 1.4f, 2f);
                    var sun = b.Sun(
                        new Vector3(55f, 60f, 0f),
                        new Color(1f, 0.97f, 0.9f),
                        1.55f,
                        0.85f
                    );
                    LeafCookie(sun, 40f);
                    // Broad, faint rays through the towering cloud over the sea.
                    var beam = new Color(1f, 0.97f, 0.88f);
                    b.Shafts(
                        "Sunbeams",
                        beam,
                        0.6f,
                        sun.transform.forward,
                        12f,
                        (-6.2f, 15.6f, 3f),
                        (4.4f, 16.6f, 3.4f),
                        (11f, 13.4f, 1.8f)
                    );
                    b.Pools(
                        "SunPools",
                        beam,
                        0.25f,
                        (-6.2f, 15.6f, 5.5f, 1f),
                        (4.4f, 16.6f, 6f, 1f),
                        (11f, 13.4f, 3.4f, 1f)
                    );
                    b.Motes(new Color(0.95f, 0.98f, 1f), 30);
                }
            ),
            // A swamp in the mist on a grey evening: twisted dead trees, a boat sunk in the mud,
            // pale will-o'-the-wisps and glowing toadstools.
            new(
                "MistySwamp",
                "Swamp",
                new StageSetAssets.StageEnvironment(
                    new Color(0.32f, 0.38f, 0.36f),
                    new Color(0.36f, 0.42f, 0.36f),
                    12f,
                    55f,
                    new Color(0.27f, 0.31f, 0.25f),
                    new Color(0.1f, 0.11f, 0.08f)
                ),
                Grade(
                    0f,
                    10f,
                    -6f,
                    0f,
                    new Color(0.38f, 0.46f, 0.44f),
                    new Color(0.58f, 0.58f, 0.46f),
                    0.38f,
                    new Color(0.03f, 0.05f, 0.03f)
                ),
                b =>
                {
                    b.Ground(new Color(0.9f, 0.95f, 0.9f), 1f);
                    b.Floor(Color.white);
                    b.Backdrop(Color.white);
                    b.Frames(b.Cutout("Tree", Color.white, 2.5f), 7f);
                    b.Mids(b.Cutout("Stump", Color.white), 2.6f, true);
                    b.Story(b.Cutout("Boat", Color.white), 3.4f);
                    b.Smalls(
                        b.Cutout("Shrooms", Color.white, 2f, new Color(0.45f, 0.65f, 0.5f)),
                        1f
                    );
                    var reeds = b.Foliage("Reeds", 0.045f);
                    b.Tufts(reeds, 36, 0.6f, 1.1f, outside: true);
                    b.Tufts(reeds, 8, 0.45f, 0.65f, outside: false);
                    b.Litter(b.Decal("Litter", Color.white), 12, 1.4f, 2.2f);
                    b.Sun(new Vector3(28f, 70f, 0f), new Color(0.82f, 0.84f, 0.64f), 0.75f, 0.65f);
                    b.Fill(new Color(0.5f, 0.6f, 0.55f), 0.25f);
                    // Will-o'-the-wisps: pale green lights drifting low over the water.
                    var wisps = Group(b.Root, "Wisps", Vector3.zero).transform;
                    PointLight(
                        wisps,
                        "WispLeft",
                        new Vector3(-9.5f, 1.4f, 12.5f),
                        new Color(0.55f, 1f, 0.7f),
                        1.6f,
                        6f,
                        false,
                        0.25f,
                        2f
                    );
                    PointLight(
                        wisps,
                        "WispRight",
                        new Vector3(9.8f, 1.4f, 13.5f),
                        new Color(0.55f, 1f, 0.75f),
                        1.6f,
                        6f,
                        false,
                        0.25f,
                        4f
                    );
                    // Pale light breaking through the fog, and the wisps' green on the water.
                    var haze = new Color(0.82f, 0.92f, 0.72f);
                    b.Shafts(
                        "FogShafts",
                        haze,
                        0.6f,
                        Quaternion.Euler(42f, 68f, 0f) * Vector3.forward,
                        9f,
                        (-7.2f, 12.2f, 1.6f),
                        (-2.2f, 15.8f, 2.2f),
                        (4.8f, 14.2f, 1.4f),
                        (9.6f, 16.6f, 1.2f)
                    );
                    b.Pools(
                        "FogPools",
                        haze,
                        0.3f,
                        (-7.2f, 12.2f, 3.2f, 1f),
                        (-2.2f, 15.8f, 4.2f, 1f),
                        (4.8f, 14.2f, 2.8f, 1f),
                        (9.6f, 16.6f, 2.4f, 1f)
                    );
                    b.Pools(
                        "WispPools",
                        new Color(0.5f, 1f, 0.65f),
                        0.5f,
                        (-9.5f, 12.5f, 3.4f, 1f),
                        (9.8f, 13.5f, 3.4f, 1f),
                        (-5.7f, 4f, 2.2f, 0.8f),
                        (5.5f, 4.1f, 2.2f, 0.8f)
                    );
                    b.Motes(new Color(0.6f, 1f, 0.75f), 36);
                }
            ),
            // A snowfield on a grey winter day: snow-laden firs, rocks and ice, a traveller's
            // sled left half buried, and snow falling.
            new(
                "SnowField",
                "Snow",
                new StageSetAssets.StageEnvironment(
                    new Color(0.62f, 0.68f, 0.8f),
                    new Color(0.8f, 0.84f, 0.9f),
                    18f,
                    70f,
                    new Color(0.55f, 0.6f, 0.68f),
                    new Color(0.48f, 0.5f, 0.56f)
                ),
                Grade(
                    0f,
                    8f,
                    -4f,
                    -6f,
                    new Color(0.4f, 0.46f, 0.62f),
                    new Color(0.58f, 0.58f, 0.6f),
                    0.22f,
                    new Color(0.05f, 0.07f, 0.12f)
                ),
                b =>
                {
                    // White snow under a grey sky, a little darker so the characters stand out.
                    b.Ground(new Color(0.9f, 0.92f, 0.95f), 1.5f);
                    b.Floor(new Color(0.92f, 0.93f, 0.96f));
                    b.Backdrop(Color.white);
                    b.Frames(b.Cutout("Pine", Color.white, 2f), 7.5f);
                    b.Mids(b.Cutout("Rocks", Color.white), 2.9f, true);
                    b.Story(b.Cutout("Sled", Color.white), 3f);
                    b.Smalls(b.Cutout("Stones", Color.white), 1.1f);
                    var grass = b.Foliage("Grass", 0.035f);
                    b.Tufts(grass, 26, 0.5f, 0.8f, outside: true);
                    b.Litter(b.Decal("Litter", Color.white), 10, 1.4f, 2f);
                    var sun = b.Sun(
                        new Vector3(40f, 64f, 0f),
                        new Color(0.92f, 0.94f, 1f),
                        1.15f,
                        0.6f
                    );
                    // Cold light breaking through the clouds onto the snow.
                    var beam = new Color(0.88f, 0.94f, 1f);
                    b.Shafts(
                        "CloudShafts",
                        beam,
                        0.7f,
                        sun.transform.forward,
                        11f,
                        (-5.2f, 14.6f, 2.4f),
                        (3.6f, 16.2f, 2.8f),
                        (9.2f, 12.8f, 1.6f)
                    );
                    b.Pools(
                        "CloudPools",
                        beam,
                        0.22f,
                        (-5.2f, 14.6f, 4.6f, 1f),
                        (3.6f, 16.2f, 5.2f, 1f),
                        (9.2f, 12.8f, 3.2f, 1f)
                    );
                    b.Snowfall();
                }
            ),
            // A volcano: black basalt under a sky of smoke, rivers of lava glowing, a charred
            // dragon's skull, embers rising from the cracks.
            new(
                "VolcanoCrater",
                "Volcano",
                // The red is in the lava and the sky; the light on the field stays near neutral
                // so the characters keep their own colours (a green slime stays green).
                new StageSetAssets.StageEnvironment(
                    new Color(0.26f, 0.19f, 0.21f),
                    new Color(0.2f, 0.08f, 0.07f),
                    18f,
                    65f,
                    new Color(0.3f, 0.2f, 0.18f),
                    new Color(0.34f, 0.16f, 0.08f)
                ),
                Grade(
                    0f,
                    14f,
                    4f,
                    0f,
                    new Color(0.45f, 0.38f, 0.46f),
                    new Color(0.64f, 0.52f, 0.42f),
                    0.38f,
                    new Color(0.08f, 0.01f, 0.01f)
                ),
                b =>
                {
                    var lava = new Color(0.9f, 0.55f, 0.4f);
                    b.Ground(Color.white);
                    b.Floor(Color.white);
                    b.Backdrop(Color.white, glowBoost: 1.5f);
                    b.Frames(b.Cutout("Spire", Color.white, 2.5f, lava), 7.5f);
                    b.Mids(b.Cutout("Rocks", Color.white, 2.5f, lava), 2.7f, true);
                    b.Story(b.Cutout("Bones", Color.white), 3.2f);
                    b.Smalls(b.Cutout("Embers", Color.white, 2f, lava), 1.1f);
                    // The lava in the cracks shines past white and blooms.
                    var cracks = b.Decal("Cracks", Color.white, new Color(2.2f, 1.5f, 1.1f));
                    b.Litter(cracks, 9, 1.6f, 2.4f);
                    b.Sun(new Vector3(26f, 70f, 0f), new Color(1f, 0.72f, 0.54f), 1f, 0.8f);
                    b.Fill(new Color(0.6f, 0.52f, 0.62f), 0.3f);
                    var heat = Group(b.Root, "LavaLights", Vector3.zero).transform;
                    var ember = AssetDatabase.LoadAssetAtPath<Material>(EmberMaterialPath);
                    (float x, float z)[] vents =
                    {
                        (-9.2f, 10f),
                        (8.8f, 12.5f),
                        (-2.5f, 16.4f),
                        (3.5f, 2.2f),
                    };
                    for (int i = 0; i < vents.Length; i++)
                    {
                        var (x, z) = vents[i];
                        PointLight(
                            heat,
                            "Lava" + i,
                            new Vector3(x, 0.8f, z),
                            new Color(1f, 0.45f, 0.15f),
                            2.2f,
                            7f,
                            false,
                            0.12f,
                            i * 1.7f
                        );
                        Embers(heat, "Embers" + i, new Vector3(x, 0.1f, z), ember, 3f, 0.6f, 0.08f);
                    }
                    // Each vent is a glowing crack with its light lying round it on the rock, and
                    // the heat rising from it as a faint orange column.
                    b.Lay(
                        cracks,
                        "VentCracks",
                        (-9.2f, 10f, 2.6f, 1),
                        (8.8f, 12.5f, 2.4f, 2),
                        (-2.5f, 16.4f, 2.2f, 3),
                        (3.5f, 2.2f, 2.6f, 0)
                    );
                    var glow = new Color(1f, 0.45f, 0.15f);
                    b.Pools(
                        "LavaPools",
                        glow,
                        0.6f,
                        (-9.2f, 10f, 5f, 1f),
                        (8.8f, 12.5f, 5f, 1f),
                        (-2.5f, 16.4f, 4.4f, 1f),
                        (3.5f, 2.2f, 4.4f, 0.8f)
                    );
                    b.Shafts(
                        "HeatColumns",
                        glow,
                        0.45f,
                        Vector3.down,
                        4.5f,
                        (-9.2f, 10f, 1.8f),
                        (8.8f, 12.5f, 1.8f),
                        (-2.5f, 16.4f, 1.6f)
                    );
                    b.Motes(new Color(1f, 0.55f, 0.3f), 48);
                }
            ),
            // An old graveyard under the full moon: moonlight and a pale blue haze, a dead tree
            // and rows of stones, a broken angel and iron lanterns.
            new(
                "MoonlitGraveyard",
                "Grave",
                new StageSetAssets.StageEnvironment(
                    new Color(0.17f, 0.23f, 0.32f),
                    new Color(0.08f, 0.12f, 0.15f),
                    14f,
                    60f,
                    new Color(0.12f, 0.16f, 0.2f),
                    new Color(0.04f, 0.05f, 0.06f)
                ),
                Grade(
                    0f,
                    14f,
                    4f,
                    0f,
                    new Color(0.38f, 0.44f, 0.6f),
                    new Color(0.56f, 0.6f, 0.62f),
                    0.4f,
                    new Color(0.01f, 0.02f, 0.05f)
                ),
                b =>
                {
                    b.Ground(Color.white);
                    b.Floor(Color.white);
                    // The moon and the stars bloom.
                    b.Backdrop(Color.white, 1.6f, 0.6f, 1.5f);
                    b.Frames(b.Cutout("Tree", Color.white, 2.5f), 7f);
                    b.Mids(b.Cutout("Stones", Color.white), 2.9f, true);
                    b.Story(b.Cutout("Angel", Color.white), 2.6f, tall: true);
                    b.Smalls(b.Cutout("Crosses", Color.white), 1.1f);
                    var grass = b.Foliage("Grass", 0.035f);
                    b.Tufts(grass, 34, 0.5f, 0.85f, outside: true);
                    b.Tufts(grass, 10, 0.35f, 0.55f, outside: false);
                    b.Litter(b.Decal("Litter", Color.white), 12, 1.4f, 2f);
                    b.Sun(new Vector3(36f, 64f, 0f), new Color(0.6f, 0.75f, 1f), 0.8f, 0.85f);
                    b.Fill(new Color(0.42f, 0.52f, 0.9f), 0.3f);
                    b.Lanterns(
                        b.Cutout("Lantern", Color.white),
                        3f,
                        Lantern.GraveLamp,
                        new Color(0.6f, 0.85f, 1f)
                    );
                    // Moonbeams through the drifting cloud, from behind the field like the moon in
                    // the sky, each landing in a pale pool on the flagstones.
                    var moon = new Color(0.7f, 0.85f, 1f);
                    b.Shafts(
                        "Moonbeams",
                        moon,
                        0.6f,
                        new Vector3(0.45f, -1f, -0.28f),
                        10f,
                        (-1.6f, 12.6f, 2.6f),
                        (4.6f, 13.8f, 1.8f),
                        (-6.4f, 14.2f, 1.6f)
                    );
                    b.Pools(
                        "MoonPools",
                        moon,
                        0.35f,
                        (-1.6f, 12.6f, 4.6f, 1f),
                        (4.6f, 13.8f, 3f, 1f),
                        (-6.4f, 14.2f, 2.8f, 1f)
                    );
                    b.Motes(new Color(0.6f, 0.85f, 1f), 32);
                }
            ),
            // The great hall of an old castle at night: moonlight through stained glass, stone
            // columns hung with banners, a red carpet, and fire in iron bowls.
            new(
                "CastleHall",
                "Castle",
                new StageSetAssets.StageEnvironment(
                    new Color(0.22f, 0.18f, 0.28f),
                    new Color(0.07f, 0.05f, 0.08f),
                    18f,
                    60f,
                    new Color(0.24f, 0.16f, 0.18f),
                    new Color(0.08f, 0.05f, 0.06f)
                ),
                Grade(
                    0f,
                    14f,
                    6f,
                    0f,
                    new Color(0.42f, 0.38f, 0.58f),
                    new Color(0.66f, 0.52f, 0.4f),
                    0.34f,
                    new Color(0.04f, 0.01f, 0.04f)
                ),
                b =>
                {
                    b.Ground(Color.white, 2f);
                    // The deep red carpet a little lighter, so the battle reads on it.
                    b.Floor(new Color(1.15f, 1.1f, 1.1f), "Carpet");
                    b.Backdrop(Color.white, glowBoost: 1.4f);
                    b.Frames(b.Cutout("Pillar", Color.white, 2.5f), 9f);
                    b.Mids(b.Cutout("Armor", Color.white), 2.6f, false);
                    b.Story(b.Cutout("Rubble", Color.white), 3.4f);
                    b.Smalls(b.Cutout("Debris", Color.white), 1.1f);
                    // Broken stones lie on the stone floor only; on the carpet they read as stains.
                    b.Litter(b.Decal("Litter", Color.white), 10, 1.4f, 2f, 9.5f);
                    b.Sun(new Vector3(48f, 70f, 0f), new Color(0.62f, 0.6f, 0.95f), 0.6f, 0.8f);
                    b.Fill(new Color(0.5f, 0.42f, 0.6f), 0.34f);
                    b.Braziers(b.Cutout("Brazier", Color.white), 2.2f, Lantern.CastleBowlTop);
                    b.Warm(new Color(1f, 0.6f, 0.32f), 2.6f);
                    // Moonlight through the stained glass behind the throne: blue, gold and red
                    // shafts slanting down towards the camera, each leaving its colour on the
                    // carpet.
                    var window = new Vector3(0.45f, -1f, -0.45f);
                    (string name, Color color, float x, float z)[] panes =
                    {
                        ("BlueShaft", new Color(0.5f, 0.66f, 1f), -3.4f, 11.2f),
                        ("GoldShaft", new Color(1f, 0.84f, 0.52f), 0.2f, 12.4f),
                        ("RedShaft", new Color(1f, 0.46f, 0.42f), 3.6f, 11.4f),
                    };
                    foreach (var (name, color, x, z) in panes)
                    {
                        b.Shafts(name, color, 1f, window, 9f, (x, z, 2.2f));
                        b.Pools(name + "Pool", color, 0.3f, (x, z, 4.6f, 1f));
                    }
                    b.Motes(new Color(1f, 0.8f, 0.55f), 34);
                }
            ),
        };

        /// <summary>
        /// Where on its picture a light stand's light is (measured on the pictures): the height
        /// as a fraction of the picture's from the bottom, and how far right of the middle as a
        /// fraction of its width.
        /// </summary>
        private static class Lantern
        {
            public static readonly Vector2 MineLamp = new(0.33f, 0.68f);
            public static readonly Vector2 GraveLamp = new(0f, 0.85f);
            public const float CastleBowlTop = 0.95f;
        }

        /// <summary>
        /// Puts one battle stage together in the shared layout: the ground, the floor, the painted
        /// distance, the scenery and props in their places, then the tufts, litter and shades.
        /// </summary>
        internal sealed class Builder : IDisposable
        {
            private readonly Set set;
            private readonly GameObject stage;
            private readonly List<Piece> tufts = new();
            private readonly List<Vector3> bedded = new();
            private Material tuftMaterial;
            private int seed;

            public readonly MeshStore Store;
            public readonly Transform Root;
            public readonly Transform Dressing;

            public Builder(Set set)
            {
                this.set = set;
                // A seed of its own for each stage, the same on every build.
                foreach (char letter in set.Name)
                    seed = seed * 31 + letter & 0xffff;
                Store = new MeshStore(set.Directory + "/" + set.Name + "StageMeshes.asset");
                stage = new GameObject(set.Name + "Stage");
                Root = stage.transform;
                Dressing = Group(Root, "Dressing", Vector3.zero).transform;
            }

            private string Path(string part) => $"{set.Directory}/{set.Prefix}{part}";

            // The ground round the floor, a little lower so the two never fight over depth.
            public void Ground(Color tint, float relief = 2f)
            {
                var texture = PixelTexture(Path("Ground") + ".aseprite", true);
                var material = LitMaterial(
                    Path("Ground") + ".mat",
                    texture,
                    tint,
                    false,
                    null,
                    ReliefMap(texture, relief)
                );
                Hd2dStageKit.Floor(
                    Root,
                    Store,
                    "Ground",
                    material,
                    new Vector3(0f, -0.03f, 10f),
                    new Vector2(64f, 44f),
                    3f,
                    8f
                );
            }

            // The floor both parties stand on (height 0): one picture over about 17 m.
            public void Floor(Color tint, string part = "Floor")
            {
                var texture = PixelTexture(Path(part) + ".aseprite", false);
                var material = LitMaterial(
                    Path(part) + ".mat",
                    texture,
                    tint,
                    true,
                    null,
                    ReliefMap(texture, 2.5f, 0.8f)
                );
                const float width = 17f;
                PaintedFloor(
                    Root,
                    Store,
                    "Floor",
                    material,
                    new Vector3(Centre.x, 0f, Centre.y),
                    new Vector2(width, width * texture.height / texture.width),
                    5.7f
                );
            }

            // The far land and sky as three wide boards (the middle one mirrored), sunk so the
            // ground meets them at about 70% of the screen's height, like the dusk highland's.
            public void Backdrop(
                Color tint,
                float starBoost = 1f,
                float twinkle = 0f,
                float glowBoost = 1f
            )
            {
                var texture = PixelTexture(Path("Backdrop") + ".aseprite", false);
                var material = PaintedMaterial(
                    Path("Backdrop") + ".mat",
                    texture,
                    tint,
                    starBoost,
                    twinkle,
                    glowBoost
                );
                for (int tile = -1; tile <= 1; tile++)
                    Flat(
                        Root,
                        Store,
                        "Backdrop" + (tile + 1),
                        material,
                        new Vector3(tile * 19.2f, -2.4f, 17.6f),
                        new Vector2(19.2f, 8f),
                        Vector3.back,
                        false,
                        tile == 0
                    );
            }

            /// <summary>A lit cut-out picture of this stage with its relief (and its glow).</summary>
            public StageDressing.Prop Cutout(
                string part,
                Color tint,
                float relief = 2f,
                Color? glow = null
            )
            {
                var texture = PixelTexture(Path(part) + ".aseprite", false);
                var material = LitMaterial(
                    Path(part) + ".mat",
                    texture,
                    tint,
                    true,
                    glow,
                    ReliefMap(texture, relief, 1.2f)
                );
                return new StageDressing.Prop(material, texture.height / (float)texture.width);
            }

            /// <summary>Litter laid on the ground, with soft relief (the low light rakes across it).</summary>
            public StageDressing.Prop Decal(string part, Color tint, Color? glow = null)
            {
                var texture = PixelTexture(Path(part) + ".aseprite", false);
                var material = LitMaterial(
                    Path(part) + ".mat",
                    texture,
                    tint,
                    true,
                    glow,
                    ReliefMap(texture, 1.5f, 1.2f)
                );
                return new StageDressing.Prop(material, texture.height / (float)texture.width);
            }

            public StageDressing.Prop Foliage(string part, float sway) =>
                StageDressing.LoadFoliage(set.Directory, set.Prefix + part, sway);

            private void Stand(
                StageDressing.Prop prop,
                string name,
                float x,
                float z,
                Vector2 size,
                bool mirror,
                bool bed = true
            )
            {
                // Sunk a little so the bottom edge never shows as a straight line on the ground.
                Flat(
                    Dressing,
                    Store,
                    name,
                    prop.Material,
                    new Vector3(x, -0.03f, z),
                    size,
                    Vector3.back,
                    true,
                    mirror
                );
                if (bed)
                    bedded.Add(new Vector3(x, size.x, z));
            }

            // Large scenery at the back corners frames the field; the camera's drift parts it from
            // the painted distance. Taller than the frame, it runs out of the top of the screen.
            // Everything stands in front of the painted distance (17.6 m), which hides what is
            // behind it.
            public void Frames(StageDressing.Prop prop, float height)
            {
                Stand(prop, "FrameLeft", -12.2f, 15.6f, prop.Tall(height), false);
                Stand(prop, "FrameRight", 12.6f, 16.1f, prop.Tall(height * 0.94f), true);
                Stand(prop, "FrameBackLeft", -10.4f, 17.3f, prop.Tall(height * 0.62f), true);
                Stand(prop, "FrameBackRight", 10.8f, 17.25f, prop.Tall(height * 0.58f), false);
            }

            // Middle-sized things along the back edge and the sides, behind both parties.
            // <paramref name="size"/> is their height, or their width when <paramref name="wide"/>.
            public void Mids(StageDressing.Prop prop, float size, bool wide)
            {
                Vector2 Size(float scale) =>
                    wide ? prop.Wide(size * scale) : prop.Tall(size * scale);
                Stand(prop, "MidLeft", -10.9f, 13.2f, Size(1f), false);
                Stand(prop, "MidBackLeft", -5.6f, 17.3f, Size(0.85f), true);
                Stand(prop, "MidBackRight", 5.8f, 17.2f, Size(0.9f), false);
                Stand(prop, "MidRight", 10.6f, 14.8f, Size(1.05f), true);
            }

            // What happened here, at the back of the floor between the two parties.
            public void Story(StageDressing.Prop prop, float size, bool tall = false) =>
                Stand(prop, "Story", -0.9f, 17.1f, tall ? prop.Tall(size) : prop.Wide(size), false);

            // Small things at the rim: beside the story props and at the two front corners.
            public void Smalls(StageDressing.Prop prop, float width)
            {
                Stand(prop, "SmallBack", 2.4f, 16.9f, prop.Wide(width), false);
                Stand(prop, "SmallFrontLeft", -5.7f, 4f, prop.Wide(width * 1.15f), true);
                Stand(prop, "SmallFrontRight", 5.5f, 4.1f, prop.Wide(width * 1.05f), false);
                Stand(prop, "SmallLeft", -9.4f, 10.4f, prop.Wide(width * 0.9f), false);
                Stand(prop, "SmallRight", 9.1f, 9.6f, prop.Wide(width * 0.85f), true);
            }

            /// <summary>
            /// Tufts of grass, ferns or reeds: round the floor (<paramref name="outside"/>), or
            /// short ones in the floor's cracks, away from the characters' feet.
            /// </summary>
            public void Tufts(
                StageDressing.Prop prop,
                int count,
                float minWidth,
                float maxWidth,
                bool outside
            )
            {
                tuftMaterial = prop.Material;
                var random = new System.Random(++seed);
                var area = outside ? new Rect(-13f, 1f, 26f, 17f) : new Rect(-8f, 1.5f, 16f, 15f);
                foreach (
                    var p in Scatter(
                        ++seed,
                        count,
                        area,
                        outside ? 0.9f : 1.1f,
                        p =>
                            InView(p)
                            && (
                                outside
                                    ? (p - Centre).magnitude > 7.8f
                                    : (p - Centre).magnitude is > 2.5f and < 7.2f
                            )
                            && !StageDressing.NearAny(p, Feet, outside ? 1.3f : 1.4f)
                    )
                )
                    tufts.Add(
                        new Piece(
                            new Vector3(p.x, outside ? -0.05f : -0.01f, p.y),
                            prop.Wide(StageDressing.Range(random, minWidth, maxWidth)),
                            random.Next(2) == 0
                        )
                    );
            }

            /// <summary>Litter over the floor's rim and the ground, quarter-turned on the grid.</summary>
            public void Litter(
                StageDressing.Prop prop,
                int count,
                float minSize,
                float maxSize,
                float clear = 5.5f
            )
            {
                var random = new System.Random(++seed);
                var pieces = new List<Piece>();
                foreach (
                    var p in Scatter(
                        ++seed,
                        count,
                        new Rect(-12f, 1f, 24f, 16.4f),
                        2.3f,
                        p =>
                            InView(p)
                            && (p - Centre).magnitude > clear
                            && !StageDressing.NearAny(p, Feet, 1.9f)
                    )
                )
                {
                    float size = StageDressing.Range(random, minSize, maxSize);
                    pieces.Add(
                        new Piece(
                            new Vector3(p.x, StageDressing.DecalHeight, p.y),
                            prop.Wide(size),
                            random.Next(2) == 0,
                            random.Next(4)
                        )
                    );
                }
                Decals(Dressing, Store, "Litter", prop.Material, pieces, StageDressing.Chunk);
            }

            /// <summary>The key light, from the left so the shadows fall beside the characters.</summary>
            public Light Sun(Vector3 euler, Color color, float intensity, float shadow)
            {
                var light = KeyLight(
                    Root,
                    StageSetAssets.KeyLightName,
                    euler,
                    color,
                    intensity,
                    shadow
                );
                // Hard like the phone's (which has no soft shadows), crisp as the pixel art.
                light.shadows = LightShadows.Hard;
                return light;
            }

            /// <summary>
            /// A weak light from the front without shadows, so the characters' faces and the
            /// scenery facing the camera read in the dark (a lie of lighting, as on Top).
            /// </summary>
            public void Fill(Color color, float intensity) =>
                KeyLight(
                    Root,
                    "FillLight",
                    new Vector3(20f, 25f, 0f),
                    color,
                    intensity,
                    0f
                ).shadows = LightShadows.None;

            /// <summary>
            /// Warm light from lamps out of sight on both sides, over the two parties, so they
            /// read on a dark stage (the lamps are off the screen).
            /// </summary>
            public void Warm(Color color, float intensity)
            {
                var lamps = Group(Root, "SideLamps", Vector3.zero).transform;
                PointLight(
                    lamps,
                    "LampLeft",
                    new Vector3(-8.5f, 3.4f, 6f),
                    color,
                    intensity,
                    11f,
                    false,
                    0.12f,
                    1f
                );
                PointLight(
                    lamps,
                    "LampRight",
                    new Vector3(8.5f, 3.4f, 7.5f),
                    color,
                    intensity,
                    11f,
                    false,
                    0.12f,
                    2.5f
                );
            }

            /// <summary>
            /// Lantern stands at the back of the floor, like the dusk highland's braziers, each
            /// with a glow round its lamp and a flickering light. <paramref name="lamp"/> is
            /// where on the picture the lamp is (<see cref="Lantern"/>).
            /// </summary>
            public void Lanterns(StageDressing.Prop prop, float height, Vector2 lamp, Color color)
            {
                var lamps = Group(Root, "Lanterns", Vector3.zero).transform;
                var glow = StageSetAssets.GlowMaterial(Path("LanternGlow") + ".mat", color, 1.2f);
                var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                foreach (float side in new[] { -1f, 1f })
                {
                    var label = side < 0f ? "Left" : "Right";
                    var basePosition = new Vector3(side * 7.2f, 0f, 15.2f);
                    var size = prop.Tall(height);
                    // The right one is mirrored, its arm reaching towards the middle as well.
                    Stand(prop, "Lantern" + label, basePosition.x, basePosition.z, size, side > 0f);
                    var light =
                        basePosition + new Vector3(-side * lamp.x * size.x, lamp.y * size.y, 0f);
                    var halo = new GameObject(
                        "Glow" + label,
                        typeof(MeshFilter),
                        typeof(MeshRenderer)
                    );
                    halo.transform.SetParent(lamps, false);
                    halo.transform.position = light + Vector3.back * 0.08f;
                    halo.transform.localScale = Vector3.one * 1.1f;
                    halo.GetComponent<MeshFilter>().sharedMesh = quad;
                    var renderer = halo.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = glow;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    PointLight(
                        lamps,
                        "LanternLight" + label,
                        light + Vector3.back * 0.5f,
                        color,
                        2.4f,
                        8.5f,
                        false,
                        0.15f,
                        side * 3f
                    );
                }
                // The lamps' light lies on the ground round the stands.
                Pools(
                    "LanternPools",
                    color,
                    0.5f,
                    (-7.2f + lamp.x * prop.Tall(height).x, 15f, 3.6f, 1f),
                    (7.2f - lamp.x * prop.Tall(height).x, 15f, 3.6f, 1f)
                );
            }

            /// <summary>Iron fire bowls at the back of the floor, with fire, embers and light.</summary>
            public void Braziers(StageDressing.Prop prop, float height, float bowlHeight)
            {
                var fires = Group(Root, "Braziers", Vector3.zero).transform;
                var flame = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
                var ember = AssetDatabase.LoadAssetAtPath<Material>(EmberMaterialPath);
                foreach (float side in new[] { -1f, 1f })
                {
                    var label = side < 0f ? "Left" : "Right";
                    var basePosition = new Vector3(side * 7.2f, 0f, 15.2f);
                    Stand(
                        prop,
                        "Brazier" + label,
                        basePosition.x,
                        basePosition.z,
                        prop.Tall(height),
                        side > 0f
                    );
                    var bowl = basePosition + Vector3.up * height * bowlHeight;
                    Flame(
                        fires,
                        "Flame" + label,
                        bowl + new Vector3(0f, -0.08f, 0.06f),
                        new Vector2(0.6f, 1f),
                        flame
                    );
                    PointLight(
                        fires,
                        "FireLight" + label,
                        bowl + Vector3.up * 0.3f + Vector3.back * 0.6f,
                        new Color(1f, 0.58f, 0.26f),
                        2.8f,
                        9f,
                        false,
                        0.2f,
                        side * 5f
                    );
                    Embers(
                        fires,
                        "Embers" + label,
                        bowl + Vector3.up * 0.5f,
                        ember,
                        4f,
                        0.12f,
                        0.08f
                    );
                }
                // The fire's light lies on the floor round the stands.
                Pools(
                    "FirePools",
                    new Color(1f, 0.58f, 0.26f),
                    0.55f,
                    (-7.2f, 15f, 4f, 1f),
                    (7.2f, 15f, 4f, 1f)
                );
            }

            /// <summary>
            /// Shafts of light (god rays) from a light out of sight down to the ground at (x, z),
            /// each <c>width</c> m wide: <paramref name="travel"/> is the way the light goes and
            /// the shafts start <paramref name="height"/> m up. Drawn added on top of the stage
            /// with a colour and strength of this stage's own, and fading into the fog.
            /// </summary>
            public void Shafts(
                string name,
                Color color,
                float intensity,
                Vector3 travel,
                float height,
                params (float x, float z, float width)[] shafts
            )
            {
                var material = BeamMaterial(Path(name) + ".mat", color, intensity);
                var group = Group(Root, name, Vector3.zero).transform;
                var down = travel.normalized;
                for (int i = 0; i < shafts.Length; i++)
                {
                    var (x, z, width) = shafts[i];
                    var bottom = new Vector3(x, 0f, z);
                    LightBeam(
                        group,
                        name + i,
                        bottom - down * (height / -down.y),
                        bottom,
                        width,
                        material
                    );
                }
            }

            /// <summary>
            /// Pools of light on the ground (each x, z, size in m and strength 0-1): where a shaft
            /// lands, under a lamp or round glowing lava and crystals, a soft glow added on top
            /// of the ground, so the light is seen to fall somewhere.
            /// </summary>
            public void Pools(
                string name,
                Color color,
                float intensity,
                params (float x, float z, float size, float strength)[] pools
            )
            {
                var material = StageSetAssets.GlowMaterial(Path(name) + ".mat", color, intensity);
                var pieces = new List<Piece>();
                foreach (var (x, z, size, strength) in pools)
                    pieces.Add(
                        new Piece(
                            new Vector3(x, PoolHeight, z),
                            new Vector2(size, size),
                            false,
                            0,
                            strength
                        )
                    );
                Decals(Dressing, Store, name, material, pieces, StageDressing.Chunk);
            }

            // Above the litter and the shade, under the characters' contact shadows (1 cm).
            private const float PoolHeight = 0.008f;

            /// <summary>Litter laid at chosen places (x, z, size in m and quarter turns).</summary>
            public void Lay(
                StageDressing.Prop prop,
                string name,
                params (float x, float z, float size, int turns)[] places
            )
            {
                var pieces = new List<Piece>();
                foreach (var (x, z, size, turns) in places)
                    pieces.Add(
                        new Piece(
                            new Vector3(x, StageDressing.DecalHeight, z),
                            prop.Wide(size),
                            false,
                            turns
                        )
                    );
                Decals(Dressing, Store, name, prop.Material, pieces, StageDressing.Chunk);
            }

            private static Material BeamMaterial(string path, Color color, float intensity)
            {
                var shader = Shader.Find("Baryonyx/HD2D/Light Beam");
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool created = material == null;
                if (created)
                    material = new Material(shader);
                material.shader = shader;
                material.SetColor("_Color", color);
                material.SetFloat("_Intensity", intensity);
                material.SetFloat("_EdgeSoftness", 0.7f);
                material.SetFloat("_StripeScale", 3f);
                material.SetFloat("_StripeAmount", 0.5f);
                material.SetFloat("_Speed", 0.1f);
                if (created)
                    AssetDatabase.CreateAsset(material, path);
                else
                    EditorUtility.SetDirty(material);
                return material;
            }

            /// <summary>Specks drifting in the light over the floor (dust, pollen, wisps, sparks).</summary>
            public void Motes(Color tint, int count) =>
                Hd2dStageKit.Motes(
                    Root,
                    "Motes",
                    new Vector3(0f, 2f, 10f),
                    new Vector3(22f, 3.2f, 13f),
                    AssetDatabase.LoadAssetAtPath<Material>(MoteMaterialPath),
                    count,
                    tint
                );

            /// <summary>Snow falling slowly over the whole field.</summary>
            public void Snowfall()
            {
                var snow = Hd2dStageKit.Motes(
                    Root,
                    "Snowfall",
                    new Vector3(0f, 4.5f, 10f),
                    new Vector3(28f, 6f, 16f),
                    AssetDatabase.LoadAssetAtPath<Material>(MoteMaterialPath),
                    160,
                    new Color(0.95f, 0.97f, 1f)
                );
                var main = snow.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
                var velocity = snow.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                velocity.y = new ParticleSystem.MinMaxCurve(-0.9f, -0.55f);
                velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            }

            /// <summary>Beds the standing things in tufts, shades them and saves the prefab.</summary>
            public void Save()
            {
                if (tuftMaterial != null)
                {
                    // Tufts at the foot of everything standing: at both ends and one in front.
                    var random = new System.Random(++seed);
                    foreach (var thing in bedded)
                    {
                        float half = thing.y * 0.5f;
                        foreach (
                            var (dx, dz) in new[]
                            {
                                (-half + StageDressing.Range(random, -0.1f, 0.25f), -0.12f),
                                (half + StageDressing.Range(random, -0.25f, 0.1f), -0.1f),
                                (StageDressing.Range(random, -half * 0.6f, half * 0.6f), -0.22f),
                            }
                        )
                        {
                            var at = new Vector2(thing.x + dx, thing.z + dz);
                            if (StageDressing.NearAny(at, Feet, 1f))
                                continue;
                            float width = StageDressing.Range(random, 0.4f, 0.7f);
                            tufts.Add(
                                new Piece(
                                    new Vector3(at.x, -0.04f, at.y),
                                    new Vector2(width, width * 0.5f),
                                    random.Next(2) == 0
                                )
                            );
                        }
                    }
                }
                StageDressing.ShadeStanding(Root, Dressing, Store, 18f);
                if (tuftMaterial != null)
                    Boards(Dressing, Store, "Tufts", tuftMaterial, tufts, StageDressing.Chunk);
                // The embers, motes and snow run in the scene while it is edited too.
                stage.AddComponent<Hd2dParticlePreview>();
                PrefabUtility.SaveAsPrefabAsset(stage, set.PrefabPath);
            }

            public void Dispose() => Discard(stage);

            // Inside the camera's view at the ground's height (16:9).
            private static bool InView(Vector2 p) => Mathf.Abs(p.x) < 0.5f * (p.y + 8f) + 0.3f;
        }
    }
}
