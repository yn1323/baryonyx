using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.SkillEvolution;
using Baryonyx.UI.Cards;

namespace Baryonyx.Inventory
{
    /// <summary>The quality lab's tabs, one per kind of thing that has a quality.</summary>
    public enum QualityLabTab
    {
        Skills,
        UniqueSkills,
        Weapons,
        Armor,
        Accessories,
    }

    /// <summary>
    /// One thing whose quality the lab tries: a card skill, a unique skill's stage, a weapon, a
    /// piece of armour or a sample accessory. It knows its kind's upper limit and
    /// how its effect reads at any multiplier.
    /// </summary>
    public sealed class QualityLabEntry
    {
        public QualityLabTab Tab;
        public string Id = "";
        public string Name = "";

        /// <summary>The kind's rarity in stars (0 when it has none or it is not decided).</summary>
        public int Stars;

        /// <summary>The kind's own upper limit of the multiplier.</summary>
        public float Limit = Quality.MinMultiplier;

        /// <summary>A skill's card (null for the others), and its art's place in the baked art.</summary>
        public CardSkill Skill;
        public int ArtIndex = -1;

        public EquipmentDefinition Gear;

        /// <summary>
        /// How the thing sparkles (doc/art/direction.md, card sparkle): a skill its kind's style,
        /// the others a style from their place in their list, stepped apart from the skills'.
        /// </summary>
        public int FoilStyle;

        // 見本のアクセサリーの光り方の割り当て（一覧の何番目かから決める）。装備は EquipmentCatalog の
        // 割り当てを、実際の画面と同じく使う。
        public const int AccessoryStyleStep = 2;
        public const int AccessoryStyleOffset = 1;

        /// <summary>A sample accessory's effect at ×1.00.</summary>
        public string Detail = "";

        public bool IsSkill => Skill != null;

        /// <summary>The card at <paramref name="multiplier"/> with the lab's <paramref name="limit"/>.</summary>
        public CardSkill SkillAt(float multiplier, float limit) =>
            Skill?.WithMultiplier(multiplier, limit);

        /// <summary>The effect at <paramref name="multiplier"/>, as one line (a skill's in rich text).</summary>
        public string EffectAt(float multiplier, float limit)
        {
            if (Skill != null)
                return UI.Cards.CardText.Description(SkillAt(multiplier, limit));
            if (Gear != null)
                return Gear.DetailAt(multiplier);
            return EquipmentCatalog.Scale(Detail, multiplier);
        }

        /// <summary>
        /// The numbers that change with the multiplier, before and after ("属攻の100% → 137%"),
        /// one to a line.
        /// </summary>
        public IEnumerable<string> NumbersAt(float multiplier, float limit)
        {
            if (Skill != null)
            {
                var grown = SkillAt(multiplier, limit);
                yield return $"コスト　{Skill.Cost} → {grown.Cost}";
                for (int i = 0; i < Skill.Actions.Count; i++)
                {
                    var from = Skill.Actions[i];
                    var to = grown.Actions[i];
                    if (from.HasPower)
                        yield return $"{CardRules.StatName(from.Stat)}の割合　{from.Percent}% → {to.Percent}%";
                    if (from.Kind == CardActionKind.Damage)
                        yield return $"回数　{from.Hits} → {to.Hits}";
                    if (from.Turns > 0)
                        yield return $"ターン　{from.Turns} → {to.Turns}";
                    if (from.Amount > 0)
                        yield return $"量　{from.Amount} → {to.Amount}";
                }
            }
            else
                yield return $"効果　{(Gear != null ? Gear.Detail : Detail)}";
            yield return $"光り方　{CardFoil.NameOf(FoilStyle)}";
        }

        /// <summary>The kind's name on the detail line ("カードスキル", "武器").</summary>
        public string KindName =>
            Tab switch
            {
                QualityLabTab.Skills => "カードスキル",
                QualityLabTab.UniqueSkills => "固有スキル",
                QualityLabTab.Weapons => "武器",
                QualityLabTab.Armor => "防具",
                _ => "アクセサリー（見本）",
            };

        /// <summary>
        /// Sample accessories for the lab only, while accessories have no data of their own
        /// (doc/features/equipment.md#アクセサリー): names, rarity and effects are placeholders.
        /// </summary>
        public static readonly (
            string Id,
            string Name,
            int Stars,
            string Detail,
            float Limit
        )[] SampleAccessories =
        {
            ("sample-accessory-1", "見本のアクセサリー1", 1, "物攻 +5%", 2f),
            ("sample-accessory-2", "見本のアクセサリー2", 3, "属攻 +6%｜会心 +3%", 2.2f),
            ("sample-accessory-3", "見本のアクセサリー3", 5, "物防 +4%｜HP +30", 2.6f),
        };

        /// <summary>The card skills, then the unique skills' stages, in the order their art is baked.</summary>
        public static IReadOnlyList<CardSkill> Cards =>
            CardSkills.All.Concat(UniqueSkillTrees.Stages().Select(stage => stage.Card)).ToArray();

        /// <summary>Every entry of <paramref name="tab"/>.</summary>
        public static IReadOnlyList<QualityLabEntry> Of(QualityLabTab tab)
        {
            switch (tab)
            {
                case QualityLabTab.Skills:
                    return CardSkills.All.Select((skill, i) => Card(tab, skill, i)).ToArray();
                case QualityLabTab.UniqueSkills:
                    int offset = CardSkills.All.Count;
                    return UniqueSkillTrees
                        .Stages()
                        .Select((stage, i) => Card(tab, stage.Card, offset + i))
                        .ToArray();
                case QualityLabTab.Weapons:
                case QualityLabTab.Armor:
                    var slot =
                        tab == QualityLabTab.Weapons ? EquipmentSlot.Weapon : EquipmentSlot.Armor;
                    return EquipmentCatalog
                        .All.Where(gear => gear.Slot == slot)
                        .Select(gear => new QualityLabEntry
                        {
                            Tab = tab,
                            Id = gear.Id,
                            Name = gear.Name,
                            Stars = gear.Rarity,
                            Limit = gear.MaxMultiplier,
                            Gear = gear,
                            FoilStyle = EquipmentCatalog.FoilStyleOf(gear.Id),
                        })
                        .ToArray();
                default:
                    return SampleAccessories
                        .Select(
                            (sample, i) =>
                                new QualityLabEntry
                                {
                                    Tab = tab,
                                    Id = sample.Id,
                                    Name = sample.Name,
                                    Stars = sample.Stars,
                                    Detail = sample.Detail,
                                    Limit = sample.Limit,
                                    FoilStyle = CardFoil.StyleAt(
                                        i,
                                        AccessoryStyleStep,
                                        AccessoryStyleOffset
                                    ),
                                }
                        )
                        .ToArray();
            }
        }

        // 固有スキルはレア度を持たない。スキルのレア度は未決のため、☆は出さない。
        private static QualityLabEntry Card(QualityLabTab tab, CardSkill skill, int art) =>
            new()
            {
                Tab = tab,
                Id = skill.Id,
                Name = skill.Name,
                Limit = CardSkillQuality.LimitOf(skill),
                Skill = skill,
                ArtIndex = art,
                FoilStyle = skill.FoilStyle,
            };
    }
}
