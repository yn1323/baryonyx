using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>How big an enemy is drawn: its picture is 64, 96 or 128 dots square.</summary>
    public enum EnemyBody
    {
        Small,
        Medium,

        // 128ドットの大きな敵。最奥の主として出し、倒れる演出を重くする。
        Large,
    }

    /// <summary>
    /// The battle numbers of one kind of enemy at level 100 (doc/catalog/enemies, each enemy's
    /// stats and skills), from which a battle takes the numbers at the level the enemy shows up
    /// at (<see cref="CombatFormula.GrowthRate"/>). An enemy whose magic attack (属攻) is higher
    /// than its physical attack (物攻) attacks with spells, softened by the ally's magic defense.
    /// The name of its attack is the first of its skills that hurts the party, its own skill
    /// first. 苔スライム, 苔むした狼 and 森の守り手 have their numbers given at level 10, and keep
    /// the mock's names for their attacks.
    /// </summary>
    public sealed class EnemyStats
    {
        public EnemyStats(
            string id,
            EnemyBody body,
            int hp,
            int physicalAttack,
            int physicalDefense,
            int magicAttack,
            int magicDefense,
            string skill
        )
        {
            Id = id;
            Body = body;
            Hp = hp;
            PhysicalAttack = physicalAttack;
            PhysicalDefense = physicalDefense;
            MagicAttack = magicAttack;
            MagicDefense = magicDefense;
            Skill = skill;
        }

        public string Id { get; }
        public EnemyBody Body { get; }
        public int Hp { get; }
        public int PhysicalAttack { get; }
        public int PhysicalDefense { get; }
        public int MagicAttack { get; }
        public int MagicDefense { get; }

        /// <summary>The name of its attack, shown while it attacks.</summary>
        public string Skill { get; }

        /// <summary>It attacks with spells (its magic attack is the higher).</summary>
        public bool Magic => MagicAttack > PhysicalAttack;

        /// <summary>A stat of level 100 at <paramref name="level"/>, at least 1.</summary>
        public static int At(int level100, int level) =>
            Math.Max(1, (int)Math.Round(level100 * CombatFormula.GrowthRate(level)));
    }

    /// <summary>
    /// Every enemy's battle numbers, in the order of <see cref="CombatEnemies.All"/>. Made from
    /// the enemy documents; keep them in step when a document's stats change.
    /// </summary>
    public static class CombatEnemyStats
    {
        public static readonly IReadOnlyList<EnemyStats> All = new EnemyStats[]
        {
            new("moss-slime", EnemyBody.Small, 3000, 760, 330, 650, 160, "たいあたり"),
            new("moss-wolf", EnemyBody.Medium, 6500, 1250, 490, 240, 320, "かみつき"),
            new("forest-guardian", EnemyBody.Large, 17300, 1940, 1300, 970, 650, "大地の拳"),
            new("dew-slime", EnemyBody.Small, 2900, 480, 240, 900, 420, "たいあたり"),
            new("acorn-soldier", EnemyBody.Small, 3500, 1040, 350, 420, 300, "どんぐりつぶて"),
            new("spore-cap", EnemyBody.Medium, 7300, 610, 520, 1200, 760, "たいあたり"),
            new("thorn-serpent", EnemyBody.Medium, 5200, 1260, 400, 800, 450, "とげのとぐろ"),
            new("crystal-hare", EnemyBody.Small, 2600, 840, 280, 600, 300, "水晶角の照り返し"),
            new("stump-boar", EnemyBody.Medium, 8900, 1330, 750, 560, 540, "牙のかち上げ"),
            new("nectar-bloom", EnemyBody.Medium, 5800, 720, 350, 1200, 630, "吸い取り"),
            new("hollow-armor", EnemyBody.Medium, 8100, 1020, 750, 800, 540, "空ろの鎧ばらし"),
            new("stone-gargoyle", EnemyBody.Medium, 8100, 1020, 750, 800, 540, "屋根からの急降下"),
            new("cracked-urn", EnemyBody.Small, 4000, 680, 520, 600, 360, "壺の吸い込み"),
            new("chest-mimic", EnemyBody.Medium, 7200, 1560, 500, 560, 450, "欲張りな舌"),
            new("floating-grimoire", EnemyBody.Small, 2900, 480, 240, 900, 420, "頁の詠唱"),
            new("brass-warden", EnemyBody.Large, 21200, 1620, 1650, 1300, 960, "蒼炉の砲撃"),
            new("cursed-candelabra", EnemyBody.Small, 2900, 480, 240, 900, 420, "火の玉"),
            new("big-eared-bat", EnemyBody.Small, 2600, 840, 280, 600, 300, "吸い取り"),
            new("crystal-hermit", EnemyBody.Medium, 8100, 1020, 750, 800, 540, "斬りつけ"),
            new("ore-mole", EnemyBody.Medium, 7200, 1560, 500, 560, 450, "地中からの突き上げ"),
            new("glow-worm", EnemyBody.Small, 2900, 480, 240, 900, 420, "電撃"),
            new("rock-troll", EnemyBody.Large, 18700, 2470, 1100, 910, 800, "肩石投げ"),
            new("blind-fishman", EnemyBody.Medium, 6500, 1200, 500, 800, 450, "波紋聞き"),
            new("frog-king", EnemyBody.Medium, 5800, 720, 350, 1200, 630, "舌のかすめ取り"),
            new("mud-golem", EnemyBody.Medium, 8100, 1020, 750, 800, 540, "たいあたり"),
            new("blood-leech", EnemyBody.Small, 3200, 800, 350, 600, 300, "ふさがらない噛み傷"),
            new("will-o-wisp", EnemyBody.Small, 2300, 500, 200, 900, 420, "火の玉"),
            new("bone-croc", EnemyBody.Large, 18700, 2470, 1100, 910, 800, "血の匂い"),
            new("sand-scorpion", EnemyBody.Medium, 7200, 1560, 500, 560, 450, "毒まわし"),
            new("cactus-warrior", EnemyBody.Small, 3200, 800, 350, 600, 300, "針千本"),
            new("wrapped-jackal", EnemyBody.Medium, 5200, 1260, 400, 800, 450, "かみつき"),
            new("sand-shark", EnemyBody.Large, 15000, 2590, 880, 910, 800, "砂の大波"),
            new("hourglass-sprite", EnemyBody.Small, 2900, 480, 240, 900, 420, "電撃"),
            new("snowball", EnemyBody.Small, 4000, 680, 520, 600, 360, "たいあたり"),
            new("icicle-fox", EnemyBody.Medium, 4700, 760, 280, 1200, 630, "つらら刺し"),
            new("frost-ape", EnemyBody.Large, 18700, 2470, 1100, 910, 800, "雪崩おとし"),
            new("ice-penguin", EnemyBody.Small, 3200, 800, 350, 600, 300, "二段突き"),
            new("snow-mammoth", EnemyBody.Large, 21200, 1620, 1650, 1300, 960, "雪山のしかかり"),
            new("fire-lizard", EnemyBody.Medium, 5800, 720, 350, 1200, 630, "火の玉"),
            new("lava-slime", EnemyBody.Small, 4000, 680, 520, 600, 360, "殻はじけ"),
            new("flame-tail-bird", EnemyBody.Medium, 4700, 760, 280, 1200, 630, "火の尾羽落とし"),
            new("obsidian-colossus", EnemyBody.Large, 21200, 1620, 1650, 1300, 960, "黒曜の破片嵐"),
            new("barnacle-crab", EnemyBody.Medium, 8100, 1020, 750, 800, 540, "大ばさみ砕き"),
            new("ghost-jelly", EnemyBody.Small, 2900, 480, 240, 900, 420, "火の玉"),
            new("skeleton-pirate", EnemyBody.Medium, 5200, 1260, 400, 800, 450, "卑怯な足払い"),
            new("giant-octopus", EnemyBody.Large, 17000, 1900, 1100, 1300, 800, "絡め取り"),
            new("skeleton-soldier", EnemyBody.Medium, 6500, 1200, 500, 800, 450, "斬りつけ"),
            new("grave-raven", EnemyBody.Medium, 4700, 760, 280, 1200, 630, "鴉のものまね"),
            new("lich", EnemyBody.Large, 15300, 1140, 770, 1950, 1120, "王冠の覚醒"),
            new("pumpkin-scarecrow", EnemyBody.Medium, 6500, 1200, 500, 800, 450, "畑の見張り番"),
            new("lost-signpost", EnemyBody.Small, 3200, 800, 350, 600, 300, "たいあたり"),
            new("coin-pouch", EnemyBody.Small, 2600, 840, 280, 600, 300, "たいあたり"),
            new("mist-dragon", EnemyBody.Large, 16800, 1480, 770, 1360, 1120, "迷い路の霧"),
        };

        public static EnemyStats Find(string id)
        {
            foreach (var stats in All)
                if (stats.Id == id)
                    return stats;
            return null;
        }
    }
}
