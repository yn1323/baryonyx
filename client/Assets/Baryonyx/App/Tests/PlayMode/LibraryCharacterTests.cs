using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.App;
using Baryonyx.Library;
using Baryonyx.UI.GuideMenu;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class LibraryCharacterTests
    {
        // 見つけた物をすべて返すライブラリのサーバー。
        private sealed class AllFound : ILibrarySource
        {
            public LibraryRecords Records = LibraryRecords.Empty;

            public string Owner => "library-character-test";

            public Task<LibraryRecords> LoadAsync(CancellationToken token) =>
                Task.FromResult(Records);
        }

        private TestGameServices services;
        private AllFound library;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            LibrarySession.Reset();
            library = new AllFound();
            LibrarySession.Source = library;
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            LibrarySession.Reset();
            yield return SceneTests.UnloadAll(nameof(LibraryCharacterTests));
        }

        private static int ItemOf(GuideMenuView view, LibraryKind kind) =>
            Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == LibraryKinds.GuideItemKey(kind)
            );

        // キャラクターの右の面は、固有スキルを3枚のカードと覚醒の進み具合で、パッシブを専用の行で出し、
        // 2つを見出しで分ける。固有スキルを文字で並べた欄と、説明のパッシブの名前は出さない。
        [UnityTest]
        public IEnumerator ACharacterShowsTheUniqueSkillsAsCardsAndThePassivesApart()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Library, value => guide = value);
            var view = guide.View;
            int characters = ItemOf(view, LibraryKind.Character);
            var catalog = view.PanelFor(characters).GetComponent<LibraryCatalogView>();
            library.Records = new LibraryRecords(
                catalog.Tiles.Select(tile => (LibraryKind.Character, tile.Id, 0)),
                Array.Empty<LibraryStepDay>()
            );
            view.MenuItems[characters].onClick.Invoke();
            yield return SceneTests.WaitForTask(catalog.LoadTask);

            string id = catalog.Tiles[0].Id;
            catalog.Select(id);
            var character = catalog.Training.Find(id);
            Assert.That(catalog.CharacterSkills.activeInHierarchy, Is.True);
            Assert.That(catalog.DetailExtra.text, Is.Empty);
            Assert.That(catalog.DetailBody.text, Is.Empty);
            Assert.That(catalog.UniqueCards, Has.Length.EqualTo(3));
            Assert.That(catalog.CharacterScroll.vertical, Is.True);
            for (int i = 0; i < character.Uniques.Length && i < 3; i++)
            {
                Assert.That(catalog.UniqueCards[i].Name.text, Is.Not.Empty);
                // 今セットしている段階のカードを、効果の文まで出す（入りきらない分はスクロールする）。
                Assert.That(catalog.UniqueCards[i].Description.text, Is.Not.Empty);
                StringAssert.StartsWith("覚醒 ", catalog.UniqueSteps[i].text);
            }
            for (int i = 0; i < character.Passives.Length; i++)
            {
                var row = catalog.PassiveRows[i];
                Assert.That(row.Name.text, Is.EqualTo(character.Passives[i].Name));
                Assert.That(row.Description.text, Is.EqualTo(character.Passives[i].Description));
                Assert.That(
                    row.When.text,
                    Is.EqualTo(LibraryCatalogView.UnlockText(character.Passives[i].UnlockLevel))
                );
            }
        }

        // 装備の左の分類は、武器・防具・アクセサリーのどれも押せ、アクセサリーの一覧を開ける。
        [UnityTest]
        public IEnumerator TheArmorAndAccessoryTabsOpen()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Library, value => guide = value);
            var view = guide.View;
            int weapons = ItemOf(view, LibraryKind.Weapon);
            int accessories = ItemOf(view, LibraryKind.Accessory);
            Assert.That(accessories, Is.GreaterThanOrEqualTo(0));
            var weapon = view.PanelFor(weapons).GetComponent<LibraryCatalogView>();
            view.MenuItems[weapons].onClick.Invoke();
            yield return SceneTests.WaitForTask(weapon.LoadTask);
            Assert.That(weapon.KindTabs, Has.Length.EqualTo(3));
            Assert.That(weapon.KindTabs.All(tab => tab.Lock == null), Is.True);

            weapon.KindTabs[2].Button.onClick.Invoke();
            var accessory = view.PanelFor(accessories).GetComponent<LibraryCatalogView>();
            yield return SceneTests.WaitForTask(accessory.LoadTask);
            Assert.That(accessory.gameObject.activeInHierarchy, Is.True);
            Assert.That(accessory.Kind, Is.EqualTo(LibraryKind.Accessory));
            Assert.That(accessory.Tiles, Is.Not.Empty);
            Assert.That(accessory.KindTabs[2].Chosen, Is.True);
        }
    }
}
