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
        private static readonly string Forest = AdventureCatalog.DestinationName(
            AdventureCatalog.ForestRuins
        );

        // アプリの中だけの冒険はすぐに答えるため、完了をそのまま受け取る。
        private static T Run<T>(System.Threading.Tasks.Task<T> task) =>
            task.GetAwaiter().GetResult();

        // サーバーの応答（server/src/features/adventure/routes.ts）を、JsonUtilityで読んだ形から変える。
        // 道は、応答の種と部屋の数から作る。
        [Test]
        public void TheServersAnswerBecomesTheRunOnTheRouteOfItsSeed()
        {
            const string json =
                "{\"run\":{\"id\":\"r1\",\"destinationId\":\"forest-ruins\",\"seed\":12345,\"roomCount\":8,"
                + "\"roomId\":\"f2-0\",\"floor\":2,\"roomKind\":\"battle\","
                + "\"roomCleared\":false,\"route\":[\"entrance\",\"f2-0\"],\"revives\":1,\"reviveCost\":200,"
                + "\"rewards\":[{\"roomId\":\"entrance\",\"bonusId\":\"luck\",\"rank\":\"A\",\"outcome\":\"updated\"},"
                + "{\"roomId\":\"x\",\"bonusId\":\"luck\",\"rank\":\"Z\",\"outcome\":\"added\"}]},"
                + "\"records\":[{\"destinationId\":\"forest-ruins\",\"bestFloor\":3,\"clears\":0}],"
                + "\"runes\":4500,\"reward\":null,\"revive\":null}";
            var state = AdventureServerSource.ToState(
                JsonUtility.FromJson<AdventureApiClient.State>(json)
            );

            Assert.That(state.InProgress, Is.True);
            var run = state.Run;
            Assert.That(
                AdventureServerSource
                    .ToState(JsonUtility.FromJson<AdventureApiClient.State>(json))
                    .Run.Rooms.Select(room => room.Id),
                Is.EqualTo(run.Rooms.Select(room => room.Id))
            );
            Assert.That(run.Map.Seed, Is.EqualTo(12345));
            Assert.That(run.Map.RoomCount, Is.EqualTo(8));
            Assert.That(run.Room.Kind, Is.EqualTo(AdventureRoomKind.Battle));
            Assert.That(run.Room.Encounter, Is.EqualTo("forest-pack"));
            Assert.That(run.Floor, Is.EqualTo(2));
            Assert.That(run.InBattle, Is.True);
            Assert.That(run.Exits, Is.Empty);
            Assert.That(run.ReviveCost, Is.EqualTo(200));
            Assert.That(run.DeepestFloor, Is.EqualTo(10));
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

        // 道にない部屋（以前の冒険の部屋など）でも、サーバーが保存した階を出す。
        [Test]
        public void ARoomOffTheRouteKeepsTheSavedFloor()
        {
            var state = AdventureServerSource.ToState(
                JsonUtility.FromJson<AdventureApiClient.State>(
                    "{\"run\":{\"id\":\"r1\",\"destinationId\":\"forest-ruins\",\"seed\":7,\"roomCount\":8,"
                        + "\"roomId\":\"moss-hall\",\"floor\":3,\"roomKind\":\"battle\",\"roomCleared\":true,"
                        + "\"route\":[],\"revives\":0,\"reviveCost\":100,\"rewards\":[]},\"records\":[],\"runes\":0}"
                )
            );
            Assert.That(state.Run.Room, Is.Null);
            Assert.That(state.Run.Floor, Is.EqualTo(3));
            Assert.That(state.Run.Exits, Is.Empty);
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

        // 次の階の部屋へ進み、最後の部屋以外の出来事を終える。
        private static AdventureState Walk(AdventureLocalSource source, int moves)
        {
            var state = Run(source.StartAsync(AdventureCatalog.ForestRuins, default));
            for (int i = 0; i < moves; i++)
            {
                var room = state.Run.Exits[0];
                state = Run(source.MoveAsync(room, default));
                if (i < moves - 1)
                    state = Run(source.ClearAsync(room.Id, default));
            }
            return state;
        }

        // 部屋の出来事（宝箱・戦闘）を終えるまで次の部屋は選べず、終えると次の階の部屋と手掛かりを出す。
        [Test]
        public void TheRoomOffersTheNextFloorOnlyOnceItsEventIsDone()
        {
            var source = new AdventureLocalSource(seed: 7);
            var start = Run(source.StartAsync(AdventureCatalog.ForestRuins, default));
            var entrance = ExplorationRoom.From(start.Run);
            Assert.That(entrance.Location, Is.EqualTo(Forest + " 第1層"));
            Assert.That(
                entrance.Exits.Select(exit => exit.Id),
                Is.EqualTo(start.Run.Map.Find(AdventureRouteMap.EntranceId).Next)
            );
            Assert.That(entrance.Exits.Select(exit => exit.Hint), Is.All.EqualTo("魔物の気配"));
            Assert.That(entrance.Prompt, Is.EqualTo(ExplorationRoom.ChoosePrompt));
            Assert.That(entrance.StartsBattle, Is.False);

            var first = entrance.Exits[0].Room;
            var battle = ExplorationRoom.From(Run(source.MoveAsync(first, default)).Run);
            Assert.That(battle.StartsBattle, Is.True);
            Assert.That(battle.Prompt, Is.EqualTo(ExplorationRoom.BattlePrompt));
            Assert.That(battle.Exits, Is.Empty);
            var won = ExplorationRoom.From(Run(source.ClearAsync(first.Id, default)).Run);
            Assert.That(won.Exits.Select(exit => exit.Id), Is.EqualTo(first.Next));
            Assert.That(won.Location, Is.EqualTo(Forest + " 第2層"));

            // 道中の中ほどの階（第6層）は宝箱。開けるまで次へは進めない。
            var state = Walk(new AdventureLocalSource(seed: 7), 5);
            var treasure = ExplorationRoom.From(state.Run);
            Assert.That(state.Run.Room.Kind, Is.EqualTo(AdventureRoomKind.Treasure));
            Assert.That(treasure.Chest, Is.EqualTo(ExplorationChest.Closed));
            Assert.That(treasure.Exits, Is.Empty);
            Assert.That(treasure.Prompt, Is.EqualTo(ExplorationRoom.ChestPrompt));
        }

        // サーバーと同じ決まり：出来事を終えるまで進めない・道でつながっていない部屋へは進めない。
        [Test]
        public void TheLocalAdventureKeepsTheServersRules()
        {
            var source = new AdventureLocalSource(runes: 250, seed: 3);
            var stray = new AdventureRoom("f2-0", 2, AdventureRoomKind.Battle, "", new string[0]);
            Assert.That(Fails(() => Run(source.MoveAsync(stray, default))), Is.EqualTo(404));
            var start = Run(source.StartAsync(AdventureCatalog.ForestRuins, default));
            Assert.That(
                Fails(() => Run(source.StartAsync(AdventureCatalog.ForestRuins, default))),
                Is.Zero
            );
            var map = start.Run.Map;
            var far = map.Rooms.First(room => room.Floor == 3);
            Assert.That(Fails(() => Run(source.MoveAsync(far, default))), Is.EqualTo(409));
            var first = start.Run.Exits[0];
            Run(source.MoveAsync(first, default));
            Assert.That(
                Fails(() => Run(source.MoveAsync(map.Find(first.Next[0]), default))),
                Is.EqualTo(409)
            );

            // 復活は100、2回目は200。足りなければ断る。
            var revived = Run(source.ReviveAsync(first.Id, default));
            Assert.That(revived.RevivedFor, Is.EqualTo(100));
            Assert.That(revived.Runes, Is.EqualTo(150));
            Assert.That(revived.Run.ReviveCost, Is.EqualTo(200));
            Assert.That(Fails(() => Run(source.ReviveAsync(first.Id, default))), Is.EqualTo(409));

            var defeated = Run(source.EndAsync(AdventureEndReason.Defeat, default));
            Assert.That(defeated.InProgress, Is.False);
            Assert.That(defeated.Result.Status, Is.EqualTo(AdventureEndStatus.Defeated));
            Assert.That(defeated.Result.Floor, Is.EqualTo(2));
            Assert.That(defeated.Result.NewRecord, Is.True);
            Assert.That(defeated.RecordOf(AdventureCatalog.ForestRuins).BestFloor, Is.EqualTo(2));
        }

        [Test]
        public void BeatingTheBossEndsTheAdventureAsCleared()
        {
            var source = new AdventureLocalSource(seed: 5);
            var state = Walk(source, 9);
            Assert.That(state.Run.Room.Kind, Is.EqualTo(AdventureRoomKind.Boss));
            state = Run(source.ClearAsync(state.Run.RoomId, default));
            Assert.That(state.InProgress, Is.False);
            Assert.That(state.Result.Status, Is.EqualTo(AdventureEndStatus.Cleared));
            Assert.That(state.Result.Floor, Is.EqualTo(10));
            Assert.That(state.Result.Clears, Is.EqualTo(1));
            Assert.That(state.Result.Rewards.Count, Is.EqualTo(9));
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
                Is.EqualTo(Forest + "　第3層まで到達\n最深記録 第4層\n持ち帰ったボーナス：luck A")
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
