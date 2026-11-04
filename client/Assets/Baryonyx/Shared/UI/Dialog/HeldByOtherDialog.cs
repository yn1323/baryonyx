using System;

namespace Baryonyx.UI
{
    /// <summary>
    /// Asks before taking a skill or a piece of equipment someone else wears
    /// (doc/features/screens/loadout.md#ほかの人が付けている物): 「〇〇が装備していますが、変更しますか？」 with
    /// 「やめる」 and 「変更する」. Moving one's own thing to another slot does not ask.
    /// </summary>
    public static class HeldByOtherDialog
    {
        public const string ChangeChoice = "変更する";
        public const string CancelChoice = "やめる";

        public static string Question(string holder) =>
            $"{holder}が装備していますが、変更しますか？";

        /// <summary>
        /// Runs <paramref name="change"/> at once when no one else holds the thing (or there is no
        /// window to ask in); otherwise asks on <paramref name="dialog"/> first.
        /// </summary>
        public static void Ask(GameDialog dialog, string holder, Action change)
        {
            if (string.IsNullOrEmpty(holder) || dialog == null)
            {
                change?.Invoke();
                return;
            }
            dialog.Show(
                Question(holder),
                null,
                null,
                dialog.Hide,
                DialogStyle.Row,
                new DialogChoice(CancelChoice, dialog.Hide),
                new DialogChoice(
                    ChangeChoice,
                    () =>
                    {
                        dialog.Hide();
                        change?.Invoke();
                    },
                    role: ButtonRole.Primary
                )
            );
        }
    }
}
