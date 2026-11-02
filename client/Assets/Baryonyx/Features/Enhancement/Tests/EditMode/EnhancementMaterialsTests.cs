using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.Enhancement;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class EnhancementMaterialsTests
    {
        // 1段階の上げ幅は倍率の幅の5%で、サーバー（enhancement/catalog.ts）と同じく四捨五入する。
        [Test]
        public void AStepRaisesAShareOfTheRangeRoundedLikeTheServer()
        {
            // 上限×2.2なら、幅120の5%で6。
            Assert.That(EnhancementMaterials.StepPercent(2.2f), Is.EqualTo(6));
            // 上限×1.5なら、幅50の5%で2.5を3に上げる。
            Assert.That(EnhancementMaterials.StepPercent(1.5f), Is.EqualTo(3));
            // 上限を超える分は上限で止める。
            Assert.That(
                EnhancementMaterials.Polished(2.18f, 1, 2.2f),
                Is.EqualTo(2.2f).Within(0.001f)
            );
            Assert.That(
                EnhancementMaterials.Polished(1f, 8, 2f),
                Is.EqualTo(1.4f).Within(0.001f)
            );
        }

        // 1段階に使う石は、段階を始める品質で C1・B2・A3・S6 個に増える。
        [Test]
        public void AStepTakesMoreStonesTheHigherTheQuality()
        {
            // 上限×2.0（1段階で5）：CからBの入口までの8段階は1個ずつ。
            Assert.That(EnhancementMaterials.StonesFor(1f, 8, 2f), Is.EqualTo(8));
            // Cの終わり（×1.35）から3段階：C1個・C1個のあと、Bに入って2個。
            Assert.That(EnhancementMaterials.StonesFor(1.3f, 3, 2f), Is.EqualTo(4));
            // ×1.00から上限まで：C8・B12・A12・S12で44個。
            Assert.That(EnhancementMaterials.UsefulSteps(1f, 2f), Is.EqualTo(20));
            Assert.That(EnhancementMaterials.StonesFor(1f, 20, 2f), Is.EqualTo(44));
            Assert.That(EnhancementMaterials.RunesFor(44), Is.EqualTo(4400));
            // 上限を超える段階は数えない。
            Assert.That(EnhancementMaterials.StonesFor(1.95f, 3, 2f), Is.EqualTo(6));
        }

        // 最後の1段階が何も上げない数は、サーバーが断るため選ばせない。
        [Test]
        public void UsefulStepsStopWhereTheLastStepWouldRaiseNothing()
        {
            // 上限×2.15：1段階で6上がり、×2.12からは1段階で上限に届く。
            Assert.That(EnhancementMaterials.UsefulSteps(2.12f, 2.15f), Is.EqualTo(1));
            Assert.That(EnhancementMaterials.UsefulSteps(2.15f, 2.15f), Is.Zero);
        }

        // サーバーなしの強化も、段階の数から石とルーンを数え、足りなければ断る。
        [Test]
        public async Task TheMockPolishesWithTheSameCost()
        {
            var source = new EnhancementMemorySource(
                new Dictionary<string, int> { [EnhancementMaterials.White] = 5 },
                1000,
                shownBalance: false
            );
            var target = EnhancementTargets.Skills(
                new[] { new Party.PartySkillCard("card-1", "Fire", 1.3f) }
            )[0];
            var outcome = await source.PolishAsync(target, 3, CancellationToken.None);
            Assert.That(
                outcome.Multiplier,
                Is.EqualTo(EnhancementMaterials.Polished(1.3f, 3, target.MaxMultiplier)).Within(0.001f)
            );
            int used = EnhancementMaterials.StonesFor(1.3f, 3, target.MaxMultiplier);
            Assert.That(outcome.Materials[EnhancementMaterials.White], Is.EqualTo(5 - used));
            Assert.That(outcome.BalanceAfter, Is.EqualTo(1000 - 100 * used));
            Assert.ThrowsAsync<System.InvalidOperationException>(() =>
                source.PolishAsync(target.WithMultiplier(outcome.Multiplier), 3, CancellationToken.None)
            );
        }

        [Test]
        public void AwakeningCostsMoreRunesForABranchSecondStep()
        {
            foreach (int stage in new[] { 1, 3, 5 })
                Assert.That(
                    EnhancementMaterials.AwakeningRunes(stage),
                    Is.EqualTo(EnhancementMaterials.FirstStageRunes)
                );
            foreach (int stage in new[] { 2, 4, 6 })
                Assert.That(
                    EnhancementMaterials.AwakeningRunes(stage),
                    Is.EqualTo(EnhancementMaterials.SecondStageRunes)
                );
        }

        // 冒険で落ちた強化素材は、分類 material として読み、☆と名前を素材の定義から出す。
        [Test]
        public void DroppedMaterialsReadAsTheirOwnCategory()
        {
            Assert.That(AdventureDrops.TryParse("material", out var category), Is.True);
            Assert.That(category, Is.EqualTo(AdventureDropCategory.Material));
            Assert.That(
                AdventureDrops.KeyOf(AdventureDropCategory.Material),
                Is.EqualTo("material")
            );
            Assert.That(AdventureDrops.StarsOf(category, EnhancementMaterials.Shard), Is.EqualTo(4));
            var drop = new AdventureDrop("f2-0", category, EnhancementMaterials.Shard);
            Assert.That(drop.Quality, Is.Null);
            Assert.That(AdventureTexts.DropName(drop), Is.EqualTo("覚醒の欠片"));
            Assert.That(AdventureTexts.DropLine(drop), Does.Contain("強化素材"));
        }

        // サーバーの rollMaterials と同じく、浅い行き先の1周で磨き石が約3.6個落ちる（doc/features/enhancement.md）。
        [Test]
        public void AShallowRunDropsAboutFourStones()
        {
            var random = new System.Random(11);
            var rows = Enumerable
                .Repeat(AdventureRoomKind.Battle, 6)
                .Concat(Enumerable.Repeat(AdventureRoomKind.Elite, 3))
                .Append(AdventureRoomKind.Boss)
                .ToArray();
            const int runs = 4000;
            var counts = new Dictionary<string, int>();
            for (int run = 0; run < runs; run++)
                foreach (var row in rows)
                foreach (
                    var drop in AdventureDrops.RollMaterials(
                        AdventureCatalog.ForestRuins,
                        row,
                        "room",
                        random
                    )
                )
                {
                    Assert.That(drop.Category, Is.EqualTo(AdventureDropCategory.Material));
                    counts[drop.Id] = counts.GetValueOrDefault(drop.Id) + 1;
                }
            Assert.That(counts.Keys, Is.EquivalentTo(new[] { EnhancementMaterials.White, EnhancementMaterials.Shard }));
            Assert.That(
                counts[EnhancementMaterials.White] / (float)runs,
                Is.EqualTo(3.6f).Within(0.2f)
            );
            Assert.That(
                counts[EnhancementMaterials.Shard] / (float)runs,
                Is.EqualTo(0.24f).Within(0.05f)
            );
        }
    }
}
