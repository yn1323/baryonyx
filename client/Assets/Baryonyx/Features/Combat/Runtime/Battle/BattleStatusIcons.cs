namespace Baryonyx.Combat
{
    /// <summary>
    /// The icon a status shows under a character's HP bar (doc/features/screens/battle-deck.md,
    /// 状態のアイコン). A status that raises or lowers a stat shows the stat's picture with a green
    /// rising or red falling triangle, however much it changes it.
    /// </summary>
    public enum BattleStatusIcon
    {
        None,
        AttackUp,
        DefenseUp,
        SpeedUp,
        DefenseDown,
        Paralysis,
        Chill,
        Freeze,
        Bleed,
        Burn,
        Poison,
        Curse,
        BlastSigil,
    }

    public static class BattleStatusIcons
    {
        /// <summary>The icons in the order they stand in the row: the boosts first, then the rest.</summary>
        public static readonly BattleStatusIcon[] Order =
        {
            BattleStatusIcon.AttackUp,
            BattleStatusIcon.DefenseUp,
            BattleStatusIcon.SpeedUp,
            BattleStatusIcon.DefenseDown,
            BattleStatusIcon.Paralysis,
            BattleStatusIcon.Chill,
            BattleStatusIcon.Freeze,
            BattleStatusIcon.Bleed,
            BattleStatusIcon.Burn,
            BattleStatusIcon.Poison,
            BattleStatusIcon.Curse,
            BattleStatusIcon.BlastSigil,
        };

        /// <summary>
        /// The icon of a status, or <see cref="BattleStatusIcon.None"/> for one without its own
        /// yet (those show only in the list of statuses). プロテクト counts as defense up.
        /// </summary>
        public static BattleStatusIcon Of(CardStatus status) =>
            status switch
            {
                CardStatus.AttackUp => BattleStatusIcon.AttackUp,
                CardStatus.Protect => BattleStatusIcon.DefenseUp,
                CardStatus.SpeedUp => BattleStatusIcon.SpeedUp,
                CardStatus.Vulnerable => BattleStatusIcon.DefenseDown,
                CardStatus.Paralysis => BattleStatusIcon.Paralysis,
                CardStatus.Chill => BattleStatusIcon.Chill,
                CardStatus.Freeze => BattleStatusIcon.Freeze,
                CardStatus.Bleed => BattleStatusIcon.Bleed,
                CardStatus.Burn => BattleStatusIcon.Burn,
                CardStatus.Poison => BattleStatusIcon.Poison,
                CardStatus.Curse => BattleStatusIcon.Curse,
                CardStatus.BlastSigil => BattleStatusIcon.BlastSigil,
                _ => BattleStatusIcon.None,
            };

        /// <summary>The .aseprite the icon is drawn in (Features/Combat/UI/Art/Statuses/).</summary>
        public static string ArtName(BattleStatusIcon icon) => "Status" + icon;
    }
}
