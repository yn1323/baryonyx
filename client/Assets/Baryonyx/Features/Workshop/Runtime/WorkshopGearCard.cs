using System;
using Baryonyx.UI.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// A piece of equipment shown as a card on the enhance screen, beside the skills' deck cards:
    /// a glass plate whose rim takes the quality's colour, the art with the quality's sparkle,
    /// the name, the quality and the effect at that quality.
    /// </summary>
    [Serializable]
    public sealed class WorkshopGearCard
    {
        public GameObject Root;
        public Image Rim;
        public RawImage Art;
        public CardFoil Foil;
        public TMP_Text Name;
        public TMP_Text Quality;
        public TMP_Text Effect;
    }
}
