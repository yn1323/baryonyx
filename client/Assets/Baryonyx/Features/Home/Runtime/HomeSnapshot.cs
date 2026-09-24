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
        SyncSteps,
        Settings,
        Party,
        Equipment,
        Summon,
        Goals,
        WorldMap,
        Resume,
    }

    /// <summary>
    /// The values the home screen shows. The mock builds it from a fixed asset, and the
    /// step source replaces the step values with the ones saved on the server.
    /// </summary>
    public sealed class HomeSnapshot
    {
        public HomeStepLink StepLink = HomeStepLink.Linked;
        public int Steps;
        public bool StepSyncing;
        public int DailyGoal;
        public int WeeklyDone;
        public int WeeklyTarget;
        public int Runes;
        public string DestinationName = "";
        public string DestinationFloor = "";
        public DateTime Today = DateTime.Today;
    }
}
