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

        public const string ShadyChestTitle = "怪しい宝箱";
        public const string ShadyChestBody =
            "ふたのすき間から、何かがこちらを見ている気がする…\nミミックなら手ごわいが、勝てば大きな報酬になる。";
        public const string OpenChoice = "開ける";
        public const string PassChoice = "通り過ぎる";
        public const string MerchantTitle = "行商人";
        public const string LeaveChoice = "立ち去る";
        public const string BuyChoice = "買う";
        public const string KeepChoice = "やめる";
        public const string AltarTitle = "覚醒の祭壇";
        public const string AltarBody =
            "祭壇の光が、仲間の固有スキルを1段覚醒させる。\n誰の力を引き出す？";
        public const string EvolveChoice = "覚醒させる";
        public const string ChestAwakeningTitle = "宝箱の覚醒";
        public const string ChestAwakeningBody =
            "宝箱の底から、覚醒の光があふれ出した。\n誰の固有スキルを1段覚醒させる？";
        public const string BackChoice = "もどる";

        // 祭壇で、選べるものを並べ、最後に閉じる・戻るを足す（選択肢は5つまで）。
        public static void Altar(
            GameDialog dialog,
            string body,
            IReadOnlyList<(string Label, Action Choose)> options,
            string backLabel,
            Action back
        ) => Choose(dialog, AltarTitle, body, options, backLabel, back);

        // 選べるものを並べ、最後に戻る（閉じる）を足す。backLabel が null なら足さず、選ぶまで閉じない。
        public static void Choose(
            GameDialog dialog,
            string title,
            string body,
            IReadOnlyList<(string Label, Action Choose)> options,
            string backLabel,
            Action back
        )
        {
            var choices = new List<DialogChoice>();
            foreach (var (label, choose) in options)
                choices.Add(new DialogChoice(label, choose));
            if (backLabel != null)
                choices.Add(new DialogChoice(backLabel, back));
            dialog.Show(title, body, null, back, choices.ToArray());
        }

        // 怪しい宝箱を開けるか、開けずに通り過ぎるか。閉じると、選ばずに地図へ戻る。
        public static void ShadyChest(GameDialog dialog, Action open, Action pass, Action close) =>
            dialog.Show(
                ShadyChestTitle,
                ShadyChestBody,
                null,
                close,
                new DialogChoice(OpenChoice, open),
                new DialogChoice(PassChoice, pass),
                new DialogChoice(AdventureTexts.CloseChoice, close)
            );

        // 行商人の品物の一覧。売り切れと、ルーンが足りない物は押せない。立ち去ると、行商人のマスを終える。
        public static void Merchant(
            GameDialog dialog,
            IReadOnlyList<AdventureWare> wares,
            long runes,
            Action<AdventureWare> buy,
            Action leave
        )
        {
            var choices = new List<DialogChoice>();
            foreach (var ware in wares)
            {
                var chosen = ware;
                choices.Add(
                    new DialogChoice(
                        AdventureTexts.WareLine(ware),
                        () => buy(chosen),
                        !ware.Sold && runes >= ware.Price
                    )
                );
            }
            choices.Add(new DialogChoice(LeaveChoice, leave));
            dialog.Show(
                MerchantTitle,
                AdventureTexts.MerchantBody(runes),
                null,
                leave,
                choices.ToArray()
            );
        }

        public static void ConfirmBuy(
            GameDialog dialog,
            AdventureWare ware,
            long runes,
            Action buy,
            Action back
        ) =>
            dialog.Show(
                MerchantTitle,
                AdventureTexts.ConfirmBuyBody(ware, runes),
                null,
                back,
                new DialogChoice(BuyChoice, buy),
                new DialogChoice(KeepChoice, back)
            );

        // 宝箱の中身を演出の札で1つずつ見せたあとの、題だけのダイアログ。
        public static void TreasureOpened(GameDialog dialog, Action close) =>
            dialog.Show(
                TreasureTitle,
                null,
                null,
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
        /// The menu: suspend to Home, quit, or stay. The body is the location, then the battle
        /// adds what suspending mid-battle does (the exploration adds nothing); the last choice
        /// is worded differently.
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
                string.IsNullOrEmpty(body)
                    ? AdventureCatalog.Location(run)
                    : AdventureCatalog.Location(run) + "\n" + body,
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
        public static void Result(GameDialog dialog, AdventureResult result, Action home) =>
            dialog.Show(
                AdventureCatalog.StatusTitle(result.Status),
                AdventureTexts.ResultBody(result),
                null,
                null,
                new DialogChoice(AdventureTexts.HomeChoice, home)
            );
    }
}
