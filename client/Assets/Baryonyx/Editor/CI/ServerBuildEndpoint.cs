using System;
using Baryonyx.App;
using Baryonyx.Health;
using Baryonyx.Networking;
using UnityEditor.Build;

namespace Baryonyx.Editor.CI
{
    // APKへ入れるゲームサーバーのURLを、環境変数から決める。
    // BARYONYX_SERVER_URL があればそのURL、なければ BARYONYX_ENVIRONMENT（dev・prod・preview、既定dev）の設定値を使う。
    public static class ServerBuildEndpoint
    {
        public const string EnvironmentVariable = "BARYONYX_ENVIRONMENT";
        public const string UrlVariable = "BARYONYX_SERVER_URL";

        public static (string name, string url) Resolve(HealthConnectionSettings settings) =>
            Resolve(
                settings,
                Environment.GetEnvironmentVariable(EnvironmentVariable),
                Environment.GetEnvironmentVariable(UrlVariable)
            );

        public static (string name, string url) Resolve(
            HealthConnectionSettings settings,
            string environment,
            string url
        )
        {
            if (settings == null)
                throw new BuildFailedException("HealthConnectionSettings.asset is missing.");
            string name;
            if (!string.IsNullOrWhiteSpace(url))
            {
                name = "custom";
                url = url.Trim();
            }
            else
            {
                if (!ServerEndpoint.TryParse(environment, out var selected))
                    throw new BuildFailedException(
                        $"{EnvironmentVariable} must be dev, prod or preview: '{environment}'."
                    );
                if (selected == ServerEnvironment.Local)
                    throw new BuildFailedException(
                        $"Use {UrlVariable} to build an APK for a local server."
                    );
                name = string.IsNullOrWhiteSpace(environment)
                    ? "dev"
                    : environment.Trim().ToLowerInvariant();
                url = ServerEndpoint.ForEnvironment(settings, selected);
                if (string.IsNullOrWhiteSpace(url))
                    throw new BuildFailedException(
                        $"The server URL for '{name}' is not set in HealthConnectionSettings."
                    );
            }
            try
            {
                _ = new ServerApi(url);
            }
            catch (Exception exception) when (exception is ArgumentException or UriFormatException)
            {
                throw new BuildFailedException($"Invalid server URL '{url}': {exception.Message}");
            }
            return (name, url);
        }
    }
}
