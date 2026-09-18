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
            root.sizeDelta = new Vector2(360, height * 360f / width);
            layout.ApplyViewport(new Vector2(width, height), safe, root.rect.size);
            foreach (var follower in view.GetComponentsInChildren<WireframeSafeAreaFollower>(true))
                follower.Apply();
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        [TestCase(720, 1280)]
        [TestCase(1080, 2340)]
        [TestCase(1080, 2400)]
        [TestCase(1536, 2048)]
        [TestCase(2048, 1536)]
        public void HeaderAndModalCloseStayInsideAsymmetricSafeArea(int width, int height)
        {
            Size(width, height, new Rect(24, 64, width - 60, height - 152));
            AssertInside((RectTransform)view.Button("Back").transform, layout.SafeArea);
            AssertInside((RectTransform)view.Button("Preview").transform, layout.SafeArea);
            AssertInside((RectTransform)view.Button("PopupClose").transform, layout.SafeArea);
            Assert.That(layout.Panel.rect.width, Is.LessThanOrEqualTo(layout.SafeArea.rect.width));
            Assert.That(view.Session.Popup, Is.EqualTo(WirePopup.Intro));
        }

        [Test]
        public void PortraitBattleKeepsCoreControlsVisibleWhenSelectingASkill()
        {
            var s = view.Session;
            s.Back();
            s.Open(WireScreen.Destination);
            s.BeginAdventure(0);
            s.EnterDoor();
            s.SelectSlot(1);
            s.ChooseSkill(0);
            Size(720, 1280, new Rect(0, 0, 720, 1280));
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
            Assert.That(view.HpFills[3].anchorMax.x, Is.EqualTo(.55f).Within(.001f));
        }

        [Test]
        public void ResizingWithAModalKeepsSelectionAndAllOperationsScrollable()
        {
            var s = view.Session;
            s.Back();
            s.Open(WireScreen.Goals);
            s.ShowGoals(false);
            s.ToggleGoal(1);
            Size(1080, 2400, new Rect(0, 96, 1080, 2208));
            Size(2048, 1536, new Rect(24, 64, 1988, 1384));
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
            s.ToggleDebug();
            s.PreviewCombat(WireCombatState.Down);
            Assert.That(enemy.color.r, Is.LessThan(normal));
            Assert.That(s.EnemyHp, Is.EqualTo(100));
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
    }
}
