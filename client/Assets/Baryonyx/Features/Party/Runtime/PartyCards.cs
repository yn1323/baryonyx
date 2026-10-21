using System;
using Baryonyx.Combat;
using Baryonyx.Inventory;
using Baryonyx.Items;

namespace Baryonyx.Party
{
    /// <summary>
    /// What a custom skill slot holds, written as one key (doc/features/party.md): an owned skill
    /// card (<c>card:&lt;id&gt;</c>), an item (<c>item:&lt;item id&gt;</c>), or, without the server,
    /// a skill id from the mock data (every skill at ×1.00).
    /// </summary>
    public static class PartyCardKey
    {
        public const string CardPrefix = "card:";
        public const string ItemPrefix = "item:";

        public static string Card(string id) => CardPrefix + id;

        public static string Item(string itemId) => ItemPrefix + itemId;

        public static bool IsCard(string key) =>
            key != null && key.StartsWith(CardPrefix, StringComparison.Ordinal);

        public static bool IsItem(string key) =>
            key != null && key.StartsWith(ItemPrefix, StringComparison.Ordinal);

        /// <summary>The owned card's id or the item's id, without the prefix.</summary>
        public static string IdOf(string key) =>
            IsCard(key) ? key.Substring(CardPrefix.Length)
            : IsItem(key) ? key.Substring(ItemPrefix.Length)
            : key;

        /// <summary>The item the key stands for, or null for a skill card or an unknown item.</summary>
        public static ItemCard ItemOf(string key) => IsItem(key) ? ItemCards.Find(IdOf(key)) : null;
    }

    /// <summary>One skill card the player owns: its kind and its own multiplier.</summary>
    public sealed class PartySkillCard
    {
        public PartySkillCard(string id, string skillId, float multiplier)
        {
            Id = id ?? "";
            SkillId = skillId ?? "";
            Multiplier = Math.Max(Quality.MinMultiplier, multiplier);
        }

        public string Id { get; }
        public string SkillId { get; }
        public float Multiplier { get; }
        public string Key => PartyCardKey.Card(Id);

        /// <summary>The card with its multiplier applied, or null for a skill this build does not know.</summary>
        public CardSkill Skill => CardSkills.Find(SkillId)?.WithMultiplier(Multiplier);
    }

    /// <summary>One kind of item the player owns, and how many.</summary>
    public sealed class PartyItem
    {
        public PartyItem(string itemId, int count)
        {
            ItemId = itemId ?? "";
            Count = Math.Max(0, count);
        }

        public string ItemId { get; }
        public int Count { get; }
        public string Key => PartyCardKey.Item(ItemId);
        public ItemCard Item => ItemCards.Find(ItemId);
    }
}
