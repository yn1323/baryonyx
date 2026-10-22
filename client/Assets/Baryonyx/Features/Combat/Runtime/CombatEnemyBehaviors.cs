using System.Collections.Generic;
using static Baryonyx.Combat.CardStat;
using static Baryonyx.Combat.EnemyAim;
using static Baryonyx.Combat.EnemyCare;
using static Baryonyx.Combat.EnemyCue;
using static Baryonyx.Combat.EnemyMove;
using static Baryonyx.Combat.EnemyPattern;

namespace Baryonyx.Combat
{
    /// <summary>
    /// Every enemy's skills and patterns (doc/catalog/enemies: 技 and 行動パターン, with the
    /// skills of doc/catalog/enemy-skills), one behaviour per kind of <see cref="CombatEnemies"/>.
    /// Written from the documents; keep them in step when a document changes. Effects the battle
    /// does not have yet are left out as the comments tell: statuses on the party, calls for help
    /// and the like. The kinds are split over files by where they first show up.
    /// </summary>
    public static partial class CombatEnemyBehaviors
    {
        private static IReadOnlyList<EnemyBehavior> all;

        public static IReadOnlyList<EnemyBehavior> All => all ??= Gather();

        public static EnemyBehavior Find(string id)
        {
            foreach (var behavior in All)
                if (behavior.Id == id)
                    return behavior;
            return null;
        }

        private static IReadOnlyList<EnemyBehavior> Gather()
        {
            var list = new List<EnemyBehavior>();
            list.AddRange(Mock());
            list.AddRange(Forest());
            list.AddRange(Coast());
            list.AddRange(Depths());
            list.AddRange(Tomb());
            list.AddRange(Peaks());
            return list;
        }

        // The three enemies of the battle mock (2026-10-04).
        private static EnemyBehavior[] Mock() =>
            new[]
            {
                new EnemyBehavior
                {
                    Id = "moss-slime",
                    Moves = new[]
                    {
                        Attack("たいあたり", PhysicalAttack, 100),
                        Other("胞子ばらまき", "味方全体への毒"),
                        Mend("朝露すすり", 100),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "たいあたり",
                        Choices = new[] { Weight("たいあたり", 60), Weight("胞子ばらまき", 40) },
                        Once = new[] { "胞子ばらまき" },
                        Rules = new[] { EnemyRule.When(Hurt, "朝露すすり").Times(1) },
                    },
                    // 激化で胞子ばらまきの毒が3重ねになる分は、毒がないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("たいあたり", 150) },
                },
                new EnemyBehavior
                {
                    Id = "moss-wolf",
                    Moves = new[]
                    {
                        Attack("かみつき", PhysicalAttack, 100),
                        // 出血2ターンは入れない。
                        Attack("引き裂き", PhysicalAttack, 60),
                        Boost("遠吠え", 2),
                        Guard("草陰に伏せる", 120).WithPowerUp(50),
                        Attack("喉笛狙い", PhysicalAttack, 140, Weakest),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "遠吠え",
                        OpeningAlone = "草陰に伏せる",
                        Choices = new[]
                        {
                            Weight("かみつき", 40),
                            Weight("引き裂き", 30),
                            Weight("草陰に伏せる", 30),
                        },
                        Once = new[] { "草陰に伏せる" },
                        Rules = new[]
                        {
                            EnemyRule.When(Together, "遠吠え").Apart(3),
                            EnemyRule.When(PartyHurt, "喉笛狙い").Apart(2),
                        },
                    },
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        new EnemyEnrage("かみつき", 130),
                        new EnemyEnrage("引き裂き", 130),
                        new EnemyEnrage("喉笛狙い", 130),
                    },
                },
                new EnemyBehavior
                {
                    Id = "forest-guardian",
                    Moves = new[]
                    {
                        Attack("大地の拳", PhysicalAttack, 100),
                        Attack("地響き", PhysicalAttack, 50, EnemyAim.All),
                        // 絡みつき1ターンは入れない。
                        Attack("根縛り", PhysicalAttack, 30),
                        Guard("身を固める", 120),
                        Mend("森の恵み", 100, Others).WithRegen(2, 20),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        Boost("刻印の目覚め", Forever, Self),
                        // 防御ダウン2ターンは入れない。溜めのターンに身を固めるも使う。
                        Attack("森羅の鉄槌", PhysicalAttack, 140, EnemyAim.All)
                            .Charged(1, "身を固める"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "大地の拳",
                        Order = new[] { "大地の拳", "身を固める", "根縛り", "地響き" },
                        Rules = new[]
                        {
                            EnemyRule.When(OtherHurt, "森の恵み").Apart(3),
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                        },
                    },
                    LateTurn = 6,
                    LateMove = "刻印の目覚め",
                    Late = new EnemyPattern
                    {
                        Cycle = new[] { Pick, Pick, "森羅の鉄槌" },
                        Choices = new[]
                        {
                            Weight("大地の拳", 1),
                            Weight("地響き", 1),
                            Weight("根縛り", 1),
                        },
                        MaxRun = 1,
                        Rules = new[]
                        {
                            EnemyRule.When(OtherHurt, "森の恵み").Apart(3),
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                        },
                    },
                },
            };
    }
}
