using System.Collections.Generic;
using Baryonyx.UI.GuideMenu;
using UnityEngine;

namespace Baryonyx.Training
{
    /// <summary>
    /// The breadcrumb and the back of a panel another screen lends to the adventurer's tabs
    /// (the trading house's enhance as the 「強化」 tab): 「冒険者 › アリア」 as the other tabs
    /// show, and back to where the tabs were opened. The panel tells it whom it shows.
    /// </summary>
    public sealed class PartySheetTrail : MonoBehaviour, IGuideBreadcrumb, IGuideBackHandler
    {
        public GuideMenuView Guide;

        private string person;

        /// <summary>The name of the person shown, or null until read.</summary>
        public string Person
        {
            get => person;
            set
            {
                if (person == value)
                    return;
                person = value;
                if (Guide != null)
                    Guide.RefreshTrail();
            }
        }

        public IReadOnlyList<string> Trail => TrainingSession.Trail(Guide, person);

        public bool HandleBack() => TrainingSession.LeavePage(Guide);
    }
}
