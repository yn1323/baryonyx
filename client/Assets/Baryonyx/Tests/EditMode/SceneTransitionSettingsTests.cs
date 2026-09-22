using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class SceneTransitionSettingsTests
    {
        [Test]
        public void NormalizeKeepsDurationsUsable()
        {
            var settings = new SceneTransitionSettings
            {
                CoverDuration = 0f,
                RevealDuration = -1f,
            };

            settings.Normalize();

            Assert.That(settings.CoverDuration, Is.EqualTo(0.01f));
            Assert.That(settings.RevealDuration, Is.EqualTo(0.01f));
        }

        [Test]
        public void CloneCopiesEachSelectableOptionWithoutSharingColorState()
        {
            var settings = new SceneTransitionSettings
            {
                Type = SceneTransitionType.Shutter,
                WipeDirection = SceneTransitionWipeDirection.RightToLeft,
                ShutterAxis = SceneTransitionShutterAxis.Horizontal,
                CoverDuration = 0.4f,
                RevealDuration = 0.6f,
                Color = Color.magenta,
            };

            var clone = settings.Clone();
            clone.Type = SceneTransitionType.Wipe;
            clone.Color = Color.cyan;

            Assert.That(clone.WipeDirection, Is.EqualTo(settings.WipeDirection));
            Assert.That(clone.ShutterAxis, Is.EqualTo(settings.ShutterAxis));
            Assert.That(clone.CoverDuration, Is.EqualTo(settings.CoverDuration));
            Assert.That(clone.RevealDuration, Is.EqualTo(settings.RevealDuration));
            Assert.That(settings.Type, Is.EqualTo(SceneTransitionType.Shutter));
            Assert.That(settings.Color, Is.EqualTo(Color.magenta));
        }
    }
}
