using System.Linq;
using System.Text.RegularExpressions;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.SkillEvolution;
using Baryonyx.UI.Cards;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 品質の決まり（doc/features/inventory.md の品質）。サーバー（server/src/shared/quality.test.ts）と同じ区切りになる。
    public sealed class QualityTests
    {
        [TestCase(1f, ItemQuality.C)]
        [TestCase(1.19f, ItemQuality.C)]
        [TestCase(1.2f, ItemQuality.B)]
        [TestCase(1.34f, ItemQuality.B)]
        [TestCase(1.35f, ItemQuality.A)]
        [TestCase(1.44f, ItemQuality.A)]
        [TestCase(1.45f, ItemQuality.S)]
        [TestCase(1.5f, ItemQuality.S)]
        public void TheQualityIsWhereTheMultiplierFallsInTheRange(
            float multiplier,
            ItemQuality quality
        ) => Assert.That(Quality.Of(multiplier, 1.5f), Is.EqualTo(quality));

        [Test]
        public void AMultiplierRightOnABorderGoesToTheHigherQuality()
        {
            // ×1.3 の上限では、A の下端は 100 + 30 × 0.7 = 121。
            Assert.That(Quality.Of(1.21f, 1.3f), Is.EqualTo(ItemQuality.A));
            Assert.That(Quality.Of(1.2f, 1.3f), Is.EqualTo(ItemQuality.B));
        }

        [Test]
        public void TheRangesOfTheQualitiesMeetWithoutGaps()
        {
            foreach (var max in new[] { 1.1f, 1.3f, 1.5f, 2f })
            {
                int next = 100;
                foreach (var quality in Quality.All)
                {
                    var (low, high) = Quality.Range(quality, max);
                    Assert.That(Quality.Percent(low), Is.EqualTo(next), $"{quality} up to {max}");
                    for (
                        int percent = Quality.Percent(low);
                        percent <= Quality.Percent(high);
                        percent++
                    )
                        Assert.That(Quality.Of(percent / 100f, max), Is.EqualTo(quality));
                    Assert.That(Quality.Of(Quality.Middle(quality, max), max), Is.EqualTo(quality));
                    next = Quality.Percent(high) + 1;
                }
                Assert.That(next - 1, Is.EqualTo(Quality.Percent(max)));
            }
        }

        [Test]
        public void EveryNumberGrowsTheWayThatHelpsItsOwner()
        {
            Assert.That(Quality.Amount(100, 1.37f), Is.EqualTo(137));
            Assert.That(Quality.Count(1, 1.5f), Is.EqualTo(2));
            Assert.That(Quality.Count(2, 1.4f), Is.EqualTo(3));
            Assert.That(Quality.Count(2, 1.2f), Is.EqualTo(2));
            Assert.That(Quality.Chance(80, 1.5f), Is.EqualTo(100));
            // コストは倍率の伸び1.0ごとに1下げる（四捨五入）。大技は上限でも大技のまま。
            Assert.That(Quality.Cost(4, 1.4f), Is.EqualTo(4));
            Assert.That(Quality.Cost(4, 1.5f), Is.EqualTo(3));
            Assert.That(Quality.Cost(9, 2.4f), Is.EqualTo(8));
            Assert.That(Quality.Cost(9, 3f), Is.EqualTo(7));
            Assert.That(Quality.Cost(2, 3f), Is.EqualTo(1));
            Assert.That(Quality.Cost(1, 1.5f), Is.EqualTo(1));
            Assert.That(Quality.Cost(0, 3f), Is.EqualTo(0));
            Assert.That(Quality.Adverse(10, 1.5f), Is.EqualTo(7));
            Assert.That(Quality.Times(1.3f), Is.EqualTo("×1.30"));
        }

        [Test]
        public void TheServersLettersAreRead()
        {
            Assert.That(Quality.TryParse("S", out var s), Is.True);
            Assert.That(s, Is.EqualTo(ItemQuality.S));
            Assert.That(Quality.TryParse("E", out _), Is.False);
            Assert.That(Quality.TryParse(null, out _), Is.False);
        }

        [Test]
        public void AFireAtTheLimitGrowsItsPowerTurnsAndHitsAndCostsLess()
        {
            var fire = CardSkills.Find("Fire");
            Assert.That(fire.Quality, Is.EqualTo(ItemQuality.C));
            Assert.That(fire.WithMultiplier(1f), Is.SameAs(fire));

            Assert.That(CardSkillQuality.LimitOf(fire), Is.EqualTo(2.4f));
            var best = fire.WithMultiplier(2.4f);
            Assert.That(best.Quality, Is.EqualTo(ItemQuality.S));
            Assert.That(best.Base, Is.SameAs(fire));
            Assert.That(best.Cost, Is.EqualTo(1));
            Assert.That(best.Actions[0].Percent, Is.EqualTo(240));
            Assert.That(best.Actions[0].Hits, Is.EqualTo(2));
            Assert.That(best.Actions[1].Turns, Is.EqualTo(5));
            Assert.That(
                best.Text,
                Is.EqualTo("敵1体に{0}ダメージ×2。[弱点]を突くと[やけど]5ターン。")
            );
            Assert.That(CardRules.Describe(best), Does.Contain("属攻の240%ダメージ×2"));
            // The definition itself stays at ×1.00.
            Assert.That(fire.Actions[0].Percent, Is.EqualTo(100));
        }

        [Test]
        public void ACardThatHitsMoreThanOnceHasALowerLimitAndRewritesItsHits()
        {
            var serpent = CardSkills.Find("SerpentFang");
            Assert.That(CardSkillQuality.LimitOf(serpent), Is.EqualTo(1.6f));

            var best = serpent.WithMultiplier(1.6f);
            Assert.That(best.Quality, Is.EqualTo(ItemQuality.S));
            Assert.That(best.Actions[0].Hits, Is.EqualTo(3));
            Assert.That(best.Text, Does.StartWith("敵1体に{0}ダメージ×3。"));
            // A multiplier past the limit stops at it.
            Assert.That(serpent.WithMultiplier(2f).Multiplier, Is.EqualTo(1.6f));
        }

        [Test]
        public void AmountsAreRewrittenWhereTheirUnitFollows()
        {
            Assert.That(
                CardSkills.Find("Scout").WithMultiplier(1.5f).Text,
                Is.EqualTo("カードを3枚引く。")
            );
            Assert.That(
                CardSkills.Find("WarCry").WithMultiplier(1.5f).Text,
                Is.EqualTo("味方全体の攻撃力を3ターン38%上げる。")
            );
            Assert.That(
                CardSkills.Find("Resurrection").WithMultiplier(1.5f).Text,
                Is.EqualTo("味方1体のHPを最大の75%回復。倒れていれば復活。")
            );
        }

        // どのスキルも、倍率を掛けても説明文の数字の差し込み（{0}）は崩れず、×1.00では元のまま。
        [Test]
        public void EverySkillKeepsItsDescriptionWorkingAtAnyMultiplier()
        {
            var all = CardSkills
                .All.Concat(CardSkills.Prototypes)
                .Concat(UniqueSkillTrees.Stages().Select(stage => stage.Card));
            foreach (var skill in all)
            {
                Assert.That(skill.WithMultiplier(1f).Text, Is.EqualTo(skill.Text), skill.Id);
                foreach (var multiplier in new[] { 1.2f, 1.37f, 1.5f, 2.2f, 3f })
                {
                    var grown = skill.WithMultiplier(multiplier);
                    Assert.That(
                        Regex.Matches(grown.Text, "\\{[0-9]+\\}").Count,
                        Is.EqualTo(Regex.Matches(skill.Text, "\\{[0-9]+\\}").Count),
                        skill.Id
                    );
                    Assert.DoesNotThrow(() => CardRules.Describe(grown), skill.Id);
                    Assert.That(grown.Cost, Is.LessThanOrEqualTo(skill.Cost), skill.Id);
                }
            }
        }

        [Test]
        public void AnEquipmentsDescriptionGrowsWithItsMultiplier()
        {
            var dagger = EquipmentCatalog.Find("flame-dagger");
            Assert.That(dagger.MaxMultiplier, Is.EqualTo(1.8f));
            Assert.That(dagger.DetailAt(1f), Is.EqualTo(dagger.Detail));
            Assert.That(dagger.DetailAt(1.8f), Is.EqualTo("速度の 225%｜斬・炎｜4回攻撃"));
            Assert.That(
                EquipmentCatalog.Find("iron-helm").DetailAt(1.5f),
                Is.EqualTo("物防の 173%｜HP +60")
            );
            Assert.That(dagger.QualityOf(1.8f), Is.EqualTo(ItemQuality.S));
        }

        // スキル・固有スキル・装備は、種類ごとに×1.00より大きい上限を持つ。仮の×1.5を共通で使わない。
        [Test]
        public void EverySkillAndEquipmentHasALimitOfItsOwn()
        {
            var skills = CardSkills
                .All.Concat(CardSkills.Prototypes)
                .Concat(UniqueSkillTrees.Stages().Select(stage => stage.Card));
            foreach (var skill in skills)
                Assert.That(skill.MaxMultiplier, Is.GreaterThan(1f), skill.Id);
            Assert.That(
                CardSkills.All.Select(skill => skill.MaxMultiplier).Distinct().Count(),
                Is.GreaterThan(3),
                "Each kind has its own limit."
            );
            // 固有スキルは、進化しても同じ上限を使う。
            foreach (var tree in UniqueSkillTrees.All)
                Assert.That(
                    tree.Stages.Where(stage => stage != null)
                        .Select(stage => stage.Card.MaxMultiplier)
                        .Distinct()
                        .Count(),
                    Is.EqualTo(1),
                    tree.Id
                );
            foreach (var gear in EquipmentCatalog.All)
                Assert.That(gear.MaxMultiplier, Is.GreaterThan(1f), gear.Id);
        }

        // 枠はSが虹、Aが金で、B・Cは今の枠のまま。B・Cの頂点色は白（元の枠）になり、
        // 線形の色空間で値が変わっても見分けられるよう、金は虹と白から離れた値にする。
        [Test]
        public void TheFrameShinesByQuality()
        {
            Assert.That(
                CardFrameFoil.LookOf(ItemQuality.S),
                Is.EqualTo(CardFrameFoil.Look.Rainbow)
            );
            Assert.That(CardFrameFoil.LookOf(ItemQuality.A), Is.EqualTo(CardFrameFoil.Look.Gold));
            Assert.That(CardFrameFoil.LookOf(ItemQuality.B), Is.EqualTo(CardFrameFoil.Look.Plain));
            Assert.That(CardFrameFoil.LookOf(ItemQuality.C), Is.EqualTo(CardFrameFoil.Look.Plain));
            Assert.That(CardFrameFoil.RedOf(CardFrameFoil.Look.Plain), Is.EqualTo(1f));
            Assert.That(CardFrameFoil.RedOf(CardFrameFoil.Look.Rainbow), Is.EqualTo(0f));
            float gold = CardFrameFoil.RedOf(CardFrameFoil.Look.Gold);
            Assert.That(gold, Is.InRange(0.1f, 0.75f));
            Assert.That(Mathf.GammaToLinearSpace(gold), Is.InRange(0.1f, 0.75f));
        }

        // スキルが違えば光り方も違う。固有スキルは進化しても同じ光り方で、倍率を掛けても変わらない。
        [Test]
        public void EverySkillSparklesItsOwnWay()
        {
            var styles = CardSkills
                .All.Concat(CardSkills.Prototypes)
                .Select(skill => skill.FoilStyle)
                .Concat(UniqueSkillTrees.All.Select(tree => tree.Stages[0].Card.FoilStyle))
                .ToArray();
            Assert.That(styles, Is.Unique);
            Assert.That(styles, Has.All.InRange(0, CardFoil.StyleCount - 1));
            foreach (var tree in UniqueSkillTrees.All)
            foreach (var stage in tree.Stages.Where(stage => stage != null))
                Assert.That(stage.Card.FoilStyle, Is.EqualTo(tree.Stages[0].Card.FoilStyle));
            var fire = CardSkills.Find("Fire");
            Assert.That(fire.WithMultiplier(2f).FoilStyle, Is.EqualTo(fire.FoilStyle));
            // 隣り合うスキルは模様から違う。
            Assert.That(
                CardFoil.NameOf(CardSkills.All[0].FoilStyle),
                Is.Not.EqualTo(CardFoil.NameOf(CardSkills.All[1].FoilStyle))
            );
        }

        // 装備・アクセサリー・ACTボーナスも、それぞれの一覧の中で光り方が違う。
        [Test]
        public void EveryPieceOfEquipmentAndBonusSparklesItsOwnWay()
        {
            var gear = QualityLabEntry
                .Of(QualityLabTab.Weapons, null)
                .Concat(QualityLabEntry.Of(QualityLabTab.Armor, null))
                .Select(entry => entry.FoilStyle);
            Assert.That(gear, Is.Unique);
            // デバッグルームと編成の画面は、装備の同じ割り当てを使う。
            foreach (
                var entry in QualityLabEntry
                    .Of(QualityLabTab.Weapons, null)
                    .Concat(QualityLabEntry.Of(QualityLabTab.Armor, null))
            )
                Assert.That(entry.FoilStyle, Is.EqualTo(EquipmentCatalog.FoilStyleOf(entry.Id)));
            Assert.That(
                QualityLabEntry
                    .Of(QualityLabTab.Accessories, null)
                    .Select(entry => entry.FoilStyle),
                Is.Unique
            );
            var bonuses =
                UnityEngine.ScriptableObject.CreateInstance<Baryonyx.StepBonus.StepBonusMockData>();
            try
            {
                bonuses.Bonuses = Enumerable
                    .Range(0, 7)
                    .Select(i => new Baryonyx.StepBonus.StepBonusDefinition { Id = $"bonus-{i}" })
                    .ToArray();
                Assert.That(
                    QualityLabEntry
                        .Of(QualityLabTab.Bonuses, bonuses)
                        .Select(entry => entry.FoilStyle),
                    Is.Unique
                );
                // デバッグルームと酒場のボーナス設定は、ボーナスの同じ割り当てを使う。
                foreach (var entry in QualityLabEntry.Of(QualityLabTab.Bonuses, bonuses))
                    Assert.That(entry.FoilStyle, Is.EqualTo(bonuses.FoilStyleOf(entry.Id)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bonuses);
            }
        }

        // 装備の上限はサーバー（server/src/features/equipment/catalog.ts の maxPercent）と同じ。
        [Test]
        public void EquipmentLimitsMatchTheServer()
        {
            var path = System.IO.Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "..",
                "server",
                "src",
                "features",
                "equipment",
                "catalog.ts"
            );
            var source = System.IO.File.ReadAllText(path);
            var server = Regex
                .Matches(
                    source,
                    "\\{ id: \"([a-z-]+)\", slot: \"[a-z]+\", maxPercent: ([0-9]+) \\}"
                )
                .Cast<Match>()
                .ToDictionary(
                    match => match.Groups[1].Value,
                    match => int.Parse(match.Groups[2].Value)
                );
            Assert.That(server.Keys, Is.EquivalentTo(EquipmentCatalog.All.Select(gear => gear.Id)));
            foreach (var gear in EquipmentCatalog.All)
                Assert.That(
                    Quality.Percent(gear.MaxMultiplier),
                    Is.EqualTo(server[gear.Id]),
                    gear.Id
                );
        }
    }
}
