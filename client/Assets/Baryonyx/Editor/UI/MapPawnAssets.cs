using Baryonyx.Editor.Art;
using Baryonyx.UI;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Editor.UI
{
    /// <summary>
    /// Builds the wooden pawn that stands for the party on the exploration's map and the
    /// highway's world map (doc/features/stage-progression.md): the picture standing on its
    /// feet with a soft shadow under them, hopping from square to square (<see cref="MapPawnHop"/>).
    /// The picture is smooth (not pixel art), so it is drawn with bilinear filtering.
    /// </summary>
    public static class MapPawnAssets
    {
        /// <summary>
        /// The pawn under <paramref name="parent"/>, its feet at the parent's origin and
        /// <paramref name="height"/> tall (design coordinates).
        /// </summary>
        public static RectTransform Build(RectTransform parent, string name, float height)
        {
            var texture = ArtAssets.ImportTexture(UiArt.MapPawnPath, FilterMode.Bilinear);
            var body = Rect(name, parent);
            Place(body, Vector2.zero, Vector2.zero);
            float width = height * texture.width / texture.height;
            var shadow = Picture(
                body,
                "Shadow",
                ArtAssets.LoadSprite(UiArt.ShadowPath),
                new Vector2(0, 2),
                new Vector2(width * 0.95f, width * 0.24f),
                0.5f
            );
            var picture = Rect("Pawn", body);
            picture.anchorMin = picture.anchorMax = new Vector2(0.5f, 0.5f);
            picture.pivot = new Vector2(0.5f, 0f);
            picture.anchoredPosition = Vector2.zero;
            picture.sizeDelta = new Vector2(width, height);
            var image = picture.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            // マスからマスへ跳ねる動き。
            var hop = body.gameObject.AddComponent<MapPawnHop>();
            hop.Picture = picture;
            hop.Shadow = shadow.rectTransform;
            return body;
        }
    }
}
