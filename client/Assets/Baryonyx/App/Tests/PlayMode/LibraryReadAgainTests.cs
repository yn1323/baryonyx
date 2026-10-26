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
    public sealed class LibraryReadAgainTests
    {
        // 読み込みの答えを待たせ、失敗させられるライブラリのサーバー。読み始めたときの記録を返す。
        private sealed class FakeLibrary : ILibrarySource
        {
            public LibraryRecords Records = LibraryRecords.Empty;
            public TaskCompletionSource<bool> Gate;
            public bool Fail;

            public string Owner => "library-read-again-test";

            public async Task<LibraryRecords> LoadAsync(CancellationToken token)
            {
                var read = Records;
                bool fail = Fail;
                if (Gate != null)
                    await Gate.Task;
                token.ThrowIfCancellationRequested();
                if (fail)
                    throw new InvalidOperationException("The test library is offline.");
                return read;
            }
        }

        private TestGameServices services;
        private FakeLibrary library;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            LibrarySession.Reset();
            library = new FakeLibrary();
            LibrarySession.Source = library;
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            LibrarySession.Reset();
            yield return SceneTests.UnloadAll(nameof(LibraryReadAgainTests));
        }

        private static LibraryRecords Records(LibraryCatalogView catalog, int found, int days) =>
            new(
                catalog.Tiles.Take(found).Select(tile => (catalog.Kind, tile.Id, 0)),
                Enumerable
                    .Range(0, days)
                    .Select(day => new LibraryStepDay($"2026-10-{24 - day:00}", 1000 + day))
                    .ToArray()
            );

        // 初めて開くと読み終えるまで「読み込み中…」を出す。開き直すと、前に読んだ記録をすぐ出して裏で読み直し、
        // 変わっていれば描き直す。読み直せなかったときは、出している記録を残して通知の帯で知らせる。
        // 日々の歩数も、前に読んだ記録をすぐ出す。
        [UnityTest]
        public IEnumerator AReopenedPanelShowsTheRecordsReadBeforeWhileReadingThemAgain()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Library, value => guide = value);
            var view = guide.View;
            int weapons = Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == LibraryKinds.GuideItemKey(LibraryKind.Weapon)
            );
            int steps = Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == LibraryKinds.GuideItemKey(LibraryKind.Steps)
            );
            Assert.That(weapons, Is.GreaterThanOrEqualTo(0));
            Assert.That(steps, Is.GreaterThanOrEqualTo(0));
            var catalog = view.PanelFor(weapons).GetComponent<LibraryCatalogView>();
            var days = view.PanelFor(steps).GetComponent<LibraryStepsView>();
            Assert.That(catalog.Tiles.Length, Is.GreaterThanOrEqualTo(2));
            int total = catalog.Tiles.Length;

            // 初めて開いたときは、読み終えるまでどれを見つけたかを見せない。
            Assert.That(LibrarySession.Known, Is.Null);
            library.Records = Records(catalog, 1, 2);
            library.Gate = new TaskCompletionSource<bool>();
            view.MenuItems[weapons].onClick.Invoke();
            Assert.That(catalog.Status.text, Is.EqualTo(GuidePanel.LoadingText));
            Assert.That(
                catalog.Tiles.Any(tile => tile.Card != null && tile.Card.gameObject.activeSelf),
                Is.False
            );
            library.Gate.SetResult(true);
            yield return SceneTests.WaitForTask(catalog.LoadTask);
            Assert.That(catalog.Count.text, Is.EqualTo($"1/{total}"));
            // 右の面が空にならないよう、最初の見つけた物を選んでおく。
            var first = catalog.Tiles[0];
            Assert.That(catalog.Selected, Is.EqualTo(first.Id));
            Assert.That(first.Card.Selected, Is.True);
            Assert.That(catalog.Detail.activeSelf, Is.True);
            Assert.That(catalog.DetailName.text, Is.EqualTo(first.Name.text));

            // 開き直すと、読み直しを待たずに前の記録を出す。読み直して変わっていれば描き直す。
            view.Back.onClick.Invoke();
            library.Records = Records(catalog, 2, 3);
            library.Gate = new TaskCompletionSource<bool>();
            view.MenuItems[weapons].onClick.Invoke();
            Assert.That(catalog.Status.text, Is.Empty);
            Assert.That(
                catalog.Tiles.All(tile => tile.Card == null || tile.Card.gameObject.activeSelf),
                Is.True
            );
            Assert.That(catalog.Count.text, Is.EqualTo($"1/{total}"));
            library.Gate.SetResult(true);
            yield return SceneTests.WaitForTask(catalog.LoadTask);
            Assert.That(catalog.Count.text, Is.EqualTo($"2/{total}"));

            // 日々の歩数も、前に読んだ記録をすぐ出す。
            view.Back.onClick.Invoke();
            library.Gate = new TaskCompletionSource<bool>();
            view.MenuItems[steps].onClick.Invoke();
            Assert.That(days.RowCount, Is.EqualTo(3));
            Assert.That(days.Status.text, Is.Empty);
            library.Gate.SetResult(true);
            yield return SceneTests.WaitForTask(days.LoadTask);
            Assert.That(days.RowCount, Is.EqualTo(3));

            // 読み直せなかったときは、出している記録を残して知らせる。
            view.Back.onClick.Invoke();
            library.Gate = null;
            library.Fail = true;
            view.MenuItems[weapons].onClick.Invoke();
            yield return SceneTests.WaitUntil(() => catalog.LoadTask.IsCompleted);
            Assert.That(catalog.Status.text, Is.Not.EqualTo(GuidePanel.LoadFailedText));
            Assert.That(catalog.Count.text, Is.EqualTo($"2/{total}"));
            Assert.That(catalog.LastNotice, Is.EqualTo(LibraryCatalogView.LoadFailedMessage));
        }
    }
}
