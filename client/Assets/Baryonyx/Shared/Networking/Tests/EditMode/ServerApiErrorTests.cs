using Baryonyx.Networking;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 断られた要求の本文から、サーバーが返したエラーの種類を読む（server/src/shared/http.ts）。
    public sealed class ServerApiErrorTests
    {
        [Test]
        public void TheKindOfErrorComesFromTheBody()
        {
            Assert.That(
                ServerApi.ErrorOf("{\"error\":\"destination_locked\"}"),
                Is.EqualTo("destination_locked")
            );
        }

        // 接続の失敗や、サーバーのものでない応答には、種類がない。
        [TestCase(null)]
        [TestCase("")]
        [TestCase("{}")]
        [TestCase("<html>Bad Gateway</html>")]
        public void ABodyWithoutAnErrorHasNoKind(string body)
        {
            Assert.That(ServerApi.ErrorOf(body), Is.Null);
        }
    }
}
