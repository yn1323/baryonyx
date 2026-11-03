namespace Baryonyx.Combat.Presentation
{
    public sealed partial class BattleInspectView
    {
        /// <summary>
        /// The device's back key (doc/rules/ui-design.md#操作の決まり): closes the pile's list, then
        /// the status list. The pick of a unique skill's next stage stays until one is picked, and
        /// takes the key. False when none is open, so the adventure's overlay opens its menu.
        /// </summary>
        public bool HandleBack()
        {
            if (Choice != null && Choice.IsOpen)
                return true;
            if (PileListOpen)
            {
                ClosePileList();
                return true;
            }
            if (StatusListOpen)
            {
                HideStatusList();
                return true;
            }
            return false;
        }
    }
}
