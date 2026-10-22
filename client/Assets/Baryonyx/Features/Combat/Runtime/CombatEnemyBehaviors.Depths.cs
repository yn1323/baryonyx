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
        // The kinds first met in the Glaciera ice cave and the Hush Mire.
        private static EnemyBehavior[] Depths() =>
            new[]
            {
                new EnemyBehavior
                {
                    Id = "blind-fishman",
                    Moves = new[]
                    {
                        Attack("二段突き", PhysicalAttack, 60, One, 2),
                        // 凍え1ターンは入れない。
                        Attack("氷のつぶて", MagicAttack, 100),
                        Guard("身を固める", 120),
                        Mend("傷ふさぎ", 60).WithRegen(2, 20),
                        // 前の味方のターンに最も多くカードを使った味方は狙えないため、ランダムな1人とする。
                        Attack("波紋聞き", PhysicalAttack, 120),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "身を固める",
                        Choices = new[]
                        {
                            Weight("二段突き", 45),
                            Weight("氷のつぶて", 35),
                            Weight("身を固める", 20),
                        },
                        Once = new[] { "身を固める" },
                        // 1人の味方が3枚以上のカードを使ったときの波紋聞きは、使った枚数が見えないため入れない。
                        Rules = new[] { EnemyRule.When(Hurt, "傷ふさぎ").Times(1) },
                    },
                    // 激化で波紋聞きの条件が2枚以上になる分は、その条件がないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("二段突き", 130) },
                },
                new EnemyBehavior
                {
                    Id = "icicle-fox",
                    Moves = new[]
                    {
                        // 凍え1ターンは入れない。
                        Attack("氷のつぶて", MagicAttack, 100),
                        // 凍え2ターンは入れない。
                        Attack("冷たい息", MagicAttack, 50, EnemyAim.All),
                        Other("にらみつけ", "味方単体への麻痺"),
                        Attack("かみつき", PhysicalAttack, 100),
                        // 凍えた味方を優先して狙う分と、凍え・凍結は入れない。
                        Attack("つらら刺し", MagicAttack, 80),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "にらみつけ",
                        Choices = new[]
                        {
                            Weight("氷のつぶて", 40),
                            Weight("冷たい息", 30),
                            Weight("かみつき", 30),
                        },
                        MaxRun = 1,
                        // 凍えが付いた味方がいるときのつらら刺しは、凍えがないため入れない。
                        Rules = new[] { EnemyRule.When(Always, "にらみつけ").Apart(4) },
                    },
                    // 激化で氷のつぶての凍えが2ターンになる分は、凍えがないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("つらら刺し", 130) },
                },
                new EnemyBehavior
                {
                    Id = "rock-troll",
                    Moves = new[]
                    {
                        // 防御ダウン2ターンは入れない。
                        Attack("打ち砕き", PhysicalAttack, 60),
                        Attack("地響き", PhysicalAttack, 50, EnemyAim.All),
                        Attack("渾身の一撃", PhysicalAttack, 180).Charged(1),
                        Guard("身を固める", 120),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        Attack("肩石投げ", PhysicalAttack, 80).Splash(30),
                        // 失ったHPの20%の回復を、HPが半分のときと同じ最大HPの10%で近似する。
                        Mend("岩肌の再生", 10, Self, None),
                        // 絡みつき1ターンは入れない。溜めのターンに身を固めるも使う。
                        Attack("洞窟崩し", PhysicalAttack, 120, EnemyAim.All)
                            .Charged(1, "身を固める"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "肩石投げ",
                        // 1周ごとの渾身の一撃（溜めと合わせて2ターン）を、順番の最後に置く。
                        Order = new[]
                        {
                            "打ち砕き",
                            "地響き",
                            "肩石投げ",
                            "身を固める",
                            "渾身の一撃",
                        },
                        Rules = new[]
                        {
                            EnemyRule.When(Hurt, "岩肌の再生", 0.7f).Apart(3),
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                        },
                    },
                    LateTurn = 6,
                    LateMove = "岩肌の再生",
                    Late = new EnemyPattern
                    {
                        Cycle = new[] { Pick, Pick, "洞窟崩し" },
                        Choices = new[]
                        {
                            Weight("打ち砕き", 1),
                            Weight("地響き", 1),
                            Weight("肩石投げ", 1),
                        },
                        MaxRun = 1,
                        Rules = new[] { EnemyRule.When(Alone, "仲間を呼ぶ").Times(2) },
                    },
                },
                new EnemyBehavior
                {
                    Id = "blood-leech",
                    Moves = new[]
                    {
                        Attack("吸い取り", PhysicalAttack, 80).Draining(50),
                        Guard("身を固める", 120),
                        // 傷口2ターンは入れない。
                        Attack("ふさがらない噛み傷", PhysicalAttack, 50),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "ふさがらない噛み傷",
                        Choices = new[]
                        {
                            Weight("吸い取り", 50),
                            Weight("ふさがらない噛み傷", 30),
                            Weight("身を固める", 20),
                        },
                        Once = new[] { "身を固める" },
                        // 傷口の有無で狙う味方と技を変える分は、傷口がないため入れない。
                        // HPが半分以下になった直後の1回とする。
                        Rules = new[] { EnemyRule.When(Hurt, "吸い取り").Times(1) },
                    },
                    // 激化で吸い取りの回復が与えたダメージの100%になる分は、回復の割合を変えられないため入れない。
                    EnrageTurn = 5,
                },
                new EnemyBehavior
                {
                    Id = "will-o-wisp",
                    Moves = new[]
                    {
                        // やけど1ターンは入れない。
                        Attack("火の玉", MagicAttack, 100),
                        Other("金切り声", "味方全体への麻痺"),
                        Other("迷い火の誘い", "味方単体への惑い"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "迷い火の誘い",
                        Choices = new[]
                        {
                            Weight("火の玉", 60),
                            Weight("金切り声", 20),
                            Weight("迷い火の誘い", 20),
                        },
                        Once = new[] { "金切り声", "迷い火の誘い" },
                        // 惑いの有無で狙う味方と技を変える分と、仲間がいないときに迷い火の誘いを
                        // 使わない分は、惑いがないため入れない。
                    },
                    // 激化でやけどが2ターンになる分は、やけどがないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("火の玉", 130) },
                },
                new EnemyBehavior
                {
                    Id = "frog-king",
                    Moves = new[]
                    {
                        Attack("たいあたり", PhysicalAttack, 100),
                        Other("にらみつけ", "味方単体への麻痺"),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        // 標的の強化を1つ奪う分は入れない。
                        Attack("舌のかすめ取り", PhysicalAttack, 60),
                        // 属攻の30%×立っている敵の数を、使う条件の2体のときの60%で近似する。
                        Attack("沼の大合唱", MagicAttack, 60, EnemyAim.All),
                    },
                    Early = new EnemyPattern
                    {
                        // 空いた立ち位置の有無は見えないため、仲間がいなければ仲間を呼ぶとする。
                        Opening = "にらみつけ",
                        OpeningAlone = "仲間を呼ぶ",
                        Choices = new[]
                        {
                            Weight("たいあたり", 40),
                            Weight("舌のかすめ取り", 30),
                            Weight("にらみつけ", 30),
                        },
                        Once = new[] { "にらみつけ" },
                        // 強化の付いた味方を狙い、舌のかすめ取りの割合を上げる分は、強化が見えないため入れない。
                        Rules = new[]
                        {
                            EnemyRule.When(Together, "沼の大合唱").Apart(3),
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                        },
                    },
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        new EnemyEnrage("沼の大合唱", 150),
                        new EnemyEnrage("たいあたり", 130),
                    },
                },
                new EnemyBehavior
                {
                    Id = "mud-golem",
                    Moves = new[]
                    {
                        Attack("たいあたり", PhysicalAttack, 100),
                        // 防御ダウン2ターンと、防御ダウンの付いていない味方を狙う分は入れない。
                        Attack("打ち砕き", PhysicalAttack, 60),
                        Guard("身を固める", 120),
                        Mend("傷ふさぎ", 60).WithRegen(2, 20),
                        // 自分のHPが最大HPの30%以下のときは使わない条件は入れない。
                        Mend("泥の継ぎ当て", 100, WeakestOther).Costing(10),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "身を固める",
                        Choices = new[]
                        {
                            Weight("たいあたり", 40),
                            Weight("打ち砕き", 30),
                            Weight("身を固める", 30),
                        },
                        Once = new[] { "身を固める" },
                        // 重なったときは傷ふさぎを先にするため、文書の順と入れ替える。
                        Rules = new[]
                        {
                            EnemyRule.When(Hurt, "傷ふさぎ").Apart(3),
                            EnemyRule.When(OtherHurt, "泥の継ぎ当て").Apart(3),
                        },
                    },
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        // 身を固めるは、ブロックを1.5倍にする。
                        new EnemyEnrage("身を固める", 150),
                        new EnemyEnrage("たいあたり", 130),
                        new EnemyEnrage("打ち砕き", 130),
                    },
                },
                new EnemyBehavior
                {
                    Id = "bone-croc",
                    Moves = new[]
                    {
                        Attack("かみつき", PhysicalAttack, 100),
                        // 出血2ターンは入れない。
                        Attack("引き裂き", PhysicalAttack, 60),
                        Attack("なぎ払い", PhysicalAttack, 50, EnemyAim.All),
                        Guard("身を固める", 120),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        Other("丸のみ", "自分以外の敵を倒して回復する"),
                        // 出血している味方を狙い、その味方へのダメージを1.5倍にする分は、
                        // 出血がないため入れない。後半へ切り替えるだけの技にする。
                        Awaken("血の匂い"),
                        // 凍結は入れない。出血している味方を優先する分は入れず、HPの割合が
                        // 最も低い味方を狙う。溜めのターンに身を固めるも使う。
                        Attack("死の回転", PhysicalAttack, 160, Weakest).Charged(1, "身を固める"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "かみつき",
                        Order = new[] { "かみつき", "引き裂き", "身を固める", "なぎ払い" },
                        // 丸のみの、自分以外の敵がいるとき、の条件は重ねられないため入れない。
                        Rules = new[]
                        {
                            EnemyRule.When(Hurt, "丸のみ", 0.7f).Apart(3),
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                        },
                    },
                    LateTurn = 6,
                    LateMove = "血の匂い",
                    Late = new EnemyPattern
                    {
                        Cycle = new[] { Pick, Pick, "死の回転" },
                        Choices = new[]
                        {
                            Weight("かみつき", 1),
                            Weight("引き裂き", 1),
                            Weight("なぎ払い", 1),
                        },
                        MaxRun = 1,
                        // 出血している味方がいないときに引き裂きを選ぶ分は、出血がないため入れない。
                        Rules = new[]
                        {
                            EnemyRule.When(Hurt, "丸のみ", 0.7f).Apart(3),
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                        },
                    },
                },
            };
    }
}
