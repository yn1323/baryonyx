using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Enhancement;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.Items;
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
            "今いるマスから、ホームの行き先カードで再開できます。\n戦闘の途中なら、その戦闘の始めからになります。";

        public const string SaveFailed = "冒険を保存できませんでした";

        // 探索から装備の付け替えを開けなかったとき。
        public const string EquipmentFailed = "装備の画面を開けませんでした";
        public const string Rested = "焚き火で休んで、全員のHPが回復した";

        // 休憩の焚き火（doc/features/stage-progression.md の休憩の鍛える）。
        public const string CampfireTitle = "焚き火";
        public const string RestChoice = "休む";
        public const string TrainChoice = "鍛える";
        public const string StudyChoice = "研鑽";

        // 鍛えると上がる能力値。選んだ仲間の上に出す。
        public static string TrainEffect =>
            $"物攻・属攻 +{Mathf.RoundToInt(AdventureParty.HoneStat * 100f)}%";

        public static string HonedMember(string name) => $"{name}の物攻・属攻が上がった";

        public static string HonedCard(string name) =>
            $"「{name}」が「{name}{AdventureParty.HonedMark}」になった";

        // 戦闘後の強化（doc/features/stage-progression.md の戦闘後の強化）。
        public const string BoonTitle = "強化";
        public const string BoonChoice = "選ぶ";

        // 「？」のマスに着いたときの、中身の種類の知らせ。
        public static string Revealed(AdventureRoomKind kind) => $"「？」は{KindName(kind)}だった";

        public const string PassedChest = "怪しい宝箱を通り過ぎた";
        public const string BuyFailed = "買えませんでした";
        public const string TrialMet = "試練を達成した！";
        public const string TrialMissed = "試練の条件は満たせなかった";

        public static string TrialNotice(AdventureTrial trial) =>
            "試練：" + AdventureTrials.NameOf(trial);

        // 歩数の扉の報酬の段（AdventureStepDoor.Tier の順）。
        private static readonly string[] DoorTiers =
        {
            "少しだけ開いた",
            "大きく開いた",
            "大きく開き、奥まで光が差した",
            "全開になり、まばゆい光があふれた",
        };

        public static string StepDoorNotice(AdventureStepDoor door) =>
            $"今日の歩数 {door.Steps:N0}歩：扉が{DoorTiers[Math.Clamp(door.Tier, 0, DoorTiers.Length - 1)]}";

        public static string MerchantBody(long runes) =>
            "旅の品をそろえているよ。\n" + $"所持ルーン {runes:N0}";

        // 品物の行：☆、名前、品質と倍率、値段。売り切れなら値段の代わりに「売り切れ」。
        public static string WareLine(AdventureWare ware)
        {
            var drop = ware.AsDrop("");
            string stars = ware.Stars > 0 ? new string('☆', ware.Stars) + " " : "";
            string grade = ware.Quality is { } quality ? $" {quality}×{ware.Multiplier:0.00}" : "";
            string price = ware.Sold ? "売り切れ" : $"{ware.Price:N0}ルーン";
            return $"{stars}{DropName(drop)}{grade}　{price}";
        }

        public static string ConfirmBuyBody(AdventureWare ware, long runes) =>
            $"{DropName(ware.AsDrop(""))}を {ware.Price:N0}ルーンで買いますか？\n"
            + $"所持ルーン {runes:N0} → {runes - ware.Price:N0}";

        /// <summary>The name of a room's kind, as the map's legend shows it (doc/features/stage-progression.md).</summary>
        public static string KindName(AdventureRoomKind kind) =>
            kind switch
            {
                AdventureRoomKind.Battle => "魔物",
                AdventureRoomKind.Elite => "強い魔物",
                AdventureRoomKind.Treasure => "宝箱",
                AdventureRoomKind.Boss => "最奥の間",
                AdventureRoomKind.Rest => "休憩",
                AdventureRoomKind.Merchant => "行商人",
                AdventureRoomKind.ShadyChest => "怪しい宝箱",
                AdventureRoomKind.Trial => "試練",
                AdventureRoomKind.StepDoor => "歩数の扉",
                AdventureRoomKind.Altar => "覚醒の祭壇",
                AdventureRoomKind.Mystery => "？",
                _ => "入口",
            };

        public const string LoadFailed = "冒険を取得できませんでした";

        public static string ReviveChoice(int cost) => $"ルーンで復活（{cost:N0}）";

        public static string ReviveBody(int cost, long runes) =>
            "手に入れた物は持ち帰れます。\n"
            // 1行に収まる長さで、文の途中で折り返さないよう2つに分ける（2026-10-26）。
            + "復活すると、味方が全快して戦闘を続けます。\n"
            + "敵の傷はそのまま残ります。\n"
            + $"所持ルーン {runes:N0}"
            + (runes < cost ? $"（{cost - runes:N0} 足りません）" : "");

        /// <summary>The name of a dropped kind, from its definition (the id when it has none).</summary>
        public static string DropName(AdventureDrop drop)
        {
            string name = drop.Category switch
            {
                AdventureDropCategory.Item => ItemCards.Find(drop.Id)?.Name,
                AdventureDropCategory.Skill => CardSkills.Find(drop.Id)?.Name,
                AdventureDropCategory.Material => EnhancementMaterials.Find(drop.Id)?.Name,
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
                AdventureDropCategory.Item or AdventureDropCategory.Material =>
                    Quality.MinMultiplier,
                _ => EquipmentCatalog.Find(id)?.MaxMultiplier ?? Quality.MinMultiplier,
            };

        /// <summary>One drop as a line: the stars, the name, the category and the quality.</summary>
        public static string DropLine(AdventureDrop drop)
        {
            string stars = EquipmentCatalog.Stars(drop.Stars);
            string detail = drop.Quality is { } quality
                ? $"{AdventureDrops.NameOf(drop.Category)}・品質{Quality.Letter(quality)}"
                : AdventureDrops.NameOf(drop.Category);
            return $"{stars} {DropName(drop)}（{detail}）".TrimStart();
        }

        public static string DropsBody(IReadOnlyList<AdventureDrop> drops) =>
            drops == null || drops.Count == 0
                ? "何も見つからなかった"
                : "手に入れた物\n" + string.Join("\n", drops.Select(DropLine));

        /// <summary>
        /// The picture of one drop: the kind's own art for an item, an enhancement material, an
        /// accessory, a weapon or armour, and the card's art for a skill (<see cref="ItemArt"/>).
        /// Null for a kind without art.
        /// </summary>
        public static Sprite DropIcon(AdventureDrop drop, ItemArt art)
        {
            if (drop == null || art == null)
                return null;
            return art.Find(drop.Id);
        }

        /// <summary>The picture of the drop worth showing most: the rarest that has a picture.</summary>
        public static Sprite DropIcon(IReadOnlyList<AdventureDrop> drops, ItemArt art = null) =>
            (drops ?? Array.Empty<AdventureDrop>())
                .OrderByDescending(drop => drop.Stars)
                .Select(drop => DropIcon(drop, art))
                .FirstOrDefault(icon => icon != null);

        // 結果に「目玉」として名前を挙げる物：☆4以上か、品質S。
        private static bool Highlight(AdventureDrop drop) =>
            drop.Stars >= 4 || drop.Quality == ItemQuality.S;

        public static string ResultBody(AdventureResult result)
        {
            var lines = new List<string>
            {
                $"{AdventureCatalog.DestinationName(result.DestinationId)}　{AdventureCatalog.FloorText(result.Floor)}まで到達",
                result.NewRecord
                    ? "最深記録を更新！"
                    : $"最深記録 {AdventureCatalog.FloorText(result.BestFloor)}",
            };
            var counts = result
                .Drops.GroupBy(drop => drop.Category)
                .OrderBy(group => group.Key)
                .Select(group => $"{AdventureDrops.NameOf(group.Key)} {group.Count()}")
                .ToArray();
            // 名前の途中で折り返さないよう、行を分けて並べる（2026-10-26、「スキ／ル」と割れていたため）。
            if (counts.Length == 0)
                lines.Add("持ち帰った物：なし");
            else
            {
                lines.Add("持ち帰った物");
                lines.AddRange(Rows(counts, CountsPerRow));
            }
            var best = result
                .Drops.Where(Highlight)
                .OrderByDescending(drop => drop.Stars)
                .ThenByDescending(drop => drop.Quality ?? ItemQuality.C)
                .Take(3)
                .Select(drop =>
                    $"{EquipmentCatalog.Stars(drop.Stars)} {DropName(drop)}"
                    + (drop.Quality is { } quality ? $" {Quality.Letter(quality)}" : "")
                )
                .ToArray();
            if (best.Length > 0)
            {
                lines.Add("目玉");
                lines.AddRange(Rows(best, BestPerRow));
            }
            return string.Join("\n", lines);
        }

        // 結果の1行に並べる、持ち帰った物の分類の数と、目玉の数。
        private const int CountsPerRow = 3;
        private const int BestPerRow = 2;

        private static IEnumerable<string> Rows(IReadOnlyList<string> parts, int perRow)
        {
            for (int i = 0; i < parts.Count; i += perRow)
                yield return string.Join("・", parts.Skip(i).Take(perRow));
        }
    }
}
