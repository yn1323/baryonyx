using System;
using Baryonyx.Account;
using Baryonyx.Networking;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class GuestCredentialTests
    {
        [Test]
        public void CreatedSecretsMatchTheServerFormatAndDiffer()
        {
            var first = GuestCredential.Create();
            var second = GuestCredential.Create();
            Assert.That(first, Does.Match("^[a-f0-9]{64}$"));
            Assert.That(GuestCredential.IsValid(first), Is.True);
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
        [TestCase("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde")]
        [TestCase("g123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
        public void MalformedSecretsAreReplaced(string secret)
        {
            Assert.That(GuestCredential.IsValid(secret), Is.False);
        }

        [TestCase("https://baryonyx-server-dev.croissant-lab.workers.dev")]
        [TestCase("http://127.0.0.1:4000")]
        [TestCase("http://localhost:4000")]
        [TestCase("http://10.0.2.2:4000")]
        public void ServerAcceptsHttpsAndLocalDevelopmentHosts(string url)
        {
            Assert.DoesNotThrow(() => new ServerApi(url));
        }

        [TestCase("http://192.168.0.10:4000")]
        [TestCase("http://example.com")]
        public void ServerRejectsPlainHttpElsewhere(string url)
        {
            Assert.Throws<ArgumentException>(() => new ServerApi(url));
        }
    }
}
