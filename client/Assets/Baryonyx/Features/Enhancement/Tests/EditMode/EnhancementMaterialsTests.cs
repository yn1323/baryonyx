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
        }

        // 色ごとの上限：白はBの入口、青はAの入口、紫はSの入口、金は種類の上限で止まり、上の品質へ突破しない
        // （サーバーの polishCeilingPercent と同じ）。
        [Test]
        public void EachColourStopsAtTheEntranceOfItsCeilingQuality()
        {
            var white = Stone(EnhancementMaterials.White);
            var blue = Stone(EnhancementMaterials.Blue);
            var purple = Stone(EnhancementMaterials.Purple);
            var gold = Stone(EnhancementMaterials.Gold);
            Assert.That(EnhancementMaterials.CeilingPercent(white, 2f), Is.EqualTo(140));
            Assert.That(EnhancementMaterials.CeilingPercent(blue, 2f), Is.EqualTo(170));
            Assert.That(EnhancementMaterials.CeilingPercent(purple, 2f), Is.EqualTo(190));
            Assert.That(EnhancementMaterials.CeilingPercent(gold, 2f), Is.EqualTo(200));

            // 白はいくつ使ってもBの入口まで。そこからは青で上げる。
            float atB = EnhancementMaterials.Polished(1f, white, 20, 2f);
            Assert.That(atB, Is.EqualTo(1.4f).Within(0.001f));
            Assert.That(Inventory.Quality.Of(atB, 2f), Is.EqualTo(Inventory.ItemQuality.B));
            Assert.That(EnhancementMaterials.CanRaise(atB, white, 2f), Is.False);
            Assert.That(EnhancementMaterials.CanRaise(atB, blue, 2f), Is.True);
            // Aの入口の手前で青を使っても、入口で止まる（10%のうち5%だけ上がる）。
            Assert.That(
                EnhancementMaterials.Polished(1.65f, blue, 1, 2f),
                Is.EqualTo(1.7f).Within(0.001f)
            );
            // 上限より上にある物は、低い色で下がらない。
            Assert.That(
                EnhancementMaterials.Polished(1.8f, white, 1, 2f),
                Is.EqualTo(1.8f).Within(0.001f)
            );
            // 幅が割り切れないとき（上限×1.5、幅50）も、上限はBの入口（×1.20）でBに入る。
            float narrow = EnhancementMaterials.Polished(1f, white, 7, 1.5f);
            Assert.That(narrow, Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(Inventory.Quality.Of(narrow, 1.5f), Is.EqualTo(Inventory.ItemQuality.B));
        }

        // 合成：1つ下の色3個で上の色1個。白と覚醒の欠片は作れない。
        [Test]
        public void ThreeOfTheColourBelowMakeOneOfTheNext()
        {
            Assert.That(
                EnhancementMaterials.Combined.Select(stone => stone.Id),
                Is.EqualTo(
                    new[]
                    {
                        EnhancementMaterials.Blue,
                        EnhancementMaterials.Purple,
                        EnhancementMaterials.Gold,
                    }
                )
            );
            Assert.That(
                EnhancementMaterials.CombineFrom(Stone(EnhancementMaterials.Blue)).Id,
                Is.EqualTo(EnhancementMaterials.White)
            );
            Assert.That(
                EnhancementMaterials.CombineFrom(Stone(EnhancementMaterials.Gold)).Id,
                Is.EqualTo(EnhancementMaterials.Purple)
            );
            Assert.That(
                EnhancementMaterials.CombineFrom(Stone(EnhancementMaterials.White)),
                Is.Null
            );
            Assert.That(
                EnhancementMaterials.CombineFrom(EnhancementMaterials.AwakeningShard),
                Is.Null
            );
        }

        // 足りない分は、下の色から順に作る。白が足りなければ作れない。
        [Test]
        public void APlanMakesTheShortStonesFromTheColoursBelow()
        {
            var gold = Stone(EnhancementMaterials.Gold);
            var held = new Dictionary<string, int>
            {
                [EnhancementMaterials.White] = 8,
                [EnhancementMaterials.Blue] = 4,
                [EnhancementMaterials.Purple] = 2,
                [EnhancementMaterials.Gold] = 1,
            };
            int Held(string id) => held.GetValueOrDefault(id);
            Assert.That(EnhancementMaterials.CombinePlan(gold, 1, Held), Is.Empty);
            // 金をもう1個：紫3個が要り、足りない紫1個を青3個から作る。
            var plan = EnhancementMaterials.CombinePlan(gold, 2, Held);
            Assert.That(
                plan.Select(step => (step.Made.Id, step.Count, step.Uses)),
                Is.EqualTo(
                    new[] { (EnhancementMaterials.Purple, 1, 3), (EnhancementMaterials.Gold, 1, 3) }
                )
            );
            Assert.That(EnhancementMaterials.CombineRunesOf(plan), Is.EqualTo(300 + 900));
            // 金をもう2個：紫6個が要り、足りない紫4個に青12個、足りない青8個に白24個が要る。白は8個しかない。
            Assert.That(EnhancementMaterials.CombinePlan(gold, 3, Held), Is.Null);
            Assert.That(
                EnhancementMaterials.CombinePlan(Stone(EnhancementMaterials.White), 9, Held),
                Is.Null
            );
        }

        // 複数の色をまとめて使うときは、低い色から順に使う。どれかの石が何も上げなければ断る。
        // 直接使う下の色は、上の色を作る分に回さない。
        [Test]
        public void SeveralColoursAreUsedFromTheLowest()
        {
            float after = EnhancementMaterials.PolishedAll(
                1f,
                new[] { 2, 1, 0, 0 },
                2f,
                out bool allRaise
            );
            Assert.That(after, Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(allRaise, Is.True);
            EnhancementMaterials.PolishedAll(1f, new[] { 9, 0, 0, 0 }, 2f, out allRaise);
            Assert.That(allRaise, Is.False);

            var needs = new[] { 3, 2, 0, 0 };
            int WhiteOnly(int whites, string id) => id == EnhancementMaterials.White ? whites : 0;
            Assert.That(EnhancementMaterials.CombinePlan(needs, id => WhiteOnly(6, id)), Is.Null);
            var plan = EnhancementMaterials.CombinePlan(needs, id => WhiteOnly(9, id));
            Assert.That(
                plan.Select(step => (step.Made.Id, step.Count)),
                Is.EqualTo(new[] { (EnhancementMaterials.Blue, 2) })
            );
        }

        // サーバーなしの合成は、サーバーと同じく下の色3個とルーンを使い、足りなければ断る。
        [Test]
        public async Task TheMockCombinesWithTheSameCost()
        {
            var source = new EnhancementMemorySource(
                new Dictionary<string, int> { [EnhancementMaterials.White] = 7 },
                1000,
                shownBalance: false
            );
            var blue = Stone(EnhancementMaterials.Blue);
            var outcome = await source.CombineAsync(blue, 2, CancellationToken.None);
            Assert.That(outcome.Materials[EnhancementMaterials.White], Is.EqualTo(1));
            Assert.That(outcome.Materials[EnhancementMaterials.Blue], Is.EqualTo(2));
            Assert.That(outcome.BalanceAfter, Is.EqualTo(1000 - blue.CombineRunes * 2));
            Assert.ThrowsAsync<System.InvalidOperationException>(() =>
                source.CombineAsync(blue, 1, CancellationToken.None)
            );
        }

        // 最後の1つが何も上げない数は、サーバーが断るため選ばせない。
        [Test]
        public void UsefulCountStopsWhereTheLastStoneWouldRaiseNothing()
        {
            var gold = Stone(EnhancementMaterials.Gold);
            // 上限×2.15：1つで58上がり、2つで上限（115）に届く。
            Assert.That(EnhancementMaterials.UsefulCount(1f, gold, 2.15f), Is.EqualTo(2));
            Assert.That(EnhancementMaterials.UsefulCount(2.15f, gold, 2.15f), Is.Zero);
            // 白はBの入口（幅の40%）で止まるため、×1.00からは8つまで。
            Assert.That(
                EnhancementMaterials.UsefulCount(1f, Stone(EnhancementMaterials.White), 2f),
                Is.EqualTo(8)
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
            Assert.That(AdventureDrops.StarsOf(category, EnhancementMaterials.Gold), Is.EqualTo(4));
            var drop = new AdventureDrop("f2-0", category, EnhancementMaterials.Shard);
            Assert.That(drop.Quality, Is.Null);
            Assert.That(AdventureTexts.DropName(drop), Is.EqualTo("覚醒の欠片"));
            Assert.That(AdventureTexts.DropLine(drop), Does.Contain("強化素材"));
        }

        // サーバーの rollMaterials と同じく、浅い行き先の1周で磨き石が2つ弱落ち、ほとんどが白（doc/features/enhancement.md）。
        [Test]
        public void AShallowRunDropsFewMostlyWhiteStones()
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
                Is.EqualTo(1.32f).Within(0.1f)
            );
            Assert.That(
                counts[EnhancementMaterials.Blue] / (float)runs,
                Is.EqualTo(0.41f).Within(0.06f)
            );
            Assert.That(
                counts[EnhancementMaterials.Shard] / (float)runs,
                Is.EqualTo(0.24f).Within(0.05f)
            );
        }
    }
}
