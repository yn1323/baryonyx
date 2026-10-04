using Baryonyx.App.Editor;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class SeedPlayerMenuTests
    {
        [Test]
        public void TheSecretFollowsTheServerSeedRule()
        {
            // server/scripts/seed.ts の seedGuestSecret("veteran") と同じ値。
            Assert.That(
                SeedPlayerMenu.SecretFor("veteran"),
                Is.EqualTo("9e06b5feea58b4168f165ab808ad4bc9b0255bdf41965ed0fcae2ec79812e7dc")
            );
        }
    }
}
