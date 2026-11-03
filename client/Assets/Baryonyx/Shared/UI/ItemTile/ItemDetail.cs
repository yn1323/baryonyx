using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// The detail face of one thing (doc/rules/ui-design.md の装備の見せ方): from the top, the
    /// quality's letter large with the multiplier, the art, the name, the stars, the effect and
    /// the attributes, in a column that closes up the lines a thing does not have. A screen puts
    /// its own things (the count held, the materials, the price) under it. ItemDetailAssets bakes
    /// it; the shop, the library and the detail window use the same one.
    /// </summary>
    public sealed class ItemDetail : MonoBehaviour
    {
        public TMP_Text Quality;

        // 絵と、絵の後ろの光。絵のない物は光ごと隠す。
        public GameObject ArtStage;
        public RawImage Art;
        public float ArtBox = 256f;

        public TMP_Text Name;
        public TMP_Text Stars;
        public TMP_Text Effect;
        public ElementIconRow Elements;

        /// <summary>Shows <paramref name="face"/>; none clears the face.</summary>
        public void Show(ItemFace face)
        {
            Set(Quality, face != null && face.HasQuality ? ItemText.DetailQuality(face.Quality, face.Multiplier) : "");
            if (ArtStage != null)
                ArtStage.SetActive(face != null && face.Art != null);
            if (face != null)
                face.PaintArt(Art, ArtBox);
            Set(Name, face?.Name);
            Set(Stars, face != null ? ItemText.Stars(face.Rarity) : "");
            Set(Effect, face?.Effect);
            if (Elements != null)
            {
                Elements.Show(face?.Elements);
                Elements.gameObject.SetActive(Elements.Any);
            }
        }

        // 書くことのない行は隠して、列を詰める。
        private static void Set(TMP_Text label, string text)
        {
            if (label == null)
                return;
            label.text = text ?? "";
            label.gameObject.SetActive(label.text.Length > 0);
        }
    }
}
