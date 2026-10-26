using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Inventory;
using Baryonyx.UI;
using Baryonyx.UI.EditModePreview;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>The ground the exploration map is laid on, for one destination.</summary>
    [Serializable]
    public sealed class ExplorationGround
    {
        public string DestinationId = "";
        public Texture Texture;
    }

    /// <summary>
    /// The exploration screen (doc/features/stage-progression.md, 地図の作り直し): the
    /// adventure's route laid out from left to right on the destination's ground, its rooms as
    /// round pedestals with their kind's icon, joined by lines of light; the roads to the rooms
    /// the party can go to next shine and pulse, the roads further on glow faintly, the roads
    /// walked are dim and the roads no longer open fade away. The party stands on the room it is
    /// in, the chest of a room that holds one beside it. The map is wider than the screen: one
    /// finger dragged across it looks along the route (<see cref="Look"/>), a thin bar at the
    /// bottom shows where the screen is on it, and walking to a room brings the party back near
    /// the left of the screen. The place, floor and prompt are at the top right, the menu at the
    /// top left, and the "?" at the bottom right opens the legend. AdventureAssets builds it; the
    /// adventure's flow (<see cref="ExplorationFlow"/>) drives it. AdventureAssets also lays the
    /// sample route out once, so the prefab and the scene show the map outside Play Mode.
    /// </summary>
    public sealed partial class ExplorationView : MonoBehaviour, IEditModePreview
    {
        // 移動の時間：駒が次のマスへ跳ぶ空中の時間と、地図が駒に追いつくまでの時間（どれもマスを押してからの秒数）。
        public const float WalkSeconds = MapPawnHop.AirSeconds;
        public const float CameraSeconds = 0.6f;

        // マスの印が消える時間。選んだマスの印は、駒が着くときに消えきる。
        public const float MarkerFadeSeconds = 0.3f;
        private const float ChosenFadeDelaySeconds = WalkSeconds - MarkerFadeSeconds;

        // 地図の寸法（設計座標）：階の間隔、入口と最奥の間の外の余白、マスが並ぶ上下の幅と中心。
        // 上の行き先の文字と、下の位置のバーに掛からないよう、少し下に寄せる。
        public const float FloorGap = 300f;
        public const float EdgeMargin = 240f;
        public const float AcrossSpan = 660f;
        public const float AcrossCenter = -20f;

        // 4人を、画面の左から何割の所に置くか。先のマスを広く見せる。
        public const float PartyAtScreen = 0.26f;

        // 宝箱のドット絵は、1ドット3pxで置く。
        public const int MapDot = 3;

        // 地面の絵1枚の大きさ（設計座標）。地図いっぱいに並べて敷く。
        public const float GroundTile = 512f;

        // 印の大きさ（px）：次に進めるマスは大きく、最奥の間はさらに大きく、ほかのマスは小さく。
        public const float LargeRing = 112f;
        public const float SmallRing = 80f;
        public const float BossRing = 136f;
        public const float HereRing = 96f;

        // 道の太さ（px）：光の芯と、まわりの淡い光。
        private const float RoadCore = 7f;
        private const float RoadGlow = 22f;

        private static readonly Color RingExit = new(1f, 0.86f, 0.45f);
        private static readonly Color RingHere = new(1f, 0.95f, 0.78f);
        private static readonly Color RingFaint = new(0.28f, 0.31f, 0.33f);
        private static readonly Color PlateFaint = new(0.12f, 0.14f, 0.15f, 0.92f);
        private static readonly Color ExitGlow = new(1f, 0.82f, 0.36f, 0.6f);
        private static readonly Color EliteGlow = new(1f, 0.35f, 0.23f, 0.55f);
        private static readonly Color BossGlow = new(0.6f, 0.3f, 0.95f, 0.7f);
        private static readonly Color Faded = new(0.55f, 0.55f, 0.55f, 0.7f);

        // 道の色：次に進める道は金、その先の行ける道は淡い青、通った道は暗い金、行けなくなった道は消えかけ。
        private static readonly Color RoadExit = new(1f, 0.84f, 0.42f);
        private static readonly Color RoadAhead = new(0.56f, 0.82f, 1f, 0.95f);
        private static readonly Color RoadWalked = new(0.72f, 0.62f, 0.4f, 0.55f);
        private static readonly Color RoadClosed = new(0.55f, 0.6f, 0.66f, 0.16f);

        // マスの種類ごとの札の地の色と、縁の明るい色（AdventureRoomKind の順）。
        // いちばん多い魔物は目立たない青灰色にし、強い魔物は赤、宝箱は金、最奥の間は紫で目を引く。
        // 休憩は焚き火の橙、行商人は革の茶、怪しい宝箱はくすんだ紫、試練は深紅、歩数の扉は緑、
        // 進化の祭壇は青白い光の色、「？」は中身の分からない灰青にする。
        private static readonly Color[] KindPlates =
        {
            new(0.11f, 0.165f, 0.19f, 0.96f),
            new(0.15f, 0.19f, 0.23f, 0.96f),
            new(0.46f, 0.08f, 0.08f, 0.96f),
            new(0.42f, 0.30f, 0.05f, 0.96f),
            new(0.26f, 0.11f, 0.42f, 0.96f),
            new(0.42f, 0.19f, 0.05f, 0.96f),
            new(0.30f, 0.21f, 0.12f, 0.96f),
            new(0.20f, 0.16f, 0.25f, 0.96f),
            new(0.40f, 0.06f, 0.22f, 0.96f),
            new(0.07f, 0.30f, 0.14f, 0.96f),
            new(0.08f, 0.22f, 0.42f, 0.96f),
            new(0.18f, 0.22f, 0.27f, 0.96f),
        };
        private static readonly Color[] KindAccents =
        {
            new(0.42f, 0.48f, 0.47f),
            new(0.60f, 0.67f, 0.75f),
            new(1f, 0.34f, 0.26f),
            new(1f, 0.80f, 0.30f),
            new(0.70f, 0.45f, 0.98f),
            new(1f, 0.62f, 0.28f),
            new(0.92f, 0.72f, 0.44f),
            new(0.68f, 0.58f, 0.80f),
            new(1f, 0.40f, 0.62f),
            new(0.46f, 1f, 0.55f),
            new(0.56f, 0.86f, 1f),
            new(0.86f, 0.80f, 0.62f),
        };

        public static Color PlateOf(AdventureRoomKind kind) => KindPlates[(int)kind];

        public static Color AccentOf(AdventureRoomKind kind) => KindAccents[(int)kind];

        [Header("地図")]
        // 画面いっぱいの地図の窓。中の Content を左右に動かして、道の先を見る。
        public RectTransform Map;
        public RectTransform Content;
        public RawImage Ground;
        public ExplorationGround[] Grounds = Array.Empty<ExplorationGround>();

        // 1本指で地図を左右に動かし、道の先を見る。
        public ExplorationMapDrag Drag;
        public RectTransform Roads;
        public RectTransform RoadTemplate;
        public RectTransform Markers;
        public ExplorationMapMarker MarkerTemplate;

        // 地図の上でパーティを表す駒と、マスの中心からの足元の位置（設計座標）。
        public RectTransform[] Party = Array.Empty<RectTransform>();
        public Vector2[] Formation = Array.Empty<Vector2>();

        // 駒の跳ねる動き。次のマスへ1回跳ぶ。
        public MapPawnHop PawnHop;
        public RectTransform Chest;
        public CanvasGroup ChestGroup;
        public Button ChestButton;
        public RawImage ChestSprite;
        public Texture ChestClosed;
        public Texture ChestOpen;

        [Header("手前")]
        public TMP_Text Location;
        public TMP_Text Prompt;
        public Button MenuButton;

        // メニューの下の「装備」。冒険を続けたまま、酒場の装備の付け替えを開く。
        public Button EquipmentButton;
        public GameDialog Dialog;
        public NoticeBand Notice;

        // 休憩の焚き火（休む・鍛える・研鑽を選ぶ）。
        public ExplorationCampfire Campfire;

        // 戦闘後の強化を選ぶ前にアプリが終わったとき、再開した探索で開き直す3枚のカード。
        public AdventureBoonPicker Boons;

        // 地図のどこを見ているかを示す、下の細いバー。
        public RectTransform ScrollTrack;
        public RectTransform ScrollThumb;

        // 右下の「？」と、押すと開く凡例：札の色とアイコンが、どのマスの種類かを示す。
        public Button LegendButton;
        public RectTransform Legend;

        // マスの種類ごとのアイコン（AdventureRoomKind の順）。
        public Sprite[] KindIcons = Array.Empty<Sprite>();

        // ドロップのアイテム・装備の絵（種類のIDで引く）。
        public ItemArt ItemArt;

        // 見本の種。冒険の流れが動かさないとき（展示室など）に、この種の道を描く。0なら描かない。
        public int SampleSeed;

        private readonly List<ExplorationMapMarker> pool = new();
        private readonly Dictionary<string, ExplorationMapMarker> shown = new();
        private readonly List<Road> roadPool = new();
        private readonly List<Road> roads = new();

        // 地図を左右に動かした量（設計座標、0で入口が画面の左）と、パーティの位置（地図の中の設計座標）。
        private float scroll;
        private Vector2 partyAt;
        private AdventureRouteMap laidOut;
        private float width;
        private Move move;
        private bool walking;
        private string arrivedAt;

        // 宝箱を置いたマスと、宝箱の見え方（1で見える）。
        private string chestRoom;
        private float chestAlpha;
        private float chestTarget;
        private Coroutine animating;
        private int animationRun;
        private bool wired;

        /// <summary>One road between two rooms: the line of light and the glow around it.</summary>
        private sealed class Road
        {
            public RectTransform Body;
            public Image Core;
            public Image Glow;
            public bool Pulses;
        }

        /// <summary>A walk along a road, with the map following the party.</summary>
        private sealed class Move
        {
            public string RoomId = "";
            public Vector2 From;
            public Vector2 To;
            public float ScrollFrom;
            public float ScrollTo;
            public float Elapsed;

            // 4人と地図が着いたときに呼ぶ。呼んだあとは null。
            public Action Arrived;
        }

        public event Action<string> RoomPressed;
        public event Action ChestPressed;
        public event Action MenuPressed;
        public event Action EquipmentPressed;

        // 端末の戻るキー。開いているダイアログが先に受け取り、なければメニューを開く。
        public event Action BackPressed;

        public AdventureRun ShownRun { get; private set; }
        private ExplorationRoom shownRoom;

        // 4人が道を歩き、地図が追いかけている。
        public bool Walking => walking;

        // 着いたあと、消える印や宝箱が消え切るまで。
        public bool Clearing => animating != null && !walking;

        // 地図を左右に動かした量（設計座標）と、動かせる上限。
        public float Scroll => scroll;
        public float MaxScroll => Mathf.Max(0f, width - ViewportWidth);
        public float MapWidth => width;
        public string LocationText => Location != null ? Location.text : "";
        public string PromptText => Prompt != null ? Prompt.text : "";
        public Vector2 PartyPosition => partyAt;
        public IReadOnlyCollection<ExplorationMapMarker> ShownMarkers => shown.Values;
        public int RoadCount => roads.Count;
        public bool LegendShown => Legend != null && Legend.gameObject.activeSelf;

        // 画面に入る地図の幅（設計座標）。配置が済む前は、設計の画面幅とする。
        private float ViewportWidth => Map != null && Map.rect.width > 0f ? Map.rect.width : 1920f;

        public ExplorationMapMarker MarkerOf(string roomId) =>
            roomId != null && shown.TryGetValue(roomId, out var marker) ? marker : null;

        /// <summary>Where a room lies on the map (design units, from the map's left and middle).</summary>
        public Vector2 PointOf(string roomId)
        {
            var map = laidOut ?? ShownRun?.Map;
            var point = map?.PointOf(roomId);
            if (point == null)
                return Vector2.zero;
            return new Vector2(
                EdgeMargin + point.T * (map.RoomCount + 1) * FloorGap,
                AcrossCenter + (0.5f - point.S) * AcrossSpan
            );
        }

        /// <summary>Where on the screen a room is now, from the screen's left (design units).</summary>
        public float ScreenXOf(string roomId) => PointOf(roomId).x - scroll;

        private void Awake()
        {
            if (MarkerTemplate != null)
                MarkerTemplate.gameObject.SetActive(false);
            if (RoadTemplate != null)
                RoadTemplate.gameObject.SetActive(false);
            if (Legend != null)
                Legend.gameObject.SetActive(false);
            KeepSample();
            Wire();
        }

        // 生成時に見本の道を描いて作った印と道は、隠して使い回す。
        private void KeepSample()
        {
            if (Markers != null)
                foreach (var marker in Markers.GetComponentsInChildren<ExplorationMapMarker>(true))
                    if (marker != MarkerTemplate && !pool.Contains(marker))
                    {
                        Keep(marker);
                        marker.gameObject.SetActive(false);
                    }
            if (Roads != null)
                foreach (Transform child in Roads)
                    if (child != RoadTemplate && Wrap((RectTransform)child) is { } road)
                    {
                        if (roadPool.Any(entry => entry.Body == road.Body))
                            continue;
                        roadPool.Add(road);
                        child.gameObject.SetActive(false);
                    }
            shown.Clear();
            roads.Clear();
        }

        private void Start()
        {
            if (SampleSeed == 0 || ShownRun != null)
                return;
            RenderSample(SampleSeed);
            // 展示室では、見本の道のマスを押すと、そこまで歩いて先へ進む様子を確かめられる。
            RoomPressed += WalkSample;
        }

        /// <summary>On the sample route, the party walks to <paramref name="roomId"/> and the room it reaches shows.</summary>
        public void WalkSample(string roomId)
        {
            var run = ShownRun;
            if (Walking || run?.Map == null)
                return;
            var next = new AdventureRun(
                run.Id,
                run.DestinationId,
                roomId,
                true,
                run.Route.Append(roomId).ToArray(),
                0,
                0,
                run.Map,
                run.Drops
            );
            WalkTo(roomId, () => Render(ExplorationRoom.From(next)));
        }

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
            if (ChestButton != null)
                ChestButton.onClick.AddListener(() => ChestPressed?.Invoke());
            if (MenuButton != null)
                MenuButton.onClick.AddListener(() => MenuPressed?.Invoke());
            if (EquipmentButton != null)
                EquipmentButton.onClick.AddListener(() => EquipmentPressed?.Invoke());
            if (LegendButton != null)
                LegendButton.onClick.AddListener(ToggleLegend);
            if (Legend != null && Legend.GetComponent<Button>() is { } close)
                close.onClick.AddListener(ToggleLegend);
            if (Drag != null)
                Drag.Dragged += Look;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                PressBack();
            Pulse(Time.unscaledTime);
        }

        /// <summary>
        /// The device's back key, as <see cref="Update"/> reads it: a dialog first, then the
        /// rest's campfire, then the legend, then the menu.
        /// </summary>
        public void PressBack()
        {
            if (Dialog != null && Dialog.HandleBack())
                return;
            if (Boons != null && Boons.HandleBack())
                return;
            if (Campfire != null && Campfire.HandleBack())
                return;
            if (LegendShown)
            {
                ToggleLegend();
                return;
            }
            BackPressed?.Invoke();
        }

        /// <summary>Opens the legend, or closes it when open.</summary>
        public void ToggleLegend()
        {
            if (Legend != null)
                Legend.gameObject.SetActive(!Legend.gameObject.activeSelf);
        }

        /// <summary>Draws the route of a seed from its entrance, as a sample (the showcase).</summary>
        public void RenderSample(int seed)
        {
            var map = AdventureCatalog.Route(
                AdventureCatalog.ForestRuins,
                seed,
                AdventureCatalog.RoomCount(AdventureCatalog.ForestRuins)
            );
            Render(
                ExplorationRoom.From(
                    new AdventureRun(
                        "sample",
                        AdventureCatalog.ForestRuins,
                        AdventureRouteMap.EntranceId,
                        true,
                        new[] { AdventureRouteMap.EntranceId },
                        0,
                        0,
                        map,
                        Array.Empty<AdventureDrop>()
                    )
                )
            );
        }

        /// <summary>
        /// Shows the room: the route on the destination's ground, the party in the room, the
        /// rooms of the next floor to choose once the room is done, and its chest. The room the
        /// party just walked to keeps the map where it followed the party; any other room puts the
        /// party near the left of the screen.
        /// </summary>
        public void Render(ExplorationRoom room)
        {
            if (room == null)
                throw new ArgumentNullException(nameof(room));
            var run = room.Run;
            bool arrived = run?.Map != null && arrivedAt != null && run.RoomId == arrivedAt;
            arrivedAt = null;
            StopAnimation();
            if (Location != null)
                Location.text = room.Location;
            if (Prompt != null)
                Prompt.text = room.Prompt;
            ShownRun = run;
            shownRoom = room;
            if (run?.Map == null)
            {
                HideMarkers();
                HideRoads();
                PlaceChest(room);
                return;
            }
            LayOut(run);
            partyAt = PointOf(run.RoomId);
            if (!arrived || laidOut == null)
                scroll = Home(partyAt.x);
            PlaceRoads(run, room, run);
            PlaceMarkers(run, room);
            PlaceParty();
            PlaceChest(room);
            ApplyScroll();
        }

        // 道の長さに合わせて地図の幅を決め、行き先の地面を敷く。
        private void LayOut(AdventureRun run)
        {
            laidOut = run.Map;
            width = EdgeMargin * 2f + (run.Map.RoomCount + 1) * FloorGap;
            if (Content != null)
                Content.sizeDelta = new Vector2(width, Content.sizeDelta.y);
            if (Ground == null)
                return;
            var ground =
                Grounds.FirstOrDefault(entry => entry.DestinationId == run.DestinationId)
                ?? Grounds.FirstOrDefault();
            if (ground?.Texture != null)
            {
                Ground.texture = ground.Texture;
                Ground.color = Color.white;
            }
            float height =
                Content != null && Content.rect.height > 0f ? Content.rect.height : 1080f;
            Ground.uvRect = new Rect(0f, 0f, width / GroundTile, height / GroundTile);
        }

        // 4人を画面の左寄りに置く地図の位置。地図の端より先には動かさない。
        private float Home(float x) =>
            Mathf.Clamp(x - ViewportWidth * PartyAtScreen, 0f, MaxScroll);

        /// <summary>
        /// Looks along the route: the finger's move across the map (design units) moves the map
        /// with it, from the entrance at the left of the screen to the deepest room at the right.
        /// The map stays where it is left; walking to a room brings it back to the party.
        /// </summary>
        public void Look(Vector2 delta)
        {
            if (walking || ShownRun?.Map == null)
                return;
            float to = Mathf.Clamp(scroll - delta.x, 0f, MaxScroll);
            if (Mathf.Approximately(to, scroll))
                return;
            scroll = to;
            ApplyScroll();
        }

        private void ApplyScroll()
        {
            if (Content != null)
                Content.anchoredPosition = new Vector2(
                    -Mathf.Round(scroll),
                    Content.anchoredPosition.y
                );
            if (ScrollTrack == null || ScrollThumb == null)
                return;
            bool scrolls = MaxScroll > 0f && width > 0f;
            ScrollTrack.gameObject.SetActive(scrolls);
            if (!scrolls)
                return;
            float track =
                ScrollTrack.rect.width > 0f ? ScrollTrack.rect.width : ScrollTrack.sizeDelta.x;
            float thumb = Mathf.Max(64f, track * ViewportWidth / width);
            ScrollThumb.sizeDelta = new Vector2(thumb, ScrollThumb.sizeDelta.y);
            ScrollThumb.anchoredPosition = new Vector2(
                (track - thumb) * (scroll / MaxScroll),
                ScrollThumb.anchoredPosition.y
            );
        }

        // --- Roads ---------------------------------------------------------------------------

        private void HideRoads()
        {
            foreach (var road in roads)
                road.Body.gameObject.SetActive(false);
            roads.Clear();
        }

        // 道を引く。walked は通った道として描く冒険（マスを選ぶと、着く前から選んだ道を通った道にする）。
        private void PlaceRoads(AdventureRun run, ExplorationRoom room, AdventureRun walked)
        {
            HideRoads();
            if (RoadTemplate == null || Roads == null)
                return;
            var map = run.Map;
            var route = walked.Route;
            var passed = new HashSet<(string, string)>();
            for (int i = 1; i < route.Count; i++)
                passed.Add((route[i - 1], route[i]));
            var here = map.Find(walked.RoomId);
            bool open = room.Exits.Count > 0 && walked.RoomId == run.RoomId;
            var exits = open
                ? new HashSet<string>(room.Exits.Select(exit => exit.Id))
                : new HashSet<string>();
            var ahead = map.Reachable(here != null ? here.Next : Array.Empty<string>());
            foreach (var from in map.Rooms)
            foreach (var to in from.Next)
            {
                Color color;
                bool pulses = false;
                float core = RoadCore;
                if (passed.Contains((from.Id, to)))
                    color = RoadWalked;
                else if (from.Id == walked.RoomId && exits.Contains(to))
                {
                    color = RoadExit;
                    pulses = true;
                    core = RoadCore + 3f;
                }
                else if (
                    (from.Id == walked.RoomId || ahead.Contains(from.Id)) && ahead.Contains(to)
                )
                    color = RoadAhead;
                else
                    color = RoadClosed;
                Draw(PointOf(from.Id), PointOf(to), color, core, pulses);
            }
        }

        private void Draw(Vector2 from, Vector2 to, Color color, float core, bool pulses)
        {
            var road = TakeRoad();
            var delta = to - from;
            road.Body.anchoredPosition = (from + to) * 0.5f;
            road.Body.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg
            );
            float length = delta.magnitude;
            road.Core.rectTransform.sizeDelta = new Vector2(length, core);
            road.Core.color = color;
            road.Glow.rectTransform.sizeDelta = new Vector2(length + RoadGlow * 0.5f, RoadGlow);
            road.Glow.color = new Color(color.r, color.g, color.b, color.a * 0.42f);
            road.Glow.gameObject.SetActive(color.a > 0.3f);
            road.Pulses = pulses;
            roads.Add(road);
        }

        private Road TakeRoad()
        {
            var road = roadPool.FirstOrDefault(entry =>
                !entry.Body.gameObject.activeSelf && !roads.Contains(entry)
            );
            if (road == null)
            {
                var body = Instantiate(RoadTemplate, Roads);
                body.name = "Road" + roadPool.Count;
                road = Wrap(body);
                roadPool.Add(road);
            }
            road.Body.gameObject.SetActive(true);
            return road;
        }

        private static Road Wrap(RectTransform body)
        {
            var coreBody = body.Find("Core");
            var glowBody = body.Find("Glow");
            var core = coreBody != null ? coreBody.GetComponent<Image>() : null;
            var glow = glowBody != null ? glowBody.GetComponent<Image>() : null;
            return core != null && glow != null
                ? new Road
                {
                    Body = body,
                    Core = core,
                    Glow = glow,
                }
                : null;
        }

        // 次に進める道と、次に進めるマスの光を、ゆっくり脈打たせる。
        private void Pulse(float time)
        {
            if (!Application.isPlaying)
                return;
            float beat = 0.5f + 0.5f * Mathf.Sin(time * 3.2f);
            foreach (var road in roads)
                if (road.Pulses)
                    road.Glow.color = new Color(
                        RoadExit.r,
                        RoadExit.g,
                        RoadExit.b,
                        0.18f + 0.3f * beat
                    );
            foreach (var marker in shown.Values)
                if (marker.IsExit && marker.Glow != null && marker.Glow.gameObject.activeSelf)
                    marker.Glow.color = new Color(
                        ExitGlow.r,
                        ExitGlow.g,
                        ExitGlow.b,
                        0.3f + 0.4f * beat
                    );
        }

        // --- Rooms ---------------------------------------------------------------------------

        private void HideMarkers()
        {
            foreach (var marker in pool)
                marker.gameObject.SetActive(false);
            shown.Clear();
        }

        // マスの印を置く。今いるマスは4人が立つ台だけ、通ったマスと行けなくなったマスは暗く沈める。
        private void PlaceMarkers(AdventureRun run, ExplorationRoom room)
        {
            HideMarkers();
            if (MarkerTemplate == null || Markers == null)
                return;
            var exits = new HashSet<string>(room.Exits.Select(exit => exit.Id));
            var here = run.Room ?? run.Map.Find(AdventureRouteMap.EntranceId);
            var reachable = run.Map.Reachable(here != null ? here.Next : Array.Empty<string>());
            var passed = new HashSet<string>(run.Route);
            foreach (var mapRoom in run.Map.Rooms)
            {
                var marker = Take();
                marker.Alpha = marker.TargetAlpha = 1f;
                marker.Wait = 0f;
                Configure(
                    marker,
                    mapRoom,
                    mapRoom.Id == run.RoomId,
                    exits.Contains(mapRoom.Id),
                    reachable.Contains(mapRoom.Id),
                    passed.Contains(mapRoom.Id)
                );
                ApplyFade(marker);
                shown[mapRoom.Id] = marker;
            }
        }

        private ExplorationMapMarker Take()
        {
            var marker = pool.FirstOrDefault(entry => !entry.gameObject.activeSelf);
            if (marker == null)
            {
                marker = Instantiate(MarkerTemplate, Markers);
                marker.name = "Room" + pool.Count;
                Keep(marker);
            }
            marker.gameObject.SetActive(true);
            return marker;
        }

        private static void ApplyFade(ExplorationMapMarker marker)
        {
            if (marker.Group != null)
                marker.Group.alpha = marker.Alpha;
        }

        private void Keep(ExplorationMapMarker marker)
        {
            marker.Button.onClick.AddListener(() => RoomPressed?.Invoke(marker.RoomId));
            pool.Add(marker);
        }

        private void Configure(
            ExplorationMapMarker marker,
            AdventureRoom room,
            bool here,
            bool exit,
            bool reachable,
            bool passed
        )
        {
            marker.RoomId = room.Id;
            marker.IsExit = exit;
            bool boss = room.Kind == AdventureRoomKind.Boss;
            bool start = room.Kind == AdventureRoomKind.Start;
            bool lit = exit || reachable || boss;
            marker.Body.anchoredPosition = PointOf(room.Id);

            // 大きい札は24ドットのアイコンを3倍、小さい札は2倍で出す。今いるマスと入口は台だけ。
            float ring =
                boss ? BossRing
                : exit ? LargeRing
                : here ? HereRing
                : SmallRing;
            marker.Ring.rectTransform.sizeDelta = Vector2.one * ring;
            marker.Plate.rectTransform.sizeDelta = Vector2.one * (ring - (exit || boss ? 12 : 8));
            marker.Icon.rectTransform.sizeDelta = Vector2.one * (exit || boss ? 72 : 48);
            marker.Ring.color =
                here ? RingHere
                : exit ? RingExit
                : lit ? AccentOf(room.Kind)
                : RingFaint;
            marker.Plate.color =
                here || start ? PlateFaint
                : lit ? PlateOf(room.Kind)
                : PlateFaint;
            marker.Icon.sprite = KindIcon(room.Kind);
            marker.Icon.enabled = marker.Icon.sprite != null && !here && !start;
            marker.Icon.color = lit ? Color.white : Faded;

            // 次に進めるマスは金の光、行ける先の強い魔物は赤い光、最奥の間は紫の光で見せる。
            bool eliteGlow = reachable && !exit && room.Kind == AdventureRoomKind.Elite;
            marker.Glow.gameObject.SetActive(exit || boss || eliteGlow);
            marker.Glow.color =
                exit ? ExitGlow
                : boss ? BossGlow
                : EliteGlow;
            marker.Glow.rectTransform.sizeDelta =
                Vector2.one
                * (
                    boss ? 300
                    : exit ? 220
                    : 150
                );
            // 通ったマスは、来た道が分かるよう薄く残す。
            marker.Alpha = marker.TargetAlpha = passed && !here ? 0.55f : 1f;

            marker.Button.interactable = exit;
            if (marker.Button.targetGraphic != null)
                marker.Button.targetGraphic.raycastTarget = exit;
        }

        private void PlaceParty()
        {
            for (int i = 0; i < Party.Length && i < Formation.Length; i++)
                Party[i].anchoredPosition = Snap(partyAt + Formation[i]);
        }

        private static Vector2 Snap(Vector2 position) =>
            new(Mathf.Round(position.x), Mathf.Round(position.y));

        // 宝箱は、宝箱のマスの右手前に置く。マスを離れると、マスに残したまま薄れて消える。
        private void PlaceChest(ExplorationRoom room)
        {
            if (ChestGroup == null)
                return;
            bool chest = room.Chest != ExplorationChest.None && room.Run?.Map != null;
            bool closed = chest && room.Chest == ExplorationChest.Closed;
            ChestGroup.blocksRaycasts = closed;
            ChestGroup.interactable = closed;
            if (ChestButton != null)
                ChestButton.gameObject.SetActive(closed);
            chestAlpha = chestTarget = chest ? 1f : 0f;
            ChestGroup.alpha = chestAlpha;
            if (!chest)
                return;
            chestRoom = room.Run.RoomId;
            PlaceChestOnMap();
            if (ChestSprite != null)
                ChestSprite.texture = room.Chest == ExplorationChest.Open ? ChestOpen : ChestClosed;
        }

        private void PlaceChestOnMap()
        {
            if (Chest != null && chestRoom != null)
                Chest.anchoredPosition = Snap(PointOf(chestRoom) + new Vector2(96f, -36f));
        }

        // --- Walking -------------------------------------------------------------------------

        /// <summary>
        /// The party's pawn hops along the road to the room with the map following it, then
        /// <paramref name="arrived"/> runs. The road chosen is drawn as walked at once, the other
        /// roads as closed, and the rooms not chosen fade.
        /// </summary>
        public void WalkTo(string roomId, Action arrived)
        {
            var run = ShownRun;
            bool known = run?.Map?.Find(roomId) != null && run.Map.Find(run.RoomId) != null;
            if (!known || !isActiveAndEnabled)
            {
                arrived?.Invoke();
                return;
            }
            StopAnimation();
            var to = PointOf(roomId);
            move = new Move
            {
                RoomId = roomId,
                From = partyAt,
                To = to,
                ScrollFrom = scroll,
                ScrollTo = Home(to.x),
                Arrived = arrived,
            };
            walking = true;
            var walked = new AdventureRun(
                run.Id,
                run.DestinationId,
                roomId,
                false,
                run.Route.Append(roomId).ToArray(),
                run.Revives,
                run.ReviveCost,
                run.Map,
                run.Drops,
                run.Floor + 1
            );
            PlaceRoads(run, shownRoom, walked);
            // 選んだマスの印は着くときに、ほかの次に進めたマスの印はすぐに薄れて消える。
            foreach (var marker in shown.Values)
                if (marker.IsExit)
                {
                    marker.TargetAlpha = marker.RoomId == roomId ? 0f : 0.45f;
                    marker.Wait = marker.RoomId == roomId ? ChosenFadeDelaySeconds : 0f;
                    marker.Button.interactable = false;
                }
            chestTarget = 0f;
            if (PawnHop != null && Application.isPlaying)
                PawnHop.Play(to.x - partyAt.x);
            Animate();
        }

        private void Animate()
        {
            if (animating != null)
                return;
            // 停止中（生成時の描画）は、移動の途中を見せずに描き終える。
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                while (Step(MarkerFadeSeconds)) { }
                return;
            }
            animating = StartCoroutine(Run(++animationRun));
        }

        private IEnumerator Run(int id)
        {
            while (Step(Time.deltaTime) && id == animationRun)
                yield return null;
            if (id == animationRun)
                animating = null;
        }

        private void StopAnimation()
        {
            animationRun++;
            if (animating != null)
                StopCoroutine(animating);
            animating = null;
            move = null;
            walking = false;
        }

        private static float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // 1コマ進める：駒と地図を道に沿って動かし、印と宝箱を薄れさせる。まだ動いている物があれば true。
        private bool Step(float seconds)
        {
            bool moving = false;
            if (move != null)
            {
                move.Elapsed += seconds;
                // 駒は空中を一定の速さで進み、地図はゆっくり出てゆっくり止まる。
                float walked = Mathf.Clamp01(move.Elapsed / WalkSeconds);
                float followed = Ease(move.Elapsed / CameraSeconds);
                partyAt = Vector2.Lerp(move.From, move.To, walked);
                scroll = Mathf.Lerp(move.ScrollFrom, move.ScrollTo, followed);
                PlaceParty();
                ApplyScroll();
                if (move.Arrived != null && walked >= 1f && followed >= 1f)
                {
                    var arrived = move.Arrived;
                    arrivedAt = move.RoomId;
                    move = null;
                    walking = false;
                    // 着いたマスを見せる（Render）。
                    arrived();
                    return animating != null && FadeMarkers(seconds);
                }
                moving = true;
            }
            moving |= FadeMarkers(seconds);
            return moving;
        }

        private bool FadeMarkers(float seconds)
        {
            bool fading = false;
            float step = seconds / MarkerFadeSeconds;
            foreach (var marker in pool)
            {
                if (!marker.gameObject.activeSelf)
                    continue;
                if (marker.Wait > 0f)
                    marker.Wait -= seconds;
                else
                    marker.Alpha = Mathf.MoveTowards(marker.Alpha, marker.TargetAlpha, step);
                ApplyFade(marker);
                if (marker.Alpha != marker.TargetAlpha)
                    fading = true;
            }
            if (ChestGroup != null)
            {
                chestAlpha = Mathf.MoveTowards(chestAlpha, chestTarget, step);
                ChestGroup.alpha = chestAlpha;
                fading |= chestAlpha != chestTarget;
            }
            return fading;
        }

        public void ShowNotice(string message)
        {
            if (Notice != null)
                Notice.Show(message);
        }

        // 停止中の表示の切り替え：探索のダイアログを、ExplorationFlow と同じ AdventureDialogs で出す。
        public IReadOnlyList<string> PreviewStates =>
            new[]
            {
                "メニュー",
                "やめる確認",
                "宝箱",
                "結果（踏破）",
                "結果（帰還）",
                "取得できない",
            };

        public void ShowPreviewState(int index)
        {
            if (Dialog == null)
                return;
            Action none = () => { };
            switch (index)
            {
                case 0:
                    AdventureDialogs.Menu(
                        Dialog,
                        AdventureDialogSamples.Run,
                        null,
                        AdventureTexts.CloseChoice,
                        none,
                        none,
                        none
                    );
                    break;
                case 1:
                    AdventureDialogs.ConfirmQuit(Dialog, none, none, none);
                    break;
                case 2:
                    AdventureDialogs.Treasure(
                        Dialog,
                        AdventureDialogSamples.Drops(),
                        none,
                        ItemArt
                    );
                    break;
                case 3:
                case 4:
                    AdventureDialogs.Result(
                        Dialog,
                        AdventureDialogSamples.Result(
                            index == 3 ? AdventureEndStatus.Cleared : AdventureEndStatus.Retreated
                        ),
                        none
                    );
                    break;
                default:
                    AdventureDialogs.LoadFailed(Dialog, none, none);
                    break;
            }
        }

        public Sprite KindIcon(AdventureRoomKind kind) =>
            (int)kind < KindIcons.Length ? KindIcons[(int)kind] : null;
    }
}
