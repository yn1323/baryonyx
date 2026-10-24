using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// The highway's world map (doc/features/stage-progression.md#街道とダンジョン): a big picture
    /// seen through the screen and dragged in any direction, with sugoroku squares and straight
    /// lines over it. Tapping a cleared or open square walks the party there by the shortest way
    /// over cleared squares. The progress is read from <see cref="WorldMapSession"/> when the map
    /// opens and each square reached is saved there. A prototype: reaching an open square clears
    /// it at once (no battle yet).
    /// </summary>
    public sealed class WorldMapView : MonoBehaviour
    {
        // 背景の絵の1画素を、設計座標で何pxに出すか。
        public float Scale = 3f;

        public RectTransform Viewport;
        public RectTransform Map;
        public WorldMapDrag Drag;

        // マス。並びは WorldMapCatalog.Build() のマスの並び。
        public RectTransform[] Nodes = Array.Empty<RectTransform>();
        public Image[] NodeImages = Array.Empty<Image>();
        public Button[] NodeButtons = Array.Empty<Button>();

        // 種類ごとの台（WorldNodeKind の順）と、関所の先で種類を伏せる台。
        public Sprite[] KindSprites = Array.Empty<Sprite>();
        public Sprite HiddenSprite;

        // マスを結ぶ線。並びは WorldMapGraph.Lines。
        public Image[] LineFills = Array.Empty<Image>();
        public Image[] LineEdges = Array.Empty<Image>();

        public RectTransform Party;
        public TMP_Text Place;
        public Button Back;
        public Button Dungeons;

        // 保存に失敗したときの知らせ。しばらく出して消す。
        public TMP_Text Notice;
        public CanvasGroup NoticeGroup;

        public const string LoadingText = "読み込み中…";
        private const float NoticeSeconds = 2.5f;

        public static readonly Color ClearedLine = new(1f, 0.96f, 0.86f, 1f);
        public static readonly Color OpenLine = new(1f, 0.82f, 0.3f, 1f);
        public static readonly Color SeenLine = new(0.78f, 0.76f, 0.72f, 0.75f);
        public static readonly Color HiddenLine = new(0.45f, 0.45f, 0.5f, 0.45f);
        public static readonly Color EdgeColor = new(0.32f, 0.2f, 0.12f, 1f);
        private static readonly Color SeenTint = new(0.62f, 0.62f, 0.68f, 1f);

        private const float WalkSpeed = 1400f;
        private const float FollowSharpness = 8f;
        private const float GlideDecay = 5f;

        public event Action BackPressed;
        public event Action DungeonsPressed;

        private WorldMapGraph graph;
        private readonly HashSet<string> cleared = new();
        private WorldNodeState[] states = Array.Empty<WorldNodeState>();
        private int partyAt;
        private Vector2 partyDesign;
        private Vector2 scroll;
        private Vector2 glide;
        private bool holding;
        private readonly List<Vector2> walk = new();
        private int walkTarget = -1;
        private float pulse;
        private float noticeLeft;

        public WorldMapGraph Graph => graph;
        public IReadOnlyList<WorldNodeState> States => states;
        public int PartyAt => partyAt;
        public bool Walking => walk.Count > 0;
        public Vector2 Scroll => scroll;

        // 保存先から進み具合を読み終えたか。読む前はマスを押しても歩かない。
        public bool Loaded { get; private set; }

        // 保存先から読む処理と、最後に出した保存。テストで完了を待つために公開する。
        public System.Threading.Tasks.Task LoadTask { get; private set; } =
            System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task SaveTask { get; private set; } =
            System.Threading.Tasks.Task.CompletedTask;

        // Play中の再コンパイルでは、保存しない状態が消えて OnEnable から呼び直される。
        // そのときは手元の進み具合（なければ最初の街だけ）から作り直し、読み直す。
        private void OnEnable()
        {
            if (graph == null)
            {
                graph = WorldMapCatalog.Build();
                walk.Clear();
                Apply(WorldMapSession.Known ?? WorldMapProgress.Start);
                Loaded = false;
                if (Place != null)
                    Place.text = LoadingText;
                if (isActiveAndEnabled && didStart)
                    LoadTask = LoadAsync();
            }
            for (int i = 0; i < NodeButtons.Length; i++)
            {
                int index = i;
                NodeButtons[i].onClick.AddListener(() => Tap(index));
            }
            if (Drag != null)
            {
                Drag.Dragged += Look;
                Drag.Holding += Hold;
            }
            if (Back != null)
                Back.onClick.AddListener(PressBack);
            if (Dungeons != null)
                Dungeons.onClick.AddListener(PressDungeons);
        }

        private void OnDisable()
        {
            foreach (var button in NodeButtons)
                button.onClick.RemoveAllListeners();
            if (Drag != null)
            {
                Drag.Dragged -= Look;
                Drag.Holding -= Hold;
            }
            if (Back != null)
                Back.onClick.RemoveListener(PressBack);
            if (Dungeons != null)
                Dungeons.onClick.RemoveListener(PressDungeons);
        }

        // 画面の大きさが決まってから、パーティを画面の中央に置き直し、保存先から読み直す。
        private void Start()
        {
            Home();
            if (Place != null && !Loaded)
                Place.text = LoadingText;
            LoadTask = LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                var progress = await WorldMapSession.LoadAsync(destroyCancellationToken);
                if (this == null || progress == null)
                    return;
                if (!Walking)
                    Apply(progress);
                Loaded = true;
                Refresh();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogWarning("街道の進み具合を読み込めませんでした。" + exception.Message);
                if (this == null)
                    return;
                // 読めなくても、手元の進み具合で遊べるようにする。保存は次の移動で試す。
                Loaded = true;
                Refresh();
            }
        }

        // 進み具合をそのまま映し、パーティを今いるマスへ置く。
        private void Apply(WorldMapProgress progress)
        {
            cleared.Clear();
            foreach (var id in progress.Cleared)
                cleared.Add(id);
            int at = graph.IndexOf(progress.At);
            partyAt = at >= 0 ? at : graph.IndexOf(WorldMapCatalog.Start);
            Home();
        }

        private void Home()
        {
            Refresh();
            PlaceParty(DesignOf(partyAt));
            scroll = Clamp(DesignOf(partyAt) - ViewSize * 0.5f);
            ApplyScroll();
        }

        private void PressBack() => BackPressed?.Invoke();

        private void PressDungeons() => DungeonsPressed?.Invoke();

        private void Hold(bool held)
        {
            holding = held;
            if (held)
                glide = Vector2.zero;
        }

        /// <summary>Taps a square: walks there when it can be reached.</summary>
        public bool Tap(int index)
        {
            if (!Loaded || Walking || index == partyAt)
                return false;
            var path = graph.PathTo(partyAt, index, states);
            if (path.Count < 2)
                return false;
            walk.Clear();
            for (int i = 1; i < path.Count; i++)
                walk.Add(DesignOf(path[i]));
            walkTarget = index;
            glide = Vector2.zero;
            return true;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            pulse += dt;
            if (Walking)
                Step(dt);
            else if (!holding && glide.sqrMagnitude > 1f)
            {
                MoveBy(glide * dt);
                glide *= Mathf.Exp(-GlideDecay * dt);
            }
            PulseOpen();
            FadeNotice(dt);
        }

        // 選んだマスへ、通ったマスを1つずつ直線で歩く。カメラはパーティを追う。
        private void Step(float dt)
        {
            var next = walk[0];
            var moved = Vector2.MoveTowards(partyDesign, next, WalkSpeed * dt);
            PlaceParty(moved);
            if ((moved - next).sqrMagnitude < 0.01f)
                walk.RemoveAt(0);
            var want = Clamp(moved - ViewSize * 0.5f);
            scroll = Vector2.Lerp(scroll, want, 1f - Mathf.Exp(-FollowSharpness * dt));
            ApplyScroll();
            if (Walking)
                return;
            partyAt = walkTarget;
            walkTarget = -1;
            // 試作：挑めるマスに着いたら、そのままクリアにする（戦闘はまだない）。
            bool clear = states[partyAt] == WorldNodeState.Open;
            string id = graph.Nodes[partyAt].Id;
            if (clear)
                cleared.Add(id);
            Refresh();
            SaveTask = SaveAsync(id, clear);
        }

        // 着いたマスを保存する。失敗したら、保存先に残っている進み具合へ戻して知らせる。
        private async System.Threading.Tasks.Task SaveAsync(string id, bool clear)
        {
            bool saved = await WorldMapSession.MoveAsync(id, clear);
            if (saved || this == null)
                return;
            if (!Walking && WorldMapSession.Known != null)
                Apply(WorldMapSession.Known);
            ShowNotice(WorldMapSession.SaveFailedMessage);
        }

        public void ShowNotice(string message)
        {
            if (Notice == null || NoticeGroup == null)
                return;
            Notice.text = message;
            NoticeGroup.alpha = 1f;
            noticeLeft = NoticeSeconds;
        }

        private void FadeNotice(float dt)
        {
            if (NoticeGroup == null || noticeLeft <= 0f)
                return;
            noticeLeft -= dt;
            NoticeGroup.alpha = Mathf.Clamp01(noticeLeft / 0.4f);
        }

        private void Look(Vector2 delta)
        {
            if (Walking)
                return;
            var move = new Vector2(-delta.x, delta.y);
            MoveBy(move);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);
            glide = Vector2.Lerp(glide, move / dt, 0.5f);
        }

        private void MoveBy(Vector2 move)
        {
            scroll = Clamp(scroll + move);
            ApplyScroll();
        }

        private Vector2 ViewSize =>
            Viewport != null && Viewport.rect.width > 0f
                ? Viewport.rect.size
                : new Vector2(1920f, 1080f);

        private Vector2 MapSize => WorldMapCatalog.PictureSize * Scale;

        // scroll は地図の左上から見た、画面の左上の位置（y は下向き）。
        private Vector2 Clamp(Vector2 value)
        {
            var max = Vector2.Max(Vector2.zero, MapSize - ViewSize);
            return new Vector2(Mathf.Clamp(value.x, 0f, max.x), Mathf.Clamp(value.y, 0f, max.y));
        }

        private void ApplyScroll()
        {
            if (Map != null)
                Map.anchoredPosition = new Vector2(-Mathf.Round(scroll.x), Mathf.Round(scroll.y));
        }

        // マスの設計座標（地図の左上から、y は下向き）。
        private Vector2 DesignOf(int index) => graph.Nodes[index].Point * Scale;

        // 地図の部品は左上を基準に置くため、y を上向きに直して置く。
        private void PlaceParty(Vector2 design)
        {
            partyDesign = design;
            if (Party != null)
                Party.anchoredPosition = new Vector2(design.x, -design.y);
        }

        /// <summary>Paints the squares and lines from the cleared squares.</summary>
        public void Refresh()
        {
            states = graph.StatesOf(cleared);
            for (int i = 0; i < Nodes.Length && i < graph.Nodes.Count; i++)
            {
                var state = states[i];
                var image = NodeImages[i];
                image.sprite =
                    state == WorldNodeState.Hidden
                        ? HiddenSprite
                        : KindSprites[(int)graph.Nodes[i].Kind];
                image.color = state == WorldNodeState.Seen ? SeenTint : Color.white;
                Nodes[i].localScale = Vector3.one;
            }
            for (int i = 0; i < LineFills.Length && i < graph.Lines.Count; i++)
            {
                var (from, to) = graph.Lines[i];
                var color = LineColor(states[from], states[to]);
                LineFills[i].color = color;
                var edge = EdgeColor;
                edge.a = color.a;
                LineEdges[i].color = edge;
            }
            if (Place != null)
                Place.text = graph.Nodes[partyAt].Name;
        }

        private static Color LineColor(WorldNodeState a, WorldNodeState b)
        {
            if (a == WorldNodeState.Hidden || b == WorldNodeState.Hidden)
                return HiddenLine;
            if (a == WorldNodeState.Cleared && b == WorldNodeState.Cleared)
                return ClearedLine;
            if (
                (a == WorldNodeState.Cleared && b == WorldNodeState.Open)
                || (a == WorldNodeState.Open && b == WorldNodeState.Cleared)
            )
                return OpenLine;
            return SeenLine;
        }

        // 次に挑めるマスを、ゆっくり大きく小さくして知らせる。
        private void PulseOpen()
        {
            float scale = 1f + 0.08f * (0.5f + 0.5f * Mathf.Sin(pulse * 4f));
            for (int i = 0; i < Nodes.Length && i < states.Length; i++)
                if (states[i] == WorldNodeState.Open)
                    Nodes[i].localScale = Vector3.one * scale;
        }
    }
}
