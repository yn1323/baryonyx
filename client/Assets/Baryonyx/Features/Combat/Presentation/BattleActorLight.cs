using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The light a skill's effect casts on a character: a copy of the character's picture drawn
    /// over it with additive blending, so the character takes on the light's colour. Each corner
    /// has its own brightness, so the side facing the light, or the feet for a light from below,
    /// is lit more than the far side.
    /// </summary>
    public sealed class BattleActorLight : RawImage
    {
        // Bottom left, top left, top right, bottom right, as the quad's vertices are made.
        private readonly Color[] corners = { Color.clear, Color.clear, Color.clear, Color.clear };

        /// <summary>True while any corner is lit.</summary>
        public bool Lit { get; private set; }

        /// <summary>Sets the light of each corner (its alpha is the strength) and redraws.</summary>
        public void SetCorners(Color bottomLeft, Color topLeft, Color topRight, Color bottomRight)
        {
            if (
                corners[0] == bottomLeft
                && corners[1] == topLeft
                && corners[2] == topRight
                && corners[3] == bottomRight
            )
                return;
            corners[0] = bottomLeft;
            corners[1] = topLeft;
            corners[2] = topRight;
            corners[3] = bottomRight;
            Lit = bottomLeft.a + topLeft.a + topRight.a + bottomRight.a > 0.002f;
            enabled = Lit;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            base.OnPopulateMesh(vh);
            if (vh.currentVertCount != 4)
                return;
            var vertex = new UIVertex();
            for (int i = 0; i < 4; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.color = corners[i];
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
