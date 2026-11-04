using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.Combat.Presentation;
using Baryonyx.UI.EditModePreview;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// The fireworks shot up from a town when its dungeon opens (doc/features/stage-progression/destinations.md
    /// ダンジョンの解放): the map sinks a little, four shells rise from the town one after another and
    /// burst above it in white and pale blue (the Battle Vfx Shape shader's Firework, Streak and Spark),
    /// and the window telling of the dungeon shows while the last of them fade behind it.
    /// <see cref="Show"/> draws it at a time since the town was reached, so <see cref="Play"/> runs it
    /// frame by frame and any moment can be shown still. The prefab is saved showing the bursts open,
    /// for the showcase; it hides itself when it wakes.
    /// </summary>
    public sealed class DungeonFireworks : MonoBehaviour, IEditModePreview
    {
        /// <summary>Where the prefab is under Resources.</summary>
        public const string ResourcePath = "DungeonFireworks";

        // 開いてから消えるまで、窓を出す時刻（町に着いてから）。
        public const float BurstSeconds = 0.95f;
        public const float DialogAt = 1.15f;

        // 地図を沈める暗さが届くまで。
        private const float SinkSeconds = 0.25f;

        // 光の色：白に近い淡い青、少し青緑寄りの青、ほぼ白の青。黄色・金は使わない（2026-11-04、ユーザー）。
        private static readonly Color Pale = new(0.62f, 0.84f, 1f, 1f);
        private static readonly Color Teal = new(0.55f, 0.9f, 0.86f, 1f);
        private static readonly Color Mist = new(0.8f, 0.9f, 1f, 1f);

        // 1発ずつ：町から見た開く位置、打ち上げる時刻と開く時刻、板の一辺（星が飛ぶのは一辺の4割ほど）、色、種。
        private readonly struct Shell
        {
            public readonly Vector2 Offset;
            public readonly float Launch;
            public readonly float Burst;
            public readonly float Size;
            public readonly Color Tint;
            public readonly float Seed;

            public Shell(Vector2 offset, float launch, float burst, float size, Color tint, float seed)
            {
                Offset = offset;
                Launch = launch;
                Burst = burst;
                Size = size;
                Tint = tint;
                Seed = seed;
            }
        }

        // 町の真上に大きく1つ、左・右・上に小さく3つを、少しずつずらして開く（2026-11-04、ユーザー「一輪は寂しいから他にも」）。
        private static readonly Shell[] Shells =
        {
            new(new Vector2(0f, 300f), 0f, 0.32f, 640f, Pale, 0.13f),
            new(new Vector2(-380f, 200f), 0.14f, 0.44f, 460f, Teal, 0.58f),
            new(new Vector2(400f, 250f), 0.26f, 0.58f, 520f, Mist, 0.31f),
            new(new Vector2(-130f, 420f), 0.5f, 0.8f, 380f, Pale, 0.84f),
        };

        private static readonly string[] States = { "上がる", "開く", "広がる", "消える" };
        private static readonly float[] StateTimes = { 0.2f, 0.5f, 0.78f, 1.1f };

        /// <summary>The town the preview shoots from, in this layer's space (0 at its middle).</summary>
        public static readonly Vector2 PreviewTown = new(0f, -160f);

        /// <summary>The time the saved prefab shows (the bursts open).</summary>
        public static float PreviewTime => StateTimes[1];

        /// <summary>The whole length, from reaching the town until the last burst is gone.</summary>
        public static float Seconds => Shells[^1].Burst + BurstSeconds;

        [Tooltip("地図を少し沈め、花火の間は地図を押せなくする。")]
        public Image Dim;

        [Tooltip("1発ずつの、開いた花火・打ち上げの尾・打ち上げの光の頭。")]
        public BattleVfxImage[] Bursts = Array.Empty<BattleVfxImage>();
        public BattleVfxImage[] Trails = Array.Empty<BattleVfxImage>();
        public BattleVfxImage[] Heads = Array.Empty<BattleVfxImage>();

        [Tooltip("沈める暗さ（窓の幕と同じにして、窓が出たときに暗さが変わらないようにする）、打ち上げの尾の幅と最も長いとき、光の頭の大きさ。")]
        public float DimAlpha = 0.72f;
        public float TrailWidth = 30f;
        public float TrailLength = 150f;
        public float HeadSize = 40f;

        private Vector2 town;
        private Action due;
        private Coroutine running;

        /// <summary>True while it plays.</summary>
        public bool Playing => running != null;

        public IReadOnlyList<string> PreviewStates => States;

        private void Awake()
        {
            BattleVfxImage.EnableShapeChannel(transform);
            if (running == null)
                Hide();
        }

        private void OnDisable()
        {
            if (running == null)
                return;
            running = null;
            Finish();
        }

        /// <summary>Lays this layer over the whole of the screen it was put in.</summary>
        public void Attach()
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            BattleVfxImage.EnableShapeChannel(transform);
            Hide();
        }

        /// <summary>
        /// Shoots the fireworks from <paramref name="square"/>; <paramref name="dialogTime"/> runs
        /// when the window should show (or at once if this cannot play).
        /// </summary>
        public void Play(RectTransform square, Action dialogTime)
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
                Finish();
            }
            town = PlaceOf(square);
            due = dialogTime;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (!isActiveAndEnabled)
            {
                Finish();
                return;
            }
            running = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float time = 0f;
            while (time < Seconds)
            {
                Show(time);
                if (time >= DialogAt)
                    Tell();
                yield return null;
                time += Time.unscaledDeltaTime;
            }
            running = null;
            Finish();
        }

        // 窓を出す時刻になったら、1回だけ知らせる。
        private void Tell()
        {
            var next = due;
            due = null;
            next?.Invoke();
        }

        private void Finish()
        {
            Hide();
            Tell();
        }

        private void Hide()
        {
            if (Dim != null)
            {
                Dim.color = WithAlpha(Dim.color, 0f);
                Dim.raycastTarget = false;
            }
            foreach (var board in Bursts)
                Off(board);
            foreach (var board in Trails)
                Off(board);
            foreach (var board in Heads)
                Off(board);
        }

        /// <summary>Draws the fireworks <paramref name="time"/> seconds after the town was reached.</summary>
        public void Show(float time)
        {
            // 窓が出るまでは地図を沈めて押せなくし、窓が出たら窓の幕に任せる。
            if (Dim != null)
            {
                bool before = time < DialogAt;
                float sink = before ? Mathf.SmoothStep(0f, 1f, time / SinkSeconds) : 0f;
                Dim.color = WithAlpha(Dim.color, DimAlpha * sink);
                Dim.raycastTarget = before;
            }
            for (int i = 0; i < Shells.Length; i++)
                ShowShell(i, time);
        }

        private void ShowShell(int i, float time)
        {
            var shell = Shells[i];
            var at = BurstPlace(shell);
            var burst = i < Bursts.Length ? Bursts[i] : null;
            var trail = i < Trails.Length ? Trails[i] : null;
            var head = i < Heads.Length ? Heads[i] : null;

            // 打ち上げ：町から開く位置へ、上がるほど遅く。尾は上がった道のりの分だけ伸びる。
            float rise = (time - shell.Launch) / Mathf.Max(shell.Burst - shell.Launch, 1e-4f);
            if (rise >= 0f && rise < 1f)
            {
                float eased = 1f - (1f - rise) * (1f - rise);
                var tip = Vector2.Lerp(town, at, eased);
                var path = at - town;
                var dir = path.sqrMagnitude > 1e-4f ? path.normalized : Vector2.up;
                float length = Mathf.Min(Vector2.Distance(town, tip), TrailLength);
                if (trail != null)
                {
                    // 尾の頭は板の84%の所にある（Streak）。
                    float h = Mathf.Max(length / 0.84f, 1f);
                    Place(trail, tip - dir * (0.34f * h), new Vector2(TrailWidth, h), shell.Tint);
                    trail.rectTransform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
                    trail.SetShape(shell.Seed, rise, 0f, 0.9f);
                }
                if (head != null)
                {
                    Place(head, tip, Vector2.one * HeadSize, shell.Tint);
                    // 開く直前は頭を細らせ、開いた花火の中心の光へ渡す。
                    head.SetShape(shell.Seed, rise, 0f, 1f - 0.5f * rise);
                }
            }
            else
            {
                Off(trail);
                Off(head);
            }

            float life = (time - shell.Burst) / BurstSeconds;
            if (burst != null && life >= 0f && life < 1f)
            {
                Place(burst, at, Vector2.one * shell.Size, shell.Tint);
                burst.SetShape(shell.Seed, life, 0f, 1f);
            }
            else
                Off(burst);
        }

        // 開く位置。画面の外へ出る分は内側へ寄せる（町が画面の端にあるとき）。
        private Vector2 BurstPlace(Shell shell)
        {
            var area = ((RectTransform)transform).rect;
            var at = town + shell.Offset;
            if (area.height <= 0f)
                return at;
            float reach = shell.Size * 0.42f;
            float left = -area.width * 0.5f + reach;
            float right = area.width * 0.5f - reach;
            float top = area.height * 0.5f - reach;
            if (left < right)
                at.x = Mathf.Clamp(at.x, left, right);
            at.y = Mathf.Min(at.y, top);
            return at;
        }

        private static void Place(BattleVfxImage board, Vector2 at, Vector2 size, Color tint)
        {
            var rect = board.rectTransform;
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
            board.color = tint;
            if (!board.gameObject.activeSelf)
                board.gameObject.SetActive(true);
        }

        private static void Off(BattleVfxImage board)
        {
            if (board != null && board.gameObject.activeSelf)
                board.gameObject.SetActive(false);
        }

        private static Color WithAlpha(Color color, float alpha) =>
            new(color.r, color.g, color.b, alpha);

        // target の中心が、この層の中のどこか（0は層の中央）。
        private Vector2 PlaceOf(RectTransform target)
        {
            var root = (RectTransform)transform;
            if (target == null)
                return Vector2.zero;
            Vector3 world = target.TransformPoint(target.rect.center);
            return (Vector2)root.InverseTransformPoint(world) - root.rect.center;
        }

        public void ShowPreviewState(int index)
        {
            town = PreviewTown;
            Show(StateTimes[Mathf.Clamp(index, 0, StateTimes.Length - 1)]);
        }

        /// <summary>Shows the saved still: the bursts open over <see cref="PreviewTown"/>.</summary>
        public void ShowPreview()
        {
            town = PreviewTown;
            Show(PreviewTime);
        }
    }
}
