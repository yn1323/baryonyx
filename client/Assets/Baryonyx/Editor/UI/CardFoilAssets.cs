using Baryonyx.Inventory;
using Baryonyx.UI.Cards;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Editor.UI
{
    /// <summary>
    /// Builds the sparkle over a card's art (doc/art/direction.md, card sparkle) for the screens'
    /// generators: one shared material of the CardFoil shader, and a layer laid right over the art.
    /// </summary>
    public static class CardFoilAssets
    {
        public const string ShaderName = "Baryonyx/UI/CardFoil";
        public const string MaterialPath = "Assets/Baryonyx/Shared/UI/Cards/CardFoil.mat";

        /// <summary>The shared material, made the first time it is asked for.</summary>
        public static Material Material()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;
            material = new Material(Shader.Find(ShaderName)) { name = "CardFoil" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// Lays the sparkle over <paramref name="art"/>: a sibling just after it, the same size and
        /// place, showing <paramref name="quality"/> in <paramref name="style"/>.
        /// </summary>
        public static CardFoil Add(RawImage art, ItemQuality quality = ItemQuality.C, int style = 0)
        {
            var foil = Lay(art.rectTransform);
            foil.Show(art.texture, art.uvRect, quality, style);
            return foil;
        }

        /// <summary>
        /// Lays the sparkle over an icon drawn as a sprite (equipment, ACT bonuses), the same way
        /// as over a card's art.
        /// </summary>
        public static CardFoil Add(Image art, ItemQuality quality = ItemQuality.C, int style = 0)
        {
            var foil = Lay(art.rectTransform);
            foil.Show(art.sprite, quality, style);
            return foil;
        }

        // 絵のすぐ後ろの兄弟として、同じ大きさ・位置にキラキラの層を置く。
        private static CardFoil Lay(RectTransform source)
        {
            var go = new GameObject(
                "Foil",
                typeof(RectTransform),
                typeof(RawImage),
                typeof(CardFoil)
            );
            go.layer = source.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(source.parent, false);
            rect.SetSiblingIndex(source.GetSiblingIndex() + 1);
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.anchoredPosition = source.anchoredPosition;
            rect.sizeDelta = source.sizeDelta;
            rect.localScale = source.localScale;
            var foil = go.GetComponent<CardFoil>();
            foil.Image.material = Material();
            return foil;
        }
    }
}
