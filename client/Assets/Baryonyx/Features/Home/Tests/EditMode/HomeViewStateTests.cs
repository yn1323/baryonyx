using System;
using Baryonyx.Home;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class HomeViewStateTests
    {
        private static HomeSnapshot Sample(
            int steps = 3820,
            HomeStepLink link = HomeStepLink.Linked
        ) =>
            new()
            {
                StepLink = link,
                Steps = steps,
                Runes = 12480,
                AdventureInProgress = true,
                DestinationName = "森の遺跡",
                DestinationFloor = "B3F",
                Today = new DateTime(2026, 9, 24),
            };

        [Test]
        public void InProgressShowsRemainingUptAndPartialGauge()
        {
            var state = HomeViewState.From(Sample());

            Assert.That(state.DateText, Is.EqualTo("9/24（木）"));
            Assert.That(state.ShowSteps, Is.True);
            Assert.That(state.UptText, Is.EqualTo("3,820"));
            Assert.That(state.FilledSegments, Is.EqualTo(9));
            Assert.That(state.DailyAchieved, Is.False);
            Assert.That(state.RemainingText, Is.EqualTo("あと 1,180 UPTで次のボーナス獲得"));
            Assert.That(state.ClaimText, Is.EqualTo("タップでルーン獲得"));
            Assert.That(state.ClaimPulses, Is.True);
            Assert.That(state.RunesText, Is.EqualTo("12,480"));
            Assert.That(state.DestinationNameText, Is.EqualTo("森の遺跡"));
            Assert.That(state.DestinationFloorText, Is.EqualTo("B3F"));
            Assert.That(state.ResumeText, Is.EqualTo("再開"));
        }

        // 冒険していないとき、右下のカードは行き先の代わりに旅の案内所を出す。
        [Test]
        public void WithNoAdventureTheCardOpensTheTravelOffice()
        {
            var snapshot = Sample();
            snapshot.AdventureInProgress = false;
            var state = HomeViewState.From(snapshot);

            Assert.That(state.AdventureInProgress, Is.False);
            Assert.That(state.DestinationNameText, Is.EqualTo(HomeViewState.TravelTitle));
            Assert.That(state.DestinationFloorText, Is.EqualTo(HomeViewState.TravelName));
            Assert.That(state.ResumeText, Is.EqualTo("出発"));
        }

        [Test]
        public void Reaching8000StepsFillsTheGaugeAndUnlocksEveryBonus()
        {
            var partial = HomeViewState.From(Sample(6240));
            Assert.That(partial.FilledSegments, Is.EqualTo(15));
            Assert.That(partial.DailyAchieved, Is.False);
            Assert.That(partial.RemainingText, Is.EqualTo("あと 1,760 UPTで次のボーナス獲得"));

            var state = HomeViewState.From(Sample(8000));
            Assert.That(state.FilledSegments, Is.EqualTo(HomeViewState.GaugeSegments));
            Assert.That(state.DailyAchieved, Is.True);
            Assert.That(state.RemainingText, Is.EqualTo("ボーナスをすべて獲得！"));
        }

        [TestCase(0, 1000)]
        [TestCase(999, 1000)]
        [TestCase(1000, 2000)]
        [TestCase(3820, 5000)]
        [TestCase(7999, 8000)]
        [TestCase(8000, 0)]
        public void NextBonusIsTheFirstLockedStage(int upt, int expected)
        {
            Assert.That(HomeViewState.NextBonusFor(upt), Is.EqualTo(expected));
        }

        [TestCase(-10, 0)]
        [TestCase(0, 0)]
        [TestCase(3820, 3820)]
        public void OneStepIsOneUpt(int steps, int expected)
        {
            Assert.That(HomeViewState.UptFor(steps), Is.EqualTo(expected));
        }

        [Test]
        public void UnknownRunesShowPlaceholder()
        {
            var snapshot = Sample();
            snapshot.RunesKnown = false;
            Assert.That(HomeViewState.From(snapshot).RunesText, Is.EqualTo("--"));
            Assert.That(HomeViewState.Runes(1234567L), Is.EqualTo("1,234,567"));
        }

        [TestCase(0L, 0)]
        [TestCase(1L, 1)]
        [TestCase(9L, 9)]
        [TestCase(10L, 10)]
        [TestCase(1340L, 26)]
        [TestCase(36896L, 34)]
        [TestCase(10000000L, 40)]
        public void MoreRunesFlyForLargerGains(long granted, int expected)
        {
            Assert.That(HomeView.ParticleCountFor(granted, 40), Is.EqualTo(expected));
        }

        [Test]
        public void LargerGainsBurstFarther()
        {
            float one = HomeView.BurstDistanceFor(1, 140f, 520f);
            float thousand = HomeView.BurstDistanceFor(1340, 140f, 520f);
            float huge = HomeView.BurstDistanceFor(10000000, 140f, 520f);
            Assert.That(one, Is.EqualTo(140f));
            Assert.That(thousand, Is.GreaterThan(one).And.LessThan(huge));
            Assert.That(huge, Is.EqualTo(520f));
        }

        [Test]
        public void UnknownStepsShowPlaceholderWithoutProgress()
        {
            var snapshot = Sample(8000);
            snapshot.StepsKnown = false;
            var state = HomeViewState.From(snapshot);

            Assert.That(state.UptText, Is.EqualTo("--"));
            Assert.That(state.FilledSegments, Is.Zero);
            Assert.That(state.DailyAchieved, Is.False);
            Assert.That(state.RemainingText, Is.Empty);
        }

        [Test]
        public void SyncingShowsProgressWithoutPulsing()
        {
            var snapshot = Sample();
            snapshot.StepSyncing = true;
            var state = HomeViewState.From(snapshot);

            Assert.That(state.ClaimText, Is.EqualTo("Loading..."));
            Assert.That(state.ClaimPulses, Is.False);
        }

        [Test]
        public void ClaimPulseStartsOpaqueAndDimsHalfwayThroughThePeriod()
        {
            Assert.That(HomeView.PulseAlpha(0f, 2.4f, 0.35f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(HomeView.PulseAlpha(1.2f, 2.4f, 0.35f), Is.EqualTo(0.35f).Within(1e-5f));
            Assert.That(HomeView.PulseAlpha(2.4f, 2.4f, 0.35f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(HomeView.PulseAlpha(0.6f, 2.4f, 0.35f), Is.InRange(0.35f, 1f));
        }

        [Test]
        public void UnlinkedHidesStepsAndAsksToLink()
        {
            var state = HomeViewState.From(Sample(8000, HomeStepLink.Unlinked));

            Assert.That(state.ShowSteps, Is.False);
            Assert.That(state.DailyAchieved, Is.False);
            Assert.That(state.ClaimText, Is.EqualTo("タップして歩数を連携"));
            Assert.That(
                HomeViewState.MessageFor(HomeAction.SyncSteps, HomeStepLink.Unlinked),
                Is.EqualTo("歩数の連携（準備中）")
            );
        }

        [TestCase(0, 0)]
        [TestCase(-10, 0)]
        [TestCase(399, 0)]
        [TestCase(400, 1)]
        [TestCase(7999, 19)]
        [TestCase(99999, 20)]
        public void GaugeFillsUpTo8000Upt(int upt, int expected)
        {
            Assert.That(
                HomeViewState.FilledSegmentsFor(upt, HomeViewState.GaugeMaxUpt),
                Is.EqualTo(expected)
            );
        }

        [Test]
        public void EveryActionHasFeedback()
        {
            foreach (HomeAction action in Enum.GetValues(typeof(HomeAction)))
                Assert.That(
                    HomeViewState.MessageFor(action, HomeStepLink.Linked),
                    Is.Not.Empty,
                    action.ToString()
                );
        }

        [Test]
        public void AdventureStartsOnlyOnceAndBlocksOtherActions()
        {
            var host = new GameObject("HomeViewTest");
            try
            {
                var view = host.AddComponent<HomeView>();
                int starts = 0;
                using var presenter = new HomePresenter(view, Sample(), () => ++starts > 0);

                presenter.Handle(HomeAction.Resume);
                presenter.Handle(HomeAction.Resume);
                presenter.Handle(HomeAction.Tavern);

                Assert.That(starts, Is.EqualTo(1));
                Assert.That(presenter.AdventureStarted, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FailedAdventureCanBeRetried()
        {
            var host = new GameObject("HomeViewTest");
            try
            {
                var view = host.AddComponent<HomeView>();
                int attempts = 0;
                using var presenter = new HomePresenter(view, Sample(), () => ++attempts > 1);

                presenter.Handle(HomeAction.Resume);
                Assert.That(presenter.AdventureStarted, Is.False);
                presenter.Handle(HomeAction.Resume);

                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(presenter.AdventureStarted, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
