using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Enhancement;
using Baryonyx.Party;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 持っているスキルの1枚の覚醒（doc/features/skill-awakening.md）：サーバーが返す枝、覚醒版として
    // 使うカード、覚醒させた後の持ち物、商会の「スキル覚醒」に並ぶ枝。
    public sealed class PartySkillAwakeningTests
    {
        // サーバーの応答の持っているスキル（覚醒していない1枚は awakenedBranch が null）。
        private const string Response =
            "{\"characters\":[{\"id\":\"toma\",\"level\":5,\"cards\":[\"card:a\",\"card:b\"]}],"
            + "\"skillCards\":["
            + "{\"id\":\"a\",\"skillId\":\"Fire\",\"multiplier\":1.5,\"awakenedBranch\":null},"
            + "{\"id\":\"b\",\"skillId\":\"Ice\",\"multiplier\":1.2,\"awakenedBranch\":2}],"
            + "\"runes\":0}";

        private static PartyState State() =>
            PartyServerSource.ToState(JsonUtility.FromJson<PartyApiClient.State>(Response));

        [Test]
        public void AnAwakenedCardPlaysAsItsStageWithItsMultiplier()
        {
            var state = State();
            var fire = state.SkillCardOf("card:a");
            var ice = state.SkillCardOf("card:b");
            Assert.That(fire.AwakenedBranch, Is.Zero);
            Assert.That(fire.StageId, Is.EqualTo("Fire"));
            Assert.That(ice.AwakenedBranch, Is.EqualTo(2));
            Assert.That(ice.StageId, Is.EqualTo("Ice.2"));

            var skill = state.SkillOf("card:b");
            Assert.That(skill.Id, Is.EqualTo("Ice.2"));
            Assert.That(skill.Name, Is.EqualTo("アイスランス・霜"));
            Assert.That(skill.Multiplier, Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(state.SkillIdOf("card:b"), Is.EqualTo("Ice.2"), "Its art is the stage's.");
            Assert.That(CardSkills.Find("Ice.2"), Is.Not.Null, "A stage is found by its id.");
        }

        [Test]
        public void ACardAwakensOnceAndKeepsWhatItBecame()
        {
            var state = State().WithAwakened("a", 1);
            Assert.That(state.SkillOf("card:a").Id, Is.EqualTo("Fire.1"));
            Assert.That(state.SkillCardOf("card:a").Multiplier, Is.EqualTo(1.5f).Within(1e-4f));
            var again = state.WithAwakened("a", 2).WithAwakened("b", 1);
            Assert.That(again.SkillOf("card:a").Id, Is.EqualTo("Fire.1"), "It never goes back.");
            Assert.That(again.SkillOf("card:b").Id, Is.EqualTo("Ice.2"));
        }

        [Test]
        public void TheTradingHouseOffersTheTwoBranchesOfEachCardNotAwakened()
        {
            var state = State();
            var holders = new Dictionary<string, string> { ["a"] = "トーマ" };
            var options = EnhancementTargets.SkillAwakenings(state.SkillCards, holders);
            Assert.That(
                options.Select(option => option.Card.Id),
                Is.EqualTo(new[] { "Fire.1", "Fire.2" })
            );
            foreach (var option in options)
            {
                Assert.That(option.IsSkillCard, Is.True);
                Assert.That(option.OwnedCard, Is.EqualTo("a"));
                Assert.That(option.From.Id, Is.EqualTo("Fire"));
                Assert.That(option.CharacterName, Is.EqualTo("トーマ"));
                Assert.That(option.Runes, Is.EqualTo(EnhancementMaterials.FirstStageRunes));
                Assert.That(option.Multiplier, Is.EqualTo(1.5f).Within(1e-4f));
            }
            Assert.That(options.Select(option => option.Branch), Is.EqualTo(new[] { 1, 2 }));
        }
    }
}
