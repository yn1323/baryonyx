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
        // The kinds of the forest, the ruins and the roads, not placed in any stage yet.
        private static EnemyBehavior[] Forest() =>
            new[]
            {
                new EnemyBehavior
                {
                    Id = "dew-slime",
                    Moves = new[]
                    {
                        Attack("たいあたり", PhysicalAttack, 100),
                        // 凍え1ターンは入れない。
                        Attack("氷のつぶて", MagicAttack, 100),
                        Shield("露の膜", 2),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "露の膜",
                        OpeningAlone = "氷のつぶて",
                        Choices = new[] { Weight("氷のつぶて", 60), Weight("たいあたり", 40) },
                        Rules = new[]
                        {
                            EnemyRule.When(Together, "露の膜").Apart(3),
                            // 仲間がすべて倒れたあとに限る条件は重ねられないため、
                            // HPが半分以下になったときの1回だけとする。
                            EnemyRule.When(Hurt, "露の膜").Times(1),
                        },
                    },
                    // 激化で凍えが2ターンになる分は、凍えがないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("氷のつぶて", 130) },
                },
                new EnemyBehavior
                {
                    Id = "acorn-soldier",
                    Moves = new[]
                    {
                        Attack("二段突き", PhysicalAttack, 60, One, 2),
                        Guard("身を固める", 120),
                        Attack("どんぐりつぶて", PhysicalAttack, 40, EnemyAim.Random, 3),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "二段突き",
                        Choices = new[]
                        {
                            Weight("二段突き", 50),
                            Weight("どんぐりつぶて", 30),
                            Weight("身を固める", 20),
                        },
                        Once = new[] { "どんぐりつぶて" },
                        Rules = new[] { EnemyRule.When(Hurt, "身を固める").Times(2) },
                    },
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        new EnemyEnrage("二段突き", 130),
                        new EnemyEnrage("どんぐりつぶて", 100, 1),
                    },
                },
                new EnemyBehavior
                {
                    Id = "crystal-hare",
                    Moves = new[]
                    {
                        Attack("二段突き", PhysicalAttack, 60, One, 2),
                        Focus("力を溜める", 50),
                        Other("水晶角の照り返し", "単体攻撃の反射"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "水晶角の照り返し",
                        Choices = new[] { Weight("二段突き", 60), Weight("力を溜める", 40) },
                        // 力を溜めた次のターンの二段突きは、力を溜めるを2回続けないことで出す。
                        Once = new[] { "力を溜める" },
                        // 照り返しが残っているうちは使わない条件は入れない。
                        Rules = new[] { EnemyRule.When(Always, "水晶角の照り返し").Apart(3) },
                    },
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("二段突き", 130) },
                },
                new EnemyBehavior
                {
                    Id = "spore-cap",
                    Moves = new[]
                    {
                        Attack("たいあたり", PhysicalAttack, 100),
                        Other("胞子ばらまき", "味方全体への毒"),
                        Other("胞子の芽吹き", "味方の毒を増やす"),
                        Guard("身を固める", 120),
                        Mend("傷ふさぎ", 60).WithRegen(2, 20),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "胞子ばらまき",
                        Choices = new[]
                        {
                            Weight("たいあたり", 40),
                            Weight("胞子ばらまき", 30),
                            Weight("身を固める", 30),
                        },
                        Once = new[] { "胞子ばらまき" },
                        // 毒が2以上付いた味方が2人以上いるときの胞子の芽吹きは、
                        // 毒が見えないため入れない。
                        Rules = new[] { EnemyRule.When(Hurt, "傷ふさぎ").Times(2) },
                    },
                    // 激化で毒が3重ねになり、胞子の芽吹きが2ターンおきになる分は、
                    // 毒がないため入れない。
                    EnrageTurn = 5,
                },
                new EnemyBehavior
                {
                    Id = "thorn-serpent",
                    Moves = new[]
                    {
                        Attack("かみつき", PhysicalAttack, 100),
                        // 出血2ターンは入れない。
                        Attack("引き裂き", PhysicalAttack, 60),
                        Attack("なぎ払い", PhysicalAttack, 50, EnemyAim.All),
                        Other("にらみつけ", "味方単体への麻痺"),
                        // ブロックが残っている間の、攻撃した味方への反撃は入れない。
                        Guard("とげのとぐろ", 80),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "にらみつけ",
                        Choices = new[]
                        {
                            Weight("かみつき", 40),
                            Weight("引き裂き", 30),
                            Weight("なぎ払い", 30),
                        },
                        Once = new[] { "なぎ払い" },
                        Rules = new[]
                        {
                            EnemyRule.When(Always, "にらみつけ").Apart(4),
                            EnemyRule.When(Hurt, "とげのとぐろ").Apart(3),
                        },
                    },
                    // 激化で出血が3ターンになる分は、出血がないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("かみつき", 130) },
                },
                new EnemyBehavior
                {
                    Id = "stump-boar",
                    Moves = new[]
                    {
                        Attack("たいあたり", PhysicalAttack, 100),
                        Attack("地響き", PhysicalAttack, 50, EnemyAim.All),
                        Guard("身を固める", 120),
                        Focus("力を溜める", 50),
                        Attack("牙のかち上げ", PhysicalAttack, 100).Breaking(),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "力を溜める",
                        Choices = new[]
                        {
                            Weight("たいあたり", 40),
                            Weight("地響き", 30),
                            Weight("身を固める", 30),
                        },
                        Rules = new[]
                        {
                            // 2ターン目の牙のかち上げ。
                            EnemyRule.When(Always, "牙のかち上げ").After(2).Times(1),
                            EnemyRule.When(Always, "力を溜める").Apart(4).Next("牙のかち上げ"),
                            EnemyRule.When(PartyGuarded, "牙のかち上げ").Apart(2),
                        },
                    },
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        new EnemyEnrage("たいあたり", 130),
                        new EnemyEnrage("地響き", 130),
                    },
                },
                new EnemyBehavior
                {
                    Id = "nectar-bloom",
                    Moves = new[]
                    {
                        Attack("吸い取り", PhysicalAttack, 80).Draining(50),
                        Attack("なぎ払い", PhysicalAttack, 50, EnemyAim.All),
                        Other("胞子ばらまき", "味方全体への毒"),
                        Other("甘い蜜の香り", "味方全体への防御ダウン"),
                        Mend("傷ふさぎ", 60).WithRegen(2, 20),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "甘い蜜の香り",
                        Choices = new[]
                        {
                            Weight("吸い取り", 40),
                            Weight("なぎ払い", 30),
                            Weight("胞子ばらまき", 30),
                        },
                        Once = new[] { "胞子ばらまき" },
                        Rules = new[]
                        {
                            // 2ターン目の吸い取り。
                            EnemyRule.When(Always, "吸い取り").After(2).Times(1),
                            EnemyRule.When(Always, "甘い蜜の香り").Apart(4).Next("なぎ払い"),
                            EnemyRule.When(Hurt, "傷ふさぎ").Times(2),
                        },
                    },
                    // 激化で吸い取りの回復が100%に、防御ダウンが2ターンになる分は、
                    // 激化で変えられる値でないため入れない。
                    EnrageTurn = 5,
                },
                new EnemyBehavior
                {
                    Id = "chest-mimic",
                    Moves = new[]
                    {
                        Attack("かみつき", PhysicalAttack, 100),
                        // 防御ダウン2ターンは入れない。
                        Attack("打ち砕き", PhysicalAttack, 60),
                        Focus("力を溜める", 50),
                        Guard("身を固める", 120),
                        // 手札のカードを1枚、山札の一番下へ戻す分は入れない。
                        Attack("欲張りな舌", PhysicalAttack, 40),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "身を固める",
                        Choices = new[]
                        {
                            Weight("かみつき", 40),
                            Weight("打ち砕き", 25),
                            Weight("欲張りな舌", 20),
                            Weight("身を固める", 15),
                        },
                        Once = new[] { "身を固める", "欲張りな舌" },
                        Rules = new[]
                        {
                            EnemyRule.When(Hurt, "力を溜める").Times(1).Next("かみつき"),
                        },
                    },
                    // 激化で奪うカードが2枚になる分は、カードを奪えないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("かみつき", 130) },
                },
                new EnemyBehavior
                {
                    Id = "coin-pouch",
                    Moves = new[]
                    {
                        Attack("たいあたり", PhysicalAttack, 100),
                        Guard("身を固める", 120),
                        Other("すたこら逃げ", "戦闘から逃げる"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "身を固める",
                        Choices = new[] { Weight("たいあたり", 60), Weight("身を固める", 40) },
                        Rules = new[]
                        {
                            EnemyRule.When(Hurt, "すたこら逃げ"),
                            // 激化：5ターン目の行動は必ずすたこら逃げ。前のターンからの
                            // 「逃走」の予告は入れない。
                            EnemyRule.When(Always, "すたこら逃げ").After(5),
                        },
                    },
                    EnrageTurn = 5,
                },
                new EnemyBehavior
                {
                    Id = "lost-signpost",
                    Moves = new[]
                    {
                        Attack("たいあたり", PhysicalAttack, 100),
                        Guard("身を固める", 120),
                        Other("道案内", "仲間の次の単体攻撃を弱った味方へ向けて強める"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "道案内",
                        OpeningAlone = "たいあたり",
                        Choices = new[] { Weight("たいあたり", 60), Weight("身を固める", 40) },
                        Once = new[] { "身を固める" },
                        Rules = new[] { EnemyRule.When(Together, "道案内").Apart(3) },
                    },
                    // 激化で道案内の倍率が1.5倍になる分は、道案内がないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("たいあたり", 130) },
                },
            };
    }
}
