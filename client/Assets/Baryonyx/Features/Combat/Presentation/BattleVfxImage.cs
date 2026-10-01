using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// A board an effect's shape is worked out on (Battle Vfx Shape shader): a RawImage that also
    /// writes the shape's own values into the second UV of its vertices, so boards sharing one
    /// material can each show their own moment. The canvas must carry
    /// <see cref="AdditionalCanvasShaderChannels.TexCoord1"/>, as <see cref="EnableShapeChannel"/> sets.
    /// </summary>
    public sealed class BattleVfxImage : RawImage
    {
        private Vector4 shape;

        /// <summary>
        /// The shape's values: x the seed (0 to 1), y how far along it is (0 to 1), z how much of it
        /// is eaten away (0 whole, 1 gone), w its brightness (0 is taken as 1).
        /// </summary>
        public Vector4 Shape
        {
            get => shape;
            set
            {
                if (shape == value)
                    return;
                shape = value;
                SetVerticesDirty();
            }
        }

        /// <summary>Sets the shape's values (see <see cref="Shape"/>).</summary>
        public void SetShape(float seed, float progress, float eaten, float brightness = 1f) =>
            Shape = new Vector4(seed, progress, eaten, brightness);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            base.OnPopulateMesh(vh);
            var vertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.uv1 = shape;
                vh.SetUIVertex(vertex, i);
            }
        }

        /// <summary>Lets the canvas a layer is drawn on carry the shapes' values to the shader.</summary>
        public static void EnableShapeChannel(Transform layer)
        {
            if (layer == null)
                return;
            var canvas = layer.GetComponentInParent<Canvas>(true);
            if (canvas == null)
                return;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            if (canvas.rootCanvas != null)
                canvas.rootCanvas.additionalShaderChannels |=
                    AdditionalCanvasShaderChannels.TexCoord1;
        }
    }
}
