using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Baryonyx.Combat;
using Baryonyx.Inventory;
using Baryonyx.UI.Cards;

namespace Baryonyx.Equipment
{
    /// <summary>
    /// The two equipment slots every character has, and accessories. Accessories are owned one by
    /// one like weapons and armour.
    /// </summary>
    public enum EquipmentSlot
    {
        Weapon,
        Armor,
        Accessory,
    }

    /// <summary>
    /// One effect of an equipment kind at ×1.00 (doc/features/equipment.md#装備の効果): a stat
    /// (<see cref="CharacterStats"/> order) raised by a percent, or an attack skill played
    /// twice by a chance.
    /// </summary>
    public readonly struct EquipmentEffect
    {
        // 攻撃スキルが2回発動する効果の Stat。
        public const int TwiceStat = -1;

        public EquipmentEffect(int stat, int percent)
        {
            Stat = stat;
            Percent = percent;
        }

        // 上げるステータス（CharacterStats の順）。2回発動の効果は TwiceStat。
        public int Stat { get; }

        // 上げる割合、または2回発動する確率（百分率）。
        public int Percent { get; }

        public bool Twice => Stat == TwiceStat;

        /// <summary>
        /// The percent at <paramref name="multiplier"/>: grown by it, and a chance stops at 100
        /// (doc/features/inventory.md, how the multiplier is applied).
        /// </summary>
        public int PercentAt(float multiplier) =>
            Twice ? Quality.Chance(Percent, multiplier) : Quality.Amount(Percent, multiplier);

        // 「属攻 +20%」「攻撃スキルが10%で2回発動」。
        public string TextAt(float multiplier) =>
            Twice
                ? $"攻撃スキルが{PercentAt(multiplier)}%で2回発動"
                : $"{CharacterStats.Labels[Stat]} +{PercentAt(multiplier)}%";
    }

    /// <summary>One kind of weapon, armour or accessory as the screens show it.</summary>
    public sealed class EquipmentDefinition
    {
        public EquipmentDefinition(
            string id,
            string name,
            EquipmentSlot slot,
            int rarity,
            float maxMultiplier,
            IReadOnlyList<EquipmentEffect> effects
        )
        {
            Id = id;
            Name = name;
            Slot = slot;
            Rarity = rarity;
            MaxMultiplier = maxMultiplier;
            Effects = effects ?? Array.Empty<EquipmentEffect>();
        }

        public string Id { get; }
        public string Name { get; }
        public EquipmentSlot Slot { get; }

        // レア度（★の数）。
        public int Rarity { get; }

        // 1つずつ持つ倍率の上限（doc/features/inventory.md の品質）。種類ごとに決め、サーバーと同じ値にする。
        public float MaxMultiplier { get; }

        // 倍率×1.00のときの効果（doc/features/equipment.md#装備の効果）。
        public IReadOnlyList<EquipmentEffect> Effects { get; }

        // 効果の短い説明（「属攻 +12%｜物防 +11%」）。倍率×1.00のときの値。
        public string Detail => DetailAt(Quality.MinMultiplier);

        public ItemQuality QualityOf(float multiplier) => Quality.Of(multiplier, MaxMultiplier);

        /// <summary>The description at <paramref name="multiplier"/>, every percent grown with it.</summary>
        public string DetailAt(float multiplier) =>
            string.Join("｜", Effects.Select(effect => effect.TextAt(multiplier)));
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
            // 2回発動の確率も伸びる炎のダガーと、素朴な木・革の装備は低め、尖った性能の装備は高めにした。
            // 効果の割合は仮（doc/features/equipment.md#装備の効果）。
            Weapon("flame-dagger", "炎のダガー", 4, 1.8f, Speed(13), Twice(10)),
            Weapon("thunder-spear", "雷鳴の槍", 3, 2.4f, PhysicalAttack(14)),
            Weapon("frost-wand", "氷晶のワンド", 3, 2.2f, MagicAttack(14), Critical(5)),
            Weapon("war-hammer", "戦鎚", 2, 2.6f, PhysicalAttack(15)),
            Weapon("iron-sword", "鉄の剣", 2, 2f, PhysicalAttack(12)),
            Weapon("short-bow", "短弓", 1, 2f, Speed(10)),
            Weapon("oak-staff", "樫の杖", 1, 2f, MagicAttack(11)),
            Weapon("wooden-sword", "木の剣", 1, 1.8f, PhysicalAttack(10)),
            Armor("traveler-cloak", "旅人のマント", 3, 2.2f, Speed(8), PhysicalDefense(11)),
            Armor("chainmail", "鎖かたびら", 2, 2.4f, PhysicalDefense(13)),
            Armor("magic-robe", "魔法のローブ", 2, 2.2f, MagicAttack(12), PhysicalDefense(11)),
            Armor("iron-helm", "鉄の兜", 2, 2f, PhysicalDefense(12), Hp(3)),
            Armor("leather-armor", "革の鎧", 1, 1.8f, PhysicalDefense(11)),
            Armor("wooden-shield", "木の丸盾", 1, 2f, PhysicalDefense(12)),
            // アクセサリーは、冒険のドロップで見つかる行き先ごとに作る（doc/features/drops.md）。
            // ミストラ遺跡
            Accessory("moss-charm", "苔玉のお守り", 1, 2f, Hp(2)),
            Accessory("acorn-brooch", "どんぐりのブローチ", 2, 2f, PhysicalDefense(4)),
            Accessory("dew-pendant", "朝露のペンダント", 3, 2.2f, MagicAttack(5)),
            Accessory(
                "guardian-circlet",
                "守り手の葉冠",
                5,
                2.4f,
                PhysicalAttack(6),
                MagicAttack(6)
            ),
            // 港町ポルトリア
            Accessory("shell-necklace", "貝殻の首飾り", 1, 2f, PhysicalDefense(6)),
            Accessory("anchor-bangle", "錨の腕輪", 2, 2.2f, PhysicalAttack(6)),
            Accessory("gold-earring", "金貨の耳飾り", 3, 2.2f, Speed(6)),
            Accessory("kraken-pearl", "大ダコの黒真珠", 5, 2.6f, MagicAttack(8), Critical(4)),
            // ここからは、グラシエラ氷窟より奥の行き先の武器・防具・アクセサリー（doc/features/drops.md）。
            // 奥の行き先ほど、効果の基本の値を大きくした。
            // グラシエラ氷窟（武器は上の雷鳴の槍・氷晶のワンド）
            Armor("miner-helm", "鉱夫の兜", 2, 2f, PhysicalDefense(13), Hp(5)),
            Armor("crystal-shield", "水晶の大盾", 4, 2.4f, PhysicalDefense(15)),
            Accessory("penguin-feather", "ペンギンの羽飾り", 1, 2f, PhysicalDefense(8)),
            Accessory("crystal-ring", "水晶の指輪", 3, 2.2f, MagicAttack(9)),
            Accessory(
                "troll-heartstone",
                "トロルの心臓石",
                5,
                2.4f,
                PhysicalAttack(10),
                PhysicalDefense(8)
            ),
            // しじまの沼
            Weapon("reed-bow", "葦の長弓", 2, 2f, Speed(14)),
            Weapon("wisp-staff", "鬼火の杖", 3, 2.2f, MagicAttack(16)),
            Armor("mudcloth-robe", "泥染めのローブ", 2, 2.2f, MagicAttack(13), PhysicalDefense(12)),
            Armor("clay-armor", "土くれの鎧", 3, 2.4f, PhysicalDefense(15)),
            Accessory("leech-bell", "ヒルよけの鈴", 2, 2f, Hp(5)),
            Accessory("frog-crown", "カエル王の冠", 3, 2.2f, Speed(10)),
            Accessory("croc-fang", "大ワニの牙飾り", 5, 2.6f, PhysicalAttack(12), Critical(5)),
            // アルマジャ王墓
            Weapon("scorpion-bow", "砂蠍の弓", 2, 2f, Speed(16)),
            Weapon("tomb-khopesh", "王墓の鎌剣", 3, 2.2f, PhysicalAttack(18)),
            Armor("burial-wraps", "王墓の包帯", 2, 2f, PhysicalDefense(14), Hp(7)),
            Armor("ancient-plate", "古王国の鎧", 4, 2.4f, PhysicalDefense(17)),
            Accessory("scarab-amulet", "スカラベの護符", 1, 2f, PhysicalDefense(12)),
            Accessory("gargoyle-eye", "ガーゴイルの石眼", 3, 2.2f, PhysicalAttack(12)),
            Accessory("golden-mask", "王の黄金仮面", 5, 2.6f, MagicAttack(14), PhysicalDefense(10)),
            // 月影の墓所
            Weapon("bone-axe", "骨の大斧", 2, 2.4f, PhysicalAttack(19)),
            Weapon("moonshade-staff", "月影の杖", 4, 2.2f, MagicAttack(20), Critical(8)),
            Armor("hollow-helm", "空鎧の兜", 2, 2f, PhysicalDefense(17), Hp(8)),
            Armor("grave-cloak", "墓守りの外套", 3, 2.2f, Speed(12), PhysicalDefense(16)),
            Accessory("bone-ring", "骨の指輪", 2, 2f, PhysicalAttack(13)),
            Accessory("moonstone-pendant", "月長石のペンダント", 3, 2.2f, MagicAttack(14)),
            Accessory("lich-signet", "死霊の印章", 5, 2.6f, MagicAttack(16), Critical(6)),
            // ヴォルガン火山
            Weapon("firebird-bow", "炎尾の弓", 2, 2f, Speed(18)),
            Weapon("magma-hammer", "溶岩の大槌", 3, 2.6f, PhysicalAttack(23)),
            Armor("lizardscale-mail", "火とかげの鱗鎧", 2, 2.2f, PhysicalDefense(19)),
            Armor("obsidian-shield", "黒曜石の盾", 4, 2.4f, PhysicalDefense(20), Hp(8)),
            Accessory("ember-charm", "熾火のお守り", 1, 2f, PhysicalDefense(15)),
            Accessory("magma-ring", "溶岩の指輪", 3, 2.2f, PhysicalAttack(16)),
            Accessory(
                "obsidian-crown",
                "黒曜石の冠",
                5,
                2.6f,
                PhysicalAttack(18),
                PhysicalDefense(14)
            ),
            // 白嶺ネーヴェ
            Weapon("hailstone-staff", "雹の杖", 3, 2.2f, MagicAttack(24)),
            Weapon("glacier-greatsword", "氷河の大剣", 4, 2.4f, PhysicalAttack(24)),
            Armor("snow-cloak", "雪原の外套", 2, 2.2f, Speed(14), PhysicalDefense(19)),
            Armor("mammoth-fur", "マンモスの毛皮", 3, 2.2f, PhysicalDefense(21), Hp(10)),
            Accessory("snowflake-brooch", "雪の結晶のブローチ", 2, 2f, PhysicalDefense(18)),
            Accessory("foxtail-charm", "つらら尾の房飾り", 3, 2.2f, Speed(18)),
            Accessory("mistdragon-eye", "霧竜の瞳", 5, 2.6f, MagicAttack(20), Critical(8)),
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
            float maxMultiplier,
            params EquipmentEffect[] effects
        ) => new(id, name, EquipmentSlot.Weapon, rarity, maxMultiplier, effects);

        private static EquipmentDefinition Armor(
            string id,
            string name,
            int rarity,
            float maxMultiplier,
            params EquipmentEffect[] effects
        ) => new(id, name, EquipmentSlot.Armor, rarity, maxMultiplier, effects);

        private static EquipmentDefinition Accessory(
            string id,
            string name,
            int rarity,
            float maxMultiplier,
            params EquipmentEffect[] effects
        ) => new(id, name, EquipmentSlot.Accessory, rarity, maxMultiplier, effects);

        private static EquipmentEffect Hp(int percent) => new(0, percent);

        private static EquipmentEffect PhysicalAttack(int percent) => new(1, percent);

        private static EquipmentEffect PhysicalDefense(int percent) => new(2, percent);

        private static EquipmentEffect MagicAttack(int percent) => new(3, percent);

        private static EquipmentEffect Speed(int percent) => new(5, percent);

        private static EquipmentEffect Critical(int percent) => new(6, percent);

        private static EquipmentEffect Twice(int percent) =>
            new(EquipmentEffect.TwiceStat, percent);
    }
}
