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
    /// point-filtered pixel art; the shades are smooth. The navigation icons are 24x24
    /// drawings saved as Aseprite files next to the other art, so regeneration only fixes
    /// their import settings. The rune icon is shared game art drawn in Aseprite and is
    /// treated the same way, with mipmaps because the flying runes shrink below their drawn
    /// size. Shapes, shades and icons that other screens use too come from <see cref="UiArt"/>.
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
        public const string IconPartyPath = ArtFolder + "/IconParty.aseprite";
        public const string IconEquipmentPath = ArtFolder + "/IconEquipment.aseprite";
        public const string IconCompassPath = ArtFolder + "/IconCompass.aseprite";
        public const string IconSummonPath = ArtFolder + "/IconSummon.aseprite";

        // The 5x5 four-point star of the rune sparkles, white so the emitter can tint it.
        public const string RuneTwinklePath = ArtFolder + "/RuneTwinkle.png";

        // Drawn in Aseprite (24x24, full colour).
        public const string IconRunePath =
            "Assets/Baryonyx/Shared/Art/GameResources/IconRune.aseprite";

        // Drawn in Aseprite (24x24) and shown at 4x.
        private static readonly string[] DrawnIconPaths =
        {
            IconPartyPath,
            IconEquipmentPath,
            IconSummonPath,
            IconCompassPath,
        };

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
            foreach (var path in DrawnIconPaths)
                ArtAssets.ImportDrawn(path);
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
