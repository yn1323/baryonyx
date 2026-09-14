using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Health;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    [PrebuildSetup(typeof(HealthScreenTestAssets))]
    [PostBuildCleanup(typeof(HealthScreenTestAssets))]
    public sealed class HealthScreenScenarioTests : ScenarioInputFixture
    {
        private HealthScreenView view;
        private HealthScreenPresenter presenter;
        private GameObject events;
        private Authentication authentication;
        private Provider provider;
        private InputActionAsset actions;

        [SetUp]
        public void CreateScreen()
        {
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine
                .InputSystem
                .InputSettings
                .BackgroundBehavior
                .IgnoreFocus;
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                HealthScreenTestAssets.Source
            );
#else
            var prefab = Resources.Load<GameObject>("BaryonyxHealthScreenTest");
#endif
            Assert.That(
                prefab,
                Is.Not.Null,
                "The production screen must be available in test players."
            );
            events = new GameObject("Test EventSystem");
            events.SetActive(false);
            events.AddComponent<EventSystem>();
            var module = events.AddComponent<InputSystemUIInputModule>();
            // A fresh asset prevents the Editor's cached device bindings from crossing fixtures.
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
            view = UnityEngine.Object.Instantiate(prefab).GetComponent<HealthScreenView>();
            authentication = new Authentication();
            provider = new Provider();
            presenter = new HealthScreenPresenter(authentication, provider);
            view.Bind(presenter);
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public override void TearDown()
        {
            presenter?.Dispose();
            if (view != null)
                UnityEngine.Object.Destroy(view.gameObject);
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

        [UnityTest]
        public IEnumerator ClickThroughAuthenticationConnectionAndJsonDetails()
        {
            Assert.That(view.ConnectButton.interactable, Is.True);
            Assert.That(view.SignInButton.gameObject.activeSelf, Is.True);
            Assert.That(
                view.ConnectButton.transform.parent,
                Is.Not.SameAs(view.SignInButton.transform.parent)
            );
            yield return Click(view.SignInButton);
            Assert.That(view.ConnectButton.interactable, Is.True);
            Assert.That(provider.Reads, Is.Zero);
            yield return Click(view.ConnectButton);
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
            Assert.That(view.DayLabels[0].text, Does.Contain("6,432"));
            Assert.That(view.DayLabels[1].text, Does.Contain("データなし"));
            Assert.That(view.DayLabels[2].text, Does.Contain("0 歩"));
            yield return Click(view.DayButtons[0]);
            Assert.That(view.DetailsOverlay.activeSelf, Is.True);
            Assert.That(view.JsonText.text, Is.EqualTo(presenter.Days[0].Json));
            Assert.That(view.JsonText.richText, Is.False);
            Assert.That(view.RefreshButton.interactable, Is.False);
            Assert.That(view.SignOutButton.interactable, Is.False);
            // Click where the refresh button lies underneath the modal.
            int reads = provider.Reads;
            yield return Click(view.RefreshButton);
            Assert.That(provider.Reads, Is.EqualTo(reads));
            yield return Click(view.CloseButton);
            Assert.That(view.DetailsOverlay.activeSelf, Is.False);
            Assert.That(view.JsonText.text, Is.Empty);
            Assert.That(view.DayButtons[0].interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator BackClosesDetailsAndRestoresListSelection()
        {
            yield return Click(view.SignInButton);
            yield return Click(view.ConnectButton);
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
            yield return Click(view.DayButtons[0]);
            Assert.That(view.DetailsOverlay.activeSelf, Is.True);
            Press(Keyboard.escapeKey);
            yield return null;
            Release(Keyboard.escapeKey);
            yield return null;
            Assert.That(view.DetailsOverlay.activeSelf, Is.False);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(view.DayButtons[0].gameObject)
            );
        }

        [UnityTest]
        public IEnumerator AdditionalHealthJsonOpensWithoutStepsAndRetainsSourceAndUnits()
        {
            provider.AdditionalOnly = true;
            yield return Click(view.ConnectButton);
            Assert.That(view.DayLabels[0].text, Is.EqualTo("歩数は未許可"));
            Assert.That(view.ConnectButton.gameObject.activeSelf, Is.True);
            Assert.That(view.ConnectButton.interactable, Is.True);
            Assert.That(view.Status.gameObject.activeSelf, Is.True);
            Assert.That(view.Status.text, Does.Contain("歩数の読み取りが未許可"));
            Assert.That(view.SettingsButton.gameObject.activeSelf, Is.True);
            yield return Click(view.DayButtons[0]);
            Assert.That(view.DetailsOverlay.activeSelf, Is.True);
            var record = JObject.Parse(view.JsonText.text)["records"]["weight"]["records"][0];
            Assert.That(record.Value<string>("sourceApp"), Is.EqualTo("example.health"));
            Assert.That(record.Value<double>("kilograms"), Is.EqualTo(62.5));
            Assert.That(view.SettingsButton.interactable, Is.False);
            yield return Click(view.CloseButton);
            Assert.That(view.SettingsButton.interactable, Is.True);
            yield return Click(view.SettingsButton);
            Assert.That(provider.SettingsOpened, Is.EqualTo(1));
            provider.AdditionalOnly = false;
            yield return Click(view.ConnectButton);
            Assert.That(view.DayLabels[0].text, Does.Contain("6,432"));
            Assert.That(view.ConnectButton.gameObject.activeSelf, Is.False);
            Assert.That(view.RefreshButton.gameObject.activeSelf, Is.True);
            Assert.That(provider.PermissionRequests, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator DenialShowsRetryAndSettingsThenReadsAfterPermission()
        {
            provider.Permission = HealthPermission.NotGranted;
            yield return Click(view.SignInButton);
            yield return Click(view.ConnectButton);
            Assert.That(view.SettingsButton.gameObject.activeSelf, Is.True);
            Assert.That(view.EmptyState.activeSelf, Is.True);
            Assert.That(provider.Reads, Is.Zero);
            yield return Click(view.SettingsButton);
            Assert.That(provider.SettingsOpened, Is.EqualTo(1));
            provider.Permission = HealthPermission.Granted;
            yield return Click(view.ConnectButton);
            Assert.That(view.EmptyState.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator HealthWorksAfterGoogleFailureAndSignOutPreservesList()
        {
            authentication.Result = GoogleSignInStatus.Incomplete;
            yield return Click(view.SignInButton);
            Assert.That(view.SignInButton.interactable, Is.True);
            Assert.That(view.ConnectButton.interactable, Is.True);
            yield return Click(view.ConnectButton);
            Assert.That(presenter.SignedIn, Is.False);
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
            yield return Click(view.DayButtons[0]);
            Assert.That(view.DetailsOverlay.activeSelf, Is.True);
            yield return Click(view.CloseButton);
            authentication.Result = GoogleSignInStatus.Success;
            yield return Click(view.SignInButton);
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
            yield return Click(view.SignOutButton);
            Assert.That(view.EmptyState.activeSelf, Is.False);
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
            Assert.That(view.RefreshButton.interactable, Is.True);
            Assert.That(view.SignInButton.interactable, Is.True);
            foreach (var label in view.DayLabels)
                Assert.That(label.text, Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator LongJsonScrollsAndClosingPreservesListPosition()
        {
            provider.LongJson = true;
            yield return Click(view.SignInButton);
            yield return Click(view.ConnectButton);
            view.MainScroll.verticalNormalizedPosition = 0;
            yield return null;
            yield return Reveal(view.DayButtons[6]);
            float position = view.MainScroll.verticalNormalizedPosition;
            yield return Click(view.DayButtons[6]);
            Assert.That(view.DetailsOverlay.activeSelf, Is.True);
            Canvas.ForceUpdateCanvases();
            Assert.That(
                view.JsonScroll.content.rect.height,
                Is.GreaterThan(view.JsonScroll.viewport.rect.height)
            );
            Assert.That(
                view.JsonScroll.content.rect.width,
                Is.GreaterThan(view.JsonScroll.viewport.rect.width)
            );
            Set(
                Mouse.position,
                RectTransformUtility.WorldToScreenPoint(
                    null,
                    view.JsonScroll.viewport.TransformPoint(view.JsonScroll.viewport.rect.center)
                )
            );
            yield return null;
            Set(Mouse.scroll, new Vector2(-120, -120));
            yield return null;
            Set(Mouse.scroll, Vector2.zero);
            yield return null;
            Assert.That(view.JsonScroll.verticalNormalizedPosition, Is.LessThan(1));
            Assert.That(view.JsonScroll.horizontalNormalizedPosition, Is.GreaterThan(0));
            yield return Click(view.CloseButton);
            Assert.That(
                view.MainScroll.verticalNormalizedPosition,
                Is.EqualTo(position).Within(0.001f)
            );
        }

        [UnityTest]
        public IEnumerator CopyUsesTheCurrentDayAndResetsWhenDetailsChange()
        {
            string previousClipboard = GUIUtility.systemCopyBuffer;
            try
            {
                yield return Click(view.ConnectButton);
                yield return Click(view.DayButtons[0]);
                yield return Click(view.CopyButton);
                Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo(presenter.SelectedDay.Json));
                Assert.That(
                    view.CopyButton.GetComponentInChildren<TMPro.TMP_Text>().text,
                    Is.EqualTo("コピーしました")
                );

                yield return Click(view.CloseButton);
                yield return Click(view.DayButtons[1]);
                Assert.That(
                    view.CopyButton.GetComponentInChildren<TMPro.TMP_Text>().text,
                    Is.EqualTo("JSONをコピー")
                );
                yield return Click(view.CopyButton);
                Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo(presenter.SelectedDay.Json));

                view.Bind(presenter, preview: true);
                Assert.That(view.DetailsTitle.text, Does.Contain("サンプルJSON"));
                Assert.That(
                    view.CopyButton.GetComponentInChildren<TMPro.TMP_Text>().text,
                    Is.EqualTo("JSONをコピー")
                );
            }
            finally
            {
                GUIUtility.systemCopyBuffer = previousClipboard;
            }
        }

        private IEnumerator Click(UnityEngine.UI.Button button)
        {
            yield return Reveal(button);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var point = RectTransformUtility.WorldToScreenPoint(
                null,
                ((RectTransform)button.transform).TransformPoint(
                    ((RectTransform)button.transform).rect.center
                )
            );
            Set(Mouse.position, point);
            yield return null;
            yield return null;
            var module = events.GetComponent<InputSystemUIInputModule>();
            Assert.That(module.point.action.enabled, Is.True, "UI point action is disabled.");
            Assert.That(module.leftClick.action.enabled, Is.True, "UI click action is disabled.");
            Assert.That(
                module.point.action.controls[0].device,
                Is.SameAs(Mouse),
                "UI input must use the fixture mouse."
            );
            if (button.interactable)
            {
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(
                    new PointerEventData(EventSystem.current) { position = point },
                    hits
                );
                Assert.That(hits.Count, Is.GreaterThan(0), $"No UI at {button.name} {point}.");
                Assert.That(
                    hits[0].gameObject,
                    Is.EqualTo(button.gameObject),
                    $"Unexpected UI at {button.name} {point}."
                );
                Assert.That(
                    module.GetLastRaycastResult(Mouse.deviceId).gameObject,
                    Is.EqualTo(button.gameObject),
                    "The UI module did not process the virtual pointer."
                );
            }
            Press(Mouse.leftButton);
            yield return null;
            Release(Mouse.leftButton);
            yield return null;
            yield return null;
        }

        private IEnumerator Reveal(UnityEngine.UI.Button button)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (
                view.DetailsOverlay.activeSelf
                || !button.gameObject.activeInHierarchy
                || !button.transform.IsChildOf(view.MainScroll.content)
            )
                yield break;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                view.MainScroll.viewport,
                button.transform
            );
            var viewport = view.MainScroll.viewport.rect;
            if (bounds.min.y < viewport.yMin || bounds.max.y > viewport.yMax)
            {
                view.MainScroll.StopMovement();
                var position = view.MainScroll.content.anchoredPosition;
                position.y = Mathf.Clamp(
                    position.y + viewport.yMax - bounds.max.y,
                    0,
                    Mathf.Max(0, view.MainScroll.content.rect.height - viewport.height)
                );
                view.MainScroll.content.anchoredPosition = position;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator MainScrollUsesPointerInputAndModalBlocksBackgroundScroll()
        {
            yield return Click(view.SignInButton);
            yield return Click(view.ConnectButton);
            view.MainScroll.verticalNormalizedPosition = 1;
            view.MainScroll.StopMovement();
            yield return null;
            Set(
                Mouse.position,
                RectTransformUtility.WorldToScreenPoint(
                    null,
                    view.MainScroll.viewport.TransformPoint(view.MainScroll.viewport.rect.center)
                )
            );
            yield return null;
            Set(Mouse.scroll, new Vector2(0, -120));
            yield return null;
            Set(Mouse.scroll, Vector2.zero);
            yield return null;
            Assert.That(view.MainScroll.verticalNormalizedPosition, Is.LessThan(1));
            yield return Click(view.DayButtons[6]);
            var previous = view.MainScroll.content.anchoredPosition;
            Set(Mouse.scroll, new Vector2(0, -120));
            yield return null;
            Set(Mouse.scroll, Vector2.zero);
            yield return null;
            Assert.That(view.MainScroll.enabled, Is.False);
            Assert.That(view.MainScroll.content.anchoredPosition, Is.EqualTo(previous));
            yield return Click(view.CloseButton);
            Assert.That(view.MainScroll.enabled, Is.True);
            yield return Click(view.SignOutButton);
            Assert.That(view.SignInButton.gameObject.activeSelf, Is.True);
            Assert.That(view.SignOutButton.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator SafeAreaChangesWhileDetailsAreOpenPreserveDataAndInteraction()
        {
            yield return Click(view.SignInButton);
            yield return Click(view.ConnectButton);
            yield return Click(view.DayButtons[0]);
            int reads = provider.Reads;
            var selected = presenter.SelectedDay;
            var layout = view.GetComponent<HealthScreenLayout>();
            layout.enabled = false;
            var canvasSize = ((RectTransform)view.transform).rect.size;
            Assert.That(
                layout.ApplyViewport(
                    new Vector2(720, 1280),
                    new Rect(24, 72, 672, 1112),
                    canvasSize
                ),
                Is.True
            );
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(view.DetailsOverlay.activeSelf, Is.True);
            Assert.That(presenter.SelectedDay, Is.SameAs(selected));
            Assert.That(provider.Reads, Is.EqualTo(reads));
            Assert.That(view.JsonText.text, Is.EqualTo(selected.Json));
            layout.enabled = true;
            Assert.That(
                layout.ApplyViewport(
                    new Vector2(720, 1280),
                    new Rect(24, 72, 672, 1112),
                    canvasSize
                ),
                Is.True
            );
            layout.enabled = false;
            yield return Click(view.CloseButton);
            Assert.That(view.DetailsOverlay.activeSelf, Is.False);
            Assert.That(provider.Reads, Is.EqualTo(reads));
        }

        [UnityTest]
        public IEnumerator LongStatusAndLargeStepCountsGrowWithoutClipping()
        {
            provider.LargeSteps = true;
            yield return Click(view.SignInButton);
            yield return Click(view.ConnectButton);
            Assert.That(view.DayDates[0].text, Is.EqualTo("09/13（日）"));
            Assert.That(view.DayLabels[0].text, Does.Contain("9,223,372,036,854,775,807"));
            Assert.That(view.DayLabels[1].text, Is.EqualTo("データなし"));
            Assert.That(view.DayLabels[2].text, Is.EqualTo("0 歩"));
            view.Status.gameObject.SetActive(true);
            view.Status.text = new string('長', 300);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(
                view.Status.rectTransform.rect.height,
                Is.GreaterThanOrEqualTo(view.Status.preferredHeight - 1)
            );
            Assert.That(
                view.DayLabels[0].rectTransform.rect.height,
                Is.GreaterThanOrEqualTo(view.DayLabels[0].preferredHeight - 1)
            );
            yield return Click(view.DayButtons[0]);
            yield return Click(view.CloseButton);
        }

        [UnityTest]
        public IEnumerator ProductionPrefabFitsRepresentativeSafeAreas()
        {
            yield return Click(view.SignInButton);
            yield return Click(view.ConnectButton);
            yield return Click(view.DayButtons[0]);
            var canvas = view.GetComponent<Canvas>();
            view.GetComponent<CanvasScaler>().enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            var layout = view.GetComponent<HealthScreenLayout>();
            layout.enabled = false;
            var root = (RectTransform)view.transform;
            foreach (
                var size in new[]
                {
                    new Vector2(640, 1136),
                    new Vector2(720, 1280),
                    new Vector2(1080, 2340),
                    new Vector2(1080, 2400),
                    new Vector2(1536, 2048),
                    new Vector2(2048, 1536),
                }
            )
            {
                float scale = Mathf.Min(size.x / 800, size.y / 1100);
                root.sizeDelta = size / scale;
                layout.ApplyViewport(
                    size,
                    new Rect(24, 72, size.x - 64, size.y - 168),
                    root.rect.size
                );
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertRectInside(view.MainScroll.viewport, layout.SafeArea);
                AssertRectInside((RectTransform)view.CloseButton.transform, layout.DetailsSafeArea);
                AssertRectInside(view.JsonScroll.viewport, layout.DetailsPanel);
                Assert.That(view.JsonScroll.viewport.rect.height, Is.GreaterThan(0));
                foreach (var button in view.GetComponentsInChildren<UnityEngine.UI.Button>())
                {
                    var rect = ((RectTransform)button.transform).rect;
                    Assert.That(rect.width, Is.GreaterThanOrEqualTo(120 - 0.1f), button.name);
                    Assert.That(rect.height, Is.GreaterThanOrEqualTo(120 - 0.1f), button.name);
                }
                foreach (var label in view.DayLabels)
                    Assert.That(
                        label.rectTransform.rect.height,
                        Is.GreaterThanOrEqualTo(label.preferredHeight - 1)
                    );
            }
        }

        private static void AssertRectInside(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = parent.InverseTransformPoint(corner);
                Assert.That(point.x, Is.InRange(parent.rect.xMin - 0.1f, parent.rect.xMax + 0.1f));
                Assert.That(point.y, Is.InRange(parent.rect.yMin - 0.1f, parent.rect.yMax + 0.1f));
            }
        }

        // Synthetic values exist only in this test assembly.
        [UnityTest]
        public IEnumerator StartupNoticeRoutesToSettingsAndUpdatesOnReturn()
        {
            provider.Requirements = new HealthRequirementState(
                HealthAvailability.Available,
                HealthPermission.Granted,
                HealthStepsDataState.Empty,
                34,
                19,
                true
            );
            var startup = presenter.InitializeAsync();
            yield return new WaitUntil(() => startup.IsCompleted);
            Assert.That(view.Status.gameObject.activeSelf, Is.True);
            Assert.That(view.Status.text, Does.Contain("Google Playシステムアップデート"));
            Assert.That(
                view.SettingsButton.GetComponentInChildren<TMPro.TMP_Text>().text,
                Is.EqualTo("端末の設定を開く")
            );
            Assert.That(provider.PermissionRequests, Is.Zero);
            Assert.That(provider.Reads, Is.Zero);
            yield return Click(view.SettingsButton);
            Assert.That(provider.SettingsOpened, Is.EqualTo(1));
            Assert.That(provider.SettingsDestination, Is.EqualTo(HealthSettingsDestination.Device));
            provider.Requirements = new HealthRequirementState(
                HealthAvailability.Available,
                HealthPermission.Granted,
                HealthStepsDataState.Present,
                34,
                20,
                true
            );
            presenter.SetForeground(false);
            presenter.SetForeground(true);
            yield return null;
            Assert.That(presenter.RequirementNotice.HasNotice, Is.False);
            Assert.That(view.Status.text, Does.Not.Contain("Google Playシステムアップデート"));
        }

        [UnityTest]
        public IEnumerator ConnectedEmptyStepsKeepsReadableNoticeAndJsonAccessible()
        {
            provider.Requirements = new HealthRequirementState(
                HealthAvailability.Available,
                HealthPermission.Granted,
                HealthStepsDataState.Empty,
                33,
                null,
                true
            );
            provider.EmptySteps = true;
            var startup = presenter.InitializeAsync();
            yield return new WaitUntil(() => startup.IsCompleted);
            yield return Click(view.ConnectButton);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.Ready));
            Assert.That(view.Status.gameObject.activeSelf, Is.True);
            Assert.That(view.Status.text, Does.Contain("このAndroidバージョン"));
            Assert.That(
                view.Status.rectTransform.rect.height + 1,
                Is.GreaterThanOrEqualTo(view.Status.preferredHeight)
            );
            Assert.That(view.RefreshButton.interactable, Is.True);
            yield return Click(view.DayButtons[0]);
            Assert.That(view.DetailsOverlay.activeSelf, Is.True);
            yield return Click(view.CloseButton);
            provider.EmptySteps = false;
            yield return Click(view.RefreshButton);
            Assert.That(presenter.RequirementNotice.HasNotice, Is.False);
            Assert.That(view.Status.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator PreviewTextRendersWithoutAnAppAndCanReturnToLiveDisplay()
        {
            view.Bind(presenter, preview: true);
            Assert.That(
                view.SignInButton.GetComponentInChildren<TMPro.TMP_Text>(true).text,
                Does.Contain("サンプル")
            );
            Assert.That(view.GoogleStatus.text, Does.Contain("未接続"));
            yield return Click(view.ConnectButton);
            Assert.That(view.Progress.text, Does.Contain("サンプルデータ"));
            Assert.That(view.Footnote.text, Does.Contain("架空"));
            yield return Click(view.SignInButton);
            Assert.That(view.GoogleStatus.text, Does.Contain("Google接続済みのサンプル"));
            yield return Click(view.DayButtons[0]);
            Assert.That(view.DetailsTitle.text, Does.Contain("サンプルJSON"));
            string json = view.JsonText.text;

            // Rebinding the same presenter must also redraw cached day/detail text.
            view.Bind(presenter);
            Assert.That(view.Progress.text, Is.EqualTo("Health Connectに接続済み"));
            Assert.That(view.Footnote.text, Does.Not.Contain("架空"));
            Assert.That(view.GoogleStatus.text, Is.EqualTo(presenter.GoogleMessage));
            Assert.That(view.DetailsTitle.text, Does.Not.Contain("サンプル"));
            Assert.That(view.JsonText.text, Is.EqualTo(json));
            Assert.That(
                view.SignOutButton.GetComponentInChildren<TMPro.TMP_Text>(true).text,
                Is.EqualTo("Google接続を解除")
            );
            yield return Click(view.CloseButton);
            Assert.That(view.DetailsOverlay.activeSelf, Is.False);
        }

        private sealed class Authentication : IGoogleSignInProvider
        {
            public GoogleSignInStatus Result = GoogleSignInStatus.Success;

            public Task<GoogleSignInStatus> SignInAsync(CancellationToken token) =>
                Task.FromResult(Result);

            public Task<bool> SignOutAsync(CancellationToken token) => Task.FromResult(true);
        }

        private sealed class Provider : IHealthDataProvider, IHealthRequirementProvider
        {
            public string ProviderId => "test";
            public HealthPermission Permission = HealthPermission.Granted;
            public bool LongJson;
            public bool LargeSteps;
            public bool AdditionalOnly;
            public bool EmptySteps;
            public HealthRequirementState Requirements;
            public HealthSettingsDestination SettingsDestination;

            public Task<HealthRequirementState> GetRequirementsAsync(CancellationToken token) =>
                Task.FromResult(
                    Requirements
                        ?? new HealthRequirementState(
                            HealthAvailability.Available,
                            Permission,
                            HealthStepsDataState.Present,
                            34,
                            20,
                            true
                        )
                );

            public Task<bool> OpenSettingsAsync(
                HealthSettingsDestination destination,
                CancellationToken token
            )
            {
                SettingsOpened++;
                SettingsDestination = destination;
                return Task.FromResult(true);
            }

            public int Reads,
                PermissionRequests,
                SettingsOpened;

            public Task<HealthAvailability> GetAvailabilityAsync(CancellationToken token) =>
                Task.FromResult(HealthAvailability.Available);

            public Task<HealthPermission> GetPermissionAsync(CancellationToken token) =>
                Task.FromResult(Permission);

            public Task<HealthPermission> RequestPermissionAsync(CancellationToken token)
            {
                PermissionRequests++;
                return Task.FromResult(Permission);
            }

            public Task<HealthReadResult> ReadRecentDaysAsync(CancellationToken token)
            {
                Reads++;
                var days = new JArray();
                for (int i = 0; i < 7; i++)
                {
                    var date = new DateTimeOffset(
                        2026,
                        9,
                        7,
                        0,
                        0,
                        0,
                        TimeSpan.FromHours(9)
                    ).AddDays(i);
                    days.Add(
                        new JObject
                        {
                            ["day"] = date.ToString("yyyy-MM-dd"),
                            ["zone"] = "Asia/Tokyo",
                            ["startAt"] = date.ToString("O"),
                            ["endAt"] = date.AddDays(1).ToString("O"),
                            ["hasValue"] = i != 5 && !EmptySteps,
                            ["steps"] =
                                EmptySteps || i == 5 || i == 4 ? 0
                                : LargeSteps ? long.MaxValue
                                : 6432,
                            ["observedAt"] = "2026-09-13T23:59:00.000+09:00",
                        }
                    );
                }
                if (AdditionalOnly)
                {
                    foreach (var day in days)
                    {
                        day["hasValue"] = false;
                        day["steps"] = 0;
                        day["stepsStatus"] = "permission_required";
                        day["records"] = new JObject
                        {
                            ["weight"] = new JObject
                            {
                                ["status"] = "success",
                                ["records"] = new JArray(
                                    new JObject
                                    {
                                        ["sourceApp"] = "example.health",
                                        ["time"] = day["startAt"].DeepClone(),
                                        ["kilograms"] = 62.5,
                                    }
                                ),
                            },
                        };
                    }
                }
                if (LongJson)
                {
                    var extra = new JArray();
                    for (int i = 0; i < 60; i++)
                        extra.Add(i);
                    days[0]["extra"] = extra;
                    days[0]["longText"] = new string('x', 150) + "日本語";
                }
                return Task.FromResult(
                    new HealthReadResult(
                        HealthReadStatus.Success,
                        rawJson: new JObject { ["status"] = "success", ["days"] = days }.ToString()
                    )
                );
            }

            public void OpenSettings() => SettingsOpened++;
        }
    }

    // Unity runs this only for tests; the temporary Resources copy is removed afterwards.
    public sealed class HealthScreenTestAssets : IPrebuildSetup, IPostBuildCleanup
    {
        public const string Source = "Assets/Baryonyx/Features/Health/UI/HealthScreen.prefab";
        private const string Folder = "Assets/Baryonyx/Features/Health/Tests/PlayMode/Resources";
        private const string Destination = Folder + "/BaryonyxHealthScreenTest.prefab";

        public void Setup()
        {
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Destination) != null)
                throw new InvalidOperationException(
                    "Remove the leftover health test prefab before running tests."
                );
            System.IO.Directory.CreateDirectory(Folder);
            UnityEditor.AssetDatabase.Refresh();
            if (!UnityEditor.AssetDatabase.CopyAsset(Source, Destination))
                throw new InvalidOperationException("Could not prepare the health test screen.");
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
