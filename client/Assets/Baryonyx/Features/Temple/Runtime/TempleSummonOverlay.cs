using Baryonyx.UI.GuideMenu;
using UnityEngine;

namespace Baryonyx.Temple
{
    /// <summary>
    /// The temple's windows over the whole screen, the results and the rates: "back" and the
    /// device back key close the open one first.
    /// </summary>
    public sealed class TempleSummonOverlay : MonoBehaviour, IGuideBackHandler
    {
        public TempleSummonResults Results;
        public TempleSummonRates Rates;

        public bool HandleBack()
        {
            if (Rates != null && Rates.IsOpen)
            {
                Rates.Hide();
                return true;
            }
            return Results != null && Results.HandleBack();
        }
    }
}
