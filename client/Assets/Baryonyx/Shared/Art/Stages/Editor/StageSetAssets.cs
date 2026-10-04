using System;
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
    /// Builds the 3D stages (HD-2D) the screens stand their pixel-art characters on: the gate on
    /// the mountain under the stars of Top, the forest glade of Home in the daytime, and the stone
    /// circle on the highland at dusk of the battle (each at its own time of day). The dungeon
    /// hall and the forest ruins, which those screens used first, are kept as stages too.
    /// Each is a prefab of a floor, walls, boxes and cut-out boards textured with pixel art
    /// (.aseprite made from Codex CLI's pictures), with its key light, torches or sunbeams; the
    /// three stages the screens use are dressed with props, tufts and litter (StageDressing). How
    /// each screen looks at it (camera, ambient light, fog, and the post-processing "look") is
    /// kept here too, beside the layout it was measured against; placing a stage in a scene is
    /// App's job (<c>Baryonyx.App.Editor.Hd2dStageSceneSetup</c>).
    /// </summary>
    public static class StageSetAssets
    {
        private const string Root = "Assets/Baryonyx/Shared/Art/Stages";
        private const string HallDirectory = Root + "/DungeonHall";
        private const string ForestDirectory = Root + "/ForestRuins";
        private const string GladeDirectory = Root + "/ForestGlade";
        private const string StarlitDirectory = Root + "/StarlitGate";
        private const string DuskDirectory = Root + "/DuskHighland";
        public const string DungeonHallPrefabPath = HallDirectory + "/DungeonHallStage.prefab";
        public const string ForestRuinsPrefabPath = ForestDirectory + "/ForestRuinsStage.prefab";
        public const string ForestGladePrefabPath = GladeDirectory + "/ForestGladeStage.prefab";
        public const string StarlitGatePrefabPath = StarlitDirectory + "/StarlitGateStage.prefab";
        public const string DuskHighlandPrefabPath = DuskDirectory + "/DuskHighlandStage.prefab";
        public const string DungeonHallLookPath = HallDirectory + "/DungeonHallLook.asset";
        public const string ForestRuinsLookPath = ForestDirectory + "/ForestRuinsLook.asset";
        public const string ForestGladeLookPath = GladeDirectory + "/ForestGladeLook.asset";
        public const string StarlitGateLookPath = StarlitDirectory + "/StarlitGateLook.asset";
        public const string DuskHighlandLookPath = DuskDirectory + "/DuskHighlandLook.asset";
        public const string KeyLightName = "KeyLight";

        /// <summary>Where a screen's camera stands and how wide it sees.</summary>
        public readonly struct StageView
        {
            public readonly Vector3 Position;
            public readonly Vector3 Euler;
            public readonly float Fov;
            public readonly float FocusDistance;

            public StageView(Vector3 position, Vector3 euler, float fov, float focusDistance)
            {
                Position = position;
                Euler = euler;
                Fov = fov;
                FocusDistance = focusDistance;
            }
        }

        /// <summary>
        /// The air of a stage: the ambient light, the fog and the colour behind it all. With
        /// <see cref="Equator"/> and <see cref="Ground"/> the ambient light is a gradient: the sky's
        /// colour from above (<see cref="Ambient"/>), the trees' from the sides and the ground's
        /// from below, so the shade under the leaves turns cool and green instead of grey.
        /// </summary>
        public readonly struct StageEnvironment
        {
            public readonly Color Ambient;
            public readonly Color Fog;
            public readonly float FogStart;
            public readonly float FogEnd;
            public readonly Color? Equator;
            public readonly Color? Ground;

            public StageEnvironment(
                Color ambient,
                Color fog,
                float fogStart,
                float fogEnd,
                Color? equator = null,
                Color? ground = null
            )
            {
                Ambient = ambient;
                Fog = fog;
                FogStart = fogStart;
                FogEnd = fogEnd;
                Equator = equator;
                Ground = ground;
            }
        }

        // Top looks from about eye height along the stone path to the gate on the mountain,
        // through a narrow-ish lens that flattens the scene a little, as HD-2D does; the sky
        // with the moon fills the top of the frame.
        public static readonly StageView TopView = new(
            new Vector3(0f, 2.6f, -4f),
            new Vector3(5f, 0f, 0f),
            42f,
            12f
        );

        // Home looks down at 18 degrees through a long lens on the camp in the glade: the
        // party's feet (24-28% of the screen's height) stand about 10 m away, the clearing and
        // the tent lie behind them, and the forest's foot meets the ground at about 75%, with
        // the canopy closing the top of the frame like a diorama's box.
        public static readonly StageView HomeView = new(
            new Vector3(0f, 4.4f, -3.1f),
            new Vector3(18f, 0f, 0f),
            32f,
            10.6f
        );

        // The battle looks down at about 20 degrees through a long lens, like Octopath
        // Traveler's battles: both parties stand on the stone stage (their feet span 30-66% of
        // the screen's height), the ground ends at about 70% and the far land and sky rise behind.
        public static readonly StageView BattleView = new(
            new Vector3(0f, 6f, -8f),
            new Vector3(20f, 0f, 0f),
            30f,
            16f
        );

        public static readonly StageEnvironment HallEnvironment = new(
            new Color(0.17f, 0.19f, 0.29f),
            new Color(0.05f, 0.06f, 0.1f),
            12f,
            42f
        );
        public static readonly StageEnvironment ForestEnvironment = new(
            new Color(0.11f, 0.13f, 0.22f),
            new Color(0.07f, 0.1f, 0.17f),
            20f,
            60f
        );

        // A night on the mountain: deep blue light from the sky, dimmer around and almost none
        // from below; a dark blue haze between the stage and the painted mountains.
        public static readonly StageEnvironment StarlitEnvironment = new(
            new Color(0.16f, 0.2f, 0.34f),
            new Color(0.07f, 0.09f, 0.17f),
            18f,
            70f,
            new Color(0.1f, 0.12f, 0.2f),
            new Color(0.04f, 0.04f, 0.06f)
        );

        // Dusk on the highland: violet light from the darkening sky, warm rose from the glow
        // round the horizon and a dark bounce from the dry ground; a rosy haze in the distance.
        public static readonly StageEnvironment DuskEnvironment = new(
            new Color(0.34f, 0.3f, 0.5f),
            new Color(0.42f, 0.3f, 0.36f),
            22f,
            75f,
            new Color(0.4f, 0.28f, 0.3f),
            new Color(0.14f, 0.1f, 0.09f)
        );

        // A bright forest in the day: blue sky light from above, green from the trees around
        // and a dark, earthy bounce from below; a pale green-blue haze swallows the far trees.
        public static readonly StageEnvironment GladeEnvironment = new(
            new Color(0.5f, 0.6f, 0.74f),
            new Color(0.62f, 0.74f, 0.72f),
            16f,
            62f,
            new Color(0.36f, 0.45f, 0.36f),
            new Color(0.2f, 0.21f, 0.14f)
        );

        [MenuItem("Baryonyx/Stages/Create 3D Stage Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();
            EnsureSharedMaterials();
            BuildDungeonHall();
            BuildForestRuins();
            BuildForestGlade();
            BuildStarlitGate();
            BuildDuskHighland();
            BuildLooks();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>Builds the stages if they have not been built yet.</summary>
        public static void EnsureAssets()
        {
            EnsureSharedMaterials();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DungeonHallPrefabPath) == null)
                BuildDungeonHall();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ForestRuinsPrefabPath) == null)
                BuildForestRuins();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ForestGladePrefabPath) == null)
                BuildForestGlade();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StarlitGatePrefabPath) == null)
                BuildStarlitGate();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DuskHighlandPrefabPath) == null)
                BuildDuskHighland();
            BuildLooks();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Sets the open scene's ambient light and fog for a stage.</summary>
        public static void ApplyEnvironment(StageEnvironment environment)
        {
            if (environment.Equator is Color equator && environment.Ground is Color ground)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = environment.Ambient;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = environment.Ambient;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = environment.Fog;
            RenderSettings.fogStartDistance = environment.FogStart;
            RenderSettings.fogEndDistance = environment.FogEnd;
            RenderSettings.skybox = null;
        }

        /// <summary>The hall of Top and Home: a long stone room with pillars and a glowing gate.</summary>
        public static void BuildDungeonHall()
        {
            AssetFolders.Ensure(HallDirectory);
            var floorTexture = PixelTexture(HallDirectory + "/DungeonFloor.aseprite", true);
            var wallTexture = PixelTexture(HallDirectory + "/DungeonWall.aseprite", true);
            var archTexture = PixelTexture(HallDirectory + "/DungeonArch.aseprite", false);
            var bannerTexture = PixelTexture(HallDirectory + "/DungeonBanner.aseprite", false);
            var torchTexture = PixelTexture(HallDirectory + "/DungeonTorch.aseprite", false);
            var floor = LitMaterial(HallDirectory + "/DungeonFloor.mat", floorTexture, Color.white);
            var wall = LitMaterial(
                HallDirectory + "/DungeonWall.mat",
                wallTexture,
                new Color(0.86f, 0.88f, 0.95f)
            );
            var pillar = LitMaterial(
                HallDirectory + "/DungeonPillar.mat",
                wallTexture,
                new Color(0.95f, 0.93f, 0.9f)
            );
            var arch = LitMaterial(
                HallDirectory + "/DungeonArch.mat",
                archTexture,
                Color.white,
                true
            );
            var banner = LitMaterial(
                HallDirectory + "/DungeonBanner.mat",
                bannerTexture,
                Color.white,
                true
            );
            var torch = LitMaterial(
                HallDirectory + "/DungeonTorch.mat",
                torchTexture,
                Color.white,
                true
            );
            var flame = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
            var ember = AssetDatabase.LoadAssetAtPath<Material>(EmberMaterialPath);
            var gateGlow = GlowMaterial(
                HallDirectory + "/DungeonGateGlow.mat",
                new Color(0.3f, 0.85f, 0.8f),
                0.8f
            );

            var store = new MeshStore(HallDirectory + "/DungeonHallStageMeshes.asset");
            var stage = new GameObject("DungeonHallStage");
            try
            {
                var root = stage.transform;
                const float width = 14f;
                const float height = 8f;
                const float backZ = 16f;
                Floor(
                    root,
                    store,
                    "Floor",
                    floor,
                    new Vector3(0f, 0f, 6f),
                    new Vector2(width, 24f),
                    4f,
                    5f
                );

                // The back wall leaves a doorway for the gate; a short dark passage lies behind it.
                const float doorHalf = 1.7f;
                const float doorHeight = 5f;
                float sideWidth = width * 0.5f - doorHalf;
                Wall(
                    root,
                    store,
                    "BackWallLeft",
                    wall,
                    new Vector3(-doorHalf - sideWidth * 0.5f, 0f, backZ),
                    new Vector2(sideWidth, height),
                    Vector3.back,
                    2.5f
                );
                Wall(
                    root,
                    store,
                    "BackWallRight",
                    wall,
                    new Vector3(doorHalf + sideWidth * 0.5f, 0f, backZ),
                    new Vector2(sideWidth, height),
                    Vector3.back,
                    2.5f
                );
                Wall(
                    root,
                    store,
                    "BackWallTop",
                    wall,
                    new Vector3(0f, doorHeight, backZ),
                    new Vector2(doorHalf * 2f, height - doorHeight),
                    Vector3.back,
                    2.5f
                );
                Floor(
                    root,
                    store,
                    "PassageFloor",
                    floor,
                    new Vector3(0f, 0.75f, backZ + 2.5f),
                    new Vector2(doorHalf * 2f, 5f),
                    4f,
                    5f
                );
                Wall(
                    root,
                    store,
                    "PassageLeft",
                    wall,
                    new Vector3(-doorHalf, 0f, backZ + 2.5f),
                    new Vector2(5f, height),
                    Vector3.right,
                    2.5f
                );
                Wall(
                    root,
                    store,
                    "PassageRight",
                    wall,
                    new Vector3(doorHalf, 0f, backZ + 2.5f),
                    new Vector2(5f, height),
                    Vector3.left,
                    2.5f
                );
                Wall(
                    root,
                    store,
                    "PassageEnd",
                    wall,
                    new Vector3(0f, 0f, backZ + 5f),
                    new Vector2(doorHalf * 2f, height),
                    Vector3.back,
                    2.5f
                );
                Flat(
                    root,
                    store,
                    "Gate",
                    arch,
                    new Vector3(0f, 0f, backZ - 0.06f),
                    new Vector2(5.6f, 7f),
                    Vector3.back,
                    false
                );
                Flat(
                    root,
                    store,
                    "GateGlow",
                    gateGlow,
                    new Vector3(0f, 0.5f, backZ + 1.5f),
                    new Vector2(doorHalf * 2.6f, doorHeight),
                    Vector3.back,
                    false
                );

                // Three steps up to the gate.
                for (int step = 0; step < 3; step++)
                {
                    float depth = 2.1f - step * 0.7f;
                    Box(
                        root,
                        store,
                        $"Step{step}",
                        pillar,
                        new Vector3(0f, step * 0.25f, backZ - depth * 0.5f),
                        new Vector3(6.4f - step * 0.6f, 0.25f, depth),
                        2.5f
                    );
                }

                Wall(
                    root,
                    store,
                    "SideWallLeft",
                    wall,
                    new Vector3(-width * 0.5f, 0f, 6f),
                    new Vector2(24f, height),
                    Vector3.right,
                    2.5f
                );
                Wall(
                    root,
                    store,
                    "SideWallRight",
                    wall,
                    new Vector3(width * 0.5f, 0f, 6f),
                    new Vector2(24f, height),
                    Vector3.left,
                    2.5f
                );

                // Pillars down both sides, each with a torch facing the camera.
                var torches = Group(root, "Torches", Vector3.zero).transform;
                float[] pillarZ = { 1.5f, 7.5f, 13f };
                for (int i = 0; i < pillarZ.Length; i++)
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var x = side * 5.1f;
                        var label = (side < 0f ? "Left" : "Right") + i;
                        Box(
                            root,
                            store,
                            "Pillar" + label,
                            pillar,
                            new Vector3(x, 0f, pillarZ[i]),
                            new Vector3(1.3f, height, 1.3f),
                            2.5f
                        );
                        var front = new Vector3(x, 0f, pillarZ[i] - 0.68f);
                        Flat(
                            torches,
                            store,
                            "Torch" + label,
                            torch,
                            front + Vector3.up * 2.1f,
                            new Vector2(0.36f, 1.26f),
                            Vector3.back,
                            false
                        );
                        var tip = front + new Vector3(0f, 3.5f, -0.05f);
                        // The fire burns on the wrapped head of the torch (its top is 3.36 m up).
                        Flame(
                            torches,
                            "Flame" + label,
                            front + new Vector3(0f, 3.3f, 0.05f),
                            new Vector2(0.3f, 0.6f),
                            flame
                        );
                        PointLight(
                            torches,
                            "TorchLight" + label,
                            tip + new Vector3(-side * 0.4f, 0f, -0.5f),
                            new Color(1f, 0.58f, 0.26f),
                            2.4f,
                            6.5f,
                            false,
                            0.22f,
                            i * 3.7f + side
                        );
                        Embers(
                            torches,
                            "Embers" + label,
                            tip + Vector3.up * 0.2f,
                            ember,
                            3f,
                            0.08f,
                            0.06f
                        );
                    }

                // Banners on the back wall either side of the gate.
                foreach (float side in new[] { -1f, 1f })
                    Flat(
                        root,
                        store,
                        side < 0f ? "BannerLeft" : "BannerRight",
                        banner,
                        new Vector3(side * 4.4f, 2.6f, backZ - 0.05f),
                        new Vector2(1.3f, 2.6f),
                        Vector3.back,
                        false
                    );

                PointLight(
                    root,
                    "GateLight",
                    new Vector3(0f, 2.4f, backZ + 1.2f),
                    new Color(0.35f, 0.85f, 0.9f),
                    3f,
                    9f,
                    false,
                    0.06f,
                    11f
                );
                KeyLight(
                    root,
                    KeyLightName,
                    new Vector3(50f, 70f, 0f),
                    new Color(0.55f, 0.65f, 1f),
                    0.6f,
                    0.85f
                );

                // The embers, motes and dust run in the scene while it is edited too.
                stage.AddComponent<Hd2dParticlePreview>();
                PrefabUtility.SaveAsPrefabAsset(stage, DungeonHallPrefabPath);
            }
            finally
            {
                Discard(stage);
            }
        }

        /// <summary>The battle's forest ruins: a round stone stage in a clearing at night.</summary>
        public static void BuildForestRuins()
        {
            AssetFolders.Ensure(ForestDirectory);
            var stageTexture = PixelTexture(ForestDirectory + "/ForestStageFloor.aseprite", false);
            var groundTexture = PixelTexture(ForestDirectory + "/ForestGround.aseprite", true);
            var backdropTexture = PixelTexture(ForestDirectory + "/ForestBackdrop.aseprite", false);
            var pillarTexture = PixelTexture(ForestDirectory + "/ForestRuinPillar.aseprite", false);
            var treeTexture = PixelTexture(ForestDirectory + "/ForestTree.aseprite", false);
            var stoneStage = LitMaterial(
                ForestDirectory + "/ForestStageFloor.mat",
                stageTexture,
                new Color(0.72f, 0.74f, 0.82f)
            );
            var ground = LitMaterial(
                ForestDirectory + "/ForestGround.mat",
                groundTexture,
                new Color(0.85f, 0.9f, 0.85f)
            );
            var backdrop = LitMaterial(
                ForestDirectory + "/ForestBackdrop.mat",
                backdropTexture,
                new Color(0.5f, 0.55f, 0.65f),
                false,
                new Color(0.75f, 0.8f, 0.95f)
            );
            var ruin = LitMaterial(
                ForestDirectory + "/ForestRuinPillar.mat",
                pillarTexture,
                Color.white,
                true
            );
            var tree = LitMaterial(
                ForestDirectory + "/ForestTree.mat",
                treeTexture,
                new Color(0.8f, 0.85f, 0.85f),
                true
            );
            var flame = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
            var ember = AssetDatabase.LoadAssetAtPath<Material>(EmberMaterialPath);

            var store = new MeshStore(ForestDirectory + "/ForestRuinsStageMeshes.asset");
            var stage = new GameObject("ForestRuinsStage");
            try
            {
                var root = stage.transform;
                // The stone stage is the ground the characters stand on (height 0); the grass
                // around it lies a little lower so the two never fight over depth.
                Floor(
                    root,
                    store,
                    "Ground",
                    ground,
                    new Vector3(0f, -0.03f, 10f),
                    new Vector2(60f, 44f),
                    3f,
                    8f
                );
                PaintedFloor(
                    root,
                    store,
                    "StoneStage",
                    stoneStage,
                    new Vector3(0f, 0f, 9f),
                    new Vector2(17f, 17f),
                    5.7f
                );

                // The far forest and ruins as wide boards whose grassy foot meets the ground at
                // about 70% of the screen's height; three side by side (the middle one mirrored)
                // cover the screen with room for the camera's drift.
                for (int tile = -1; tile <= 1; tile++)
                    Flat(
                        root,
                        store,
                        "Backdrop" + (tile + 1),
                        backdrop,
                        new Vector3(tile * 15.6f, -0.4f, 17.2f),
                        new Vector2(15.6f, 6.5f),
                        Vector3.back,
                        false,
                        tile == 0
                    );

                // Broken pillars between the stage and the backdrop, and old trees at the sides,
                // so the camera's drift parts near from far.
                (string name, float x, float z, float scale, bool mirror)[] pillars =
                {
                    ("RuinLeftFar", -11.5f, 16f, 1.1f, false),
                    ("RuinLeft", -8.4f, 15f, 0.85f, true),
                    ("RuinRight", 8.8f, 15.2f, 0.95f, false),
                    ("RuinRightFar", 12.2f, 16.2f, 1.15f, true),
                };
                foreach (var (name, x, z, scale, mirror) in pillars)
                    Flat(
                        root,
                        store,
                        name,
                        ruin,
                        new Vector3(x, 0f, z),
                        new Vector2(2.2f, 4.4f) * scale,
                        Vector3.back,
                        true,
                        mirror
                    );
                Flat(
                    root,
                    store,
                    "TreeLeft",
                    tree,
                    new Vector3(-14.8f, 0f, 16.4f),
                    new Vector2(8.6f, 11f),
                    Vector3.back,
                    true,
                    true
                );
                Flat(
                    root,
                    store,
                    "TreeRight",
                    tree,
                    new Vector3(15.2f, 0f, 16.6f),
                    new Vector2(8.6f, 11f),
                    Vector3.back,
                    true
                );

                // Fires burning on two broken columns at the back of the stage give warm light
                // against the moonlight.
                var fires = Group(root, "Beacons", Vector3.zero).transform;
                foreach (float side in new[] { -1f, 1f })
                {
                    var label = side < 0f ? "Left" : "Right";
                    var basePosition = new Vector3(side * 7.2f, 0f, 15.2f);
                    Flat(
                        root,
                        store,
                        "BeaconColumn" + label,
                        ruin,
                        basePosition,
                        new Vector2(1.1f, 2.2f),
                        Vector3.back,
                        true,
                        side > 0f
                    );
                    var tip = basePosition + Vector3.up * 2.35f;
                    Flame(
                        fires,
                        "Flame" + label,
                        basePosition + new Vector3(0f, 2.12f, 0.06f),
                        new Vector2(0.7f, 1.15f),
                        flame
                    );
                    PointLight(
                        fires,
                        "FireLight" + label,
                        tip + Vector3.back * 0.6f,
                        new Color(1f, 0.55f, 0.25f),
                        3.2f,
                        9f,
                        false,
                        0.2f,
                        side * 5f
                    );
                    Embers(
                        fires,
                        "Embers" + label,
                        tip + Vector3.up * 0.3f,
                        ember,
                        4f,
                        0.12f,
                        0.08f
                    );
                }

                PointLight(
                    root,
                    "CircleGlow",
                    new Vector3(0f, 1.2f, 9f),
                    new Color(0.45f, 0.65f, 1f),
                    1f,
                    9f,
                    false,
                    0.05f,
                    3f
                );
                // The moon shines from the upper left (the art's light comes from the upper left),
                // so the shadows fall to the right beside the characters: not under the bars at
                // their feet (light from behind) nor hidden behind their own boards (from the front).
                KeyLight(
                    root,
                    KeyLightName,
                    new Vector3(45f, 75f, 0f),
                    new Color(0.62f, 0.72f, 1f),
                    1.1f,
                    0.9f
                );

                // The embers, motes and dust run in the scene while it is edited too.
                stage.AddComponent<Hd2dParticlePreview>();
                PrefabUtility.SaveAsPrefabAsset(stage, ForestRuinsPrefabPath);
            }
            finally
            {
                Discard(stage);
            }
        }

        /// <summary>
        /// Home's camp in a forest glade in the daytime: a trodden clearing in the grass, a tent
        /// and logs behind the party, old trees and the deep forest around, the canopy over the
        /// top of the frame, and the sun falling through the leaves in dappled light and beams.
        /// </summary>
        public static void BuildForestGlade()
        {
            AssetFolders.Ensure(GladeDirectory);
            var grassTexture = PixelTexture(GladeDirectory + "/GladeGrass.aseprite", true);
            var clearingTexture = PixelTexture(GladeDirectory + "/GladeClearing.aseprite", false);
            var backdropTexture = PixelTexture(GladeDirectory + "/GladeBackdrop.aseprite", false);
            var treeTexture = PixelTexture(GladeDirectory + "/GladeTree.aseprite", false);
            var bushTexture = PixelTexture(GladeDirectory + "/GladeBush.aseprite", false);
            var canopyTexture = PixelTexture(GladeDirectory + "/GladeCanopy.aseprite", false);
            var logTexture = PixelTexture(GladeDirectory + "/GladeLog.aseprite", false);
            var tentTexture = PixelTexture(GladeDirectory + "/GladeTent.aseprite", false);
            // The ground, the bark, the leaves and the canvas take a relief (normal map) worked
            // out from their pictures, so the sun and the fire pick out their bumps.
            var grass = LitMaterial(
                GladeDirectory + "/GladeGrass.mat",
                grassTexture,
                Color.white,
                false,
                null,
                ReliefMap(grassTexture, 2f)
            );
            var clearing = LitMaterial(
                GladeDirectory + "/GladeClearing.mat",
                clearingTexture,
                Color.white,
                true,
                null,
                ReliefMap(clearingTexture, 2.5f)
            );
            // The far forest faces the camera with the sun beside and behind it, so it lights
            // itself a little to read as the bright, hazy depth of the woods.
            var backdrop = LitMaterial(
                GladeDirectory + "/GladeBackdrop.mat",
                backdropTexture,
                new Color(0.7f, 0.8f, 0.74f),
                false,
                new Color(0.55f, 0.62f, 0.55f)
            );
            var tree = LitMaterial(
                GladeDirectory + "/GladeTree.mat",
                treeTexture,
                Color.white,
                true,
                null,
                ReliefMap(treeTexture, 2f, 1.2f)
            );
            // The undergrowth and the canopy let the sun through their leaves and sway a little.
            var bush = FoliageMaterial(
                GladeDirectory + "/GladeBush.mat",
                bushTexture,
                Color.white,
                0.03f
            );
            // The leaves over the camera are seen from below, against the light: darker.
            var canopy = FoliageMaterial(
                GladeDirectory + "/GladeCanopy.mat",
                canopyTexture,
                new Color(0.62f, 0.7f, 0.6f),
                0.06f,
                0.35f,
                true
            );
            var log = LitMaterial(
                GladeDirectory + "/GladeLog.mat",
                logTexture,
                Color.white,
                true,
                null,
                ReliefMap(logTexture, 2.5f, 1.2f)
            );
            var tent = LitMaterial(
                GladeDirectory + "/GladeTent.mat",
                tentTexture,
                Color.white,
                true,
                null,
                ReliefMap(tentTexture, 2f, 1.2f)
            );
            var beam = AssetDatabase.LoadAssetAtPath<Material>(LightBeamMaterialPath);
            var mote = AssetDatabase.LoadAssetAtPath<Material>(MoteMaterialPath);

            var store = new MeshStore(GladeDirectory + "/ForestGladeStageMeshes.asset");
            var stage = new GameObject("ForestGladeStage");
            try
            {
                var root = stage.transform;
                // The clearing is the ground the party stands on (height 0); the grass round it
                // lies a little lower so the two never fight over depth.
                Floor(
                    root,
                    store,
                    "Grass",
                    grass,
                    new Vector3(0f, -0.02f, 16f),
                    new Vector2(56f, 44f),
                    2.75f,
                    8f
                );
                PaintedFloor(
                    root,
                    store,
                    "Clearing",
                    clearing,
                    new Vector3(0f, 0f, 8.2f),
                    new Vector2(9.6f, 8.8f),
                    4.8f
                );

                // The deep forest as wide boards whose foot meets the ground at about 75% of the
                // screen's height; three side by side (the middle one mirrored) cover the screen
                // with room for the camera's sway.
                for (int tile = -1; tile <= 1; tile++)
                    Flat(
                        root,
                        store,
                        "Backdrop" + (tile + 1),
                        backdrop,
                        new Vector3(tile * 19.2f, -0.3f, 25f),
                        new Vector2(19.2f, 8f),
                        Vector3.back,
                        false,
                        tile == 0
                    );

                // Old trees at three depths, so the sway parts near from far: trunks at the sides
                // of the clearing near the edges of the frame, and more towards the forest.
                (string name, float x, float z, float height, bool mirror)[] trees =
                {
                    ("TreeSideLeft", -6.8f, 8.5f, 11f, false),
                    ("TreeSideRight", 7.2f, 9.5f, 11.5f, true),
                    ("TreeMidLeft", -9.5f, 15f, 11f, true),
                    ("TreeMidRight", 9.2f, 16.5f, 11.5f, false),
                    ("TreeFarLeft", -15.5f, 21f, 12f, false),
                    ("TreeFarRight", 16f, 22f, 12f, true),
                };
                foreach (var (name, x, z, height, mirror) in trees)
                    Flat(
                        root,
                        store,
                        name,
                        tree,
                        new Vector3(x, -0.05f, z),
                        new Vector2(height * 0.675f, height),
                        Vector3.back,
                        true,
                        mirror
                    );

                // The camp behind the party: a tent and two logs by the fire.
                Flat(
                    root,
                    store,
                    "Tent",
                    tent,
                    new Vector3(-3.4f, 0f, 12.4f),
                    new Vector2(4.2f, 2.17f),
                    Vector3.back
                );
                Flat(
                    root,
                    store,
                    "LogRight",
                    log,
                    new Vector3(3.3f, 0f, 10.6f),
                    new Vector2(3f, 0.93f),
                    Vector3.back
                );
                Flat(
                    root,
                    store,
                    "LogBack",
                    log,
                    new Vector3(0.6f, 0f, 13.6f),
                    new Vector2(2.6f, 0.8f),
                    Vector3.back,
                    true,
                    true
                );

                // Undergrowth round the clearing. None stands in front of the camera: a bush there
                // blurs far more than the ground just behind its top and reads as a dark blurred
                // band across the bottom of the frame, so the ground alone blurs towards it.
                (string name, float x, float z, float width, bool mirror)[] bushes =
                {
                    ("BushLeft", -7f, 10.5f, 3.8f, true),
                    ("BushRight", 7.4f, 11.5f, 4f, false),
                    ("BushBackLeft", -5.6f, 17.5f, 4.2f, false),
                    ("BushBack", 1.8f, 18.5f, 4.4f, true),
                    ("BushBackRight", 6.4f, 19.5f, 4.2f, false),
                };
                foreach (var (name, x, z, width, mirror) in bushes)
                    Flat(
                        root,
                        store,
                        name,
                        bush,
                        new Vector3(x, -0.02f, z),
                        new Vector2(width, width * 70f / 160f),
                        Vector3.back,
                        true,
                        mirror
                    );

                // The canopy hangs into the top of the frame; it casts no shadow of its own (the
                // sun's leaf cookie dapples the ground instead).
                (string name, float x, float z, float bottom, float width, bool mirror)[] leaves =
                {
                    ("CanopyNearLeft", -3.6f, 2.6f, 3.4f, 8.4f, false),
                    ("CanopyNearRight", 4.2f, 3f, 3.6f, 8.4f, true),
                    ("CanopyFar", 0f, 14f, 4.6f, 16f, false),
                };
                foreach (var (name, x, z, bottom, width, mirror) in leaves)
                    Flat(
                        root,
                        store,
                        name,
                        canopy,
                        new Vector3(x, bottom, z),
                        new Vector2(width, width * 120f / 336f),
                        Vector3.back,
                        false,
                        mirror
                    );

                // The sun comes from the upper left behind the camera, so the shadows fall to the
                // right beside the characters and back into the glade; its light is dappled by
                // the leaves overhead.
                var sun = KeyLight(
                    root,
                    KeyLightName,
                    new Vector3(42f, 62f, 0f),
                    new Color(1f, 0.93f, 0.78f),
                    1.45f,
                    0.85f
                );
                // Hard like the phone's (which has no soft shadows), crisp as the pixel art.
                sun.shadows = LightShadows.Hard;
                LeafCookie(sun, 12f);

                // Beams of sunlight through gaps in the canopy, slanting the same way as the sun,
                // with dust and pollen floating in the light over the camp.
                var light = Group(root, "Sunbeams", Vector3.zero).transform;
                var towardsSun = -sun.transform.forward;
                (float x, float z, float width)[] beams =
                {
                    (-6.5f, 9f, 1.6f),
                    (-2.6f, 15f, 2.4f),
                    (2.2f, 11.5f, 1.3f),
                    (5.6f, 16f, 2f),
                    (9f, 12f, 1.5f),
                };
                for (int i = 0; i < beams.Length; i++)
                {
                    var (x, z, width) = beams[i];
                    var bottom = new Vector3(x, 0f, z);
                    LightBeam(
                        light,
                        "Beam" + i,
                        bottom + towardsSun * (9.5f / towardsSun.y),
                        bottom,
                        width,
                        beam
                    );
                }
                Motes(
                    light,
                    "Motes",
                    new Vector3(0f, 2.2f, 10f),
                    new Vector3(15f, 3.6f, 10f),
                    mote,
                    70
                );

                StageDressing.ForestGlade(root, store, GladeDirectory);

                // The embers, motes and dust run in the scene while it is edited too.
                stage.AddComponent<Hd2dParticlePreview>();
                PrefabUtility.SaveAsPrefabAsset(stage, ForestGladePrefabPath);
            }
            finally
            {
                Discard(stage);
            }
        }

        /// <summary>
        /// Top's night on the mountain: a stone path between torches up a few steps to an ancient
        /// gate glowing from within, cliffs either side, and the snowy range and the starry sky
        /// with the full moon painted in the distance. The moon lights the scene from behind the
        /// gate, so the faces turned to the camera are lit by the torches.
        /// </summary>
        public static void BuildStarlitGate()
        {
            AssetFolders.Ensure(StarlitDirectory);
            var skyTexture = PixelTexture(StarlitDirectory + "/StarlitSky.aseprite", false);
            var mountainsTexture = PixelTexture(
                StarlitDirectory + "/StarlitMountains.aseprite",
                false
            );
            var groundTexture = PixelTexture(StarlitDirectory + "/StarlitGround.aseprite", true);
            var pathTexture = PixelTexture(StarlitDirectory + "/StarlitPath.aseprite", true);
            var archTexture = PixelTexture(StarlitDirectory + "/StarlitArch.aseprite", false);
            var cliffTexture = PixelTexture(StarlitDirectory + "/StarlitCliff.aseprite", false);
            var rocksTexture = PixelTexture(StarlitDirectory + "/StarlitRocks.aseprite", false);
            var postTexture = PixelTexture(StarlitDirectory + "/StarlitTorchPost.aseprite", false);
            // The sky and the far range are painted at night already; the stars bloom and twinkle.
            var sky = PaintedMaterial(
                StarlitDirectory + "/StarlitSky.mat",
                skyTexture,
                Color.white,
                2.6f,
                0.7f,
                1.6f
            );
            var mountains = PaintedMaterial(
                StarlitDirectory + "/StarlitMountains.mat",
                mountainsTexture,
                new Color(0.82f, 0.86f, 0.95f)
            );
            // The torches rake across the paving's joints and the carved stone (normal maps
            // worked out from the pictures).
            var ground = LitMaterial(
                StarlitDirectory + "/StarlitGround.mat",
                groundTexture,
                Color.white,
                false,
                null,
                ReliefMap(groundTexture, 2f)
            );
            var path = LitMaterial(
                StarlitDirectory + "/StarlitPath.mat",
                pathTexture,
                Color.white,
                false,
                null,
                ReliefMap(pathTexture, 3f)
            );
            var arch = LitMaterial(
                StarlitDirectory + "/StarlitArch.mat",
                archTexture,
                Color.white,
                true,
                null,
                ReliefMap(archTexture, 2.5f, 1.2f)
            );
            var cliff = LitMaterial(
                StarlitDirectory + "/StarlitCliff.mat",
                cliffTexture,
                new Color(0.86f, 0.88f, 0.95f),
                true,
                null,
                ReliefMap(cliffTexture, 2.5f, 1.2f)
            );
            var rocks = LitMaterial(
                StarlitDirectory + "/StarlitRocks.mat",
                rocksTexture,
                Color.white,
                true,
                null,
                ReliefMap(rocksTexture, 2.5f, 1.2f)
            );
            var post = LitMaterial(
                StarlitDirectory + "/StarlitTorchPost.mat",
                postTexture,
                Color.white,
                true,
                null,
                ReliefMap(postTexture, 2f, 1.2f)
            );
            var flame = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
            var ember = AssetDatabase.LoadAssetAtPath<Material>(EmberMaterialPath);
            var mote = AssetDatabase.LoadAssetAtPath<Material>(MoteMaterialPath);
            var gateGlow = GlowMaterial(
                StarlitDirectory + "/StarlitGateGlow.mat",
                new Color(0.42f, 0.62f, 1f),
                0.9f
            );

            var store = new MeshStore(StarlitDirectory + "/StarlitGateStageMeshes.asset");
            var stage = new GameObject("StarlitGateStage");
            try
            {
                var root = stage.transform;
                Floor(
                    root,
                    store,
                    "Ground",
                    ground,
                    new Vector3(0f, -0.02f, 20f),
                    new Vector2(72f, 56f),
                    3f,
                    8f
                );
                // The path from the camera to the steps, a little above the grass.
                Floor(
                    root,
                    store,
                    "Path",
                    path,
                    new Vector3(0f, 0f, 6.4f),
                    new Vector2(3.4f, 14.4f),
                    2.6f,
                    4.8f
                );

                // Four steps up to the platform the gate stands on.
                const float platformFront = 15.6f;
                const float platformHeight = 1.2f;
                for (int step = 0; step < 4; step++)
                {
                    float depth = 2.4f - step * 0.6f;
                    Box(
                        root,
                        store,
                        $"Step{step}",
                        path,
                        new Vector3(0f, step * 0.3f, platformFront - depth * 0.5f),
                        new Vector3(5.2f - step * 0.4f, 0.3f, depth),
                        2.6f
                    );
                }
                Box(
                    root,
                    store,
                    "Platform",
                    path,
                    new Vector3(0f, 0f, platformFront + 3f),
                    new Vector3(10f, platformHeight, 6f),
                    2.6f
                );
                const float gateZ = 19f;
                Flat(
                    root,
                    store,
                    "Gate",
                    arch,
                    new Vector3(0f, platformHeight, gateZ),
                    new Vector2(6.7f, 7f),
                    Vector3.back
                );
                Flat(
                    root,
                    store,
                    "GateGlow",
                    gateGlow,
                    new Vector3(0f, platformHeight + 0.2f, gateZ + 0.3f),
                    new Vector2(3f, 4.6f),
                    Vector3.back,
                    false
                );
                PointLight(
                    root,
                    "GateLight",
                    new Vector3(0f, platformHeight + 2.4f, gateZ - 0.8f),
                    new Color(0.5f, 0.7f, 1f),
                    3f,
                    9f,
                    false,
                    0.06f,
                    11f
                );

                // Cliffs either side of the gate, and rocks at the near corners.
                Flat(
                    root,
                    store,
                    "CliffLeft",
                    cliff,
                    new Vector3(-9.6f, -0.3f, 22f),
                    new Vector2(10f, 16.5f),
                    Vector3.back
                );
                Flat(
                    root,
                    store,
                    "CliffRight",
                    cliff,
                    new Vector3(10.4f, -0.3f, 24f),
                    new Vector2(11f, 18.1f),
                    Vector3.back,
                    true,
                    true
                );
                Flat(
                    root,
                    store,
                    "RocksNearLeft",
                    rocks,
                    new Vector3(-4.6f, -0.02f, 2.6f),
                    new Vector2(4f, 1.5f),
                    Vector3.back
                );
                Flat(
                    root,
                    store,
                    "RocksNearRight",
                    rocks,
                    new Vector3(5f, -0.02f, 3.4f),
                    new Vector2(4.4f, 1.64f),
                    Vector3.back,
                    true,
                    true
                );
                Flat(
                    root,
                    store,
                    "RocksLeft",
                    rocks,
                    new Vector3(-6.4f, -0.02f, 13f),
                    new Vector2(5f, 1.86f),
                    Vector3.back,
                    true,
                    true
                );
                Flat(
                    root,
                    store,
                    "RocksRight",
                    rocks,
                    new Vector3(6.8f, -0.02f, 12f),
                    new Vector2(5f, 1.86f),
                    Vector3.back
                );

                // Torches along the path and either side of the steps.
                var torches = Group(root, "Torches", Vector3.zero).transform;
                (float x, float y, float z)[] posts =
                {
                    (-2.2f, 0f, 3.8f),
                    (2.2f, 0f, 3.8f),
                    (-2.2f, 0f, 9.2f),
                    (2.2f, 0f, 9.2f),
                    (-3f, platformHeight, 16.2f),
                    (3f, platformHeight, 16.2f),
                };
                for (int i = 0; i < posts.Length; i++)
                {
                    var (x, y, z) = posts[i];
                    var foot = new Vector3(x, y, z);
                    Flat(
                        torches,
                        store,
                        "TorchPost" + i,
                        post,
                        foot,
                        new Vector2(0.5f, 1.45f),
                        Vector3.back
                    );
                    var tip = foot + new Vector3(0f, 1.6f, -0.05f);
                    // The fire burns inside the bowl (its rim is 1.43 m up) and fills most of it.
                    Flame(
                        torches,
                        "Flame" + i,
                        foot + new Vector3(0f, 1.33f, 0.06f),
                        new Vector2(0.38f, 0.62f),
                        flame
                    );
                    PointLight(
                        torches,
                        "TorchLight" + i,
                        tip + new Vector3(0f, 0.1f, -0.4f),
                        new Color(1f, 0.58f, 0.26f),
                        2.2f,
                        6f,
                        false,
                        0.22f,
                        i * 3.1f
                    );
                    Embers(
                        torches,
                        "Embers" + i,
                        tip + Vector3.up * 0.15f,
                        ember,
                        2.5f,
                        0.06f,
                        0.05f
                    );
                }

                // The snowy range and the sky, painted at night, far behind everything.
                for (int tile = -1; tile <= 1; tile++)
                    Flat(
                        root,
                        store,
                        "Mountains" + (tile + 1),
                        mountains,
                        new Vector3(tile * 48f, -2f, 58f),
                        new Vector2(48f, 10.1f),
                        Vector3.back,
                        false,
                        tile == 0
                    );
                // The full moon (at 81% across and 79% up the picture) rises right behind
                // the gate; a second tile to the right fills the rest of the sky (its edges meet).
                const float skyWidth = 120f;
                const float skyHeight = 40f;
                var moon = new Vector2(0.81f, 0.79f);
                float skyLeft = -(moon.x - 0.5f) * skyWidth;
                float skyBottom = 18.5f - moon.y * skyHeight;
                for (int tile = 0; tile < 2; tile++)
                    Flat(
                        root,
                        store,
                        "Sky" + tile,
                        sky,
                        new Vector3(skyLeft + tile * skyWidth, skyBottom, 74f),
                        new Vector2(skyWidth, skyHeight),
                        Vector3.back,
                        false
                    );

                // Fireflies drifting over the grass along the path.
                Motes(
                    root,
                    "Fireflies",
                    new Vector3(0f, 1.1f, 9f),
                    new Vector3(13f, 1.8f, 12f),
                    mote,
                    36,
                    new Color(0.7f, 1f, 0.55f)
                );

                // The moon shines from behind the gate, down towards the camera; the faces turned
                // to the camera would be black, so a weak cool fill from the front (a lie of the
                // lighting, with no shadows) keeps the cliffs and the gate readable.
                KeyLight(
                    root,
                    KeyLightName,
                    new Vector3(30f, 180f, 0f),
                    new Color(0.62f, 0.72f, 1f),
                    0.75f,
                    0.9f
                ).shadows = LightShadows.Hard;
                KeyLight(
                    root,
                    "FillLight",
                    new Vector3(20f, 25f, 0f),
                    new Color(0.42f, 0.52f, 0.9f),
                    0.32f,
                    0f
                ).shadows = LightShadows.None;

                StageDressing.StarlitGate(root, store, StarlitDirectory, platformHeight);

                // The embers, motes and dust run in the scene while it is edited too.
                stage.AddComponent<Hd2dParticlePreview>();
                PrefabUtility.SaveAsPrefabAsset(stage, StarlitGatePrefabPath);
            }
            finally
            {
                Discard(stage);
            }
        }

        /// <summary>
        /// The battle's stone circle on the highland at dusk: a round sandstone stage in dry
        /// grass, standing stones and fire bowls at the back, rock spires and dead trees at the
        /// sides, and the far mesas under the sunset sky painted beyond. The low sun from the left
        /// throws long shadows to the right.
        /// </summary>
        public static void BuildDuskHighland()
        {
            AssetFolders.Ensure(DuskDirectory);
            var groundTexture = PixelTexture(DuskDirectory + "/DuskGround.aseprite", true);
            var arenaTexture = PixelTexture(DuskDirectory + "/DuskArena.aseprite", false);
            var backdropTexture = PixelTexture(DuskDirectory + "/DuskBackdrop.aseprite", false);
            var menhirTexture = PixelTexture(DuskDirectory + "/DuskMenhir.aseprite", false);
            var spireTexture = PixelTexture(DuskDirectory + "/DuskSpire.aseprite", false);
            var treeTexture = PixelTexture(DuskDirectory + "/DuskTree.aseprite", false);
            var brazierTexture = PixelTexture(DuskDirectory + "/DuskBrazier.aseprite", false);
            // The low sun rakes across the cracked ground and the stones' joints (normal maps
            // worked out from the pictures); the circle's rim rounds off like a raised slab.
            var ground = LitMaterial(
                DuskDirectory + "/DuskGround.mat",
                groundTexture,
                Color.white,
                false,
                null,
                ReliefMap(groundTexture, 2f)
            );
            // The sandstone is pale; a little darker it keeps the characters and effects in front.
            var arena = LitMaterial(
                DuskDirectory + "/DuskArena.mat",
                arenaTexture,
                new Color(0.82f, 0.8f, 0.82f),
                true,
                null,
                ReliefMap(arenaTexture, 2.5f, 0.8f)
            );
            var backdrop = PaintedMaterial(
                DuskDirectory + "/DuskBackdrop.mat",
                backdropTexture,
                new Color(0.9f, 0.86f, 0.9f)
            );
            var menhir = LitMaterial(
                DuskDirectory + "/DuskMenhir.mat",
                menhirTexture,
                Color.white,
                true,
                null,
                ReliefMap(menhirTexture, 2.5f, 1.2f)
            );
            var spire = LitMaterial(
                DuskDirectory + "/DuskSpire.mat",
                spireTexture,
                Color.white,
                true,
                null,
                ReliefMap(spireTexture, 2.5f, 1.2f)
            );
            var tree = LitMaterial(
                DuskDirectory + "/DuskTree.mat",
                treeTexture,
                Color.white,
                true,
                null,
                ReliefMap(treeTexture, 2f, 1.2f)
            );
            var brazier = LitMaterial(
                DuskDirectory + "/DuskBrazier.mat",
                brazierTexture,
                Color.white,
                true,
                null,
                ReliefMap(brazierTexture, 2f, 1.2f)
            );
            var flame = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
            var ember = AssetDatabase.LoadAssetAtPath<Material>(EmberMaterialPath);
            var mote = AssetDatabase.LoadAssetAtPath<Material>(MoteMaterialPath);

            var store = new MeshStore(DuskDirectory + "/DuskHighlandStageMeshes.asset");
            var stage = new GameObject("DuskHighlandStage");
            try
            {
                var root = stage.transform;
                // The stone stage is the ground the characters stand on (height 0); the dry grass
                // round it lies a little lower so the two never fight over depth.
                Floor(
                    root,
                    store,
                    "Ground",
                    ground,
                    new Vector3(0f, -0.03f, 10f),
                    new Vector2(64f, 44f),
                    3f,
                    8f
                );
                PaintedFloor(
                    root,
                    store,
                    "StoneCircle",
                    arena,
                    new Vector3(0f, 0f, 9f),
                    new Vector2(17f, 17f),
                    5.7f
                );

                // The far land and the sunset sky as wide boards, sunk so the ground meets them
                // at about 70% of the screen's height and the mesas lie low on the horizon with
                // the sky above; three side by side (the middle one mirrored).
                for (int tile = -1; tile <= 1; tile++)
                    Flat(
                        root,
                        store,
                        "Backdrop" + (tile + 1),
                        backdrop,
                        new Vector3(tile * 19.2f, -2.4f, 17.6f),
                        new Vector2(19.2f, 8f),
                        Vector3.back,
                        false,
                        tile == 0
                    );

                // Standing stones round the back of the circle, and spires and dead trees at the
                // sides, so the camera's drift parts near from far.
                (string name, float x, float z, float height, bool mirror)[] stones =
                {
                    ("StoneLeftFar", -11.6f, 16f, 3.4f, false),
                    ("StoneLeft", -8.6f, 15f, 2.8f, true),
                    ("StoneBackLeft", -4.6f, 16.8f, 2.2f, false),
                    ("StoneBackRight", 4.9f, 17f, 2.4f, true),
                    ("StoneRight", 8.9f, 15.2f, 3f, false),
                    ("StoneRightFar", 12.3f, 16.2f, 3.6f, true),
                };
                foreach (var (name, x, z, height, mirror) in stones)
                    Flat(
                        root,
                        store,
                        name,
                        menhir,
                        new Vector3(x, -0.02f, z),
                        new Vector2(height * 52f / 128f, height),
                        Vector3.back,
                        true,
                        mirror
                    );
                Flat(
                    root,
                    store,
                    "SpireLeft",
                    spire,
                    new Vector3(-15.4f, -0.05f, 16.6f),
                    new Vector2(7.3f, 12.8f),
                    Vector3.back
                );
                Flat(
                    root,
                    store,
                    "SpireRight",
                    spire,
                    new Vector3(15.8f, -0.05f, 17f),
                    new Vector2(7.6f, 13.3f),
                    Vector3.back,
                    true,
                    true
                );
                Flat(
                    root,
                    store,
                    "TreeLeft",
                    tree,
                    new Vector3(-12.2f, -0.03f, 14f),
                    new Vector2(4.7f, 6f),
                    Vector3.back,
                    true,
                    true
                );
                Flat(
                    root,
                    store,
                    "TreeRight",
                    tree,
                    new Vector3(12.6f, -0.03f, 13.4f),
                    new Vector2(4.4f, 5.6f),
                    Vector3.back
                );

                // Fire bowls at the back of the circle give warm light against the dusk.
                var fires = Group(root, "Braziers", Vector3.zero).transform;
                foreach (float side in new[] { -1f, 1f })
                {
                    var label = side < 0f ? "Left" : "Right";
                    var basePosition = new Vector3(side * 7.2f, 0f, 15.2f);
                    Flat(
                        root,
                        store,
                        "Brazier" + label,
                        brazier,
                        basePosition,
                        new Vector2(1f, 2.15f),
                        Vector3.back,
                        true,
                        side > 0f
                    );
                    var tip = basePosition + Vector3.up * 2.15f;
                    // The fire burns in the iron basket (1.8 to 2.13 m up) and shows between its bars.
                    Flame(
                        fires,
                        "Flame" + label,
                        basePosition + new Vector3(0f, 1.86f, 0.06f),
                        new Vector2(0.7f, 1.15f),
                        flame
                    );
                    PointLight(
                        fires,
                        "FireLight" + label,
                        tip + Vector3.back * 0.6f,
                        new Color(1f, 0.55f, 0.25f),
                        2.6f,
                        9f,
                        false,
                        0.2f,
                        side * 5f
                    );
                    Embers(
                        fires,
                        "Embers" + label,
                        tip + Vector3.up * 0.3f,
                        ember,
                        4f,
                        0.12f,
                        0.08f
                    );
                }

                // Dust drifting in the low warm light over the circle.
                Motes(
                    root,
                    "Dust",
                    new Vector3(0f, 2f, 10f),
                    new Vector3(20f, 3f, 12f),
                    mote,
                    44,
                    new Color(1f, 0.78f, 0.55f)
                );

                // The low sun from the left, a little in front, throws the shadows long to the
                // right beside the characters.
                KeyLight(
                    root,
                    KeyLightName,
                    new Vector3(30f, 72f, 0f),
                    new Color(1f, 0.62f, 0.38f),
                    1.15f,
                    0.85f
                ).shadows = LightShadows.Hard;

                StageDressing.DuskHighland(root, store, DuskDirectory);

                // The embers, motes and dust run in the scene while it is edited too.
                stage.AddComponent<Hd2dParticlePreview>();
                PrefabUtility.SaveAsPrefabAsset(stage, DuskHighlandPrefabPath);
            }
            finally
            {
                Discard(stage);
            }
        }

        /// <summary>
        /// The post-processing of each stage (its "look"), after HD-2D's camera: a lens that
        /// keeps the characters' depth sharp and blurs what is nearer and farther, light that
        /// blooms past white, a deep vignette, and a grade with cool shadows and warm lights.
        /// </summary>
        public static void BuildLooks()
        {
            Profile(
                ForestGladeLookPath,
                profile =>
                {
                    // Sharp from the party's feet (9.7 m) back past the tent; the ground in front of
                    // them blurs towards the bottom of the frame. The blur starts right under the
                    // front feet and grows slowly over the whole strip to the bottom edge (7.6 m,
                    // about 11 px there), so the dots soften step by step instead of at once.
                    AddFocus(profile, HomeView.FocusDistance, 0.9f, 4.2f, 11f, 20f, 7f);
                    // Sunlit leaves and the beams pass white on the HDR camera and bloom softly.
                    var bloom = profile.Add<Bloom>(true);
                    bloom.threshold.Override(0.95f);
                    bloom.intensity.Override(0.75f);
                    bloom.scatter.Override(0.72f);
                    bloom.tint.Override(new Color(1f, 0.93f, 0.8f));
                    bloom.highQualityFiltering.Override(false);
                    bloom.downscale.Override(BloomDownscaleMode.Half);
                    bloom.maxIterations.Override(6);
                    AddGrade(
                        profile,
                        0.1f,
                        16f,
                        14f,
                        6f,
                        new Color(0.4f, 0.48f, 0.58f),
                        new Color(0.62f, 0.55f, 0.44f),
                        0f
                    );
                    AddVignette(profile, 0.34f, new Color(0.02f, 0.05f, 0.03f));
                }
            );
            Profile(
                StarlitGateLookPath,
                profile =>
                {
                    // The sky stays nearly sharp so the stars read; the near rocks blur.
                    AddFocus(profile, TopView.FocusDistance + 2f, 5f, 4f, 14f, 12f, 3f);
                    var bloom = profile.Add<Bloom>(true);
                    bloom.threshold.Override(0.85f);
                    bloom.intensity.Override(1.3f);
                    bloom.scatter.Override(0.7f);
                    bloom.highQualityFiltering.Override(false);
                    bloom.downscale.Override(BloomDownscaleMode.Half);
                    bloom.maxIterations.Override(6);
                    AddGrade(
                        profile,
                        0f,
                        14f,
                        6f,
                        0f,
                        new Color(0.4f, 0.45f, 0.62f),
                        new Color(0.62f, 0.54f, 0.42f),
                        0f
                    );
                    AddVignette(profile, 0.38f, new Color(0.01f, 0.02f, 0.06f));
                }
            );
            Profile(
                DuskHighlandLookPath,
                profile =>
                {
                    // Sharp from the front row's feet (13.6 m) to the back row's (23 m); the ground
                    // below them, where the hand of cards lies, blurs towards the bottom. The blur
                    // grows over the whole strip to the bottom edge (10.1 m) instead of reaching
                    // its largest radius well above it.
                    AddFocus(profile, 18.2f, 4.7f, 3.8f, 9f, 12f, 5f);
                    AddGrade(
                        profile,
                        0f,
                        12f,
                        8f,
                        0f,
                        new Color(0.42f, 0.42f, 0.6f),
                        new Color(0.64f, 0.52f, 0.4f),
                        0f
                    );
                    AddVignette(profile, 0.34f, new Color(0.06f, 0.02f, 0.06f));
                }
            );
            Profile(
                DungeonHallLookPath,
                profile =>
                {
                    AddFocus(profile, 12f, 4f, 4f, 9f, 10f, 6f);
                    var bloom = profile.Add<Bloom>(true);
                    bloom.threshold.Override(0.9f);
                    bloom.intensity.Override(1.3f);
                    bloom.scatter.Override(0.65f);
                    bloom.highQualityFiltering.Override(false);
                    bloom.downscale.Override(BloomDownscaleMode.Half);
                    bloom.maxIterations.Override(6);
                    AddGrade(
                        profile,
                        0f,
                        12f,
                        8f,
                        0f,
                        new Color(0.4f, 0.46f, 0.6f),
                        new Color(0.62f, 0.54f, 0.42f),
                        0f
                    );
                    AddVignette(profile, 0.36f, Color.black);
                }
            );
            // The battle keeps the bloom of its effects (BattlePostProcess); this adds the lens
            // and the grade on top, with a higher priority.
            Profile(
                ForestRuinsLookPath,
                profile =>
                {
                    AddFocus(profile, BattleView.FocusDistance, 5f, 6f, 9f, 10f, 5f);
                    AddGrade(
                        profile,
                        0f,
                        10f,
                        8f,
                        0f,
                        new Color(0.4f, 0.46f, 0.6f),
                        new Color(0.6f, 0.54f, 0.44f),
                        0f
                    );
                    AddVignette(profile, 0.32f, Color.black);
                }
            );
        }

        internal static void AddFocus(
            VolumeProfile profile,
            float distance,
            float range,
            float near,
            float far,
            float nearRadius,
            float farRadius
        )
        {
            var focus = profile.Add<Hd2dStageFocus>(true);
            focus.intensity.Override(1f);
            focus.focusDistance.Override(distance);
            focus.focusRange.Override(range);
            focus.nearFalloff.Override(near);
            focus.farFalloff.Override(far);
            focus.nearMaxRadius.Override(nearRadius);
            focus.maxRadius.Override(farRadius);
        }

        internal static void AddGrade(
            VolumeProfile profile,
            float exposure,
            float contrast,
            float saturation,
            float temperature,
            Color shadows,
            Color highlights,
            float balance
        )
        {
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(exposure);
            color.contrast.Override(contrast);
            color.saturation.Override(saturation);
            if (temperature != 0f)
                profile.Add<WhiteBalance>(true).temperature.Override(temperature);
            var toning = profile.Add<SplitToning>(true);
            toning.shadows.Override(shadows);
            toning.highlights.Override(highlights);
            toning.balance.Override(balance);
        }

        internal static void AddVignette(VolumeProfile profile, float intensity, Color color)
        {
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(intensity);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(color);
            vignette.rounded.Override(false);
        }

        internal static Material GlowMaterial(string path, Color color, float intensity)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Baryonyx/HD2D/Glow Particle"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_Color", color);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_Core", 0.01f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
