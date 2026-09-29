using Baryonyx.App;
using Baryonyx.Editor.CI;
using Baryonyx.Health;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class ServerEndpointTests
    {
        private const string Dev = "https://dev.example.com";
        private const string Prod = "https://prod.example.com";
        private HealthConnectionSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<HealthConnectionSettings>();
            settings.DevServerUrl = Dev;
            settings.ProdServerUrl = Prod;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        [TestCase(ServerEnvironment.Local, ServerEndpoint.LocalUrl)]
        [TestCase(ServerEnvironment.Dev, Dev)]
        [TestCase(ServerEnvironment.Prod, Prod)]
        public void EditorUsesTheSelectedEnvironmentAndIgnoresTheBuildValue(
            ServerEnvironment environment,
            string expected
        )
        {
            settings.BuildServerUrl = "https://left-over.example.com";
            Assert.That(ServerEndpoint.Resolve(settings, true, environment), Is.EqualTo(expected));
        }

        [Test]
        public void PlayerUsesTheBuildValueAndFallsBackToDev()
        {
            Assert.That(
                ServerEndpoint.Resolve(settings, false, ServerEnvironment.Local),
                Is.EqualTo(Dev)
            );
            settings.BuildServerUrl = Prod;
            Assert.That(
                ServerEndpoint.Resolve(settings, false, ServerEnvironment.Local),
                Is.EqualTo(Prod)
            );
        }

        [TestCase(null, "dev", Dev)]
        [TestCase("", "dev", Dev)]
        [TestCase("dev", "dev", Dev)]
        [TestCase("preview", "preview", Dev)]
        [TestCase(" PROD ", "prod", Prod)]
        public void BuildSelectsTheEnvironmentUrl(string environment, string name, string url)
        {
            Assert.That(
                ServerBuildEndpoint.Resolve(settings, environment, null),
                Is.EqualTo((name, url))
            );
        }

        [Test]
        public void ExplicitBuildUrlWinsForTheEmulator()
        {
            Assert.That(
                ServerBuildEndpoint.Resolve(settings, "prod", " http://10.0.2.2:4000 "),
                Is.EqualTo(("custom", "http://10.0.2.2:4000"))
            );
        }

        [TestCase("staging", null)]
        [TestCase("local", null)]
        [TestCase("dev", "http://192.168.0.10:4000")]
        public void UnusableBuildSettingsStopTheBuild(string environment, string url)
        {
            Assert.Throws<BuildFailedException>(() =>
                ServerBuildEndpoint.Resolve(settings, environment, url)
            );
        }

        [Test]
        public void MissingProdUrlStopsAProdBuild()
        {
            settings.ProdServerUrl = "";
            Assert.Throws<BuildFailedException>(() =>
                ServerBuildEndpoint.Resolve(settings, "prod", null)
            );
        }

        [Test]
        public void RepositorySettingsKeepTheBuildValueEmpty()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<HealthConnectionSettings>(
                AndroidBuild.SettingsPath
            );
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.BuildServerUrl, Is.Empty);
            Assert.That(asset.DevServerUrl, Does.StartWith("https://"));
        }
    }
}
