using System;
using System.Collections.Generic;
using Baryonyx.Adventure;
using Baryonyx.Inventory;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// The highway's world map (doc/features/stage-progression.md#街道とダンジョン): a big picture
    /// seen through the screen, dragged in any direction and pinched to zoom, with sugoroku
    /// squares and straight lines over it. Tapping a cleared or open square walks the party there
    /// by the shortest way over cleared squares. The progress is read from <see cref="WorldMapSession"/> when the map
    /// opens and each square reached is saved there. Reaching an open square with a battle asks
    /// for the battle (<see cref="BattleRequested"/>), which clears it when won; any other open
    /// square is cleared on reaching it, and a treasure square shows what its chest held once
    /// the server has given it.
    /// </summary>
    public sealed class WorldMapView : MonoBehaviour
    {
        // 背景の絵の1画素を、設計座標で何pxに出すか。
        public float Scale = 3f;

        // 入れ子の地図の試作の1枚（WorldAtlasLayout のJSON）。あるときは、この地図のマスを使い、
        // 進み具合は読みも保存もせず、WorldAtlasNavigator がこの画面の中だけで持つ。
        public TextAsset Layout;

        // 入れ子の地図で、地図を開いたときの拡大率。
        public float StartZoom = 1f;

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

        // 入れ子の地図で、マスに置いた出来事（WorldEventKind の値。何も起きないマスは -1）と、
        // 出来事ごとの台（WorldEventKind の順）。クリアしたマスは何もない台に戻す。
        public int[] NodeEvents = Array.Empty<int>();
        public Sprite[] EventSprites = Array.Empty<Sprite>();

        // マスを結ぶ線。並びは WorldMapGraph.Lines。
        public Image[] LineFills = Array.Empty<Image>();
        public Image[] LineEdges = Array.Empty<Image>();

        public RectTransform Party;

        // パーティの駒の跳ねる動き。マスからマスへ1回ずつ跳ぶ。
        public MapPawnHop PawnHop;
        public TMP_Text Place;
        public Button Back;
        public Button Dungeons;

        // 保存に失敗したときの知らせ。しばらく出して消す。
        public TMP_Text Notice;
        public CanvasGroup NoticeGroup;

        // 札がないときに宝箱の中身を見せるダイアログと、中身の絵。
        public GameDialog Dialog;
        public ItemArt ItemArt;

        [Tooltip("宝箱の中身をまとめて見せる札。冒険の宝箱と同じ部品。")]
        public RewardReveal Reveal;

        // 入れ子の地図で、マスの出来事をマスから開いて見せる窓。
        public WorldMapEventWindow EventWindow;

        public const string LoadingText = "読み込み中…";
        private const float NoticeSeconds = 2.5f;

        // 宝箱の札が開いてから閉じるまで（中身1つのとき）。
        private const float ChestReadSeconds = RewardReveal.OpenTime + ExplorationFlow.ItemReadSeconds;

        public static readonly Color ClearedLine = new(1f, 0.96f, 0.86f, 1f);
        public static readonly Color OpenLine = new(1f, 0.82f, 0.3f, 1f);
        public static readonly Color SeenLine = new(0.78f, 0.76f, 0.72f, 0.75f);
        public static readonly Color HiddenLine = new(0.45f, 0.45f, 0.5f, 0.45f);
        public static readonly Color EdgeColor = new(0.32f, 0.2f, 0.12f, 1f);

        // 確かめている道の線。
        public static readonly Color RouteLine = new(1f, 0.93f, 0.62f, 1f);
        private static readonly Color SeenTint = new(0.62f, 0.62f, 0.68f, 1f);

        // 途中のマスに着いてから、次のマスへ跳ぶまでの間（秒）。
        private const float HopPauseSeconds = 0.1f;
        private const float FollowSharpness = 8f;
        private const float GlideDecay = 5f;

        // 近づける上限。離すほうは、地図が画面を埋めるところまで。
        public const float MaxZoom = 2f;

        public event Action BackPressed;
        public event Action DungeonsPressed;

        // 戦闘のあるマスに着いた。引数はマスのID。
        public event Action<string> BattleRequested;

        // 入れ子の地図の試作で、マスに着いた（今いるマスを押したときも）。
        // 引数はマスのIDと、そのマスを今クリアしたか。
        public event Action<string, bool> Arrived;

        private WorldMapGraph graph;
        private readonly HashSet<string> cleared = new();
        private WorldNodeState[] states = Array.Empty<WorldNodeState>();
        private int partyAt;
        private Vector2 partyDesign;
        private Vector2 scroll;
        private float zoom = 1f;
        private Vector2 glide;
        private Vector2 fittedView;
        private bool holding;
        private readonly List<Vector2> walk = new();

        // 歩く先のマス（walk と同じ並び）。
        private readonly List<int> walkSquares = new();
        private int walkTarget = -1;

        // 入れ子の地図で、マスごとの初めて入る値段（配置の並び）。
        private int[] prices = Array.Empty<int>();

        // 入れ子の地図で、パーティが立ったマスの並び。支払いを断られたとき、まだ開けているマスまで戻る。
        private readonly List<int> trail = new();

        // 途中のマスの出来事の窓を開いている間は、続きを歩かない。
        private bool held;

        // 確かめの窓を開いている間、光らせる道（マスの並び）。
        private readonly HashSet<int> route = new();
        private readonly HashSet<(int, int)> routeLines = new();
        private bool hopping;
        private float hopElapsed;
        private float hopPause;
        private Vector2 hopFrom;
        private float pulse;
        private float noticeLeft;

        /// <summary>
        /// Pays for the square with the given ID as the party hops onto it (the nested world
        /// map), at once without waiting for the server (<see cref="Unpaid"/> takes it back when
        /// the server does not take it). Without it, or while
        /// <see cref="WorldAtlasSession.Charging"/> is off, every square is free.
        /// </summary>
        public Func<string, WorldAtlasPayment> Pay { get; set; }

        /// <summary>Whether a square with the given ID was paid for earlier (the nested world map).</summary>
        public Func<string, bool> IsPaid { get; set; }

        // 払うマスを含む道を押した。引数は道（今いるマスから行き先まで）と、払うルーンの合計。
        // 受け取った側が確かめてから Go を呼ぶ。受け取る先がなければ、そのまま歩く。
        public event Action<IReadOnlyList<int>, int> MoveAsked;

        // 道の途中で、出来事を置いたマスに初めて着いた。窓を閉じたら Resume で続きを歩く。
        public event Action<string> EventReached;

        // 開けたマスと残高をまだ読めていないのに、払うマスを押した。読み直してもらう。
        public event Action LoadNeeded;

        // マスの色を塗り直した。
        public event Action Refreshed;

        public WorldMapGraph Graph => graph;
        public IReadOnlyList<WorldNodeState> States => states;
        public int PartyAt => partyAt;
        public bool Walking => walk.Count > 0;

        /// <summary>The squares still to walk to, in order.</summary>
        public IReadOnlyList<int> WalkSquares => walkSquares;

        /// <summary>Walking, and not held by an event on the way: the walk can be stopped.</summary>
        public bool CanStop => Walking && !held;

        /// <summary>Whether moving onto a square not yet entered costs runes now.</summary>
        public bool Charging => InAtlas && Pay != null && WorldAtlasSession.Charging;

        /// <summary>This map's level and the base price of a square (the nested world map).</summary>
        public int Level => InAtlas ? AtlasLayout.level : 0;
        public int BaseCost => InAtlas ? AtlasLayout.@base : 0;
        public Vector2 Scroll => scroll;

        /// <summary>How far <see cref="Scroll"/> may go before the screen shows past the map.</summary>
        public Vector2 MaxScroll => MapSize - SeenSize;
        public float Zoom => zoom;

        // 戦闘の画面へ移るところ。マスを押しても歩かない。
        public bool Fighting { get; private set; }

        // 宝箱を開けて中身を見せ終えるまで。マスを押しても歩かない。
        public bool Opening { get; private set; }

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
                graph = BuildGraph();
                walk.Clear();
                walkSquares.Clear();
                Apply(
                    InAtlas
                        ? WorldMapProgress.Start
                        : WorldMapSession.Known ?? WorldMapProgress.Start
                );
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
                Drag.Pinched += Pinch;
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
                Drag.Pinched -= Pinch;
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

        private bool InAtlas => Layout != null;

        private WorldAtlasLayout atlasLayout;

        private WorldAtlasLayout AtlasLayout =>
            atlasLayout ??= InAtlas ? WorldAtlasLayout.Parse(Layout.text) : null;

        private WorldMapGraph BuildGraph()
        {
            if (!InAtlas)
                return WorldMapCatalog.Build();
            var nodes = AtlasLayout.nodes;
            prices = new int[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
                prices[i] = nodes[i].price;
            WorldAtlasSession.Register(AtlasLayout);
            return AtlasLayout.Build();
        }

        /// <summary>
        /// The runes that entering the square <paramref name="index"/> costs now: 0 when it was
        /// entered or paid for before, or while charging is off.
        /// </summary>
        public int PriceOf(int index)
        {
            if (!Charging || index < 0 || index >= prices.Length)
                return 0;
            string id = graph.Nodes[index].Id;
            return cleared.Contains(id) || (IsPaid?.Invoke(id) ?? false) ? 0 : prices[index];
        }

        /// <summary>Marks squares paid for elsewhere as entered (the nested world map's saved squares).</summary>
        public void Merge(IEnumerable<string> squares)
        {
            foreach (var id in squares)
                if (graph.IndexOf(id) >= 0)
                    cleared.Add(id);
            if (!Walking)
                Refresh();
        }

        /// <summary>
        /// Zooms to <paramref name="value"/> with the party as near the middle of the screen as
        /// the map's edges allow, past the usual zoom limits (the nested map prototype's switch
        /// between maps). Where the map is narrower than the screen it stays in the middle.
        /// </summary>
        public void FrameAt(float value)
        {
            zoom = Mathf.Max(0.05f, value);
            glide = Vector2.zero;
            scroll = Clamp(partyDesign - SeenSize * 0.5f);
            ApplyScroll();
        }

        /// <summary>The squares cleared on this map.</summary>
        public IReadOnlyCollection<string> Cleared => cleared;

        /// <summary>
        /// Puts the party on <paramref name="at"/> with <paramref name="clearedSquares"/> cleared
        /// (the nested map prototype, which keeps the progress itself).
        /// </summary>
        public void StartAt(string at, IEnumerable<string> clearedSquares)
        {
            graph ??= BuildGraph();
            walk.Clear();
            walkSquares.Clear();
            zoom = ClampZoom(StartZoom);
            Apply(new WorldMapProgress(at, clearedSquares));
            Loaded = true;
            Refresh();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            if (InAtlas)
            {
                Loaded = true;
                Refresh();
                return;
            }
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
            if (at < 0)
                at = graph.IndexOf(WorldMapCatalog.Start);
            // 入れ子の地図の試作には最初の街がないため、地図の最初のマスに置く。
            partyAt = Mathf.Max(0, at);
            trail.Clear();
            trail.Add(partyAt);
            Home();
        }

        private void Home()
        {
            Refresh();
            PlaceParty(DesignOf(partyAt));
            scroll = Clamp(DesignOf(partyAt) - SeenSize * 0.5f);
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
            if (!Loaded || Walking || Fighting || Opening)
                return false;
            if (EventWindow != null && EventWindow.IsShown)
                return false;
            if (index == partyAt)
            {
                // 入れ子の地図では、今いる地域や入口をもう一度押すと、その先へ入る。
                if (InAtlas)
                    Arrived?.Invoke(graph.Nodes[index].Id, false);
                return false;
            }
            if (InAtlas)
                return TapAtlas(index);
            var path = graph.PathTo(partyAt, index, states);
            if (path.Count < 2)
                return false;
            Go(path);
            return true;
        }

        // 入れ子の地図では、開けていない先のマスまで、払うルーンがいちばん少ない道で一度に進める。
        // 払うマスがあれば、MoveAsked で確かめてもらってから歩く。
        private bool TapAtlas(int index)
        {
            if (Charging && !WorldAtlasSession.Loaded)
            {
                LoadNeeded?.Invoke();
                return false;
            }
            var path = graph.CheapestPathTo(partyAt, index, states, PriceOf);
            if (path.Count < 2)
                return false;
            int total = 0;
            for (int i = 1; i < path.Count; i++)
                total += PriceOf(path[i]);
            if (total > 0 && MoveAsked != null)
            {
                MoveAsked(path, total);
                return true;
            }
            Go(path);
            return true;
        }

        /// <summary>
        /// Walks <paramref name="path"/> (from the party's square). On the nested world map, each
        /// square not yet entered is paid for just before the party hops onto it.
        /// </summary>
        public void Go(IReadOnlyList<int> path)
        {
            if (path == null || path.Count < 2 || path[0] != partyAt || Walking)
                return;
            walk.Clear();
            walkSquares.Clear();
            for (int i = 1; i < path.Count; i++)
            {
                walk.Add(DesignOf(path[i]));
                walkSquares.Add(path[i]);
            }
            walkTarget = path[^1];
            glide = Vector2.zero;
            hopping = false;
            hopPause = 0f;
            held = false;
        }

        /// <summary>
        /// Stops the walk without going on to the next square: the party stays where it stands,
        /// or lands on the square it is in the air to. Squares further on are not paid for.
        /// </summary>
        public void Stop()
        {
            if (!CanStop)
                return;
            if (hopping)
            {
                walk.RemoveRange(1, walk.Count - 1);
                walkSquares.RemoveRange(1, walkSquares.Count - 1);
                walkTarget = walkSquares[0];
                return;
            }
            Halt();
        }

        /// <summary>Walks on after the event on the way has been closed.</summary>
        public void Resume() => held = false;

        private void Halt()
        {
            walk.Clear();
            walkSquares.Clear();
            walkTarget = -1;
            hopping = false;
            held = false;
            Refresh();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            pulse += dt;
            // 開いた直後は画面の大きさが決まっていないことがある。決まったら、地図の外が写らない
            // 位置へ置き直す。
            if (Loaded && ViewSize != fittedView)
            {
                fittedView = ViewSize;
                scroll = Clamp(scroll);
                ApplyScroll();
            }
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

        // 選んだマスへ、通るマスに1つずつ跳んで移る。1回の跳躍は一定の時間で、空中を直線で進む。
        // 途中のマスでは少し間を置いてから次へ跳ぶ。カメラはパーティを追う。
        private void Step(float dt)
        {
            if (!hopping)
            {
                hopPause -= dt;
                if (hopPause <= 0f && ReadyToHop())
                    TakeOff();
            }
            if (hopping)
            {
                hopElapsed += dt;
                float x = Mathf.Clamp01(hopElapsed / MapPawnHop.AirSeconds);
                PlaceParty(Vector2.Lerp(hopFrom, walk[0], x));
                if (x >= 1f)
                {
                    walk.RemoveAt(0);
                    int landed = walkSquares[0];
                    walkSquares.RemoveAt(0);
                    hopping = false;
                    hopPause = HopPauseSeconds;
                    if (InAtlas)
                        Landed(landed);
                }
            }
            var want = Clamp(partyDesign - SeenSize * 0.5f);
            scroll = Vector2.Lerp(scroll, want, 1f - Mathf.Exp(-FollowSharpness * dt));
            ApplyScroll();
            if (Walking || InAtlas)
                return;
            partyAt = walkTarget;
            walkTarget = -1;
            bool clear = states[partyAt] == WorldNodeState.Open;
            string id = graph.Nodes[partyAt].Id;
            // 戦闘のあるマスは、勝つまでクリアにしない。保存も勝ったときに戦闘の画面がする。
            // 戦闘の画面を開く先がない（展示室）ときは、着いたらそのままクリアにする。
            if (clear && BattleRequested != null && WorldMapEncounters.Of(id) != null)
            {
                Fighting = true;
                BattleRequested(id);
                return;
            }
            // 戦闘のないマスは、着いたらクリアにする（物語の中身はまだない）。
            if (clear)
                cleared.Add(id);
            Refresh();
            // 宝箱は、初めてクリアしたときに中身をもらう。中身は保存できてから見せる。
            SaveTask =
                clear && graph.Nodes[partyAt].Kind == WorldNodeKind.Treasure
                    ? OpenChestAsync(id)
                    : SaveAsync(id, clear);
        }

        // 次のマスへ跳んでよいか。開けていないマスは、跳ぶ直前に払う。支払いはサーバーを待たずに決まるため、
        // そのまま跳ぶ。払えなければ、そこで止まる。止めたときに入っていないマスを払わないよう、前もっては払わない。
        private bool ReadyToHop()
        {
            if (held)
                return false;
            int next = walkSquares[0];
            if (PriceOf(next) == 0)
                return true;
            var result = Pay(graph.Nodes[next].Id);
            if (result.Paid)
                return true;
            Halt();
            ShowNotice(result.Message);
            return false;
        }

        /// <summary>
        /// Takes back the square <paramref name="id"/> whose payment the server did not take: it
        /// is no longer cleared, the walk stops, and a party standing on a square no longer paid
        /// for goes back to the last square on its way that still is. <paramref name="message"/>
        /// (when given) is told below the map.
        /// </summary>
        public void Unpaid(string id, string message)
        {
            if (!string.IsNullOrEmpty(message))
                ShowNotice(message);
            if (graph == null)
                return;
            // 払ったマスへ跳んでいる途中なら、まだ着いていないので、止めて元のマスへ戻す。
            if (!cleared.Remove(id) && !Walking)
                return;
            if (Walking)
                Halt();
            // 開けたマスか、値段のないマスまで戻る。
            int back = trail.Count - 1;
            while (
                back > 0
                && !cleared.Contains(graph.Nodes[trail[back]].Id)
                && trail[back] < prices.Length
                && prices[trail[back]] > 0
            )
                back--;
            // 立ち始めたマスは、開けたマスかタダのマスなので、そこより前へは戻らない。
            if (back >= 0 && back < trail.Count - 1)
            {
                trail.RemoveRange(back + 1, trail.Count - back - 1);
                partyAt = trail[back];
            }
            Home();
        }

        // 入れ子の地図で、道のマスに着いた。行き先なら Arrived で知らせ、途中で出来事を置いたマスに初めて
        // 着いたら、止まって EventReached で窓を開いてもらう。
        private void Landed(int at)
        {
            partyAt = at;
            trail.Add(at);
            string id = graph.Nodes[at].Id;
            bool first = cleared.Add(id);
            Refresh();
            if (walk.Count == 0)
            {
                walkTarget = -1;
                Arrived?.Invoke(id, first);
                return;
            }
            if (first && TryEventAt(at, out _) && EventReached != null)
            {
                held = true;
                EventReached(id);
            }
        }

        /// <summary>Lights the squares and lines of <paramref name="path"/>; null puts them out.</summary>
        public void Highlight(IReadOnlyList<int> path)
        {
            route.Clear();
            routeLines.Clear();
            if (path != null)
                for (int i = 0; i < path.Count; i++)
                {
                    route.Add(path[i]);
                    if (i > 0)
                        routeLines.Add((Mathf.Min(path[i - 1], path[i]), Mathf.Max(path[i - 1], path[i])));
                }
            Refresh();
        }

        private void TakeOff()
        {
            hopping = true;
            hopElapsed = 0f;
            hopFrom = partyDesign;
            if (PawnHop != null)
                PawnHop.Play(walk[0].x - hopFrom.x);
        }

        // 宝箱のマスのクリアを保存し、もらった中身を見せる。失敗したら保存先の進み具合へ戻す。
        private async System.Threading.Tasks.Task OpenChestAsync(string id)
        {
            Opening = true;
            var chest = await WorldMapSession.OpenChestAsync(id);
            if (this == null)
                return;
            if (chest == null)
            {
                Opening = false;
                if (!Walking && WorldMapSession.Known != null)
                    Apply(WorldMapSession.Known);
                ShowNotice(WorldMapSession.SaveFailedMessage);
                return;
            }
            if (Reveal != null)
            {
                await RevealChestAsync(chest);
                return;
            }
            if (Dialog == null)
            {
                Opening = false;
                return;
            }
            AdventureDialogs.Treasure(Dialog, chest, CloseChest, ItemArt);
        }

        // 冒険の宝箱と同じく、中身を良い物からすべての札でまとめて見せ、読む間をおいて閉じる。
        // 読む間は中身の数に合わせて少し延ばす（2026-10-29、ダイアログの文字から札へそろえた）。
        private async System.Threading.Tasks.Task RevealChestAsync(
            IReadOnlyList<AdventureDrop> chest
        )
        {
            var items = new List<RewardItem>();
            foreach (var drop in chest)
            {
                var item = AdventureDropItems.From(drop, ItemArt);
                if (item != null)
                    items.Add(item);
            }
            items.Sort(
                (a, b) =>
                    (b.Graded ? Quality.Grade(b.Quality) : -1)
                    - (a.Graded ? Quality.Grade(a.Quality) : -1)
            );
            if (items.Count == 0)
                Reveal.Show(null);
            else
                Reveal.ShowAll(items);
            try
            {
                await Awaitable.WaitForSecondsAsync(
                    ChestReadSeconds
                        + ExplorationFlow.ExtraReadSeconds * Mathf.Max(0, items.Count - 1),
                    destroyCancellationToken
                );
            }
            catch (OperationCanceledException)
            {
                return;
            }
            Reveal.Hide();
            Opening = false;
        }

        private void CloseChest()
        {
            if (Dialog != null)
                Dialog.Hide();
            Opening = false;
        }

        /// <summary>
        /// The battle could not be opened: the party goes back to where it stood, and the square
        /// can be tapped again.
        /// </summary>
        public void CancelBattle()
        {
            Fighting = false;
            Apply(WorldMapSession.Known ?? WorldMapProgress.Start);
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
            var move = new Vector2(-delta.x, delta.y) / zoom;
            MoveBy(move);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);
            glide = Vector2.Lerp(glide, move / dt, 0.5f);
        }

        private void MoveBy(Vector2 move)
        {
            scroll = Clamp(scroll + move);
            ApplyScroll();
        }

        // 2本指の中点の下にある地図の場所を、動いた後の中点の下へ運びながら大きさを変える。
        private void Pinch(Vector2 before, Vector2 after, float ratio)
        {
            var under = scroll + FromTopLeft(before) / zoom;
            zoom = ClampZoom(zoom * ratio);
            scroll = Clamp(under - FromTopLeft(after) / zoom);
            glide = Vector2.zero;
            ApplyScroll();
        }

        private float MinZoom => Mathf.Max(ViewSize.x / MapSize.x, ViewSize.y / MapSize.y);

        private float ClampZoom(float value) =>
            Mathf.Clamp(value, Mathf.Min(MinZoom, MaxZoom), MaxZoom);

        // 指の位置（Viewport の中の設計座標、y が上）を、画面の左上から見た位置（y が下）にする。
        private Vector2 FromTopLeft(Vector2 local)
        {
            if (Viewport == null)
                return local;
            var rect = Viewport.rect;
            return new Vector2(local.x - rect.xMin, rect.yMax - local.y);
        }

        private Vector2 ViewSize =>
            Viewport != null && Viewport.rect.width > 0f
                ? Viewport.rect.size
                : new Vector2(1920f, 1080f);

        private Vector2 MapSize =>
            (InAtlas ? AtlasLayout.PictureSize : WorldMapCatalog.PictureSize) * Scale;

        // 画面に入る地図の広さ（地図の設計座標）。
        private Vector2 SeenSize => ViewSize / zoom;

        // scroll は地図の左上から見た、画面の左上の位置（地図の設計座標、y は下向き）。
        // 地図の外が写らないよう縁で止める。地図が画面より狭い向きは、地図を画面の中央に置く
        // （地図を切り替える演出で大きく引いたとき）。
        private Vector2 Clamp(Vector2 value)
        {
            var max = MapSize - SeenSize;
            return new Vector2(Fit(value.x, max.x), Fit(value.y, max.y));
        }

        private static float Fit(float value, float max) =>
            max >= 0f ? Mathf.Clamp(value, 0f, max) : max * 0.5f;

        private void ApplyScroll()
        {
            if (Map == null)
                return;
            Map.localScale = new Vector3(zoom, zoom, 1f);
            Map.anchoredPosition = new Vector2(
                -Mathf.Round(scroll.x * zoom),
                Mathf.Round(scroll.y * zoom)
            );
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
                // 出来事の台は、まだ行けないマスでも暗くせず、先の目当てとして見せる。
                var happening = default(WorldEventKind);
                bool happens =
                    state is not (WorldNodeState.Hidden or WorldNodeState.Cleared)
                    && TryEventAt(i, out happening);
                image.sprite =
                    state == WorldNodeState.Hidden ? HiddenSprite
                    : happens ? EventSprites[(int)happening]
                    : KindSprites[(int)ShownKind(graph.Nodes[i].Kind, state)];
                image.color =
                    state == WorldNodeState.Seen && !happens && !route.Contains(i)
                        ? SeenTint
                        : Color.white;
                Nodes[i].localScale = Vector3.one;
            }
            for (int i = 0; i < LineFills.Length && i < graph.Lines.Count; i++)
            {
                var (from, to) = graph.Lines[i];
                var color = routeLines.Contains((Mathf.Min(from, to), Mathf.Max(from, to)))
                    ? RouteLine
                    : LineColor(states[from], states[to]);
                LineFills[i].color = color;
                var edge = EdgeColor;
                edge.a = color.a;
                LineEdges[i].color = edge;
            }
            if (Place != null)
                Place.text = InAtlas
                    ? $"{graph.Nodes[partyAt].Name}  Lv{AtlasLayout.level}"
                    : graph.Nodes[partyAt].Name;
            Refreshed?.Invoke();
        }

        /// <summary>The event placed on the square <paramref name="index"/>, if any.</summary>
        public bool TryEventAt(int index, out WorldEventKind kind)
        {
            kind = default;
            if (index < 0 || index >= NodeEvents.Length || NodeEvents[index] < 0)
                return false;
            kind = (WorldEventKind)NodeEvents[index];
            return true;
        }

        /// <summary>
        /// The kind of platform a square shows. A cleared square has nothing left to happen on
        /// it, so any square but a town, a region or a gate shows the empty platform once cleared.
        /// </summary>
        public static WorldNodeKind ShownKind(WorldNodeKind kind, WorldNodeState state) =>
            state == WorldNodeState.Cleared
            && kind is not (WorldNodeKind.Town or WorldNodeKind.Region or WorldNodeKind.Gate)
                ? WorldNodeKind.Empty
                : kind;

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
                if (states[i] == WorldNodeState.Open || (route.Count > 0 && route.Contains(i) && i != partyAt))
                    Nodes[i].localScale = Vector3.one * scale;
        }
    }
}
