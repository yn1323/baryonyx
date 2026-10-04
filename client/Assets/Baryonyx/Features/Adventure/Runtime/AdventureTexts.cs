using System.Collections.Generic;
using System.Linq;
using Baryonyx.StepBonus;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>The words of the adventure's dialogs, shared by the exploration and the battle.</summary>
    public static class AdventureTexts
    {
        public const string MenuTitle = "冒険のメニュー";
        public const string SuspendChoice = "中断してホームへ";
        public const string QuitChoice = "冒険をやめる";
        public const string CloseChoice = "閉じる";
        public const string GoOnChoice = "つづける";
        public const string NextChoice = "先へ進む";
        public const string HomeChoice = "ホームへ";
        public const string ReturnChoice = "帰還する";

        public const string QuitTitle = "冒険をやめますか？";
        public const string QuitBody =
            "ここまでに手に入れた物は持ち帰れます。\n次の冒険は入口（B1F）から始まります。";

        public const string SuspendBody =
            "今いる部屋から、ホームの行き先カードで再開できます。\n戦闘の途中なら、その戦闘の始めからになります。";

        public const string SaveFailed = "冒険を保存できませんでした";
        public const string LoadFailed = "冒険を取得できませんでした";

        public static string ReviveChoice(int cost) => $"ルーンで復活（{cost:N0}）";

        public static string ReviveBody(int cost, long runes) =>
            "手に入れた物は持ち帰れます。\n"
            + "復活すると、敵の傷はそのままで、味方が全快して戦闘を続けます。\n"
            + $"所持ルーン {runes:N0}"
            + (runes < cost ? $"（{cost - runes:N0} 足りません）" : "");

        public static string BonusName(StepBonusMockData bonuses, string id)
        {
            var bonus = bonuses != null ? bonuses.Find(id) : null;
            return bonus != null && !string.IsNullOrEmpty(bonus.Name) ? bonus.Name : id;
        }

        public static Sprite BonusIcon(StepBonusMockData bonuses, string id) =>
            bonuses != null ? bonuses.Find(id)?.Icon : null;

        public static string RewardBody(StepBonusMockData bonuses, AdventureReward reward) =>
            reward == null
                ? "何も見つからなかった"
                : $"ACTボーナス「{BonusName(bonuses, reward.BonusId)}」（{reward.Rank}）を手に入れた\n"
                    + AdventureCatalog.Outcome(reward.Outcome);

        public static string ResultBody(StepBonusMockData bonuses, AdventureResult result)
        {
            var lines = new List<string>
            {
                $"{AdventureCatalog.DestinationName(result.DestinationId)}　{AdventureCatalog.FloorText(result.Floor)}まで到達",
                result.NewRecord
                    ? "最深記録を更新！"
                    : $"最深記録 {AdventureCatalog.FloorText(result.BestFloor)}",
            };
            var kept = result
                .Rewards.Where(reward => reward.Outcome != AdventureRewardOutcome.Discarded)
                .Select(reward => $"{BonusName(bonuses, reward.BonusId)} {reward.Rank}")
                .ToArray();
            lines.Add(
                kept.Length == 0
                    ? "持ち帰ったボーナス：なし"
                    : "持ち帰ったボーナス：" + string.Join("・", kept)
            );
            return string.Join("\n", lines);
        }
    }
}
