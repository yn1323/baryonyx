using System;
using System.Collections.Generic;

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
            string detail
        )
        {
            Id = id;
            Name = name;
            Slot = slot;
            Rarity = rarity;
            Detail = detail;
        }

        public string Id { get; }
        public string Name { get; }
        public EquipmentSlot Slot { get; }

        // レア度（★の数）。
        public int Rarity { get; }

        // 効果の短い説明（「物攻の 120%｜斬」）。効果の計算はまだない。
        public string Detail { get; }
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
            Weapon("flame-dagger", "炎のダガー", 4, "速度の 125%｜斬・炎｜2回攻撃"),
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

        private static EquipmentDefinition Weapon(
            string id,
            string name,
            int rarity,
            string detail
        ) => new(id, name, EquipmentSlot.Weapon, rarity, detail);

        private static EquipmentDefinition Armor(
            string id,
            string name,
            int rarity,
            string detail
        ) => new(id, name, EquipmentSlot.Armor, rarity, detail);
    }
}
