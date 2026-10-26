using System.Collections.Generic;
using System.Linq;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 一覧が初めて読む間にカードを伏せ、読み終えたら表示するカードからめくる（doc/features/screens/guide.md#カードの裏面）。
    public sealed class GlassCardBackTests
    {
        private readonly List<GameObject> made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in made)
                Object.DestroyImmediate(obj);
            made.Clear();
        }

        [Test]
        public void AFlipNarrowsToAnEdgeWithTheBackThenWidensWithTheFace()
        {
            float half = GlassCardBack.HalfSeconds;

            Assert.That(GlassCardBack.WidthAt(0f, out bool atStart), Is.EqualTo(1f));
            Assert.That(atStart, Is.True);
            float closing = GlassCardBack.WidthAt(half * 0.5f, out bool closingDown);
            float edge = GlassCardBack.WidthAt(half, out bool edgeDown);
            float opening = GlassCardBack.WidthAt(half * 1.5f, out bool openingDown);
            float done = GlassCardBack.WidthAt(half * 2f, out bool doneDown);

            Assert.That(closing, Is.LessThan(1f).And.GreaterThan(0f));
            Assert.That(closingDown, Is.True, "The back shows while closing");
            Assert.That(edge, Is.EqualTo(0f).Within(0.001f));
            Assert.That(edgeDown, Is.False, "The face takes over at the edge");
            Assert.That(opening, Is.LessThan(1f).And.GreaterThan(0f));
            Assert.That(openingDown, Is.False);
            Assert.That(done, Is.EqualTo(1f).Within(0.001f));
            Assert.That(doneDown, Is.False);
            // 真横を向くときに少し持ち上がり、表で元の大きさに戻る。
            Assert.That(GlassCardBack.LiftAt(0f), Is.EqualTo(1f));
            Assert.That(
                GlassCardBack.LiftAt(half),
                Is.EqualTo(1f + GlassCardBack.Lift).Within(0.001f)
            );
            Assert.That(GlassCardBack.LiftAt(half * 2f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void ACoverLaysTheCardsDownAndTakesNoTaps()
        {
            var cards = Cards(3);
            cards[2].gameObject.SetActive(false);
            var cover = new GlassCardCover();

            cover.Cover(cards.Select(card => card.gameObject));

            Assert.That(cover.Covered, Is.True);
            Assert.That(cards.All(card => card.gameObject.activeSelf), Is.True);
            Assert.That(cards.All(card => BackOf(card).FaceDown), Is.True);
            Assert.That(cards.All(card => !card.Group.blocksRaycasts), Is.True);
        }

        [Test]
        public void AFadedCardLiesOpaqueSoItsFaceDoesNotShowThrough()
        {
            var cards = Cards(1);
            var card = cards[0];
            card.Interactable = false;
            var cover = new GlassCardCover();

            cover.Cover(cards.Select(item => item.gameObject));
            Assert.That(card.Group.alpha, Is.EqualTo(1f));
            // 伏せている間に押せなくしても、裏は透かさない。
            card.Interactable = false;
            Assert.That(card.Group.alpha, Is.EqualTo(1f));

            cover.Clear();
            Assert.That(card.Group.alpha, Is.EqualTo(card.RestingAlpha));
            Assert.That(card.RestingAlpha, Is.LessThan(1f));
        }

        [Test]
        public void ARevealTurnsTheShownCardsInOrderAndTheRestAtOnce()
        {
            var cards = Cards(4);
            var cover = new GlassCardCover();
            cover.Cover(cards.Select(card => card.gameObject));
            cards[3].gameObject.SetActive(false);

            cover.Reveal(new[] { cards[1].gameObject, cards[0].gameObject });

            Assert.That(cover.Covered, Is.False);
            // 表示する2枚はめくり始め、伏せたまま最後に表になる。
            Assert.That(BackOf(cards[1]).Flipping, Is.True);
            Assert.That(BackOf(cards[0]).Flipping, Is.True);
            Assert.That(BackOf(cards[1]).FaceDown, Is.True);
            // 表示しないカードは、すぐ表に戻す（あとで出しても伏せたままにならない）。
            Assert.That(BackOf(cards[2]).FaceDown, Is.False);
            Assert.That(BackOf(cards[3]).FaceDown, Is.False);
            Assert.That(cards[2].Group.blocksRaycasts, Is.True);
        }

        [Test]
        public void TheEmptyPlacesTurnToo()
        {
            var cards = Cards(3);
            var cover = new GlassCardCover();
            cover.Cover(cards.Select(card => card.gameObject));
            // 一覧は、出す物のない場所のカードを隠してから知らせる。
            cards[1].gameObject.SetActive(false);
            cards[2].gameObject.SetActive(false);

            cover.Reveal(
                new[] { cards[0].gameObject },
                new[] { cards[1].gameObject, cards[2].gameObject }
            );

            Assert.That(BackOf(cards[0]).Leaving, Is.False);
            // 空の場所も伏せたまま並び、表示するカードと一緒にめくれ、めくり終えたら消えていく。
            Assert.That(cards.All(card => card.gameObject.activeSelf), Is.True);
            Assert.That(BackOf(cards[1]).Leaving && BackOf(cards[2]).Leaving, Is.True);
            Assert.That(BackOf(cards[1]).FaceDown && BackOf(cards[1]).Flipping, Is.True);
            Assert.That(cards[1].Group.blocksRaycasts, Is.False);
        }

        [Test]
        public void ShowingAFadingCardAgainStopsItsFading()
        {
            var cards = Cards(2);
            var cover = new GlassCardCover();
            cover.Cover(cards.Select(card => card.gameObject));
            cover.Reveal(new[] { cards[0].gameObject }, new[] { cards[1].gameObject });

            // 消えていく途中で、一覧がそのカードに別の物を出した（タブを替えたなど）。
            cover.Reveal(cards.Select(card => card.gameObject));

            Assert.That(BackOf(cards[1]).Leaving, Is.False);
            Assert.That(BackOf(cards[1]).FaceDown, Is.False);
            Assert.That(cards[1].Group.alpha, Is.EqualTo(1f));
            Assert.That(cards[1].Body.Find("Content").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void AListThatWasNotCoveredTurnsNothing()
        {
            var cards = Cards(2);
            var cover = new GlassCardCover();

            cover.Reveal(cards.Select(card => card.gameObject));

            Assert.That(
                cards.Any(card => BackOf(card).FaceDown || BackOf(card).Flipping),
                Is.False
            );
        }

        [Test]
        public void ClearingTurnsEveryCoveredCardFaceUp()
        {
            var cards = Cards(2);
            var cover = new GlassCardCover();
            cover.Cover(cards.Select(card => card.gameObject));

            cover.Clear();

            Assert.That(cover.Covered, Is.False);
            Assert.That(cards.Any(card => BackOf(card).FaceDown), Is.False);
        }

        [Test]
        public void OnlyWideCardsDrawTheLinesBesideTheOrnament()
        {
            Assert.That(GlassCardBackLine.IsWide(new Vector2(812, 140)), Is.True);
            Assert.That(GlassCardBackLine.IsWide(new Vector2(597, 291)), Is.True);
            Assert.That(GlassCardBackLine.IsWide(new Vector2(265, 300)), Is.False);
            Assert.That(GlassCardBackLine.IsWide(new Vector2(200, 200)), Is.False);
            // 低いカードでは高さが、細いカードでは幅が飾りの大きさを決める。
            Assert.That(
                GlassCardBackLine.EmblemDrawnWidth(new Vector2(812, 140), 1f),
                Is.EqualTo(140 * GlassCardBackLine.EmblemHeight).Within(0.01f)
            );
            Assert.That(
                GlassCardBackLine.EmblemDrawnWidth(new Vector2(265, 300), 1f),
                Is.EqualTo(265 * GlassCardBackLine.EmblemWidth).Within(0.01f)
            );
        }

        private static GlassCardBack BackOf(GlassCard card) => card.GetComponent<GlassCardBack>();

        // 板と裏面だけの、最小のカード。
        private List<GlassCard> Cards(int count)
        {
            var cards = new List<GlassCard>();
            for (int i = 0; i < count; i++)
            {
                var obj = new GameObject("Card" + i, typeof(RectTransform));
                made.Add(obj);
                var card = obj.AddComponent<GlassCard>();
                card.Group = obj.AddComponent<CanvasGroup>();
                var body = new GameObject("Body", typeof(RectTransform));
                body.transform.SetParent(obj.transform, false);
                card.Body = (RectTransform)body.transform;
                new GameObject("Content", typeof(RectTransform)).transform.SetParent(
                    body.transform,
                    false
                );
                var face = new GameObject("Back", typeof(RectTransform));
                face.transform.SetParent(body.transform, false);
                face.SetActive(false);
                var back = obj.AddComponent<GlassCardBack>();
                back.Card = card;
                back.Face = face;
                cards.Add(card);
            }
            return cards;
        }
    }
}
