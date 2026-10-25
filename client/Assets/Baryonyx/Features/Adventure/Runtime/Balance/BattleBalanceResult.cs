using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>One battle of an automatic run, as it was fought.</summary>
    [Serializable]
    public sealed class BattleBalanceRecord
    {
        public const string Victory = "Victory";
        public const string Defeat = "Defeat";
        public const string Timeout = "Timeout";

        /// <summary>The adventure it was fought in ("through"), or the battle's number ("single").</summary>
        public int run;
        public int seed;

        /// <summary>The room it was fought in ("through"; empty in "single").</summary>
        public string roomId = "";
        public string encounter = "";
        public string enemies = "";

        /// <summary>Each enemy's numbers as the battle began: "id Lv HP 物防/属防 攻撃 速度".</summary>
        public string enemyStats = "";

        /// <summary>Each member's numbers as the battle began, with what they wear: "名前 Lv HP 物攻 属攻 物防 属防 速度 会心 2回".</summary>
        public string partyStats = "";
        public string outcome = "";

        /// <summary>The party's turns the battle took.</summary>
        public int turns;

        /// <summary>The enemies' HP when the battle began.</summary>
        public int enemyHp;

        /// <summary>The HP the enemies lost, and the HP they got back (healing and regeneration).</summary>
        public int dealt;
        public int enemyHealed;

        /// <summary>The HP the party lost, and the HP it got back.</summary>
        public int taken;
        public int partyHealed;

        /// <summary>The party's HP as a share of its maximum: when the battle began, at its lowest and when it ended.</summary>
        public float hpStart;
        public float hpLowest;
        public float hpEnd;

        /// <summary>How many members were down when it ended, and how many fell in it (not those carried in fallen).</summary>
        public int downAtEnd;
        public int fell;
        public int cards;

        /// <summary>The HP the enemies lost in each of the party's turns (with the enemies' turn after it).</summary>
        public int[] dealtByTurn = Array.Empty<int>();
        public bool Won => outcome == Victory;
    }

    /// <summary>One adventure walked in "through": how far it went.</summary>
    [Serializable]
    public sealed class BattleBalanceRun
    {
        public int run;
        public int seed;
        public bool cleared;

        /// <summary>The deepest floor reached, and the room the party was wiped out in (empty when not).</summary>
        public int floor;
        public string lostIn = "";
        public int battles;
        public float hpEnd;
    }

    /// <summary>The numbers of a group of battles (one encounter, one kind of room, or all).</summary>
    [Serializable]
    public sealed class BattleBalanceGroup
    {
        public string key = "";
        public int battles;
        public int wins;
        public int defeats;
        public int timeouts;
        public float winRate;
        public float turnsAverage;
        public int turnsMin;
        public int turnsMedian;
        public int turnsMax;

        /// <summary>How many battles took each number of turns: "3:4 4:6 5:1".</summary>
        public string turnsHistogram = "";
        public int enemyHpAverage;
        public float dealtPerTurn;

        /// <summary>The party's HP when won (share of the maximum), and at its lowest in every battle.</summary>
        public float hpEndWhenWon;
        public float hpLowestAverage;
        public float fellAverage;
    }

    /// <summary>What an automatic run found: the request, the numbers by group and every battle.</summary>
    [Serializable]
    public sealed class BattleBalanceResult
    {
        public BattleBalanceRequest request = new();
        public string startedAt = "";
        public float seconds;
        public BattleBalanceGroup overall = new();
        public BattleBalanceGroup[] groups = Array.Empty<BattleBalanceGroup>();
        public BattleBalanceRun[] runs = Array.Empty<BattleBalanceRun>();
        public BattleBalanceRecord[] battles = Array.Empty<BattleBalanceRecord>();

        public string ToJson() => JsonUtility.ToJson(this, true);

        /// <summary>
        /// Sums <paramref name="battles"/> up: by encounter in "single", by kind of room in
        /// "through" (the encounter's middle part: pack, elite, boss, mimic).
        /// </summary>
        public static BattleBalanceResult Of(
            BattleBalanceRequest request,
            IReadOnlyList<BattleBalanceRecord> battles,
            IReadOnlyList<BattleBalanceRun> runs = null
        )
        {
            battles ??= Array.Empty<BattleBalanceRecord>();
            bool through = request != null && request.IsThrough;
            return new BattleBalanceResult
            {
                request = request ?? new BattleBalanceRequest(),
                overall = Group("all", battles),
                groups = battles
                    .GroupBy(battle => through ? RoomOf(battle.encounter) : battle.encounter)
                    .Select(group => Group(group.Key, group.ToArray()))
                    .ToArray(),
                runs = runs?.ToArray() ?? Array.Empty<BattleBalanceRun>(),
                battles = battles.ToArray(),
            };
        }

        /// <summary>The kind of room of an encounter id ("forest-ruins/elite/5/94" is elite).</summary>
        public static string RoomOf(string encounter)
        {
            var parts = (encounter ?? "").Split('/');
            return parts.Length > 1 ? parts[1] : encounter ?? "";
        }

        public static BattleBalanceGroup Group(
            string key,
            IReadOnlyList<BattleBalanceRecord> battles
        )
        {
            var group = new BattleBalanceGroup { key = key ?? "", battles = battles.Count };
            if (battles.Count == 0)
                return group;
            group.wins = battles.Count(battle => battle.Won);
            group.defeats = battles.Count(battle => battle.outcome == BattleBalanceRecord.Defeat);
            group.timeouts = battles.Count(battle => battle.outcome == BattleBalanceRecord.Timeout);
            group.winRate = group.wins / (float)battles.Count;
            var turns = battles.Select(battle => battle.turns).OrderBy(value => value).ToArray();
            group.turnsAverage = (float)turns.Average();
            group.turnsMin = turns[0];
            group.turnsMax = turns[^1];
            group.turnsMedian = turns[(turns.Length - 1) / 2];
            group.turnsHistogram = string.Join(
                " ",
                turns.GroupBy(value => value).Select(each => $"{each.Key}:{each.Count()}")
            );
            group.enemyHpAverage = (int)Math.Round(battles.Average(battle => battle.enemyHp));
            int allTurns = battles.Sum(battle => Math.Max(1, battle.turns));
            group.dealtPerTurn = battles.Sum(battle => battle.dealt) / (float)allTurns;
            var won = battles.Where(battle => battle.Won).ToArray();
            group.hpEndWhenWon = won.Length > 0 ? won.Average(battle => battle.hpEnd) : 0f;
            group.hpLowestAverage = battles.Average(battle => battle.hpLowest);
            group.fellAverage = (float)battles.Average(battle => battle.fell);
            return group;
        }

        /// <summary>A table to read: the request in a line, then the groups, the overall row last, and the adventures in "through".</summary>
        public string ToMarkdown()
        {
            var text = new StringBuilder();
            var r = request ?? new BattleBalanceRequest();
            text.AppendLine("# 自動対戦の結果");
            text.AppendLine();
            text.AppendLine(
                $"- {r.mode}・{r.destination}"
                    + (
                        r.IsThrough
                            ? $"・{r.runs}回の冒険"
                            : $"・{r.room}・深さ{r.depthPercent}%・各{r.repeats}回"
                    )
                    + $"・種{r.seed}・Lv[{string.Join(",", r.levels ?? Array.Empty<int>())}]"
                    + $"・品質×{F(r.cardMultiplier)}・覚醒{r.awakenedBranch}・装備{r.equipment?.Length ?? 0}個・弱点{(r.knowWeaknesses ? "既知" : "未知")}"
                    + $"・確率{r.odds}・カードの選び方{r.policy}"
            );
            text.AppendLine($"- {startedAt}に開始、{seconds:0}秒");
            text.AppendLine();
            text.AppendLine(
                "| 組 | 戦闘 | 勝率 | ターン 平均/中央/最小〜最大 | 分布 | 敵のHP | 与ダメ/ターン | 勝ったときのHP | 最低HP | 倒れた |"
            );
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var group in groups.Append(overall))
                text.AppendLine(
                    $"| {Label(group.key)} | {group.battles} | {P(group.winRate)} | {group.turnsAverage:0.0}/{group.turnsMedian}/{group.turnsMin}〜{group.turnsMax}"
                        + $" | {group.turnsHistogram} | {group.enemyHpAverage} | {group.dealtPerTurn:0} | {P(group.hpEndWhenWon)} | {P(group.hpLowestAverage)} | {group.fellAverage:0.0} |"
                );
            if (runs.Length > 0)
            {
                text.AppendLine();
                text.AppendLine("| 冒険 | 種 | 踏破 | 届いた階 | 負けた部屋 | 戦闘 | 終わりのHP |");
                text.AppendLine("|---|---|---|---|---|---|---|");
                foreach (var run in runs)
                    text.AppendLine(
                        $"| {run.run} | {run.seed} | {(run.cleared ? "○" : "×")} | {run.floor} | {run.lostIn} | {run.battles} | {P(run.hpEnd)} |"
                    );
            }
            return text.ToString();
        }

        // 組の名前に、敵の組み合わせを添える。
        private string Label(string key)
        {
            var battle = battles.FirstOrDefault(each => each.encounter == key);
            return battle != null && !string.IsNullOrEmpty(battle.enemies)
                ? $"{key}（{battle.enemies}）"
                : key;
        }

        private static string P(float share) =>
            (share * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

        private static string F(float value) =>
            value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
