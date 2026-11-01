using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// The card of one thing the player holds, finds or can get (doc/rules/ui-design.md の持ち物の札):
    /// the shop's goods and materials, the library's entries, the treasures, the summon's and the
    /// adventure's results. Every list uses this one type, and the parts sit in the same places on
    /// every card: the quality's letter at the top left, the count at the bottom right, a mark at
    /// the top right, the name along the bottom. A part a card does not have is null.
    /// </summary>
    public sealed class ItemTile : MonoBehaviour
    {
        // 何の札か（図鑑やお宝の定義のID）。並べ直しても、どの物の札かを見分ける。
        public string Id = "";

        public GlassCard Card;

        // 札ごと少しずつ出す一覧（冒険の結果）の、札の透け方。
        public CanvasGroup Group;
        public RawImage Art;
        public TMP_Text Name;

        // 左上の品質の文字、☆、右下の数。
        public TMP_Text Quality;
        public TMP_Text Stars;
        public TMP_Text Count;

        // 図鑑の番号、値段、名前の下の1行（効果・品質など）。
        public TMP_Text Number;
        public TMP_Text Price;
        public TMP_Text Detail;

        // 右上の印（使っている物の「E」、新しく載った「NEW」）と、まだ見つけていない物の「？」。
        public GameObject Mark;
        public GameObject Unknown;

        /// <summary>
        /// Shows <paramref name="sprite"/> as the art (an icon cut from an atlas shows only its
        /// own part); none hides the art.
        /// </summary>
        public void PaintSprite(Sprite sprite)
        {
            if (Art == null)
                return;
            Art.enabled = sprite != null;
            if (sprite == null)
                return;
            var texture = sprite.texture;
            var rect = sprite.textureRect;
            Art.texture = texture;
            Art.uvRect = new Rect(
                rect.x / texture.width,
                rect.y / texture.height,
                rect.width / texture.width,
                rect.height / texture.height
            );
        }
    }
}
