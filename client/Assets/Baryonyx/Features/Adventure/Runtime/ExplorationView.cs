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
    /// tapped sends the party walking along its road. AdventureAssets builds it; the adventure's
    /// flow (<see cref="ExplorationFlow"/>) drives it.
    /// </summary>
    public sealed class ExplorationView : MonoBehaviour
    {
        // 道を歩く時間と、次の部屋を見せるときに地図を薄くする時間。
        public const float WalkSeconds = 1.2f;
        public const float FadeSeconds = 0.25f;

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
        public CanvasGroup MapGroup;
        public RawImage MapImage;
        public ExplorationMapArt Art;
        public RectTransform Markers;
        public ExplorationMapMarker MarkerTemplate;

        // 地図の上のパーティ。部屋の中心からの隊列の位置に置いておく。
        public RectTransform[] Party = Array.Empty<RectTransform>();
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
        private readonly List<TMP_Text> floorLabels = new();
        private Vector2[] formation = Array.Empty<Vector2>();
        private Texture2D texture;
        private Color32[] pixels;
        private ExplorationMapProjection projection;
        private Vector2 partyAt;
        private Coroutine walking;
        private Coroutine fading;
        private bool wired;

        public event Action<string> RoomPressed;
        public event Action ChestPressed;
        public event Action MenuPressed;

        // 端末の戻るキー。開いているダイアログが先に受け取り、なければメニューを開く。
        public event Action BackPressed;

        public AdventureRun ShownRun { get; private set; }
        public bool Walking => walking != null;
        public bool Fading => fading != null;
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
            formation = Party.Select(member => member.anchoredPosition).ToArray();
            if (MarkerTemplate != null)
                MarkerTemplate.gameObject.SetActive(false);
            if (FloorTemplate != null)
                FloorTemplate.gameObject.SetActive(false);
            Wire();
        }

        private void Start()
        {
            if (SampleSeed != 0 && ShownRun == null)
                RenderSample(SampleSeed);
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
        /// the next floor's rooms to choose once the room is done, and its chest.
        /// </summary>
        public void Render(ExplorationRoom room)
        {
            if (room == null)
                throw new ArgumentNullException(nameof(room));
            StopWalking();
            if (Location != null)
                Location.text = room.Location;
            if (Prompt != null)
                Prompt.text = room.Prompt;
            var run = room.Run;
            ShownRun = run;
            HideMarkers();
            if (run?.Map == null)
            {
                PlaceChest(room);
                return;
            }
            projection = new ExplorationMapProjection(run.Map, run.RoomId);
            var sight = new ExplorationMapSight(run);
            Paint(run, sight);
            partyAt = PointOf(run.RoomId, 0f);
            PlaceParty(partyAt);
            PlaceMarkers(run, room, sight);
            PlaceChest(room);
            PlaceFloors(run);
        }

        private void Paint(AdventureRun run, ExplorationMapSight sight)
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
            ExplorationMapPainter.Paint(pixels, run, projection, sight, Art);
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
            shown.Clear();
        }

        private void PlaceMarkers(AdventureRun run, ExplorationRoom room, ExplorationMapSight sight)
        {
            if (MarkerTemplate == null || Markers == null)
                return;
            var exits = new HashSet<string>(room.Exits.Select(exit => exit.Id));
            var placed = new List<ExplorationMapMarker>();
            foreach (var mapRoom in run.Map.Rooms)
            {
                if (mapRoom.Id == run.RoomId || sight.Passed.Contains(mapRoom.Id))
                    continue;
                bool exit = exits.Contains(mapRoom.Id);
                var level = exit ? ExplorationRoomSight.Detail : sight.Of(mapRoom);
                if (level == ExplorationRoomSight.Hidden)
                    continue;
                var marker = Take(placed.Count);
                Configure(marker, mapRoom, level, exit, sight.Reachable.Contains(mapRoom.Id));
                shown[mapRoom.Id] = marker;
                placed.Add(marker);
            }
            // 奥の印から描き、手前の印を上に重ねる。
            foreach (var marker in placed.OrderByDescending(entry => entry.Body.anchoredPosition.y))
                marker.transform.SetAsLastSibling();
        }

        private ExplorationMapMarker Take(int index)
        {
            if (index >= pool.Count)
            {
                var copy = Instantiate(MarkerTemplate, Markers);
                copy.name = "Room" + index;
                var button = copy.Button;
                button.onClick.AddListener(() => RoomPressed?.Invoke(copy.RoomId));
                pool.Add(copy);
            }
            var marker = pool[index];
            marker.gameObject.SetActive(true);
            return marker;
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
            bool glow = level == ExplorationRoomSight.Glow;
            marker.Body.anchoredPosition = PointOf(room.Id, detail ? Lift : Lift * 0.6f);

            // 近い部屋は24ドットのアイコンを3倍、少し先は2倍、霧の奥の光は1倍で出す。
            float icon =
                detail ? 24 * Dot
                : glow ? 24
                : 24 * 2;
            float ring =
                detail ? (exit ? 108 : 96)
                : glow ? 36
                : 60;
            marker.Ring.rectTransform.sizeDelta = Vector2.one * ring;
            marker.Plate.rectTransform.sizeDelta = Vector2.one * (ring - (detail ? 12 : 8));
            marker.Icon.rectTransform.sizeDelta = Vector2.one * icon;
            marker.Ring.color =
                boss ? RingBoss
                : exit ? RingExit
                : reachable ? RingAhead
                : RingFaint;
            marker.Icon.sprite = KindIcon(room.Kind);
            marker.Icon.enabled = marker.Icon.sprite != null;
            marker.Icon.color = reachable || boss ? Color.white : Faded;
            marker.Ring.enabled = !glow;
            marker.Plate.enabled = !glow;

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

        private void PlaceParty(Vector2 at)
        {
            for (int i = 0; i < Party.Length && i < formation.Length; i++)
                Party[i].anchoredPosition = Snap(at + formation[i]);
        }

        private void PlaceChest(ExplorationRoom room)
        {
            if (ChestGroup == null)
                return;
            bool chest = room.Chest != ExplorationChest.None && room.Run?.Map != null;
            ChestGroup.alpha = chest ? 1f : 0f;
            ChestGroup.blocksRaycasts = chest && room.Chest == ExplorationChest.Closed;
            ChestGroup.interactable = chest && room.Chest == ExplorationChest.Closed;
            if (ChestButton != null)
                ChestButton.gameObject.SetActive(chest && room.Chest == ExplorationChest.Closed);
            if (!chest)
                return;
            // 宝箱は、パーティの右手前に置く。
            Chest.anchoredPosition = Snap(partyAt + new Vector2(126f, -18f));
            if (ChestSprite != null)
                ChestSprite.texture = room.Chest == ExplorationChest.Open ? ChestOpen : ChestClosed;
        }

        // 左端の階の目安：今いる階から、詳しく見える階まで。
        private void PlaceFloors(AdventureRun run)
        {
            foreach (var label in floorLabels)
                label.gameObject.SetActive(false);
            if (FloorTemplate == null || Floors == null)
                return;
            int index = 0;
            for (int floor = run.Floor; floor < run.Map.BossFloor; floor++)
            {
                var rooms = run.Map.Rooms.Where(room => room.Floor == floor).ToArray();
                if (rooms.Length == 0)
                    continue;
                float y = rooms.Average(room => PointOf(room.Id, 0f).y);
                if (y > 330f || floor - run.Floor > ExplorationMapSight.DefaultMarkFloors)
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
                label.fontSize =
                    floor - run.Floor > ExplorationMapSight.DefaultDetailFloors ? 20 : 26;
                var rect = (RectTransform)label.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
            }
        }

        /// <summary>The party walks the road to the room, then <paramref name="arrived"/> runs.</summary>
        public void WalkTo(string roomId, Action arrived)
        {
            StopWalking();
            if (ShownRun?.Map == null || projection == null)
            {
                arrived?.Invoke();
                return;
            }
            var road = ShownRun
                .Map.Path(ShownRun.RoomId, roomId, 48)
                .Select(point =>
                    ExplorationMapProjection.ToDesign(projection.ToDots(point.T, point.S))
                )
                .ToArray();
            walking = StartCoroutine(Walk(road, arrived));
        }

        private IEnumerator Walk(Vector2[] road, Action arrived)
        {
            if (road.Length > 1)
            {
                var lengths = new float[road.Length];
                for (int i = 1; i < road.Length; i++)
                    lengths[i] = lengths[i - 1] + Vector2.Distance(road[i - 1], road[i]);
                float total = Mathf.Max(1f, lengths[^1]);
                for (float t = 0f; t < WalkSeconds; t += Time.deltaTime)
                {
                    float along = Mathf.SmoothStep(0f, 1f, t / WalkSeconds) * total;
                    int k = 1;
                    while (k < road.Length - 1 && lengths[k] < along)
                        k++;
                    float part = Mathf.InverseLerp(lengths[k - 1], lengths[k], along);
                    partyAt = Vector2.Lerp(road[k - 1], road[k], part);
                    PlaceParty(partyAt);
                    yield return null;
                }
                partyAt = road[^1];
                PlaceParty(partyAt);
            }
            walking = null;
            arrived?.Invoke();
        }

        private void StopWalking()
        {
            if (walking == null)
                return;
            StopCoroutine(walking);
            walking = null;
        }

        /// <summary>Fades the map out, runs <paramref name="change"/> and fades it back in.</summary>
        public void Crossfade(Action change)
        {
            if (!isActiveAndEnabled || MapGroup == null)
            {
                change?.Invoke();
                return;
            }
            if (fading != null)
                StopCoroutine(fading);
            fading = StartCoroutine(Fade(change));
        }

        private IEnumerator Fade(Action change)
        {
            for (float t = 0f; t < FadeSeconds; t += Time.deltaTime)
            {
                MapGroup.alpha = 1f - t / FadeSeconds;
                yield return null;
            }
            MapGroup.alpha = 0f;
            change?.Invoke();
            yield return null;
            for (float t = 0f; t < FadeSeconds; t += Time.deltaTime)
            {
                MapGroup.alpha = t / FadeSeconds;
                yield return null;
            }
            MapGroup.alpha = 1f;
            fading = null;
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
