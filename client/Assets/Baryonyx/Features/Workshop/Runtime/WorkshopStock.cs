using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.Items;
using Baryonyx.Library;
using Baryonyx.Party;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// One thing the player holds, as the trading house shows it: a skill card or a piece of
    /// equipment (one row each, with its own quality), or an item kind (counted, no quality).
    /// </summary>
    public sealed class WorkshopOwned
    {
        public LibraryKind Kind { get; set; }

        // スキル・装備は持ち物の1つずつのID、アイテムは種類のID。
        public string RowId { get; set; } = "";
        public string KindId { get; set; } = "";
        public string Name { get; set; } = "";
        public int Stars { get; set; }
        public float Multiplier { get; set; } = Inventory.Quality.MinMultiplier;
        public float MaxMultiplier { get; set; } = Inventory.Quality.MinMultiplier;

        // アイテムは持っている数。スキル・装備は1。
        public int Count { get; set; } = 1;

        // 付けている人の名前（アイテムはデッキに入れている人）。誰も使っていなければnull。
        public string User { get; set; }

        // アイテムをデッキの枠に入れている数。合成ではこの数を残す。
        public int Reserved { get; set; }

        public bool HasQuality => Kind != LibraryKind.Item;
        public ItemQuality Quality => Inventory.Quality.Of(Multiplier, MaxMultiplier);

        // 倍率の幅の中の位置（0〜1）。合成の品質は材料のこの値の平均で決まる。
        public float Place => Inventory.Quality.Place(Multiplier, MaxMultiplier);

        public WorkshopOwned Copy() => (WorkshopOwned)MemberwiseClone();
    }

    /// <summary>
    /// What the player holds, for the trading house's sell and craft (doc/features/screens.md#商会):
    /// the skill cards and items from the party, and the equipment, with who uses each one.
    /// Without a server, a fixed mock list stands in, so the screens can be checked.
    /// </summary>
    public static class WorkshopStock
    {
        // 並べる分類と、その順（売る・合成のタブの順）。
        public static readonly IReadOnlyList<LibraryKind> Kinds = new[]
        {
            LibraryKind.Weapon,
            LibraryKind.Armor,
            LibraryKind.Accessory,
            LibraryKind.Skill,
            LibraryKind.Item,
        };

        public static readonly string[] TabLabels =
        {
            "武器",
            "防具",
            "アクセサリー",
            "スキル",
            "アイテム",
        };

        /// <summary>
        /// Reads the party and the equipment, and lists what they hold. Without a party server,
        /// the mock list (<see cref="Mock"/>).
        /// </summary>
        public static async Task<IReadOnlyList<WorkshopOwned>> LoadAsync(
            PartyMockData data,
            CancellationToken token
        )
        {
            if (PartySession.Source == null)
                return Mock();
            var formation = await PartySession.LoadAsync(data, PartySession.Source, token);
            var gear = await EquipmentSession.SourceOrLocal.LoadAsync(token);
            return Of(PartySession.State, formation?.Roster, gear);
        }

        /// <summary>Everything held, kind by kind (<see cref="Kinds"/>), the stronger kinds last.</summary>
        public static IReadOnlyList<WorkshopOwned> Of(
            PartyState party,
            IReadOnlyList<PartyMember> roster,
            EquipmentState gear
        )
        {
            var names = (roster ?? Array.Empty<PartyMember>()).ToDictionary(
                member => member.Id,
                member => member.Name
            );
            string NameOf(string id) =>
                id != null && names.TryGetValue(id, out string name) ? name : id;
            var cards = new Dictionary<string, string>();
            var items = new Dictionary<string, string>();
            var slots = new Dictionary<string, int>();
            foreach (var character in party?.Owned ?? Array.Empty<PartyCharacterState>())
            foreach (string key in character.Cards)
            {
                if (PartyCardKey.IsCard(key))
                    cards[PartyCardKey.IdOf(key)] = NameOf(character.Id);
                else if (PartyCardKey.IsItem(key))
                {
                    string id = PartyCardKey.IdOf(key);
                    items.TryAdd(id, NameOf(character.Id));
                    slots[id] = (slots.TryGetValue(id, out int count) ? count : 0) + 1;
                }
            }

            var owned = new List<WorkshopOwned>();
            foreach (var item in gear?.Items ?? Array.Empty<EquipmentItem>())
            {
                var definition = EquipmentCatalog.Find(item.EquipmentId);
                if (definition == null)
                    continue;
                string wearer = gear.WornBy(item.Id);
                owned.Add(
                    new WorkshopOwned
                    {
                        Kind = KindOf(definition.Slot),
                        RowId = item.Id,
                        KindId = definition.Id,
                        Name = definition.Name,
                        Stars = StarsOf(KindOf(definition.Slot), definition.Id),
                        Multiplier = item.Multiplier,
                        MaxMultiplier = definition.MaxMultiplier,
                        User = string.IsNullOrEmpty(wearer) ? null : NameOf(wearer),
                    }
                );
            }
            foreach (var card in party?.SkillCards ?? Array.Empty<PartySkillCard>())
            {
                var skill = CardSkills.Find(card.SkillId);
                if (skill == null)
                    continue;
                owned.Add(
                    new WorkshopOwned
                    {
                        Kind = LibraryKind.Skill,
                        RowId = card.Id,
                        KindId = card.SkillId,
                        Name = skill.Name,
                        Stars = StarsOf(LibraryKind.Skill, card.SkillId),
                        Multiplier = card.Multiplier,
                        MaxMultiplier = CardSkillQuality.LimitOf(skill),
                        User = cards.TryGetValue(card.Id, out string user) ? user : null,
                    }
                );
            }
            foreach (var item in party?.Items ?? Array.Empty<PartyItem>())
            {
                if (item.Count <= 0 || item.Item == null)
                    continue;
                var thing = Item(
                    item.ItemId,
                    item.Count,
                    items.TryGetValue(item.ItemId, out string user) ? user : null
                );
                thing.Reserved = slots.TryGetValue(item.ItemId, out int reserved) ? reserved : 0;
                owned.Add(thing);
            }
            return Sorted(owned);
        }

        /// <summary>The things of one kind, the rarer and the better first.</summary>
        public static IReadOnlyList<WorkshopOwned> OfKind(
            IEnumerable<WorkshopOwned> owned,
            LibraryKind kind
        ) => (owned ?? Array.Empty<WorkshopOwned>()).Where(thing => thing.Kind == kind).ToArray();

        /// <summary>How many of the kind <paramref name="kindId"/> are held.</summary>
        public static int CountOf(
            IEnumerable<WorkshopOwned> owned,
            LibraryKind kind,
            string kindId
        ) =>
            (owned ?? Array.Empty<WorkshopOwned>())
                .Where(thing => thing.Kind == kind && thing.KindId == kindId)
                .Sum(thing => thing.Count);

        public static LibraryKind KindOf(EquipmentSlot slot) =>
            slot switch
            {
                EquipmentSlot.Weapon => LibraryKind.Weapon,
                EquipmentSlot.Armor => LibraryKind.Armor,
                _ => LibraryKind.Accessory,
            };

        /// <summary>The stars of a kind, from the adventure's drop lists (0 when no list has it).</summary>
        public static int StarsOf(LibraryKind kind, string id) =>
            kind == LibraryKind.Item
                ? AdventureDrops.StarsOf(AdventureDropCategory.Item, id)
                : WorkshopGoods.StarsOf(kind, id);

        // 分類の順、☆の高い順、品質の高い順に並べる。
        private static IReadOnlyList<WorkshopOwned> Sorted(IEnumerable<WorkshopOwned> owned) =>
            owned
                .OrderBy(thing => IndexOf(thing.Kind))
                .ThenByDescending(thing => thing.Stars)
                .ThenBy(thing => thing.KindId, StringComparer.Ordinal)
                .ThenByDescending(thing => Quality.Percent(thing.Multiplier))
                .ToArray();

        private static int IndexOf(LibraryKind kind)
        {
            for (int i = 0; i < Kinds.Count; i++)
                if (Kinds[i] == kind)
                    return i;
            return Kinds.Count;
        }

        private static WorkshopOwned Item(string id, int count, string user = null) =>
            new()
            {
                Reserved = user != null ? 1 : 0,
                Kind = LibraryKind.Item,
                RowId = id,
                KindId = id,
                Name = ItemCards.Find(id)?.Name ?? id,
                Stars = StarsOf(LibraryKind.Item, id),
                Count = count,
                User = user,
            };

        private static WorkshopOwned Gear(string row, string id, int percent, string user = null)
        {
            var definition = EquipmentCatalog.Find(id);
            return new WorkshopOwned
            {
                Kind = definition != null ? KindOf(definition.Slot) : LibraryKind.Weapon,
                RowId = row,
                KindId = id,
                Name = definition?.Name ?? id,
                Stars = definition != null ? StarsOf(KindOf(definition.Slot), id) : 0,
                Multiplier = percent / 100f,
                MaxMultiplier = definition?.MaxMultiplier ?? Quality.MinMultiplier,
                User = user,
            };
        }

        private static WorkshopOwned Skill(string row, string id, int percent, string user = null)
        {
            var skill = CardSkills.Find(id);
            return new WorkshopOwned
            {
                Kind = LibraryKind.Skill,
                RowId = row,
                KindId = id,
                Name = skill?.Name ?? id,
                Stars = StarsOf(LibraryKind.Skill, id),
                Multiplier = percent / 100f,
                MaxMultiplier = CardSkillQuality.LimitOf(skill),
                User = user,
            };
        }

        /// <summary>
        /// The fixed list without a server (the showcase, the tests): ☆1 things in every quality
        /// to craft from, a few in use, and some items.
        /// </summary>
        public static IReadOnlyList<WorkshopOwned> Mock() =>
            Sorted(
                new[]
                {
                    Gear("mock-w1", "wooden-sword", 100, "アリア"),
                    Gear("mock-w2", "wooden-sword", 118),
                    Gear("mock-w3", "short-bow", 104),
                    Gear("mock-w4", "short-bow", 152),
                    Gear("mock-w5", "oak-staff", 100),
                    Gear("mock-w6", "oak-staff", 171, "トーマ"),
                    Gear("mock-w7", "wooden-sword", 189),
                    Gear("mock-a1", "leather-armor", 100, "アリア"),
                    Gear("mock-a2", "leather-armor", 126),
                    Gear("mock-a3", "wooden-shield", 108),
                    Gear("mock-a4", "wooden-shield", 140),
                    Gear("mock-c1", "moss-charm", 100),
                    Gear("mock-c2", "moss-charm", 115),
                    Gear("mock-c3", "moss-charm", 133),
                    Skill("mock-s1", "Slash", 100, "アリア"),
                    Skill("mock-s2", "Slash", 124),
                    Skill("mock-s3", "Fire", 100, "トーマ"),
                    Skill("mock-s4", "Fire", 145),
                    Skill("mock-s5", "Heal", 110),
                    Skill("mock-s6", "VitalThrust", 100),
                    Item("herb", 5, "ミナ"),
                    Item("acorn", 2),
                    Item("moss-wad", 1),
                }
            );
    }
}
