using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Networking;
using Baryonyx.StepBonus;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class AdventureStateTests
    {
        // アプリの中だけの冒険はすぐに答えるため、完了をそのまま受け取る。
        private static T Run<T>(System.Threading.Tasks.Task<T> task) =>
            task.GetAwaiter().GetResult();

        // サーバーの応答（server/src/features/adventure/routes.ts）を、JsonUtilityで読んだ形から変える。
        [Test]
        public void TheServersAnswerBecomesTheRunAndItsRewards()
        {
            const string json =
                "{\"run\":{\"id\":\"r1\",\"destinationId\":\"forest-ruins\",\"roomId\":\"moss-hall\","
                + "\"roomCleared\":false,\"route\":[\"entrance\",\"moss-hall\"],\"revives\":1,\"reviveCost\":200,"
                + "\"rooms\":[{\"id\":\"entrance\",\"floor\":1,\"kind\":\"start\",\"encounter\":\"\",\"next\":[\"moss-hall\"]},"
                + "{\"id\":\"moss-hall\",\"floor\":2,\"kind\":\"battle\",\"encounter\":\"forest-pack\",\"next\":[\"sanctum\"]},"
                + "{\"id\":\"sanctum\",\"floor\":3,\"kind\":\"boss\",\"encounter\":\"forest-boss\",\"next\":[]}],"
                + "\"rewards\":[{\"roomId\":\"entrance\",\"bonusId\":\"luck\",\"rank\":\"A\",\"outcome\":\"updated\"},"
                + "{\"roomId\":\"x\",\"bonusId\":\"luck\",\"rank\":\"Z\",\"outcome\":\"added\"}]},"
                + "\"records\":[{\"destinationId\":\"forest-ruins\",\"bestFloor\":3,\"clears\":0}],"
                + "\"runes\":4500,\"reward\":null,\"revive\":null}";
            var state = AdventureServerSource.ToState(
                JsonUtility.FromJson<AdventureApiClient.State>(json)
            );

            Assert.That(state.InProgress, Is.True);
            var run = state.Run;
            Assert.That(run.Room.Kind, Is.EqualTo(AdventureRoomKind.Battle));
            Assert.That(run.Floor, Is.EqualTo(2));
            Assert.That(run.InBattle, Is.True);
            Assert.That(run.Exits, Is.Empty);
            Assert.That(run.ReviveCost, Is.EqualTo(200));
            Assert.That(run.DeepestFloor, Is.EqualTo(3));
            // 知らないランクの報酬は外す。
            Assert.That(
                run.Rewards.Select(reward => (reward.BonusId, reward.Rank, reward.Outcome)),
                Is.EqualTo(new[] { ("luck", StepBonusRank.A, AdventureRewardOutcome.Updated) })
            );
            Assert.That(state.Runes, Is.EqualTo(4500));
            Assert.That(state.RecordOf("forest-ruins").BestFloor, Is.EqualTo(3));
            // nullの報酬と復活は、JsonUtilityが空の値で埋めても、なしとして扱う。
            Assert.That(state.Reward, Is.Null);
            Assert.That(state.Result, Is.Null);
            Assert.That(state.RevivedFor, Is.Null);
        }

        [Test]
        public void NoRunIsNoAdventureInProgress()
        {
            var state = AdventureServerSource.ToState(
                JsonUtility.FromJson<AdventureApiClient.State>(
                    "{\"run\":null,\"records\":[],\"runes\":0}"
                )
            );
            Assert.That(state.InProgress, Is.False);
            Assert.That(AdventureServerSource.ToState(null).InProgress, Is.False);
        }

        [Test]
        public void TheResultOfAnEndedAdventureIsRead()
        {
            var state = AdventureServerSource.ToState(
                JsonUtility.FromJson<AdventureApiClient.State>(
                    "{\"run\":null,\"records\":[],\"runes\":0,\"result\":{\"runId\":\"r1\",\"destinationId\":\"forest-ruins\","
                        + "\"status\":\"defeated\",\"floor\":3,\"bestFloor\":4,\"newRecord\":false,\"clears\":1,\"revives\":2,"
                        + "\"rewards\":[]}}"
                )
            );
            Assert.That(state.Result.Status, Is.EqualTo(AdventureEndStatus.Defeated));
            Assert.That(state.Result.Floor, Is.EqualTo(3));
            Assert.That(state.Result.BestFloor, Is.EqualTo(4));
        }

        // 部屋の出来事（宝箱・戦闘）を終えるまで入口は出さず、終えると次の部屋の入口と手掛かりを出す。
        [Test]
        public void TheRoomShowsItsDoorsOnlyOnceItsEventIsDone()
        {
            var source = new AdventureLocalSource(seed: 7);
            var start = Run(source.StartAsync(AdventureLocalSource.ForestRuins, default));
            var entrance = ExplorationRoom.From(start.Run);
            Assert.That(entrance.Location, Is.EqualTo("森の遺跡 B1F"));
            Assert.That(
                entrance.Exits.Select(exit => exit.Id),
                Is.EqualTo(new[] { "moss-hall", "hidden-store" })
            );
            Assert.That(
                entrance.Exits.Select(exit => exit.Hint),
                Is.EqualTo(new[] { "魔物の気配", "宝箱がありそう" })
            );
            Assert.That(entrance.Prompt, Is.EqualTo(ExplorationRoom.ChoosePrompt));
            Assert.That(entrance.StartsBattle, Is.False);

            var treasure = ExplorationRoom.From(Run(source.MoveAsync("hidden-store", default)).Run);
            Assert.That(treasure.Chest, Is.EqualTo(ExplorationChest.Closed));
            Assert.That(treasure.Exits, Is.Empty);
            Assert.That(treasure.Prompt, Is.EqualTo(ExplorationRoom.ChestPrompt));

            var opened = Run(source.ClearAsync("hidden-store", default));
            Assert.That(opened.Reward, Is.Not.Null);
            var after = ExplorationRoom.From(opened.Run);
            Assert.That(after.Chest, Is.EqualTo(ExplorationChest.Open));
            Assert.That(
                after.Exits.Select(exit => exit.Kind),
                Is.EqualTo(new[] { AdventureRoomKind.Battle, AdventureRoomKind.Elite })
            );

            var battle = ExplorationRoom.From(Run(source.MoveAsync("guardian-gate", default)).Run);
            Assert.That(battle.StartsBattle, Is.True);
            Assert.That(battle.Prompt, Is.EqualTo(ExplorationRoom.BattlePrompt));
        }

        // サーバーと同じ決まり：出来事を終えるまで進めない・つながっていない部屋へは進めない。
        [Test]
        public void TheLocalAdventureKeepsTheServersRules()
        {
            var source = new AdventureLocalSource(runes: 250, seed: 3);
            Assert.That(Fails(() => Run(source.MoveAsync("moss-hall", default))), Is.EqualTo(404));
            Run(source.StartAsync(AdventureLocalSource.ForestRuins, default));
            Assert.That(
                Fails(() => Run(source.StartAsync(AdventureLocalSource.ForestRuins, default))),
                Is.Zero
            );
            Assert.That(Fails(() => Run(source.MoveAsync("sanctum", default))), Is.EqualTo(409));
            Run(source.MoveAsync("moss-hall", default));
            Assert.That(
                Fails(() => Run(source.MoveAsync("root-gallery", default))),
                Is.EqualTo(409)
            );

            // 復活は100、2回目は200。足りなければ断る。
            var first = Run(source.ReviveAsync("moss-hall", default));
            Assert.That(first.RevivedFor, Is.EqualTo(100));
            Assert.That(first.Runes, Is.EqualTo(150));
            Assert.That(first.Run.ReviveCost, Is.EqualTo(200));
            Assert.That(
                Fails(() => Run(source.ReviveAsync("moss-hall", default))),
                Is.EqualTo(409)
            );

            var defeated = Run(source.EndAsync(AdventureEndReason.Defeat, default));
            Assert.That(defeated.InProgress, Is.False);
            Assert.That(defeated.Result.Status, Is.EqualTo(AdventureEndStatus.Defeated));
            Assert.That(defeated.Result.Floor, Is.EqualTo(2));
            Assert.That(defeated.Result.NewRecord, Is.True);
            Assert.That(
                defeated.RecordOf(AdventureLocalSource.ForestRuins).BestFloor,
                Is.EqualTo(2)
            );
        }

        [Test]
        public void BeatingTheBossEndsTheAdventureAsCleared()
        {
            var source = new AdventureLocalSource(seed: 5);
            Run(source.StartAsync(AdventureLocalSource.ForestRuins, default));
            AdventureState state = null;
            foreach (var room in new[] { "moss-hall", "root-gallery", "sanctum" })
            {
                Run(source.MoveAsync(room, default));
                state = Run(source.ClearAsync(room, default));
            }
            Assert.That(state.InProgress, Is.False);
            Assert.That(state.Result.Status, Is.EqualTo(AdventureEndStatus.Cleared));
            Assert.That(state.Result.Floor, Is.EqualTo(4));
            Assert.That(state.Result.Clears, Is.EqualTo(1));
            Assert.That(state.Result.Rewards.Count, Is.EqualTo(3));
            Assert.That(state.Reward.Rank, Is.GreaterThanOrEqualTo(StepBonusRank.B));
        }

        // 結果には、手放したボーナスを持ち帰った物として出さない。
        [Test]
        public void TheResultListsTheBonusesKept()
        {
            var result = new AdventureResult(
                "forest-ruins",
                AdventureEndStatus.Retreated,
                3,
                4,
                false,
                0,
                0,
                new[]
                {
                    new AdventureReward("a", "luck", StepBonusRank.A, AdventureRewardOutcome.Added),
                    new AdventureReward(
                        "b",
                        "guard",
                        StepBonusRank.E,
                        AdventureRewardOutcome.Discarded
                    ),
                }
            );
            Assert.That(
                AdventureTexts.ResultBody(null, result),
                Is.EqualTo("森の遺跡　B3Fまで到達\n最深記録 B4F\n持ち帰ったボーナス：luck A")
            );
            Assert.That(
                AdventureTexts.ReviveBody(200, 150),
                Does.EndWith("所持ルーン 150（50 足りません）")
            );
        }

        private static long Fails(System.Action action)
        {
            try
            {
                action();
                return 0;
            }
            catch (ServerApiException exception)
            {
                return exception.StatusCode;
            }
        }
    }
}
