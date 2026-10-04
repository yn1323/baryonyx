using System;
using System.Collections.Generic;
using Baryonyx.UI.GuideMenu;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class GuideMenuPresenterTests
    {
        private sealed class FakeView : IGuideMenuView
        {
            public readonly List<GuideMenuState> Renders = new();
            public readonly List<string> Toasts = new();

            public event Action<int> ItemPressed;
            public event Action<int> EntryPressed;
            public event Action ConfirmPressed;
            public event Action BackPressed;

            public GuideMenuState Last => Renders[^1];

            public void Render(GuideMenuState state) => Renders.Add(state);

            public void ShowToast(string message) => Toasts.Add(message);

            public void PressItem(int index) => ItemPressed?.Invoke(index);

            public void PressEntry(int index) => EntryPressed?.Invoke(index);

            public void PressConfirm() => ConfirmPressed?.Invoke();

            public void PressBack() => BackPressed?.Invoke();
        }

        private GuideMenuDefinition definition;

        [SetUp]
        public void CreateDefinition()
        {
            definition = ScriptableObject.CreateInstance<GuideMenuDefinition>();
            definition.Items = new[]
            {
                new GuideMenuItem
                {
                    Label = "編成",
                    ConfirmLabel = "編成する",
                    Entries = new[]
                    {
                        new GuideListEntry { Name = "トーマ" },
                        new GuideListEntry { Name = "ルカ" },
                    },
                },
                new GuideMenuItem { Label = "育成", ConfirmLabel = "レベルアップ" },
            };
        }

        [TearDown]
        public void DestroyDefinition() => UnityEngine.Object.DestroyImmediate(definition);

        [Test]
        public void MenuOpensAFullListAndConfirmsTheChosenEntry()
        {
            var view = new FakeView();
            using var presenter = new GuideMenuPresenter(view, definition, () => true);
            Assert.That(view.Last.Page, Is.EqualTo(GuideMenuPage.Menu));

            view.PressItem(0);
            Assert.That(view.Last.Page, Is.EqualTo(GuideMenuPage.List));
            Assert.That(view.Last.Item, Is.EqualTo(0));
            Assert.That(view.Last.CanConfirm, Is.False);

            view.PressEntry(1);
            Assert.That(view.Last.Selected, Is.EqualTo(1));
            Assert.That(view.Last.CanConfirm, Is.True);

            view.PressConfirm();
            Assert.That(view.Toasts, Is.EqualTo(new[] { "編成する（準備中）" }));
        }

        [Test]
        public void ConfirmWithoutAChoiceDoesNothing()
        {
            var view = new FakeView();
            using var presenter = new GuideMenuPresenter(view, definition, () => true);
            view.PressItem(0);
            view.PressConfirm();
            view.PressEntry(5);
            view.PressConfirm();
            Assert.That(view.Toasts, Is.Empty);
        }

        [Test]
        public void EntriesCannotBeChosenFromTheMenuPage()
        {
            var view = new FakeView();
            using var presenter = new GuideMenuPresenter(view, definition, () => true);
            int renders = view.Renders.Count;
            view.PressEntry(0);
            Assert.That(view.Renders.Count, Is.EqualTo(renders));
        }

        [Test]
        public void BackClosesTheListFirstThenLeavesOnlyOnce()
        {
            var view = new FakeView();
            int leaves = 0;
            using var presenter = new GuideMenuPresenter(view, definition, () => ++leaves > 0);

            view.PressItem(0);
            view.PressEntry(0);
            view.PressBack();
            Assert.That(view.Last.Page, Is.EqualTo(GuideMenuPage.Menu));
            Assert.That(view.Last.Selected, Is.EqualTo(-1));
            Assert.That(leaves, Is.Zero);

            view.PressBack();
            view.PressBack();
            view.PressItem(0);
            Assert.That(leaves, Is.EqualTo(1));
            Assert.That(presenter.Left, Is.True);
            Assert.That(view.Last.Page, Is.EqualTo(GuideMenuPage.Menu));
        }

        [Test]
        public void FailedLeaveCanBeRetried()
        {
            var view = new FakeView();
            int tries = 0;
            using var presenter = new GuideMenuPresenter(view, definition, () => ++tries > 1);

            view.PressBack();
            Assert.That(presenter.Left, Is.False);
            Assert.That(view.Toasts, Is.EqualTo(new[] { "戻れませんでした" }));
            view.PressBack();
            Assert.That(presenter.Left, Is.True);
        }

        [Test]
        public void DestinationsChooseOnesThatAreNotLocked()
        {
            definition.Layout = GuideMenuLayout.Destinations;
            definition.DepartLabel = "出発";
            definition.Destinations = new[]
            {
                new GuideDestination { Name = "森の遺跡" },
                new GuideDestination { Name = "火山", Locked = true },
            };
            var view = new FakeView();
            using var presenter = new GuideMenuPresenter(view, definition, () => true);

            view.PressItem(0);
            Assert.That(view.Last.Page, Is.EqualTo(GuideMenuPage.Menu));

            view.PressEntry(1);
            Assert.That(view.Last.CanConfirm, Is.False);
            view.PressConfirm();
            Assert.That(view.Toasts, Is.Empty);

            view.PressEntry(0);
            Assert.That(view.Last.Selected, Is.EqualTo(0));
            view.PressConfirm();
            Assert.That(view.Toasts, Is.EqualTo(new[] { "出発（準備中）" }));
        }

        // 旅の案内所：出発できる行き先は呼び出し元が引き受け、引き受けない行き先は準備中と知らせる。
        [Test]
        public void DepartureGoesToTheCallerForDestinationsItTakes()
        {
            definition.Layout = GuideMenuLayout.Destinations;
            definition.DepartLabel = "出発";
            definition.Destinations = new[]
            {
                new GuideDestination { Name = "城下町" },
                new GuideDestination { Name = "森の遺跡", Id = "forest-ruins" },
            };
            var view = new FakeView();
            var departed = new System.Collections.Generic.List<int>();
            using var presenter = new GuideMenuPresenter(
                view,
                definition,
                () => true,
                index =>
                {
                    departed.Add(index);
                    return definition.Destinations[index].Id != "";
                }
            );

            view.PressEntry(1);
            view.PressConfirm();
            Assert.That(departed, Is.EqualTo(new[] { 1 }));
            Assert.That(view.Toasts, Is.Empty);

            view.PressEntry(0);
            view.PressConfirm();
            Assert.That(departed, Is.EqualTo(new[] { 1, 0 }));
            Assert.That(view.Toasts, Is.EqualTo(new[] { "出発（準備中）" }));
        }

        [Test]
        public void DisposedPresenterIgnoresInput()
        {
            var view = new FakeView();
            var presenter = new GuideMenuPresenter(view, definition, () => true);
            presenter.Dispose();
            int renders = view.Renders.Count;
            view.PressItem(0);
            view.PressBack();
            Assert.That(view.Renders.Count, Is.EqualTo(renders));
        }
    }
}
