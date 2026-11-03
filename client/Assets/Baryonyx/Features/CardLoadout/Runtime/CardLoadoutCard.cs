using Baryonyx.Combat.Presentation;
using Baryonyx.Inventory;
using Baryonyx.UI;
using TMPro;
using UnityEngine;

namespace Baryonyx.CardLoadout
{
    /// <summary>
    /// One battle card on the card skills tab (doc/features/screens/loadout.md#スキル): a skill,
    /// a stage of a unique skill's tree or an item, drawn as in battle
    /// (<see cref="BattleInspectCardView"/>) on a <see cref="GlassCard"/> that takes the tap and
    /// turns over when its slot is given something else. The generator bakes the parts; copies
    /// made at run time keep them.
    /// </summary>
    public sealed class CardLoadoutCard : MonoBehaviour
    {
        // 引いてから表になるまでの暗さ（押せない・まだ解放していない）。
        public const float ShadeDarkness = 0.6f;

        public GlassCard Glass;
        public BattleInspectCardView Face;

        // 右上の印（「E」「所持 3」）の札と文字。
        public GameObject MarkPlate;
        public TMP_Text Mark;

        // 効果の欄の真ん中の文字（「空き」「Lv 30で解放」）。
        public TMP_Text Note;

        // 取得していない段階の封印（伏せたカードの上）。
        public GameObject Seal;

        // 空いた枠：戦闘のカードの代わりに出す点線のカード（真ん中に「＋」、その下に「空き」）。
        public GameObject Empty;

        // 押したときに付け替えるキー（PartyCardKey の形）か、ツリーの段階の番号。
        [HideInInspector]
        public string Id = "";

        [HideInInspector]
        public int Stage = -1;

        // 挿絵の切り出し。アイテムの絵（正方形）は上下を少し切って、つぶさずに出す。
        private Rect artRect;
        private bool artRectRead;

        /// <summary>
        /// Shows <paramref name="card"/> face up with its frame and element: dark and not
        /// pressable unless <paramref name="playable"/>, with <paramref name="note"/> in place of
        /// the effect when given.
        /// </summary>
        public void Show(
            CardLoadoutCardState card,
            Texture art,
            Texture frame,
            Texture element,
            bool playable = true,
            string note = null
        )
        {
            FaceUp();
            FitArt(art, card.ItemId != null);
            Face.Show(
                card.Name,
                "",
                card.KindLine,
                string.IsNullOrEmpty(note) ? card.Description : "",
                art,
                frame,
                element,
                card.Cost,
                card.Quality,
                card.FoilStyle
            );
            Face.SetPlayable(playable, ShadeDarkness);
            // 暗くしてもコストは赤くしない（エネルギーが足りないのではない）。
            Face.CostDigit.color = Color.white;
            Face.CostDigit.transform.parent.gameObject.SetActive(true);
            SetNote(note);
        }

        /// <summary>
        /// An empty slot: the dashed card with a plus and 「空き」 in place of the battle card
        /// (the guide screens' empty places, doc/features/screens/guide.md#すりガラスのカード).
        /// </summary>
        public void ShowEmpty()
        {
            Face.Back.gameObject.SetActive(false);
            Face.gameObject.SetActive(false);
            if (Empty != null)
                Empty.SetActive(true);
            SetMark(null, false);
            SetNote(null);
        }

        /// <summary>Lays the card face down (still reading, or a stage not acquired).</summary>
        public void ShowBack(bool seal)
        {
            ShowFace();
            Face.Back.gameObject.SetActive(true);
            if (Seal != null)
                Seal.SetActive(seal);
            SetMark(null, false);
            SetNote(null);
        }

        /// <summary>The mark at the top right: gold, or grey when another person holds the card.</summary>
        public void SetMark(string text, bool other)
        {
            bool on = !string.IsNullOrEmpty(text);
            if (MarkPlate != null)
                MarkPlate.SetActive(on);
            if (Mark != null)
            {
                Mark.text = text ?? "";
                Mark.color = other ? CardLoadoutView.OtherMark : UiPalette.Gold;
            }
        }

        private void FaceUp()
        {
            ShowFace();
            Face.Back.gameObject.SetActive(false);
            foreach (var band in Face.Bands)
                band.gameObject.SetActive(true);
            if (Seal != null)
                Seal.SetActive(false);
        }

        private void ShowFace()
        {
            Face.gameObject.SetActive(true);
            if (Empty != null)
                Empty.SetActive(false);
        }

        private void SetNote(string text)
        {
            if (Note == null)
                return;
            Note.text = text ?? "";
            Note.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private void FitArt(Texture art, bool item)
        {
            if (!artRectRead)
            {
                artRect = Face.Art.uvRect;
                artRectRead = true;
            }
            if (!item || art == null)
            {
                Face.Art.uvRect = artRect;
                return;
            }
            var size = Face.Art.rectTransform.rect.size;
            float height = size.x > 0f ? Mathf.Clamp01(size.y / size.x) : 1f;
            Face.Art.uvRect = new Rect(0f, (1f - height) / 2f, 1f, height);
        }
    }
}
