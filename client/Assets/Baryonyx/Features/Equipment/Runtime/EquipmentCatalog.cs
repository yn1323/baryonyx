using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Baryonyx.Inventory;
using Baryonyx.UI.Cards;

namespace Baryonyx.Equipment
{
    /// <summary>
    /// The two equipment slots every character has, and accessories. Accessories are owned one by
    /// one like weapons and armour, but cannot be worn yet.
    /// </summary>
    public enum EquipmentSlot
    {
        Weapon,
        Armor,
        Accessory,
    }

    /// <summary>One kind of weapon or armour as the screens show it.</summary>
    public sealed class EquipmentDefinition
    {
        public EquipmentDefinition(
            string id,
            string name,
            EquipmentSlot slot,
            int rarity,
            string detail,
            float maxMultiplier
        )
        {
            Id = id;
            Name = name;
            Slot = slot;
            Rarity = rarity;
            Detail = detail;
            MaxMultiplier = maxMultiplier;
        }

        public string Id { get; }
        public string Name { get; }
        public EquipmentSlot Slot { get; }

        // レア度（★の数）。
        public int Rarity { get; }

        // 効果の短い説明（「物攻の 120%｜斬」）。倍率×1.00のときの値。効果の計算はまだない。
        public string Detail { get; }

        // 1つずつ持つ倍率の上限（doc/features/inventory.md の品質）。種類ごとに決め、サーバーと同じ値にする。
        public float MaxMultiplier { get; }

        public ItemQuality QualityOf(float multiplier) => Quality.Of(multiplier, MaxMultiplier);

        /// <summary>
        /// The description at <paramref name="multiplier"/>: every percent and "+" amount grows with
        /// it and every count of hits ("2回") is rounded (doc/features/inventory.md, how the
        /// multiplier is applied).
        /// </summary>
        public string DetailAt(float multiplier) => EquipmentCatalog.Scale(Detail, multiplier);
    }

    /// <summary>
    /// The mock weapons, armour and accessories until equipment has data (doc/features/equipment.md).
    /// The ids and slots match the server (server/src/features/equipment/catalog.ts), which keeps
    /// what each player owns and wears; the names, rarity and effects are shown only here.
    /// </summary>
    public static class EquipmentCatalog
    {
        private static readonly EquipmentDefinition[] all =
        {
            // 上限の倍率はサーバー（server/src/features/equipment/catalog.ts）と同じ。
            // 回数も伸びる炎のダガーと、素朴な木・革の装備は低め、尖った性能の装備は高めにした。
            Weapon("flame-dagger", "炎のダガー", 4, "速度の 125%｜斬・炎｜2回攻撃", 1.8f),
            Weapon("thunder-spear", "雷鳴の槍", 3, "物攻の 140%｜貫・雷", 2.4f),
            Weapon("frost-wand", "氷晶のワンド", 3, "属攻の 135%｜氷｜会心 +5%", 2.2f),
            Weapon("war-hammer", "戦鎚", 2, "物攻の 150%｜打", 2.6f),
            Weapon("iron-sword", "鉄の剣", 2, "物攻の 120%｜斬", 2f),
            Weapon("short-bow", "短弓", 1, "速度の 100%｜貫", 2f),
            Weapon("oak-staff", "樫の杖", 1, "属攻の 110%｜炎", 2f),
            Weapon("wooden-sword", "木の剣", 1, "物攻の 100%｜斬", 1.8f),
            Armor("traveler-cloak", "旅人のマント", 3, "速度 +8%｜物防の 110%", 2.2f),
            Armor("chainmail", "鎖かたびら", 2, "物防の 130%", 2.4f),
            Armor("magic-robe", "魔法のローブ", 2, "属攻の 115%｜物防の 105%", 2.2f),
            Armor("iron-helm", "鉄の兜", 2, "物防の 115%｜HP +40", 2f),
            Armor("leather-armor", "革の鎧", 1, "物防の 110%", 1.8f),
            Armor("wooden-shield", "木の丸盾", 1, "物防の 120%", 2f),
            // アクセサリーは、冒険のドロップで見つかる行き先ごとに作る（doc/features/drops.md）。
            // ミストラ遺跡
            Accessory("moss-charm", "苔玉のお守り", 1, "HP +20", 2f),
            Accessory("acorn-brooch", "どんぐりのブローチ", 2, "物防 +4%", 2f),
            Accessory("dew-pendant", "朝露のペンダント", 3, "属攻 +5%", 2.2f),
            Accessory("guardian-circlet", "守り手の葉冠", 5, "物攻 +6%｜属攻 +6%", 2.4f),
            // 港町ポルトリア
            Accessory("shell-necklace", "貝殻の首飾り", 1, "物防 +6%", 2f),
            Accessory("anchor-bangle", "錨の腕輪", 2, "物攻 +6%", 2.2f),
            Accessory("gold-earring", "金貨の耳飾り", 3, "速度 +6%", 2.2f),
            Accessory("kraken-pearl", "大ダコの黒真珠", 5, "属攻 +8%｜会心 +4%", 2.6f),
            // ここからは、グラシエラ氷窟より奥の行き先の武器・防具・アクセサリー（doc/features/drops.md）。
            // 奥の行き先ほど、効果の基本の値を大きくした。
            // グラシエラ氷窟（武器は上の雷鳴の槍・氷晶のワンド）
            Armor("miner-helm", "鉱夫の兜", 2, "物防の 125%｜HP +60", 2f),
            Armor("crystal-shield", "水晶の大盾", 4, "物防の 150%", 2.4f),
            Accessory("penguin-feather", "ペンギンの羽飾り", 1, "物防 +8%", 2f),
            Accessory("crystal-ring", "水晶の指輪", 3, "属攻 +9%", 2.2f),
            Accessory("troll-heartstone", "トロルの心臓石", 5, "物攻 +10%｜物防 +8%", 2.4f),
            // しじまの沼
            Weapon("reed-bow", "葦の長弓", 2, "速度の 140%｜貫", 2f),
            Weapon("wisp-staff", "鬼火の杖", 3, "属攻の 155%｜炎", 2.2f),
            Armor("mudcloth-robe", "泥染めのローブ", 2, "属攻の 125%｜物防の 115%", 2.2f),
            Armor("clay-armor", "土くれの鎧", 3, "物防の 150%", 2.4f),
            Accessory("leech-bell", "ヒルよけの鈴", 2, "HP +60", 2f),
            Accessory("frog-crown", "カエル王の冠", 3, "速度 +10%", 2.2f),
            Accessory("croc-fang", "大ワニの牙飾り", 5, "物攻 +12%｜会心 +5%", 2.6f),
            // アルマジャ王墓
            Weapon("scorpion-bow", "砂蠍の弓", 2, "速度の 155%｜貫", 2f),
            Weapon("tomb-khopesh", "王墓の鎌剣", 3, "物攻の 175%｜斬", 2.2f),
            Armor("burial-wraps", "王墓の包帯", 2, "物防の 140%｜HP +80", 2f),
            Armor("ancient-plate", "古王国の鎧", 4, "物防の 170%", 2.4f),
            Accessory("scarab-amulet", "スカラベの護符", 1, "物防 +12%", 2f),
            Accessory("gargoyle-eye", "ガーゴイルの石眼", 3, "物攻 +12%", 2.2f),
            Accessory("golden-mask", "王の黄金仮面", 5, "属攻 +14%｜物防 +10%", 2.6f),
            // 月影の墓所
            Weapon("bone-axe", "骨の大斧", 2, "物攻の 190%｜斬", 2.4f),
            Weapon("moonshade-staff", "月影の杖", 4, "属攻の 200%｜氷｜会心 +8%", 2.2f),
            Armor("hollow-helm", "空鎧の兜", 2, "物防の 165%｜HP +100", 2f),
            Armor("grave-cloak", "墓守りの外套", 3, "速度 +12%｜物防の 160%", 2.2f),
            Accessory("bone-ring", "骨の指輪", 2, "物攻 +13%", 2f),
            Accessory("moonstone-pendant", "月長石のペンダント", 3, "属攻 +14%", 2.2f),
            Accessory("lich-signet", "死霊の印章", 5, "属攻 +16%｜会心 +6%", 2.6f),
            // ヴォルガン火山
            Weapon("firebird-bow", "炎尾の弓", 2, "速度の 180%｜貫・炎", 2f),
            Weapon("magma-hammer", "溶岩の大槌", 3, "物攻の 225%｜打・炎", 2.6f),
            Armor("lizardscale-mail", "火とかげの鱗鎧", 2, "物防の 185%", 2.2f),
            Armor("obsidian-shield", "黒曜石の盾", 4, "物防の 200%｜HP +100", 2.4f),
            Accessory("ember-charm", "熾火のお守り", 1, "物防 +15%", 2f),
            Accessory("magma-ring", "溶岩の指輪", 3, "物攻 +16%", 2.2f),
            Accessory("obsidian-crown", "黒曜石の冠", 5, "物攻 +18%｜物防 +14%", 2.6f),
            // 白嶺ネーヴェ
            Weapon("hailstone-staff", "雹の杖", 3, "属攻の 235%｜氷", 2.2f),
            Weapon("glacier-greatsword", "氷河の大剣", 4, "物攻の 240%｜斬・氷", 2.4f),
            Armor("snow-cloak", "雪原の外套", 2, "速度 +14%｜物防の 190%", 2.2f),
            Armor("mammoth-fur", "マンモスの毛皮", 3, "物防の 210%｜HP +120", 2.2f),
            Accessory("snowflake-brooch", "雪の結晶のブローチ", 2, "物防 +18%", 2f),
            Accessory("foxtail-charm", "つらら尾の房飾り", 3, "速度 +18%", 2.2f),
            Accessory("mistdragon-eye", "霧竜の瞳", 5, "属攻 +20%｜会心 +8%", 2.6f),
        };

        public static IReadOnlyList<EquipmentDefinition> All => all;

        // 最初から持っている装備（1つずつ）と、初めの4人が付けている装備。サーバーの初期データと同じ。
        public static readonly IReadOnlyList<(
            string Character,
            string Weapon,
            string Armor
        )> StarterEquipped = new[]
        {
            ("toma", "oak-staff", "magic-robe"),
            ("luka", "short-bow", "traveler-cloak"),
            ("aria", "iron-sword", "chainmail"),
            ("mina", "war-hammer", "leather-armor"),
        };

        public static EquipmentDefinition Find(string id) =>
            string.IsNullOrEmpty(id) ? null : Array.Find(all, entry => entry.Id == id);

        // 仮データの順。並べるときの最後の手がかりにする。
        public static int IndexOf(string id) => Array.FindIndex(all, entry => entry.Id == id);

        // 光り方の割り当て（doc/art/direction.md のカードのキラキラ）。仮データの順から、スキルと重ならないよう歩幅をずらして決める。
        public const int FoilStyleStep = 5;
        public const int FoilStyleOffset = 3;

        /// <summary>How the kind <paramref name="id"/> sparkles (one of <see cref="CardFoil.StyleCount"/> styles).</summary>
        public static int FoilStyleOf(string id) =>
            CardFoil.StyleAt(IndexOf(id), FoilStyleStep, FoilStyleOffset);

        public static string NameOf(EquipmentSlot slot) =>
            slot switch
            {
                EquipmentSlot.Weapon => "武器",
                EquipmentSlot.Armor => "防具",
                _ => "アクセサリー",
            };

        // サーバーのAPIでの枠（分類）の名前。
        public static string KeyOf(EquipmentSlot slot) =>
            slot switch
            {
                EquipmentSlot.Weapon => "weapon",
                EquipmentSlot.Armor => "armor",
                _ => "accessory",
            };

        public static string Stars(int rarity) => new('★', Math.Max(0, rarity));

        // 説明の数字のうち、「%」「回」が後ろに付く数と「+」の後ろの数に倍率を掛ける。
        private static readonly Regex Numbers = new(
            "(?<![0-9.])[0-9]+(?=%|回)|(?<=\\+)[0-9]+",
            RegexOptions.CultureInvariant
        );

        /// <summary>The numbers of a free-text effect grown by <paramref name="multiplier"/>.</summary>
        public static string Scale(string text, float multiplier) =>
            string.IsNullOrEmpty(text) || Quality.Percent(multiplier) == 100
                ? text ?? ""
                : Numbers.Replace(
                    text,
                    match => Quality.Amount(int.Parse(match.Value), multiplier).ToString()
                );

        private static EquipmentDefinition Weapon(
            string id,
            string name,
            int rarity,
            string detail,
            float maxMultiplier
        ) => new(id, name, EquipmentSlot.Weapon, rarity, detail, maxMultiplier);

        private static EquipmentDefinition Armor(
            string id,
            string name,
            int rarity,
            string detail,
            float maxMultiplier
        ) => new(id, name, EquipmentSlot.Armor, rarity, detail, maxMultiplier);

        private static EquipmentDefinition Accessory(
            string id,
            string name,
            int rarity,
            string detail,
            float maxMultiplier
        ) => new(id, name, EquipmentSlot.Accessory, rarity, detail, maxMultiplier);
    }
}
