using System;
using System.Collections.Generic;
using Baryonyx.Inventory;
using Baryonyx.StepBonus;
using Baryonyx.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The adventure's dialogs, shared by the exploration and the battle. The flows call them
    /// while playing, and the screens call them with <see cref="AdventureDialogSamples"/> to show
    /// each dialog while the editor is stopped (doc/rules/ui-design.md#停止中の表示の切り替え).
    /// </summary>
    public static class AdventureDialogs
    {
        public const string RetryChoice = "もう一度";
        public const string RetryBody = "通信を確かめて、もう一度お試しください。";
        public const string TreasureTitle = "宝箱を開けた！";
        public const string VictoryTitle = "勝利！";
        public const string DefeatTitle = "全滅してしまった…";
        public const string ToResultChoice = "つぎへ";

        public static void LoadFailed(GameDialog dialog, Action retry, Action home) =>
            dialog.Show(
                AdventureTexts.LoadFailed,
                RetryBody,
                null,
                null,
                new DialogChoice(RetryChoice, retry),
                new DialogChoice(AdventureTexts.HomeChoice, home)
            );

        public static void SaveFailed(GameDialog dialog, Action retry) =>
            dialog.Show(
                AdventureTexts.SaveFailed,
                RetryBody,
                null,
                null,
                new DialogChoice(RetryChoice, retry)
            );

        public static void Treasure(
            GameDialog dialog,
            StepBonusMockData bonuses,
            IReadOnlyList<AdventureDrop> drops,
            Action close,
            ItemArt art = null
        ) =>
            dialog.Show(
                TreasureTitle,
                AdventureTexts.DropsBody(bonuses, drops),
                AdventureTexts.DropIcon(bonuses, drops, art),
                close,
                new DialogChoice(AdventureTexts.NextChoice, close)
            );

        // 手に入れた物は、ダイアログの前にドロップの演出の札で1つずつ見せるため、本文には書かない。
        // 冒険が終わった勝利（最奥の主を倒した）は、「つぎへ」で結果へ進む。
        public static void Victory(GameDialog dialog, bool ended, Action next) =>
            dialog.Show(
                VictoryTitle,
                null,
                null,
                null,
                new DialogChoice(ended ? ToResultChoice : AdventureTexts.NextChoice, next)
            );

        // 復活と帰還を同じ重みで並べる。どちらを選んでも手に入れた物は残る。
        public static void Revive(
            GameDialog dialog,
            int cost,
            long runes,
            bool canRevive,
            Action revive,
            Action giveUp
        ) =>
            dialog.Show(
                DefeatTitle,
                AdventureTexts.ReviveBody(cost, runes),
                null,
                null,
                new DialogChoice(AdventureTexts.ReviveChoice(cost), revive, canRevive),
                new DialogChoice(AdventureTexts.ReturnChoice, giveUp)
            );

        /// <summary>
        /// The menu: suspend to Home, quit, or stay. The exploration and the battle word the
        /// body and the last choice differently.
        /// </summary>
        public static void Menu(
            GameDialog dialog,
            AdventureRun run,
            string body,
            string stayChoice,
            Action stay,
            Action suspend,
            Action quit
        ) =>
            dialog.Show(
                AdventureTexts.MenuTitle,
                AdventureCatalog.Location(run) + "\n" + body,
                null,
                stay,
                new DialogChoice(AdventureTexts.SuspendChoice, suspend),
                new DialogChoice(AdventureTexts.QuitChoice, quit),
                new DialogChoice(stayChoice, stay)
            );

        public static void ConfirmQuit(GameDialog dialog, Action back, Action quit, Action goOn) =>
            dialog.Show(
                AdventureTexts.QuitTitle,
                AdventureTexts.QuitBody,
                null,
                back,
                new DialogChoice(AdventureTexts.QuitChoice, quit),
                new DialogChoice(AdventureTexts.GoOnChoice, goOn)
            );

        /// <summary>The end of an adventure: how it ended and what it brought back, then Home.</summary>
        public static void Result(
            GameDialog dialog,
            StepBonusMockData bonuses,
            AdventureResult result,
            Action home
        ) =>
            dialog.Show(
                AdventureCatalog.StatusTitle(result.Status),
                AdventureTexts.ResultBody(bonuses, result),
                null,
                null,
                new DialogChoice(AdventureTexts.HomeChoice, home)
            );
    }
}
