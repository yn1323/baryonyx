using System.Collections.Generic;
using Baryonyx.Home;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class HomePresenterScreenTests
    {
        [Test]
        public void ScreenButtonsOpenTheirScreenOnceAndBlockOtherActions()
        {
            var host = new GameObject("HomeViewTest");
            try
            {
                var view = host.AddComponent<HomeView>();
                var opened = new List<HomeAction>();
                using var presenter = new HomePresenter(
                    view,
                    new HomeSnapshot(),
                    null,
                    openScreen: action =>
                    {
                        if (action == HomeAction.Settings)
                            return false;
                        opened.Add(action);
                        return true;
                    }
                );

                // 開く画面がない操作は、従来どおり準備中の通知になる。
                presenter.Handle(HomeAction.Settings);
                Assert.That(presenter.ScreenOpened, Is.False);

                presenter.Handle(HomeAction.Workshop);
                presenter.Handle(HomeAction.Tavern);
                presenter.Handle(HomeAction.TravelOffice);

                Assert.That(opened, Is.EqualTo(new[] { HomeAction.Workshop }));
                Assert.That(presenter.ScreenOpened, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ScreenThatCannotOpenFallsBackToTheToast()
        {
            var host = new GameObject("HomeViewTest");
            try
            {
                var view = host.AddComponent<HomeView>();
                using var presenter = new HomePresenter(
                    view,
                    new HomeSnapshot(),
                    null,
                    openScreen: _ => false
                );

                presenter.Handle(HomeAction.Temple);
                presenter.Handle(HomeAction.Tavern);

                Assert.That(presenter.ScreenOpened, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
