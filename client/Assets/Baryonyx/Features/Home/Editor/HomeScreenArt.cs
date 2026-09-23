using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Home.Editor
{
    /// <summary>
    /// Images owned by the home screen, drawn from code and saved as PNG so the showcase can
    /// list them and regeneration stays reproducible. Icons are point-filtered pixel art;
    /// shadows, capsules and the resume card are smooth shapes with anti-aliased edges.
    /// </summary>
    public static class HomeScreenArt
    {
        public const string ArtFolder = "Assets/Baryonyx/Features/Home/UI/Art";
        public const string TextShadowPath = "Assets/Baryonyx/Features/Home/UI/HomeTextShadow.mat";
        public const string CampfirePath = ArtFolder + "/Campfire.png";
        public const string ShadowPath = ArtFolder + "/GroundShadow.png";
        public const string ShadePath = ArtFolder + "/ScreenShade.png";
        public const string ShadeHorizontalPath = ArtFolder + "/ShadeHorizontal.png";
        public const string SoftSpotPath = ArtFolder + "/SoftSpot.png";
        public const string CardShadePath = ArtFolder + "/CardShade.png";
        public const string FeatherPath = ArtFolder + "/FeatherPlate.png";
        public const string CirclePath = ArtFolder + "/Circle.png";
        public const string CapsulePath = ArtFolder + "/Capsule.png";
        public const string RoundedRectPath = ArtFolder + "/RoundedRect.png";
        public const string RoundedRingPath = ArtFolder + "/RoundedRing.png";
        public const string ArrowsPath = ArtFolder + "/Arrows.png";
        public const string IconPartyPath = ArtFolder + "/IconParty.png";
        public const string IconEquipmentPath = ArtFolder + "/IconEquipment.png";
        public const string IconGoalPath = ArtFolder + "/IconGoal.png";
        public const string IconCompassPath = ArtFolder + "/IconCompass.png";
        public const string IconSummonPath = ArtFolder + "/IconSummon.png";
        public const string IconSettingsPath = ArtFolder + "/IconSettings.png";
        public const string IconRunePath = ArtFolder + "/IconRune.png";

        // Rounded corners of the resume card, in texture pixels (1 pixel = 1 canvas unit).
        public const int CardRadius = 18;

        public static readonly Color Ink = new(0.106f, 0.071f, 0.024f);

        private static readonly Dictionary<char, Color> IconPalette = new()
        {
            ['o'] = new Color(0.106f, 0.071f, 0.024f),
            ['f'] = new Color(0.953f, 0.914f, 0.824f),
            ['h'] = Color.white,
            ['a'] = new Color(0.914f, 0.635f, 0.231f),
            ['r'] = new Color(0.816f, 0.318f, 0.247f),
        };

        private static readonly Dictionary<char, Color> CompassPalette = new()
        {
            ['o'] = new Color(0.063f, 0.075f, 0.114f),
            ['a'] = new Color(0.788f, 0.631f, 0.353f),
            ['f'] = new Color(0.910f, 0.886f, 0.831f),
            ['r'] = new Color(0.816f, 0.318f, 0.247f),
            ['s'] = new Color(0.353f, 0.525f, 0.784f),
        };

        // The same 11x14 flame as the design mock: outer red, orange, yellow, white core, logs.
        private static readonly string[] Campfire =
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
            ".LLmmmmmLL.",
            "LLDLLLLLDLL",
            ".DDDD.DDDD.",
        };

        private static readonly string[] Party =
        {
            "................",
            "....oooo........",
            "...offffo.oooo..",
            "...offhfo.offfo.",
            "...offffooffhfo.",
            "....offo.offffo.",
            "...oooooo.offo..",
            "..offffffooooooo",
            ".offfhffffoffffo",
            ".offffffffoffffo",
            ".offffffffoffffo",
            ".oooooooooooooo.",
            "................",
            "................",
            "................",
            "................",
        };

        private static readonly string[] Sword =
        {
            ".............ooo",
            "............ofho",
            "...........ofho.",
            "..........ofho..",
            ".........ofho...",
            "..oo....ofho....",
            "..oao..ofho.....",
            "...oaoofho......",
            "....oaaoo.......",
            ".....oaao.......",
            "....oaooao......",
            "...oao..oao.....",
            "..ooo....oo.....",
            ".oao............",
            "oao.............",
            "oo..............",
        };

        private static readonly string[] Flag =
        {
            ".oo.............",
            ".oaooooooooo....",
            ".oaorrrrrrrro...",
            ".oaorrhrrrrro...",
            ".oaorrrrrrro....",
            ".oaorrrrrro.....",
            ".oaorrrrrrro....",
            ".oaorrrrrrrro...",
            ".oaooooooooo....",
            ".oao............",
            ".oao............",
            ".oao............",
            ".oao............",
            ".oao............",
            ".oao............",
            ".ooo............",
        };

        private static readonly string[] Compass =
        {
            ".....oooooo.....",
            "...ooaaaaaaoo...",
            "..oaaffffffaao..",
            ".oaffffffffrrao.",
            ".oafffffffrrfao.",
            "oafffffffrrfffao",
            "oaffffffrrffffao",
            "oafffffoofffffao",
            "oafffffoofffffao",
            "oaffffssffffffao",
            "oafffssfffffffao",
            ".oafssfffffffao.",
            ".oasfffffffffao.",
            "..oaaffffffaao..",
            "...ooaaaaaaoo...",
            ".....oooooo.....",
        };

        private static readonly Dictionary<char, Color> CirclePalette = new()
        {
            ['o'] = new Color(0.106f, 0.071f, 0.2f),
            ['c'] = new Color(0.608f, 0.416f, 0.839f),
            ['g'] = new Color(0.498f, 0.890f, 0.839f),
            ['h'] = Color.white,
        };

        // A summoning circle: a purple ring around a rune-coloured hexagram.
        private static readonly string[] MagicCircle =
        {
            ".....oooooo.....",
            "...ooccccccoo...",
            "..occ..gg..cco..",
            ".occ..g..g..cco.",
            ".oc...g..g...co.",
            "oc.gggggggggg.co",
            "oc..gg....gg..co",
            "oc..g..hh..g..co",
            "oc..gg.hh.gg..co",
            "oc.g.g....g.g.co",
            "oc.gggggggggg.co",
            ".oc...g..g...co.",
            ".occ...gg...cco.",
            "..occ..gg..cco..",
            "...ooccccccoo...",
            ".....oooooo.....",
        };

        private static readonly string[] Gear =
        {
            "......####......",
            "......####......",
            "..##.######.##..",
            "..############..",
            "...##########...",
            ".#####....#####.",
            ".####......####.",
            "#####......#####",
            "#####......#####",
            ".####......####.",
            ".#####....#####.",
            "...##########...",
            "..############..",
            "..##.######.##..",
            "......####......",
            "......####......",
        };

        private static readonly string[] Rune =
        {
            ".......##.......",
            "......#..#......",
            ".....#....#.....",
            "....#..##..#....",
            "...#...##...#...",
            "..#....##....#..",
            ".#.....##.....#.",
            "#......##......#",
            "#......##......#",
            ".#.....##.....#.",
            "..#....##....#..",
            "...#...##...#...",
            "....#..##..#....",
            ".....#....#.....",
            "......#..#......",
            ".......##.......",
        };

        public static void EnsureAll()
        {
            Directory.CreateDirectory(ArtFolder);
            WritePattern(
                CampfirePath,
                Campfire,
                c =>
                    c switch
                    {
                        'o' => new Color(0.851f, 0.282f, 0.110f),
                        'm' => new Color(0.949f, 0.549f, 0.157f),
                        'y' => new Color(1f, 0.784f, 0.290f),
                        'w' => new Color(1f, 0.945f, 0.722f),
                        'L' => new Color(0.420f, 0.267f, 0.149f),
                        'D' => new Color(0.239f, 0.149f, 0.086f),
                        _ => Color.clear,
                    }
            );
            WritePattern(IconPartyPath, Party, c => Lookup(IconPalette, c));
            WritePattern(IconEquipmentPath, Sword, c => Lookup(IconPalette, c));
            WritePattern(IconGoalPath, Flag, c => Lookup(IconPalette, c));
            WritePattern(IconCompassPath, Compass, c => Lookup(CompassPalette, c));
            WritePattern(IconSummonPath, MagicCircle, c => Lookup(CirclePalette, c));
            WritePattern(IconSettingsPath, Gear, c => c == '#' ? Color.white : Color.clear);
            WritePattern(IconRunePath, Rune, c => c == '#' ? Color.white : Color.clear);

            WriteShadow();
            WriteShade();
            WriteSmooth(
                ShadeHorizontalPath,
                64,
                1,
                Vector4.zero,
                (x, _) => Color.white * new Color(1, 1, 1, x * x)
            );
            WriteSmooth(
                SoftSpotPath,
                64,
                64,
                Vector4.zero,
                (x, y) =>
                {
                    float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f;
                    return new Color(1, 1, 1, 1f - Mathf.SmoothStep(0f, 1f, d));
                }
            );
            // Dark at the bottom so the destination text reads over the art. The values are a
            // little stronger than the web mock because uGUI blends in linear colour space.
            // White with alpha only, like ScreenShade; the RawImage colour supplies the tint.
            WriteSmooth(
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
            // A plate that is evenly dark inside and fades out over its outer 20 pixels, so text
            // stays readable without drawing a frame. Sliced so the fade keeps its width.
            WriteSmooth(
                FeatherPath,
                64,
                64,
                new Vector4(24, 24, 24, 24),
                (u, v) =>
                {
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 64f;
                    return new Color(
                        1f,
                        1f,
                        1f,
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 22f))
                    );
                }
            );
            WriteRounded(CirclePath, 64, 32f, 0f, Vector4.zero, _ => Color.white);
            WriteRounded(CapsulePath, 81, 40f, 0f, new Vector4(40, 40, 40, 40), _ => Color.white);
            WriteRounded(
                RoundedRectPath,
                48,
                CardRadius,
                0f,
                new Vector4(20, 20, 20, 20),
                _ => Color.white
            );
            WriteRounded(
                RoundedRingPath,
                48,
                CardRadius,
                2.5f,
                new Vector4(20, 20, 20, 20),
                _ => Color.white
            );
            WriteArrows();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>A copy of the font material with a soft drop shadow (TMP underlay).</summary>
        public static Material EnsureTextShadow(TMP_FontAsset font)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(TextShadowPath);
            if (material == null)
            {
                material = new Material(font.material) { name = "HomeTextShadow" };
                AssetDatabase.CreateAsset(material, TextShadowPath);
            }
            material.shader = font.material.shader;
            material.CopyPropertiesFromMaterial(font.material);
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.85f));
            material.SetFloat("_UnderlayOffsetX", 0f);
            material.SetFloat("_UnderlayOffsetY", -0.7f);
            material.SetFloat("_UnderlayDilate", 0.25f);
            material.SetFloat("_UnderlaySoftness", 0.35f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        public static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException("Home artwork is missing: " + path);
            return sprite;
        }

        public static Texture2D LoadTexture(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                throw new InvalidOperationException("Home artwork is missing: " + path);
            return texture;
        }

        /// <summary>Imports shared pixel art with the same point-filtered settings the wireframe uses.</summary>
        public static Texture2D ImportPixelTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Required artwork is missing: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return LoadTexture(path);
        }

        private static Color Lookup(Dictionary<char, Color> palette, char c) =>
            palette.TryGetValue(c, out var color) ? color : Color.clear;

        private static void WritePattern(string path, string[] rows, Func<char, Color> palette)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    if (rows[y].Length != width)
                        throw new InvalidOperationException(
                            $"Pattern row {y} of {path} is ragged."
                        );
                    for (int x = 0; x < width; x++)
                        texture.SetPixel(x, height - 1 - y, palette(rows[y][x]));
                }
                texture.Apply();
                Save(path, texture, Vector4.zero, FilterMode.Point);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // Evaluates color(u, v) at pixel centres, u and v in 0..1 (v = 0 at the bottom).
        private static void WriteSmooth(
            string path,
            int width,
            int height,
            Vector4 border,
            Func<float, float, Color> color,
            bool sprite = true
        )
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, color((x + 0.5f) / width, (y + 0.5f) / height));
                texture.Apply();
                Save(path, texture, border, FilterMode.Bilinear, sprite);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // A square rounded rectangle (or ring when ringWidth > 0) with one-pixel anti-aliasing.
        // fill(v) colours the shape by the vertical position v (0 = bottom, 1 = top).
        private static void WriteRounded(
            string path,
            int size,
            float radius,
            float ringWidth,
            Vector4 border,
            Func<float, Color> fill
        )
        {
            float half = size / 2f;
            WriteSmooth(
                path,
                size,
                size,
                border,
                (u, v) =>
                {
                    float px = Mathf.Abs(u * size - half) - (half - radius);
                    float py = Mathf.Abs(v * size - half) - (half - radius);
                    float outside =
                        new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude
                        + Mathf.Min(Mathf.Max(px, py), 0f)
                        - radius;
                    float alpha = Mathf.Clamp01(0.5f - outside);
                    if (ringWidth > 0f)
                        alpha *= Mathf.Clamp01(outside + ringWidth + 0.5f);
                    var color = fill(v);
                    color.a *= alpha;
                    return color;
                }
            );
        }

        // Three chevrons (">>>") fading in from the left, used after "再開".
        private static void WriteArrows()
        {
            const int width = 88;
            const int height = 56;
            float[] opacity = { 0.4f, 0.7f, 1f };
            WriteSmooth(
                ArrowsPath,
                width,
                height,
                Vector4.zero,
                (u, v) =>
                {
                    var p = new Vector2(u * width, v * height);
                    float alpha = 0f;
                    for (int i = 0; i < 3; i++)
                    {
                        float x0 = 8f + i * 26f;
                        var top = new Vector2(x0, height - 8f);
                        var tip = new Vector2(x0 + 20f, height / 2f);
                        var bottom = new Vector2(x0, 8f);
                        float d = Mathf.Min(Segment(p, top, tip), Segment(p, tip, bottom));
                        alpha = Mathf.Max(alpha, Mathf.Clamp01(4.5f - d) * opacity[i]);
                    }
                    return new Color(1f, 1f, 1f, alpha);
                }
            );
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static void WriteShadow()
        {
            const int width = 32;
            const int height = 8;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f - width / 2f) / (width / 2f);
                    float dy = (y + 0.5f - height / 2f) / (height / 2f);
                    float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, alpha)));
                }
                texture.Apply();
                Save(ShadowPath, texture, Vector4.zero, FilterMode.Bilinear);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // Opaque at the top edge, clear at the bottom. The lower shade flips it with a UV rect.
        private static void WriteShade()
        {
            const int height = 64;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    float t = y / (height - 1f);
                    texture.SetPixel(0, y, new Color(1f, 1f, 1f, t * t));
                }
                texture.Apply();
                Save(ShadePath, texture, Vector4.zero, FilterMode.Bilinear);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void Save(
            string path,
            Texture2D texture,
            Vector4 border,
            FilterMode filter,
            bool sprite = true
        )
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite
                ? TextureImporterType.Sprite
                : TextureImporterType.Default;
            if (sprite)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.spriteBorder = border;
            }
            importer.filterMode = filter;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = sprite;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
