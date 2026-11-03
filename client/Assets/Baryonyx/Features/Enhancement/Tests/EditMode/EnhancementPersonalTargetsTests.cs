using System;
using System.Linq;
using Baryonyx.Enhancement;
using Baryonyx.Equipment;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 冒険者の強化のタブ（doc/features/screens/loadout.md#強化）：その人が付けている装備（武器・防具・アクセサリーの
    // 枠の順）、解放した固有スキル（番号の順）、カスタムスキルの枠のスキル（枠の順）を見出しごとに並べる。
    // ほかの人の物、空いた枠、アイテムの枠は出さない。
    public sealed class EnhancementPersonalTargetsTests
    {
        [Test]
        public void APersonsThingsComeUnderHeadingsInTheirSlotsOrder()
        {
            var gear = new EquipmentState(
                Array.Empty<EquipmentItem>(),
                new (string, EquipmentPlace, string)[]
                {
                    ("toma", EquipmentPlace.Accessory(1), "ring"),
                    ("toma", EquipmentSlot.Armor, "robe"),
                    ("toma", EquipmentSlot.Weapon, "staff"),
                    ("luka", EquipmentSlot.Weapon, "spear"),
                }
            );
            var equipment = new[] { "spear", "staff", "robe", "ring" }
                .Select(id => new EnhancementTarget { ItemId = id, Name = id })
                .ToArray();
            var uniques = new[]
            {
                new EnhancementTarget
                {
                    CharacterId = "toma",
                    Index = 1,
                    Name = "star",
                },
                new EnhancementTarget
                {
                    CharacterId = "luka",
                    Index = 0,
                    Name = "lance",
                },
                new EnhancementTarget
                {
                    CharacterId = "toma",
                    Index = 0,
                    Name = "burst",
                },
            };
            var skills = new[] { "c1", "c2", "c3" }
                .Select(id => new EnhancementTarget { ItemId = id, Name = id })
                .ToArray();

            var listed = EnhancementTargets.OfPerson(
                "toma",
                gear,
                new[] { "c3", null, "c1" },
                equipment,
                uniques,
                skills
            );

            Assert.That(
                listed.Select(entry => entry.Target.Name),
                Is.EqualTo(new[] { "staff", "robe", "ring", "burst", "star", "c3", "c1" })
            );
            Assert.That(
                listed.Select(entry => entry.Place),
                Is.EqualTo(
                    new[]
                    {
                        "weapon",
                        "armor",
                        "accessory2",
                        "unique:0",
                        "unique:1",
                        "slot:0",
                        "slot:2",
                    }
                )
            );
            Assert.That(
                listed.Select(entry => entry.Group).Distinct(),
                Is.EqualTo(
                    new[]
                    {
                        EnhancementTargets.GearGroup,
                        EnhancementTargets.UniqueGroup,
                        EnhancementTargets.SkillGroup,
                    }
                )
            );
        }

        // 何も付けていない人は、空の一覧になる（見出しも出さない）。
        [Test]
        public void APersonWithNothingHasAnEmptyList()
        {
            var listed = EnhancementTargets.OfPerson(
                "anselm",
                null,
                null,
                Array.Empty<EnhancementTarget>(),
                Array.Empty<EnhancementTarget>(),
                Array.Empty<EnhancementTarget>()
            );
            Assert.That(listed, Is.Empty);
        }
    }
}
