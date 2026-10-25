using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // カスタムスキルの覚醒版（doc/features/skill-awakening.md）。
    public sealed class CardSkillAwakeningsTests
    {
        private static IEnumerable<CardSkill> Stages =>
            CardSkillAwakenings.All.SelectMany(awakening => awakening.Branches);

        [Test]
        public void EveryCardSkillAwakensIntoTwoStagesOfItsOwn()
        {
            Assert.That(
                CardSkillAwakenings.All.Select(awakening => awakening.Skill),
                Is.EqualTo(CardSkills.All)
            );
            foreach (var awakening in CardSkillAwakenings.All)
            {
                var skill = awakening.Skill;
                Assert.That(
                    awakening.Branches.Select(stage => stage.Id),
                    Is.EqualTo(new[] { skill.Id + ".1", skill.Id + ".2" })
                );
                foreach (var stage in awakening.Branches)
                {
                    Assert.That(stage.User, Is.EqualTo(skill.User), stage.Name);
                    Assert.That(stage.Element, Is.EqualTo(skill.Element), stage.Name);
                    Assert.That(stage.MaxMultiplier, Is.EqualTo(skill.MaxMultiplier), stage.Name);
                    Assert.That(stage.FoilStyle, Is.EqualTo(skill.FoilStyle), stage.Name);
                    Assert.That(
                        stage.Art,
                        Is.EqualTo("Card" + stage.Id.Replace(".", "")),
                        "Each stage has art of its own."
                    );
                    Assert.That(
                        System.IO.File.Exists(
                            $"Assets/Baryonyx/Features/Combat/UI/Art/{stage.Art}.aseprite"
                        ),
                        Is.True,
                        stage.Art
                    );
                    Assert.That(
                        stage.Name,
                        Does.StartWith(skill.Name + "・"),
                        "The name grows from the card skill's."
                    );
                }
            }
            var names = CardSkills.All.Concat(Stages).Select(card => card.Name);
            Assert.That(names, Is.Unique);
        }

        [Test]
        public void NoStageDoesTheSameAsAnotherCard()
        {
            // What a card does: the element and who it is played on, how it moves between the
            // piles (its keywords, a fusion or a combo), and each action's kind, reach, status,
            // hits, conditions and when it works.
            static string Signature(CardSkill card) =>
                $"{card.Element}/{card.Target}/{card.Traits}/{card.FuseWith}/{card.ComboWith}:"
                + string.Join(
                    ";",
                    card.Actions.Select(action =>
                        $"{action.Kind},{action.Target},{action.Status},{action.Hits},"
                        + $"{action.When},{action.BonusWhen},{action.ActStepPercent > 0},"
                        + $"{action.Phase},{action.BlockPercent > 0},{action.PerDiscarded},"
                        + $"{action.MakesFree},"
                        + string.Join("+", action.Elements)
                    )
                );
            var seen = new Dictionary<string, string>();
            foreach (var card in CardSkills.All.Concat(Stages))
            {
                var signature = Signature(card);
                Assert.That(
                    seen.TryGetValue(signature, out var other),
                    Is.False,
                    $"{card.Name} does the same as {other}."
                );
                seen[signature] = card.Name;
            }
        }

        [Test]
        public void EveryStageCanBePaidForAndReadsWell()
        {
            foreach (var stage in Stages)
            {
                Assert.That(stage.Cost, Is.InRange(0, 9), stage.Name);
                Assert.That(stage.Actions, Is.Not.Empty, stage.Name);
                int powered = stage.Powered().Count();
                for (int i = 0; i < powered; i++)
                    StringAssert.Contains("{" + i + "}", stage.Text, stage.Name);
                StringAssert.DoesNotContain("{" + powered + "}", stage.Text, stage.Name);
                Assert.DoesNotThrow(() => CardRules.Describe(stage), stage.Name);
                foreach (var name in CardKeywords.Names(stage.Text))
                    Assert.That(CardKeywords.Find(name), Is.Not.Null, $"{stage.Name}: [{name}]");
            }
        }

        [Test]
        public void AStageIsFoundByItsIdAndAwakensNoFurther()
        {
            var fire = CardSkillAwakenings.Of("Fire");
            Assert.That(fire.Skill, Is.SameAs(CardSkills.Find("Fire")));
            Assert.That(CardSkillAwakenings.Of("Fire.2"), Is.SameAs(fire));
            Assert.That(CardSkillAwakenings.Find("Fire.1"), Is.SameAs(fire.Branches[0]));
            Assert.That(CardSkillAwakenings.Find("Fire"), Is.Null, "A card skill is no stage.");
            Assert.That(CardSkillAwakenings.Find("Fire.3"), Is.Null);
            Assert.That(CardSkillAwakenings.NextStages("Fire"), Is.EqualTo(fire.Branches));
            Assert.That(CardSkillAwakenings.NextStages("Fire.1"), Is.Empty);
            Assert.That(CardSkillAwakenings.NextStages("ManaBurst"), Is.Empty, "A unique skill.");
            Assert.That(CardSkillAwakenings.NextStages(null), Is.Empty);
            Assert.That(CardSkillAwakenings.IsAwakened("Fire.1"), Is.True);
            Assert.That(CardSkillAwakenings.IsAwakened("Fire"), Is.False);
        }

        [Test]
        public void AStageKeepsTheMultiplierOfItsCardSkill()
        {
            var stage = CardSkillAwakenings.Find("Fire.1");
            var grown = stage.WithMultiplier(2f);
            Assert.That(grown.Multiplier, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(
                grown.Actions[0].Percent,
                Is.EqualTo(260),
                "Its power grows as the card skill's does."
            );
            Assert.That(
                grown.Quality,
                Is.EqualTo(CardSkills.Find("Fire").WithMultiplier(2f).Quality)
            );
        }
    }
}
