using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// A list's cards lying face down while the list reads what to show for the first time
    /// (doc/features/screens/guide.md#カードの裏面): <see cref="Cover"/> lays them down in the
    /// places the list fills, and <see cref="Reveal"/>, once the list shows what it read, turns
    /// every place over at once: the cards then shown face up, and the places left empty to an
    /// empty face that fades away when every card has turned. A list showing what it read before
    /// never covers, so redrawing it flips nothing.
    /// </summary>
    public sealed class GlassCardCover
    {
        private readonly List<GlassCardBack> covered = new();
        private readonly List<GlassCardBack> leaving = new();

        public bool Covered => covered.Count > 0;

        /// <summary>Shows <paramref name="cards"/> face down. The list hides its other cards.</summary>
        public void Cover(IEnumerable<GameObject> cards)
        {
            Clear();
            foreach (var card in cards)
            {
                if (card == null)
                    continue;
                card.SetActive(true);
                var back = card.GetComponent<GlassCardBack>();
                if (back == null)
                    continue;
                back.ShowFaceDown();
                covered.Add(back);
            }
        }

        /// <summary>
        /// When covered, turns <paramref name="shown"/> face up and <paramref name="empty"/> (the
        /// places the list leaves empty, after the shown cards) to an empty face that fades away,
        /// all together (ユーザーの指定、2026-10-27), and every other card laid down at once. When
        /// not covered, only stops the fading of a card shown again.
        /// </summary>
        public void Reveal(IEnumerable<GameObject> shown, IEnumerable<GameObject> empty = null)
        {
            var shownBacks = new List<GlassCardBack>();
            foreach (var card in shown)
            {
                var back = card != null ? card.GetComponent<GlassCardBack>() : null;
                if (back != null && card.activeInHierarchy && !shownBacks.Contains(back))
                    shownBacks.Add(back);
            }
            if (!Covered)
            {
                // 消えていく途中のカードに、別の物をまた出したら、消すのをやめる。
                foreach (var back in shownBacks)
                    if (leaving.Remove(back))
                        back.ShowFaceUp();
                return;
            }
            foreach (var back in shownBacks)
            {
                // 伏せた場所に来たカードが別のカードでも、同じ裏からめくれて見える。
                back.ShowFaceDown();
                back.FlipUp();
            }
            leaving.Clear();
            if (empty != null)
                foreach (var card in empty)
                {
                    var back = card != null ? card.GetComponent<GlassCardBack>() : null;
                    if (back == null || shownBacks.Contains(back) || leaving.Contains(back))
                        continue;
                    back.FlipAway();
                    leaving.Add(back);
                }
            foreach (var back in covered)
                if (back != null && !shownBacks.Contains(back) && !leaving.Contains(back))
                    back.ShowFaceUp();
            covered.Clear();
        }

        /// <summary>Turns every covered card face up at once (the read failed, or the list closes).</summary>
        public void Clear()
        {
            foreach (var back in covered)
                if (back != null)
                    back.ShowFaceUp();
            covered.Clear();
            foreach (var back in leaving)
                if (back != null && back.Leaving)
                    back.gameObject.SetActive(false);
            leaving.Clear();
        }
    }
}
