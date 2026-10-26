using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// Shows the tap sparkle wherever a finger touches, on every screen
    /// (doc/features/screens/common.md#押した位置のキラキラ). <see cref="Install"/> puts one in
    /// front of every screen when the game starts and keeps it across scenes: an overlay canvas that
    /// takes no clicks, so the screens under it work as before. It reads the touches (and, without a
    /// touchscreen, the mouse) itself, follows <see cref="TapSparkleGestures"/>, and draws the gold
    /// dust (<see cref="TapSparkleGoldDust"/>). It runs on real time, so a hit stop or a paused game
    /// does not hold it, except while frames are captured at a fixed step.
    /// </summary>
    public sealed class TapSparkleLayer : MonoBehaviour
    {
        public const string ShaderName = "Baryonyx/UI/Tap Sparkle";

        // ほかのどの画面（遷移の演出の1000を含む）より手前に描く。
        private const int SortingOrder = 32000;
        private const int MouseFinger = -1;

        [Tooltip("形を並べて描く部品。")]
        public TapSparkleMarks Marks;

        [Tooltip(
            "演出の大きさの倍率（1がブラウザの試作と同じ大きさ）。指の動きの判定には掛けない。"
        )]
        [Range(0.3f, 1.5f)]
        public float Size = 0.6f;

        [Tooltip("指とマウスの入力を読む。展示室の見本では切る。")]
        public bool ReadsInput = true;

        [Tooltip("0より大きいと、この秒数ごとに中央を自動で押す（展示室の見本）。")]
        public float PreviewEvery;

        private static TapSparkleLayer installed;
        private TapSparkleGestures gestures;
        private float clock;
        private float nextPreview;
        private bool previewHeld;
        private uint seed = 1;

        /// <summary>The layer's clock (seconds of real time), which <see cref="Gestures"/> is told.</summary>
        public float Clock => clock;

        /// <summary>The rules this layer follows (for tests).</summary>
        public TapSparkleGestures Gestures => gestures ??= NewGestures();

        /// <summary>The layer in front of every screen, once the game has started.</summary>
        public static TapSparkleLayer Installed => installed;

        // Domain Reloadを省略したPlay開始でも、前回の層を持ち越さない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => installed = null;

        /// <summary>Puts the layer in front of every screen, once, and keeps it across scenes.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (installed != null)
                return;
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning(
                    $"The tap sparkle is off: the shader '{ShaderName}' is not in the build."
                );
                return;
            }
            var root = new GameObject("TapSparkle", typeof(RectTransform));
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1;

            var marks = TapSparkleMarks.AddTo(root.transform, shader);
            installed = root.AddComponent<TapSparkleLayer>();
            installed.Marks = marks;
            DontDestroyOnLoad(root);
            root.SetActive(true);
        }

        private TapSparkleGestures NewGestures() =>
            new(
                TapSparkleGoldDust.Duration,
                () => TapSparkleGoldDust.NewState(new TapRandom(seed++ * 2654435761u))
            );

        private void OnDisable()
        {
            Gestures.Clear();
            if (Marks != null)
                Marks.Clear();
        }

        private void Update()
        {
            // 実時間で進める。コマを決まった間隔で撮っている間だけは、その間隔で進める。
            clock += Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            if (ReadsInput)
                ReadInput();
            if (PreviewEvery > 0f)
                Preview();
            Gestures.Tick(clock);
            Draw();
        }

        // --- Input ---------------------------------------------------------------------

        private void ReadInput()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    int id = touch.touchId.ReadValue();
                    if (touch.press.wasPressedThisFrame)
                        Gestures.Down(id, DesignPoint(touch.position.ReadValue()), clock);
                    else if (touch.press.wasReleasedThisFrame)
                        Gestures.Up(
                            id,
                            clock,
                            touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled
                        );
                    else if (touch.press.isPressed)
                        Gestures.Move(id, DesignPoint(touch.position.ReadValue()), clock);
                }
                return;
            }
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            var button = mouse.leftButton;
            if (button.wasPressedThisFrame)
                Gestures.Down(MouseFinger, DesignPoint(mouse.position.ReadValue()), clock);
            else if (button.wasReleasedThisFrame)
                Gestures.Up(MouseFinger, clock);
            else if (button.isPressed)
                Gestures.Move(MouseFinger, DesignPoint(mouse.position.ReadValue()), clock);
        }

        private Vector2 DesignPoint(Vector2 screen) =>
            Marks != null ? Marks.DesignPoint(screen) : screen;

        // 見本：中央を0.12秒押して離し、PreviewEvery 秒ごとに繰り返す。
        private void Preview()
        {
            const int previewFinger = -100;
            if (clock < nextPreview)
                return;
            if (previewHeld)
            {
                Gestures.Up(previewFinger, clock);
                previewHeld = false;
                nextPreview = clock + PreviewEvery;
                return;
            }
            var size = Marks != null ? Marks.rectTransform.rect.size : Vector2.zero;
            Gestures.Down(previewFinger, size / 2f, clock);
            previewHeld = true;
            nextPreview = clock + 0.12f;
        }

        // --- Drawing -------------------------------------------------------------------

        private void Draw()
        {
            if (Marks == null)
                return;
            Marks.Clear();
            foreach (var spark in Gestures.Sparks)
            {
                Marks.SetOrigin(spark.At, Size);
                var age = clock - spark.Start;
                if (spark.Phase == TapSparkleGestures.Phase.Burst)
                    TapSparkleGoldDust.DrawBurst(Marks, spark.State, age);
                else
                    TapSparkleGoldDust.DrawSign(
                        Marks,
                        spark.State,
                        age,
                        TapSparkleGestures.SignLeft(spark, clock)
                    );
            }
        }
    }
}
