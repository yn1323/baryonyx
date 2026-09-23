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
                DailyGoal = 5000,
                WeeklyDone = 2,
                WeeklyTarget = 3,
                Runes = 12480,
                DestinationName = "森の遺跡",
                DestinationFloor = "B3F",
                Today = new DateTime(2026, 9, 24),
            };

        [Test]
        public void InProgressShowsRemainingStepsAndPartialGauge()
        {
            var state = HomeViewState.From(Sample());

            Assert.That(state.DateText, Is.EqualTo("9月24日（木）"));
            Assert.That(state.ShowSteps, Is.True);
            Assert.That(state.StepsText, Is.EqualTo("3,820"));
            Assert.That(state.GoalText, Is.EqualTo("今日の目標 5,000"));
            Assert.That(state.FilledSegments, Is.EqualTo(15));
            Assert.That(state.DailyAchieved, Is.False);
            Assert.That(state.RemainingText, Is.EqualTo("あと 1,180 歩"));
            Assert.That(state.WeeklyText, Is.EqualTo("今週の目標 2 / 3 回"));
            Assert.That(state.ClaimText, Is.EqualTo("タップでルーンを取得"));
            Assert.That(state.RunesText, Is.EqualTo("12,480"));
            Assert.That(state.DestinationNameText, Is.EqualTo("森の遺跡"));
            Assert.That(state.DestinationFloorText, Is.EqualTo("B3F"));
        }

        [Test]
        public void ReachingTheGoalFillsTheGaugeAndMarksAchievement()
        {
            var state = HomeViewState.From(Sample(6240));

            Assert.That(state.FilledSegments, Is.EqualTo(HomeViewState.GaugeSegments));
            Assert.That(state.DailyAchieved, Is.True);
            Assert.That(state.RemainingText, Is.EqualTo("今日の目標 達成！"));
        }

        [Test]
        public void UnlinkedHidesStepsAndAsksToLink()
        {
            var state = HomeViewState.From(Sample(6240, HomeStepLink.Unlinked));

            Assert.That(state.ShowSteps, Is.False);
            Assert.That(state.DailyAchieved, Is.False);
            Assert.That(state.ClaimText, Is.EqualTo("タップして歩数を連携"));
            Assert.That(
                HomeViewState.MessageFor(HomeAction.ClaimRunes, HomeStepLink.Unlinked),
                Is.EqualTo("歩数の連携（準備中）")
            );
        }

        [TestCase(0, 5000, 0)]
        [TestCase(-10, 5000, 0)]
        [TestCase(249, 5000, 0)]
        [TestCase(250, 5000, 1)]
        [TestCase(4999, 5000, 19)]
        [TestCase(99999, 5000, 20)]
        [TestCase(3000, 0, 0)]
        public void GaugeSegmentsFollowTheDailyGoal(int steps, int goal, int expected)
        {
            Assert.That(HomeViewState.FilledSegmentsFor(steps, goal), Is.EqualTo(expected));
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

        [TestCase(1920f, 1f)]
        [TestCase(2400f, 1f)]
        [TestCase(1440f, 0.75f)]
        [TestCase(0f, 1f)]
        public void WorldShrinksOnlyOnNarrowScreens(float width, float expected)
        {
            Assert.That(HomeWorldFit.ScaleFor(width, 1920f), Is.EqualTo(expected).Within(0.0001f));
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
                presenter.Handle(HomeAction.Party);

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
