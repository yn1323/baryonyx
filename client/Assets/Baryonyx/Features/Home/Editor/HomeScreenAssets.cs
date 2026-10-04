using System;
using System.Collections.Generic;
using System.IO;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.Vfx.Hd2d;
using Baryonyx.Vfx.Hd2d.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Home.Editor
{
    /// <summary>
    /// Generates the home screen prefab ("camp" layout) and its mock data.
    /// Coordinates follow the 1920x1080 design: the world layer is centred, and the
    /// controls are anchored to the Safe Area corners.
    /// </summary>
    public static class HomeScreenAssets
    {
        public const string PrefabPath = "Assets/Baryonyx/Features/Home/UI/HomeScreen.prefab";
        public const string DataPath = "Assets/Baryonyx/Features/Home/Data/HomeMockData.asset";

        /// <summary>The computed flame of the campfire burning on the 3D glade (HomeSceneSetup).</summary>
        public const string CampfireFlameMaterialPath =
            "Assets/Baryonyx/Features/Home/Vfx/CampfireFlame.mat";
        public const string BackgroundPath = "Assets/Baryonyx/Shared/Art/Stages/DungeonHall.png";
        public const string DestinationArtPath = "Assets/Baryonyx/Shared/Art/Stages/Forest.png";
        public const string CharactersPath =
            "Assets/Baryonyx/Shared/Art/Characters/Adventurers.png";

        /// <summary>Toma's standing sprite on a 100x100 canvas (centred, feet on the bottom row).</summary>
        public const string TomaPath = "Assets/Baryonyx/Shared/Art/Characters/Toma.aseprite";

        // Light colours taken from the rune palette (highlight and the brightest facets).
        private static readonly Color RuneSpark = new(0.933f, 0.988f, 1f, 1f);
        private static readonly Color RuneSparkFade = new(0.32f, 0.706f, 0.933f, 0f);
        private static readonly Color RuneGlowColor = new(0.588f, 0.878f, 0.98f, 0f);

        private static readonly Color TextMain = new(0.953f, 0.914f, 0.824f);
        private static readonly Color TextSub = new(0.788f, 0.749f, 0.659f);
        private static readonly Color TextFaint = new(0.604f, 0.580f, 0.514f);
        private static readonly Color Teal = new(0.498f, 0.890f, 0.839f);
        private static readonly Color Flame = new(1f, 0.58f, 0.24f, 1f);

        private const float NavWidth = 128f;

        // Gap between the bottom-left buttons.
        private const float NavSpacing = 32f;

        // The 24x24 button icons are drawn at 4x so each dot covers 4x4 pixels.
        private const float NavIconSize = 24f * 4f;

        // Room for the 4x icon above the label.
        private const float NavHeight = 168f;

        // The 24x24 rune is drawn at 4x so each dot covers 4x4 pixels.
        // Raises the party, the campfire and their shadows and lights together (design px,
        // a multiple of the 4 px dot).
        private const float CampLift = 160f;

        /// <summary>How far below the screen's middle the campfire stands (design px).</summary>
        public const float CampfireFeetBelowCenter = 405f - CampLift;

        private const float RuneIconSize = 24f * 4f;

        // The balance icon beside the digits is the same rune at 2x.
        private const float BalanceRuneSize = 24f * 2f;

        private static Texture2D destinationArt;

        [MenuItem("Baryonyx/Home/Create Screen Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            AssetDatabase.Refresh();
            HomeScreenArt.EnsureAll();
            EnsureCampfireFlame();
            EnsureMockData();
            var font = GameFontAssets.GetOrCreate();
            var shadowText = UiArt.EnsureTextShadow(font);
            destinationArt = ArtAssets.ImportTexture(DestinationArtPath, FilterMode.Point);
            var background = ArtAssets.ImportTexture(BackgroundPath, FilterMode.Point);
            var characters = ArtAssets.ImportTexture(CharactersPath, FilterMode.Point);
            var toma = ArtAssets.ImportTexture(TomaPath, FilterMode.Point);

            using (UiBuild.Begin(font, shadowText))
            {
                var root = CanvasRoot("HomeScreen");
                var view = root.gameObject.AddComponent<HomeView>();

                BuildBackground(root, background);
                BuildWorld(root, view, characters, toma);
                var safe = SafeArea(root);
                BuildStepPanel(safe, view);
                BuildTopRight(safe, view);
                BuildNavigation(safe, view);
                BuildResume(safe, view);
                BuildRuneGain(root, safe, view);
                BuildToast(root, view);
                CollectTintGraphics(root);

                // Bake the sample values so prefab and showcase previews match the running screen.
                var data = AssetDatabase.LoadAssetAtPath<HomeMockData>(DataPath);
                view.Render(HomeViewState.From(data.ToSnapshot(DateTime.Today)));

                // Edit-time layout helpers may have run in the preview scene; save design values.
                root.Find("World").localScale = Vector3.one;

                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }

        // The campfire burns in the daylight. Only added, the stage's night flame whitens with the
        // sunlit ground under it and loses its shape, so this one hides the ground behind its hot
        // parts, burns redder, and keeps little of the glow round it.
        private static void EnsureCampfireFlame()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(CampfireFlameMaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CampfireFlameMaterialPath));
                material = new Material(Shader.Find("Baryonyx/HD2D/Flame"))
                {
                    name = Path.GetFileNameWithoutExtension(CampfireFlameMaterialPath),
                };
                AssetDatabase.CreateAsset(material, CampfireFlameMaterialPath);
            }
            material.SetColor("_CoreColor", new Color(1f, 0.84f, 0.46f));
            material.SetColor("_MidColor", new Color(1f, 0.44f, 0.08f));
            material.SetColor("_EdgeColor", new Color(0.8f, 0.12f, 0.02f));
            material.SetColor("_HaloColor", new Color(1f, 0.45f, 0.15f));
            material.SetFloat("_Intensity", 1.4f);
            material.SetFloat("_Halo", 0.06f);
            material.SetFloat("_Turbulence", 1.3f);
            material.SetFloat("_Speed", 2.2f);
            material.SetFloat("_Puff", 1.6f);
            material.SetFloat("_Base", 0.1f);
            material.SetFloat("_FlameWidth", 0.5f);
            material.SetFloat("_FlameHeight", 0.5f);
            material.SetFloat("_Cover", 0.75f);
            EditorUtility.SetDirty(material);
        }

        private static void EnsureMockData()
        {
            if (AssetDatabase.LoadAssetAtPath<HomeMockData>(DataPath) != null)
                return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<HomeMockData>(), DataPath);
        }

        private static void BuildBackground(RectTransform root, Texture2D background)
        {
            float aspect = background.width / (float)background.height;
            var image = Rect("Background", root).gameObject.AddComponent<RawImage>();
            image.texture = background;
            image.raycastTarget = false;
            image.gameObject.AddComponent<ResponsiveBackground>().AspectRatio = aspect;
            // The painting and what is laid over it give way to the 3D hall when the scene has
            // one (HD-2D); the prefab previewed on its own keeps them.
            Hd2dStageKit.FlatOnly(image.gameObject);

            var fog = Vfx<Hd2dFog>(Hd2dAssets.FogPrefabPath, "BackgroundFog", root, aspect);
            Hd2dStageKit.FlatOnly(fog.gameObject);
            fog.Layers.Add(
                new Hd2dFogLayer
                {
                    Name = "DeepHaze",
                    AnchorMin = new Vector2(0.36f, 0.34f),
                    AnchorMax = new Vector2(0.64f, 0.86f),
                    Color = new Color(0.45f, 0.58f, 0.82f, 0.22f),
                    Softness = new Vector2Int(150, 170),
                    TileSize = new Vector2(420f, 300f),
                    ScrollSpeed = new Vector2(5f, 3f),
                    DetailOpacity = 0.5f,
                    BreathAmount = 0.2f,
                    BreathSpeed = 0.15f,
                }
            );

            // Painted torches and the far gate: positions are normalized to the background image.
            var lights = Vfx<Hd2dFlickerLight>(
                Hd2dAssets.FlickerLightPrefabPath,
                "BackgroundLights",
                root,
                aspect
            );
            lights.Sources = new List<Hd2dFlickerLightSource>
            {
                Torch("OuterLeft", new Vector2(0.105f, 0.72f), new Vector2(0.17f, 0.27f), 1f),
                Torch("InnerLeft", new Vector2(0.358f, 0.635f), new Vector2(0.37f, 0.36f), 0.7f),
                Torch("InnerRight", new Vector2(0.642f, 0.635f), new Vector2(0.63f, 0.36f), 0.7f),
                Torch("OuterRight", new Vector2(0.894f, 0.72f), new Vector2(0.83f, 0.27f), 1f),
                new Hd2dFlickerLightSource
                {
                    Name = "GateGlow",
                    Anchor = new Vector2(0.5f, 0.6f),
                    Color = new Color(0.37f, 0.83f, 0.78f, 1f),
                    CoreSize = new Vector2(200f, 260f),
                    CoreAlpha = 0.18f,
                    HaloSize = new Vector2(420f, 520f),
                    HaloAlpha = 0.35f,
                    ReflectionAlpha = 0f,
                    FlickerAmount = 0.12f,
                    FlickerSpeed = 0.5f,
                    SizeJitter = 0.03f,
                },
            };

            var embers = Vfx<Hd2dEmberEmitter>(
                Hd2dAssets.EmberEmitterPrefabPath,
                "BackgroundEmbers",
                root,
                aspect
            );
            embers.Sources = new List<Hd2dEmberSource>
            {
                Embers("OuterLeft", new Vector2(0.105f, 0.745f), 10, 1f),
                Embers("InnerLeft", new Vector2(0.358f, 0.652f), 6, 0.7f),
                Embers("InnerRight", new Vector2(0.642f, 0.652f), 6, 0.7f),
                Embers("OuterRight", new Vector2(0.894f, 0.745f), 10, 1f),
            };
            Hd2dStageKit.FlatOnly(lights.gameObject);
            Hd2dStageKit.FlatOnly(embers.gameObject);

            var dim = Rect("Dim", root);
            Stretch(dim);
            AddImage(dim, new Color(0.031f, 0.039f, 0.071f, 0.22f), false);
            Hd2dStageKit.FlatOnly(dim.gameObject);
            Shade(root, "ShadeTop", top: true, 300f, 0.78f);
            Shade(root, "ShadeBottom", top: false, 240f, 0.72f);
        }

        private static void BuildWorld(
            RectTransform root,
            HomeView view,
            Texture2D characters,
            Texture2D toma
        )
        {
            var world = Rect("World", root);
            world.anchorMin = world.anchorMax = world.pivot = Vector2.one * 0.5f;
            world.sizeDelta = new Vector2(1920, 1080);
            world.gameObject.AddComponent<WorldLayerFit>();

            // The campfire light is normalized to this 1920x1080 layer, so it stays on the fire.
            var campLight = Vfx<Hd2dFlickerLight>(
                Hd2dAssets.FlickerLightPrefabPath,
                "CampLight",
                world,
                0f
            );
            campLight.Sources = new List<Hd2dFlickerLightSource>
            {
                new Hd2dFlickerLightSource
                {
                    Name = "Campfire",
                    Anchor = new Vector2(0.5f, 0.194f + CampLift / 1080f),
                    Color = Flame,
                    CoreSize = new Vector2(170f, 190f),
                    CoreAlpha = 0.5f,
                    HaloSize = new Vector2(900f, 620f),
                    HaloAlpha = 0.28f,
                    ReflectionAnchor = new Vector2(0.5f, 0.11f + CampLift / 1080f),
                    ReflectionSize = new Vector2(760f, 150f),
                    ReflectionAlpha = 0.3f,
                    FlickerAmount = 0.25f,
                    FlickerSpeed = 2.6f,
                    SizeJitter = 0.05f,
                },
            };
            // On the 3D hall the campfire's real light takes over.
            Hd2dStageKit.FlatOnly(campLight.gameObject);

            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            var shadowToma = Picture(
                world,
                "ShadowToma",
                shadow,
                new Vector2(-175, -394 + CampLift),
                new Vector2(150, 24),
                0.45f
            );
            var shadowLuka = Picture(
                world,
                "ShadowLuka",
                shadow,
                new Vector2(175, -394 + CampLift),
                new Vector2(150, 24),
                0.45f
            );
            var tomaPicture = PixelActor(
                world,
                "Toma",
                toma,
                new Vector2(-175, -392 + CampLift),
                4f
            );
            var lukaPicture = Actor(
                world,
                "Luka",
                characters,
                ActorUv(2),
                new Vector2(158.5f, -271 + CampLift),
                new Vector2(207, 276),
                true
            );
            var shadowFire = Picture(
                world,
                "ShadowFire",
                shadow,
                new Vector2(0, -405 + CampLift),
                new Vector2(180, 30),
                0.5f
            );
            // The 11x14 campfire at 8x: the logs (its bottom 3 rows) and the flame over them.
            var fire = Picture(
                world,
                "Campfire",
                ArtAssets.LoadSprite(HomeScreenArt.CampfireLogsPath),
                new Vector2(0, -392 + CampLift),
                new Vector2(88, 24),
                1f
            );
            fire.color = Color.white;
            var flame = Picture(
                world,
                "CampfireFlame",
                ArtAssets.LoadSprite(HomeScreenArt.CampfireFlamePath),
                new Vector2(0, -340 + CampLift),
                new Vector2(88, 96),
                1f
            );
            flame.color = Color.white;
            // On a 3D stage the members and the logs stand on the floor as boards, and the
            // scene burns a computed flame in the logs (HomeSceneSetup) in place of the drawn one.
            Hd2dStageKit.Stand(tomaPicture.gameObject, tomaPicture, shadowToma);
            Hd2dStageKit.Stand(lukaPicture.gameObject, lukaPicture, shadowLuka);
            var fireBoard = Hd2dStageKit.Stand(fire.gameObject, fire, shadowFire);
            fireBoard.HideWhenStaged = new Graphic[] { flame };
            // The logs keep their drawn colours: lit, the fire's light beside them turned them
            // into bright orange bars.
            fireBoard.CastShadow = false;
            fireBoard.Lit = false;

            var campEmbers = Vfx<Hd2dEmberEmitter>(
                Hd2dAssets.EmberEmitterPrefabPath,
                "CampEmbers",
                world,
                0f
            );
            campEmbers.Sources = new List<Hd2dEmberSource>
            {
                new Hd2dEmberSource
                {
                    Name = "Campfire",
                    Anchor = new Vector2(0.5f, 0.23f + CampLift / 1080f),
                    SpawnArea = new Vector2(40f, 10f),
                    Count = 14,
                    Loop = true,
                    LifetimeRange = new Vector2(1f, 2.2f),
                    Direction = 90f,
                    Spread = 30f,
                    SpeedRange = new Vector2(40f, 90f),
                    Buoyancy = 16f,
                    Sway = 0f,
                    Curl = 42f,
                    CurlScale = 64f,
                    CurlSpeed = 0.5f,
                    DotSize = 2,
                    CrossShare = 0.3f,
                    StreakShare = 0.15f,
                    StartColor = new Color(1f, 0.84f, 0.46f, 1f),
                    EndColor = new Color(1f, 0.32f, 0.08f, 0f),
                    Twinkle = 0.35f,
                },
            };
            // On the 3D stage the computed flame's own embers rise instead.
            Hd2dStageKit.FlatOnly(campEmbers.gameObject);

            var shadowAria = Picture(
                world,
                "ShadowAria",
                shadow,
                new Vector2(-330, -443 + CampLift),
                new Vector2(164, 26),
                0.5f
            );
            var shadowMina = Picture(
                world,
                "ShadowMina",
                shadow,
                new Vector2(330, -443 + CampLift),
                new Vector2(164, 26),
                0.5f
            );
            var ariaPicture = Actor(
                world,
                "Aria",
                characters,
                ActorUv(0),
                new Vector2(-323.5f, -316 + CampLift),
                new Vector2(225, 300),
                false
            );
            var minaPicture = Actor(
                world,
                "Mina",
                characters,
                ActorUv(3),
                new Vector2(314.5f, -316 + CampLift),
                new Vector2(225, 300),
                true
            );
            Hd2dStageKit.Stand(ariaPicture.gameObject, ariaPicture, shadowAria);
            Hd2dStageKit.Stand(minaPicture.gameObject, minaPicture, shadowMina);

            // One invisible target over the four members opens the party screen.
            var party = Rect("PartyTapArea", world);
            Place(party, new Vector2(0, -301 + CampLift), new Vector2(900, 340));
            view.PartyWorldButton = AddButton(party, AddImage(party, Color.clear, true));
        }

        private static void BuildStepPanel(RectTransform safe, HomeView view)
        {
            // No frame: a soft dark spot behind the text keeps it readable over the background.
            var panel = Rect("StepPanel", safe);
            Corner(panel, new Vector2(0, 1), new Vector2(24, -8), new Vector2(800, 0));
            var spot = Sliced(panel, UiArt.FeatherPath, new Color(0.012f, 0.02f, 0.04f, 0.9f));
            spot.raycastTarget = true;
            view.StepButton = AddTintButton(panel, spot);
            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(68, 68, 56, 60);
            column.spacing = 12;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            var details = Rect("StepDetails", panel);
            var detailColumn = details.gameObject.AddComponent<VerticalLayoutGroup>();
            detailColumn.spacing = 12;
            detailColumn.childControlWidth = detailColumn.childControlHeight = true;
            detailColumn.childForceExpandWidth = true;
            detailColumn.childForceExpandHeight = false;
            view.StepDetails = details.gameObject;

            var stepsRow = Row(details, "StepsRow", 104, 12);
            stepsRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
            view.StepsLabel = Label(
                stepsRow,
                "StepsLabel",
                "",
                96,
                TextMain,
                TextAlignmentOptions.BottomLeft
            );
            Label(
                stepsRow,
                "StepsUnit",
                "UPT",
                48,
                TextSub,
                TextAlignmentOptions.BottomLeft
            ).margin = new Vector4(0, 0, 0, 8);

            // The date sits on the right of the steps, so the panel needs no header row.
            var side = Rect("StepsSide", stepsRow);
            var sideColumn = side.gameObject.AddComponent<VerticalLayoutGroup>();
            sideColumn.padding = new RectOffset(0, 0, 0, 8);
            sideColumn.spacing = 10;
            sideColumn.childAlignment = TextAnchor.LowerRight;
            sideColumn.childControlWidth = sideColumn.childControlHeight = true;
            sideColumn.childForceExpandWidth = true;
            sideColumn.childForceExpandHeight = false;
            side.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            view.DateLabel = Label(
                side,
                "DateLabel",
                "",
                32,
                TextFaint,
                TextAlignmentOptions.BottomRight
            );

            var gauge = Row(details, "Gauge", 18, 4);
            var segments = new Image[HomeViewState.GaugeSegments];
            for (int i = 0; i < segments.Length; i++)
            {
                var segment = Rect($"Segment{i:00}", gauge);
                segments[i] = AddImage(segment, HomeView.GaugeEmpty, false);
                var size = segment.gameObject.AddComponent<LayoutElement>();
                size.flexibleWidth = 1;
                size.minHeight = size.preferredHeight = 18;
            }
            view.Segments = segments;

            // Gold notches under the gauge mark where each bonus tier opens.
            var ticks = Rect("GaugeTicks", details);
            var tickSize = ticks.gameObject.AddComponent<LayoutElement>();
            tickSize.minHeight = tickSize.preferredHeight = 8;
            foreach (int tier in HomeViewState.BonusUpt)
            {
                float x = Mathf.Clamp01(tier / (float)HomeViewState.GaugeMaxUpt);
                var tick = Rect($"Tick{tier}", ticks);
                tick.anchorMin = tick.anchorMax = new Vector2(x, 1);
                tick.pivot = new Vector2(x >= 1f ? 1f : 0.5f, 1f);
                tick.anchoredPosition = Vector2.zero;
                tick.sizeDelta = new Vector2(4, 8);
                AddImage(tick, HomeView.GaugeAchieved, false);
            }

            var footer = Row(details, "StepsFooter", 40, 12);
            view.RemainingLabel = Label(
                footer,
                "RemainingLabel",
                "",
                32,
                TextSub,
                TextAlignmentOptions.MidlineLeft
            );
            Flexible(view.RemainingLabel);

            var unlinked = Rect("UnlinkedDetails", panel);
            var unlinkedColumn = unlinked.gameObject.AddComponent<VerticalLayoutGroup>();
            unlinkedColumn.spacing = 8;
            unlinkedColumn.childControlWidth = unlinkedColumn.childControlHeight = true;
            unlinkedColumn.childForceExpandWidth = true;
            unlinkedColumn.childForceExpandHeight = false;
            Label(
                unlinked,
                "UnlinkedTitle",
                "歩数がまだ届いていません",
                48,
                TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Label(
                unlinked,
                "UnlinkedBody",
                "1歩が1UPTになり、仲間の力になります",
                32,
                TextSub,
                TextAlignmentOptions.MidlineLeft
            );
            view.UnlinkedDetails = unlinked.gameObject;
            unlinked.gameObject.SetActive(false);

            // A gold hairline that fades to the right separates the claim hint.
            var divider = Rect("ClaimDivider", panel);
            var line = divider.gameObject.AddComponent<RawImage>();
            line.texture = ArtAssets.LoadTexture(HomeScreenArt.ShadeHorizontalPath);
            line.uvRect = new Rect(1, 0, -1, 1);
            line.color = new Color(0.914f, 0.769f, 0.471f, 0.7f);
            line.raycastTarget = false;
            var lineSize = divider.gameObject.AddComponent<LayoutElement>();
            lineSize.minHeight = lineSize.preferredHeight = 2;

            // The hint pulses like TAP TO START; the panel itself takes the tap.
            var claim = Row(panel, "ClaimRow", 40, 10);
            claim.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            var claimGroup = claim.gameObject.AddComponent<CanvasGroup>();
            claimGroup.interactable = false;
            claimGroup.blocksRaycasts = false;
            view.ClaimGroup = claimGroup;
            view.ClaimLabel = Label(
                claim,
                "ClaimLabel",
                "",
                32,
                Teal,
                TextAlignmentOptions.MidlineLeft
            );
            // Without a pointer position the runes burst from the hint text.
            view.RuneOrigin = view.ClaimLabel.rectTransform;
        }

        private static void BuildTopRight(RectTransform safe, HomeView view)
        {
            var settings = Rect("SettingsButton", safe);
            Corner(settings, Vector2.one, new Vector2(-64, -40), new Vector2(88, 88));
            HitArea(settings, 20, 20);
            var spot = SpriteImage(
                settings,
                UiArt.SoftSpotPath,
                new Color(0.012f, 0.02f, 0.04f, 0.8f)
            );
            spot.raycastTarget = true;
            view.SettingsButton = AddTintButton(settings, spot);
            var gear = Icon(settings, "SettingsIcon", UiArt.IconSettingsPath, 40, TextMain);
            Place((RectTransform)gear.transform, Vector2.zero, new Vector2(40, 40));

            var pill = Rect("RunePill", safe);
            Corner(pill, Vector2.one, new Vector2(-(64 + 88 + 16), -40), new Vector2(0, 88));
            // The same soft dark plate as the step panel, but reaching past the pill with a
            // twice as wide fade, so it thins out gradually toward every edge.
            var plate = Rect("RuneShade", pill);
            Stretch(plate);
            plate.offsetMin = new Vector2(-24, -24);
            plate.offsetMax = new Vector2(24, 24);
            plate.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var shade = Sliced(plate, UiArt.FeatherPath, new Color(0.012f, 0.02f, 0.04f, 0.9f));
            shade.pixelsPerUnitMultiplier = 0.5f;
            shade.raycastTarget = false;
            var row = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(44, 44, 0, 0);
            row.spacing = 12;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            pill.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            view.RuneTarget = (RectTransform)
                Icon(
                    pill,
                    "RuneIcon",
                    HomeScreenArt.IconRunePath,
                    BalanceRuneSize,
                    Color.white
                ).transform;
            view.RunesLabel = Label(
                pill,
                "RunesLabel",
                "",
                48,
                TextMain,
                TextAlignmentOptions.Midline
            );
        }

        private static void BuildNavigation(RectTransform safe, HomeView view)
        {
            var nav = Rect("Navigation", safe);
            // 128-wide buttons (the touch minimum) keep the row clear of the party.
            Corner(
                nav,
                Vector2.zero,
                new Vector2(48, 32),
                new Vector2(NavWidth * 4 + NavSpacing * 3, NavHeight)
            );
            var row = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = NavSpacing;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            view.TavernButton = NavButton(nav, "TavernButton", "酒場", HomeScreenArt.IconPartyPath);
            view.WorkshopButton = NavButton(
                nav,
                "WorkshopButton",
                "工房",
                HomeScreenArt.IconEquipmentPath
            );
            view.TempleButton = NavButton(
                nav,
                "TempleButton",
                "神殿",
                HomeScreenArt.IconSummonPath
            );
            view.TravelOfficeButton = NavButton(
                nav,
                "TravelOfficeButton",
                "旅の案内所",
                HomeScreenArt.IconCompassPath
            );
        }

        private static Button NavButton(
            RectTransform parent,
            string name,
            string text,
            string iconPath
        )
        {
            var rect = Rect(name, parent);
            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = NavWidth;
            size.minHeight = size.preferredHeight = NavHeight;
            var spot = SpriteImage(
                rect,
                UiArt.SoftSpotPath,
                new Color(0.012f, 0.02f, 0.04f, 0.78f)
            );
            spot.raycastTarget = true;
            var button = AddTintButton(rect, spot);
            var icon = Icon(rect, name + "Icon", iconPath, NavIconSize, Color.white);
            Place(
                (RectTransform)icon.transform,
                new Vector2(0, 24),
                new Vector2(NavIconSize, NavIconSize)
            );
            var label = Label(
                rect,
                name + "Label",
                text,
                32,
                TextMain,
                TextAlignmentOptions.Center
            );
            Place((RectTransform)label.transform, new Vector2(0, -60), new Vector2(NavWidth, 40));
            return button;
        }

        private static void BuildResume(RectTransform safe, HomeView view)
        {
            const float cardWidth = 500f;
            const float cardHeight = 200f;
            var cardOffset = new Vector2(-48, 44);

            // Glow and drop shadow sit behind the card because the card masks its children.
            var glow = Rect("ResumeGlow", safe);
            Corner(
                glow,
                new Vector2(1, 0),
                cardOffset + new Vector2(70, -70),
                new Vector2(cardWidth + 140, cardHeight + 140)
            );
            SpriteImage(glow, UiArt.SoftSpotPath, new Color(0.949f, 0.675f, 0.263f, 0.45f));
            var dropShadow = Rect("ResumeShadow", safe);
            Corner(
                dropShadow,
                new Vector2(1, 0),
                cardOffset + new Vector2(30, -50),
                new Vector2(cardWidth + 60, cardHeight + 60)
            );
            SpriteImage(dropShadow, UiArt.SoftSpotPath, new Color(0f, 0f, 0f, 0.6f));

            var card = Rect("ResumeCard", safe);
            Corner(card, new Vector2(1, 0), cardOffset, new Vector2(cardWidth, cardHeight));
            var cardImage = Sliced(card, UiArt.RoundedRectPath, new Color(0.039f, 0.047f, 0.086f));
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            // The destination art covers the card like CSS object-fit: cover at 30% from the top.
            var art = Rect("DestinationArt", card);
            Stretch(art);
            var artImage = art.gameObject.AddComponent<RawImage>();
            artImage.texture = destinationArt;
            float visible = (cardHeight / cardWidth) * destinationArt.width / destinationArt.height;
            artImage.uvRect = new Rect(0, 1 - visible - (1 - visible) * 0.3f, 1, visible);
            artImage.raycastTarget = false;
            var shade = Rect("DestinationShade", card);
            Stretch(shade);
            var shadeImage = shade.gameObject.AddComponent<RawImage>();
            shadeImage.texture = ArtAssets.LoadTexture(HomeScreenArt.CardShadePath);
            shadeImage.color = new Color(0.024f, 0.031f, 0.055f);
            shadeImage.raycastTarget = false;
            var rim = Rect("ResumeRim", card);
            Stretch(rim);
            Sliced(rim, UiArt.RoundedRingPath, new Color(1f, 0.824f, 0.478f, 0.55f)).raycastTarget =
                false;

            view.DestinationNameLabel = Label(
                card,
                "DestinationNameLabel",
                "",
                32,
                new Color(0.910f, 0.863f, 0.769f),
                TextAlignmentOptions.BottomLeft
            );
            Corner(
                (RectTransform)view.DestinationNameLabel.transform,
                Vector2.zero,
                new Vector2(28, 100),
                new Vector2(280, 40)
            );
            view.DestinationFloorLabel = Label(
                card,
                "DestinationFloorLabel",
                "",
                64,
                TextMain,
                TextAlignmentOptions.BottomLeft
            );
            Corner(
                (RectTransform)view.DestinationFloorLabel.transform,
                Vector2.zero,
                new Vector2(28, 24),
                new Vector2(220, 72)
            );

            // "再開" is written straight on the card, in the same white as the floor number.
            var resume = Label(
                card,
                "ResumeLabel",
                "再開",
                64,
                TextMain,
                TextAlignmentOptions.BottomRight
            );
            resume.characterSpacing = 12;
            Corner(
                (RectTransform)resume.transform,
                new Vector2(1, 0),
                new Vector2(-28, 24),
                new Vector2(180, 72)
            );

            // The whole card is the button; pressing darkens the destination art.
            cardImage.raycastTarget = true;
            view.ResumeButton = AddButton(card, artImage);
        }

        // Runes fly from the step panel to the balance; the view clones the hidden particle.
        private static void BuildRuneGain(RectTransform root, RectTransform safe, HomeView view)
        {
            var layer = Rect("RuneEffect", root);
            Stretch(layer);
            view.RuneEffectLayer = layer;

            // A soft additive light over the balance icon, under the runes and the sparkles.
            var glow = Rect("RuneGlow", layer);
            Place(glow, Vector2.zero, new Vector2(BalanceRuneSize, BalanceRuneSize) * 2.2f);
            view.RuneGlow = SpriteImage(glow, Hd2dAssets.GlowTexturePath, RuneGlowColor);
            view.RuneGlow.material = AssetDatabase.LoadAssetAtPath<Material>(
                Hd2dAssets.AdditiveMaterialPath
            );
            glow.gameObject.SetActive(false);

            // A haze of soft light drifts behind the sparkles and the runes: a puff where the
            // runes burst out, a mist along their paths and a bloom where they land. It uses
            // the same sources as the sparkles, with the soft glow as its only shape.
            var haze = Vfx<Hd2dEmberEmitter>(
                Hd2dAssets.EmberEmitterPrefabPath,
                "RuneHaze",
                layer,
                0f
            );
            haze.CrossSprite = ArtAssets.LoadSprite(Hd2dAssets.GlowTexturePath);
            haze.Sources = new List<Hd2dEmberSource>
            {
                RuneHazeSource(
                    "Arrival",
                    12,
                    new Vector2(30f, 30f),
                    new Vector2(20f, 70f),
                    new Vector2(0.4f, 0.6f),
                    200f,
                    2,
                    0.45f
                ),
                RuneHazeSource(
                    "Final",
                    6,
                    new Vector2(30f, 30f),
                    new Vector2(40f, 110f),
                    new Vector2(0.6f, 0.9f),
                    260f,
                    3,
                    0.5f
                ),
                RuneHazeSource(
                    "Burst",
                    8,
                    new Vector2(60f, 60f),
                    new Vector2(60f, 180f),
                    new Vector2(0.5f, 0.9f),
                    360f,
                    3,
                    0.4f
                ),
                RuneHazeSource(
                    "Trail",
                    32,
                    new Vector2(24f, 24f),
                    new Vector2(0f, 20f),
                    new Vector2(0.35f, 0.6f),
                    360f,
                    2,
                    0.32f
                ),
            };
            view.RuneHaze = haze;

            // Sparkles pop from the balance icon as each rune lands, with a bigger burst for
            // the last one; they lean left, away from the balance digits. The runes also burst
            // out with a spray of sparkles and leave a short sparkling trail as they fly. All
            // are drawn in 4 px dots like the 4x rune, the stars from a 5x5 twinkle. The order
            // matches HomeView's sources.
            var sparkles = Vfx<Hd2dEmberEmitter>(
                Hd2dAssets.EmberEmitterPrefabPath,
                "RuneSparkles",
                layer,
                0f
            );
            sparkles.CrossSprite = ArtAssets.LoadSprite(HomeScreenArt.RuneTwinklePath);
            sparkles.Sources = new List<Hd2dEmberSource>
            {
                RuneSparkSource(
                    "Arrival",
                    48,
                    new Vector2(40f, 40f),
                    new Vector2(160f, 280f),
                    new Vector2(0.25f, 0.45f),
                    200f,
                    -80f
                ),
                RuneSparkSource(
                    "Final",
                    20,
                    new Vector2(24f, 24f),
                    new Vector2(220f, 400f),
                    new Vector2(0.4f, 0.75f),
                    260f,
                    -220f
                ),
                RuneSparkSource(
                    "Burst",
                    24,
                    new Vector2(40f, 40f),
                    new Vector2(220f, 460f),
                    new Vector2(0.3f, 0.6f),
                    360f,
                    -120f
                ),
                RuneSparkSource(
                    "Trail",
                    64,
                    new Vector2(36f, 36f),
                    new Vector2(10f, 50f),
                    new Vector2(0.25f, 0.5f),
                    360f,
                    -40f
                ),
            };
            view.RuneSparkles = sparkles;

            var particle = Rect("RuneParticle", layer);
            Place(particle, Vector2.zero, new Vector2(RuneIconSize, RuneIconSize));
            view.RuneParticle = SpriteImage(particle, HomeScreenArt.IconRunePath, Color.white);
            view.RuneParticle.preserveAspect = true;
            particle.gameObject.SetActive(false);

            // The gained amount appears under the balance while the counter rises.
            var gain = Rect("RuneGain", safe);
            Corner(
                gain,
                Vector2.one,
                new Vector2(-(64 + 88 + 16 + 28), -(40 + 88 + 4)),
                new Vector2(320, 64)
            );
            var group = gain.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;
            view.GainGroup = group;
            view.GainLabel = Label(
                gain,
                "RuneGainLabel",
                "",
                48,
                Teal,
                TextAlignmentOptions.MidlineRight
            );
            Stretch(view.GainLabel.rectTransform);
        }

        // Both use the shared notice band at the top middle (doc/rules/ui-design.md): the
        // result of a tap that granted nothing, and the mock buttons' "coming soon".
        private static void BuildToast(RectTransform root, HomeView view)
        {
            var notice = NoticeBandAssets.Build(root, "Notice");
            view.Notice = notice.Group;
            view.NoticeLabel = notice.Label;
            var toast = NoticeBandAssets.Build(root, "Toast");
            view.Toast = toast.Group;
            view.ToastLabel = toast.Label;
        }

        private static T Vfx<T>(
            string prefabPath,
            string name,
            RectTransform parent,
            float backgroundAspect
        )
            where T : Component
        {
            var instance = InstantiatePrefab(prefabPath, name, parent);
            var rect = (RectTransform)instance.transform;
            Stretch(rect);
            // Background-aligned effects follow the covered artwork, not the screen edges.
            if (backgroundAspect > 0f)
            {
                var aligned = instance.AddComponent<ResponsiveBackground>();
                aligned.AspectRatio = backgroundAspect;
            }
            var component = instance.GetComponent<T>();
            if (component == null)
                throw new InvalidOperationException($"{prefabPath} has no {typeof(T).Name}.");
            return component;
        }

        private static Hd2dEmberSource RuneSparkSource(
            string name,
            int count,
            Vector2 area,
            Vector2 speed,
            Vector2 lifetime,
            float spread,
            float buoyancy
        ) =>
            new()
            {
                Name = name,
                Anchor = new Vector2(0.5f, 0.5f),
                SpawnArea = area,
                Count = count,
                Loop = false,
                LifetimeRange = lifetime,
                Direction = 180f,
                Spread = spread,
                SpeedRange = speed,
                Buoyancy = buoyancy,
                Curl = 0f,
                DotSize = 4,
                CrossShare = 0.6f,
                StreakShare = 0f,
                StartColor = RuneSpark,
                EndColor = RuneSparkFade,
                Twinkle = 0.75f,
            };

        private static Hd2dEmberSource RuneHazeSource(
            string name,
            int count,
            Vector2 area,
            Vector2 speed,
            Vector2 lifetime,
            float spread,
            int scale,
            float alpha
        ) =>
            new()
            {
                Name = name,
                Anchor = new Vector2(0.5f, 0.5f),
                SpawnArea = area,
                Count = count,
                Loop = false,
                LifetimeRange = lifetime,
                Direction = 180f,
                Spread = spread,
                SpeedRange = speed,
                Buoyancy = 0f,
                Curl = 0f,
                // The 64 px glow is scaled whole, so a puff is 128 or 192 px across.
                DotSize = scale,
                CrossShare = 1f,
                StreakShare = 0f,
                StartColor = new Color(RuneGlowColor.r, RuneGlowColor.g, RuneGlowColor.b, alpha),
                EndColor = RuneSparkFade,
                Twinkle = 0.2f,
            };

        private static Hd2dFlickerLightSource Torch(
            string name,
            Vector2 anchor,
            Vector2 reflection,
            float scale
        ) =>
            new()
            {
                Name = name,
                Anchor = anchor,
                Color = Flame,
                CoreSize = new Vector2(140f, 150f) * scale,
                CoreAlpha = 0.55f,
                HaloSize = new Vector2(460f, 460f) * scale,
                HaloAlpha = 0.22f,
                ReflectionAnchor = reflection,
                ReflectionSize = new Vector2(420f, 100f) * scale,
                ReflectionAlpha = 0.24f,
                FlickerAmount = 0.22f,
                FlickerSpeed = 2.4f,
                SizeJitter = 0.05f,
            };

        private static Hd2dEmberSource Embers(
            string name,
            Vector2 anchor,
            int count,
            float scale
        ) =>
            new()
            {
                Name = name,
                Anchor = anchor,
                SpawnArea = new Vector2(28f, 10f) * scale,
                Count = count,
                Loop = true,
                LifetimeRange = new Vector2(1.1f, 2.3f),
                Direction = 90f,
                Spread = 34f,
                SpeedRange = new Vector2(34f, 72f) * scale,
                Buoyancy = 14f * scale,
                // Curl noise bends the embers instead of a periodic sway.
                Sway = 0f,
                Curl = 36f * scale,
                CurlScale = 56f * scale,
                CurlSpeed = 0.5f,
                DotSize = 2,
                // The far torches get fewer large embers, so they read as smaller.
                CrossShare = scale < 1f ? 0.25f : 0.3f,
                StreakShare = scale < 1f ? 0.1f : 0.15f,
                StartColor = new Color(1f, 0.84f, 0.46f, 1f),
                EndColor = new Color(1f, 0.32f, 0.08f, 0f),
                Twinkle = 0.35f,
            };

        private static RawImage Actor(
            RectTransform world,
            string name,
            Texture2D texture,
            Rect uv,
            Vector2 center,
            Vector2 size,
            bool mirrored
        )
        {
            var rect = Rect(name, world);
            Place(rect, center, size);
            if (mirrored)
                rect.localScale = new Vector3(-1, 1, 1);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>The 4x2 character sheet: party members on the top row.</summary>
        public static Rect ActorUv(int index) =>
            new((index % 4) * 0.25f, index < 4 ? 0.5f : 0f, 0.25f, 0.5f);

        private static Image Icon(
            RectTransform parent,
            string name,
            string path,
            float size,
            Color color
        )
        {
            var rect = Rect(name, parent);
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ArtAssets.LoadSprite(path);
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = size;
            layout.minHeight = layout.preferredHeight = size;
            return image;
        }

        // An invisible child that widens the touch target without changing the drawn frame.
        private static void HitArea(RectTransform parent, float horizontal, float vertical)
        {
            var hit = Rect("HitArea", parent);
            Stretch(hit);
            hit.offsetMin = new Vector2(-horizontal, -vertical);
            hit.offsetMax = new Vector2(horizontal, vertical);
            AddImage(hit, Color.clear, true);
            hit.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        private static RectTransform Row(
            RectTransform parent,
            string name,
            float height,
            float spacing
        )
        {
            var row = Rect(name, parent);
            var group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = false;
            var size = row.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            return row;
        }

        private static void Flexible(TMP_Text label)
        {
            var size = label.GetComponent<LayoutElement>();
            if (size == null)
                size = label.gameObject.AddComponent<LayoutElement>();
            size.flexibleWidth = 1;
        }
    }
}
