using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// One kind of enemy: the id it is recorded under (its revealed weaknesses on the server,
    /// server/src/features/combat/catalog.ts), its name and its weaknesses in icon order.
    /// </summary>
    public sealed class EnemyDefinition
    {
        public EnemyDefinition(string id, string name, params CardElement[] weaknesses)
        {
            Id = id;
            Name = name;
            Weaknesses = weaknesses ?? Array.Empty<CardElement>();
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<CardElement> Weaknesses { get; }
    }

    /// <summary>
    /// The kinds of enemy (doc/catalog/enemies, the ids without their "enemy-" prefix). Add a
    /// new kind here and to the server's ENEMY_IDS, so its weaknesses can be recorded.
    /// </summary>
    public static class CombatEnemies
    {
        public static readonly EnemyDefinition MossSlime = new(
            "moss-slime",
            "苔スライム",
            CardElement.Thunder,
            CardElement.Fire
        );

        public static readonly EnemyDefinition MossWolf = new(
            "moss-wolf",
            "苔むした狼",
            CardElement.Ice,
            CardElement.Fire
        );

        public static readonly EnemyDefinition ForestGuardian = new(
            "forest-guardian",
            "森の守り手",
            CardElement.Fire,
            CardElement.Slash
        );

        // The trial kinds of 2026-10-19 (doc/catalog/enemies), in roster order.
        public static readonly EnemyDefinition DewSlime = new(
            "dew-slime",
            "朝露ぷるん",
            CardElement.Thunder
        );

        public static readonly EnemyDefinition AcornSoldier = new(
            "acorn-soldier",
            "どんぐり兵",
            CardElement.Fire,
            CardElement.Blunt
        );

        public static readonly EnemyDefinition SporeCap = new(
            "spore-cap",
            "毒胞子オオダケ",
            CardElement.Fire
        );

        public static readonly EnemyDefinition ThornSerpent = new(
            "thorn-serpent",
            "いばら蛇",
            CardElement.Fire,
            CardElement.Slash
        );

        public static readonly EnemyDefinition CrystalHare = new(
            "crystal-hare",
            "水晶角うさぎ",
            CardElement.Blunt
        );

        public static readonly EnemyDefinition StumpBoar = new(
            "stump-boar",
            "切り株猪",
            CardElement.Slash
        );

        public static readonly EnemyDefinition NectarBloom = new(
            "nectar-bloom",
            "蜜吸い花",
            CardElement.Ice
        );

        public static readonly EnemyDefinition HollowArmor = new(
            "hollow-armor",
            "古王国の空鎧",
            CardElement.Blunt,
            CardElement.Thunder
        );

        public static readonly EnemyDefinition StoneGargoyle = new(
            "stone-gargoyle",
            "石のガーゴイル",
            CardElement.Blunt
        );

        public static readonly EnemyDefinition CrackedUrn = new(
            "cracked-urn",
            "割れ壺の番",
            CardElement.Blunt
        );

        public static readonly EnemyDefinition ChestMimic = new(
            "chest-mimic",
            "人食い宝箱",
            CardElement.Pierce
        );

        public static readonly EnemyDefinition FloatingGrimoire = new(
            "floating-grimoire",
            "浮遊する魔導書",
            CardElement.Fire
        );

        public static readonly EnemyDefinition BrassWarden = new(
            "brass-warden",
            "真鍮の番人",
            CardElement.Thunder,
            CardElement.Blunt
        );

        public static readonly EnemyDefinition CursedCandelabra = new(
            "cursed-candelabra",
            "呪いの燭台",
            CardElement.Ice
        );

        public static readonly EnemyDefinition BigEaredBat = new(
            "big-eared-bat",
            "大耳コウモリ",
            CardElement.Pierce
        );

        public static readonly EnemyDefinition CrystalHermit = new(
            "crystal-hermit",
            "水晶ヤドカリ",
            CardElement.Blunt
        );

        public static readonly EnemyDefinition OreMole = new(
            "ore-mole",
            "鉱石喰いモグラ",
            CardElement.Thunder
        );

        public static readonly EnemyDefinition GlowWorm = new(
            "glow-worm",
            "ツチボタル",
            CardElement.Fire
        );

        public static readonly EnemyDefinition RockTroll = new(
            "rock-troll",
            "岩肌トロル",
            CardElement.Pierce
        );

        public static readonly EnemyDefinition BlindFishman = new(
            "blind-fishman",
            "地底湖の盲魚人",
            CardElement.Thunder
        );

        public static readonly EnemyDefinition FrogKing = new(
            "frog-king",
            "沼のカエル王",
            CardElement.Ice,
            CardElement.Slash
        );

        public static readonly EnemyDefinition MudGolem = new(
            "mud-golem",
            "泥人形",
            CardElement.Fire
        );

        public static readonly EnemyDefinition BloodLeech = new(
            "blood-leech",
            "吸血大ヒル",
            CardElement.Fire
        );

        public static readonly EnemyDefinition WillOWisp = new(
            "will-o-wisp",
            "鬼火",
            CardElement.Ice
        );

        public static readonly EnemyDefinition BoneCroc = new(
            "bone-croc",
            "骨背の大ワニ",
            CardElement.Pierce
        );

        public static readonly EnemyDefinition SandScorpion = new(
            "sand-scorpion",
            "砂サソリ",
            CardElement.Blunt,
            CardElement.Ice
        );

        public static readonly EnemyDefinition CactusWarrior = new(
            "cactus-warrior",
            "サボテン戦士",
            CardElement.Slash
        );

        public static readonly EnemyDefinition WrappedJackal = new(
            "wrapped-jackal",
            "包帯の山犬",
            CardElement.Fire
        );

        public static readonly EnemyDefinition SandShark = new(
            "sand-shark",
            "砂鮫",
            CardElement.Thunder
        );

        public static readonly EnemyDefinition HourglassSprite = new(
            "hourglass-sprite",
            "砂時計の精",
            CardElement.Blunt
        );

        public static readonly EnemyDefinition Snowball = new(
            "snowball",
            "雪玉ころん",
            CardElement.Fire
        );

        public static readonly EnemyDefinition IcicleFox = new(
            "icicle-fox",
            "つらら尾の狐",
            CardElement.Fire
        );

        public static readonly EnemyDefinition FrostApe = new(
            "frost-ape",
            "霜の大猿人",
            CardElement.Fire,
            CardElement.Pierce
        );

        public static readonly EnemyDefinition IcePenguin = new(
            "ice-penguin",
            "氷槍のペンギン兵",
            CardElement.Thunder
        );

        public static readonly EnemyDefinition SnowMammoth = new(
            "snow-mammoth",
            "雪原のマンモス",
            CardElement.Fire
        );

        public static readonly EnemyDefinition FireLizard = new(
            "fire-lizard",
            "火とかげ",
            CardElement.Ice
        );

        public static readonly EnemyDefinition LavaSlime = new(
            "lava-slime",
            "溶岩スライム",
            CardElement.Ice
        );

        public static readonly EnemyDefinition FlameTailBird = new(
            "flame-tail-bird",
            "炎尾の鳥",
            CardElement.Ice,
            CardElement.Pierce
        );

        public static readonly EnemyDefinition ObsidianColossus = new(
            "obsidian-colossus",
            "黒曜石の巨像",
            CardElement.Ice,
            CardElement.Blunt
        );

        public static readonly EnemyDefinition BarnacleCrab = new(
            "barnacle-crab",
            "フジツボ大ガニ",
            CardElement.Blunt
        );

        public static readonly EnemyDefinition GhostJelly = new(
            "ghost-jelly",
            "クラゲの亡霊",
            CardElement.Thunder
        );

        public static readonly EnemyDefinition SkeletonPirate = new(
            "skeleton-pirate",
            "骸骨海賊",
            CardElement.Blunt
        );

        public static readonly EnemyDefinition GiantOctopus = new(
            "giant-octopus",
            "沖の大ダコ",
            CardElement.Slash,
            CardElement.Thunder
        );

        public static readonly EnemyDefinition SkeletonSoldier = new(
            "skeleton-soldier",
            "骸骨兵",
            CardElement.Blunt
        );

        public static readonly EnemyDefinition GraveRaven = new(
            "grave-raven",
            "墓守りの大鴉",
            CardElement.Thunder
        );

        public static readonly EnemyDefinition Lich = new(
            "lich",
            "死霊の魔導師",
            CardElement.Fire,
            CardElement.Blunt
        );

        public static readonly EnemyDefinition PumpkinScarecrow = new(
            "pumpkin-scarecrow",
            "かぼちゃ頭の案山子",
            CardElement.Fire
        );

        public static readonly EnemyDefinition LostSignpost = new(
            "lost-signpost",
            "迷い道標",
            CardElement.Slash
        );

        public static readonly EnemyDefinition CoinPouch = new(
            "coin-pouch",
            "金貨袋",
            CardElement.Slash
        );

        public static readonly EnemyDefinition MistDragon = new(
            "mist-dragon",
            "霧の竜",
            CardElement.Thunder,
            CardElement.Pierce
        );

        public static readonly IReadOnlyList<EnemyDefinition> All = new[]
        {
            MossSlime,
            MossWolf,
            ForestGuardian,
            DewSlime,
            AcornSoldier,
            SporeCap,
            ThornSerpent,
            CrystalHare,
            StumpBoar,
            NectarBloom,
            HollowArmor,
            StoneGargoyle,
            CrackedUrn,
            ChestMimic,
            FloatingGrimoire,
            BrassWarden,
            CursedCandelabra,
            BigEaredBat,
            CrystalHermit,
            OreMole,
            GlowWorm,
            RockTroll,
            BlindFishman,
            FrogKing,
            MudGolem,
            BloodLeech,
            WillOWisp,
            BoneCroc,
            SandScorpion,
            CactusWarrior,
            WrappedJackal,
            SandShark,
            HourglassSprite,
            Snowball,
            IcicleFox,
            FrostApe,
            IcePenguin,
            SnowMammoth,
            FireLizard,
            LavaSlime,
            FlameTailBird,
            ObsidianColossus,
            BarnacleCrab,
            GhostJelly,
            SkeletonPirate,
            GiantOctopus,
            SkeletonSoldier,
            GraveRaven,
            Lich,
            PumpkinScarecrow,
            LostSignpost,
            CoinPouch,
            MistDragon,
        };

        public static EnemyDefinition Find(string id)
        {
            foreach (var enemy in All)
                if (enemy.Id == id)
                    return enemy;
            return null;
        }
    }

    /// <summary>The names the server keeps the elements under ("slash", "fire" and so on).</summary>
    public static class CardElementKeys
    {
        public static string Key(CardElement element) =>
            element switch
            {
                CardElement.Slash => "slash",
                CardElement.Fire => "fire",
                CardElement.Ice => "ice",
                CardElement.Thunder => "thunder",
                CardElement.Blunt => "blunt",
                CardElement.Pierce => "pierce",
                _ => null,
            };

        public static bool TryParse(string key, out CardElement element)
        {
            element = key switch
            {
                "slash" => CardElement.Slash,
                "fire" => CardElement.Fire,
                "ice" => CardElement.Ice,
                "thunder" => CardElement.Thunder,
                "blunt" => CardElement.Blunt,
                "pierce" => CardElement.Pierce,
                _ => CardElement.None,
            };
            return element != CardElement.None;
        }
    }
}
