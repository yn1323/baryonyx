using System.IO;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Home.Editor
{
    /// <summary>
    /// Images owned by the home screen, drawn from code and saved as PNG so the showcase can
    /// list them and regeneration stays reproducible. The campfire and the rune sparkle are
    /// point-filtered pixel art; the shades are smooth. The navigation icons are plain white
    /// shapes without outlines, drawn as tools (sword and shield, scales, a summoning circle,
    /// fanned cards for the skill debugging room, a gem for the quality one) on a 64-unit design
    /// square and tinted by the screen. The rune icon is shared game art and the compass is
    /// shared pixel art, both drawn in Aseprite, so regeneration only fixes their import
    /// settings; the rune uses mipmaps because the flying runes shrink below its drawn size.
    /// Shapes, shades and icons that other screens use too come from <see cref="UiArt"/>.
    /// </summary>
    public static class HomeScreenArt
    {
        public const string ArtFolder = "Assets/Baryonyx/Features/Home/UI/Art";

        // The campfire is drawn in two parts: the logs, which stand on the 3D stage, and the
        // flame over them, which only the 2D look shows (the stage burns a computed flame).
        public const string CampfireLogsPath = ArtFolder + "/CampfireLogs.png";
        public const string CampfireFlamePath = ArtFolder + "/CampfireFlame.png";
        public const string ShadeHorizontalPath = ArtFolder + "/ShadeHorizontal.png";
        public const string CardShadePath = ArtFolder + "/CardShade.png";
        public const string IconPartyPath = ArtFolder + "/IconParty.png";
        public const string IconShopPath = ArtFolder + "/IconShop.png";
        public const string IconSummonPath = ArtFolder + "/IconSummon.png";
        public const string IconLibraryPath = ArtFolder + "/IconLibrary.png";

        // The skill debugging room, opened from under the settings at the top right.
        public const string IconSkillLabPath = ArtFolder + "/IconSkillLab.png";

        // The quality debugging room, opened from under the skill debugging room.
        public const string IconQualityLabPath = ArtFolder + "/IconQualityLab.png";

        // Drawn at twice the 96 px the buttons show them at, so they stay sharp on larger
        // screens and shrink smoothly with mipmaps.
        private const int NavIconTextureSize = 192;

        // The clear margin that separates a front part from the part behind it (design units).
        private const float PartGap = 2.6f;

        // The 5x5 four-point star of the rune sparkles, white so the emitter can tint it.
        public const string RuneTwinklePath = ArtFolder + "/RuneTwinkle.png";

        // Drawn in Aseprite (24x24, full colour).
        public const string IconRunePath =
            "Assets/Baryonyx/Shared/Art/GameResources/IconRune.aseprite";

        // The party: a sword standing behind a heater shield.
        private static VectorIcon SwordAndShield()
        {
            IconShape Sword(IconShape part) => part.Rotated(14f, 32f, 32f).Translated(9f, 0f);
            return new VectorIcon()
                .Fill(Sword(IconShape.Circle(32f, 4.5f, 3.5f)))
                .Fill(Sword(IconShape.Rect(30f, 7f, 4f, 8f)))
                .Fill(Sword(IconShape.Rect(21f, 15f, 22f, 4.5f)))
                .Fill(Sword(IconShape.Path("M28.3 19.5 L35.7 19.5 L35.7 52 L32 59 L28.3 52Z")))
                .Fill(IconShape.Path("M3 23 L33 23 L33 37 Q33 52 18 60 Q3 52 3 37Z"), PartGap)
                .CutStroke(IconShape.Path("M18 27 L18 54"), 2.4f);
        }

        // The shop: a pair of scales for buying and selling.
        private static VectorIcon Scales() =>
            new VectorIcon()
                .Fill(IconShape.Rect(29.5f, 12f, 5f, 40f))
                .Fill(IconShape.Path("M17 59 L47 59 L43 51 L21 51Z"))
                .Stroke(IconShape.Path("M8 16 L56 16"), 4.6f)
                .Fill(IconShape.Circle(32f, 8f, 4.2f))
                .Stroke(
                    IconShape.Path("M9 17 L3.5 35 M9 17 L14.5 35 M55 17 L49.5 35 M55 17 L60.5 35"),
                    2.8f
                )
                .Fill(IconShape.Path("M1 34 L17 34 Q17 45 9 45 Q1 45 1 34Z"))
                .Fill(IconShape.Path("M47 34 L63 34 Q63 45 55 45 Q47 45 47 34Z"));

        // The temple, where companions are summoned: a ring around a four-pointed star.
        private static VectorIcon SummoningCircle() =>
            new VectorIcon()
                .Stroke(IconShape.Circle(32f, 32f, 26.5f), 5f)
                .Fill(
                    IconShape.Path(
                        "M32 12 Q33.6 30.4 52 32 Q33.6 33.6 32 52 Q30.4 33.6 12 32 Q30.4 30.4 32 12Z"
                    )
                );

        // The library, where what was found is looked up: an open book with lines on both pages.
        private static VectorIcon OpenBook() =>
            new VectorIcon()
                .Fill(IconShape.Path("M4 14 Q18 10 30 16 L30 56 Q18 50 4 54Z"))
                .Fill(IconShape.Path("M60 14 Q46 10 34 16 L34 56 Q46 50 60 54Z"))
                .CutStroke(
                    IconShape.Path(
                        "M10 24 Q17 22 24 25 M10 32 Q17 30 24 33 M10 40 Q17 38 24 41 M40 25 Q47 22 54 24 M40 33 Q47 30 54 32 M40 41 Q47 38 54 40"
                    ),
                    2.4f
                );

        // The skill debugging room, where every skill's card is played: two cards fanned out,
        // the front one marked with a four-pointed star.
        private static VectorIcon SkillCards()
        {
            IconShape Back(IconShape part) => part.Rotated(-14f, 24f, 32f);
            IconShape Front(IconShape part) => part.Rotated(10f, 38f, 34f);
            return new VectorIcon()
                .Fill(Back(IconShape.Rect(10f, 9f, 28f, 42f)))
                .Fill(Front(IconShape.Rect(24f, 12f, 29f, 44f)), PartGap)
                .Cut(
                    Front(
                        IconShape.Path(
                            "M38.5 22 Q39.8 32.7 50 34 Q39.8 35.3 38.5 46 Q37.2 35.3 27 34 Q37.2 32.7 38.5 22Z"
                        )
                    )
                );
        }

        // The quality debugging room, where a thing's quality and sparkle are tried: a cut gem
        // with a line across its widest part, and a small four-pointed star at its top right.
        private static VectorIcon Gem() =>
            new VectorIcon()
                .Fill(IconShape.Path("M8 24 L18 12 L40 12 L50 24 L29 54 Z"))
                .Cut(IconShape.Rect(8f, 23f, 42f, 3f))
                .Fill(
                    IconShape.Path(
                        "M52 4 Q53.2 10.8 60 12 Q53.2 13.2 52 20 Q50.8 13.2 44 12 Q50.8 10.8 52 4Z"
                    ),
                    PartGap
                );

        // The same 11x14 campfire as the design mock, split where the flame meets the logs: the
        // flame's bottom row and the logs' top row share a row of the picture without overlapping.
        // Flame: outer red, orange, yellow, white core (11x12, the top of the campfire).
        private static readonly string[] CampfireFlame =
        {
            ".....o.....",
            "....oo.....",
            "....omo....",
            "...ommmo...",
            "...omymoo..",
            "..omyyymo..",
            "..omywymo..",
            ".omywwwymo.",
            ".omywwwymo.",
            ".omyywyymo.",
            "..omyyymo..",
            "...mmmmm...",
        };

        // Logs: light and dark wood (11x3, the bottom of the campfire).
        private static readonly string[] CampfireLogs =
        {
            ".LL.....LL.",
            "LLDLLLLLDLL",
            ".DDDD.DDDD.",
        };

        // A bright centre, the arms a little dimmer and faint tips, so it twinkles as a star.
        private static readonly string[] RuneTwinkle =
        {
            "..+..",
            "..#..",
            "+#O#+",
            "..#..",
            "..+..",
        };

        public static void EnsureAll()
        {
            UiArt.EnsureAll();
            Directory.CreateDirectory(ArtFolder);
            ArtAssets.WritePattern(CampfireFlamePath, CampfireFlame, CampfireColor);
            ArtAssets.WritePattern(CampfireLogsPath, CampfireLogs, CampfireColor);
            SwordAndShield().Save(IconPartyPath, NavIconTextureSize);
            Scales().Save(IconShopPath, NavIconTextureSize);
            SummoningCircle().Save(IconSummonPath, NavIconTextureSize);
            OpenBook().Save(IconLibraryPath, NavIconTextureSize);
            SkillCards().Save(IconSkillLabPath, NavIconTextureSize);
            Gem().Save(IconQualityLabPath, NavIconTextureSize);
            ArtAssets.ImportDrawn(UiArt.IconCompassPath);
            ArtAssets.ImportDrawn(IconRunePath, mipmaps: true);
            ArtAssets.WritePattern(
                RuneTwinklePath,
                RuneTwinkle,
                c =>
                    c switch
                    {
                        'O' => Color.white,
                        '#' => new Color(1f, 1f, 1f, 0.8f),
                        '+' => new Color(1f, 1f, 1f, 0.4f),
                        _ => Color.clear,
                    }
            );
            ArtAssets.WriteSmooth(
                ShadeHorizontalPath,
                64,
                1,
                Vector4.zero,
                (x, _) => Color.white * new Color(1, 1, 1, x * x)
            );
            // Dark at the bottom so the destination text reads over the art. The values are a
            // little stronger than the web mock because uGUI blends in linear colour space.
            // White with alpha only, like ScreenShade; the RawImage colour supplies the tint.
            ArtAssets.WriteSmooth(
                CardShadePath,
                1,
                64,
                Vector4.zero,
                (_, v) =>
                    new Color(
                        1f,
                        1f,
                        1f,
                        v < 0.5f
                            ? Mathf.Lerp(0.97f, 0.72f, v / 0.5f)
                            : Mathf.Lerp(0.72f, 0.18f, (v - 0.5f) / 0.5f)
                    )
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static Color CampfireColor(char c) =>
            c switch
            {
                'o' => new Color(0.851f, 0.282f, 0.110f),
                'm' => new Color(0.949f, 0.549f, 0.157f),
                'y' => new Color(1f, 0.784f, 0.290f),
                'w' => new Color(1f, 0.945f, 0.722f),
                'L' => new Color(0.420f, 0.267f, 0.149f),
                'D' => new Color(0.239f, 0.149f, 0.086f),
                _ => Color.clear,
            };
    }
}
