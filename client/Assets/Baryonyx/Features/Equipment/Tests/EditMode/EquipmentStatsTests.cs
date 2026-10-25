using Baryonyx.Combat;
using Baryonyx.Equipment;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 装備の効果（doc/features/equipment.md#装備の効果）：付けている装備の割合でステータスを上げる。
    public sealed class EquipmentStatsTests
    {
        // トーマのLv 12のステータス。
        private static readonly CharacterStats Toma = new(309, 94, 148, 282, 248, 188, 40, 67);

        private static EquipmentState Wearing(
            params (string Item, string Kind, float Multiplier, EquipmentPlace Place)[] worn
        )
        {
            var items = new EquipmentItem[worn.Length];
            var equipped = new (string, EquipmentPlace, string)[worn.Length];
            for (int i = 0; i < worn.Length; i++)
            {
                items[i] = new EquipmentItem(worn[i].Item, worn[i].Kind, worn[i].Multiplier);
                equipped[i] = ("toma", worn[i].Place, worn[i].Item);
            }
            return new EquipmentState(items, equipped);
        }

        // 同じ項目の割合は足し合わせ、ステータスに掛けて四捨五入する。
        [Test]
        public void TheWornItemsPercentsAddUpAndRaiseTheStats()
        {
            // 樫の杖（属攻 +11%）と魔法のローブ（属攻 +12%・物防 +11%）。
            var raise = EquipmentStats.Bonus(Toma, EquipmentLocalSource.Starter(), "toma");
            Assert.That(raise.MagicAttack, Is.EqualTo(65)); // 282 × 23% = 64.86
            Assert.That(raise.PhysicalDefense, Is.EqualTo(16)); // 148 × 11% = 16.28
            Assert.That(raise.PhysicalAttack, Is.Zero);
            Assert.That(raise.Hp, Is.Zero);
            var stats = EquipmentStats.With(Toma, EquipmentLocalSource.Starter(), "toma");
            Assert.That(stats.MagicAttack, Is.EqualTo(282 + 65));
            Assert.That(stats.Speed, Is.EqualTo(Toma.Speed));
        }

        // 割合には、装備が1つずつ持つ倍率を掛ける。アクセサリーの枠の装備も足す。
        [Test]
        public void TheMultiplierGrowsThePercent()
        {
            var state = Wearing(
                ("a", "oak-staff", 2f, EquipmentSlot.Weapon),
                ("b", "dew-pendant", 1f, EquipmentPlace.Accessory(0))
            );
            var percents = EquipmentStats.Percents(EquipmentStats.Worn(state, "toma"));
            Assert.That(percents[3], Is.EqualTo(22 + 5));
            Assert.That(EquipmentStats.Bonus(Toma, state, "toma").MagicAttack, Is.EqualTo(76)); // 282 × 27% = 76.14
        }

        // ほかの人の装備と、装備のない人は上げない。
        [Test]
        public void OnlyWhatThePersonWearsCounts()
        {
            var starter = EquipmentLocalSource.Starter();
            Assert.That(
                EquipmentStats.Bonus(Toma, starter, "anselm"),
                Is.EqualTo(default(CharacterStats))
            );
            Assert.That(EquipmentStats.With(Toma, null, "toma"), Is.EqualTo(Toma));
        }

        // 2回発動の確率は、ステータスを上げずに足し合わせ、100%で止める。
        [Test]
        public void TheChanceToPlayTwiceAddsUpToOneHundred()
        {
            var dagger = Wearing(("d", "flame-dagger", 1.8f, EquipmentSlot.Weapon));
            Assert.That(EquipmentStats.TwiceChance(dagger, "toma"), Is.EqualTo(18));
            Assert.That(EquipmentStats.Bonus(Toma, dagger, "toma").Speed, Is.EqualTo(43)); // 188 × 23%
            var strong = Wearing(("d", "flame-dagger", 12f, EquipmentSlot.Weapon));
            Assert.That(EquipmentStats.TwiceChance(strong, "toma"), Is.EqualTo(100));
            Assert.That(
                EquipmentStats.TwiceChance(EquipmentLocalSource.Starter(), "toma"),
                Is.Zero
            );
        }

        // 効果の文は「属攻 +11%」の形で、倍率を掛けた値を書く。
        [Test]
        public void TheDescriptionNamesTheStatAndThePercent()
        {
            Assert.That(
                EquipmentCatalog.Find("magic-robe").Detail,
                Is.EqualTo("属攻 +12%｜物防 +11%")
            );
            Assert.That(
                EquipmentCatalog.Find("flame-dagger").DetailAt(1f),
                Is.EqualTo("速度 +13%｜攻撃スキルが10%で2回発動")
            );
            foreach (var kind in EquipmentCatalog.All)
                Assert.That(kind.Effects, Is.Not.Empty, kind.Id);
        }
    }
}
