using Baryonyx.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI.Cards
{
    /// <summary>
    /// The quality's light on a card's frame (doc/art/direction.md, card sparkle), drawn by the
    /// frame's own raw image with the CardFrameFoil shader: at A the frame's outer line turns gold,
    /// at S the frame's metal turns to a rainbow keeping some of the element's colour; at B and C
    /// the frame stays as it is. The frame keeps its width and shape. The look and a seed of the
    /// card's own go to the shader in the vertex colour (white is the plain frame), so every card
    /// shares one material; the alpha stays the frame's own.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class CardFrameFoil : MonoBehaviour
    {
        /// <summary>How the frame shines, by the vertex colour's red the shader reads.</summary>
        public enum Look
        {
            Plain,
            Gold,
            Rainbow,
        }

        private RawImage image;

        public RawImage Image => image != null ? image : image = GetComponent<RawImage>();

        /// <summary>The look of <paramref name="quality"/>: S rainbow, A gold, B and C plain.</summary>
        public static Look LookOf(ItemQuality quality) =>
            quality switch
            {
                ItemQuality.S => Look.Rainbow,
                ItemQuality.A => Look.Gold,
                _ => Look.Plain,
            };

        /// <summary>The vertex colour's red for <paramref name="look"/>: 1 plain, 0.5 gold, 0 rainbow.</summary>
        public static float RedOf(Look look) => 1f - (int)look * 0.5f;

        /// <summary>
        /// Shows the frame in the look of <paramref name="quality"/>, keeping the frame's alpha.
        /// Call it after anything that sets the frame's colour.
        /// </summary>
        public void Show(ItemQuality quality)
        {
            var color = Image.color;
            Image.color = new Color(
                RedOf(LookOf(quality)),
                CardFoil.SeedOf(transform),
                1f,
                color.a
            );
        }
    }
}
