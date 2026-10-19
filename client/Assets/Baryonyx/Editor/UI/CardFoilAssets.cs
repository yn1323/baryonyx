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
        /// place, showing <paramref name="rarity"/>.
        /// </summary>
        public static CardFoil Add(RawImage art, int rarity = CardFoil.MinRarity)
        {
            var source = art.rectTransform;
            var go = new GameObject(
                "Foil",
                typeof(RectTransform),
                typeof(RawImage),
                typeof(CardFoil)
            );
            go.layer = art.gameObject.layer;
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
            foil.Show(art.texture, art.uvRect, rarity);
            return foil;
        }
    }
}
