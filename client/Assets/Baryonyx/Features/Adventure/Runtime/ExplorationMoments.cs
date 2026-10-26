using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.Inventory;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The exploration screen's turning points (doc/features/screens/debug-rooms.md, the exploration moment
    /// lab), laid over the map: monsters found in the room the party walks into (a "！" over the
    /// party, a band with the room's words and the wipe that carries it into the battle),
    /// stronger for a strong monster and darker for the deepest room's master; and a chest opened,
    /// which trembles, glows in the colour of the quality it holds and bursts open on the plate of
    /// what was found. The light is worked out by a shader with no picture (Exploration Light).
    /// </summary>
    public sealed class ExplorationMoments : MonoBehaviour
    {
        public const string StrongPrompt = "強い魔物が現れた！";
        public const string MasterPrompt = "最奥の主が現れた！";
        public const string TrialPrompt = "試練の魔物が現れた！";
        public const string MimicPrompt = "ミミックだ！";

        // The wipe's bands, and how long they take to close and to open.
        public const float WipeClose = 0.34f;
        public const float WipeHold = 0.35f;
        public const float WipeOpen = 0.3f;
        private const float WipeStagger = 0.025f;

        // The shapes of the Exploration Light shader.
        private const int GlowShape = 0;
        private const int RaysShape = 1;
        private const int SparkleShape = 2;

        [Tooltip("演出を重ねる探索画面。")]
        public ExplorationView View;

        [Header("出現")]
        [Tooltip("パーティの頭の上に出す「！」。")]
        public TMP_Text Mark;

        [Tooltip("出現の文を出す帯と、その上下の線。")]
        public TranslucentTextPanel Band;
        public CanvasGroup BandGroup;
        public Graphic[] BandLines = Array.Empty<Graphic>();

        [Tooltip("戦闘へ移るときに、左右から交互に閉じる横長の帯。")]
        public RectTransform[] WipeBands = Array.Empty<RectTransform>();

        [Header("画面")]
        [Tooltip("最奥の主や品質A・Sの宝箱で、地図を暗くする幕。")]
        public Image Dim;

        [Header("光")]
        [Tooltip("光を置く層（画面いっぱい）。")]
        public RectTransform LightLayer;

        [Tooltip("光の形を計算で描くマテリアル（Exploration Light）。")]
        public Material LightMaterial;

        [Header("宝箱")]
        public RewardReveal Reveal;

        private readonly Material[] shapes = new Material[3];
        private readonly List<RawImage> lights = new();
        private Coroutine playing;

        /// <summary>True while a moment plays (or the party walks).</summary>
        public bool Busy => playing != null || (View != null && (View.Walking || View.Clearing));

        /// <summary>The kind's colours: the band's lines and words, and the "！".</summary>
        public static (Color line, Color words, Color mark) ColorsOf(AdventureRoomKind kind) =>
            kind switch
            {
                AdventureRoomKind.Elite
                or AdventureRoomKind.Trial
                or AdventureRoomKind.ShadyChest => (
                    new Color(1f, 0.34f, 0.26f),
                    new Color(1f, 0.6f, 0.52f),
                    new Color(1f, 0.36f, 0.26f)
                ),
                AdventureRoomKind.Boss => (
                    new Color(0.7f, 0.45f, 0.98f),
                    new Color(0.86f, 0.76f, 1f),
                    new Color(0.82f, 0.62f, 1f)
                ),
                _ => (
                    new Color(0.86f, 0.36f, 0.31f),
                    new Color(1f, 0.66f, 0.6f),
                    new Color(1f, 0.86f, 0.36f)
                ),
            };

        /// <summary>The band's words when the party meets the monsters of a room of <paramref name="kind"/>.</summary>
        public static string PromptOf(AdventureRoomKind kind) =>
            kind switch
            {
                AdventureRoomKind.Elite => StrongPrompt,
                AdventureRoomKind.Boss => MasterPrompt,
                AdventureRoomKind.Trial => TrialPrompt,
                AdventureRoomKind.ShadyChest => MimicPrompt,
                _ => ExplorationRoom.BattlePrompt,
            };

        private void Awake()
        {
            if (LightMaterial != null)
                for (int i = 0; i < shapes.Length; i++)
                {
                    shapes[i] = new Material(LightMaterial);
                    shapes[i].SetFloat("_Shape", i);
                }
            Clear();
        }

        private void OnDestroy()
        {
            foreach (var material in shapes)
                if (material != null)
                    Destroy(material);
        }

        /// <summary>Takes every moment's part away at once, as before any played.</summary>
        public void Clear()
        {
            StopAllCoroutines();
            playing = null;
            SetShown(Mark, false);
            if (Band != null)
                Band.gameObject.SetActive(false);
            foreach (var band in WipeBands)
                if (band != null)
                    band.gameObject.SetActive(false);
            SetAlpha(Dim, 0f);
            foreach (var light in lights)
                if (light != null)
                    light.gameObject.SetActive(false);
            if (Reveal != null)
                Reveal.HideNow();
            if (View != null && View.ChestSprite != null)
                View.ChestSprite.rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// The party meets the monsters of the room it is in, of <paramref name="kind"/>. Into the
        /// battle (<paramref name="intoBattle"/>, the adventure's), the wipe stays closed over the
        /// map for the battle scene to open; otherwise (the lab's) it opens on the map again.
        /// </summary>
        public void PlayEncounter(AdventureRoomKind kind, bool intoBattle = false) =>
            Play(Encounter(kind, intoBattle));

        /// <summary>The chest in the party's room opens on <paramref name="item"/> (null: nothing found).</summary>
        public void PlayChest(RewardItem item) =>
            PlayChest(item != null ? new[] { item } : Array.Empty<RewardItem>());

        /// <summary>
        /// The chest opens on <paramref name="items"/> (the best first, which sets its light) and
        /// shows them all together.
        /// </summary>
        public void PlayChest(IReadOnlyList<RewardItem> items) =>
            Play(Chest(items ?? Array.Empty<RewardItem>()));

        private void Play(IEnumerator moment)
        {
            Clear();
            playing = StartCoroutine(Run(moment));
        }

        private IEnumerator Run(IEnumerator moment)
        {
            yield return moment;
            playing = null;
        }

        // --- Monsters ----------------------------------------------------------------------------

        private IEnumerator Encounter(AdventureRoomKind kind, bool intoBattle)
        {
            var (line, words, mark) = ColorsOf(kind);
            bool strong =
                kind == AdventureRoomKind.Elite
                || kind == AdventureRoomKind.Trial
                || kind == AdventureRoomKind.ShadyChest;
            bool master = kind == AdventureRoomKind.Boss;
            if (master)
            {
                // The deepest room darkens before its master shows itself.
                yield return FadeTo(Dim, new Color(0.06f, 0.02f, 0.1f), 0.45f, 0.4f);
                yield return Wait(0.3f);
            }
            var head = PartyTop();
            StartCoroutine(PopMark(head, mark, strong || master ? 1.25f : 1f));
            StartCoroutine(PartyStartle());
            StartCoroutine(
                ShakeMap(
                    strong ? 10f
                        : master ? 14f
                        : 5f,
                    0.3f
                )
            );
            Light(GlowShape, head, 220f, mark, 0.5f, 0.55f);
            yield return Wait(0.4f);
            yield return ShowBand(PromptOf(kind), line, words, 0.75f);
            // Into the battle: the screen closes in bands from both sides.
            yield return Wipe(close: true, master ? new Color(0.05f, 0.02f, 0.08f) : Color.black);
            // In the adventure the battle opens from here; the lab opens the map again.
            if (intoBattle)
                yield break;
            yield return Wait(WipeHold);
            SetShown(Mark, false);
            SetAlpha(Dim, 0f);
            yield return Wipe(close: false, Color.black);
        }

        /// <summary>The top middle of the party, in the overlay's space (world), a little above the heads.</summary>
        private Vector3 PartyTop()
        {
            if (View == null || View.Party.Length == 0)
                return transform.position;
            var corners = new Vector3[4];
            var middle = Vector3.zero;
            float top = float.NegativeInfinity;
            foreach (var body in View.Party)
            {
                body.GetWorldCorners(corners);
                middle += (corners[0] + corners[2]) * 0.5f / View.Party.Length;
                top = Mathf.Max(top, corners[1].y);
            }
            return new Vector3(middle.x, top + 30f * transform.lossyScale.y, middle.z);
        }

        /// <summary>The "！" springs up over the party from nothing and bobs by a dot.</summary>
        private IEnumerator PopMark(Vector3 at, Color color, float size)
        {
            if (Mark == null)
                yield break;
            Mark.color = color;
            Mark.transform.position = at;
            var home = Mark.rectTransform.anchoredPosition;
            SetShown(Mark, true);
            for (float t = 0f; t < 0.26f; t += Time.deltaTime)
            {
                float k = t / 0.26f;
                Mark.rectTransform.localScale = Vector3.one * (size * RewardReveal.SpringOut(k));
                Mark.rectTransform.anchoredPosition = home + Vector2.up * (24f * (1f - k));
                yield return null;
            }
            Mark.rectTransform.localScale = Vector3.one * size;
            for (float t = 0f; Mark.gameObject.activeSelf && t < 3f; t += Time.deltaTime)
            {
                Mark.rectTransform.anchoredPosition =
                    home + Vector2.up * (Mathf.Repeat(t, 0.5f) < 0.25f ? 4f : 0f);
                yield return null;
            }
        }

        /// <summary>The four jump a dot or two at once, startled, and land.</summary>
        private IEnumerator PartyStartle()
        {
            if (View == null)
                yield break;
            var homes = new Vector2[View.Party.Length];
            for (int i = 0; i < homes.Length; i++)
                homes[i] = View.Party[i].anchoredPosition;
            const float time = 0.18f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float u = t / time;
                float lift = Mathf.Round(4f * 2f * u * (1f - u)) * 4f;
                for (int i = 0; i < homes.Length; i++)
                    View.Party[i].anchoredPosition = homes[i] + Vector2.up * lift;
                yield return null;
            }
            for (int i = 0; i < homes.Length; i++)
                View.Party[i].anchoredPosition = homes[i];
        }

        /// <summary>The map trembles side to side, fading out, in whole dots.</summary>
        private IEnumerator ShakeMap(float distance, float seconds)
        {
            if (View == null || View.Map == null)
                yield break;
            var map = View.Map;
            var home = map.anchoredPosition;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float side = Mathf.Repeat(t * 28f, 2f) < 1f ? 1f : -1f;
                float fade = 1f - t / seconds;
                map.anchoredPosition =
                    home + Vector2.right * Mathf.Round(side * distance * fade / 4f) * 4f;
                yield return null;
            }
            map.anchoredPosition = home;
        }

        /// <summary>The band opens from the middle with its words, holds, and fades.</summary>
        private IEnumerator ShowBand(string text, Color line, Color words, float hold)
        {
            if (Band == null)
                yield break;
            Band.SetText(text);
            if (Band.Label != null)
                Band.Label.color = words;
            foreach (var graphic in BandLines)
                graphic.color = line;
            Band.gameObject.SetActive(true);
            const float fade = 0.12f;
            for (float t = 0f; t < hold + fade * 2f; t += Time.deltaTime)
            {
                float alpha =
                    t < fade ? t / fade
                    : t < fade + hold ? 1f
                    : 1f - (t - fade - hold) / fade;
                if (BandGroup != null)
                    BandGroup.alpha = alpha;
                float open = Mathf.Clamp01(t / fade);
                float width = Mathf.Lerp(0.6f, 1f, 1f - (1f - open) * (1f - open));
                foreach (var graphic in BandLines)
                    graphic.rectTransform.localScale = new Vector3(width, 1f, 1f);
                yield return null;
            }
            Band.gameObject.SetActive(false);
        }

        /// <summary>
        /// The bands slide in from the left and the right by turns, one after another, until
        /// they cover the screen (<paramref name="close"/>), or slide back out.
        /// </summary>
        private IEnumerator Wipe(bool close, Color color)
        {
            if (WipeBands.Length == 0)
                yield break;
            float width = ((RectTransform)WipeBands[0].parent).rect.width;
            float time = close ? WipeClose : WipeOpen;
            float total = time + WipeStagger * (WipeBands.Length - 1);
            foreach (var band in WipeBands)
            {
                band.gameObject.SetActive(true);
                if (band.TryGetComponent(out Graphic graphic))
                    graphic.color = color;
            }
            for (float t = 0f; t <= total; t += Time.deltaTime)
            {
                PlaceWipe(width, close, t, time);
                yield return null;
            }
            PlaceWipe(width, close, total + 1f, time);
            if (!close)
                foreach (var band in WipeBands)
                    band.gameObject.SetActive(false);
        }

        private void PlaceWipe(float width, bool close, float t, float time)
        {
            for (int i = 0; i < WipeBands.Length; i++)
            {
                float k = Mathf.Clamp01((t - i * WipeStagger) / time);
                // In fast and settling; out slow and leaving fast.
                float covered = close ? 1f - (1f - k) * (1f - k) * (1f - k) : 1f - k * k;
                float side = i % 2 == 0 ? -1f : 1f;
                WipeBands[i].anchoredPosition = new Vector2(
                    side * width * (1f - covered),
                    WipeBands[i].anchoredPosition.y
                );
            }
        }

        // --- The chest ---------------------------------------------------------------------------

        // 品質の段階。何も見つからなかった宝箱は -1。
        private static int Grade(RewardItem item) =>
            item == null ? -1 : Quality.Grade(item.Quality);

        /// <summary>How long a chest of <paramref name="item"/> trembles before it opens: longer for a better one.</summary>
        public static float ChestWait(RewardItem item) =>
            Grade(item) switch
            {
                3 => 1.1f,
                2 => 0.75f,
                1 => 0.5f,
                _ => 0.35f,
            };

        private IEnumerator Chest(IReadOnlyList<RewardItem> items)
        {
            if (View == null)
                yield break;
            var item = items.Count > 0 ? items[0] : null;
            View.ShowChestHere(open: false);
            int grade = Grade(item);
            var color =
                item != null ? QualityPalette.Of(item.Quality) : new Color(0.7f, 0.7f, 0.66f);
            var sprite = View.ChestSprite != null ? View.ChestSprite.rectTransform : null;
            var at = sprite != null ? sprite.position : View.transform.position;
            float wait = ChestWait(item);

            // The wind-up: it trembles harder and harder as its light leaks out.
            if (grade >= 2)
                StartCoroutine(
                    FadeTo(
                        Dim,
                        new Color(0.02f, 0.02f, 0.05f),
                        grade == 3 ? 0.55f : 0.32f,
                        wait * 0.7f
                    )
                );
            var glow = Light(
                GlowShape,
                at,
                180f + 40f * Mathf.Max(0, grade),
                color,
                0f,
                wait + 0.2f,
                grow: true
            );
            if (grade >= 2)
                Light(
                    RaysShape,
                    at,
                    260f + 60f * grade,
                    color,
                    0f,
                    wait + 0.15f,
                    grow: true,
                    spin: 30f
                );
            for (float t = 0f; t < wait; t += Time.deltaTime)
            {
                float k = t / wait;
                float dot = (Mathf.Repeat(t, 0.08f) < 0.04f ? 1f : -1f) * (k < 0.5f ? 4f : 8f);
                if (sprite != null)
                    sprite.anchoredPosition = new Vector2(dot, 0f);
                yield return null;
            }
            if (sprite != null)
                sprite.anchoredPosition = Vector2.zero;

            // The burst: it flies open, white-hot, then the quality's light and what was in it.
            View.SetChestOpen(true);
            if (glow != null)
                glow.gameObject.SetActive(false);
            if (grade < 0)
            {
                Light(GlowShape, at, 200f, color, 0.35f, 0.4f);
                yield return Wait(0.25f);
                if (Reveal != null)
                    Reveal.Show(null);
                yield return Wait(RewardReveal.OpenTime + 0.4f);
                yield break;
            }
            Light(GlowShape, at, 360f + 120f * grade, Color.white, 0.9f, 0.45f);
            Light(GlowShape, at, 300f + 80f * grade, color, 0.7f, 1.2f + 0.2f * grade);
            if (grade >= 1)
                Light(
                    RaysShape,
                    at,
                    480f + 160f * grade,
                    color,
                    0.85f,
                    1.0f + 0.3f * grade,
                    spin: 40f
                );
            Sparkles(at, color, 5 + 5 * grade, 260f + 60f * grade);
            // S floods the screen with its light from the chest, added over the map.
            if (grade >= 3)
                Light(GlowShape, at, 2200f, Color.Lerp(Color.white, color, 0.5f), 0.5f, 0.6f);
            if (Reveal != null)
                Reveal.ShowAll(items);
            yield return Wait(0.6f + RewardReveal.Stagger * (items.Count - 1));
            if (grade >= 2)
                StartCoroutine(FadeTo(Dim, new Color(0.02f, 0.02f, 0.05f), 0f, 0.6f));
            yield return Wait(0.8f);
        }

        /// <summary>Sparkles thrown up from <paramref name="at"/> that fall back and fade.</summary>
        private void Sparkles(Vector3 at, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (90f + UnityEngine.Random.Range(-60f, 60f)) * Mathf.Deg2Rad;
                var velocity =
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                    * (speed * UnityEngine.Random.Range(0.6f, 1.2f));
                StartCoroutine(
                    Fly(
                        at,
                        velocity,
                        color,
                        UnityEngine.Random.Range(26f, 44f),
                        UnityEngine.Random.Range(0.7f, 1.2f)
                    )
                );
            }
        }

        private IEnumerator Fly(Vector3 at, Vector2 velocity, Color color, float size, float life)
        {
            var light = Take(SparkleShape, size);
            if (light == null)
                yield break;
            float scale = transform.lossyScale.x;
            var position = at;
            for (float t = 0f; t < life; t += Time.deltaTime)
            {
                velocity += Vector2.down * (520f * Time.deltaTime);
                velocity *= Mathf.Exp(-1.5f * Time.deltaTime);
                position += (Vector3)(velocity * (Time.deltaTime * scale));
                light.transform.position = position;
                light.rectTransform.localEulerAngles = new Vector3(0f, 0f, t * 180f);
                float k = t / life;
                var tint = Color.Lerp(Color.white, color, k);
                tint.a = k < 0.1f ? k / 0.1f : 1f - (k - 0.1f) / 0.9f;
                light.color = tint;
                yield return null;
            }
            light.gameObject.SetActive(false);
        }

        // --- Light and the screen ----------------------------------------------------------------

        /// <summary>
        /// A light of <paramref name="shape"/> at <paramref name="at"/> (world) that lives
        /// <paramref name="seconds"/>: it flares to <paramref name="peak"/> and fades, or, when it
        /// <paramref name="grow"/>s, swells from nothing to its size and brightness until it ends.
        /// </summary>
        private RawImage Light(
            int shape,
            Vector3 at,
            float size,
            Color color,
            float peak,
            float seconds,
            bool grow = false,
            float spin = 0f
        )
        {
            var light = Take(shape, size);
            if (light == null)
                return null;
            light.transform.position = at;
            StartCoroutine(Shine(light, color, peak, seconds, grow, spin));
            return light;
        }

        private IEnumerator Shine(
            RawImage light,
            Color color,
            float peak,
            float seconds,
            bool grow,
            float spin
        )
        {
            for (float t = 0f; t < seconds && light.gameObject.activeSelf; t += Time.deltaTime)
            {
                float k = t / seconds;
                float alpha;
                float scale;
                if (grow)
                {
                    alpha = 0.15f + 0.75f * k * k;
                    scale = 0.4f + 0.6f * k;
                }
                else
                {
                    alpha = peak * (k < 0.12f ? k / 0.12f : 1f - (k - 0.12f) / 0.88f);
                    scale = 0.7f + 0.4f * (1f - (1f - k) * (1f - k));
                }
                light.color = new Color(color.r, color.g, color.b, alpha);
                light.rectTransform.localScale = Vector3.one * scale;
                light.rectTransform.localEulerAngles = new Vector3(0f, 0f, -t * spin);
                yield return null;
            }
            light.gameObject.SetActive(false);
        }

        /// <summary>A free light on the light layer, of <paramref name="shape"/> and <paramref name="size"/>.</summary>
        private RawImage Take(int shape, float size)
        {
            if (LightLayer == null || shapes[shape] == null)
                return null;
            var light = lights.Find(found => found != null && !found.gameObject.activeSelf);
            if (light == null)
            {
                var rect = new GameObject(
                    "Light",
                    typeof(RectTransform)
                ).GetComponent<RectTransform>();
                rect.SetParent(LightLayer, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
                light = rect.gameObject.AddComponent<RawImage>();
                light.raycastTarget = false;
                lights.Add(light);
            }
            light.material = shapes[shape];
            light.rectTransform.sizeDelta = Vector2.one * size;
            light.rectTransform.localScale = Vector3.one;
            light.rectTransform.localEulerAngles = Vector3.zero;
            light.color = Color.clear;
            light.gameObject.SetActive(true);
            light.transform.SetAsLastSibling();
            return light;
        }

        private IEnumerator FadeTo(Image veil, Color color, float alpha, float seconds)
        {
            if (veil == null)
                yield break;
            float from = veil.color.a;
            veil.enabled = true;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                veil.color = new Color(
                    color.r,
                    color.g,
                    color.b,
                    Mathf.Lerp(from, alpha, t / seconds)
                );
                yield return null;
            }
            SetAlpha(veil, alpha, color);
        }

        private static void SetAlpha(Image veil, float alpha, Color? color = null)
        {
            if (veil == null)
                return;
            var c = color ?? veil.color;
            veil.color = new Color(c.r, c.g, c.b, alpha);
            veil.enabled = alpha > 0.001f;
        }

        private static void SetShown(Component part, bool shown)
        {
            if (part != null && part.gameObject.activeSelf != shown)
                part.gameObject.SetActive(shown);
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
                yield return null;
        }
    }
}
