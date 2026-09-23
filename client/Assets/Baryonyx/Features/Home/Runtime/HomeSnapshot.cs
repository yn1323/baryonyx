using System;

namespace Baryonyx.Home
{
    public enum HomeStepLink
    {
        Linked,
        Unlinked,
    }

    public enum HomeAction
    {
        ClaimRunes,
        Settings,
        Party,
        Equipment,
        Summon,
        Goals,
        WorldMap,
        Resume,
    }

    /// <summary>
    /// The values the home screen shows. The mock builds it from a fixed asset; real
    /// health and reward data will build the same snapshot later.
    /// </summary>
    public sealed class HomeSnapshot
    {
        public HomeStepLink StepLink = HomeStepLink.Linked;
        public int Steps;
        public int DailyGoal;
        public int WeeklyDone;
        public int WeeklyTarget;
        public int Runes;
        public string DestinationName = "";
        public string DestinationFloor = "";
        public DateTime Today = DateTime.Today;
    }
}
