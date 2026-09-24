using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.Health
{
    public sealed class HealthConnectProvider : IHealthDataProvider, IHealthRequirementProvider
    {
        public string ProviderId => "health_connect";

        public async Task<HealthRequirementState> GetRequirementsAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int? apiLevel = null;
            int? extension = null;
            bool? hasStepCounter = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                apiLevel = version.GetStatic<int>("SDK_INT");
                if (apiLevel >= 34)
                {
                    using var extensions = new AndroidJavaClass("android.os.ext.SdkExtensions");
                    extension = extensions.CallStatic<int>("getExtensionVersion", 34);
                }
            }
            catch (AndroidJavaException) { }
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var packages = activity.Call<AndroidJavaObject>("getPackageManager");
                hasStepCounter = packages.Call<bool>(
                    "hasSystemFeature",
                    "android.hardware.sensor.stepcounter"
                );
            }
            catch (AndroidJavaException) { }
#endif
            var reply = await CallAsync("requirements", token);
            HealthAvailability? availability = reply.status switch
            {
                "unavailable" => HealthAvailability.Unavailable,
                "update_required" => HealthAvailability.UpdateRequired,
                "steps_available" or "steps_empty" or "steps_failed" or "permission_required" =>
                    HealthAvailability.Available,
                _ => null,
            };
            var permission = reply.status switch
            {
                "steps_available" or "steps_empty" or "steps_failed" => HealthPermission.Granted,
                "permission_required" => HealthPermission.NotGranted,
                _ => HealthPermission.Unknown,
            };
            var data = reply.status switch
            {
                "steps_available" => HealthStepsDataState.Present,
                "steps_empty" => HealthStepsDataState.Empty,
                "steps_failed" => HealthStepsDataState.Failed,
                _ => HealthStepsDataState.NotChecked,
            };
            return new HealthRequirementState(
                availability,
                permission,
                data,
                apiLevel,
                extension,
                hasStepCounter
            );
        }

        public async Task<bool> OpenSettingsAsync(
            HealthSettingsDestination destination,
            CancellationToken token
        )
        {
            var reply = await CallAsync(
                destination == HealthSettingsDestination.Device
                    ? "openSystemSettings"
                    : "openHealthSettings",
                token
            );
            return reply.status == "settings_opened";
        }

        public async Task<HealthAvailability> GetAvailabilityAsync(CancellationToken token)
        {
            var reply = await CallAsync("availability", token);
            return reply.status == "available" ? HealthAvailability.Available
                : reply.status == "update_required" ? HealthAvailability.UpdateRequired
                : reply.status == "unavailable" ? HealthAvailability.Unavailable
                : throw new InvalidOperationException("Health Connect availability check failed.");
        }

        public async Task<HealthPermission> GetPermissionAsync(CancellationToken token) =>
            Permission(await CallAsync("permission", token));

        public async Task<HealthPermission> RequestPermissionAsync(CancellationToken token) =>
            Permission(await CallAsync("requestPermission", token));

        public async Task<HealthPermission> GetStepsPermissionAsync(CancellationToken token) =>
            Permission(await CallAsync("stepsPermission", token));

        public async Task<HealthPermission> RequestStepsPermissionAsync(CancellationToken token) =>
            Permission(await CallAsync("requestStepsPermission", token));

        private static HealthPermission Permission(Reply reply) =>
            reply.status == "granted" ? HealthPermission.Granted
            : reply.status == "not_granted" ? HealthPermission.NotGranted
            : reply.status == "unavailable" ? HealthPermission.Unknown
            : throw new InvalidOperationException("Health Connect permission check failed.");

        public async Task<HealthReadResult> ReadRecentDaysAsync(CancellationToken token)
        {
            var reply = await CallAsync("read", token);
            return new HealthReadResult(
                reply.status == "success" ? HealthReadStatus.Success
                    : reply.status == "permission_required" ? HealthReadStatus.PermissionRequired
                    : reply.status == "unavailable" ? HealthReadStatus.Unavailable
                    : HealthReadStatus.Failed,
                reply.days,
                reply.rawJson
            );
        }

        public void OpenSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var bridge = new AndroidJavaClass("com.baryonyx.health.HealthBridge");
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            bridge.CallStatic("openSettings", activity);
#endif
        }

        private static async Task<Reply> CallAsync(string operation, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
#if UNITY_ANDROID && !UNITY_EDITOR
            var context =
                SynchronizationContext.Current
                ?? throw new InvalidOperationException(
                    "Call Health Connect from the Unity main thread."
                );
            var completion = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            string requestId = Guid.NewGuid().ToString();
            var callback = new Callback(completion);
            using var bridge = new AndroidJavaClass("com.baryonyx.health.HealthBridge");
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var cancellation = token.Register(() =>
            {
                completion.TrySetCanceled(token);
                context.Post(
                    _ =>
                    {
                        using var cancelledBridge = new AndroidJavaClass(
                            "com.baryonyx.health.HealthBridge"
                        );
                        cancelledBridge.CallStatic("cancel", requestId);
                    },
                    null
                );
            });
            bridge.CallStatic("execute", activity, operation, requestId, callback);
            string json = await completion.Task;
            GC.KeepAlive(callback);
            token.ThrowIfCancellationRequested();
            var reply = JsonUtility.FromJson<Reply>(json);
            reply.rawJson = json;
            return reply;
#else
            await Task.CompletedTask;
            return new Reply { status = "unavailable" };
#endif
        }

        [Serializable]
        private sealed class Reply
        {
            public string status;
            public HealthDay[] days;

            [NonSerialized]
            public string rawJson;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private sealed class Callback : AndroidJavaProxy
        {
            private readonly TaskCompletionSource<string> completion;

            public Callback(TaskCompletionSource<string> completion)
                : base("com.baryonyx.health.HealthCallback")
            {
                this.completion = completion;
            }

            [UnityEngine.Scripting.Preserve]
            public void complete(string json)
            {
                completion.TrySetResult(json);
            }
        }
#endif
    }
}
