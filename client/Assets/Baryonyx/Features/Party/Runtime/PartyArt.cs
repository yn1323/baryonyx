using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Party
{
    /// <summary>How the formation's screens draw a character's 64x64 battle sprite.</summary>
    public static class PartyArt
    {
        // 64×64の戦闘のドット絵のうち、立ち姿が収まる範囲（ドット、絵の左上が原点）。
        // 周りの透明な余白を切り、冒険者のタイルを大きく並べられるようにする。
        // 左右の余白を同じだけ切るため、左右を反転しても同じ範囲になる。
        public static readonly RectInt Figure = new(8, 12, 48, 52);

        public static string LevelText(int level) => $"Lv {level}";

        /// <summary>
        /// Draws the whole 64x64 sprite, or only the <see cref="Figure"/> without the clear
        /// margin, with the member's tint and facing. The pixel-perfect image sizes itself from
        /// the texture and the crop.
        /// </summary>
        public static void Paint(RawImage image, PartyMember member, bool trim)
        {
            image.texture = member?.Art;
            image.color = member != null ? member.Tint : Color.white;
            if (member == null || member.Art == null)
                return;
            float width = member.Art.width;
            float height = member.Art.height;
            var uv = trim
                ? new Rect(
                    Figure.x / width,
                    1f - Figure.yMax / height,
                    Figure.width / width,
                    Figure.height / height
                )
                : new Rect(0f, 0f, 1f, 1f);
            if (member.Flip)
                uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
            image.uvRect = uv;
        }

        /// <summary>
        /// Fills the frame with the illustration without stretching it: the middle of the
        /// picture, cut at the sides or the top and bottom.
        /// </summary>
        public static void Cover(RawImage image, Texture texture)
        {
            image.texture = texture;
            image.enabled = texture != null;
            if (texture == null)
                return;
            var size = image.rectTransform.rect.size;
            float frame = size.y > 0 ? size.x / size.y : 1f;
            float picture = texture.width / (float)texture.height;
            image.uvRect =
                picture > frame
                    ? new Rect((1f - frame / picture) / 2f, 0f, frame / picture, 1f)
                    : new Rect(0f, (1f - picture / frame) / 2f, 1f, picture / frame);
        }
    }
}
