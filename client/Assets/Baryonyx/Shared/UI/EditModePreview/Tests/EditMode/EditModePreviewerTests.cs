using System.Linq;
using Baryonyx.UI;
using Baryonyx.UI.EditModePreview.Editor;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 切り替えられる画面の例に、通知の帯（消えている帯を、止まった文の帯にする）を使う。
    public sealed class EditModePreviewerTests
    {
        private GameObject root;
        private NoticeBand band;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PreviewRoot", typeof(RectTransform));
            var bandObject = new GameObject("Band", typeof(RectTransform), typeof(CanvasGroup));
            bandObject.transform.SetParent(root.transform, false);
            band = bandObject.AddComponent<NoticeBand>();
            band.Group = bandObject.GetComponent<CanvasGroup>();
            band.Group.alpha = 0f;
            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(bandObject.transform, false);
            band.Label = label.AddComponent<TextMeshProUGUI>();
            band.Label.text = "";
        }

        [TearDown]
        public void TearDown()
        {
            EditModePreviewer.Restore(band);
            Object.DestroyImmediate(root);
        }

        private GameObject Copy() =>
            root
                .transform.Cast<Transform>()
                .Select(child => child.gameObject)
                .FirstOrDefault(child => child.name.EndsWith(EditModePreviewer.CopySuffix));

        [Test]
        public void ShowSwitchesAnUnsavedCopyAndLeavesTheScreenAsSaved()
        {
            EditModePreviewer.Show(band, 1);

            var copy = Copy();
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
            Assert.That(copy.transform.GetSiblingIndex(), Is.EqualTo(1));
            var shown = copy.GetComponent<NoticeBand>();
            Assert.That(shown.Group.alpha, Is.EqualTo(1f));
            Assert.That(shown.Label.text, Is.EqualTo(NoticeBand.LongSample));
            Assert.That(EditModePreviewer.StateOf(band), Is.EqualTo(1));

            // 元の画面は隠すだけで、中身は保存されている形のまま。
            Assert.That(band.gameObject.activeSelf, Is.False);
            Assert.That(band.Group.alpha, Is.EqualTo(0f));
            Assert.That(band.Label.text, Is.Empty);
        }

        [Test]
        public void RestoreRemovesTheCopyAndShowsTheScreenAgain()
        {
            EditModePreviewer.Show(band, 1);
            EditModePreviewer.Show(band, 0);
            Assert.That(root.transform.childCount, Is.EqualTo(2));
            Assert.That(
                Copy().GetComponent<NoticeBand>().Label.text,
                Is.EqualTo(NoticeBand.ShortSample)
            );

            EditModePreviewer.Restore(band);

            Assert.That(Copy(), Is.Null);
            Assert.That(root.transform.childCount, Is.EqualTo(1));
            Assert.That(band.gameObject.activeSelf, Is.True);
            Assert.That(EditModePreviewer.StateOf(band), Is.EqualTo(EditModePreviewer.None));
        }

        [Test]
        public void AHiddenScreenIsShownOnTheCopyAndHiddenAgainOnRestore()
        {
            band.gameObject.SetActive(false);

            EditModePreviewer.Show(band, 0);
            Assert.That(Copy().activeSelf, Is.True);

            EditModePreviewer.Restore(band);
            Assert.That(band.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void AScreenInsideAClosedPanelIsNotShown()
        {
            root.SetActive(false);

            Assert.That(EditModePreviewer.CanShow(band), Is.False);
            EditModePreviewer.Show(band, 1);

            Assert.That(root.transform.childCount, Is.EqualTo(1));
            Assert.That(EditModePreviewer.StateOf(band), Is.EqualTo(EditModePreviewer.None));
        }

        [Test]
        public void AStrayCopyLeftOverAnotherSceneIsDestroyed()
        {
            // 閉じたシーンの複製は、保存しない印のためにシーンの外へ残る。
            var stray = new GameObject("Screen" + EditModePreviewer.CopySuffix)
            {
                hideFlags = HideFlags.DontSave | HideFlags.NotEditable,
            };
            EditModePreviewer.Show(band, 1);

            EditModePreviewer.DestroyStrayCopies();

            Assert.That(stray == null, Is.True);
            Assert.That(Copy(), Is.Not.Null);
            Assert.That(EditModePreviewer.StateOf(band), Is.EqualTo(1));
        }

        [Test]
        public void TheCopyCannotBeSwitchedItself()
        {
            EditModePreviewer.Show(band, 1);
            var copy = Copy().GetComponent<NoticeBand>();

            Assert.That(EditModePreviewer.IsCopy(copy), Is.True);
            Assert.That(EditModePreviewer.CanShow(copy), Is.False);
        }
    }
}
