using System.Collections;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 伏せたカードが、待ってから横（Vertical なら縦）に縮んで表へ替わり、元の幅で押せるようになる。
    public sealed class GlassCardBackPlayModeTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator AFaceDownCardWaitsThenTurnsFaceUp()
        {
            var back = MakeCard();
            back.ShowFaceDown();
            const float wait = 0.1f;

            back.FlipUp(wait);
            float started = Time.time;
            bool narrowed = false;
            bool faceDownUntilNarrow = true;
            while (back.Flipping && Time.time - started < 2f)
            {
                float width = back.Card.Body.localScale.x;
                if (width < 0.9f)
                    narrowed = true;
                if (!narrowed && !back.FaceDown)
                    faceDownUntilNarrow = false;
                yield return null;
            }

            Assert.That(back.Flipping, Is.False, "The flip did not end");
            Assert.That(
                Time.time - started,
                Is.GreaterThanOrEqualTo(wait + GlassCardBack.HalfSeconds * 2f - 0.02f)
            );
            Assert.That(narrowed, Is.True, "The card never narrowed");
            Assert.That(faceDownUntilNarrow, Is.True, "The face showed before turning");
            Assert.That(back.FaceDown, Is.False);
            Assert.That(back.Card.Body.localScale, Is.EqualTo(Vector3.one));
            Assert.That(back.Card.Group.blocksRaycasts, Is.True);
        }

        [UnityTest]
        public IEnumerator AVerticalCardTurnsUpAndDown()
        {
            var back = MakeCard();
            back.Vertical = true;
            back.ShowFaceDown();

            back.FlipUp();
            float started = Time.time;
            bool lowered = false;
            bool narrowed = false;
            while (back.Flipping && Time.time - started < 2f)
            {
                var scale = back.Card.Body.localScale;
                if (scale.y < 0.9f)
                    lowered = true;
                if (scale.x < 1f)
                    narrowed = true;
                yield return null;
            }

            Assert.That(back.Flipping, Is.False, "The flip did not end");
            Assert.That(lowered, Is.True, "The card never lowered");
            Assert.That(narrowed, Is.False, "The card narrowed in width");
            Assert.That(back.FaceDown, Is.False);
            Assert.That(back.Card.Body.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator ACardHiddenWhileTurningShowsItsFace()
        {
            var back = MakeCard();
            back.ShowFaceDown();
            back.FlipUp();
            yield return null;

            back.gameObject.SetActive(false);

            Assert.That(back.Flipping, Is.False);
            Assert.That(back.FaceDown, Is.False);
            Assert.That(back.Card.Body.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator AnEmptyPlaceTurnsToAnEmptyFaceThenFadesAway()
        {
            var back = MakeCard();
            var contents = back.Card.Body.Find("Content").gameObject;
            back.ShowFaceDown();
            back.gameObject.SetActive(false);

            back.FlipAway();
            Assert.That(back.gameObject.activeSelf, Is.True);
            float started = Time.time;
            bool emptyFace = false;
            bool faded = false;
            while (back.gameObject.activeSelf && Time.time - started < 3f)
            {
                if (!back.FaceDown && !contents.activeSelf)
                    emptyFace = true;
                if (back.Card.Group.alpha < 0.5f)
                    faded = true;
                yield return null;
            }

            Assert.That(back.gameObject.activeSelf, Is.False, "The card did not hide");
            Assert.That(
                Time.time - started,
                Is.GreaterThanOrEqualTo(
                    GlassCardBack.HalfSeconds * 2f + GlassCardBack.FadeSeconds - 0.05f
                )
            );
            Assert.That(emptyFace, Is.True, "The card did not turn to an empty face");
            Assert.That(faded, Is.True, "The card did not fade");
            // 隠したあとは、次に出したときのために元の姿に戻す。
            Assert.That(back.Leaving, Is.False);
            Assert.That(contents.activeSelf, Is.True);
            Assert.That(back.Card.Group.alpha, Is.EqualTo(1f));
            Assert.That(back.Card.Body.localScale, Is.EqualTo(Vector3.one));
        }

        private GlassCardBack MakeCard()
        {
            root = new GameObject("Card", typeof(RectTransform));
            var card = root.AddComponent<GlassCard>();
            card.Group = root.AddComponent<CanvasGroup>();
            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(root.transform, false);
            card.Body = (RectTransform)body.transform;
            new GameObject("Content", typeof(RectTransform)).transform.SetParent(
                body.transform,
                false
            );
            var face = new GameObject("Back", typeof(RectTransform));
            face.transform.SetParent(body.transform, false);
            var back = root.AddComponent<GlassCardBack>();
            back.Card = card;
            back.Face = face;
            return back;
        }
    }
}
