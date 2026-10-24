using System.Collections.Generic;
using Baryonyx.Combat.Presentation;
using Baryonyx.Library;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Temple
{
    /// <summary>
    /// What the summoning presentations once and ten times share: the lights' colours, the
    /// curves of time, how a layer of the Temple Summon Glass shader is set, and copying the
    /// layers' materials so each presentation sets its own light and crack pattern.
    /// </summary>
    public static class TempleSummonFx
    {
        public static readonly Color Moonlight = new(0.62f, 0.72f, 1f);
        public static readonly Color WhiteLight = new(1f, 0.96f, 0.9f);
        public static readonly Color GoldLight = new(1f, 0.66f, 0.12f);
        public static readonly Color Night = new(0.02f, 0.025f, 0.07f);

        /// <summary>The light a result gives: rainbow for a character or ☆5, gold for ☆4 or quality S.</summary>
        public static TempleSummonTier TierOf(SummonResult result) =>
            result.Entry.Kind == LibraryKind.Character || result.Entry.Stars >= 5
                ? TempleSummonTier.Rainbow
            : result.Entry.Stars >= 4 || result.Quality == Inventory.ItemQuality.S
                ? TempleSummonTier.Gold
            : TempleSummonTier.White;

        /// <summary>The best light among <paramref name="results"/>.</summary>
        public static TempleSummonTier BestOf(IEnumerable<SummonResult> results)
        {
            var best = TempleSummonTier.White;
            foreach (var result in results)
                if (TierOf(result) > best)
                    best = TierOf(result);
            return best;
        }

        public static Color ColorOf(TempleSummonTier value) =>
            value switch
            {
                TempleSummonTier.Gold => GoldLight,
                TempleSummonTier.Rainbow => Color.white,
                _ => WhiteLight,
            };

        public static float Ramp(float t, float from, float to) =>
            Mathf.Clamp01((t - from) / Mathf.Max(1e-4f, to - from));

        public static float Smooth(float x) => x * x * (3f - 2f * x);

        public static float EaseIn(float x) => x * x;

        public static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

        public static float Pulse(float t, float at, float width) =>
            Mathf.Exp(-((t - at) * (t - at)) / (width * width));

        // 揺れ：時刻から決まる -1〜1 のなめらかな値。
        public static float Noise(float x, float seed) => Mathf.PerlinNoise(x, seed) * 2f - 1f;

        /// <summary>
        /// Shows or hides one layer and sets its light (the vertex colour, alpha as strength) and
        /// its values (seed, progress, the layer's own value, brightness; see the shader).
        /// </summary>
        public static void Show(
            BattleVfxImage image,
            bool on,
            Color light,
            float alpha,
            float seed,
            float progress,
            float value,
            float brightness
        )
        {
            if (image == null)
                return;
            image.gameObject.SetActive(on);
            if (!on)
                return;
            image.color = new Color(light.r, light.g, light.b, alpha);
            image.Shape = new Vector4(seed, progress, value, brightness);
        }

        /// <summary>
        /// Gives each of <paramref name="graphics"/> its own copy of its material, so the light
        /// and the crack pattern set on it belong to this presentation alone.
        /// </summary>
        public static Material[] OwnMaterials(Transform root, IEnumerable<Graphic> graphics)
        {
            var owned = new List<Material>();
            foreach (var graphic in graphics)
            {
                if (graphic == null || graphic.material == null)
                    continue;
                var copy = new Material(graphic.material) { hideFlags = HideFlags.DontSave };
                graphic.material = copy;
                owned.Add(copy);
            }
            BattleVfxImage.EnableShapeChannel(root);
            return owned.ToArray();
        }

        public static void DestroyMaterials(IEnumerable<Material> materials)
        {
            foreach (var material in materials)
            {
                if (material == null)
                    continue;
                if (Application.isPlaying)
                    Object.Destroy(material);
                else
                    Object.DestroyImmediate(material);
            }
        }
    }
}
