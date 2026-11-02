using Baryonyx.Editor.Art;
using UnityEngine;

namespace Baryonyx.Combat.Editor
{
    // 属性の印のドット絵（doc/rules/ui-design.md#属性とコストの印）。カードと同じ12×12ドットの
    // .aseprite を、装備の属性・商会・地図の窓など、カードの外で属性を出す所からも読む。
    public static class AttributeIconArt
    {
        public const string Folder = "Assets/Baryonyx/Shared/Art/Attributes";

        public static string IconPath(CardElement element) => $"{Folder}/Element{element}.aseprite";

        // 属性ごとに並べる印。無属性には印を出さないため null にする（2026-11-03、ユーザーの指示）。
        public static Texture TextureOf(CardElement element) =>
            element == CardElement.None ? null : SpriteOf(element).texture;

        public static Sprite SpriteOf(CardElement element)
        {
            if (element == CardElement.None)
                return null;
            ArtAssets.ImportDrawn(IconPath(element));
            return ArtAssets.LoadSprite(IconPath(element));
        }
    }
}
