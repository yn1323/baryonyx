using System;
using Baryonyx.Networking;
using Baryonyx.Temple;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 召喚できなかったときの知らせ（doc/features/monetization.md の召喚の保存）。
    public sealed class TempleSummonFailureTests
    {
        [Test]
        public void OnlyARefusalForShortRunesTellsTheRunesAreShort()
        {
            Assert.That(
                TempleSummonSession.FailureMessageOf(
                    new ServerApiException(409, "insufficient_runes")
                ),
                Is.EqualTo(TempleSummonSession.ShortMessage)
            );
            // 要求IDの使い回しも409で断られるが、ルーンが足りないわけではない。
            Assert.That(
                TempleSummonSession.FailureMessageOf(
                    new ServerApiException(409, "request_conflict")
                ),
                Is.EqualTo(TempleSummonSession.FailedMessage)
            );
            Assert.That(
                TempleSummonSession.FailureMessageOf(new ServerApiException(0)),
                Is.EqualTo(TempleSummonSession.FailedMessage)
            );
            Assert.That(
                TempleSummonSession.FailureMessageOf(new InvalidOperationException()),
                Is.EqualTo(TempleSummonSession.FailedMessage)
            );
        }
    }
}
