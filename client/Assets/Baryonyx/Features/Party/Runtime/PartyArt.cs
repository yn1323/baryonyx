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

        // 頭から胸までの上半身（ドット、絵の左上が原点）。冒険者のタブの左の列の札に大きく出す。
        public static readonly RectInt Bust = new(14, 12, 36, 32);

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
            image.uvRect = Crop(
                member,
                trim ? Figure : new RectInt(0, 0, member.Art.width, member.Art.height)
            );
        }

        /// <summary>Draws only the <see cref="Bust"/> of the member's sprite, with their tint and facing.</summary>
        public static void PaintBust(RawImage image, PartyMember member)
        {
            image.texture = member?.Art;
            image.color = member != null ? member.Tint : Color.white;
            if (member != null && member.Art != null)
                image.uvRect = Crop(member, Bust);
        }

        // 絵のうち dots の範囲を出す uvRect。左右を反転する人は反転する（範囲は左右の中央にあるので同じ所）。
        private static Rect Crop(PartyMember member, RectInt dots)
        {
            float width = member.Art.width;
            float height = member.Art.height;
            var uv = new Rect(
                dots.x / width,
                1f - dots.yMax / height,
                dots.width / width,
                dots.height / height
            );
            return member.Flip ? new Rect(uv.xMax, uv.y, -uv.width, uv.height) : uv;
        }

        /// <summary>
        /// Stands the member where a scene places the party (Home, the adventure's map): the
        /// whole 64x64 sprite in the member's tint, facing the way the image is set. A stand-in's
        /// flip is left out, so everyone faces where the scene turns them.
        /// </summary>
        public static void Stand(RawImage image, PartyMember member)
        {
            if (image == null || member == null)
                return;
            image.texture = member.Art;
            image.color = member.Tint;
            image.uvRect = new Rect(0f, 0f, 1f, 1f);
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
