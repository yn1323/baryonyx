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
        public bool AtMax =>
            Inventory.Quality.Percent(Multiplier) >= Inventory.Quality.Percent(MaxMultiplier);

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

    /// <summary>One thing on a person's enhance tab: the heading it is listed under and where they hold it.</summary>
    public sealed class PersonalTarget
    {
        public EnhancementTarget Target { get; set; }

        // 見出し（EnhancementTargets.GearGroup などの1つ）。
        public string Group { get; set; } = "";

        // 付けている所：装備は枠の名前（「weapon」「accessory1」）、固有スキルは「unique:0」、カスタムスキルは「slot:0」。
        public string Place { get; set; } = "";
    }

    /// <summary>
    /// One stage a card can awaken into: a unique skill's stage not acquired whose stage before
    /// it is, or one of the two branches of an owned skill card that has not awakened
    /// (doc/features/skill-awakening.md; <see cref="OwnedCard"/> set).
    /// </summary>
    public sealed class AwakeningOption
    {
        public string CharacterId { get; set; } = "";
        public string CharacterName { get; set; } = "";
        public int Index { get; set; }
        public int Stage { get; set; }
        public UniqueSkillTree Tree { get; set; }

        /// <summary>The owned skill card that awakens (its id), or empty for a unique skill.</summary>
        public string OwnedCard { get; set; } = "";

        /// <summary>The owned card's card skill, when <see cref="OwnedCard"/> is set.</summary>
        public string SkillId { get; set; } = "";

        /// <summary>The owned card's multiplier and its kind's limit, when <see cref="OwnedCard"/> is set.</summary>
        public float Multiplier { get; set; } = Quality.MinMultiplier;
        public float MaxMultiplier { get; set; } = Quality.MinMultiplier;

        public bool IsSkillCard => !string.IsNullOrEmpty(OwnedCard);

        /// <summary>The branch an owned card awakens into (1 or 2): the option's <see cref="Stage"/>.</summary>
        public int Branch => IsSkillCard ? Stage : 0;

        public CardSkill Card =>
            IsSkillCard
                ? CardSkillAwakenings.Find(SkillId + "." + Stage)
                : Tree?.StageAt(Stage)?.Card;

        public CardSkill From =>
            IsSkillCard
                ? CardSkills.Find(SkillId)
                : Tree?.StageAt(UniqueSkillTreeShape.Parent(Stage))?.Card;

        // スキルの覚醒は、固有スキルの1段目と同じ費用（doc/features/skill-awakening.md#商会での費用）。
        public int Runes =>
            IsSkillCard
                ? EnhancementMaterials.FirstStageRunes
                : EnhancementMaterials.AwakeningRunes(Stage);
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
                        MaxMultiplier =
                            root != null ? CardSkillQuality.LimitOf(root) : Quality.MinMultiplier,
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
                    var progress =
                        progressOf(unique.Member.Id, unique.Index) ?? new UniqueSkillProgress();
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

        /// <summary>
        /// The two branches each owned skill card could awaken into, for the cards that have not
        /// awakened, in the order owned (doc/features/skill-awakening.md).
        /// <paramref name="holders"/> gives who holds a card in a slot, by its id.
        /// </summary>
        public static IReadOnlyList<AwakeningOption> SkillAwakenings(
            IEnumerable<PartySkillCard> cards,
            IReadOnlyDictionary<string, string> holders
        ) =>
            (cards ?? Array.Empty<PartySkillCard>())
                .Where(card =>
                    card.AwakenedBranch == 0
                    && CardSkillAwakenings.NextStages(card.SkillId).Count > 0
                )
                .SelectMany(card =>
                    CardSkillAwakenings
                        .NextStages(card.SkillId)
                        .Select(
                            (stage, i) =>
                                new AwakeningOption
                                {
                                    OwnedCard = card.Id,
                                    SkillId = card.SkillId,
                                    Stage = i + 1,
                                    CharacterName =
                                        holders != null
                                        && holders.TryGetValue(card.Id, out string holder)
                                            ? holder
                                            : "持ち物",
                                    Multiplier = card.Multiplier,
                                    MaxMultiplier = CardSkillQuality.LimitOf(
                                        CardSkills.Find(card.SkillId)
                                    ),
                                }
                        )
                )
                .ToArray();

        // 冒険者の強化のタブの見出し。
        public const string GearGroup = "装備";
        public const string UniqueGroup = "固有スキル";
        public const string SkillGroup = "カスタムスキル";

        /// <summary>Where a unique skill sits on a person's enhance tab ("unique:0").</summary>
        public static string UniquePlace(int index) => "unique:" + index;

        /// <summary>Where a custom skill slot sits on a person's enhance tab ("slot:0").</summary>
        public static string SlotPlace(int index) => "slot:" + index;

        /// <summary>
        /// What <paramref name="person"/> holds, for the adventurer's enhance tab: the equipment
        /// worn (the weapon, the armour, then the accessory slots), the unique skills unlocked,
        /// then the skill cards in the custom skill slots, each taken from the lists of every
        /// owned thing. <paramref name="cards"/> are the owned cards' ids in slot order (null for
        /// a slot empty or holding an item).
        /// </summary>
        public static IReadOnlyList<PersonalTarget> OfPerson(
            string person,
            EquipmentState gear,
            IReadOnlyList<string> cards,
            IReadOnlyList<EnhancementTarget> equipment,
            IReadOnlyList<EnhancementTarget> uniques,
            IReadOnlyList<EnhancementTarget> skills
        )
        {
            var list = new List<PersonalTarget>();
            var places = new[]
            {
                new EquipmentPlace(EquipmentSlot.Weapon),
                new EquipmentPlace(EquipmentSlot.Armor),
            }.Concat(Enumerable.Range(0, AccessorySlots.Count).Select(EquipmentPlace.Accessory));
            foreach (var place in places)
            {
                string item = gear?.ItemIn(person, place);
                var target = item != null ? equipment?.FirstOrDefault(t => t.ItemId == item) : null;
                if (target != null)
                    list.Add(
                        new PersonalTarget
                        {
                            Target = target,
                            Group = GearGroup,
                            Place = place.Key,
                        }
                    );
            }
            foreach (
                var target in (uniques ?? Array.Empty<EnhancementTarget>())
                    .Where(t => t.CharacterId == person)
                    .OrderBy(t => t.Index)
            )
                list.Add(
                    new PersonalTarget
                    {
                        Target = target,
                        Group = UniqueGroup,
                        Place = UniquePlace(target.Index),
                    }
                );
            for (int i = 0; i < (cards?.Count ?? 0); i++)
            {
                string id = cards[i];
                var target = id != null ? skills?.FirstOrDefault(t => t.ItemId == id) : null;
                if (target != null)
                    list.Add(
                        new PersonalTarget
                        {
                            Target = target,
                            Group = SkillGroup,
                            Place = SlotPlace(i),
                        }
                    );
            }
            return list;
        }

        private static IEnumerable<(
            PartyMember Member,
            int Index,
            UniqueSkillTree Tree
        )> UnlockedUniques(IEnumerable<PartyMember> roster, TrainingMockData training)
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
