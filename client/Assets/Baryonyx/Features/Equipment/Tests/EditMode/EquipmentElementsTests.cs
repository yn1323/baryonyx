using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 装備の属性（doc/features/equipment.md#装備の属性）。
    public sealed class EquipmentElementsTests
    {
        [Test]
        public void EveryKindHasTwoOrMoreElementsOrNone()
        {
            foreach (var definition in EquipmentCatalog.All)
            {
                var elements = definition.Elements;
                Assert.That(elements.Count, Is.Not.EqualTo(1), definition.Id);
                Assert.That(elements, Has.No.Member(CardElement.None), definition.Id);
                Assert.That(elements.Distinct().Count(), Is.EqualTo(elements.Count), definition.Id);
            }
        }

        [Test]
        public void OneStarWeaponsAndArmorHaveNone()
        {
            var plain = EquipmentCatalog.All.Where(definition =>
                definition.Rarity == 1 && definition.Slot != EquipmentSlot.Accessory
            );
            foreach (var definition in plain)
                Assert.That(definition.Elements, Is.Empty, definition.Id);
        }

        [Test]
        public void MostAccessoriesHaveNone()
        {
            var accessories = EquipmentCatalog
                .All.Where(definition => definition.Slot == EquipmentSlot.Accessory)
                .ToArray();
            int plain = accessories.Count(definition => definition.Elements.Count == 0);
            Assert.That(plain * 2, Is.GreaterThan(accessories.Length));
        }

        // 属性が1つでも合えば付けられる。属性なしと、属性の決まっていない人（カードなし）は誰でも。
        [Test]
        public void FitsWhenOneElementIsShared()
        {
            var dagger = EquipmentCatalog.Find("flame-dagger");
            Assert.That(dagger.Fits(new[] { CardElement.Fire, CardElement.Ice }), Is.True);
            Assert.That(dagger.Fits(new[] { CardElement.Pierce }), Is.False);
            Assert.That(dagger.Fits(System.Array.Empty<CardElement>()), Is.True);
            Assert.That(
                EquipmentCatalog.Find("oak-staff").Fits(new[] { CardElement.Pierce }),
                Is.True
            );
        }

        [Test]
        public void ElementsFollowTheNameInOrder()
        {
            Assert.That(
                EquipmentCatalog.Find("flame-dagger").Elements,
                Is.EqualTo(new[] { CardElement.Fire, CardElement.Slash })
            );
            Assert.That(
                EquipmentCatalog.Find("magic-robe").Elements,
                Is.EqualTo(new[] { CardElement.Fire, CardElement.Ice, CardElement.Thunder })
            );
            Assert.That(EquipmentCatalog.Find("oak-staff").Elements, Is.Empty);
        }
    }
}
