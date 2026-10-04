using System;
using Baryonyx.Combat.Presentation;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The enemies a battle of the adventure is fought with: which of the battle screen's enemies
    /// stand in it (indexes of BattleInspectView.Enemies: 0 苔スライム, 1 苔むした狼, 2 森の守り手)
    /// and how much their HP is scaled. Provisional, like the route itself.
    /// </summary>
    public sealed class AdventureEncounter
    {
        public AdventureEncounter(string id, int[] enemies, float hpScale)
        {
            Id = id;
            Enemies = enemies;
            HpScale = hpScale;
        }

        public string Id { get; }
        public int[] Enemies { get; }
        public float HpScale { get; }
    }

    /// <summary>
    /// What the client shows of the server's adventure: the destinations' names and places, the
    /// floor names, the hints at the rooms' doors and the enemies of each encounter. The route
    /// itself (rooms, kinds, encounters) comes from the server (server/src/features/adventure/catalog.ts).
    /// </summary>
    public static class AdventureCatalog
    {
        public const string ForestRuins = AdventureLocalSource.ForestRuins;

        // 戦闘画面の敵の並び（BattleInspectView.Enemies）。
        public const int MossSlime = 0;
        public const int MossWolf = 1;
        public const int ForestGuardian = 2;

        private static readonly AdventureEncounter[] Encounters =
        {
            new("forest-pack", new[] { MossSlime, MossWolf }, 1f),
            // 強い魔物の気配：同じ2体を硬くする（仮）。
            new("forest-elite", new[] { MossSlime, MossWolf }, 1.6f),
            new("forest-boss", new[] { MossSlime, MossWolf, ForestGuardian }, 1f),
        };

        public static string DestinationName(string destinationId) =>
            destinationId switch
            {
                ForestRuins => "森の遺跡",
                _ => "冒険先",
            };

        // 探索と戦闘の背景。行き先ごとに戦闘の背景から選ぶ。
        public static BattleStage StageOf(string destinationId) =>
            destinationId switch
            {
                ForestRuins => BattleStage.MistyWoods,
                _ => BattleStage.MeadowRoad,
            };

        public static string FloorText(int floor) => $"B{Math.Max(1, floor)}F";

        public static string Location(AdventureRun run) =>
            run == null ? "" : $"{DestinationName(run.DestinationId)} {FloorText(run.Floor)}";

        // 入口の近くに出す短い手掛かり。
        public static string Hint(AdventureRoomKind kind) =>
            kind switch
            {
                AdventureRoomKind.Battle => "魔物の気配",
                AdventureRoomKind.Elite => "強い魔物の気配",
                AdventureRoomKind.Treasure => "宝箱がありそう",
                AdventureRoomKind.Boss => "最奥の間",
                _ => "入口",
            };

        public static AdventureEncounter Encounter(string id) =>
            Array.Find(Encounters, encounter => encounter.Id == id) ?? Encounters[0];

        public static string Outcome(AdventureRewardOutcome outcome) =>
            outcome switch
            {
                AdventureRewardOutcome.Updated => "ランクが上がりました",
                AdventureRewardOutcome.Discarded =>
                    "持っているほうがランクが高いため、手放しました",
                _ => "持ち物に加えました",
            };

        public static string StatusTitle(AdventureEndStatus status) =>
            status switch
            {
                AdventureEndStatus.Cleared => "踏破！",
                AdventureEndStatus.Defeated => "冒険の終わり",
                _ => "帰還",
            };
    }
}
