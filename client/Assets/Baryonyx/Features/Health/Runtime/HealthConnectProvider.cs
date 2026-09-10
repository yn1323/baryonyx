using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.Health
{
    public sealed class HealthConnectProvider : IHealthDataProvider
    {
        public string ProviderId => "health_connect";

        public async Task<HealthAvailability> GetAvailabilityAsync(CancellationToken token)
        {
            var reply = await CallAsync("availability", token);
            return reply.status == "available" ? HealthAvailability.Available
                : reply.status == "update_required" ? HealthAvailability.UpdateRequired
                : HealthAvailability.Unavailable;
        }

        public async Task<HealthPermission> GetPermissionAsync(CancellationToken token) =>
            Permission(await CallAsync("permission", token));

        public async Task<HealthPermission> RequestPermissionAsync(CancellationToken token) =>
            Permission(await CallAsync("requestPermission", token));

        private static HealthPermission Permission(Reply reply) =>
            reply.status == "granted" ? HealthPermission.Granted : HealthPermission.NotGranted;

        public async Task<HealthReadResult> ReadRecentDaysAsync(CancellationToken token)
        {
            var reply = await CallAsync("read", token);
            return new HealthReadResult(
                reply.status == "success" ? HealthReadStatus.Success
                    : reply.status == "permission_required" ? HealthReadStatus.PermissionRequired
                    : reply.status == "unavailable" ? HealthReadStatus.Unavailable
                    : HealthReadStatus.Failed,
                reply.days
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
            return JsonUtility.FromJson<Reply>(json);
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
