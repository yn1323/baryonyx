using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// One kind of thing brought back on <see cref="AdventureResultPanel"/>: the art on the left,
    /// the quality's letter, the stars and the count beside it, and the name along the bottom.
    /// </summary>
    public sealed class AdventureResultCell : MonoBehaviour
    {
        public CanvasGroup Group;
        public Image Art;
        public TMP_Text Quality;
        public TMP_Text Stars;
        public TMP_Text Count;
        public TMP_Text Name;
    }
}
