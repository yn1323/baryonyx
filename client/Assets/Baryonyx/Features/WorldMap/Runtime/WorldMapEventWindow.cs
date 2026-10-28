using System;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// A window for what happens on a square, opened from the square itself with the map left
    /// behind it (doc/features/world-atlas.md#マスの出来事の見本): the map slides the square below
    /// the window's place, the square lights up and thin lines of light rise from it, then the
    /// window grows up out of the square with a slight overshoot while the map dims, and the
    /// event's characters and things drop in. What happens inside is the open
    /// <see cref="WorldEventPanel"/>'s. Closing shrinks the window back into the square; a battle
    /// instead swells the window into a white flash that hands over to the battle screen. A tap on
    /// the dimmed map skips the opening. All times run on the real clock, like the map's walk.
    /// </summary>
    public sealed class WorldMapEventWindow : MonoBehaviour
    {
        /// <summary>When the cross that closes the window shows.</summary>
        public enum CloseRule
        {
            // 窓と一緒に出し、いつでも閉じられる。
            Anytime,

            // 出来事の終わり（もらう物を見せる、仲間になる）まで出さない。
            AfterDone,

            // ×を出さず、出来事が自分で閉じる（拾い物）。
            Auto,
        }

        public CanvasGroup Group;
        public Image Veil;
        public Button Skip;
        public RawImage Glow;
        public RawImage[] Rays = Array.Empty<RawImage>();
        public RectTransform Window;
        public CanvasGroup WindowGroup;
        public RawImage Scenery;

        // 窓の中を押すと、出来事が先へ進む。
        public Button Next;
        public Button Close;
        public CanvasGroup CloseGroup;

        // 戦闘へ切り替わるときに、画面を白く覆う。
        public Image Flash;
        public WorldEventPanel[] Panels = Array.Empty<WorldEventPanel>();

        // 開く流れ：マスが光る→光の線が昇る→窓がマスから育つ→物が1つずつ降り立つ。
        public const float RaySeconds = 0.5f;
        public const float GrowDelay = 0.18f;
        public const float GrowSeconds = 0.42f;
        public const float ArriveDelay = 0.5f;
        public const float ArriveGap = 0.12f;
        public const float ArriveSeconds = 0.3f;
        public const float CloseSeconds = 0.26f;
        private const float PopSeconds = 0.22f;
        private const float CloseFadeSeconds = 0.2f;

        // 戦闘へ：窓がふくらみながら白く覆われる時間と、試作で地図へ戻るときに白が引く時間。
        public const float DepartSeconds = 0.45f;
        private const float ReturnSeconds = 0.5f;

        // 窓の置き場所（画面の中央）と、そのとき地図を滑らせてマスを見せる位置（画面の中央から、
        // 設計座標、y が上）。マスは窓の下の端より下に見え、光の線がマスから窓へ昇る。
        public static readonly Vector2 Home = Vector2.zero;
        public static readonly Vector2 SquareAt = new(0f, -485f);

        private const float StartScale = 0.08f;
        private const float ArriveDrop = 130f;

        private enum Phase
        {
            Hidden,
            Opening,
            Shown,
            Closing,
            Popping,
            Departing,
            Returning,
        }

        private static Texture2D rayTexture;
        private static Texture2D glowTexture;

        private Phase phase;
        private float elapsed;
        private RectTransform square;
        private Vector3 from;
        private Action closed;
        private Action battle;
        private float closeAlpha;
        private readonly float[] rayX = new float[16];
        private readonly float[] rayDelay = new float[16];
        private readonly float[] rayRise = new float[16];

        public bool IsShown => phase != Phase.Hidden;
        public bool Opening => phase == Phase.Opening;

        /// <summary>The window swells into the flash that leads to the battle screen.</summary>
        public bool Departing => phase is Phase.Departing or Phase.Returning;

        /// <summary>The event shown, or the last one.</summary>
        public WorldEventPanel Panel { get; private set; }

        public bool CanClose => Panel == null || Panel.Rule == CloseRule.Anytime || Panel.Done;

        /// <summary>A glow fading out from the middle, for the panels' bursts of light.</summary>
        public static Texture2D GlowTexture => glowTexture ??= MakeGlowTexture();

        private float OpenSeconds =>
            Mathf.Max(
                GrowDelay + GrowSeconds,
                ArriveDelay + Mathf.Max(0, Panel.Arrivals.Length - 1) * ArriveGap + ArriveSeconds
            );

        private void Awake()
        {
            rayTexture ??= MakeRayTexture();
            foreach (var ray in Rays)
                ray.texture = rayTexture;
            if (Glow != null)
                Glow.texture = GlowTexture;
            if (Skip != null)
                Skip.onClick.AddListener(Advance);
            if (Next != null)
                Next.onClick.AddListener(Advance);
            if (Close != null)
                Close.onClick.AddListener(Hide);
            foreach (var panel in Panels)
                panel.gameObject.SetActive(false);
            HideAll();
        }

        public T PanelOf<T>()
            where T : WorldEventPanel
        {
            foreach (var panel in Panels)
                if (panel is T found)
                    return found;
            return null;
        }

        public WorldEventPanel PanelOf(WorldEventKind kind)
        {
            foreach (var panel in Panels)
                if (panel.Kind == kind)
                    return panel;
            return null;
        }

        /// <summary>
        /// Opens the window out of <paramref name="from"/> (a square on the map) for the event of
        /// <paramref name="kind"/>. <paramref name="whenClosed"/> runs once the window has gone
        /// back into the square. A battle's fight button calls <paramref name="toBattle"/> once
        /// the screen is white; without it (the prototype), the white fades back to the map.
        /// </summary>
        public void Open(
            RectTransform from,
            WorldEventKind kind,
            Action whenClosed,
            Action toBattle = null
        )
        {
            Panel = PanelOf(kind);
            if (Panel == null)
            {
                Debug.LogError("The event window has no panel for " + kind + ".", this);
                return;
            }
            square = from;
            closed = whenClosed;
            battle = toBattle;
            foreach (var panel in Panels)
                panel.gameObject.SetActive(panel == Panel);
            Window.sizeDelta = Panel.WindowSize;
            Window.localRotation = Quaternion.identity;
            if (Scenery != null)
                Scenery.enabled = Panel.ShowScenery;
            Panel.Begin(this);
            closeAlpha = 0f;
            FollowSquare();
            var random = new System.Random(Environment.TickCount);
            for (int i = 0; i < Rays.Length && i < rayX.Length; i++)
            {
                rayX[i] = ((float)random.NextDouble() * 2f - 1f) * 34f;
                rayDelay[i] = i * 0.025f + (float)random.NextDouble() * 0.05f;
                rayRise[i] = 160f + (float)random.NextDouble() * 120f;
                var size = Rays[i].rectTransform.sizeDelta;
                size.y = 60f + (float)random.NextDouble() * 70f;
                Rays[i].rectTransform.sizeDelta = size;
            }
            Group.alpha = 1f;
            Group.blocksRaycasts = true;
            Group.interactable = true;
            transform.SetAsLastSibling();
            phase = Phase.Opening;
            elapsed = 0f;
            Draw(0f);
        }

        /// <summary>Ends the opening at once (a tap on the dimmed map).</summary>
        public void FinishOpening()
        {
            if (phase != Phase.Opening)
                return;
            elapsed = OpenSeconds;
            Draw(0f);
        }

        /// <summary>
        /// A tap on the window or the dimmed map: skips the opening, or lets the event go on.
        /// </summary>
        public void Advance()
        {
            if (phase == Phase.Opening)
                FinishOpening();
            else if (phase == Phase.Shown)
                Panel.Advance();
        }

        /// <summary>Shrinks the window back into its square, unless the event keeps it open.</summary>
        public void Hide()
        {
            if (phase is not (Phase.Opening or Phase.Shown) || !CanClose)
                return;
            phase = Phase.Closing;
            elapsed = 0f;
            Group.interactable = false;
            Draw(0f);
        }

        /// <summary>Swells the window into a white flash that leads to the battle screen.</summary>
        public void Depart()
        {
            if (phase != Phase.Shown)
                return;
            phase = Phase.Departing;
            elapsed = 0f;
            Group.interactable = false;
            Draw(0f);
        }

        private void Update()
        {
            if (phase == Phase.Hidden)
                return;
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            Draw(dt);
        }

        // 地図が滑る間も、マスの今の位置から開き、マスへ戻す。
        private void FollowSquare()
        {
            if (square == null)
                return;
            from = ((RectTransform)transform).InverseTransformPoint(square.position);
            if (Glow != null)
                Glow.rectTransform.localPosition = from;
        }

        private void Draw(float dt)
        {
            FollowSquare();
            switch (phase)
            {
                case Phase.Opening:
                    DrawOpening(elapsed);
                    if (elapsed >= OpenSeconds)
                    {
                        // 開き終えたコマから、中身を動かし始める。
                        phase = Phase.Shown;
                        Panel.Tick(0f);
                    }
                    DrawClose(dt);
                    break;
                case Phase.Shown:
                    DrawOpening(OpenSeconds);
                    Panel.Tick(dt);
                    DrawClose(dt);
                    // 窓が開いている間、マスの光はゆっくり息をする。
                    if (Glow != null)
                        Glow.color = Tint(0.55f + 0.15f * Mathf.Sin(elapsed * 3f));
                    break;
                case Phase.Closing:
                    DrawClosing(Mathf.Clamp01(elapsed / CloseSeconds));
                    if (elapsed >= CloseSeconds)
                    {
                        phase = Phase.Popping;
                        elapsed = 0f;
                        Group.blocksRaycasts = false;
                        Window.gameObject.SetActive(false);
                    }
                    break;
                case Phase.Popping:
                    DrawPop(Mathf.Clamp01(elapsed / PopSeconds));
                    if (elapsed >= PopSeconds)
                        Finish();
                    break;
                case Phase.Departing:
                    DrawDeparting(Mathf.Clamp01(elapsed / DepartSeconds));
                    if (elapsed >= DepartSeconds)
                        LeaveForBattle();
                    break;
                case Phase.Returning:
                    SetFlash(1f - Mathf.Clamp01(elapsed / ReturnSeconds));
                    if (elapsed >= ReturnSeconds)
                        Finish();
                    break;
            }
        }

        private void DrawOpening(float t)
        {
            // マスが一度ふくらんで光る。
            float flash = Mathf.Clamp01(t / 0.12f);
            if (square != null)
                square.localScale =
                    Vector3.one * (1f + 0.25f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.24f)));
            if (Glow != null)
            {
                Glow.rectTransform.localScale =
                    Vector3.one * Mathf.Lerp(0.5f, 1f, EventMotion.EaseOut(flash));
                Glow.color = Tint(
                    Mathf.Lerp(0f, 0.9f, flash) - 0.3f * Mathf.Clamp01((t - 0.3f) / 0.3f)
                );
            }

            // 細い光の線が、マスから少しずつずれて昇っては消える。
            for (int i = 0; i < Rays.Length && i < rayX.Length; i++)
            {
                float x = (t - rayDelay[i]) / RaySeconds;
                bool live = x > 0f && x < 1f;
                Rays[i].enabled = live;
                if (!live)
                    continue;
                var rect = Rays[i].rectTransform;
                rect.localPosition =
                    from + new Vector3(rayX[i], rayRise[i] * EventMotion.EaseOut(x), 0f);
                Rays[i].color = new Color(1f, 0.94f, 0.74f, Mathf.Sin(Mathf.PI * x));
            }

            // 背景の地図は、窓が育つのに合わせて暗くなる。
            float grow = Mathf.Clamp01((t - GrowDelay) / GrowSeconds);
            SetVeil(Panel.Veil * EventMotion.EaseOut(grow));
            Window.gameObject.SetActive(grow > 0f);
            Window.localPosition = Vector3.LerpUnclamped(from, Home, EventMotion.Decelerate(grow));
            Window.localScale =
                Vector3.one * Mathf.LerpUnclamped(StartScale, 1f, EventMotion.EaseOutBack(grow));
            WindowGroup.alpha = Mathf.Clamp01(grow / 0.3f);

            // 物は窓が開ききる手前で1つずつ上から降り、着地で少しつぶれる。
            var arrivals = Panel.Arrivals;
            for (int i = 0; i < arrivals.Length; i++)
            {
                float drop = Mathf.Clamp01((t - ArriveDelay - i * ArriveGap) / ArriveSeconds);
                float fall = Mathf.Clamp01(drop / 0.6f);
                float land = Mathf.Clamp01((drop - 0.6f) / 0.4f);
                var arrival = arrivals[i];
                arrival.gameObject.SetActive(drop > 0f);
                arrival.anchoredPosition = new Vector2(0f, ArriveDrop * (1f - fall * fall));
                float squash = Mathf.Sin(Mathf.PI * land) * 0.1f;
                arrival.localScale = new Vector3(1f + squash, 1f - squash, 1f);
            }
        }

        // 閉じる×は、出来事が許したときだけ0.2秒で現れる。
        private void DrawClose(float dt)
        {
            bool shows = Panel.Rule != CloseRule.Auto && CanClose;
            if (phase == Phase.Opening)
                closeAlpha = shows ? WindowGroup.alpha : 0f;
            else
                closeAlpha = Mathf.MoveTowards(closeAlpha, shows ? 1f : 0f, dt / CloseFadeSeconds);
            CloseGroup.alpha = closeAlpha;
            CloseGroup.interactable = CloseGroup.blocksRaycasts = shows;
        }

        // 窓は加速しながらマスへ縮んで戻り、背景が明るさを取り戻す。
        private void DrawClosing(float x)
        {
            float back = EventMotion.EaseIn(x);
            Window.localPosition = Vector3.LerpUnclamped(Home, from, back);
            Window.localScale = Vector3.one * Mathf.Lerp(1f, StartScale, back);
            WindowGroup.alpha = 1f - Mathf.Clamp01((x - 0.5f) / 0.5f);
            SetVeil(Panel.Veil * (1f - x));
            foreach (var ray in Rays)
                ray.enabled = false;
            if (Glow != null)
                Glow.color = Tint(0.55f * (1f - x) + 0.3f * x);
        }

        // 窓を飲み込んだマスが、一度ふくらんで光を散らす。
        private void DrawPop(float x)
        {
            if (square != null)
                square.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(Mathf.PI * x));
            if (Glow != null)
            {
                Glow.rectTransform.localScale =
                    Vector3.one * Mathf.Lerp(1f, 1.8f, EventMotion.EaseOut(x));
                Glow.color = Tint(0.9f * (1f - x));
            }
        }

        // 窓がふくらみながら少し傾き、画面が白に覆われる。
        private void DrawDeparting(float x)
        {
            float swell = EventMotion.EaseIn(x);
            Window.localScale = Vector3.one * (1f + 0.18f * swell);
            Window.localRotation = Quaternion.Euler(0f, 0f, -3f * swell);
            SetFlash(Mathf.Clamp01((x - 0.25f) / 0.75f));
        }

        private void LeaveForBattle()
        {
            if (battle != null)
            {
                // 戦闘の画面の読み込みに渡す。白い幕はシーンと一緒に消える。
                phase = Phase.Hidden;
                var then = battle;
                battle = null;
                then();
                return;
            }
            // 試作では戦闘の画面がないため、白の下で窓を片付け、地図へ戻す。
            Window.gameObject.SetActive(false);
            Window.localRotation = Quaternion.identity;
            SetVeil(0f);
            if (Glow != null)
                Glow.color = Tint(0f);
            if (square != null)
                square.localScale = Vector3.one;
            Group.blocksRaycasts = false;
            phase = Phase.Returning;
            elapsed = 0f;
        }

        private void Finish()
        {
            HideAll();
            var then = closed;
            closed = null;
            then?.Invoke();
        }

        private void HideAll()
        {
            phase = Phase.Hidden;
            elapsed = 0f;
            if (square != null)
                square.localScale = Vector3.one;
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
            Group.interactable = false;
            SetVeil(0f);
            SetFlash(0f);
            if (Glow != null)
                Glow.color = Tint(0f);
            foreach (var ray in Rays)
                ray.enabled = false;
            Window.localRotation = Quaternion.identity;
            Window.gameObject.SetActive(false);
        }

        private void SetVeil(float alpha)
        {
            if (Veil != null)
                Veil.color = EventMotion.WithAlpha(Veil.color, alpha);
        }

        private void SetFlash(float alpha)
        {
            if (Flash == null)
                return;
            Flash.color = EventMotion.WithAlpha(Flash.color, alpha);
            Flash.enabled = alpha > 0f;
        }

        private static Color Tint(float alpha) => new(1f, 0.86f, 0.5f, Mathf.Clamp01(alpha));

        // 光の線：上の端が明るく、下へ向かって消える細い帯。
        private static Texture2D MakeRayTexture()
        {
            const int height = 64;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                float alpha = Mathf.Pow(v, 1.6f) * (1f - Mathf.SmoothStep(0.88f, 1f, v));
                texture.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            return texture;
        }

        // マスの光：中心から外へなめらかに消える円。
        private static Texture2D MakeGlowTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            float half = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Clamp01(new Vector2(x - half, y - half).magnitude / half);
                float alpha = (1f - r) * (1f - r);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            return texture;
        }
    }
}
