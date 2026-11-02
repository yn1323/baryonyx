using System;
using System.Collections.Generic;
using Baryonyx.Vfx.Hd2d;
using Baryonyx.Vfx.Hd2d.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Builds the runes-gained effect (<see cref="RuneGainEffect"/>) over a screen: the layer the
    /// runes, sparkles and haze fly on, the light over the balance icon, and the gained amount
    /// under the balance. The home and the guide screens (the trading house's sale) use it.
    /// </summary>
    public static class RuneGainAssets
    {
        public const string IconRunePath =
            "Assets/Baryonyx/Shared/Art/GameResources/IconRune.aseprite";

        // Light colours taken from the rune palette (highlight and the brightest facets).
        private static readonly Color RuneSpark = new(0.933f, 0.988f, 1f, 1f);
        private static readonly Color RuneSparkFade = new(0.32f, 0.706f, 0.933f, 0f);
        private static readonly Color RuneGlowColor = new(0.588f, 0.878f, 0.98f, 0f);

        // The flying rune is the 24x24 icon at 4x.
        private const float RuneIconSize = 24f * 4f;

        /// <summary>
        /// Puts the effect on a layer over <paramref name="root"/>, flying into
        /// <paramref name="runeIcon"/> (<paramref name="iconSize"/> across) beside
        /// <paramref name="runesLabel"/>. The gained amount is
        /// written right-aligned in <paramref name="gainParent"/>, its top-right corner
        /// <paramref name="gainOffset"/> from the parent's.
        /// </summary>
        public static RuneGainEffect Build(
            RectTransform root,
            RectTransform runeIcon,
            TMP_Text runesLabel,
            float iconSize,
            RectTransform gainParent,
            Vector2 gainOffset,
            Vector2 gainSize,
            float gainFontSize
        )
        {
            var layer = Rect("RuneEffect", root);
            Stretch(layer);
            // The flying runes, sparkles and haze move every frame: on a Canvas of their own, so
            // the rest of the screen is not rebuilt with them (2026-10-29, the gain felt heavy). It
            // draws in its place among the screen's parts and takes no taps.
            layer.gameObject.AddComponent<Canvas>();
            var effect = layer.gameObject.AddComponent<RuneGainEffect>();
            effect.Layer = layer;
            effect.RuneTarget = runeIcon;
            effect.RunesLabel = runesLabel;

            // A computed light over the balance icon, under the runes and the sparkles: a white
            // core and a soft halo, added to what is behind.
            var glow = Rect("RuneGlow", layer);
            Place(glow, Vector2.zero, Vector2.one * (iconSize * 2.2f));
            effect.RuneGlow = AddImage(glow, RuneGlowColor, false);
            effect.RuneGlow.material = AssetDatabase.LoadAssetAtPath<Material>(
                Hd2dAssets.GlowCoreMaterialPath
            );
            glow.gameObject.SetActive(false);

            // A haze of soft light drifts behind the sparkles and the runes: a puff where the
            // runes burst out, a mist along their paths and a bloom where they land. It uses
            // the same sources as the sparkles, with a computed puff whose rim drifts as its
            // only shape (128 or 192 px across).
            var haze = Emitter("RuneHaze", layer);
            haze.GlintMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                Hd2dAssets.PuffMaterialPath
            );
            haze.GlintDots = 64;
            haze.GlintSpin = 360f;
            haze.Sources = new List<Hd2dEmberSource>
            {
                HazeSource(
                    "Arrival",
                    12,
                    new Vector2(30f, 30f),
                    new Vector2(20f, 70f),
                    new Vector2(0.4f, 0.6f),
                    200f,
                    2,
                    0.45f
                ),
                HazeSource(
                    "Final",
                    6,
                    new Vector2(30f, 30f),
                    new Vector2(40f, 110f),
                    new Vector2(0.6f, 0.9f),
                    260f,
                    3,
                    0.5f
                ),
                HazeSource(
                    "Burst",
                    8,
                    new Vector2(60f, 60f),
                    new Vector2(60f, 180f),
                    new Vector2(0.5f, 0.9f),
                    360f,
                    3,
                    0.4f
                ),
                HazeSource(
                    "Trail",
                    32,
                    new Vector2(24f, 24f),
                    new Vector2(0f, 20f),
                    new Vector2(0.35f, 0.6f),
                    360f,
                    2,
                    0.32f
                ),
            };
            effect.RuneHaze = haze;

            // Sparkles pop from the balance icon as each rune lands, with a bigger burst for
            // the last one; they lean left, away from the balance digits. The runes also burst
            // out with a spray of sparkles and leave a short sparkling trail as they fly. The
            // stars are computed four-pointed twinkles 20 px across (5 units of 4 px, the dot of
            // the 4x rune) and the rest small glowing points. The order matches RuneGainEffect's
            // sources.
            var sparkles = Emitter("RuneSparkles", layer);
            sparkles.GlintDots = 5;
            sparkles.Sources = new List<Hd2dEmberSource>
            {
                SparkSource(
                    "Arrival",
                    48,
                    new Vector2(40f, 40f),
                    new Vector2(160f, 280f),
                    new Vector2(0.25f, 0.45f),
                    200f,
                    -80f
                ),
                SparkSource(
                    "Final",
                    20,
                    new Vector2(24f, 24f),
                    new Vector2(220f, 400f),
                    new Vector2(0.4f, 0.75f),
                    260f,
                    -220f
                ),
                SparkSource(
                    "Burst",
                    24,
                    new Vector2(40f, 40f),
                    new Vector2(220f, 460f),
                    new Vector2(0.3f, 0.6f),
                    360f,
                    -120f
                ),
                SparkSource(
                    "Trail",
                    64,
                    new Vector2(36f, 36f),
                    new Vector2(10f, 50f),
                    new Vector2(0.25f, 0.5f),
                    360f,
                    -40f
                ),
            };
            effect.RuneSparkles = sparkles;

            var particle = Rect("RuneParticle", layer);
            Place(particle, Vector2.zero, new Vector2(RuneIconSize, RuneIconSize));
            effect.RuneParticle = SpriteImage(particle, IconRunePath, Color.white);
            effect.RuneParticle.preserveAspect = true;
            particle.gameObject.SetActive(false);

            // The gained amount appears under the balance while the counter rises.
            var gain = Rect("RuneGain", gainParent);
            gain.gameObject.AddComponent<Canvas>();
            Corner(gain, Vector2.one, gainOffset, gainSize);
            var group = gain.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;
            effect.GainGroup = group;
            effect.GainLabel = Label(
                gain,
                "RuneGainLabel",
                "",
                gainFontSize,
                UiPalette.Teal,
                TextAlignmentOptions.MidlineRight
            );
            Stretch(effect.GainLabel.rectTransform);
            return effect;
        }

        private static Hd2dEmberEmitter Emitter(string name, RectTransform parent)
        {
            var instance = InstantiatePrefab(Hd2dAssets.EmberEmitterPrefabPath, name, parent);
            Stretch((RectTransform)instance.transform);
            var emitter = instance.GetComponent<Hd2dEmberEmitter>();
            if (emitter == null)
                throw new InvalidOperationException(
                    $"{Hd2dAssets.EmberEmitterPrefabPath} has no {nameof(Hd2dEmberEmitter)}."
                );
            return emitter;
        }

        private static Hd2dEmberSource SparkSource(
            string name,
            int count,
            Vector2 area,
            Vector2 speed,
            Vector2 lifetime,
            float spread,
            float buoyancy
        ) =>
            new()
            {
                Name = name,
                Anchor = new Vector2(0.5f, 0.5f),
                SpawnArea = area,
                Count = count,
                Loop = false,
                LifetimeRange = lifetime,
                Direction = 180f,
                Spread = spread,
                SpeedRange = speed,
                Buoyancy = buoyancy,
                Curl = 0f,
                DotSize = 4,
                CrossShare = 0.6f,
                StreakShare = 0f,
                StartColor = RuneSpark,
                EndColor = RuneSparkFade,
                Twinkle = 0.75f,
            };

        private static Hd2dEmberSource HazeSource(
            string name,
            int count,
            Vector2 area,
            Vector2 speed,
            Vector2 lifetime,
            float spread,
            int scale,
            float alpha
        ) =>
            new()
            {
                Name = name,
                Anchor = new Vector2(0.5f, 0.5f),
                SpawnArea = area,
                Count = count,
                Loop = false,
                LifetimeRange = lifetime,
                Direction = 180f,
                Spread = spread,
                SpeedRange = speed,
                Buoyancy = 0f,
                Curl = 0f,
                // The 64 px glow is scaled whole, so a puff is 128 or 192 px across.
                DotSize = scale,
                CrossShare = 1f,
                StreakShare = 0f,
                StartColor = new Color(RuneGlowColor.r, RuneGlowColor.g, RuneGlowColor.b, alpha),
                EndColor = RuneSparkFade,
                Twinkle = 0.2f,
            };
    }
}
