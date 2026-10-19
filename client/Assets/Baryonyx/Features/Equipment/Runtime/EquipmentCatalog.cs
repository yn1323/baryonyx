using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Baryonyx.Inventory;

namespace Baryonyx.Equipment
{
    /// <summary>The two equipment slots every character has.</summary>
    public enum EquipmentSlot
    {
        Weapon,
        Armor,
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
            float maxMultiplier = Quality.DefaultMaxMultiplier
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

        // 1つずつ持つ倍率の上限（doc/features/inventory.md の品質）。サーバーの仮設定と同じ×1.5。
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
    /// The mock weapons and armour until equipment has data (doc/features/equipment.md). The ids
    /// and slots match the server (server/src/features/equipment/catalog.ts), which keeps what each
    /// player owns and wears; the names, rarity and effects are shown only here.
    /// </summary>
    public static class EquipmentCatalog
    {
        private static readonly EquipmentDefinition[] all =
        {
            // 回数も伸びるため、上限は低め（スキルの複数回の攻撃と同じ×1.3）。
            Weapon("flame-dagger", "炎のダガー", 4, "速度の 125%｜斬・炎｜2回攻撃", 1.3f),
            Weapon("thunder-spear", "雷鳴の槍", 3, "物攻の 140%｜貫・雷"),
            Weapon("frost-wand", "氷晶のワンド", 3, "属攻の 135%｜氷｜会心 +5%"),
            Weapon("war-hammer", "戦鎚", 2, "物攻の 150%｜打"),
            Weapon("iron-sword", "鉄の剣", 2, "物攻の 120%｜斬"),
            Weapon("short-bow", "短弓", 1, "速度の 100%｜貫"),
            Weapon("oak-staff", "樫の杖", 1, "属攻の 110%｜炎"),
            Weapon("wooden-sword", "木の剣", 1, "物攻の 100%｜斬"),
            Armor("traveler-cloak", "旅人のマント", 3, "速度 +8%｜物防の 110%"),
            Armor("chainmail", "鎖かたびら", 2, "物防の 130%"),
            Armor("magic-robe", "魔法のローブ", 2, "属攻の 115%｜物防の 105%"),
            Armor("iron-helm", "鉄の兜", 2, "物防の 115%｜HP +40"),
            Armor("leather-armor", "革の鎧", 1, "物防の 110%"),
            Armor("wooden-shield", "木の丸盾", 1, "物防の 120%"),
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

        public static string NameOf(EquipmentSlot slot) =>
            slot == EquipmentSlot.Weapon ? "武器" : "防具";

        // サーバーのAPIでの枠の名前。
        public static string KeyOf(EquipmentSlot slot) =>
            slot == EquipmentSlot.Weapon ? "weapon" : "armor";

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
            float maxMultiplier = Quality.DefaultMaxMultiplier
        ) => new(id, name, EquipmentSlot.Weapon, rarity, detail, maxMultiplier);

        private static EquipmentDefinition Armor(
            string id,
            string name,
            int rarity,
            string detail
        ) => new(id, name, EquipmentSlot.Armor, rarity, detail);
    }
}
