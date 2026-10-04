using System;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// One picture stamped on the exploration map: its size and its pixels as RGBA bytes, bottom
    /// row first like a texture's. AdventureAssets copies them from the .aseprite drawings, whose
    /// textures are not readable at run time, so the map can be painted on the device.
    /// </summary>
    [Serializable]
    public sealed class PixelStamp
    {
        public string Name = "";
        public int Width;
        public int Height;
        public byte[] Rgba = Array.Empty<byte>();

        public bool IsEmpty => Width <= 0 || Height <= 0 || Rgba.Length < Width * Height * 4;

        // 下の行から数えた (x, y) の画素。
        public Color32 Pixel(int x, int y)
        {
            int i = (y * Width + x) * 4;
            return new Color32(Rgba[i], Rgba[i + 1], Rgba[i + 2], Rgba[i + 3]);
        }

        public static PixelStamp From(string name, int width, int height, Color32[] pixels)
        {
            var bytes = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                bytes[i * 4] = pixels[i].r;
                bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b;
                bytes[i * 4 + 3] = pixels[i].a;
            }
            return new PixelStamp
            {
                Name = name,
                Width = width,
                Height = height,
                Rgba = bytes,
            };
        }
    }

    /// <summary>
    /// The pictures the exploration map is painted with (Shared/Art/Stages/Exploration): the
    /// forest's trees and undergrowth at full size for the near rows and at half size for the far
    /// ones, and the ruin standing at the deepest room.
    /// </summary>
    public sealed class ExplorationMapArt : ScriptableObject
    {
        public PixelStamp[] Trees = Array.Empty<PixelStamp>();
        public PixelStamp[] Undergrowth = Array.Empty<PixelStamp>();
        public PixelStamp[] TreesFar = Array.Empty<PixelStamp>();
        public PixelStamp[] UndergrowthFar = Array.Empty<PixelStamp>();
        public PixelStamp Ruin = new();
    }
}
