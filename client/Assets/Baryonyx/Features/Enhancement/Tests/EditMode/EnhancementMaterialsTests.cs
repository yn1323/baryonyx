using System.Collections.Generic;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Enhancement;
using Baryonyx.SkillEvolution;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class EnhancementMaterialsTests
    {
        private static EnhancementMaterial Stone(string id) => EnhancementMaterials.Find(id);

        // 上げ幅は倍率の幅に対する割合で、サーバー（enhancement/catalog.ts の polishStep）と同じく四捨五入する。
        [Test]
        public void StoneRaisesAShareOfTheRangeRoundedLikeTheServer()
        {
            // 上限×2.2なら、幅120の25%で30。
            Assert.That(
                EnhancementMaterials.Step(Stone(EnhancementMaterials.Purple), 2.2f),
                Is.EqualTo(30)
            );
            // 上限×1.5なら、幅50の5%で2.5を3に上げる。
            Assert.That(
                EnhancementMaterials.Step(Stone(EnhancementMaterials.White), 1.5f),
                Is.EqualTo(3)
            );
            Assert.That(
                EnhancementMaterials.Step(EnhancementMaterials.AwakeningShard, 2.2f),
                Is.Zero
            );
            // 上限を超える分は上限で止める。
            Assert.That(
                EnhancementMaterials.Polished(2f, Stone(EnhancementMaterials.Gold), 1, 2.2f),
                Is.EqualTo(2.2f).Within(0.001f)
            );
            // C（×1.00）からSまでは、金・紫・青・白を1つずつで幅の90%になる。
            float multiplier = 1f;
            foreach (var stone in EnhancementMaterials.Polishes)
                multiplier = EnhancementMaterials.Polished(multiplier, stone, 1, 2f);
            Assert.That(multiplier, Is.EqualTo(1.9f).Within(0.001f));
            Assert.That(Inventory.Quality.Of(multiplier, 2f), Is.EqualTo(Inventory.ItemQuality.S));
        }

        // 最後の1つが何も上げない数は、サーバーが断るため選ばせない。
        [Test]
        public void UsefulCountStopsWhereTheLastStoneWouldRaiseNothing()
        {
            var gold = Stone(EnhancementMaterials.Gold);
            // 上限×2.15：1つで58上がり、2つで上限（115）に届く。
            Assert.That(EnhancementMaterials.UsefulCount(1f, gold, 2.15f), Is.EqualTo(2));
            Assert.That(EnhancementMaterials.UsefulCount(2.15f, gold, 2.15f), Is.Zero);
            Assert.That(
                EnhancementMaterials.UsefulCount(1f, Stone(EnhancementMaterials.White), 2f),
                Is.EqualTo(20)
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
            Assert.That(UniqueSkillTreeShape.Parent(2), Is.EqualTo(1));
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
            Assert.That(AdventureDrops.StarsOf(category, EnhancementMaterials.Gold), Is.EqualTo(4));
            var drop = new AdventureDrop("f2-0", category, EnhancementMaterials.Shard);
            Assert.That(drop.Quality, Is.Null);
            Assert.That(AdventureTexts.DropName(drop), Is.EqualTo("覚醒の欠片"));
            Assert.That(AdventureTexts.DropLine(drop), Does.Contain("強化素材"));
        }

        // サーバーの rollMaterials と同じく、浅い行き先の1周で白い磨き石が3〜4個落ちる（doc/features/enhancement.md）。
        [Test]
        public void AShallowRunDropsThreeOrFourWhiteStones()
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
            Assert.That(
                counts[EnhancementMaterials.White] / (float)runs,
                Is.EqualTo(3.56f).Within(0.15f)
            );
            Assert.That(
                counts[EnhancementMaterials.Blue] / (float)runs,
                Is.EqualTo(0.9f).Within(0.1f)
            );
            Assert.That(
                counts[EnhancementMaterials.Shard] / (float)runs,
                Is.EqualTo(0.24f).Within(0.05f)
            );
        }
    }
}
