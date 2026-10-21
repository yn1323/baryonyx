using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.Items;
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
            "ここまでに手に入れた物は持ち帰れます。\n次の冒険は入口（第1層）から始まります。";

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

        /// <summary>The name of a dropped kind, from its definition (the id when it has none).</summary>
        public static string DropName(StepBonusMockData bonuses, AdventureDrop drop)
        {
            string name = drop.Category switch
            {
                AdventureDropCategory.Item => ItemCards.Find(drop.Id)?.Name,
                AdventureDropCategory.Skill => CardSkills.Find(drop.Id)?.Name,
                AdventureDropCategory.Bonus => BonusName(bonuses, drop.Id),
                _ => EquipmentCatalog.Find(drop.Id)?.Name,
            };
            return string.IsNullOrEmpty(name) ? drop.Id : name;
        }

        /// <summary>The top of a kind's multiplier, as its definition sets it.</summary>
        public static float MaxMultiplierOf(AdventureDropCategory category, string id) =>
            category switch
            {
                AdventureDropCategory.Skill => CardSkills.Find(id)?.MaxMultiplier
                    ?? Quality.MinMultiplier,
                AdventureDropCategory.Bonus => StepBonusDefinition.DefaultMaxMultiplier,
                AdventureDropCategory.Item => Quality.MinMultiplier,
                _ => EquipmentCatalog.Find(id)?.MaxMultiplier ?? Quality.MinMultiplier,
            };

        /// <summary>One drop as a line: the stars, the name, the category and the quality.</summary>
        public static string DropLine(StepBonusMockData bonuses, AdventureDrop drop)
        {
            string stars = EquipmentCatalog.Stars(drop.Stars);
            string detail = drop.Quality is { } quality
                ? $"{AdventureDrops.NameOf(drop.Category)}・品質{Quality.Letter(quality)}"
                : AdventureDrops.NameOf(drop.Category);
            string line = $"{stars} {DropName(bonuses, drop)}（{detail}）".TrimStart();
            return drop.Outcome == AdventureDropOutcome.Added
                ? line
                : line + "　" + AdventureCatalog.Outcome(drop.Outcome);
        }

        public static string DropsBody(
            StepBonusMockData bonuses,
            IReadOnlyList<AdventureDrop> drops
        ) =>
            drops == null || drops.Count == 0
                ? "何も見つからなかった"
                : "手に入れた物\n"
                    + string.Join("\n", drops.Select(drop => DropLine(bonuses, drop)));

        /// <summary>
        /// The picture of one drop: the ACT bonus's icon, or the kind's own art for an item, an
        /// accessory, a weapon or armour (<see cref="ItemArt"/>). Null for a skill or a kind
        /// without art.
        /// </summary>
        public static Sprite DropIcon(StepBonusMockData bonuses, AdventureDrop drop, ItemArt art)
        {
            if (drop == null)
                return null;
            if (drop.Category == AdventureDropCategory.Bonus)
                return BonusIcon(bonuses, drop.Id);
            if (drop.Category == AdventureDropCategory.Skill || art == null)
                return null;
            return art.Find(drop.Id);
        }

        /// <summary>The picture of the drop worth showing most: the rarest that has a picture.</summary>
        public static Sprite DropIcon(
            StepBonusMockData bonuses,
            IReadOnlyList<AdventureDrop> drops,
            ItemArt art = null
        ) =>
            (drops ?? Array.Empty<AdventureDrop>())
                .OrderByDescending(drop => drop.Stars)
                .Select(drop => DropIcon(bonuses, drop, art))
                .FirstOrDefault(icon => icon != null);

        // 結果に「目玉」として名前を挙げる物：☆4以上か、品質S。
        private static bool Highlight(AdventureDrop drop) =>
            drop.Outcome != AdventureDropOutcome.Discarded
            && (drop.Stars >= 4 || drop.Quality == ItemQuality.S);

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
                .Drops.Where(drop => drop.Outcome != AdventureDropOutcome.Discarded)
                .ToArray();
            var counts = kept.GroupBy(drop => drop.Category)
                .OrderBy(group => group.Key)
                .Select(group => $"{AdventureDrops.NameOf(group.Key)} {group.Count()}")
                .ToArray();
            lines.Add(
                counts.Length == 0
                    ? "持ち帰った物：なし"
                    : "持ち帰った物：" + string.Join("・", counts)
            );
            var best = kept.Where(Highlight)
                .OrderByDescending(drop => drop.Stars)
                .ThenByDescending(drop => drop.Quality ?? ItemQuality.C)
                .Take(3)
                .Select(drop =>
                    $"{EquipmentCatalog.Stars(drop.Stars)} {DropName(bonuses, drop)}"
                    + (drop.Quality is { } quality ? $" {Quality.Letter(quality)}" : "")
                )
                .ToArray();
            if (best.Length > 0)
                lines.Add("目玉：" + string.Join("・", best));
            return string.Join("\n", lines);
        }
    }
}
