using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // スキルのデバッグルームの一覧の並びと、覚醒を入れたときに選べる段階。
    public sealed class CardSkillLabTests
    {
        private static readonly IReadOnlyList<CardSkill> All = CardSkillLab.Skills;

        private static int At(string id) =>
            Enumerable.Range(0, All.Count).Single(i => All[i].Id == id);

        private static CardSkill Find(string id) => All[At(id)];

        private static string[] Ids(IEnumerable<CardSkill> skills) =>
            skills.Select(skill => skill.Id).ToArray();

        [Test]
        public void EachThingIsListedOnItsTab()
        {
            Assert.That(CardSkillLab.TabOf(0), Is.Zero, "A card skill.");
            Assert.That(CardSkillLab.TabOf(At("ManaBurst")), Is.EqualTo(1));
            foreach (
                var stage in CardSkillAwakenings.All.SelectMany(awakening => awakening.Branches)
            )
                Assert.That(CardSkillLab.TabOf(At(stage.Id)), Is.EqualTo(2));
            Assert.That(CardSkillLab.TabOf(All.Count), Is.EqualTo(CardSkillLab.WindUpTab));
        }

        [Test]
        public void AFirstStageOffersItsBranchesFirstStepsAndAFirstStepItsLast()
        {
            Assert.That(
                Ids(CardSkillLab.EvolvesInto(Find("ManaBurst"))),
                Is.EqualTo(new[] { "ManaBurst.1", "ManaBurst.3", "ManaBurst.5" })
            );
            Assert.That(
                Ids(CardSkillLab.EvolvesInto(Find("ManaBurst.1"))),
                Is.EqualTo(new[] { "ManaBurst.2" })
            );
            Assert.That(CardSkillLab.EvolvesInto(Find("ManaBurst.2")), Is.Empty, "A last step.");
            Assert.That(CardSkillLab.EvolvesInto(All[0]), Is.Empty, "A card skill.");
        }
    }
}
