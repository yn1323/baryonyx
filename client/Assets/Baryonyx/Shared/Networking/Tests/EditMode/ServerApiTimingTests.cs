using Baryonyx.Networking;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class ServerApiTimingTests
    {
        // サーバーの合計とDBの往復を、読める形にして要求の時間の後ろへ並べる。
        [Test]
        public void TheLineShowsTheServerTimeAndItsDatabaseRoundTrips()
        {
            var line = ServerApi.TimingLine(
                "GET",
                "/v1/party",
                200,
                412.4,
                "d1;dur=14.1;desc=\"4 calls\",total;dur=109.8;desc=\"API\""
            );

            Assert.That(
                line,
                Is.EqualTo("[計測] 通信 GET /v1/party 200 412ms（サーバー 110ms、DB 4回 14ms）")
            );
        }

        // Server-Timing を返さないサーバー（古いサーバーや接続の失敗）では、要求の時間だけを書く。
        [Test]
        public void WithoutServerTimingTheLineHasOnlyTheRequestTime()
        {
            Assert.That(
                ServerApi.TimingLine("PUT", "/v1/party/slots/0", 0, 30000, null),
                Is.EqualTo("[計測] 通信 PUT /v1/party/slots/0 0 30000ms")
            );
        }

        // 読めない形なら、受け取ったまま書く。
        [Test]
        public void AnUnknownServerTimingIsWrittenAsItCame()
        {
            Assert.That(
                ServerApi.TimingLine("GET", "/v1/library", 200, 80, "edge;dur=3"),
                Is.EqualTo("[計測] 通信 GET /v1/library 200 80ms（サーバー edge;dur=3）")
            );
        }
    }
}
