using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The table of random values the effects' value noise reads (Noise in BattleVfxShapes.cginc),
    /// worked out here from a fixed hash instead of hashing every lattice corner per pixel. One
    /// bilinear read of it gives a whole slice of the noise: r is the corner (x, y), and g the
    /// corner (x + 37, y + 17), which the shader takes as the next slice in z. It is made on load
    /// and set as the global <c>_BattleVfxNoise</c>, so every material that draws with the shapes
    /// (battle, temple, lab) finds it, in Edit Mode as well as in Play.
    /// </summary>
    public static class BattleVfxNoise
    {
        /// <summary>The table's width and height; the noise repeats every this many units.</summary>
        public const int Size = 256;

        /// <summary>How far over the next slice in z lies (must match Noise in the shader).</summary>
        public static readonly Vector2Int SliceStep = new(37, 17);

        private const string TextureName = "BattleVfxNoise";
        private static readonly int NoiseId = Shader.PropertyToID("_BattleVfxNoise");
        private static Texture2D texture;

        /// <summary>Makes the table once and sets it for the shaders.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Ensure()
        {
            if (texture == null)
            {
                // After a script reload the table made before is still set; take it back up.
                var set = Shader.GetGlobalTexture(NoiseId) as Texture2D;
                texture = set != null && set.name == TextureName ? set : Create();
            }
            Shader.SetGlobalTexture(NoiseId, texture);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void EnsureInEditor() => Ensure();
#endif

        /// <summary>The random value (0 to 255) of the lattice corner (x, y), the same every run.</summary>
        public static byte Value(int x, int y)
        {
            uint h = (uint)(x & (Size - 1)) * 0x8DA6B343u ^ (uint)(y & (Size - 1)) * 0xD8163841u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return (byte)(h >> 24);
        }

        private static Texture2D Create()
        {
            var format = SystemInfo.SupportsTextureFormat(TextureFormat.RG16)
                ? TextureFormat.RG16
                : TextureFormat.RGBA32;
            int channels = format == TextureFormat.RG16 ? 2 : 4;
            var data = new byte[Size * Size * channels];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = (y * Size + x) * channels;
                    data[i] = Value(x, y);
                    data[i + 1] = Value(x + SliceStep.x, y + SliceStep.y);
                }
            }
            var made = new Texture2D(Size, Size, format, false, true)
            {
                name = TextureName,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
            };
            made.SetPixelData(data, 0);
            made.Apply(false, false);
            return made;
        }
    }
}
