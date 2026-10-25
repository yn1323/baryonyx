using System.Collections;
using System.IO;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Combat;
using Baryonyx.Party;
using Baryonyx.Training;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    /// <summary>
    /// The automatic battles that measure the battle's balance (doc/features/stage-progression.md,
    /// 戦闘の試し; .agents/skills/battle-balance). <see cref="RunsTheRequestedBattles"/> fights
    /// what client/Temp/battle-balance/request.json asks for and writes the result beside it. It
    /// takes the request first (moving it to request.taken.json), so a request left behind does not
    /// run again in a later run of every test; with no request it does nothing and passes. The
    /// other test checks that one battle is fought to its end.
    /// </summary>
    public sealed class BattleBalanceTests
    {
        public static readonly string Folder = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Temp", "battle-balance")
        );
        public static readonly string RequestPath = Path.Combine(Folder, "request.json");
        public static readonly string TakenPath = Path.Combine(Folder, "request.taken.json");
        public static readonly string ResultPath = Path.Combine(Folder, "result.json");
        public static readonly string TablePath = Path.Combine(Folder, "result.md");

        // テストは全体で、会心なし・状態異常は必ず付くに固定している（BattleOddsSetUp）。自動対戦が変えた確率を戻す。
        [TearDown]
        public void FixOdds() => BattleOdds.Fix(critical: false, lands: true);

        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator RunsTheRequestedBattles()
        {
            if (!File.Exists(RequestPath))
                Assert.Pass($"No request at {RequestPath}.");
            var request = BattleBalanceRequest.FromJson(File.ReadAllText(RequestPath));
            File.Copy(RequestPath, TakenPath, true);
            File.Delete(RequestPath);
            var session = new BattleBalanceSession(request, Party(), Training());
            yield return session.Run();
            var result = session.Result;
            File.WriteAllText(ResultPath, result.ToJson());
            File.WriteAllText(TablePath, result.ToMarkdown());
            Debug.Log("[balance] done\n" + result.ToMarkdown());
            Assert.That(result.overall.battles, Is.GreaterThan(0), "No battle was fought.");
        }

        // 森の魔物の1組と、確率を固定して1回戦う。敵が失ったHPは、始めのHPと回復した分の和になる。
        [UnityTest]
        public IEnumerator AForestPackIsFoughtToItsEndAndCounted()
        {
            var request = new BattleBalanceRequest
            {
                room = "pack",
                variants = new[] { 0 },
                repeats = 1,
                odds = "fixed",
            };
            var session = new BattleBalanceSession(request, Party(), Training());
            yield return session.Run();

            var battle = session.Result.battles[0];
            Assert.That(battle.enemies, Is.EqualTo("moss-slime+moss-wolf"));
            Assert.That(battle.outcome, Is.EqualTo(BattleBalanceRecord.Victory), battle.enemyStats);
            Assert.That(battle.turns, Is.InRange(1, 10));
            Assert.That(battle.cards, Is.GreaterThan(0));
            Assert.That(battle.dealt, Is.EqualTo(battle.enemyHp + battle.enemyHealed));
            Assert.That(battle.hpEnd, Is.InRange(0.01f, 1f));
            Assert.That(session.Result.overall.winRate, Is.EqualTo(1f));
        }

        // 同じ種なら、敵の行動・山札・会心・状態異常・装備の2回発動まで同じに進み、同じ記録になる。
        // 味方が倒れやすい組で確かめる（倒れたときの演出が乱数の順番を変えないこと）。
        [UnityTest]
        public IEnumerator TheSameSeedFightsTheSameBattleAgain()
        {
            var request = new BattleBalanceRequest
            {
                room = "elite",
                variants = new[] { 2 },
                repeats = 1,
                seed = 5,
                equipment = new[]
                {
                    new BattleBalanceGear
                    {
                        slot = 0,
                        equipment = "flame-dagger",
                        multiplier = 1.8f,
                    },
                },
            };
            var first = new BattleBalanceSession(request, Party(), Training());
            yield return first.Run();
            var again = new BattleBalanceSession(request, Party(), Training());
            yield return again.Run();

            var a = first.Result.battles[0];
            var b = again.Result.battles[0];
            // 2回発動の装備を付けた仲間が1人いる。
            Assert.That(
                a.partyStats.Split(" ; ").Count(member => !member.EndsWith(" 2回0%")),
                Is.EqualTo(1),
                a.partyStats
            );
            Assert.That(
                (
                    b.outcome,
                    b.turns,
                    b.cards,
                    b.dealt,
                    b.taken,
                    b.partyHealed,
                    b.enemyHealed,
                    b.fell
                ),
                Is.EqualTo(
                    (
                        a.outcome,
                        a.turns,
                        a.cards,
                        a.dealt,
                        a.taken,
                        a.partyHealed,
                        a.enemyHealed,
                        a.fell
                    )
                )
            );
            Assert.That(b.dealtByTurn, Is.EqualTo(a.dealtByTurn));
        }

        private static PartyMockData Party() =>
            Load<PartyMockData>("Assets/Baryonyx/Features/Party/Data/PartyMockData.asset");

        private static TrainingMockData Training() =>
            Load<TrainingMockData>("Assets/Baryonyx/Features/Training/Data/TrainingMockData.asset");

        private static T Load<T>(string path)
            where T : Object
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
#else
            T asset = null;
#endif
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }
    }
}
