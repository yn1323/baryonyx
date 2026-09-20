using Baryonyx.Wireframe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class WireframeLayoutTests
    {
        private WireframeView view;
        private WireframeLayout layout;
        private WireframeData data;
        private RectTransform root;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Baryonyx/Features/Wireframe/UI/WireframeScreen.prefab"
            );
            view = Object.Instantiate(prefab).GetComponent<WireframeView>();
            root = (RectTransform)view.transform;
            view.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
            view.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            layout = view.GetComponent<WireframeLayout>();
            layout.enabled = false;
            data = ScriptableObject.CreateInstance<WireframeData>();
            view.Bind(new WireframeSession(data));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(view.gameObject);
            Object.DestroyImmediate(data);
        }

        private void Size(int width, int height, Rect safe)
        {
            root.sizeDelta = new Vector2(width, height);
            layout.ApplyViewport(new Vector2(width, height), safe, root.rect.size);
            foreach (var follower in view.GetComponentsInChildren<WireframeSafeAreaFollower>(true))
                follower.Apply();
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        [TestCase(1920, 1080)]
        [TestCase(2340, 1080)]
        [TestCase(2400, 1080)]
        public void HeaderAndModalCloseStayInsideAsymmetricSafeArea(int width, int height)
        {
            Size(width, height, new Rect(24, 64, width - 60, height - 152));
            AssertInside((RectTransform)view.Button("HeaderSettings").transform, layout.SafeArea);
            AssertInside((RectTransform)view.Button("PopupClose").transform, layout.SafeArea);
            Assert.That(layout.Panel.rect.width, Is.LessThanOrEqualTo(layout.SafeArea.rect.width));
            Assert.That(
                layout.HealthDetailsPanel.rect.width,
                Is.LessThanOrEqualTo(layout.SafeArea.rect.width)
            );
            Assert.That(layout.CoreArea.rect.height, Is.EqualTo(1080).Within(.01f));
            Assert.That(layout.CoreArea.rect.width, Is.EqualTo(1920).Within(.01f));
            Assert.That(layout.CoreArea.parent, Is.EqualTo(root));
            Assert.That(layout.Panel.parent, Is.EqualTo(layout.CoreArea));
            Assert.That(layout.Panel.rect.width, Is.EqualTo(560).Within(.01f));
            Assert.That(layout.BattlefieldAmbient, Is.Not.Null);
            Assert.That(layout.BattlefieldAmbient.transform.parent, Is.EqualTo(root));
            AssertInside(layout.Panel, layout.SafeArea);
            Assert.That(view.Navigation.transform.parent, Is.EqualTo(layout.SafeArea));
            AssertInside((RectTransform)view.Button("NavHome").transform, layout.SafeArea);
            Assert.That(((RectTransform)view.Button("NavHome").transform).rect.width, Is.EqualTo(192));
            AssertModalFollowsSafeArea(layout.PopupPanel, view.PopupOverlay, "PopupSafe");
            AssertModalFollowsSafeArea(layout.DebugPanel, view.DebugOverlay, "DebugSafe");
            AssertModalFollowsSafeArea(
                layout.HealthDetailsPanel,
                view.GetComponent<WireframeHealthView>().DetailsOverlay,
                "HealthDetailsSafe"
            );
            Assert.That(view.Session.Popup, Is.EqualTo(WirePopup.Intro));
            view.Session.Back();
            view.Session.Open(WireScreen.Destination);
            Size(width, height, new Rect(24, 64, width - 60, height - 152));
            AssertInside((RectTransform)view.Button("Back").transform, layout.SafeArea);
        }

        [Test]
        public void LandscapeBattleKeepsCoreControlsVisibleWhenSelectingASkill()
        {
            var s = view.Session;
            s.Back();
            s.Open(WireScreen.Destination);
            s.BeginAdventure(0);
            s.EnterDoor();
            s.SelectSlot(1);
            s.ChooseSkill(0);
            Size(1920, 1080, new Rect(0, 0, 1920, 1080));
            foreach (
                string name in new[]
                {
                    "Ally0",
                    "Ally3",
                    "Enemy0",
                    "Enemy2",
                    "Hp0",
                    "Hp3",
                    "Skill0",
                    "Skill1",
                    "UseSkill",
                }
            )
                AssertInside((RectTransform)view.Button(name).transform, view.PageScroll.viewport);
            Assert.That(view.HpFills[3].anchorMax.x, Is.EqualTo(1f).Within(.001f));
        }

        [Test]
        public void ResizingWithAModalKeepsSelectionAndAllOperationsScrollable()
        {
            var s = view.Session;
            s.Back();
            s.Open(WireScreen.Goals);
            s.ShowGoals(false);
            s.ToggleGoal(1);
            Size(2340, 1080, new Rect(0, 48, 2340, 1032));
            Size(2400, 1080, new Rect(24, 64, 2316, 952));
            Assert.That(s.Popup, Is.EqualTo(WirePopup.GoalEdit));
            Assert.That(s.GoalDistance, Is.True);
            var scroll = view.Button("PopupPrimary")
                .GetComponentInParent<UnityEngine.UI.ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            AssertInside((RectTransform)view.Button("PopupSecondary").transform, scroll.viewport);
        }

        [Test]
        public void ArtworkFollowsPartyChangesAndEnemyConditionWithoutChangingState()
        {
            var s = view.Session;
            s.Back();
            s.Open(WireScreen.Party);
            s.SelectSlot(0);
            s.ChooseCharacter(1);
            s.ConfirmCharacter();
            var images = view.GetComponentsInChildren<UnityEngine.UI.RawImage>(true);
            var home = System.Array.Find(images, image => image.name == "HomeLandscapeActor0");
            var battle = System.Array.Find(images, image => image.name == "BattleAllyArt0");
            Assert.That(home.uvRect, Is.EqualTo(WireframeArt.ActorUv(1)));
            Assert.That(battle.uvRect, Is.EqualTo(home.uvRect));
            var rest = System.Array.Find(images, image => image.name == "RestActor");
            Assert.That(rest.uvRect, Is.EqualTo(home.uvRect));
            s.HomeTab(WireScreen.Home);
            s.Open(WireScreen.Destination);
            s.BeginAdventure(1);
            s.EnterDoor();
            var backdrop = System.Array.Find(images, image => image.name == "BattleBackdrop");
            Assert.That(backdrop.texture, Is.EqualTo(view.GetComponent<WireframeArt>().Mine));
            var result = System.Array.Find(images, image => image.name == "ResultLandscapeArt");
            Assert.That(result.texture, Is.EqualTo(backdrop.texture));
            var enemy = System.Array.Find(images, image => image.name == "BattleEnemyArt0");
            float normal = enemy.color.r;
            s.SelectSlot(0);
            s.ChooseSkill(1);
            s.UseSkill();
            s.AdvanceBattle(1);
            s.SelectSlot(2);
            s.ChooseSkill(1);
            s.UseSkill();
            s.AdvanceBattle(1);
            Assert.That(enemy.color.r, Is.LessThan(normal));
            Assert.That(s.Battle.Enemies[0].IsDown, Is.True);
        }

        private static void AssertInside(RectTransform child, RectTransform container)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = container.InverseTransformPoint(corner);
                Assert.That(
                    point.x,
                    Is.InRange(container.rect.xMin - 1, container.rect.xMax + 1),
                    child.name
                );
                Assert.That(
                    point.y,
                    Is.InRange(container.rect.yMin - 1, container.rect.yMax + 1),
                    child.name
                );
            }
        }

        private void AssertModalFollowsSafeArea(
            RectTransform panel,
            GameObject overlay,
            string expectedSafeName
        )
        {
            Assert.That(overlay.transform.parent, Is.EqualTo(root), overlay.name);
            AssertRectMatches((RectTransform)overlay.transform, root, overlay.name);

            var follower = panel.GetComponentInParent<WireframeSafeAreaFollower>(true);
            Assert.That(follower, Is.Not.Null, panel.name);
            Assert.That(follower.Source, Is.EqualTo(layout.SafeArea), panel.name);
            Assert.That(follower.name, Is.EqualTo(expectedSafeName));
            Assert.That(follower.transform.parent, Is.EqualTo(overlay.transform));
            Assert.That(panel.IsChildOf(follower.transform), Is.True, panel.name);
            AssertRectMatches((RectTransform)follower.transform, layout.SafeArea, follower.name);
            AssertInside(panel, layout.SafeArea);
        }

        private static void AssertRectMatches(
            RectTransform actual,
            RectTransform expected,
            string label
        )
        {
            var actualCorners = new Vector3[4];
            var expectedCorners = new Vector3[4];
            actual.GetWorldCorners(actualCorners);
            expected.GetWorldCorners(expectedCorners);
            for (int i = 0; i < actualCorners.Length; i++)
                Assert.That(
                    Vector3.Distance(actualCorners[i], expectedCorners[i]),
                    Is.LessThan(.01f),
                    $"{label} corner {i}"
                );
        }
    }
}
