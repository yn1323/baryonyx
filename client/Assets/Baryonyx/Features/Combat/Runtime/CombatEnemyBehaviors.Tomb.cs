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
        // The kinds of the old ruins and the desert tombs, not placed in any stage yet.
        private static EnemyBehavior[] Tomb() =>
            new[]
            {
                new EnemyBehavior
                {
                    Id = "cracked-urn",
                    Moves = new[]
                    {
                        Attack("かみつき", PhysicalAttack, 100),
                        Guard("身を固める", 120),
                        // 強化をすべて吸い込んで消す分と、強化の最も多い味方を狙う分は入れない。
                        Attack("壺の吸い込み", PhysicalAttack, 50),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "身を固める",
                        Choices = new[] { Weight("かみつき", 60), Weight("身を固める", 40) },
                        // 強化の付いた味方がいるときの壺の吸い込みは、強化が見えないため入れない。
                    },
                    EnrageTurn = 5,
                    // 壺の吸い込みが物攻の80%になる分は、50%の1.6倍として入れる。
                    Enrage = new[]
                    {
                        new EnemyEnrage("かみつき", 130),
                        new EnemyEnrage("壺の吸い込み", 160),
                    },
                },
                new EnemyBehavior
                {
                    Id = "floating-grimoire",
                    Moves = new[]
                    {
                        // 凍え1ターンは入れない。
                        Attack("氷のつぶて", MagicAttack, 100),
                        Other("にらみつけ", "味方単体への麻痺"),
                        // 2ターン後の行動の前に届く詠唱は、2ターンの溜めで近づけた。詠唱の間も
                        // 技を使う分と、届く前に倒されると消える分は入れない。
                        Attack("頁の詠唱", MagicAttack, 100, EnemyAim.All).Charged(2),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "頁の詠唱",
                        Choices = new[] { Weight("氷のつぶて", 60), Weight("にらみつけ", 40) },
                        Once = new[] { "にらみつけ" },
                        Rules = new[] { EnemyRule.When(Always, "頁の詠唱").Apart(2) },
                    },
                    // 激化で凍えが2ターンになる分と、頁の詠唱が届くまでが1ターンに縮む分は、
                    // 激化で変えられる値でないため入れない。
                    EnrageTurn = 5,
                },
                new EnemyBehavior
                {
                    Id = "hourglass-sprite",
                    Moves = new[]
                    {
                        // 麻痺1ターンは入れない。
                        Attack("電撃", MagicAttack, 100),
                        Guard("身を固める", 120),
                        // 前に行動した直後のHPへ戻す分は、上限の属攻の150%の回復で近づけた。
                        Mend("時の巻き戻し", 150),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "電撃",
                        Choices = new[] { Weight("電撃", 65), Weight("身を固める", 35) },
                        Once = new[] { "身を固める" },
                        // 前に行動したあとに最大HPの25%以上を失ったときの時の巻き戻しは、
                        // 失った量が見えないため入れない。
                    },
                    // 激化で麻痺が2ターンになる分は、麻痺がないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("電撃", 130) },
                },
                new EnemyBehavior
                {
                    Id = "cactus-warrior",
                    Moves = new[]
                    {
                        Attack("二段突き", PhysicalAttack, 60, One, 2),
                        // 物防の最も高い味方を狙う分は入れない。
                        Attack("針千本", PhysicalAttack, 50).Piercing(),
                        Mend("傷ふさぎ", 60).WithRegen(2, 20),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "二段突き",
                        Choices = new[] { Weight("二段突き", 60), Weight("針千本", 40) },
                        Rules = new[] { EnemyRule.When(Hurt, "傷ふさぎ").Times(1) },
                    },
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        new EnemyEnrage("二段突き", 130),
                        new EnemyEnrage("針千本", 130),
                    },
                },
                new EnemyBehavior
                {
                    Id = "hollow-armor",
                    Moves = new[]
                    {
                        Attack("斬りつけ", PhysicalAttack, 100),
                        // 防御ダウン2ターンは入れない。
                        Attack("打ち砕き", PhysicalAttack, 60),
                        Focus("力を溜める", 50),
                        // 次の味方ターンの間、仲間への単体攻撃を引き受ける分は入れない。
                        Guard("番兵の盾", 60),
                        Other("空ろの鎧ばらし", "無敵と、次の行動の休み"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "番兵の盾",
                        OpeningAlone = "力を溜める",
                        Choices = new[]
                        {
                            Weight("斬りつけ", 50),
                            Weight("打ち砕き", 30),
                            Weight("力を溜める", 20),
                        },
                        // 力を溜めた次のターンの斬りつけか打ち砕きは、力を溜めるを2回続けない
                        // ことで出す。
                        Once = new[] { "力を溜める" },
                        Rules = new[]
                        {
                            EnemyRule.When(OtherHurt, "番兵の盾").Apart(3),
                            EnemyRule.When(Hurt, "空ろの鎧ばらし").Times(1),
                        },
                    },
                    // 激化で防御ダウンが3ターンになる分は、防御ダウンがないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("斬りつけ", 130) },
                },
                new EnemyBehavior
                {
                    Id = "stone-gargoyle",
                    Moves = new[]
                    {
                        // 出血2ターンは入れない。
                        Attack("引き裂き", PhysicalAttack, 60),
                        Other("にらみつけ", "味方単体への麻痺"),
                        Guard("身を固める", 120),
                        // 飛び上がっている間、味方の単体攻撃が当たらない分は入れない。
                        Attack("屋根からの急降下", PhysicalAttack, 150).Charged(1),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "身を固める",
                        // 「選ぶ・選ぶ・飛び上がる・急降下」の飛び上がるは、急降下の溜めで出す。
                        Cycle = new[] { Pick, Pick, "屋根からの急降下" },
                        Choices = new[]
                        {
                            Weight("引き裂き", 50),
                            Weight("身を固める", 30),
                            Weight("にらみつけ", 20),
                        },
                        MaxRun = 1,
                        Rules = new[] { EnemyRule.When(Alone, "仲間を呼ぶ").Times(1) },
                    },
                    // 激化で出血が3ターンになる分は、出血がないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("屋根からの急降下", 130) },
                },
                new EnemyBehavior
                {
                    Id = "wrapped-jackal",
                    Moves = new[]
                    {
                        Attack("かみつき", PhysicalAttack, 100),
                        // 出血2ターンは入れない。
                        Attack("引き裂き", PhysicalAttack, 60),
                        Boost("遠吠え", 2),
                        Other("にらみつけ", "味方単体への麻痺"),
                        Other("墓守の呼び覚まし", "倒れた敵を起き上がらせる"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "遠吠え",
                        OpeningAlone = "にらみつけ",
                        Choices = new[]
                        {
                            Weight("かみつき", 40),
                            Weight("引き裂き", 35),
                            Weight("にらみつけ", 25),
                        },
                        Once = new[] { "にらみつけ" },
                        // 倒れた仲間がいて立ち位置が空いているときの墓守の呼び覚ましは、
                        // 倒れた敵が見えないため入れない。
                        Rules = new[] { EnemyRule.When(Together, "遠吠え").Apart(3) },
                    },
                    // 激化で出血が3ターンになる分は、出血がないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        new EnemyEnrage("かみつき", 130),
                        new EnemyEnrage("引き裂き", 130),
                    },
                },
                new EnemyBehavior
                {
                    Id = "sand-scorpion",
                    Moves = new[]
                    {
                        Attack("斬りつけ", PhysicalAttack, 100),
                        // 毒2は入れない。
                        Attack("毒針", PhysicalAttack, 60),
                        Guard("身を固める", 120),
                        Focus("力を溜める", 50),
                        Other("毒まわし", "味方の毒の残りを一度に与える"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "力を溜める",
                        Choices = new[]
                        {
                            Weight("毒針", 40),
                            Weight("斬りつけ", 35),
                            Weight("身を固める", 25),
                        },
                        Once = new[] { "身を固める" },
                        // 力を溜めた次の行動を斬りつけか毒針に限る条件は入れない。毒の重なりが
                        // 3以上の味方がいるときの毒まわしは、毒が見えないため入れない。
                        Rules = new[] { EnemyRule.When(Always, "力を溜める").Apart(4) },
                    },
                    // 激化で毒が3になる分は、毒がないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("斬りつけ", 130) },
                },
                new EnemyBehavior
                {
                    Id = "brass-warden",
                    Moves = new[]
                    {
                        // 防御ダウン2ターンは入れない。
                        Attack("打ち砕き", PhysicalAttack, 60),
                        Attack("地響き", PhysicalAttack, 50, EnemyAim.All),
                        Guard("身を固める", 120),
                        Focus("力を溜める", 50),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        // 自分の状態異常をすべて消す分は入れない。
                        Guard("蒸気の排気", 60),
                        // 速度アップと、敵のターンの開始時に最大HPの3%を失う過熱は入れない。
                        Boost("炉心の全開", Forever, Self),
                        // 溜めのターンに身を固めるも使う。
                        Attack("蒼炉の砲撃", MagicAttack, 130, EnemyAim.All)
                            .Costing(10)
                            .Charged(1, "身を固める"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "身を固める",
                        Order = new[] { "打ち砕き", "力を溜める", "地響き", "身を固める" },
                        // 状態異常が2つ以上付いたときの蒸気の排気は、状態異常が見えないため
                        // 入れない。
                        Rules = new[] { EnemyRule.When(Alone, "仲間を呼ぶ").Times(2) },
                    },
                    LateTurn = 6,
                    LateMove = "炉心の全開",
                    Late = new EnemyPattern
                    {
                        // 「選ぶ・溜め・蒼炉の砲撃」の溜めは、砲撃の溜めで出す。
                        Cycle = new[] { Pick, "蒼炉の砲撃" },
                        Choices = new[] { Weight("打ち砕き", 1), Weight("地響き", 1) },
                        MaxRun = 1,
                        Rules = new[] { EnemyRule.When(Alone, "仲間を呼ぶ").Times(2) },
                    },
                },
                new EnemyBehavior
                {
                    Id = "sand-shark",
                    Moves = new[]
                    {
                        Attack("かみつき", PhysicalAttack, 100),
                        Attack("二段突き", PhysicalAttack, 60, One, 2),
                        // 出血2ターンは入れない。
                        Attack("引き裂き", PhysicalAttack, 60),
                        Attack("なぎ払い", PhysicalAttack, 50, EnemyAim.All),
                        Focus("力を溜める", 50),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        // 技を2つ続けて使う分は、後半の ActionsPerTurn で表す。
                        Other("狩りの狂乱", "行動の番ごとに技を2つ使う"),
                        // 麻痺2ターンは入れない。
                        Attack("砂の大波", PhysicalAttack, 120, EnemyAim.All).Charged(1),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "かみつき",
                        Order = new[]
                        {
                            "かみつき",
                            "二段突き",
                            "なぎ払い",
                            "力を溜める",
                            "引き裂き",
                        },
                        Rules = new[] { EnemyRule.When(Alone, "仲間を呼ぶ").Times(2) },
                    },
                    LateTurn = 6,
                    LateMove = "狩りの狂乱",
                    Late = new EnemyPattern
                    {
                        // 「選ぶ・選ぶ・溜め・砂の大波」の1組。選ぶターンは技を2つ続けて使い、
                        // 溜めと大技のターンは2つ目を使わない。
                        Cycle = new[] { Pick, Pick, "砂の大波" },
                        Choices = new[]
                        {
                            Weight("かみつき", 1),
                            Weight("二段突き", 1),
                            Weight("引き裂き", 1),
                            Weight("なぎ払い", 1),
                        },
                        MaxRun = 1,
                        ActionsPerTurn = 2,
                        // 仲間を呼ぶの割り込みを選ぶターンの1つ目に限る条件は入れない。
                        Rules = new[] { EnemyRule.When(Alone, "仲間を呼ぶ").Times(2) },
                    },
                },
            };
    }
}
