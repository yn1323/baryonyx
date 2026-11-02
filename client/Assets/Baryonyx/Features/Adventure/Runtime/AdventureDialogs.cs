using System;
using System.Collections.Generic;
using Baryonyx.Inventory;
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
            IReadOnlyList<AdventureDrop> drops,
            Action close,
            ItemArt art = null
        ) =>
            dialog.Show(
                TreasureTitle,
                AdventureTexts.DropsBody(drops),
                AdventureTexts.DropIcon(drops, art),
                close,
                new DialogChoice(AdventureTexts.NextChoice, close)
            );

        public const string OpenChoice = "開ける";
        public const string PassChoice = "通り過ぎる";
        public const string BackChoice = "もどる";

        // 手に入れた物は、ダイアログの前にドロップの演出の札で1つずつ見せるため、本文には書かない。
        // 冒険が終わった勝利（最奥の主を倒した）は、これを挟まずに冒険の結果へ移る。
        // ボタンを置かず、画面のどこを押しても先へ進む（2026-11-03、ユーザー）。
        public static void Victory(GameDialog dialog, Action next) =>
            dialog.Show(
                VictoryTitle,
                null,
                null,
                null,
                DialogStyle.TapAnywhere,
                new DialogChoice(AdventureTexts.NextChoice, next)
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
        /// The menu: suspend to Home or quit, and the × at the top right to stay. The body is the
        /// location, then the battle adds what suspending mid-battle does (the exploration adds
        /// nothing).
        /// </summary>
        public static void Menu(
            GameDialog dialog,
            AdventureRun run,
            string body,
            Action stay,
            Action suspend,
            Action quit
        ) =>
            dialog.Show(
                AdventureTexts.MenuTitle,
                string.IsNullOrEmpty(body)
                    ? AdventureCatalog.Location(run)
                    : AdventureCatalog.Location(run) + "\n" + body,
                null,
                stay,
                DialogStyle.Cross,
                new DialogChoice(AdventureTexts.SuspendChoice, suspend),
                new DialogChoice(AdventureTexts.QuitChoice, quit)
            );

        // 確かめる窓と同じく、左に「つづける」、右に決める「冒険をやめる」を横に並べ、×でメニューへ戻る。
        public static void ConfirmQuit(GameDialog dialog, Action back, Action quit, Action goOn) =>
            dialog.Show(
                AdventureTexts.QuitTitle,
                AdventureTexts.QuitBody,
                null,
                back,
                DialogStyle.Row | DialogStyle.Cross,
                new DialogChoice(AdventureTexts.GoOnChoice, goOn),
                new DialogChoice(AdventureTexts.QuitChoice, quit)
            );
    }
}
