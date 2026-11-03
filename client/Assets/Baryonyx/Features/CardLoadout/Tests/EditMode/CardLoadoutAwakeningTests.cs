using System.Linq;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 冒険者の覚醒のタブ（doc/features/screens/loadout.md#覚醒）：持っている段階だけ中身を見せ、覚醒できる段階を選べる。
    public sealed class CardLoadoutAwakeningTests
    {
        // まだ覚醒していないスキルの1枚は、どちらの枝へも覚醒できる。枝の中身は、別の1枚がその枝を取っていれば見せ、
        // まだなら見せない。覚醒する前の1枚は今の段階として見せる。
        [Test]
        public void ACardNotAwakenedOpensBothBranchesAndShowsOnlyTheBranchesHeld()
        {
            var card = new PartySkillCard("c1", "Fire", 1.2f);
            var other = new PartySkillCard("c2", "Fire", 1f, 2);
            var ice = new PartySkillCard("c3", "Ice", 1f, 1);

            var stages = CardLoadoutAwakening.BranchesOf(card, new[] { card, other, ice });

            Assert.That(stages.Select(stage => stage.Branch), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(stages[0].Card.Name, Is.EqualTo(CardSkills.Find("Fire").Name));
            Assert.That(stages[0].Card.Multiplier, Is.EqualTo(1.2f));
            Assert.That(
                stages.Select(stage => stage.Held),
                Is.EqualTo(new[] { true, false, true })
            );
            Assert.That(
                stages.Select(stage => stage.Set),
                Is.EqualTo(new[] { true, false, false })
            );
            Assert.That(
                stages.Select(stage => stage.Open),
                Is.EqualTo(new[] { false, true, true })
            );
            Assert.That(stages[1].Card.Id, Is.EqualTo("Fire.1"));
            Assert.That(stages[2].Card.Id, Is.EqualTo("Fire.2"));
        }

        // 覚醒した1枚は、その枝を今の段階として見せ、もう覚醒できない。
        [Test]
        public void AnAwakenedCardShowsItsBranchAndOpensNone()
        {
            var card = new PartySkillCard("c1", "Fire", 1f, 1);

            var stages = CardLoadoutAwakening.BranchesOf(card, new[] { card });

            Assert.That(
                stages.Select(stage => stage.Held),
                Is.EqualTo(new[] { true, true, false })
            );
            Assert.That(
                stages.Select(stage => stage.Set),
                Is.EqualTo(new[] { false, true, false })
            );
            Assert.That(stages.Any(stage => stage.Open), Is.False);
        }

        // 固有スキルは、取得していない段階のうち、1つ手前の段階を取得している物へ覚醒できる。未解放なら覚醒できない。
        [Test]
        public void AUniqueSkillAwakensIntoTheStagesAfterTheOnesAcquired()
        {
            var made = UniqueSkillTrees.Find("ManaBurst");
            var acquired = new[] { 0, 1 };
            var tree = Tree(acquired, locked: false);

            var open = Enumerable
                .Range(0, UniqueSkillTreeShape.Count)
                .Where(stage => CardLoadoutAwakening.CanAwakenTo(tree, made, stage))
                .ToArray();

            var expected = Enumerable
                .Range(1, UniqueSkillTreeShape.Count - 1)
                .Where(stage =>
                    !acquired.Contains(stage)
                    && acquired.Contains(UniqueSkillTreeShape.Parent(stage))
                )
                .ToArray();
            Assert.That(open, Is.EqualTo(expected));
            Assert.That(open, Does.Contain(2));
            Assert.That(
                Enumerable
                    .Range(0, UniqueSkillTreeShape.Count)
                    .Any(stage =>
                        CardLoadoutAwakening.CanAwakenTo(Tree(acquired, true), made, stage)
                    ),
                Is.False
            );
        }

        private static CardLoadoutTreeState Tree(int[] acquired, bool locked) =>
            new()
            {
                Locked = locked,
                Stages = Enumerable
                    .Range(0, UniqueSkillTreeShape.Count)
                    .Select(stage => new CardLoadoutStageState
                    {
                        Stage = stage,
                        Parent = UniqueSkillTreeShape.Parent(stage),
                        Acquired = acquired.Contains(stage),
                    })
                    .ToArray(),
            };
    }
}
