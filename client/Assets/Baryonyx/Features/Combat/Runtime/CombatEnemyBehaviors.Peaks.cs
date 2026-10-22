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
        // Kinds of the ruins, graveyard, volcano, snowfield and sky (none placed yet).
        private static EnemyBehavior[] Peaks() =>
            new[]
            {
                new EnemyBehavior
                {
                    Id = "cursed-candelabra",
                    Moves = new[]
                    {
                        // やけど1ターンは入れない。
                        Attack("火の玉", MagicAttack, 100),
                        // やけど2ターンは入れない。
                        Attack("火の息", MagicAttack, 50, EnemyAim.All),
                        Other("延焼", "味方全体へのやけどの付与と延長"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "火の玉",
                        Choices = new[]
                        {
                            Weight("火の玉", 50),
                            Weight("火の息", 30),
                            Weight("延焼", 20),
                        },
                        Once = new[] { "火の息", "延焼" },
                        // やけどの付いた味方が2人以上のときの延焼は、やけどが見えないため入れない。
                    },
                    // 激化はやけどを延ばすだけのため、始まりだけを残し、技の変化は入れない。
                    EnrageTurn = 5,
                },
                new EnemyBehavior
                {
                    Id = "skeleton-soldier",
                    Moves = new[]
                    {
                        Attack("斬りつけ", PhysicalAttack, 100),
                        // 防御ダウン2ターンは入れない。
                        Attack("打ち砕き", PhysicalAttack, 60),
                        Guard("身を固める", 120),
                        Focus("力を溜める", 50),
                        Other("組み直し", "倒れたあとの復活"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "斬りつけ",
                        Choices = new[]
                        {
                            Weight("斬りつけ", 40),
                            Weight("打ち砕き", 30),
                            Weight("身を固める", 30),
                        },
                        Once = new[] { "身を固める" },
                        // HPが0になったときの組み直しは、復活がないため入れない。
                        Rules = new[]
                        {
                            EnemyRule.When(Always, "力を溜める").Apart(4).After(4).Next("斬りつけ"),
                        },
                    },
                    // 組み直したあとから激化する分は、組み直しがないため入れない。
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        new EnemyEnrage("斬りつけ", 130),
                        new EnemyEnrage("打ち砕き", 130),
                    },
                },
                new EnemyBehavior
                {
                    Id = "grave-raven",
                    Moves = new[]
                    {
                        // やけど1ターンは入れない。
                        Attack("火の玉", MagicAttack, 100),
                        Attack("二段突き", PhysicalAttack, 60, One, 2),
                        Other("金切り声", "味方全体への麻痺"),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        // まねたカードの区分・属性と、それを使った味方を狙う分は入れず、
                        // 魔法の属攻80%で単体に当てる形にした。
                        Attack("鴉のものまね", MagicAttack, 80),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "金切り声",
                        Choices = new[] { Weight("火の玉", 45), Weight("二段突き", 55) },
                        // 前の味方ターンに魔法のカードが使われたときの鴉のものまねは、
                        // 使われたカードが見えないため入れない。
                        Rules = new[]
                        {
                            EnemyRule.When(Always, "金切り声").Apart(4),
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(1),
                        },
                    },
                    // 激化でやけどが2ターンになる分は入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("火の玉", 130) },
                },
                new EnemyBehavior
                {
                    Id = "lich",
                    Moves = new[]
                    {
                        // 凍え1ターンは入れない。
                        Attack("氷のつぶて", MagicAttack, 100),
                        // 凍え2ターンは入れない。
                        Attack("冷たい息", MagicAttack, 50, EnemyAim.All),
                        Other("にらみつけ", "味方単体への麻痺"),
                        Guard("身を固める", 120),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        Other("朽ちの呪い", "味方単体への呪い"),
                        // 単体の技を全体へ広げる効果は、後半の SpreadPercent で表す。
                        Awaken("王冠の覚醒"),
                        // 詠唱の間に炎か打で最大HPの10%以上を受けると不発になる分は入れない。
                        Attack("葬送の詠唱", MagicAttack, 150, EnemyAim.All).Charged(2),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "にらみつけ",
                        Order = new[] { "氷のつぶて", "冷たい息", "朽ちの呪い", "身を固める" },
                        // 朽ちの呪いの標的（防御のカードを最後に使った味方）は入れない。
                        Rules = new[] { EnemyRule.When(Alone, "仲間を呼ぶ").Times(2) },
                    },
                    LateTurn = 6,
                    LateMove = "王冠の覚醒",
                    Late = new EnemyPattern
                    {
                        Cycle = new[] { Pick, Pick, "葬送の詠唱" },
                        // にらみつけを1組に1回までにする分は入れない。
                        Choices = new[]
                        {
                            Weight("氷のつぶて", 1),
                            Weight("冷たい息", 1),
                            Weight("朽ちの呪い", 1),
                            Weight("にらみつけ", 1),
                        },
                        MaxRun = 1,
                        Rules = new[] { EnemyRule.When(Alone, "仲間を呼ぶ").Times(2) },
                        SpreadPercent = 60,
                    },
                },
                new EnemyBehavior
                {
                    Id = "lava-slime",
                    Moves = new[]
                    {
                        // やけど1ターンは入れない。
                        Attack("火の玉", MagicAttack, 100),
                        Guard("身を固める", 120),
                        // 自分への防御ダウン2ターンは入れない。
                        Attack("殻はじけ", MagicAttack, 80, EnemyAim.All),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "身を固める",
                        Choices = new[] { Weight("火の玉", 60), Weight("身を固める", 40) },
                        // 激化で殻はじけの間隔が3ターンに縮む分は、
                        // 5ターン目からの2つめの規則で表す。
                        Rules = new[]
                        {
                            EnemyRule.When(Always, "殻はじけ").Apart(4).After(3).Next("身を固める"),
                            EnemyRule.When(Always, "殻はじけ").Apart(3).After(5).Next("身を固める"),
                        },
                    },
                    // 激化で殻はじけにやけど1ターンが加わる分は入れない。
                    EnrageTurn = 5,
                },
                new EnemyBehavior
                {
                    Id = "fire-lizard",
                    Moves = new[]
                    {
                        // やけど1ターンは入れない。
                        Attack("火の玉", MagicAttack, 100),
                        // やけど2ターンは入れない。
                        Attack("火の息", MagicAttack, 50, EnemyAim.All),
                        Attack("かみつき", PhysicalAttack, 100),
                        Mend("傷ふさぎ", 60).WithRegen(2, 20),
                        Other("燃えるうろこ", "物理の攻撃をしてきた味方へのやけど"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "火の玉",
                        Choices = new[]
                        {
                            Weight("火の玉", 40),
                            Weight("火の息", 30),
                            Weight("かみつき", 30),
                        },
                        MaxRun = 1,
                        // 物理の攻撃を2回以上受けたあとの燃えるうろこは、
                        // 受けた攻撃が見えないため入れない。
                        Rules = new[] { EnemyRule.When(Hurt, "傷ふさぎ").Times(2) },
                    },
                    // 激化はやけどを延ばすだけのため、始まりだけを残し、技の変化は入れない。
                    EnrageTurn = 5,
                },
                new EnemyBehavior
                {
                    Id = "flame-tail-bird",
                    Moves = new[]
                    {
                        Attack("二段突き", PhysicalAttack, 60, One, 2),
                        // やけど2ターンは入れない。
                        Attack("火の息", MagicAttack, 50, EnemyAim.All),
                        Other("金切り声", "味方全体への麻痺"),
                        // 次の味方ターンの開始時に燃え上がる時間差は入れず、使ったときに当てる。
                        Attack("火の尾羽落とし", MagicAttack, 60, EnemyAim.All),
                        Other("熱風の羽ばたき", "自分と仲間の速度アップ"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "熱風の羽ばたき",
                        Choices = new[]
                        {
                            Weight("二段突き", 40),
                            Weight("火の息", 30),
                            Weight("火の尾羽落とし", 30),
                        },
                        // 尾羽が燃え上がるまで火の尾羽落としを選ばない分は、2回続けないことで表す。
                        MaxRun = 1,
                        Rules = new[]
                        {
                            EnemyRule.When(Always, "熱風の羽ばたき").Apart(3),
                            EnemyRule.When(Hurt, "金切り声").Apart(4),
                        },
                    },
                    // 激化で火の尾羽落としにやけど2ターンが加わる分は入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("二段突き", 130) },
                },
                new EnemyBehavior
                {
                    Id = "obsidian-colossus",
                    Moves = new[]
                    {
                        // 防御ダウン2ターンは入れない。
                        Attack("打ち砕き", PhysicalAttack, 60),
                        Attack("斬りつけ", PhysicalAttack, 100),
                        Attack("地響き", PhysicalAttack, 50, EnemyAim.All),
                        // やけど2ターンは入れない。
                        Attack("火の息", MagicAttack, 50, EnemyAim.All),
                        Guard("身を固める", 120),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        Other("溶岩のあふれ", "自分への防御ダウンと、攻撃の技へのやけどの追加"),
                        // 出血2ターンと、溶岩のあふれのあとのやけど1ターンは入れない。
                        Attack("黒曜の破片嵐", PhysicalAttack, 110, EnemyAim.All).Charged(1),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "身を固める",
                        Order = new[] { "斬りつけ", "打ち砕き", "地響き", "身を固める" },
                        Rules = new[]
                        {
                            EnemyRule.When(Always, "火の息").Apart(4),
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                        },
                    },
                    LateTurn = 6,
                    LateMove = "溶岩のあふれ",
                    Late = new EnemyPattern
                    {
                        Cycle = new[] { Pick, Pick, "黒曜の破片嵐" },
                        Choices = new[]
                        {
                            Weight("斬りつけ", 1),
                            Weight("打ち砕き", 1),
                            Weight("地響き", 1),
                            Weight("火の息", 1),
                        },
                        MaxRun = 1,
                        Rules = new[] { EnemyRule.When(Alone, "仲間を呼ぶ").Times(2) },
                    },
                },
                new EnemyBehavior
                {
                    Id = "snowball",
                    Moves = new[]
                    {
                        Attack("たいあたり", PhysicalAttack, 100),
                        Guard("身を固める", 120),
                        Mend("ころがり太り", 80).WithPowerUp(50),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "身を固める",
                        Choices = new[] { Weight("たいあたり", 60), Weight("身を固める", 40) },
                        Rules = new[]
                        {
                            EnemyRule.When(Hurt, "ころがり太り").Apart(3).Next("たいあたり"),
                        },
                    },
                    // 激化でたいあたりを選ぶ割合を80%に上げる分は入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("身を固める", 150) },
                },
                new EnemyBehavior
                {
                    Id = "frost-ape",
                    Moves = new[]
                    {
                        // 防御ダウン2ターンは入れない。
                        Attack("打ち砕き", PhysicalAttack, 60),
                        // 出血2ターンは入れない。
                        Attack("引き裂き", PhysicalAttack, 60),
                        // 凍え2ターンは入れない。
                        Attack("冷たい息", MagicAttack, 50, EnemyAim.All),
                        Boost("遠吠え", 2),
                        Focus("力を溜める", 50),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        Shield("霜の鎧毛", Forever, Self),
                        // 絡みつき1ターンは入れない。
                        Attack("雪崩おとし", PhysicalAttack, 100, EnemyAim.All).Charged(1),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "遠吠え",
                        OpeningAlone = "仲間を呼ぶ",
                        Order = new[] { "力を溜める", "打ち砕き", "引き裂き", "冷たい息" },
                        Rules = new[]
                        {
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                            EnemyRule.When(Together, "遠吠え").Apart(4),
                        },
                    },
                    LateTurn = 6,
                    LateMove = "霜の鎧毛",
                    Late = new EnemyPattern
                    {
                        Cycle = new[] { Pick, Pick, "雪崩おとし" },
                        Choices = new[]
                        {
                            Weight("打ち砕き", 1),
                            Weight("引き裂き", 1),
                            Weight("冷たい息", 1),
                        },
                        MaxRun = 1,
                        Rules = new[]
                        {
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                            EnemyRule.When(Together, "遠吠え").Apart(4),
                        },
                    },
                },
                new EnemyBehavior
                {
                    Id = "snow-mammoth",
                    Moves = new[]
                    {
                        Attack("たいあたり", PhysicalAttack, 100),
                        Attack("地響き", PhysicalAttack, 50, EnemyAim.All),
                        // 防御ダウン2ターンは入れない。
                        Attack("打ち砕き", PhysicalAttack, 60),
                        // 凍え2ターンは入れない。
                        Attack("冷たい息", MagicAttack, 50, EnemyAim.All),
                        Guard("身を固める", 120),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        // 戦闘の終わりまで行動の前に毎回得るブロックは、使ったときの1回だけにした。
                        Guard("降り積もる雪", 40),
                        // 残っている自分のブロックの50%を足す分と、そのあとブロックが消える分は
                        // 入れない。溜めのターンに身を固めるも使う。
                        Attack("雪山のしかかり", PhysicalDefense, 60, EnemyAim.All)
                            .Charged(1, "身を固める"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "身を固める",
                        Order = new[] { "たいあたり", "地響き", "打ち砕き", "冷たい息" },
                        // ブロックをすべて削られたあとの身を固めるは、
                        // 削られたことが見えないため入れない。
                        Rules = new[] { EnemyRule.When(Alone, "仲間を呼ぶ").Times(2) },
                    },
                    LateTurn = 6,
                    LateMove = "降り積もる雪",
                    Late = new EnemyPattern
                    {
                        Cycle = new[] { Pick, Pick, "雪山のしかかり" },
                        Choices = new[]
                        {
                            Weight("たいあたり", 1),
                            Weight("地響き", 1),
                            Weight("打ち砕き", 1),
                            Weight("冷たい息", 1),
                        },
                        MaxRun = 1,
                        Rules = new[] { EnemyRule.When(Alone, "仲間を呼ぶ").Times(2) },
                    },
                },
                new EnemyBehavior
                {
                    Id = "mist-dragon",
                    Moves = new[]
                    {
                        Attack("かみつき", PhysicalAttack, 100),
                        // 出血2ターンは入れない。
                        Attack("引き裂き", PhysicalAttack, 60),
                        Attack("なぎ払い", PhysicalAttack, 50, EnemyAim.All),
                        // 凍え2ターンは入れない。
                        Attack("冷たい息", MagicAttack, 50, EnemyAim.All),
                        Other("金切り声", "味方全体への麻痺"),
                        Other("霧隠れ", "受ける単体攻撃の軽減"),
                        // 戦闘の終わりまで行動のたびに当てる分は入れず、使ったときに1回だけ当てる。
                        Attack("迷い路の霧", MagicAttack, 10, EnemyAim.All),
                        // 次の味方ターンに引くカードが1枚減る分は入れない。
                        // 溜めのターンに霧隠れも使う。
                        Attack("白霧の大息吹", MagicAttack, 140, EnemyAim.All)
                            .Charged(1, "霧隠れ"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "金切り声",
                        Choices = new[]
                        {
                            Weight("かみつき", 30),
                            Weight("引き裂き", 25),
                            Weight("なぎ払い", 25),
                            Weight("冷たい息", 20),
                        },
                        MaxRun = 1,
                        Rules = new[]
                        {
                            EnemyRule.When(Always, "霧隠れ").Apart(4).After(4),
                            EnemyRule.When(Always, "金切り声").Apart(5),
                        },
                    },
                    LateTurn = 6,
                    LateMove = "迷い路の霧",
                    Late = new EnemyPattern
                    {
                        Cycle = new[] { Pick, Pick, "白霧の大息吹" },
                        Choices = new[]
                        {
                            Weight("かみつき", 1),
                            Weight("引き裂き", 1),
                            Weight("なぎ払い", 1),
                            Weight("冷たい息", 1),
                        },
                        MaxRun = 1,
                    },
                },
            };
    }
}
