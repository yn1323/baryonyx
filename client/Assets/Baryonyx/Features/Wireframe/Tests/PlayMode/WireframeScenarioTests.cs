using System;
using System.Collections;
using Baryonyx.Health;
using Baryonyx.Wireframe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    [PrebuildSetup(typeof(WireframeTestAssets))]
    [PostBuildCleanup(typeof(WireframeTestAssets))]
    public sealed class WireframeScenarioTests : ScenarioInputFixture
    {
        private WireframeView view;
        private WireframeData data;
        private GameObject events;
        private InputActionAsset actions;

        [SetUp]
        public void CreateScreen()
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                WireframeTestAssets.Source
            );
#else
            var prefab = Resources.Load<GameObject>("BaryonyxWireframeTest");
#endif
            Assert.That(prefab, Is.Not.Null);
            events = new GameObject("Wireframe Test Input");
            events.SetActive(false);
            events.AddComponent<EventSystem>();
            var module = events.AddComponent<InputSystemUIInputModule>();
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = actions.AddActionMap("UI");
            module.actionsAsset = actions;
            module.point = InputActionReference.Create(
                map.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position")
            );
            module.leftClick = InputActionReference.Create(
                map.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton")
            );
            module.scrollWheel = InputActionReference.Create(
                map.AddAction("Scroll", InputActionType.PassThrough, "<Mouse>/scroll")
            );
            module.submit = InputActionReference.Create(
                map.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter")
            );
            module.cancel = InputActionReference.Create(
                map.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape")
            );
            events.SetActive(true);
            data = ScriptableObject.CreateInstance<WireframeData>();
            view = UnityEngine.Object.Instantiate(prefab).GetComponent<WireframeView>();
            view.Bind(new WireframeSession(data));
        }

        [TearDown]
        public override void TearDown()
        {
            if (view != null)
                UnityEngine.Object.Destroy(view.gameObject);
            if (data != null)
                UnityEngine.Object.Destroy(data);
            if (events != null)
            {
                events.SetActive(false);
                var module = events.GetComponent<InputSystemUIInputModule>();
                foreach (
                    var reference in new[]
                    {
                        module.point,
                        module.leftClick,
                        module.scrollWheel,
                        module.submit,
                        module.cancel,
                    }
                )
                    if (reference != null)
                        UnityEngine.Object.Destroy(reference);
                UnityEngine.Object.Destroy(events);
            }
            if (actions != null)
            {
                actions.Disable();
                UnityEngine.Object.Destroy(actions);
            }
            base.TearDown();
        }

        private IEnumerator Click(string name, bool waitForTravel = true)
        {
            yield return null;
            var button = view.Button(name);
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, name);
            var scroll = button.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    scroll.viewport,
                    button.transform
                );
                float shift = scroll.viewport.rect.center.y - bounds.center.y;
                scroll.content.anchoredPosition += new Vector2(0, shift);
                scroll.velocity = Vector2.zero;
                yield return null;
            }
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center)
            );
            Set(Mouse.position, point);
            yield return null;
            Press(Mouse.leftButton);
            yield return null;
            Release(Mouse.leftButton);
            yield return null;
            if (waitForTravel && (name is "ExploreDoor" or "ExploreChest"))
                yield return new WaitForSecondsRealtime(1.2f);
        }

        private IEnumerator Explore()
        {
            yield return Click("PopupPrimary");
            yield return Click("HomeAdventure");
            yield return Click("Destination0");
            Assert.That(view.Session.Screen, Is.EqualTo(WireScreen.Explore));
        }

        private IEnumerator Win()
        {
            view.Session.ToggleDebug();
            yield return null;
            yield return Click("DebugWin");
        }

        [UnityTest]
        public IEnumerator SelectingExitThenBackingOutCancelsPendingTravel()
        {
            yield return Explore();
            var party = view.GetComponent<WireframeArt>().ExplorationParty;
            Assert.That(party, Is.Not.Null);
            Assert.That(
                party.GetComponentsInChildren<UnityEngine.UI.RawImage>().Length,
                Is.EqualTo(4)
            );
            Vector2 origin = party.anchoredPosition;
            // Pointer-driven travel is covered by the two adventure scenarios. Cancel in
            // the same frame here so this check remains reliable on slow test machines.
            view.Button("ExploreDoor").onClick.Invoke();
            Assert.That(view.Session.Screen, Is.EqualTo(WireScreen.Explore));
            Assert.That(view.GetComponent<CanvasGroup>().interactable, Is.False);
            view.Session.Back();
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(view.Session.Screen, Is.EqualTo(WireScreen.Explore));
            Assert.That(view.Session.Popup, Is.EqualTo(WirePopup.EndAdventure));
            Assert.That(party.anchoredPosition, Is.EqualTo(origin));
            Assert.That(view.GetComponent<CanvasGroup>().interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator AdventureAcquisitionEquipmentBossAndReturnAreClickable()
        {
            yield return Explore();
            yield return Click("ExploreDoor");
            yield return Click("Hp1");
            Assert.That(view.Text("Skill0Label").text, Does.Contain("火の矢"));
            yield return Click("Skill0");
            yield return Click("Enemy1");
            yield return Click("UseSkill");
            Assert.That(view.Session.Battle.Allies[1].Cooldowns[0], Is.GreaterThan(0));
            yield return Win();
            yield return Click("PopupPrimary");
            yield return Click("EquipmentConfirm");
            Assert.That(view.Session.Screen, Is.EqualTo(WireScreen.Explore));
            Assert.That(view.Session.Equipped(1), Is.EqualTo(2));
            yield return Click("ExploreDoor");
            yield return Win();
            yield return Click("ExploreDoor");
            Assert.That(view.Session.IsBoss, Is.True);
            yield return Win();
            yield return Click("ResultHome");
            Assert.That(view.Session.Screen, Is.EqualTo(WireScreen.Home));
        }

        [UnityTest]
        public IEnumerator CompanionAndDefeatRoundTripUseTheCorrectCaller()
        {
            yield return Explore();
            yield return Click("ExploreChest");
            yield return Click("PopupPrimary");
            yield return Click("PartySlot2");
            yield return Click("PartyConfirm");
            Assert.That(view.Session.PartyMember(2), Is.EqualTo(4));
            yield return Click("ExploreDoor");
            view.Session.ToggleDebug();
            yield return null;
            yield return Click("DebugLose");
            yield return Click("DefeatParty");
            yield return Click("PartyEquipment");
            yield return Click("Back");
            yield return Click("Back");
            Assert.That(view.Session.Screen, Is.EqualTo(WireScreen.Defeat));
            yield return Click("DefeatRevive");
            yield return Click("PopupSecondary");
            yield return Click("DefeatRetry");
            Assert.That(view.Session.EnemyHp, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator ModalBlocksBackgroundAndEscapeClosesOnlyTheTopLayer()
        {
            yield return null;
            // The header stays visible even when the test runner uses a short landscape view.
            var button = view.Button("HeaderSettings");
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    rect.TransformPoint(rect.rect.center)
                ),
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(
                hits[0].gameObject.transform.IsChildOf(view.PopupOverlay.transform),
                Is.True
            );
            Press(Keyboard.escapeKey);
            yield return null;
            Release(Keyboard.escapeKey);
            yield return null;
            Assert.That(view.Session.Popup, Is.EqualTo(WirePopup.None));
            Assert.That(view.Session.Screen, Is.EqualTo(WireScreen.Home));
        }

        [UnityTest]
        public IEnumerator ActualAutoCombatReachesItsRewardWithoutThePreviewControls()
        {
            yield return Explore();
            yield return Click("ExploreDoor");
            float deadline = Time.realtimeSinceStartup + 60;
            while (view.Session.Screen == WireScreen.Battle && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(view.Session.Screen, Is.EqualTo(WireScreen.Explore));
            Assert.That(view.Session.Popup, Is.EqualTo(WirePopup.NewEquipment));
            Assert.That(view.Session.Runes, Is.EqualTo(980));
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator HealthChartShowsZeroAndMissingDaysAndOpensOriginalJson()
        {
            var provider = new HealthScreenPreviewProvider(() =>
                DateTimeOffset.Parse("2026-09-19T12:00:00+09:00")
            );
            using var presenter = new HealthScreenPresenter(provider, provider);
            var health = view.GetComponent<WireframeHealthView>();
            health.Bind(presenter, true);
            yield return Click("PopupPrimary");
            yield return Click("HomeSteps");
            yield return Click("HealthConnect");
            Assert.That(view.Session.Screen, Is.EqualTo(WireScreen.Health));
            Assert.That(health.Values[4].text, Is.EqualTo("0"));
            Assert.That(health.Values[5].text, Is.EqualTo("記録なし"));
            Assert.That(health.Values[6].text, Is.EqualTo("6,432"));
            Assert.That(health.Dates[0].text, Is.EqualTo("09/13"));
            Assert.That(health.Bars[2].anchorMax.y, Is.EqualTo(1));
            yield return Click("HealthDay6");
            Assert.That(health.DetailsOpen, Is.True);
            Assert.That(health.Detail.text, Is.EqualTo(presenter.Days[0].Json));
            yield return Click("HealthDetailsClose");
            Assert.That(health.DetailsOpen, Is.False);
        }
#endif

        [UnityTest]
        public IEnumerator GoalNoticeAndIndependentSlotsAreDisplayed()
        {
            yield return Click("PopupPrimary");
            yield return Click("NavGoals");
            yield return Click("GoalsEdit");
            yield return Click("GoalOption1");
            yield return Click("PopupPrimary");
            Assert.That(view.Session.Popup, Is.EqualTo(WirePopup.Data));
            yield return Click("PopupPrimary");
            Assert.That(view.Text("GoalsDaily").text, Does.Contain("km"));
            Assert.That(view.Text("GoalsWeekly").text, Does.Contain("未設定"));
        }
    }

    public sealed class WireframeTestAssets : IPrebuildSetup, IPostBuildCleanup
    {
        public const string Source = "Assets/Baryonyx/Features/Wireframe/UI/WireframeScreen.prefab";
        private const string Folder = "Assets/Baryonyx/Features/Wireframe/Tests/PlayMode/Resources";
        private const string Destination = Folder + "/BaryonyxWireframeTest.prefab";

        public void Setup()
        {
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Destination) != null)
                throw new InvalidOperationException("Leftover wireframe test asset exists.");
            System.IO.Directory.CreateDirectory(Folder);
            UnityEditor.AssetDatabase.Refresh();
            if (!UnityEditor.AssetDatabase.CopyAsset(Source, Destination))
                throw new InvalidOperationException("Cannot prepare the wireframe test prefab.");
#endif
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.DeleteAsset(Destination);
            if (
                System.IO.Directory.Exists(Folder)
                && System.IO.Directory.GetFiles(Folder).Length == 0
            )
                UnityEditor.AssetDatabase.DeleteAsset(Folder);
#endif
        }
    }
}
