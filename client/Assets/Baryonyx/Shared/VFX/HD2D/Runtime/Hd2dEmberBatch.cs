using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// Draws all the particles of one material of a <see cref="Hd2dEmberEmitter"/> as a single
    /// mesh, so a moving burst rebuilds one graphic instead of one per particle. Each particle is
    /// the same plain board an <see cref="Image"/> without a sprite would draw (UV 0–1 and the
    /// particle's colour), and the ambient shader adds them, so the drawing order does not matter.
    /// The emitter makes these at run time and keeps them out of the scene file.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class Hd2dEmberBatch : MaskableGraphic
    {
        [System.NonSerialized]
        internal Hd2dEmberEmitter Emitter;

        // true は、きらめく粒（GlintMaterial）を描く。false は、点と尾を引く粒（EmberMaterial）を描く。
        [System.NonSerialized]
        internal bool Glints;

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (Emitter != null)
                Emitter.FillBatch(vertices, Glints);
        }
    }
}
