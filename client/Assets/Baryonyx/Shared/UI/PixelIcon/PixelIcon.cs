using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// The box an icon was laid out in, kept the first time a picture is put in it, so pictures
    /// of other sizes can be fitted to it again.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PixelIconBox : MonoBehaviour
    {
        public Vector2 Box;
    }

    /// <summary>
    /// Puts dot art into an icon at a whole multiple of its size: the largest that fits the box
    /// the icon was laid out in (doc/art/direction.md, sizes). A 24×24 slot icon in a 72 box is
    /// drawn at 3×, a 32×32 item at 2× (64), so one dot is always the same number of pixels.
    /// </summary>
    public static class PixelIcon
    {
        public static void Show(Image image, Sprite sprite)
        {
            if (image == null)
                return;
            image.sprite = sprite;
            image.enabled = sprite != null;
            if (sprite == null)
                return;
            var rect = image.rectTransform;
            var box = image.GetComponent<PixelIconBox>();
            if (box == null)
            {
                box = image.gameObject.AddComponent<PixelIconBox>();
                box.Box = rect.rect.size;
            }
            var size = sprite.rect.size;
            if (size.x <= 0f || size.y <= 0f || box.Box.x <= 0f || box.Box.y <= 0f)
                return;
            float scale = Mathf.Max(
                1f,
                Mathf.Floor(Mathf.Min(box.Box.x / size.x, box.Box.y / size.y))
            );
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x * scale);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y * scale);
        }
    }
}
