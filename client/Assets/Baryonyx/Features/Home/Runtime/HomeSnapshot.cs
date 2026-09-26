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
    /// step source replaces the steps and runes with the values saved on the server.
    /// </summary>
    public sealed class HomeSnapshot
    {
        public HomeStepLink StepLink = HomeStepLink.Linked;
        public int Steps;

        // 歩数を取得済みか。取得元があるHomeでは、最初の取得に成功するまで偽になる。
        public bool StepsKnown = true;
        public bool StepSyncing;
        public int DailyGoal;
        public int WeeklyDone;
        public int WeeklyTarget;
        public long Runes;

        // 所持ルーンを取得済みか。取得元があるHomeでは、最初の取得に成功するまで偽になる。
        public bool RunesKnown = true;
        public string DestinationName = "";
        public string DestinationFloor = "";
        public DateTime Today = DateTime.Today;
    }
}
