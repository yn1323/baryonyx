using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Training;

namespace Baryonyx.Enhancement
{
    /// <summary>What the polish can raise. The server names them as the keys of <see cref="EnhancementTargets.KeyOf"/>.</summary>
    public enum EnhancementCategory
    {
        Skill,
        Weapon,
        Armor,
        Accessory,
        Unique,
    }

    /// <summary>
    /// One thing whose multiplier a polish raises: an owned skill card or piece of equipment (by
    /// its own id), or a character's unique skill (by the character and its number).
    /// </summary>
    public sealed class EnhancementTarget
    {
        public EnhancementCategory Category { get; set; }

        // スキル・装備は持ち物の1つずつのID。固有スキルは空。
        public string ItemId { get; set; } = "";

        // 種類のID（スキルID・装備ID）。固有スキルは、セットしている段階のカードのID。
        public string KindId { get; set; } = "";
        public string Name { get; set; } = "";

        // 固有スキルの持ち主と番号。スキル・装備は空と-1。
        public string CharacterId { get; set; } = "";
        public int Index { get; set; } = -1;

        public float Multiplier { get; set; } = Inventory.Quality.MinMultiplier;
        public float MaxMultiplier { get; set; } = Inventory.Quality.MinMultiplier;

        public ItemQuality Quality => Inventory.Quality.Of(Multiplier, MaxMultiplier);
        public bool AtMax => Inventory.Quality.Percent(Multiplier) >= Inventory.Quality.Percent(MaxMultiplier);

        public EnhancementTarget WithMultiplier(float multiplier) =>
            new()
            {
                Category = Category,
                ItemId = ItemId,
                KindId = KindId,
                Name = Name,
                CharacterId = CharacterId,
                Index = Index,
                Multiplier = multiplier,
                MaxMultiplier = MaxMultiplier,
            };
    }

    /// <summary>One stage a unique skill can awaken into: one not acquired, whose stage before it is.</summary>
    public sealed class AwakeningOption
    {
        public string CharacterId { get; set; } = "";
        public string CharacterName { get; set; } = "";
        public int Index { get; set; }
        public int Stage { get; set; }
        public UniqueSkillTree Tree { get; set; }
        public CardSkill Card => Tree?.StageAt(Stage)?.Card;
        public CardSkill From => Tree?.StageAt(UniqueSkillTreeShape.Parent(Stage))?.Card;
        public int Runes => EnhancementMaterials.AwakeningRunes(Stage);
    }

    /// <summary>
    /// The things the trading house's "enhance" lists (doc/features/enhancement.md#強化の画面):
    /// the owned skills, the equipment, and the unique skills the characters have unlocked, with
    /// the stages each unique skill could awaken into.
    /// </summary>
    public static class EnhancementTargets
    {
        /// <summary>The category's name in the server's API.</summary>
        public static string KeyOf(EnhancementCategory category) =>
            category switch
            {
                EnhancementCategory.Skill => "skill",
                EnhancementCategory.Weapon => "weapon",
                EnhancementCategory.Armor => "armor",
                EnhancementCategory.Accessory => "accessory",
                _ => "unique",
            };

        /// <summary>The owned skill cards, the stronger first and in the definitions' order.</summary>
        public static IReadOnlyList<EnhancementTarget> Skills(IEnumerable<PartySkillCard> cards) =>
            (cards ?? Array.Empty<PartySkillCard>())
                .Select(card => (card, skill: CardSkills.Find(card.SkillId)))
                .Where(pair => pair.skill != null)
                .Select(pair => new EnhancementTarget
                {
                    Category = EnhancementCategory.Skill,
                    ItemId = pair.card.Id,
                    KindId = pair.card.SkillId,
                    Name = pair.skill.Name,
                    Multiplier = pair.card.Multiplier,
                    MaxMultiplier = CardSkillQuality.LimitOf(pair.skill),
                })
                .ToArray();

        /// <summary>The owned equipment: weapons, then armour, then accessories, each in the order owned.</summary>
        public static IReadOnlyList<EnhancementTarget> Equipment(EquipmentState state)
        {
            var items = state?.Items ?? Array.Empty<EquipmentItem>();
            return new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor, EquipmentSlot.Accessory }
                .SelectMany(slot =>
                    items
                        .Select(item => (item, gear: EquipmentCatalog.Find(item.EquipmentId)))
                        .Where(pair => pair.gear != null && pair.gear.Slot == slot)
                        .Select(pair => new EnhancementTarget
                        {
                            Category = CategoryOf(slot),
                            ItemId = pair.item.Id,
                            KindId = pair.gear.Id,
                            Name = pair.gear.Name,
                            Multiplier = pair.item.Multiplier,
                            MaxMultiplier = pair.gear.MaxMultiplier,
                        })
                )
                .ToArray();
        }

        public static EnhancementCategory CategoryOf(EquipmentSlot slot) =>
            slot switch
            {
                EquipmentSlot.Weapon => EnhancementCategory.Weapon,
                EquipmentSlot.Armor => EnhancementCategory.Armor,
                _ => EnhancementCategory.Accessory,
            };

        /// <summary>
        /// The unique skills of the characters, in the roster's order, that their level has
        /// unlocked. <paramref name="progressOf"/> gives each one's progress and multiplier.
        /// </summary>
        public static IReadOnlyList<EnhancementTarget> Uniques(
            IEnumerable<PartyMember> roster,
            TrainingMockData training,
            Func<string, int, UniqueSkillProgress> progressOf
        ) =>
            UnlockedUniques(roster, training)
                .Select(unique =>
                {
                    var progress = progressOf(unique.Member.Id, unique.Index);
                    var root = unique.Tree.Root?.Card;
                    // 名前と絵は、セットしている段階のカード。倍率の上限は段階によらず同じ。
                    var set = unique.Tree.StageAt(progress?.SetStage ?? 0)?.Card ?? root;
                    return new EnhancementTarget
                    {
                        Category = EnhancementCategory.Unique,
                        KindId = set?.Id ?? unique.Tree.Id,
                        Name = set?.Name ?? unique.Tree.Id,
                        CharacterId = unique.Member.Id,
                        Index = unique.Index,
                        Multiplier = progress?.Multiplier ?? Quality.MinMultiplier,
                        MaxMultiplier = root != null ? CardSkillQuality.LimitOf(root) : Quality.MinMultiplier,
                    };
                })
                .ToArray();

        /// <summary>The stages each unlocked unique skill could awaken into, character by character.</summary>
        public static IReadOnlyList<AwakeningOption> Awakenings(
            IEnumerable<PartyMember> roster,
            TrainingMockData training,
            Func<string, int, UniqueSkillProgress> progressOf
        ) =>
            UnlockedUniques(roster, training)
                .SelectMany(unique =>
                {
                    var progress = progressOf(unique.Member.Id, unique.Index) ?? new UniqueSkillProgress();
                    return Enumerable
                        .Range(1, UniqueSkillTreeShape.Count - 1)
                        .Where(stage =>
                            !progress.Has(stage)
                            && progress.Has(UniqueSkillTreeShape.Parent(stage))
                            && unique.Tree.StageAt(stage) != null
                        )
                        .Select(stage => new AwakeningOption
                        {
                            CharacterId = unique.Member.Id,
                            CharacterName = unique.Member.Name,
                            Index = unique.Index,
                            Stage = stage,
                            Tree = unique.Tree,
                        });
                })
                .ToArray();

        private static IEnumerable<(PartyMember Member, int Index, UniqueSkillTree Tree)> UnlockedUniques(
            IEnumerable<PartyMember> roster,
            TrainingMockData training
        )
        {
            foreach (var member in roster ?? Array.Empty<PartyMember>())
            {
                var growth = member != null && training != null ? training.Find(member.Id) : null;
                if (growth == null)
                    continue;
                for (int i = 0; i < growth.Uniques.Length; i++)
                {
                    var unique = growth.Uniques[i];
                    var tree = UniqueSkillTrees.Find(unique.Tree);
                    if (tree != null && unique.UnlockLevel <= member.Level)
                        yield return (member, i, tree);
                }
            }
        }
    }
}
