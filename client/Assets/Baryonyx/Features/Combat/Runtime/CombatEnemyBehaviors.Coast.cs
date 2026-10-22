using static Baryonyx.Combat.CardStat;
using static Baryonyx.Combat.EnemyAim;
using static Baryonyx.Combat.EnemyCare;
using static Baryonyx.Combat.EnemyCue;
using static Baryonyx.Combat.EnemyMove;
using static Baryonyx.Combat.EnemyPattern;

namespace Baryonyx.Combat
{
    public static partial class CombatEnemyBehaviors
    {
        // The kinds of the coast, the roadside, the snowfield and the caves.
        private static EnemyBehavior[] Coast() =>
            new[]
            {
                new EnemyBehavior
                {
                    Id = "ghost-jelly",
                    Moves = new[]
                    {
                        // やけど1ターンは入れない。
                        Attack("火の玉", MagicAttack, 100),
                        // 毒2重ねは入れない。
                        Attack("毒針", PhysicalAttack, 60),
                        Other("すり抜け", "物理の攻撃の無効"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "火の玉",
                        Choices = new[] { Weight("火の玉", 50), Weight("毒針", 50) },
                        // 前の味方ターンに物理の攻撃を2回以上受けたときのすり抜けは、
                        // 受けた攻撃の回数が見えないため入れない。
                    },
                    // 激化でやけどが2ターンになる分は、やけどがないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("火の玉", 130) },
                },
                new EnemyBehavior
                {
                    Id = "barnacle-crab",
                    Moves = new[]
                    {
                        Attack("斬りつけ", PhysicalAttack, 100),
                        Attack("なぎ払い", PhysicalAttack, 50, EnemyAim.All),
                        Guard("身を固める", 120),
                        Focus("力を溜める", 50),
                        // ブロックへ2倍のダメージを与える分は入れず、通常の攻撃として当てる。
                        Attack("大ばさみ砕き", PhysicalAttack, 100, MostBlock),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "身を固める",
                        Choices = new[]
                        {
                            Weight("斬りつけ", 40),
                            Weight("なぎ払い", 30),
                            Weight("身を固める", 30),
                        },
                        Once = new[] { "身を固める" },
                        Rules = new[]
                        {
                            EnemyRule
                                .When(PartyGuarded, "力を溜める")
                                .Apart(4)
                                .Next("大ばさみ砕き"),
                        },
                    },
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        new EnemyEnrage("斬りつけ", 130),
                        new EnemyEnrage("大ばさみ砕き", 130),
                    },
                },
                new EnemyBehavior
                {
                    Id = "skeleton-pirate",
                    Moves = new[]
                    {
                        Attack("斬りつけ", PhysicalAttack, 100),
                        // 出血2ターンは入れない。
                        Attack("引き裂き", PhysicalAttack, 60),
                        Boost("遠吠え", 2),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        // 次の味方ターンのエネルギーを1減らす分は入れない。
                        Attack("卑怯な足払い", PhysicalAttack, 60),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "遠吠え",
                        OpeningAlone = "斬りつけ",
                        Choices = new[] { Weight("斬りつけ", 50), Weight("引き裂き", 50) },
                        Rules = new[]
                        {
                            EnemyRule.When(Always, "卑怯な足払い").Apart(3).After(3),
                            EnemyRule.When(Together, "遠吠え").Apart(3),
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                        },
                    },
                    // 激化で引き裂きの出血が3ターンに延びる分と、卑怯な足払いが2ターンごとになる分は
                    // 入れない。
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        new EnemyEnrage("斬りつけ", 130),
                        new EnemyEnrage("引き裂き", 130),
                    },
                },
                new EnemyBehavior
                {
                    Id = "pumpkin-scarecrow",
                    Moves = new[]
                    {
                        Attack("たいあたり", PhysicalAttack, 100),
                        Other("にらみつけ", "味方単体への麻痺"),
                        Focus("力を溜める", 50),
                        Other("畑の見張り番", "見張り（仲間を攻撃した味方への反撃）"),
                        Attack("種つぶて", PhysicalAttack, 40, EnemyAim.All),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "にらみつけ",
                        Choices = new[]
                        {
                            Weight("たいあたり", 50),
                            Weight("種つぶて", 30),
                            Weight("力を溜める", 20),
                        },
                        // 力を溜めるの次をたいあたりにする決まりは、選ぶ技の次を決められないため
                        // 入れない。
                        Rules = new[]
                        {
                            EnemyRule.When(Together, "畑の見張り番").Apart(4).After(2),
                            EnemyRule.When(Always, "にらみつけ").Apart(4),
                        },
                    },
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        new EnemyEnrage("たいあたり", 130),
                        new EnemyEnrage("種つぶて", 130),
                    },
                },
                new EnemyBehavior
                {
                    Id = "giant-octopus",
                    Moves = new[]
                    {
                        Attack("たいあたり", PhysicalAttack, 100),
                        Attack("地響き", PhysicalAttack, 50, EnemyAim.All),
                        Guard("身を固める", 120),
                        Mend("傷ふさぎ", 60).WithRegen(2, 20),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        // 捕縛は入れない。
                        Attack("絡め取り", PhysicalAttack, 60),
                        Other("墨吐き", "味方全体への麻痺と予告を伏せる"),
                        // 溜めのターンに身を固めるも使う。
                        Attack("八腕の乱れ打ち", PhysicalAttack, 60, EnemyAim.All, 2)
                            .Charged(1, "身を固める"),
                    },
                    // 捕縛されている味方がいる間に絡め取りを替える・選ばない決まりは、捕縛がないため
                    // 入れない。
                    Early = new EnemyPattern
                    {
                        Opening = "絡め取り",
                        Order = new[] { "たいあたり", "地響き", "身を固める", "絡め取り" },
                        Rules = new[]
                        {
                            EnemyRule.When(Hurt, "傷ふさぎ", 0.75f).Apart(4),
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                        },
                    },
                    LateTurn = 6,
                    LateMove = "墨吐き",
                    Late = new EnemyPattern
                    {
                        Cycle = new[] { Pick, Pick, "八腕の乱れ打ち" },
                        Choices = new[]
                        {
                            Weight("たいあたり", 1),
                            Weight("地響き", 1),
                            Weight("絡め取り", 1),
                        },
                        MaxRun = 1,
                        Rules = new[]
                        {
                            EnemyRule.When(Always, "墨吐き").Apart(8),
                            EnemyRule.When(Hurt, "傷ふさぎ", 0.75f).Apart(4),
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                        },
                    },
                },
                new EnemyBehavior
                {
                    Id = "big-eared-bat",
                    Moves = new[]
                    {
                        Attack("吸い取り", PhysicalAttack, 80).Draining(50),
                        Other("金切り声", "味方全体への麻痺"),
                        Other("耳ざとい身かわし", "カードの回避"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "金切り声",
                        Choices = new[]
                        {
                            Weight("吸い取り", 60),
                            Weight("耳ざとい身かわし", 25),
                            Weight("金切り声", 15),
                        },
                        Once = new[] { "耳ざとい身かわし", "金切り声" },
                        // HPが半分以下で吸い取りの割合を80%に上げる分は、割合を替えられないため
                        // 入れない。
                    },
                    // 激化で回復する量が与えたダメージの70%になる分は入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("吸い取り", 130) },
                },
                new EnemyBehavior
                {
                    Id = "glow-worm",
                    Moves = new[]
                    {
                        // 麻痺1ターンは入れない。
                        Attack("電撃", MagicAttack, 100),
                        Mend("傷ふさぎ", 60).WithRegen(2, 20),
                        Other("光の釣り糸", "味方への絡みつきの罠"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "光の釣り糸",
                        Choices = new[] { Weight("電撃", 65), Weight("光の釣り糸", 35) },
                        Once = new[] { "光の釣り糸" },
                        Rules = new[] { EnemyRule.When(Hurt, "傷ふさぎ").Times(1) },
                    },
                    // 激化で電撃の麻痺が2ターンに、光の釣り糸が2人に絡みつくようになる分は、
                    // どちらもないため入れない。
                    EnrageTurn = 5,
                },
                new EnemyBehavior
                {
                    Id = "ice-penguin",
                    Moves = new[]
                    {
                        Attack("二段突き", PhysicalAttack, 60, One, 2),
                        Boost("遠吠え", 2),
                        Guard("槍ぶすま", 60, Everyone),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "槍ぶすま",
                        OpeningAlone = "二段突き",
                        Choices = new[] { Weight("二段突き", 60), Weight("槍ぶすま", 40) },
                        Once = new[] { "槍ぶすま" },
                        Rules = new[] { EnemyRule.When(Together, "遠吠え").Apart(3) },
                    },
                    // 槍ぶすまの1.5倍はブロックの量の倍率として入れる。
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        new EnemyEnrage("二段突き", 130),
                        new EnemyEnrage("槍ぶすま", 150),
                    },
                },
                new EnemyBehavior
                {
                    Id = "crystal-hermit",
                    Moves = new[]
                    {
                        Attack("斬りつけ", PhysicalAttack, 100),
                        // 防御ダウン2ターンは入れない。
                        Attack("打ち砕き", PhysicalAttack, 60),
                        Guard("身を固める", 120),
                        Mend("傷ふさぎ", 60).WithRegen(2, 20),
                        Guard("水晶の共鳴", 60, Others, MagicAttack),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "水晶の共鳴",
                        OpeningAlone = "身を固める",
                        Choices = new[]
                        {
                            Weight("斬りつけ", 35),
                            Weight("打ち砕き", 25),
                            Weight("身を固める", 20),
                            Weight("水晶の共鳴", 20),
                        },
                        Once = new[] { "身を固める", "水晶の共鳴" },
                        Rules = new[]
                        {
                            EnemyRule.When(OtherHurt, "水晶の共鳴").Apart(2),
                            EnemyRule.When(Hurt, "傷ふさぎ").Times(2),
                        },
                    },
                    // 身を固めると水晶の共鳴の1.5倍はブロックの量の倍率として入れる。
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        new EnemyEnrage("身を固める", 150),
                        new EnemyEnrage("水晶の共鳴", 150),
                        new EnemyEnrage("斬りつけ", 130),
                        new EnemyEnrage("打ち砕き", 130),
                    },
                },
                new EnemyBehavior
                {
                    Id = "ore-mole",
                    Moves = new[]
                    {
                        // 出血2ターンは入れない。
                        Attack("引き裂き", PhysicalAttack, 60),
                        Attack("たいあたり", PhysicalAttack, 100),
                        Focus("力を溜める", 50),
                        Guard("身を固める", 120),
                        // 表では味方単体だが、ブロックを持つ味方がいるときの割り込みでブロックの最も
                        // 多い味方を狙うため MostBlock にする（だれもブロックがなければランダム）。
                        Attack("地中からの突き上げ", PhysicalAttack, 90, MostBlock).PastBlock(),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "身を固める",
                        Choices = new[]
                        {
                            Weight("引き裂き", 35),
                            Weight("たいあたり", 25),
                            Weight("地中からの突き上げ", 15),
                            Weight("身を固める", 15),
                            Weight("力を溜める", 10),
                        },
                        Once = new[] { "地中からの突き上げ", "身を固める", "力を溜める" },
                        Rules = new[]
                        {
                            EnemyRule.When(PartyGuarded, "地中からの突き上げ").Apart(2),
                        },
                    },
                    // 激化で引き裂きの出血が3ターンに延びる分は入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("地中からの突き上げ", 130) },
                },
            };
    }
}
