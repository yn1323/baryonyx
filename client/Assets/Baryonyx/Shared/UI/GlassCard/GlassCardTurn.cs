using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// Turns a glass card over to what it holds next, when the player gives a slot something else
    /// (doc/features/screens/loadout.md#付け替えたときの演出). <see cref="Turn"/>, called before the
    /// card shows its new contents, copies the plate as it is; the copy turns about the card's
    /// level axis, narrowing in height to an edge and darkening as it leans away, then the plate
    /// with the new contents opens from the edge. Face on again, thin lines of light spring out
    /// from its top and bottom edges and fade, each with its own place, length, lean, width,
    /// brightness, start and speed, so no two turns look alike. The lines are drawn on a layer over the panel,
    /// so the cards around do not cover them, and are made in code (<see cref="SoftLight"/>).
    /// A card lying face down or turning up (<see cref="GlassCardBack"/>) is left alone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlassCardTurn : MonoBehaviour
    {
        // 古い面が閉じる時間と、新しい面が開く時間（秒）。
        public const float HalfSeconds = 0.14f;

        // 開き終わってから、上下の辺の線がはじけて消えるまで（秒）。一番遅い線の出だしと長さを足した時間。
        public const float LineSeconds = MaxDelay + LongestLife;

        // 真横を向いたときの陰の濃さ。
        public const float MaxShade = 0.45f;

        // 上と下の辺のそれぞれに出す線の数（回すたびに、この間で決める）。
        public const int FewestLines = 8;
        public const int MostLines = 12;

        // 線の長さ（カードの高さに対する割合）。短い線が多く、長い線が少し混ざる。
        private const float ShortLine = 0.06f;
        private const float LongLine = 0.6f;
        private const float LongLineRarity = 1.8f;

        // 辺の端に近い線ほど外へ開く角度と、1本ごとのずれ（度）。
        private const float Spread = 22f;
        private const float Lean = 12f;

        // 辺からのすき間、芯と光の太さ（画面の単位）、光の濃さ。
        private const float NearGap = 2f;
        private const float FarGap = 10f;
        private const float ThinCore = 1.8f;
        private const float ThickCore = 3.6f;
        private const float HaloScale = 4f;
        private const float HaloAlpha = 0.45f;
        private const float DimLine = 0.55f;

        // 線の出だしのばらつきと、1本が伸びて消えるまでの長さ（秒）。
        private const float MaxDelay = 0.09f;
        private const float ShortestLife = 0.2f;
        private const float LongestLife = 0.36f;
        private const float GrowPart = 0.4f;

        private sealed class Spark
        {
            public RectTransform Halo;
            public RectTransform Core;
            public RawImage HaloImage;
            public RawImage CoreImage;

            // 辺の上の根元（線を描く面の座標）と、外へ向かう向き。
            public Vector2 Root;
            public Vector2 Out;
            public float Angle;
            public float Length;
            public float Gap;
            public float Width;
            public float Brightness;
            public float Delay;
            public float Life;
            public float Drift;
        }

        private GlassCard card;
        private RectTransform layer;
        private Color tint = Color.white;
        private GameObject ghost;
        private Image shade;
        private float elapsed = float.PositiveInfinity;
        private int turns;
        private readonly List<Spark> sparks = new();

        // 上の辺と下の辺の高さ、左右の端（線を描く面の座標）。
        private float top;
        private float bottom;
        private float left;
        private float right;

        /// <summary>Whether the card is turning or its lines are still showing.</summary>
        public bool Turning => elapsed < HalfSeconds * 2f + LineSeconds;

        /// <summary>
        /// Starts turning <paramref name="card"/> from what it shows now to what it shows next,
        /// with lines of light tinted <paramref name="tint"/> on <paramref name="layer"/>. Call it
        /// before the card shows the new contents. Does nothing while stopped, or for a card not
        /// on screen or lying face down.
        /// </summary>
        public static void Turn(GlassCard card, RectTransform layer, Color tint)
        {
            if (
                !Application.isPlaying
                || card == null
                || card.Body == null
                || !card.isActiveAndEnabled
            )
                return;
            if (
                card.TryGetComponent(out GlassCardBack back)
                && (back.FaceDown || back.Flipping || back.Leaving)
            )
                return;
            if (!card.TryGetComponent(out GlassCardTurn turn))
                turn = card.gameObject.AddComponent<GlassCardTurn>();
            turn.Begin(card, layer, tint);
        }

        /// <summary>The light of the lines: white with a little of <paramref name="art"/>'s colour.</summary>
        public static Color TintOf(Color art) => Color.Lerp(Color.white, art, 0.35f);

        private void Begin(GlassCard target, RectTransform lines, Color color)
        {
            Settle();
            card = target;
            layer = lines;
            tint = color;
            turns++;
            var body = card.Body;
            ghost = Instantiate(body.gameObject, body.parent);
            ghost.name = "TurnGhost";
            ghost.transform.SetSiblingIndex(body.GetSiblingIndex() + 1);
            var group = ghost.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            shade = MakeShade(body);
            elapsed = 0f;
            enabled = true;
            Scale(1f, true);
        }

        private void Update()
        {
            if (!Turning)
            {
                enabled = false;
                return;
            }
            // ゲームの時間で進める（案内人の画面は時間を止めない。録画でも実際の速さで写る）。
            elapsed += Time.deltaTime;
            if (elapsed < HalfSeconds)
            {
                // 古い面が上の辺から奥へ倒れ、だんだん速く閉じる。
                float u = elapsed / HalfSeconds;
                Scale(Mathf.Cos(u * u * Mathf.PI / 2f), true);
                return;
            }
            if (elapsed < HalfSeconds * 2f)
            {
                // 新しい面が手前へ起き上がり、ゆっくり収まる。
                if (ghost != null)
                    Destroy(ghost);
                ghost = null;
                float u = (elapsed - HalfSeconds) / HalfSeconds;
                float rest = 1f - u;
                Scale(Mathf.Cos(rest * rest * Mathf.PI / 2f), false);
                return;
            }
            if (shade != null)
            {
                Scale(1f, false);
                Destroy(shade.gameObject);
                shade = null;
                MakeSparks();
            }
            Animate(elapsed - HalfSeconds * 2f);
            if (!Turning)
                Settle();
        }

        private void OnDisable() => Settle();

        // 板・影・選んだときの縁を、カードの中心を軸に高さだけ縮める。閉じる間は古い面の写しを、開く間は板を見せる。
        private void Scale(float height, bool closing)
        {
            var scale = GlassCardBack.FlipScale(height, 1f, true);
            card.Body.localScale = closing ? GlassCardBack.FlipScale(0f, 1f, true) : scale;
            if (ghost != null)
                ghost.transform.localScale = scale;
            if (card.Shadow != null)
                card.Shadow.localScale = scale;
            if (card.Selection != null)
                card.Selection.transform.localScale = scale;
            if (shade != null)
            {
                shade.rectTransform.localPosition = card.Body.localPosition;
                shade.rectTransform.localScale = scale;
                shade.color = new Color(0f, 0f, 0f, MaxShade * (1f - height));
            }
        }

        // 板の形の陰。奥へ倒れるほど濃くなる。
        private Image MakeShade(RectTransform body)
        {
            var fill = body.Find("Fill") != null ? body.Find("Fill").GetComponent<Image>() : null;
            var rect = new GameObject(
                "TurnShade",
                typeof(RectTransform)
            ).GetComponent<RectTransform>();
            rect.SetParent(body.parent, false);
            rect.SetSiblingIndex(body.GetSiblingIndex() + 2);
            rect.anchorMin = body.anchorMin;
            rect.anchorMax = body.anchorMax;
            rect.pivot = body.pivot;
            rect.sizeDelta = body.sizeDelta;
            rect.anchoredPosition = body.anchoredPosition;
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            if (fill != null)
            {
                image.sprite = fill.sprite;
                image.type = fill.type;
                image.pixelsPerUnitMultiplier = fill.pixelsPerUnitMultiplier;
            }
            image.color = Color.clear;
            return image;
        }

        // 開き終えた板の上の辺と下の辺に、線を8〜12本ずつ置く。辺を本数で区切り、区切りの中の
        // どこかに1本ずつ置くので、固まりすぎずに散らばる。長さ・傾き・太さ・明るさ・出だし・速さは1本ずつ変える。
        private void MakeSparks()
        {
            if (layer == null)
                return;
            layer.SetAsLastSibling();
            var corners = new Vector3[4];
            card.Body.GetWorldCorners(corners);
            var low = layer.InverseTransformPoint(corners[0]);
            var high = layer.InverseTransformPoint(corners[2]);
            left = Mathf.Min(low.x, high.x);
            right = Mathf.Max(low.x, high.x);
            bottom = Mathf.Min(low.y, high.y);
            top = Mathf.Max(low.y, high.y);
            float height = top - bottom;
            var random = new System.Random(
                turns * 7919 + card.transform.GetSiblingIndex() * 104729 + Time.frameCount
            );
            float Next() => (float)random.NextDouble();
            foreach (bool upper in new[] { true, false })
            {
                int count = random.Next(FewestLines, MostLines + 1);
                for (int i = 0; i < count; i++)
                {
                    float place = Mathf.Lerp(0.03f, 0.97f, (i + Next()) / count);
                    // 端に近いほど外へ開く。下の辺は上の辺を上下に返した向き。
                    float angle = (place - 0.5f) * 2f * Spread + (Next() * 2f - 1f) * Lean;
                    var outward = Quaternion.Euler(0f, 0f, -angle) * Vector3.up;
                    if (!upper)
                        outward = new Vector3(outward.x, -outward.y, 0f);
                    float length =
                        height * Mathf.Lerp(ShortLine, LongLine, Mathf.Pow(Next(), LongLineRarity));
                    sparks.Add(
                        new Spark
                        {
                            Root = new Vector2(
                                Mathf.Lerp(left, right, place),
                                upper ? top : bottom
                            ),
                            Out = outward,
                            Angle = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg - 90f,
                            Length = length,
                            Gap = Mathf.Lerp(NearGap, FarGap, Next()),
                            Width = Mathf.Lerp(ThinCore, ThickCore, Next()),
                            Brightness = Mathf.Lerp(DimLine, 1f, Next()),
                            Delay = Next() * MaxDelay,
                            Life = Mathf.Lerp(ShortestLife, LongestLife, Next()),
                            Drift = length * Mathf.Lerp(0.2f, 0.7f, Next()),
                        }
                    );
                }
            }
            foreach (var spark in sparks)
            {
                (spark.Halo, spark.HaloImage) = Line("Halo");
                (spark.Core, spark.CoreImage) = Line("Core");
                spark.Halo.localRotation = spark.Core.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    spark.Angle
                );
            }
        }

        private (RectTransform, RawImage) Line(string name)
        {
            var rect = new GameObject(
                "TurnLine" + name,
                typeof(RectTransform)
            ).GetComponent<RectTransform>();
            rect.SetParent(layer, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = SoftLight.Line;
            image.raycastTarget = false;
            image.color = Color.clear;
            return (rect, image);
        }

        // 線は辺から外へ伸び、伸びきると根元から追いかけて縮み、外へ流れながら消える。
        private void Animate(float t)
        {
            foreach (var spark in sparks)
            {
                float p = Mathf.Clamp01((t - spark.Delay) / spark.Life);
                float drift = spark.Drift * p;
                float outer =
                    spark.Gap
                    + spark.Length * (1f - Mathf.Pow(1f - Mathf.Clamp01(p / GrowPart), 2f))
                    + drift;
                float inner =
                    spark.Gap + spark.Length * Mathf.SmoothStep(0f, 1f, (p - 0.3f) / 0.7f) + drift;
                float alpha =
                    p <= 0f
                        ? 0f
                        : spark.Brightness * (1f - Mathf.SmoothStep(0f, 1f, (p - 0.55f) / 0.45f));
                float length = Mathf.Max(0f, outer - inner);
                var middle = spark.Root + spark.Out * ((outer + inner) / 2f);
                Place(
                    spark.Halo,
                    spark.HaloImage,
                    middle,
                    spark.Width * HaloScale,
                    length * 1.15f,
                    alpha * HaloAlpha
                );
                Place(spark.Core, spark.CoreImage, middle, spark.Width, length, alpha);
            }
        }

        private void Place(
            RectTransform rect,
            RawImage image,
            Vector2 middle,
            float width,
            float height,
            float alpha
        )
        {
            rect.anchoredPosition = middle - LayerCenterOffset();
            rect.sizeDelta = new Vector2(width, height);
            image.color = new Color(tint.r, tint.g, tint.b, alpha);
        }

        // 面の中心を原点にした座標（InverseTransformPoint）を、中央に固定した線の位置に直す差。
        private Vector2 LayerCenterOffset() => layer.rect.center;

        // 回っている途中でも、すぐに収まった姿（新しい面、線なし）にする。回していなければ何もしない。
        private void Settle()
        {
            if (!Turning && ghost == null && shade == null && sparks.Count == 0)
                return;
            elapsed = float.PositiveInfinity;
            if (ghost != null)
                Destroy(ghost);
            ghost = null;
            if (shade != null)
                Destroy(shade.gameObject);
            shade = null;
            foreach (var spark in sparks)
            {
                if (spark.Halo != null)
                    Destroy(spark.Halo.gameObject);
                if (spark.Core != null)
                    Destroy(spark.Core.gameObject);
            }
            sparks.Clear();
            if (card == null)
                return;
            var scale = Vector3.one;
            card.Body.localScale = scale;
            if (card.Shadow != null)
                card.Shadow.localScale = scale;
            if (card.Selection != null)
                card.Selection.transform.localScale = scale;
        }
    }
}
