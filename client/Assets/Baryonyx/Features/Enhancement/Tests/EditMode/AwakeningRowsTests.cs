using System.Collections.Generic;
using System.Linq;
using Baryonyx.Enhancement;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Training;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 商会のスキル覚醒の一覧の並び（doc/features/enhancement.md#覚醒の画面）：人ごとに固有スキル、続けてその人の枠のカード。
    // そのあとに誰にも付けていないカードを品質の高い順に並べ、覚醒しきった物と覚醒したカードは出さない。
    public sealed class AwakeningRowsTests
    {
        private TrainingMockData training;

        [SetUp]
        public void MakeTraining()
        {
            UniqueSkillSession.Reset();
            training = ScriptableObject.CreateInstance<TrainingMockData>();
            training.Characters = new[]
            {
                new TrainingCharacter
                {
                    Id = "toma",
                    Uniques = new[]
                    {
                        new TrainingSkill { Tree = "ManaBurst", UnlockLevel = 1 },
                        new TrainingSkill { Tree = "ManaBurst", UnlockLevel = 50 },
                    },
                },
            };
        }

        [TearDown]
        public void DropTraining()
        {
            Object.DestroyImmediate(training);
            UniqueSkillSession.Reset();
        }

        [Test]
        public void ListsEachPersonsUniquesThenTheirCardsThenTheCardsNoOneHolds()
        {
            var toma = new PartyMember
            {
                Id = "toma",
                Name = "トーマ",
                Level = 10,
            };
            var luka = new PartyMember
            {
                Id = "luka",
                Name = "ルカ",
                Level = 10,
            };
            var cards = new[]
            {
                new PartySkillCard("spare-c", "Fire", 1f),
                new PartySkillCard("luka-1", "Thunder", 1f),
                new PartySkillCard("toma-1", "Fire", 1.2f),
                new PartySkillCard("awakened", "Embers", 1.5f, awakenedBranch: 1),
                new PartySkillCard("spare-best", "Heal", 9f),
                new PartySkillCard("toma-2", "Heal", 1f),
            };
            var slots = new Dictionary<string, string[]>
            {
                ["toma"] = new[] { "toma-1", "toma-2" },
                ["luka"] = new[] { "luka-1", "awakened" },
            };

            var rows = AwakeningRows.Of(
                new[] { toma, luka },
                training,
                (person, index) => UniqueSkillSession.ProgressOf(person, index, null),
                cards,
                person => slots.TryGetValue(person, out var ids) ? ids : new string[0]
            );

            // トーマの解放した固有スキル（Lv 50の2つ目は出さない）、トーマの枠の2枚、ルカの枠の1枚（覚醒した1枚は出さない）、
            // 誰にも付けていない2枚（品質の高い順）。
            Assert.That(
                rows.Select(row => row.IsSkillCard ? row.Options[0].OwnedCard : "unique"),
                Is.EqualTo(
                    new[] { "unique", "toma-1", "toma-2", "luka-1", "spare-best", "spare-c" }
                )
            );
            Assert.That(
                rows.Select(row => row.Character?.Id),
                Is.EqualTo(new[] { "toma", "toma", "toma", "luka", null, null })
            );
            // 固有スキルは最初の段階のカードで出し、3つの枝を覚醒先にする。スキルの1枚は2つの枝。
            Assert.That(rows[0].Card.Name, Is.EqualTo("マナバースト"));
            Assert.That(
                rows[0].Options.Select(option => option.Stage),
                Is.EqualTo(new[] { 1, 3, 5 })
            );
            Assert.That(rows[1].Options.Count, Is.EqualTo(2));
            Assert.That(rows[1].Multiplier, Is.EqualTo(1.2f).Within(0.0001f));
        }

        [Test]
        public void LeavesOutAUniqueSkillWithNothingLeftToAwaken()
        {
            var toma = new PartyMember
            {
                Id = "toma",
                Name = "トーマ",
                Level = 10,
            };
            var all = new UniqueSkillProgress
            {
                Acquired = Enumerable.Range(0, UniqueSkillTreeShape.Count).ToArray(),
            };

            var rows = AwakeningRows.Of(
                new[] { toma },
                training,
                (person, index) => all,
                new PartySkillCard[0],
                person => new string[0]
            );

            Assert.That(rows, Is.Empty);
        }
    }
}
