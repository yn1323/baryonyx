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
                SteppedFrameRate = -3,
            };

            settings.Normalize();

            Assert.That(settings.CoverDuration, Is.EqualTo(0.01f));
            Assert.That(settings.RevealDuration, Is.EqualTo(0.01f));
            Assert.That(settings.SteppedFrameRate, Is.EqualTo(0));
        }

        [TestCase(SceneTransitionType.Wipe)]
        [TestCase(SceneTransitionType.Shutter)]
        public void WipeAndShutterHoldEachStepUntilTheNextFrame(SceneTransitionType type)
        {
            var settings = new SceneTransitionSettings { Type = type, SteppedFrameRate = 10 };

            var firstStep = settings.EvaluateProgress(0.01f, 1f);

            Assert.That(firstStep, Is.GreaterThan(0f));
            Assert.That(settings.EvaluateProgress(0.09f, 1f), Is.EqualTo(firstStep));
            Assert.That(settings.EvaluateProgress(0.11f, 1f), Is.GreaterThan(firstStep));
            Assert.That(settings.EvaluateProgress(1f, 1f), Is.EqualTo(1f));
        }

        [Test]
        public void FadeAndZeroFrameRateStaySmooth()
        {
            var fade = new SceneTransitionSettings
            {
                Type = SceneTransitionType.Fade,
                SteppedFrameRate = 10,
            };
            var smoothWipe = new SceneTransitionSettings
            {
                Type = SceneTransitionType.Wipe,
                SteppedFrameRate = 0,
            };

            foreach (var settings in new[] { fade, smoothWipe })
            {
                Assert.That(
                    settings.EvaluateProgress(0.01f, 1f),
                    Is.LessThan(settings.EvaluateProgress(0.09f, 1f))
                );
            }
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
                SteppedFrameRate = 8,
                Color = Color.magenta,
            };

            var clone = settings.Clone();
            clone.Type = SceneTransitionType.Wipe;
            clone.Color = Color.cyan;

            Assert.That(clone.WipeDirection, Is.EqualTo(settings.WipeDirection));
            Assert.That(clone.ShutterAxis, Is.EqualTo(settings.ShutterAxis));
            Assert.That(clone.CoverDuration, Is.EqualTo(settings.CoverDuration));
            Assert.That(clone.RevealDuration, Is.EqualTo(settings.RevealDuration));
            Assert.That(clone.SteppedFrameRate, Is.EqualTo(settings.SteppedFrameRate));
            Assert.That(settings.Type, Is.EqualTo(SceneTransitionType.Shutter));
            Assert.That(settings.Color, Is.EqualTo(Color.magenta));
        }
    }
}
