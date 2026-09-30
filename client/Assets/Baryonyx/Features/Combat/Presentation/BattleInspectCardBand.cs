using System;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The dark translucent band behind the owner, the name and the kind line of a card, only as
    /// wide as each line's text. It samples the middle column of Top's panel gradient, so it keeps
    /// that panel's soft top and bottom, and fades out over <see cref="Fade"/> after each line.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleInspectCardBand : MaskableGraphic
    {
        public Texture Texture;

        /// <summary>Where each line's text ends, in px from the band's left edge.</summary>
        public float[] LineEnds = Array.Empty<float>();

        /// <summary>The bottom of each line in px down from the band's top; the last one is the band's height.</summary>
        public float[] LineBottoms = Array.Empty<float>();

        /// <summary>How far past the end of the text the band fades out, in px.</summary>
        public float Fade = 27f;

        public override Texture mainTexture => Texture != null ? Texture : s_WhiteTexture;

        public void SetLineEnds(float[] ends)
        {
            LineEnds = ends;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.height <= 0f)
                return;
            float top = 0f;
            int lines = Mathf.Min(LineEnds.Length, LineBottoms.Length);
            for (int i = 0; i < lines; i++)
            {
                float bottom = LineBottoms[i];
                var y = new Vector2(rect.yMax - bottom, rect.yMax - top);
                var v = new Vector2(1f - bottom / rect.height, 1f - top / rect.height);
                float end = Mathf.Min(rect.xMin + LineEnds[i], rect.xMax);
                float fadeEnd = Mathf.Min(end + Fade, rect.xMax);
                Quad(vh, new Vector2(rect.xMin, end), y, v, 1f, 1f);
                if (fadeEnd > end && Fade > 0f)
                    Quad(vh, new Vector2(end, fadeEnd), y, v, 1f, 1f - (fadeEnd - end) / Fade);
                top = bottom;
            }
        }

        private void Quad(VertexHelper vh, Vector2 x, Vector2 y, Vector2 v, float left, float right)
        {
            int start = vh.currentVertCount;
            Color32 a = Faded(left);
            Color32 b = Faded(right);
            // The middle column of the texture: its horizontal fade is not used.
            vh.AddVert(new Vector3(x.x, y.x), a, new Vector2(0.5f, v.x));
            vh.AddVert(new Vector3(x.x, y.y), a, new Vector2(0.5f, v.y));
            vh.AddVert(new Vector3(x.y, y.y), b, new Vector2(0.5f, v.y));
            vh.AddVert(new Vector3(x.y, y.x), b, new Vector2(0.5f, v.x));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        private Color Faded(float alpha) => new(color.r, color.g, color.b, color.a * alpha);
    }
}
