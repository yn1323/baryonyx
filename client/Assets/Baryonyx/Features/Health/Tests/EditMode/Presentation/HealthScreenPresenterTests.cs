using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Health;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class HealthScreenPresenterTests
    {
        private Authentication authentication;
        private Provider provider;
        private HealthScreenPresenter presenter;

        [SetUp]
        public void SetUp()
        {
            authentication = new Authentication();
            provider = new Provider();
            presenter = new HealthScreenPresenter(authentication, provider);
        }

        [TearDown]
        public void TearDown() => presenter.Dispose();

        [Test]
        public async Task HealthConnectWorksWithoutGoogleAndGoogleDoesNotAutoConnect()
        {
            Assert.That(presenter.CanSignIn, Is.True);
            Assert.That(presenter.CanConnect, Is.True);
            await presenter.SignInAsync();
            Assert.That(provider.AvailabilityChecks, Is.Zero);
            await presenter.SignOutAsync();
            await presenter.ConnectAsync();
            Assert.That(authentication.Calls, Is.EqualTo(1));
            Assert.That(presenter.SignedIn, Is.False);
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
            presenter.SelectDay(0);
            Assert.That(presenter.SelectedDay, Is.Not.Null);
            var days = presenter.Days;
            await presenter.SignInAsync();
            Assert.That(presenter.Days, Is.SameAs(days));
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.Ready));
            Assert.That(provider.Reads, Is.EqualTo(1));
        }

        [TestCase(GoogleSignInStatus.Incomplete)]
        [TestCase(GoogleSignInStatus.NotConfigured)]
        [TestCase(GoogleSignInStatus.Unsupported)]
        public async Task IncompleteAuthenticationRemainsRetryable(GoogleSignInStatus result)
        {
            authentication.Result = result;
            await presenter.SignInAsync();
            Assert.That(presenter.SignedIn, Is.False);
            Assert.That(presenter.CanSignIn, Is.True);
            Assert.That(presenter.CanConnect, Is.True);
            Assert.That(provider.Reads, Is.Zero);
            await presenter.ConnectAsync();
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
        }

        [Test]
        public async Task PermissionDenialPreservesAuthenticationAndSettingsRoute()
        {
            provider.Permission = HealthPermission.NotGranted;
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            Assert.That(presenter.SignedIn, Is.True);
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.PermissionRequired));
            Assert.That(presenter.Days, Is.Empty);
            Assert.That(provider.Reads, Is.Zero);
            presenter.OpenSettings();
            Assert.That(provider.SettingsOpened, Is.EqualTo(1));
            provider.Permission = HealthPermission.Granted;
            await presenter.ConnectAsync();
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
        }

        [TestCase(HealthAvailability.Unavailable, HealthScreenPhase.Unavailable)]
        [TestCase(HealthAvailability.UpdateRequired, HealthScreenPhase.UpdateRequired)]
        public async Task UnavailableProvidersDoNotRequestPermission(
            HealthAvailability availability,
            HealthScreenPhase phase
        )
        {
            provider.Availability = availability;
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            Assert.That(presenter.Phase, Is.EqualTo(phase));
            Assert.That(provider.PermissionRequests, Is.Zero);
            Assert.That(provider.Reads, Is.Zero);
        }

        [Test]
        public async Task FailedAvailabilityCheckIsRetryable()
        {
            provider.ThrowOnAvailability = true;
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.Failed));
            Assert.That(presenter.CanConnect, Is.True);
            provider.ThrowOnAvailability = false;
            await presenter.ConnectAsync();
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.Ready));
        }

        [Test]
        public async Task RapidClicksLaunchOnlyOneAuthenticationAndRead()
        {
            authentication.Pending = new TaskCompletionSource<GoogleSignInStatus>();
            var signIn = presenter.SignInAsync();
            await presenter.SignInAsync();
            Assert.That(authentication.Calls, Is.EqualTo(1));
            authentication.Pending.SetResult(GoogleSignInStatus.Success);
            await signIn;
            provider.Pending = new TaskCompletionSource<HealthReadResult>();
            var read = presenter.ConnectAsync();
            await presenter.ConnectAsync();
            await presenter.RefreshAsync();
            Assert.That(provider.Reads, Is.EqualTo(1));
            provider.Pending.SetResult(Week());
            await read;
        }

        [Test]
        public async Task GoogleSignOutDoesNotCancelHealthRead()
        {
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            presenter.SelectDay(0);
            provider.Pending = new TaskCompletionSource<HealthReadResult>();
            var read = presenter.RefreshAsync();
            var signOut = presenter.SignOutAsync();
            Assert.That(presenter.Days, Is.Empty);
            Assert.That(presenter.SelectedDay, Is.Null);
            Assert.That(presenter.SignedIn, Is.False);
            provider.Pending.SetResult(Week());
            await read;
            await signOut;
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
            Assert.That(presenter.CanRefresh, Is.True);
            Assert.That(presenter.CanSignIn, Is.True);
        }

        [Test]
        public async Task GoogleSignOutPreservesHealthSnapshots()
        {
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            presenter.SelectDay(0);
            var days = presenter.Days;
            var selected = presenter.SelectedDay;
            await presenter.SignOutAsync();
            Assert.That(presenter.SignedIn, Is.False);
            Assert.That(presenter.Days, Is.SameAs(days));
            Assert.That(presenter.SelectedDay, Is.SameAs(selected));
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.Ready));
            Assert.That(presenter.CanRefresh, Is.True);
        }

        [TestCase(GoogleSignInStatus.Incomplete)]
        [TestCase(GoogleSignInStatus.NotConfigured)]
        [TestCase(GoogleSignInStatus.Unsupported)]
        public async Task GoogleFailurePreservesHealthState(GoogleSignInStatus result)
        {
            await presenter.ConnectAsync();
            var days = presenter.Days;
            var message = presenter.Message;
            authentication.Result = result;
            await presenter.SignInAsync();
            Assert.That(presenter.SignedIn, Is.False);
            Assert.That(presenter.Days, Is.SameAs(days));
            Assert.That(presenter.Message, Is.EqualTo(message));
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.Ready));
            Assert.That(presenter.CanRefresh, Is.True);
        }

        [Test]
        public async Task GoogleExceptionPreservesHealthState()
        {
            await presenter.ConnectAsync();
            var days = presenter.Days;
            authentication.Pending = new TaskCompletionSource<GoogleSignInStatus>();
            var pending = presenter.SignInAsync();
            authentication.Pending.SetException(new InvalidOperationException());
            await pending;
            Assert.That(presenter.Days, Is.SameAs(days));
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.Ready));
            Assert.That(presenter.GoogleMessage, Does.Contain("Google接続を完了できません"));
        }

        [TestCase(GoogleSignInStatus.Success)]
        [TestCase(GoogleSignInStatus.Incomplete)]
        public async Task GoogleDialogRechecksHealthPermissionOnReturn(GoogleSignInStatus result)
        {
            await presenter.ConnectAsync();
            authentication.Pending = new TaskCompletionSource<GoogleSignInStatus>();
            var pending = presenter.SignInAsync();
            presenter.SetForeground(false);
            Assert.That(presenter.Days, Is.Empty);
            provider.Permission = HealthPermission.NotGranted;
            authentication.Pending.SetResult(result);
            presenter.SetForeground(true);
            await pending;
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.PermissionRequired));
            Assert.That(presenter.CanOpenSettings, Is.True);
            Assert.That(provider.Reads, Is.EqualTo(1));
            Assert.That(provider.PermissionRequests, Is.EqualTo(1));
        }

        [Test]
        public async Task HealthConnectionRestoresOnResumeWithoutGoogle()
        {
            await presenter.ConnectAsync();
            presenter.SetForeground(false);
            Assert.That(presenter.Days, Is.Empty);
            presenter.SetForeground(true);
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
            Assert.That(provider.Reads, Is.EqualTo(2));
            Assert.That(authentication.Calls, Is.Zero);
        }

        [Test]
        public async Task PermissionRevokedOnResumeClearsDetailsWithoutPromptingAgain()
        {
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            presenter.SelectDay(0);
            presenter.SetForeground(false);
            Assert.That(presenter.Days, Is.Empty);
            Assert.That(presenter.SelectedDay, Is.Null);
            provider.Permission = HealthPermission.NotGranted;
            presenter.SetForeground(true);
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.PermissionRequired));
            Assert.That(provider.PermissionRequests, Is.EqualTo(1));
            Assert.That(presenter.SignedIn, Is.True);
        }

        [Test]
        public async Task SystemPermissionDialogWaitsForForegroundAndReadsOnce()
        {
            provider.Permission = HealthPermission.NotGranted;
            provider.PendingPermission = new TaskCompletionSource<HealthPermission>();
            await presenter.SignInAsync();
            var connect = presenter.ConnectAsync();
            presenter.SetForeground(false);
            provider.PendingPermission.SetResult(HealthPermission.Granted);
            Assert.That(provider.Reads, Is.Zero);
            presenter.SetForeground(true);
            await connect;
            Assert.That(provider.Reads, Is.EqualTo(1));
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
        }

        [Test]
        public async Task PermissionGrantedInSettingsIsReadOnReturnWithoutAnotherPrompt()
        {
            provider.Permission = HealthPermission.NotGranted;
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            int requests = provider.PermissionRequests;
            presenter.SetForeground(false);
            provider.Permission = HealthPermission.Granted;
            presenter.SetForeground(true);
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
            Assert.That(provider.PermissionRequests, Is.EqualTo(requests));
        }

        [Test]
        public async Task RefreshReplacesValuesAndClosesTheOldSnapshot()
        {
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            presenter.SelectDay(0);
            var old = presenter.SelectedDay;
            var json = JObject.Parse(Week().RawJson);
            foreach (var day in json["days"])
            {
                day["hasValue"] = false;
                day["steps"] = 0;
            }
            provider.Result = new HealthReadResult(
                HealthReadStatus.Success,
                rawJson: json.ToString()
            );
            await presenter.RefreshAsync();
            Assert.That(presenter.SelectedDay, Is.Null);
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
            Assert.That(presenter.Days.All(day => !day.HasValue), Is.True);
            Assert.That(presenter.Days[0], Is.Not.SameAs(old));
            Assert.That(presenter.Message, Does.Contain("記録はありません"));
        }

        [Test]
        public async Task BackgroundCancelsReadAndResumeRefreshesExactlyOnce()
        {
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            var pending = new TaskCompletionSource<HealthReadResult>();
            provider.Pending = pending;
            var read = presenter.RefreshAsync();
            presenter.SetForeground(false);
            presenter.SetForeground(true);
            provider.Pending = null;
            pending.SetResult(Week());
            await read;
            Assert.That(provider.Reads, Is.EqualTo(3));
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.Ready));
        }

        [Test]
        public async Task RepeatedBackgroundBeforeCancellationFinishesResumesOnlyOnce()
        {
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            var pending = new TaskCompletionSource<HealthReadResult>();
            provider.Pending = pending;
            var read = presenter.RefreshAsync();
            presenter.SetForeground(false);
            presenter.SetForeground(true);
            presenter.SetForeground(false);
            provider.Pending = null;
            pending.SetResult(Week());
            await read;
            Assert.That(presenter.Days, Is.Empty);
            Assert.That(provider.Reads, Is.EqualTo(2));

            presenter.SetForeground(true);

            Assert.That(provider.Reads, Is.EqualTo(3));
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.Ready));
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
        }

        [Test]
        public async Task DisposeRejectsDelayedAuthentication()
        {
            authentication.Pending = new TaskCompletionSource<GoogleSignInStatus>();
            var pending = presenter.SignInAsync();
            presenter.Dispose();
            authentication.Pending.SetResult(GoogleSignInStatus.Success);
            await pending;
            Assert.That(presenter.SignedIn, Is.False);
            Assert.That(presenter.CanConnect, Is.False);
        }

        [Test]
        public async Task MissingStepsAndMeasuredZeroAreDifferent()
        {
            var json = JObject.Parse(Week().RawJson);
            json["days"][0]["hasValue"] = false;
            json["days"][0]["steps"] = 0;
            json["days"][1]["steps"] = 0;
            provider.Result = new HealthReadResult(
                HealthReadStatus.Success,
                rawJson: json.ToString()
            );
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            Assert.That(presenter.Days.Single(x => x.Day == "2026-09-07").HasValue, Is.False);
            var zero = presenter.Days.Single(x => x.Day == "2026-09-08");
            Assert.That(zero.HasValue, Is.True);
            Assert.That(zero.Steps, Is.Zero);
        }

        [Test]
        public async Task ExplicitConnectChecksNewPermissionsButRefreshDoesNotPrompt()
        {
            await presenter.ConnectAsync();
            Assert.That(provider.PermissionRequests, Is.EqualTo(1));
            Assert.That(presenter.CanOpenSettings, Is.True);
            presenter.OpenSettings();
            Assert.That(provider.SettingsOpened, Is.EqualTo(1));
            await presenter.RefreshAsync();
            Assert.That(provider.PermissionRequests, Is.EqualTo(1));
            Assert.That(provider.Reads, Is.EqualTo(2));
        }

        [Test]
        public async Task AdditionalRecordsRemainAvailableWithoutStepPermission()
        {
            var json = JObject.Parse(Week().RawJson);
            foreach (var day in json["days"])
            {
                day["hasValue"] = false;
                day["steps"] = 0;
                day["stepsStatus"] = "permission_required";
                day["records"] = new JObject
                {
                    ["bloodPressure"] = new JObject
                    {
                        ["status"] = "success",
                        ["truncated"] = false,
                        ["records"] = new JArray(
                            new JObject
                            {
                                ["sourceApp"] = "example.health",
                                ["systolicMmHg"] = 118,
                                ["diastolicMmHg"] = 76,
                                ["time"] = day["startAt"].DeepClone(),
                            }
                        ),
                    },
                };
            }
            provider.Result = new HealthReadResult(
                HealthReadStatus.Success,
                rawJson: json.ToString()
            );
            await presenter.ConnectAsync();
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.Ready));
            Assert.That(presenter.Days.All(day => !day.HasValue && day.HasAdditionalData), Is.True);
            Assert.That(presenter.HasReadFailures, Is.False);
            Assert.That(presenter.RequiresStepsPermission, Is.True);
            Assert.That(presenter.Message, Does.Contain("歩数の読み取りが未許可"));
            presenter.SelectDay(0);
            var records = JObject.Parse(presenter.SelectedDay.Json)["records"];
            Assert.That(JToken.DeepEquals(records, json["days"][6]["records"]), Is.True);
            Assert.That(presenter.SelectedDay.StepsStatus, Is.EqualTo("permission_required"));
            provider.Result = Week();
            await presenter.ConnectAsync();
            Assert.That(presenter.RequiresStepsPermission, Is.False);
            Assert.That(presenter.Days[0].HasValue, Is.True);
            Assert.That(provider.PermissionRequests, Is.EqualTo(2));
        }

        [Test]
        public async Task FailedAdditionalTypeIsVisibleAndRefreshClearsFailure()
        {
            var json = JObject.Parse(Week().RawJson);
            json["days"][0]["records"] = new JObject
            {
                ["weight"] = new JObject { ["status"] = "failed", ["records"] = new JArray() },
            };
            provider.Result = new HealthReadResult(
                HealthReadStatus.Success,
                rawJson: json.ToString()
            );
            await presenter.ConnectAsync();
            Assert.That(presenter.HasReadFailures, Is.True);
            Assert.That(presenter.Message, Does.Contain("一部の項目"));
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
            provider.Result = Week();
            await presenter.RefreshAsync();
            Assert.That(presenter.HasReadFailures, Is.False);
        }

        [Test]
        public void RawJsonPreservesUnknownNullTimestampAndLargeInteger()
        {
            var json = JObject.Parse(Week().RawJson);
            var original = (JObject)json["days"][6];
            original["extra"] = new JObject
            {
                ["integer"] = 9007199254740993L,
                ["missing"] = JValue.CreateNull(),
                ["text"] = "<b>歩数</b>",
            };
            var snapshot = HealthDaySnapshot.ParseWeek(json.ToString())[0];
            using var input = new System.IO.StringReader(snapshot.Json);
            using var reader = new JsonTextReader(input)
            {
                DateParseHandling = DateParseHandling.None,
            };
            var actual = JObject.Load(reader);
            using var originalInput = new System.IO.StringReader(original.ToString());
            using var originalReader = new JsonTextReader(originalInput)
            {
                DateParseHandling = DateParseHandling.None,
            };
            Assert.That(JToken.DeepEquals(actual, JObject.Load(originalReader)), Is.True);
            Assert.That(actual["extra"]["integer"].Value<long>(), Is.EqualTo(9007199254740993L));
            Assert.That(actual["startAt"].Type, Is.EqualTo(JTokenType.String));
        }

        [TestCase("{}")]
        [TestCase("{\"status\":\"success\",\"days\":[]}")]
        [TestCase("broken")]
        public async Task MalformedJsonNeverDisplaysPartialSuccess(string json)
        {
            provider.Result = new HealthReadResult(HealthReadStatus.Success, rawJson: json);
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.Failed));
            Assert.That(presenter.Days, Is.Empty);
        }

        [Test]
        public void DuplicateDatesAreRejected()
        {
            var root = JObject.Parse(Week().RawJson);
            root["days"][1]["day"] = root["days"][0]["day"].DeepClone();
            Assert.Throws<FormatException>(() => HealthDaySnapshot.ParseWeek(root.ToString()));
        }

        [Test]
        public async Task MissingClientIdDoesNotCreateNativeSdkInEditor()
        {
            using var adapter = new UmothGoogleSignInProvider("");
            Assert.That(
                await adapter.SignInAsync(CancellationToken.None),
                Is.EqualTo(GoogleSignInStatus.NotConfigured)
            );
        }

        private static HealthReadResult Week()
        {
            var days = new JArray();
            for (int i = 0; i < 7; i++)
            {
                var date = new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.FromHours(9)).AddDays(
                    i
                );
                days.Add(
                    new JObject
                    {
                        ["day"] = date.ToString("yyyy-MM-dd"),
                        ["zone"] = "Asia/Tokyo",
                        ["startAt"] = date.ToString("O"),
                        ["endAt"] = date.AddDays(1).ToString("O"),
                        ["hasValue"] = true,
                        ["steps"] = 1200L + i,
                        ["observedAt"] = "2026-09-13T23:59:00.000+09:00",
                    }
                );
            }
            return new HealthReadResult(
                HealthReadStatus.Success,
                rawJson: new JObject { ["status"] = "success", ["days"] = days }.ToString()
            );
        }

        [Test]
        public async Task StartupChecksOnceWithoutAuthenticationOrPermissionRequests()
        {
            provider.Requirements = new HealthRequirementState(
                HealthAvailability.Available,
                HealthPermission.NotGranted
            );
            await presenter.InitializeAsync();
            await presenter.InitializeAsync();
            Assert.That(provider.RequirementChecks, Is.EqualTo(1));
            Assert.That(provider.PermissionRequests, Is.Zero);
            Assert.That(provider.Reads, Is.Zero);
            Assert.That(authentication.Calls, Is.Zero);
            Assert.That(
                presenter.RequirementNotice.Code,
                Is.EqualTo(HealthRequirementCode.StepsPermissionRequired)
            );
            Assert.That(presenter.CanOpenSettings, Is.True);
        }

        [Test]
        public async Task OnlySettingsReturnRepeatsRequirementsAndClearsResolvedNotice()
        {
            provider.Requirements = new HealthRequirementState(
                HealthAvailability.Available,
                HealthPermission.Granted,
                HealthStepsDataState.Empty,
                34,
                19,
                true
            );
            await presenter.InitializeAsync();
            presenter.SetForeground(false);
            presenter.SetForeground(true);
            await presenter.SignInAsync();
            Assert.That(provider.RequirementChecks, Is.EqualTo(1));
            presenter.OpenSettings();
            Assert.That(provider.SettingsDestination, Is.EqualTo(HealthSettingsDestination.Device));
            presenter.SetForeground(true);
            Assert.That(provider.RequirementChecks, Is.EqualTo(1));
            provider.Requirements = new HealthRequirementState(
                HealthAvailability.Available,
                HealthPermission.Granted,
                HealthStepsDataState.Present,
                34,
                20,
                true
            );
            presenter.SetForeground(false);
            presenter.SetForeground(false);
            presenter.SetForeground(true);
            presenter.SetForeground(true);
            Assert.That(provider.RequirementChecks, Is.EqualTo(2));
            Assert.That(presenter.RequirementNotice.HasNotice, Is.False);
        }

        [Test]
        public async Task FailedSettingsLaunchDoesNotTurnLaterAppSwitchIntoSettingsReturn()
        {
            provider.Requirements = new HealthRequirementState(HealthAvailability.Unavailable);
            await presenter.InitializeAsync();
            provider.SettingsSuccess = false;
            presenter.OpenSettings();
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.Failed));
            presenter.SetForeground(false);
            presenter.SetForeground(true);
            Assert.That(provider.RequirementChecks, Is.EqualTo(1));
            provider.SettingsSuccess = true;
            provider.Requirements = new HealthRequirementState(
                HealthAvailability.Available,
                HealthPermission.Granted,
                HealthStepsDataState.Empty,
                34,
                20,
                true
            );
            presenter.OpenSettings();
            presenter.SetForeground(false);
            presenter.SetForeground(true);
            Assert.That(provider.RequirementChecks, Is.EqualTo(2));
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.ReadyToConnect));
            Assert.That(presenter.StatusMessage, Is.EqualTo(presenter.RequirementNotice.Text));
            Assert.That(presenter.StatusMessage, Does.Contain("歩数データがまだありません"));
            provider.Requirements = provider.Requirements.WithHealthState(
                HealthAvailability.Available,
                HealthPermission.Granted,
                HealthStepsDataState.Present
            );
            presenter.OpenSettings();
            presenter.SetForeground(false);
            presenter.SetForeground(true);
            Assert.That(presenter.RequirementNotice.HasNotice, Is.False);
            Assert.That(presenter.StatusMessage, Does.Not.Contain("設定を開けませんでした"));
        }

        [Test]
        public async Task ConnectAndRefreshReuseResultsToUpdateNotice()
        {
            provider.Requirements = new HealthRequirementState(
                HealthAvailability.Available,
                HealthPermission.NotGranted,
                HealthStepsDataState.NotChecked,
                34,
                19,
                true
            );
            await presenter.InitializeAsync();
            var json = JObject.Parse(Week().RawJson);
            foreach (var day in json["days"])
            {
                day["hasValue"] = false;
                day["steps"] = 0;
                day["stepsStatus"] = "empty";
            }
            provider.Result = new HealthReadResult(
                HealthReadStatus.Success,
                rawJson: json.ToString()
            );
            await presenter.ConnectAsync();
            Assert.That(
                presenter.RequirementNotice.Code,
                Is.EqualTo(HealthRequirementCode.SystemUpdateRequiredForCounting)
            );
            provider.Result = Week();
            await presenter.RefreshAsync();
            Assert.That(presenter.RequirementNotice.HasNotice, Is.False);
            Assert.That(provider.RequirementChecks, Is.EqualTo(1));
        }

        [Test]
        public async Task InterruptedStartupIgnoresOldResultAndCompletesAfterReturning()
        {
            provider.PendingRequirements = new TaskCompletionSource<HealthRequirementState>();
            var startup = presenter.InitializeAsync();
            var pending = provider.PendingRequirements;
            presenter.SetForeground(false);
            presenter.SetForeground(true);
            provider.PendingRequirements = null;
            pending.SetResult(new HealthRequirementState(HealthAvailability.Unavailable));
            await startup;
            Assert.That(provider.RequirementChecks, Is.EqualTo(2));
            Assert.That(presenter.RequirementNotice.HasNotice, Is.False);
        }

        [Test]
        public async Task DisposedStartupDoesNotPublishItsLateResult()
        {
            provider.PendingRequirements = new TaskCompletionSource<HealthRequirementState>();
            var startup = presenter.InitializeAsync();
            presenter.Dispose();
            provider.PendingRequirements.SetResult(
                new HealthRequirementState(HealthAvailability.Unavailable)
            );
            await startup;
            Assert.That(provider.RequirementChecks, Is.EqualTo(1));
            Assert.That(presenter.RequirementNotice.HasNotice, Is.False);
        }

        [Test]
        public async Task SettingsReturnWaitsForLaunchCompletionAndRunsOneCheck()
        {
            provider.Requirements = new HealthRequirementState(HealthAvailability.Unavailable);
            await presenter.InitializeAsync();
            provider.PendingSettings = new TaskCompletionSource<bool>();
            presenter.OpenSettings();
            presenter.SetForeground(false);
            presenter.SetForeground(true);
            presenter.SetForeground(true);
            Assert.That(provider.RequirementChecks, Is.EqualTo(1));
            provider.PendingSettings.SetResult(true);
            for (int i = 0; i < 20 && presenter.IsBusy; i++)
                await Task.Yield();
            Assert.That(provider.RequirementChecks, Is.EqualTo(2));
            Assert.That(presenter.IsBusy, Is.False);
        }

        private sealed class Authentication : IGoogleSignInProvider
        {
            public GoogleSignInStatus Result = GoogleSignInStatus.Success;
            public TaskCompletionSource<GoogleSignInStatus> Pending;
            public int Calls;

            public Task<GoogleSignInStatus> SignInAsync(CancellationToken token)
            {
                Calls++;
                return Pending?.Task ?? Task.FromResult(Result);
            }

            public Task<bool> SignOutAsync(CancellationToken token) => Task.FromResult(true);
        }

        private sealed class Provider : IHealthDataProvider, IHealthRequirementProvider
        {
            public string ProviderId => "test";
            public HealthAvailability Availability = HealthAvailability.Available;
            public HealthPermission Permission = HealthPermission.Granted;
            public HealthReadResult Result = Week();
            public TaskCompletionSource<HealthReadResult> Pending;
            public TaskCompletionSource<HealthPermission> PendingPermission;
            public bool ThrowOnAvailability;
            public HealthRequirementState Requirements;
            public TaskCompletionSource<HealthRequirementState> PendingRequirements;
            public TaskCompletionSource<bool> PendingSettings;
            public int RequirementChecks;
            public bool SettingsSuccess = true;
            public HealthSettingsDestination SettingsDestination;

            public Task<HealthRequirementState> GetRequirementsAsync(CancellationToken token)
            {
                RequirementChecks++;
                return PendingRequirements?.Task
                    ?? Task.FromResult(
                        Requirements
                            ?? new HealthRequirementState(
                                Availability,
                                Permission,
                                HealthStepsDataState.Present,
                                34,
                                20,
                                true
                            )
                    );
            }

            public Task<bool> OpenSettingsAsync(
                HealthSettingsDestination destination,
                CancellationToken token
            )
            {
                SettingsOpened++;
                SettingsDestination = destination;
                return PendingSettings?.Task ?? Task.FromResult(SettingsSuccess);
            }

            public int AvailabilityChecks,
                PermissionRequests,
                Reads,
                SettingsOpened;

            public Task<HealthAvailability> GetAvailabilityAsync(CancellationToken token)
            {
                AvailabilityChecks++;
                if (ThrowOnAvailability)
                    throw new InvalidOperationException();
                return Task.FromResult(Availability);
            }

            public Task<HealthPermission> GetPermissionAsync(CancellationToken token) =>
                Task.FromResult(Permission);

            public Task<HealthPermission> RequestPermissionAsync(CancellationToken token)
            {
                PermissionRequests++;
                return PendingPermission?.Task ?? Task.FromResult(Permission);
            }

            public Task<HealthReadResult> ReadRecentDaysAsync(CancellationToken token)
            {
                Reads++;
                return Pending?.Task ?? Task.FromResult(Result);
            }

            public void OpenSettings() => SettingsOpened++;
        }
    }
}
