using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Baryonyx.Combat;
using Baryonyx.Enhancement;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.Items;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// What the drop editor (doc/features/screens/debug-rooms.md#ドロップエディター) changes: for
    /// each destination with a drop list, the kinds in the list (<see cref="AdventureDrops.Pools"/>)
    /// and the rewards of its first five clears (<see cref="AdventureClearRewards"/>), as drafts
    /// that start from those tables. A kind falls in one destination only (doc/features/drops.md),
    /// so adding one listed elsewhere moves it. <see cref="ChangesJson"/> writes the destinations
    /// changed, to be pasted to the AI and put into the tables.
    /// </summary>
    public sealed class DropLabDrafts
    {
        /// <summary>The marker the copied JSON starts with, so a paste is known for what it is.</summary>
        public const string Format = "baryonyx-drop-lab";

        public const int MaxCount = 99;

        /// <summary>The categories of a drop list, in its order.</summary>
        public static readonly AdventureDropCategory[] ListCategories =
        {
            AdventureDropCategory.Item,
            AdventureDropCategory.Accessory,
            AdventureDropCategory.Weapon,
            AdventureDropCategory.Armor,
            AdventureDropCategory.Skill,
        };

        /// <summary>The categories a clear's reward can be of: a drop list's and the materials.</summary>
        public static readonly AdventureDropCategory[] RewardCategories = ListCategories
            .Append(AdventureDropCategory.Material)
            .ToArray();

        /// <summary>One clear's reward: the kind, how many, and the quality it is sure to have (none for items and materials).</summary>
        public sealed class Reward
        {
            public AdventureDropCategory Category;
            public string Id = "";
            public int Count = 1;
            public ItemQuality? Quality;

            public AdventureDrop Drop => new("", Category, Id, 0f, Quality);

            public Reward Copy() => (Reward)MemberwiseClone();

            public bool Same(Reward other) =>
                other != null
                && Category == other.Category
                && Id == other.Id
                && Count == other.Count
                && Quality == other.Quality;
        }

        /// <summary>One destination's draft: its list in the list's order and its five clears' rewards.</summary>
        public sealed class Draft
        {
            public string Id = "";
            public string Name = "";
            public int Level;
            public List<AdventureDrops.Entry> Drops = new();
            public Reward[] Rewards = Array.Empty<Reward>();

            public bool Has(AdventureDropCategory category, string id) =>
                Drops.Exists(entry => entry.Category == category && entry.Id == id);

            public int IndexOf(AdventureDropCategory category, string id) =>
                Drops.FindIndex(entry => entry.Category == category && entry.Id == id);
        }

        private readonly List<Draft> drafts = Read();
        private readonly List<Draft> original = Read();

        /// <summary>The destinations with a drop list, from the nearest.</summary>
        public IReadOnlyList<Draft> All => drafts;

        public Draft Find(string id) => drafts.Find(draft => draft.Id == id);

        // 表から読む：ドロップの一覧を持つ行き先を近い順に。報酬の倍率は品質の幅からサーバーが決めるため持たない。
        private static List<Draft> Read() =>
            AdventureCatalog
                .Areas.Where(area => AdventureDrops.Pools.ContainsKey(area.Id))
                .Select(area => new Draft
                {
                    Id = area.Id,
                    Name = area.Name,
                    Level = area.Level,
                    Drops = AdventureDrops.Pools[area.Id].ToList(),
                    Rewards = AdventureClearRewards
                        .Of(area.Id)
                        .Select(reward => new Reward
                        {
                            Category = reward.Drop.Category,
                            Id = reward.Drop.Id,
                            Count = reward.Count,
                            Quality = reward.Drop.Quality,
                        })
                        .ToArray(),
                })
                .ToList();

        // --- 物の定義 ------------------------------------------------------------------

        /// <summary>Whether things of the category carry a quality (skills and equipment).</summary>
        public static bool HasQuality(AdventureDropCategory category) =>
            category
                is AdventureDropCategory.Skill
                    or AdventureDropCategory.Weapon
                    or AdventureDropCategory.Armor
                    or AdventureDropCategory.Accessory;

        /// <summary>Every kind of the category, in its catalog's order.</summary>
        public static IReadOnlyList<string> KindsOf(AdventureDropCategory category) =>
            category switch
            {
                AdventureDropCategory.Item => ItemCards.All.Select(item => item.Id).ToArray(),
                AdventureDropCategory.Skill => CardSkills.All.Select(skill => skill.Id).ToArray(),
                AdventureDropCategory.Material => EnhancementMaterials
                    .All.Select(material => material.Id)
                    .ToArray(),
                _ => EquipmentCatalog
                    .All.Where(gear => gear.Slot == SlotOf(category))
                    .Select(gear => gear.Id)
                    .ToArray(),
            };

        private static EquipmentSlot SlotOf(AdventureDropCategory category) =>
            category switch
            {
                AdventureDropCategory.Weapon => EquipmentSlot.Weapon,
                AdventureDropCategory.Armor => EquipmentSlot.Armor,
                _ => EquipmentSlot.Accessory,
            };

        public static string NameOf(AdventureDropCategory category, string id) =>
            AdventureTexts.DropName(new AdventureDrop("", category, id));

        /// <summary>
        /// The stars of a kind: an item's, a piece of equipment's and a material's from its
        /// definition, a skill's from the list that has it (it has no stars of its own), else 1.
        /// </summary>
        public int StarsOf(AdventureDropCategory category, string id)
        {
            switch (category)
            {
                case AdventureDropCategory.Item:
                    return ItemCards.Find(id)?.Rarity ?? 1;
                case AdventureDropCategory.Material:
                    return EnhancementMaterials.Find(id)?.Stars ?? 1;
                case AdventureDropCategory.Skill:
                    foreach (var draft in drafts)
                    {
                        int index = draft.IndexOf(category, id);
                        if (index >= 0)
                            return draft.Drops[index].Stars;
                    }
                    int listed = AdventureDrops.StarsOf(category, id);
                    return listed > 0 ? listed : 1;
                default:
                    return EquipmentCatalog.Find(id)?.Rarity ?? 1;
            }
        }

        /// <summary>The destination whose list has the kind, or null.</summary>
        public Draft ListOf(AdventureDropCategory category, string id) =>
            drafts.Find(draft => draft.Has(category, id));

        // --- ドロップの一覧 --------------------------------------------------------------

        /// <summary>
        /// Adds the kind to the destination's list, as one that also falls on the way. A kind in
        /// another list is moved from it (<paramref name="message"/> says so); one already in this
        /// list, or a material, is refused with the reason.
        /// </summary>
        public bool Add(
            string destinationId,
            AdventureDropCategory category,
            string id,
            out string message
        )
        {
            message = null;
            var draft = Find(destinationId);
            if (draft == null)
                return false;
            if (!ListCategories.Contains(category))
            {
                message = "強化素材は一覧に入れません";
                return false;
            }
            if (draft.Has(category, id))
            {
                message = "すでに一覧にあります";
                return false;
            }
            int stars = StarsOf(category, id);
            var from = ListOf(category, id);
            if (from != null)
            {
                from.Drops.RemoveAt(from.IndexOf(category, id));
                message = $"{from.Name}の一覧から移しました";
            }
            draft.Drops.Add(new AdventureDrops.Entry(category, id, stars));
            Sort(draft);
            return true;
        }

        public bool Remove(string destinationId, AdventureDropCategory category, string id)
        {
            var draft = Find(destinationId);
            int index = draft?.IndexOf(category, id) ?? -1;
            if (index < 0)
                return false;
            draft.Drops.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Makes the kind fall only in the boss's room, or on the way too. Only stars 3–5 fall in
        /// the boss's room, so a kind of fewer stars is refused.
        /// </summary>
        public bool ToggleBoss(
            string destinationId,
            AdventureDropCategory category,
            string id,
            out string message
        )
        {
            message = null;
            var draft = Find(destinationId);
            int index = draft?.IndexOf(category, id) ?? -1;
            if (index < 0)
                return false;
            var entry = draft.Drops[index];
            if (!entry.BossOnly && entry.Stars < 3)
            {
                message = "最奥だけで落ちる物は☆3以上にします";
                return false;
            }
            draft.Drops[index] = new AdventureDrops.Entry(
                category,
                id,
                entry.Stars,
                !entry.BossOnly
            );
            Sort(draft);
            return true;
        }

        /// <summary>
        /// Gives a skill in the list one star more, back to the fewest after ☆5 (☆3 for one that
        /// falls only in the boss's room). The others' stars are their definitions'.
        /// </summary>
        public bool NextStars(string destinationId, AdventureDropCategory category, string id)
        {
            var draft = Find(destinationId);
            int index = draft?.IndexOf(category, id) ?? -1;
            if (index < 0 || category != AdventureDropCategory.Skill)
                return false;
            var entry = draft.Drops[index];
            int fewest = entry.BossOnly ? 3 : 1;
            int stars = entry.Stars >= 5 ? fewest : Math.Max(fewest, entry.Stars + 1);
            draft.Drops[index] = new AdventureDrops.Entry(category, id, stars, entry.BossOnly);
            Sort(draft);
            return true;
        }

        // 分類の順に並べ、分類の中は道中でも落ちる物を☆の少ない順に、最奥だけの物をその後に置く（表と同じ並び）。
        private static void Sort(Draft draft)
        {
            draft.Drops = draft
                .Drops.Select((entry, order) => (entry, order))
                .OrderBy(pair => Array.IndexOf(ListCategories, pair.entry.Category))
                .ThenBy(pair => pair.entry.BossOnly ? 1 : 0)
                .ThenBy(pair => pair.entry.Stars)
                .ThenBy(pair => pair.order)
                .Select(pair => pair.entry)
                .ToList();
        }

        // --- クリア回数の報酬 --------------------------------------------------------------

        /// <summary>The reward of the <paramref name="clears"/>th clear (1–5), or null.</summary>
        public Reward RewardOf(string destinationId, int clears)
        {
            var draft = Find(destinationId);
            return draft != null && clears >= 1 && clears <= draft.Rewards.Length
                ? draft.Rewards[clears - 1]
                : null;
        }

        /// <summary>
        /// Makes the kind the clear's reward, one of it, keeping the quality it had (A when it had
        /// none) for a skill or equipment.
        /// </summary>
        public bool SetReward(
            string destinationId,
            int clears,
            AdventureDropCategory category,
            string id
        )
        {
            var reward = RewardOf(destinationId, clears);
            if (reward == null)
                return false;
            if (reward.Category == category && reward.Id == id)
                return true;
            var quality = reward.Quality ?? ItemQuality.A;
            reward.Category = category;
            reward.Id = id;
            reward.Count = 1;
            reward.Quality = HasQuality(category) ? quality : null;
            return true;
        }

        /// <summary>Sets how many of the reward are given, 1 to <see cref="MaxCount"/>.</summary>
        public bool SetCount(string destinationId, int clears, int count, out string message)
        {
            message = null;
            var reward = RewardOf(destinationId, clears);
            if (reward == null)
                return false;
            if (count < 1 || count > MaxCount)
            {
                message = $"数は1〜{MaxCount}にします";
                return false;
            }
            reward.Count = count;
            return true;
        }

        /// <summary>Turns the reward's quality to the next, C→B→A→S→C. Items and materials have none.</summary>
        public bool NextQuality(string destinationId, int clears)
        {
            var reward = RewardOf(destinationId, clears);
            if (reward?.Quality is not { } quality)
                return false;
            int index = Array.IndexOf(Quality.All, quality);
            reward.Quality = Quality.All[(index + 1) % Quality.All.Length];
            return true;
        }

        // --- 変えたところ ----------------------------------------------------------------

        public bool DropsChanged(string destinationId)
        {
            var draft = Find(destinationId);
            var first = original.Find(each => each.Id == destinationId);
            return draft != null
                && first != null
                && !draft.Drops.Select(Key).SequenceEqual(first.Drops.Select(Key));
        }

        public bool RewardsChanged(string destinationId)
        {
            var draft = Find(destinationId);
            var first = original.Find(each => each.Id == destinationId);
            return draft != null
                && first != null
                && (
                    draft.Rewards.Length != first.Rewards.Length
                    || draft.Rewards.Where((reward, i) => !reward.Same(first.Rewards[i])).Any()
                );
        }

        public bool Changed(string destinationId) =>
            DropsChanged(destinationId) || RewardsChanged(destinationId);

        /// <summary>The destinations changed, from the nearest.</summary>
        public IReadOnlyList<Draft> ChangedDrafts =>
            drafts.Where(draft => Changed(draft.Id)).ToArray();

        private static (AdventureDropCategory, string, int, bool) Key(AdventureDrops.Entry entry) =>
            (entry.Category, entry.Id, entry.Stars, entry.BossOnly);

        /// <summary>
        /// The destinations changed as JSON: for each its ID and name, and only what changed of
        /// it, its whole drop list (category, ID, name, stars, and "boss" for one that falls only
        /// in the boss's room) and its five clears' rewards (category, ID, name, count, quality).
        /// </summary>
        public string ChangesJson()
        {
            var json = new StringBuilder();
            json.Append("{\n  \"format\": \"").Append(Format).Append("\",\n  \"dungeons\": [");
            var changed = ChangedDrafts;
            for (int d = 0; d < changed.Count; d++)
            {
                var draft = changed[d];
                json.Append(d == 0 ? "\n" : ",\n").Append("    {\n");
                json.Append("      ");
                Field(json, "id", draft.Id, true);
                Field(json, "name", draft.Name, false);
                if (DropsChanged(draft.Id))
                {
                    json.Append(",\n      \"drops\": [");
                    for (int i = 0; i < draft.Drops.Count; i++)
                    {
                        var entry = draft.Drops[i];
                        json.Append(i == 0 ? "\n" : ",\n").Append("        {");
                        Field(json, "category", AdventureDrops.KeyOf(entry.Category), true);
                        Field(json, "id", entry.Id, false);
                        Field(json, "name", NameOf(entry.Category, entry.Id), false);
                        Number(json, "stars", entry.Stars);
                        if (entry.BossOnly)
                            json.Append(", \"boss\": true");
                        json.Append(" }");
                    }
                    json.Append(draft.Drops.Count > 0 ? "\n      ]" : "]");
                }
                if (RewardsChanged(draft.Id))
                {
                    json.Append(",\n      \"clearRewards\": [");
                    for (int i = 0; i < draft.Rewards.Length; i++)
                    {
                        var reward = draft.Rewards[i];
                        json.Append(i == 0 ? "\n" : ",\n").Append("        {");
                        json.Append(" \"clears\": ")
                            .Append((i + 1).ToString(CultureInfo.InvariantCulture));
                        Field(json, "category", AdventureDrops.KeyOf(reward.Category), false);
                        Field(json, "id", reward.Id, false);
                        Field(json, "name", NameOf(reward.Category, reward.Id), false);
                        Number(json, "count", reward.Count);
                        if (reward.Quality is { } quality)
                            Field(json, "quality", Quality.Letter(quality), false);
                        json.Append(" }");
                    }
                    json.Append(draft.Rewards.Length > 0 ? "\n      ]" : "]");
                }
                json.Append("\n    }");
            }
            json.Append(changed.Count > 0 ? "\n  ]\n}" : "]\n}");
            return json.ToString();
        }

        private static void Number(StringBuilder json, string name, int value) =>
            json.Append(", \"")
                .Append(name)
                .Append("\": ")
                .Append(value.ToString(CultureInfo.InvariantCulture));

        private static void Field(StringBuilder json, string name, string value, bool first)
        {
            if (!first)
                json.Append(',');
            json.Append(" \"").Append(name).Append("\": \"");
            foreach (char c in value ?? "")
            {
                if (c == '"' || c == '\\')
                    json.Append('\\').Append(c);
                else if (c < ' ')
                    json.Append(' ');
                else
                    json.Append(c);
            }
            json.Append('"');
        }
    }
}
