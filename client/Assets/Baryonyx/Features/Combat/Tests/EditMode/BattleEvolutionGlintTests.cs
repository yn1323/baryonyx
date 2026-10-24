using System.Linq;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 引いたときに覚醒が当たったカードの手札の光（doc/features/skill-evolution.md#覚醒の予兆）。
    public sealed class BattleEvolutionGlintTests
    {
        private const string ScreenPath =
            "Assets/Baryonyx/Features/Combat/UI/BattleInspectScreen.prefab";

        private GameObject root;
        private Material material;

        [SetUp]
        public void SetUp()
        {
            material = new Material(Shader.Find("UI/Default"));
            root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void TheLightIsMadeHiddenBehindTheCardWithItsStarOverIt()
        {
            var card = Card("Face");
            Assert.That(BattleEvolutionGlint.Make(card, material, null, material), Is.Null);

            var glint = BattleEvolutionGlint.Make(card, material, material, material);
            Assert.That(glint, Is.Not.Null);
            Assert.That(
                glint.transform.GetSiblingIndex(),
                Is.Zero,
                "The light lies behind the card."
            );
            var star = card.GetChild(card.childCount - 1);
            Assert.That(star.name, Is.EqualTo("EvolutionGlintStar"), "The star lies over it.");
            Assert.That(glint.gameObject.activeSelf, Is.False);
            Assert.That(star.gameObject.activeSelf, Is.False);

            glint.SetLit(true);
            Assert.That(glint.Lit, Is.True);
            Assert.That(glint.gameObject.activeSelf, Is.True);
            Assert.That(star.gameObject.activeSelf, Is.True);

            glint.SetLit(false);
            Assert.That(glint.gameObject.activeSelf, Is.False);
            Assert.That(star.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ACopyOfTheCardIsLeftWithoutTheLight()
        {
            var card = Card("Face");
            BattleEvolutionGlint.Make(card, material, material, material).SetLit(true);
            var copy = Object.Instantiate(card.gameObject, root.transform, false);

            BattleEvolutionGlint.StripFrom(copy);
            Assert.That(copy.GetComponentsInChildren<BattleEvolutionGlint>(true), Is.Empty);
            Assert.That(
                copy.GetComponentsInChildren<Transform>(true).Select(part => part.name),
                Is.EquivalentTo(new[] { copy.name, "Face" })
            );
            Assert.That(card.GetComponentInChildren<BattleEvolutionGlint>(true), Is.Not.Null);
        }

        [Test]
        public void TheBattleScreenShowsACardOfTheOpeningHandLitWhileTheEditorIsStopped()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
            Assert.That(prefab, Is.Not.Null);
            var screen = Object.Instantiate(prefab);
            try
            {
                var view = screen.GetComponentInChildren<BattleInspectView>(true);
                int state = view.PreviewStates.ToList().IndexOf("覚醒の予兆（光るカード）");
                Assert.That(state, Is.GreaterThanOrEqualTo(0));

                view.ShowPreviewState(state);
                var lit = screen
                    .GetComponentsInChildren<BattleEvolutionGlint>(true)
                    .Where(glint => glint.Lit)
                    .ToList();
                Assert.That(lit, Has.Count.EqualTo(1));
                Assert.That(
                    view.Cards.Any(card => lit[0].transform.parent == card.Body),
                    Is.True,
                    "A card of the hand is lit."
                );
            }
            finally
            {
                Object.DestroyImmediate(screen);
            }
        }

        private RectTransform Card(params string[] parts)
        {
            var card = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
            card.SetParent(root.transform, false);
            card.sizeDelta = new Vector2(210f, 294f);
            foreach (string part in parts)
                new GameObject(part, typeof(RectTransform)).transform.SetParent(card, false);
            return card;
        }
    }
}
