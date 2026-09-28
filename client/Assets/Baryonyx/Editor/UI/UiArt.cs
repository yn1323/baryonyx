using Baryonyx.Editor.Art;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Editor.UI
{
    // 複数の画面で使うUIの画像（形・影・アイコン）と、影つきの文字のマテリアル。
    // コードで描いてPNGで保存し、展示室に一覧でき、生成し直しても同じ画像になるようにする。
    // 形と影は縁をなめらかにし、アイコンはドット絵にする。色は白で描き、使う側で色を付ける。
    public static class UiArt
    {
        public const string Folder = "Assets/Baryonyx/Shared/UI/Art";
        public const string TextShadowPath = "Assets/Baryonyx/Shared/UI/Fonts/TextShadow.mat";
        public const string CirclePath = Folder + "/Circle.png";
        public const string CapsulePath = Folder + "/Capsule.png";
        public const string RoundedRectPath = Folder + "/RoundedRect.png";
        public const string RoundedRingPath = Folder + "/RoundedRing.png";
        public const string ShadePath = Folder + "/ScreenShade.png";
        public const string ShadowPath = Folder + "/GroundShadow.png";
        public const string FeatherPath = Folder + "/FeatherPlate.png";
        public const string SoftSpotPath = Folder + "/SoftSpot.png";
        public const string IconSettingsPath = Folder + "/IconSettings.png";

        // 角丸の半径（テクスチャのピクセル。1ピクセル = Canvasの1単位）。
        public const int CornerRadius = 18;

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

        public static void EnsureAll()
        {
            AssetFolders.Ensure(Folder);
            ArtAssets.WritePattern(
                IconSettingsPath,
                Gear,
                c => c == '#' ? Color.white : Color.clear
            );
            WriteShadow();
            WriteShade();
            ArtAssets.WriteSmooth(
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
            // 内側は一様に暗く、外側の20ピクセルで消える板。枠を描かずに文字を読みやすくする。
            // 9分割で伸ばし、消える幅を保つ。
            ArtAssets.WriteSmooth(
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
            ArtAssets.WriteRounded(CirclePath, 64, 32f, 0f, Vector4.zero, _ => Color.white);
            ArtAssets.WriteRounded(
                CapsulePath,
                81,
                40f,
                0f,
                new Vector4(40, 40, 40, 40),
                _ => Color.white
            );
            ArtAssets.WriteRounded(
                RoundedRectPath,
                48,
                CornerRadius,
                0f,
                new Vector4(20, 20, 20, 20),
                _ => Color.white
            );
            ArtAssets.WriteRounded(
                RoundedRingPath,
                48,
                CornerRadius,
                2.5f,
                new Vector4(20, 20, 20, 20),
                _ => Color.white
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        // フォントのマテリアルの写しに、やわらかい影（TMPのUnderlay）を付ける。
        public static Material EnsureTextShadow(TMP_FontAsset font) =>
            EnsureTextShadow(font, TextShadowPath, new Vector2(0f, -0.7f), 0.35f);

        public static Material EnsureTextShadow(
            TMP_FontAsset font,
            string path,
            Vector2 offset,
            float softness
        )
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = font.material.shader;
            material.CopyPropertiesFromMaterial(font.material);
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.85f));
            material.SetFloat("_UnderlayOffsetX", offset.x);
            material.SetFloat("_UnderlayOffsetY", offset.y);
            material.SetFloat("_UnderlayDilate", 0.25f);
            material.SetFloat("_UnderlaySoftness", softness);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        // 足元の影。中心が濃く、楕円の外へなめらかに消える。
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
                ArtAssets.SavePng(ShadowPath, texture, Vector4.zero, FilterMode.Bilinear);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        // 上端で不透明、下端で透明。画面の下側の影はUVで上下を反転して使う。
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
                ArtAssets.SavePng(ShadePath, texture, Vector4.zero, FilterMode.Bilinear);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
