using System;
using System.Collections;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.App;
using Baryonyx.Library;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // ライブラリの入手先（doc/features/library.md の入手先）：解放したダンジョンの名前の札を出し、押すと
    // そのダンジョンに入るかを確かめる窓を開く。「やめる」で閉じ、「行く」でそのダンジョンの冒険を始める。
    public sealed class LibrarySourcesTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            LibrarySession.Reset();
            AdventureSession.Reset();
            // 武器は、ミストラ遺跡で落ちる短弓（☆1、レシピなし）と、まだ行けないしじまの沼でしか落ちない物を
            // 見つけている。テストの街道はブリーザ・ピーノまで進み、港町と氷窟のダンジョンまで開いている
            // （DungeonUnlocks）。
            string[] open = { AdventureCatalog.ForestRuins, "port-portoria", "glaciera-cave" };
            var closed = AdventureDrops
                .Pools["hush-mire"]
                .First(entry =>
                    entry.Category == AdventureDropCategory.Weapon
                    && open.All(id => AdventureDrops.Pools[id].All(other => other.Id != entry.Id))
                );
            LibrarySession.Source = new LibraryMemorySource(
                new LibraryRecords(
                    new[]
                    {
                        (LibraryKind.Weapon, "short-bow", 0),
                        (LibraryKind.Weapon, closed.Id, 0),
                    },
                    Array.Empty<LibraryStepDay>()
                )
            );
            closedWeapon = closed.Id;
        }

        private string closedWeapon;

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            LibrarySession.Reset();
            AdventureSession.Reset();
            yield return SceneTests.UnloadAll(nameof(LibrarySourcesTests));
        }

        [UnityTest]
        public IEnumerator TheSourcesPlaceAsksAndEntersTheDungeon()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Library, value => guide = value);
            var view = guide.View;
            int weapons = Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == LibraryKinds.GuideItemKey(LibraryKind.Weapon)
            );
            var catalog = view.PanelFor(weapons).GetComponent<LibraryCatalogView>();
            view.MenuItems[weapons].onClick.Invoke();
            yield return SceneTests.WaitForTask(catalog.LoadTask);

            // まだ行けないダンジョンでしか落ちない物には、入手先を出さない。
            catalog.Select(closedWeapon);
            Assert.That(catalog.Sources.gameObject.activeSelf, Is.False);

            catalog.Select("short-bow");
            Assert.That(catalog.Sources.gameObject.activeInHierarchy, Is.True);
            Assert.That(catalog.SourcePlaceName.text, Is.EqualTo("ミストラ遺跡"));
            Assert.That(catalog.SourceDestination, Is.EqualTo(AdventureCatalog.ForestRuins));
            SceneTests.AssertTouchSize(catalog.SourcePlace.transform);

            // 押すと、ダンジョンの名前を題にした窓で入るかを確かめる。「やめる」で閉じる。
            catalog.SourcePlace.onClick.Invoke();
            var dialog = catalog.Dialog;
            Assert.That(dialog.IsShown, Is.True);
            Assert.That(dialog.TitleText, Is.EqualTo("ミストラ遺跡"));
            Assert.That(dialog.BodyText, Is.EqualTo(DungeonEntry.Question));
            Assert.That(
                dialog.ChoiceLabels,
                Is.EqualTo(new[] { DungeonEntry.StayChoice, DungeonEntry.EnterChoice })
            );
            Assert.That(dialog.Choose(DungeonEntry.StayChoice), Is.True);
            Assert.That(dialog.IsShown, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Library));

            // 端末の戻るキーも、先に窓を閉じる。
            catalog.SourcePlace.onClick.Invoke();
            view.PressBack();
            Assert.That(dialog.IsShown, Is.False);
            Assert.That(view.PanelFor(weapons).activeSelf, Is.True);

            // 「行く」で、そのダンジョンの冒険を始めて探索へ移る。
            catalog.SourcePlace.onClick.Invoke();
            Assert.That(dialog.Choose(DungeonEntry.EnterChoice), Is.True);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Exploration,
                10f,
                "The exploration did not open."
            );
            Assert.That(
                AdventureSession.Current.Run.DestinationId,
                Is.EqualTo(AdventureCatalog.ForestRuins)
            );
        }
    }
}
