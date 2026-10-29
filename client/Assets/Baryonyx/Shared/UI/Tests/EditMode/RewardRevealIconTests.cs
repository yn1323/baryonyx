using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Inventory.Editor;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 手に入れた物の札（RewardReveal）のアイコン。スキルはカードの挿絵を、ほかの物より大きく3倍で出す。
    public sealed class RewardRevealIconTests
    {
        private Texture2D texture;

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(texture);

        private Sprite Picture(int width, int height)
        {
            texture = new Texture2D(width, height);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), Vector2.one * 0.5f);
        }

        [Test]
        public void ASquarePictureFillsTheBox()
        {
            Assert.That(
                RewardReveal.IconSizeOf(Picture(32, 32)),
                Is.EqualTo(Vector2.one * RewardReveal.IconBox)
            );
        }

        [Test]
        public void ASkillsCardArtIsShownLargerAtThreeTimesItsDots()
        {
            Assert.That(
                RewardReveal.IconSizeOf(Picture(64, 58)),
                Is.EqualTo(new Vector2(192f, 174f))
            );
        }

        [Test]
        public void EverySkillThatDropsShowsItsCardArt()
        {
            var art = ItemArtAssets.Load();
            var skills = AdventureDrops
                .Pools.Values.SelectMany(pool => pool)
                .Where(entry => entry.Category == AdventureDropCategory.Skill)
                .Select(entry => entry.Id)
                .Distinct()
                .ToList();
            Assert.That(skills, Is.Not.Empty);
            foreach (var id in skills)
            {
                var drop = new AdventureDrop("test", AdventureDropCategory.Skill, id, 1f, null);
                Assert.That(AdventureTexts.DropIcon(drop, art), Is.Not.Null, id);
            }
        }
    }
}
