using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.StepBonus;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The exploration screen (doc/features/screens.md): the forest seen from above with the
    /// adventure's branching route on it, the party standing in the room it is in, the rooms of
    /// the next floor it can go to with their hints, the rooms further on as icons and marks
    /// fading into the fog, the deepest room's ruin far away, the chest of a treasure room, the
    /// place and floor at the top, a prompt at the bottom and the menu at the top left. A room
    /// tapped sends the party walking along its road with the camera following it, and once it
    /// arrives the fog ahead clears and the rooms newly in sight appear. One finger dragged on
    /// the map looks along the route ahead (<see cref="Look"/>). AdventureAssets builds
    /// it; the adventure's flow (<see cref="ExplorationFlow"/>) drives it. AdventureAssets also
    /// draws the sample route into it once, so the prefab and the scene show the map outside
    /// Play Mode; on <see cref="Awake"/> the sample's picture is hidden and its markers and
    /// floors are reused.
    /// </summary>
    public sealed class ExplorationView : MonoBehaviour
    {
        // 移動の時間：4人が道を歩く時間、カメラが追い始めるまでと追いつくまで、
        // 霧が晴れ始めるまでと晴れ切るまで（どれも部屋を押してからの秒数）。
        public const float WalkSeconds = 1.2f;
        public const float CameraDelaySeconds = 0.2f;
        public const float CameraSeconds = 1.3f;
        public const float FogDelaySeconds = 1.0f;
        public const float FogSeconds = 1.2f;

        // 霧が晴れる途中で、霧をいちばん薄くする割合。晴れ終わると元の濃さに戻る。
        public const float FogThinning = 0.12f;

        // 部屋の印・宝箱が現れる・消える時間と、霧が晴れて現れる印を奥の階ほど遅らせる時間。
        public const float MarkerFadeSeconds = 0.4f;
        public const float RevealStepSeconds = 0.12f;

        // 選んだ部屋の印は、4人が着く少し前に消す。
        private const float ChosenFadeDelaySeconds = 0.6f;

        // 1本指で先を見るとき、カメラを進められる最も奥の階（最奥の間から数えた階の数）。
        private const int LookFloors = 3;

        // 部屋の印を、空き地から浮かせる高さ（ドット）。
        private const float Lift = 15f;
        private const int Dot = ExplorationMapProjection.Dot;

        private static readonly Color RingExit = new(1f, 0.953f, 0.812f);
        private static readonly Color RingAhead = new(0.416f, 0.478f, 0.471f);
        private static readonly Color RingFaint = new(0.17f, 0.21f, 0.19f);
        private static readonly Color RingBoss = new(0.627f, 0.376f, 0.878f);
        private static readonly Color EliteGlow = new(1f, 0.35f, 0.23f, 0.85f);
        private static readonly Color BossGlow = new(0.55f, 0.25f, 0.9f, 0.6f);
        private static readonly Color Faded = new(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color FloorHere = new(0.949f, 0.761f, 0.31f);

        [Header("地図")]
        public RectTransform Map;
        public RawImage MapImage;

        // 停止中に見せる、生成時に描いた見本の地図の絵。EditorOnlyのためビルドには入らない。
        public RawImage MapPreview;

        // 1本指で地図を動かし、道の先を見る。
        public ExplorationMapDrag Drag;
        public ExplorationMapArt Art;
        public RectTransform Markers;
        public ExplorationMapMarker MarkerTemplate;

        // 地図の上のパーティと、部屋の中心からの隊列の位置（設計座標）。
        public RectTransform[] Party = Array.Empty<RectTransform>();
        public Vector2[] Formation = Array.Empty<Vector2>();
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
        public GameDialog Dialog;
        public NoticeBand Notice;
        public RectTransform Floors;
        public TMP_Text FloorTemplate;

        // 部屋の種類ごとのアイコン（AdventureRoomKind の順）。
        public Sprite[] KindIcons = Array.Empty<Sprite>();

        // 報酬のボーナスの名前とアイコン。
        public StepBonusMockData Bonuses;

        // 見本の種。冒険の流れが動かさないとき（展示室など）に、この種の道を描く。0なら描かない。
        public int SampleSeed;

        private readonly List<ExplorationMapMarker> pool = new();
        private readonly Dictionary<string, ExplorationMapMarker> shown = new();
        private readonly List<ExplorationMapMarker> leaving = new();
        private readonly List<TMP_Text> floorLabels = new();
        private Texture2D texture;
        private Color32[] pixels;
        private ExplorationMapPainter painter;

        // 地図の絵に描いている冒険の状態。部屋を選ぶと、着く前から選んだ道を通った道として描く。
        private AdventureRun paintedRun;
        private ExplorationMapSight paintedSight;
        private ExplorationMapProjection projection;

        // 道の上の位置（x が t、y が s）：カメラ、霧のカメラ、パーティ。
        private Vector2 cameraAt;
        private Vector2 fogAt;
        private Vector2 partyRoute;
        private Vector2 partyAt;
        private Move move;
        private bool walking;
        private string revealFor;

        // 宝箱を置いた部屋と、宝箱の見え方（1で見える）。
        private string chestRoom;
        private float chestAlpha;
        private float chestTarget;
        private Coroutine animating;
        private int animationRun;
        private bool repaint;
        private bool wired;

        /// <summary>A walk along a road, with the camera and the fog following the party.</summary>
        private sealed class Move
        {
            public Vector2[] Road;
            public float Elapsed;

            // 歩き出したときのカメラと霧の、道の始めからのずれ（先を見ていた分）。追いつくまでに縮める。
            public Vector2 CameraOffset;
            public Vector2 FogOffset;

            // 4人とカメラが着いたときに呼ぶ。呼んだあとは null。
            public Action Arrived;

            public Vector2 At(float progress)
            {
                float along = Mathf.Clamp01(progress) * (Road.Length - 1);
                int i = Mathf.Min(Mathf.FloorToInt(along), Road.Length - 2);
                return Vector2.Lerp(Road[i], Road[i + 1], along - i);
            }
        }

        public event Action<string> RoomPressed;
        public event Action ChestPressed;
        public event Action MenuPressed;

        // 端末の戻るキー。開いているダイアログが先に受け取り、なければメニューを開く。
        public event Action BackPressed;

        public AdventureRun ShownRun { get; private set; }
        private ExplorationRoom shownRoom;

        // 4人が道を歩き、カメラが追いかけている。
        public bool Walking => walking;

        // 着いたあと、霧が晴れて印や木が現れ切るまで。
        public bool Clearing => animating != null && !walking;

        // カメラが見下ろしている道の上の位置（x が t、y が s）。
        public Vector2 CameraPoint => cameraAt;
        public string LocationText => Location != null ? Location.text : "";
        public string PromptText => Prompt != null ? Prompt.text : "";
        public Vector2 PartyPosition => partyAt;
        public IReadOnlyCollection<ExplorationMapMarker> ShownMarkers => shown.Values;
        public IReadOnlyList<string> FloorTexts =>
            floorLabels
                .Where(label => label.gameObject.activeSelf)
                .Select(label => label.text)
                .ToArray();

        public ExplorationMapMarker MarkerOf(string roomId) =>
            roomId != null && shown.TryGetValue(roomId, out var marker) ? marker : null;

        private void Awake()
        {
            if (MarkerTemplate != null)
                MarkerTemplate.gameObject.SetActive(false);
            if (FloorTemplate != null)
                FloorTemplate.gameObject.SetActive(false);
            if (MapPreview != null)
                MapPreview.gameObject.SetActive(false);
            KeepSample();
            Wire();
        }

        // 生成時に見本の道を描いて作った印と階は、隠して使い回す。
        private void KeepSample()
        {
            if (Markers != null)
                foreach (var marker in Markers.GetComponentsInChildren<ExplorationMapMarker>(true))
                    if (marker != MarkerTemplate)
                    {
                        Keep(marker);
                        marker.gameObject.SetActive(false);
                    }
            if (Floors == null)
                return;
            foreach (Transform child in Floors)
                if (child.TryGetComponent<TMP_Text>(out var label) && label != FloorTemplate)
                {
                    label.gameObject.SetActive(false);
                    floorLabels.Add(label);
                }
        }

        private void Start()
        {
            if (SampleSeed == 0 || ShownRun != null)
                return;
            RenderSample(SampleSeed);
            // 展示室では、見本の道の部屋を押すと、そこまで歩いて霧が晴れる様子を確かめられる。
            RoomPressed += WalkSample;
        }

        private void WalkSample(string roomId)
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
                run.Rewards
            );
            WalkTo(roomId, () => Render(ExplorationRoom.From(next)));
        }

        private void OnDestroy()
        {
            if (texture != null)
                Destroy(texture);
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
            if (Drag != null)
                Drag.Dragged += Look;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                PressBack();
        }

        /// <summary>The device's back key, as <see cref="Update"/> reads it.</summary>
        public void PressBack()
        {
            if (Dialog != null && Dialog.HandleBack())
                return;
            BackPressed?.Invoke();
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
                        Array.Empty<AdventureReward>()
                    )
                )
            );
        }

        /// <summary>
        /// Shows the room: the map seen from it, the rooms ahead as far as the party can see,
        /// the next floor's rooms to choose once the room is done, and its chest. The room the
        /// party just walked to keeps the camera where it arrived: the fog goes on clearing and
        /// the rooms newly in sight appear, nearer floors first. Any other room is shown at once.
        /// </summary>
        public void Render(ExplorationRoom room)
        {
            if (room == null)
                throw new ArgumentNullException(nameof(room));
            var run = room.Run;
            bool reveal =
                run?.Map != null
                && revealFor != null
                && run.RoomId == revealFor
                && painter != null
                && SameRoute(painter.Map, run.Map);
            revealFor = null;
            if (!reveal)
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
                PlaceChest(room, false);
                return;
            }
            if (painter == null || !SameRoute(painter.Map, run.Map))
                painter = new ExplorationMapPainter(run.Map, Art);
            paintedRun = run;
            paintedSight = new ExplorationMapSight(run);
            if (!reveal)
            {
                var here = RoutePoint(run.RoomId);
                cameraAt = here;
                fogAt = here;
                partyRoute = here;
            }
            projection = ViewFrom(cameraAt);
            if (reveal)
                repaint = true;
            else
                Paint(0f, projection);
            PlaceParty();
            PlaceMarkers(run, room, paintedSight, reveal);
            PlaceChest(room, reveal);
            PlaceFloors(run);
            if (reveal)
                Animate();
        }

        // 同じ種と部屋の数から作った道は、同じ道になる。
        private static bool SameRoute(AdventureRouteMap a, AdventureRouteMap b) =>
            a == b || (a.Seed == b.Seed && a.RoomCount == b.RoomCount);

        private Vector2 RoutePoint(string roomId)
        {
            var point =
                painter.Map.PointOf(roomId) ?? painter.Map.PointOf(AdventureRouteMap.EntranceId);
            return new Vector2(point.T, point.S);
        }

        private ExplorationMapProjection ViewFrom(Vector2 at) => new(painter.Map, at.x, at.y);

        /// <summary>
        /// Looks along the route: the finger's move on the map (design units, y up) moves the
        /// camera ahead or back and across, as if the ground at the party's row followed the
        /// finger. Pulling the map down looks ahead. The camera stays where it is left, from the
        /// party's room to three floors before the deepest room, where the last floors and the
        /// deepest room fill the screen; walking to a room brings it back.
        /// </summary>
        public void Look(Vector2 delta)
        {
            if (walking || painter == null || ShownRun?.Map == null || shownRoom == null)
                return;
            var to = cameraAt + ToRoute(delta);
            var here = RoutePoint(ShownRun.RoomId);
            float last = painter
                .Map.Rooms.Where(room => room.Floor == painter.Map.BossFloor - LookFloors)
                .Select(room => painter.Map.PointOf(room.Id).T)
                .DefaultIfEmpty(here.x)
                .Average();
            to = new Vector2(
                Mathf.Clamp(to.x, here.x, Mathf.Max(here.x, last)),
                Mathf.Clamp01(to.y)
            );
            if (to == cameraAt)
                return;
            // 霧が晴れている途中なら、晴れ切った所から動かす。
            if (animating != null)
            {
                StopAnimation();
                chestAlpha = chestTarget;
            }
            cameraAt = to;
            fogAt = to;
            projection = ViewFrom(cameraAt);
            PlaceParty();
            PlaceMarkers(ShownRun, shownRoom, paintedSight, false);
            PlaceChestOnMap();
            PlaceFloors(ShownRun);
            repaint = true;
            Animate();
        }

        // 指の動き（設計座標）を、カメラの立つ道の上の位置の動きに直す。パーティの行の地面が、
        // 指と同じだけ動くようにする。
        private Vector2 ToRoute(Vector2 delta)
        {
            const float Step = 0.01f;
            var at = projection.ToDots(cameraAt.x, cameraAt.y);
            var ahead = ViewFrom(cameraAt + new Vector2(Step, 0f)).ToDots(cameraAt.x, cameraAt.y);
            var across = ViewFrom(cameraAt + new Vector2(0f, Step)).ToDots(cameraAt.x, cameraAt.y);
            // 設計座標は y が上、ドットは y が下。
            float rows = (ahead.y - at.y) / Step;
            float columns = (across.x - at.x) / Step;
            return new Vector2(
                rows != 0f ? -delta.y / Dot / rows : 0f,
                columns != 0f ? delta.x / Dot / columns : 0f
            );
        }

        // カメラの立つ階：カメラに最も近い部屋の階（今いる階より手前にはしない）。
        private int ViewFloor(AdventureRun run)
        {
            int floor = run.Floor;
            float gap = float.MaxValue;
            foreach (var room in run.Map.Rooms)
            {
                var point = run.Map.PointOf(room.Id);
                float distance = Mathf.Abs(point.T - cameraAt.x);
                if (room.Floor >= run.Floor && distance < gap)
                {
                    gap = distance;
                    floor = room.Floor;
                }
            }
            return floor;
        }

        // 地図の絵を描き直す。seconds は前の絵からの時間で、0なら木をすぐに立て終える。
        private void Paint(float seconds, ExplorationMapProjection fog, float thinning = 0f)
        {
            if (texture == null)
            {
                texture = new Texture2D(
                    ExplorationMapProjection.Width,
                    ExplorationMapProjection.Height,
                    TextureFormat.RGBA32,
                    false
                )
                {
                    name = "ExplorationMap",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                pixels = new Color32[texture.width * texture.height];
            }
            painter.Paint(pixels, paintedRun, projection, paintedSight, fog, seconds, thinning);
            repaint = false;
            texture.SetPixelData(pixels, 0);
            texture.Apply(false);
            if (MapImage != null)
            {
                MapImage.texture = texture;
                MapImage.color = Color.white;
            }
        }

        // 部屋の位置（地図の中央からの設計座標）。1ドットの格子にそろえる。
        private Vector2 PointOf(string roomId, float lift)
        {
            var point = projection.Map.PointOf(roomId);
            if (point == null)
                return Vector2.zero;
            var dots = projection.ToDots(point);
            dots.y -= lift * ExplorationMapProjection.ScaleAt(dots.y);
            return Snap(ExplorationMapProjection.ToDesign(dots));
        }

        private static Vector2 Snap(Vector2 position) =>
            new(Mathf.Round(position.x / Dot) * Dot, Mathf.Round(position.y / Dot) * Dot);

        private void HideMarkers()
        {
            foreach (var marker in shown.Values)
                marker.gameObject.SetActive(false);
            foreach (var marker in leaving)
                marker.gameObject.SetActive(false);
            shown.Clear();
            leaving.Clear();
        }

        // 部屋の印を置く。reveal では、前から見えていた印はそのまま、霧が晴れて見えた印は奥の階ほど
        // 遅れて現れ、見えなくなった印は薄れて消える。
        private void PlaceMarkers(
            AdventureRun run,
            ExplorationRoom room,
            ExplorationMapSight sight,
            bool reveal
        )
        {
            if (!reveal)
                HideMarkers();
            if (MarkerTemplate == null || Markers == null)
                return;
            var before = new Dictionary<string, ExplorationMapMarker>(shown);
            shown.Clear();
            var exits = new HashSet<string>(room.Exits.Select(exit => exit.Id));
            var placed = new List<ExplorationMapMarker>();
            int view = ViewFloor(run);
            foreach (var mapRoom in run.Map.Rooms)
            {
                if (mapRoom.Id == run.RoomId || sight.Passed.Contains(mapRoom.Id))
                    continue;
                bool exit = exits.Contains(mapRoom.Id);
                var level = exit ? ExplorationRoomSight.Detail : sight.Of(mapRoom, view);
                if (level == ExplorationRoomSight.Hidden)
                    continue;
                bool seen = before.Remove(mapRoom.Id, out var marker) && marker.TargetAlpha > 0f;
                if (marker == null || !seen)
                {
                    if (marker != null)
                        Leave(marker);
                    marker = Take();
                    marker.Alpha = reveal ? 0f : 1f;
                    marker.Wait = reveal ? (mapRoom.Floor - run.Floor - 1) * RevealStepSeconds : 0f;
                }
                marker.TargetAlpha = 1f;
                Configure(marker, mapRoom, level, exit, sight.Reachable.Contains(mapRoom.Id));
                ApplyFade(marker);
                shown[mapRoom.Id] = marker;
                placed.Add(marker);
            }
            foreach (var marker in before.Values)
                Leave(marker);
            // 奥の印から描き、手前の印を上に重ねる。
            foreach (var marker in placed.OrderByDescending(entry => entry.Body.anchoredPosition.y))
                marker.transform.SetAsLastSibling();
        }

        // 見えなくなった印は、薄れてから隠す。
        private void Leave(ExplorationMapMarker marker)
        {
            marker.TargetAlpha = 0f;
            marker.Wait = 0f;
            if (!leaving.Contains(marker))
                leaving.Add(marker);
        }

        private ExplorationMapMarker Take()
        {
            var marker = pool.FirstOrDefault(entry =>
                !entry.gameObject.activeSelf && !leaving.Contains(entry)
            );
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
            // 霧から現れる印は、少し小さい所から大きくなる。
            marker.Body.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, marker.Alpha);
        }

        private static float LiftOf(ExplorationRoomSight level) =>
            level == ExplorationRoomSight.Detail ? Lift : Lift * 0.6f;

        private void Keep(ExplorationMapMarker marker)
        {
            marker.Button.onClick.AddListener(() => RoomPressed?.Invoke(marker.RoomId));
            pool.Add(marker);
        }

        private void Configure(
            ExplorationMapMarker marker,
            AdventureRoom room,
            ExplorationRoomSight level,
            bool exit,
            bool reachable
        )
        {
            marker.RoomId = room.Id;
            marker.IsExit = exit;
            marker.Sight = level;
            bool boss = room.Kind == AdventureRoomKind.Boss;
            bool detail = level == ExplorationRoomSight.Detail;
            bool far = level == ExplorationRoomSight.Far;
            marker.Body.anchoredPosition = PointOf(room.Id, LiftOf(level));

            // カメラに近い部屋は24ドットのアイコンを3倍、少し先は2倍、遠くは1倍で出す。
            float icon =
                detail ? 24 * Dot
                : far ? 24
                : 24 * 2;
            float ring =
                detail ? (exit ? 108 : 96)
                : far ? 34
                : 60;
            marker.Ring.rectTransform.sizeDelta = Vector2.one * ring;
            marker.Plate.rectTransform.sizeDelta =
                Vector2.one
                * (
                    ring
                    - (
                        detail ? 12
                        : far ? 4
                        : 8
                    )
                );
            marker.Icon.rectTransform.sizeDelta = Vector2.one * icon;
            marker.Ring.color =
                boss ? RingBoss
                : exit ? RingExit
                : reachable ? RingAhead
                : RingFaint;
            marker.Icon.sprite = KindIcon(room.Kind);
            marker.Icon.enabled = marker.Icon.sprite != null;
            marker.Icon.color = reachable || boss ? Color.white : Faded;

            // 遠くの強い魔物は、小さい札の後ろの赤い光でも分かるようにする。
            bool glow = far && reachable && room.Kind == AdventureRoomKind.Elite;
            marker.Glow.gameObject.SetActive(glow || boss);
            marker.Glow.color = boss ? BossGlow : EliteGlow;
            marker.Glow.rectTransform.sizeDelta = Vector2.one * (boss ? 260 : 120);

            marker.Button.interactable = exit;
            if (marker.Button.targetGraphic != null)
                marker.Button.targetGraphic.raycastTarget = exit;

            bool hint = exit || boss;
            marker.HintBox.gameObject.SetActive(hint);
            if (!hint)
                return;
            marker.Hint.text = AdventureCatalog.Hint(room.Kind);
            marker.Hint.color = exit ? RingExit : Color.white;
            // 次の階の部屋の手掛かりは、横に並ぶ部屋と重ならないよう札の下に、最奥の間は横に置く。
            float below = ring / 2f + 6f;
            marker.HintBox.pivot = boss ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 1f);
            marker.HintBox.anchoredPosition = boss
                ? new Vector2(ring / 2f + 12f, 0f)
                : new Vector2(0f, -below);
        }

        private void PlaceParty()
        {
            partyAt = Snap(
                ExplorationMapProjection.ToDesign(projection.ToDots(partyRoute.x, partyRoute.y))
            );
            for (int i = 0; i < Party.Length && i < Formation.Length; i++)
                Party[i].anchoredPosition = Snap(partyAt + Formation[i]);
        }

        // 宝箱は、宝箱の部屋の右手前に置く。部屋を離れると、部屋に残したまま薄れて消える。
        private void PlaceChest(ExplorationRoom room, bool reveal)
        {
            if (ChestGroup == null)
                return;
            bool chest = room.Chest != ExplorationChest.None && room.Run?.Map != null;
            bool closed = chest && room.Chest == ExplorationChest.Closed;
            ChestGroup.blocksRaycasts = closed;
            ChestGroup.interactable = closed;
            if (ChestButton != null)
                ChestButton.gameObject.SetActive(closed);
            chestTarget = chest ? 1f : 0f;
            if (!chest)
            {
                if (!reveal)
                    chestAlpha = 0f;
                ChestGroup.alpha = chestAlpha;
                return;
            }
            if (reveal && chestRoom != room.Run.RoomId)
                chestAlpha = 0f;
            else if (!reveal)
                chestAlpha = 1f;
            chestRoom = room.Run.RoomId;
            ChestGroup.alpha = chestAlpha;
            PlaceChestOnMap();
            if (ChestSprite != null)
                ChestSprite.texture = room.Chest == ExplorationChest.Open ? ChestOpen : ChestClosed;
        }

        private void PlaceChestOnMap()
        {
            if (Chest != null && chestRoom != null)
                Chest.anchoredPosition = Snap(PointOf(chestRoom, 0f) + new Vector2(126f, -18f));
        }

        // 左端の階の目安：画面に入る今いる階から、カメラの立つ階の4階先まで。
        private void PlaceFloors(AdventureRun run)
        {
            foreach (var label in floorLabels)
                label.gameObject.SetActive(false);
            if (FloorTemplate == null || Floors == null)
                return;
            int index = 0;
            int view = ViewFloor(run);
            for (int floor = run.Floor; floor < run.Map.BossFloor; floor++)
            {
                var rooms = run.Map.Rooms.Where(room => room.Floor == floor).ToArray();
                if (rooms.Length == 0)
                    continue;
                float y = rooms.Average(room => PointOf(room.Id, 0f).y);
                if (y > 330f || floor - view > ExplorationMapSight.DefaultMarkFloors)
                    continue;
                if (index >= floorLabels.Count)
                {
                    var copy = Instantiate(FloorTemplate, Floors);
                    copy.name = "Floor" + index;
                    floorLabels.Add(copy);
                }
                var label = floorLabels[index++];
                label.gameObject.SetActive(true);
                label.text = AdventureCatalog.FloorText(floor);
                label.color = floor == run.Floor ? FloorHere : FloorTemplate.color;
                label.fontSize = floor - view > ExplorationMapSight.DefaultDetailFloors ? 20 : 26;
                var rect = (RectTransform)label.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
            }
        }

        /// <summary>
        /// The party walks the road to the room with the camera following it, then
        /// <paramref name="arrived"/> runs; the fog keeps clearing until the room is shown.
        /// The road chosen is painted as walked at once, and the other roads as closed.
        /// </summary>
        public void WalkTo(string roomId, Action arrived)
        {
            var run = ShownRun;
            var road =
                run?.Map != null && painter != null && isActiveAndEnabled
                    ? run.Map.Path(run.RoomId, roomId, 48)
                    : Array.Empty<(float T, float S)>();
            if (road.Count < 2)
            {
                arrived?.Invoke();
                return;
            }
            StopAnimation();
            move = new Move
            {
                Road = road.Select(point => new Vector2(point.T, point.S)).ToArray(),
                Arrived = arrived,
            };
            move.CameraOffset = cameraAt - move.Road[0];
            move.FogOffset = fogAt - move.Road[0];
            walking = true;
            revealFor = roomId;
            paintedRun = new AdventureRun(
                run.Id,
                run.DestinationId,
                roomId,
                false,
                run.Route.Append(roomId).ToArray(),
                run.Revives,
                run.ReviveCost,
                run.Map,
                run.Rewards,
                run.Floor + 1
            );
            paintedSight = new ExplorationMapSight(paintedRun);
            // 選んだ部屋の印は着く少し前に、ほかの部屋の印はすぐに薄れて消える。
            foreach (var marker in shown.Values)
                if (marker.IsExit)
                {
                    marker.TargetAlpha = 0f;
                    marker.Wait = marker.RoomId == roomId ? ChosenFadeDelaySeconds : 0f;
                }
            chestTarget = 0f;
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
            revealFor = null;
        }

        private static float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // 1コマ進める：4人とカメラと霧を道に沿って動かし、地図を描き直し、印と宝箱を薄れさせる。
        // まだ動いている物があれば true。
        private bool Step(float seconds)
        {
            bool moving = false;
            var fog = projection;
            if (move != null)
            {
                move.Elapsed += seconds;
                float walked = Ease(move.Elapsed / WalkSeconds);
                float followed = Ease((move.Elapsed - CameraDelaySeconds) / CameraSeconds);
                float cleared = Ease((move.Elapsed - FogDelaySeconds) / FogSeconds);
                partyRoute = move.At(walked);
                cameraAt = move.At(followed) + move.CameraOffset * (1f - followed);
                fogAt = move.At(cleared) + move.FogOffset * (1f - cleared);
                projection = ViewFrom(cameraAt);
                fog = fogAt == cameraAt ? projection : ViewFrom(fogAt);
                Paint(seconds, fog, FogThinning * Mathf.Sin(Mathf.PI * cleared));
                PlaceOnMap();
                if (move.Arrived != null && walked >= 1f && followed >= 1f)
                {
                    var arrived = move.Arrived;
                    move.Arrived = null;
                    walking = false;
                    // 着いた部屋を見せる（Render）。別の部屋を見せたら、移動はそこで終わる。
                    arrived();
                    if (move == null)
                        return false;
                }
                if (cleared >= 1f && move.Arrived == null)
                    move = null;
                else
                    moving = true;
            }
            else if (repaint || (painter != null && !painter.Settled))
            {
                Paint(seconds, fog);
                PlaceOnMap();
            }
            moving |= painter != null && !painter.Settled;
            moving |= FadeMarkers(seconds);
            return moving;
        }

        // 動いたカメラから見た、パーティ・印・宝箱・階の位置。
        private void PlaceOnMap()
        {
            PlaceParty();
            foreach (var marker in pool)
                if (marker.gameObject.activeSelf)
                    marker.Body.anchoredPosition = PointOf(marker.RoomId, LiftOf(marker.Sight));
            PlaceChestOnMap();
            if (ShownRun != null)
                PlaceFloors(ShownRun);
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
                else if (marker.TargetAlpha <= 0f && leaving.Remove(marker))
                    marker.gameObject.SetActive(false);
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

        public Sprite KindIcon(AdventureRoomKind kind) =>
            (int)kind < KindIcons.Length ? KindIcons[(int)kind] : null;
    }
}
