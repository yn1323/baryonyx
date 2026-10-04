using System.Linq;
using Baryonyx.Party;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class PartySourceTests
    {
        // サーバーの応答（server/src/features/party/routes.ts）を JsonUtility で読んだ形。
        private const string Response =
            "{\"change\":\"join\","
            + "\"characters\":["
            + "{\"id\":\"toma\",\"level\":14,\"cards\":[\"Fire\",null,\"Ice\",\"\"]},"
            + "{\"id\":\"\",\"level\":3,\"cards\":[]},"
            + "{\"id\":\"luka\",\"level\":0}],"
            + "\"slots\":["
            + "{\"slot\":0,\"characterId\":\"toma\"},"
            + "{\"slot\":1,\"characterId\":null},"
            + "{\"slot\":2,\"characterId\":\"\"},"
            + "{\"slot\":7,\"characterId\":\"luka\"}],"
            + "\"runes\":4500,"
            + "\"rules\":{\"maxLevel\":30,\"costPerLevel\":100}}";

        [Test]
        public void TheServersPartyBecomesTheState()
        {
            var state = PartyServerSource.ToState(
                JsonUtility.FromJson<PartyApiClient.State>(Response)
            );

            // IDのないキャラは外し、Lvは1以上にする。空いたカードの枠はnull。
            Assert.That(state.Owned.Select(c => c.Id), Is.EqualTo(new[] { "toma", "luka" }));
            Assert.That(state.Find("toma").Level, Is.EqualTo(14));
            Assert.That(state.Find("toma").Cards, Is.EqualTo(new[] { "Fire", null, "Ice", null }));
            Assert.That(state.Find("luka").Level, Is.EqualTo(1));
            Assert.That(state.Find("luka").Cards, Is.Empty);
            Assert.That(state.Find("nobody"), Is.Null);

            // 枠は番号の位置へ4つ並べ、範囲外の枠は捨てる。
            Assert.That(state.Slots, Is.EqualTo(new[] { "toma", null, null, null }));
            Assert.That(state.Runes, Is.EqualTo(4500));
            Assert.That(state.MaxLevel, Is.EqualTo(30));
            Assert.That(state.CostPerLevel, Is.EqualTo(100));
        }

        [Test]
        public void AnEmptyAnswerIsAnEmptyParty()
        {
            var state = PartyServerSource.ToState(null);

            Assert.That(state.Owned, Is.Empty);
            Assert.That(state.Slots, Is.EqualTo(new string[PartyFormation.Size]));
            Assert.That(state.Runes, Is.EqualTo(0));
            Assert.That(state.MaxLevel, Is.EqualTo(0));
        }
    }
}
