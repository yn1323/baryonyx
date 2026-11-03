using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Inventory;
using Baryonyx.Networking;
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
                + "\"drops\":[{\"roomId\":\"entrance\",\"category\":\"accessory\",\"id\":\"moss-charm\",\"multiplier\":1.4,\"quality\":\"A\"},"
                + "{\"roomId\":\"entrance\",\"category\":\"item\",\"id\":\"herb\"},"
                + "{\"roomId\":\"x\",\"category\":\"weapon\",\"id\":\"iron-sword\",\"multiplier\":1.4,\"quality\":\"Z\"},"
                + "{\"roomId\":\"x\",\"category\":\"ring\",\"id\":\"gold\",\"multiplier\":1.4,\"quality\":\"A\"}]},"
                + "\"records\":[{\"destinationId\":\"forest-ruins\",\"bestFloor\":3,\"clears\":0}],"
                + "\"runes\":4500,\"revive\":null}";
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
            Assert.That(run.Room.Encounter, Does.StartWith("forest-ruins/pack/"));
            Assert.That(run.Floor, Is.EqualTo(2));
            Assert.That(run.InBattle, Is.True);
            Assert.That(run.Exits, Is.Empty);
            Assert.That(run.ReviveCost, Is.EqualTo(200));
            Assert.That(run.DeepestFloor, Is.EqualTo(10));
            // 知らない品質や分類のドロップは外す。アイテムは品質を持たない。
            Assert.That(
                run.Drops.Select(drop => (drop.Category, drop.Id, drop.Multiplier, drop.Quality)),
                Is.EqualTo(
                    new (AdventureDropCategory, string, float, ItemQuality?)[]
                    {
                        (AdventureDropCategory.Accessory, "moss-charm", 1.4f, ItemQuality.A),
                        (AdventureDropCategory.Item, "herb", 0f, null),
                    }
                )
            );
            Assert.That(state.Runes, Is.EqualTo(4500));
            Assert.That(state.RecordOf("forest-ruins").BestFloor, Is.EqualTo(3));
            // ないドロップとnullの復活は、JsonUtilityが空の値で埋めても、なしとして扱う。
            Assert.That(state.Drops, Is.Null);
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

        // 「？」のマスにいるとき、サーバーが保存した種類を正体として読み、その種類の部屋にする。
        [Test]
        public void AMysteryRoomIsReadAsWhatTheServerChose()
        {
            int seed = Enumerable
                .Range(1, 500)
                .First(value =>
                    AdventureCatalog
                        .Route(AdventureCatalog.ForestRuins, value, 8)
                        .Rooms.Any(room => room.Kind == AdventureRoomKind.Mystery)
                );
            var mystery = AdventureCatalog
                .Route(AdventureCatalog.ForestRuins, seed, 8)
                .Rooms.First(room => room.Kind == AdventureRoomKind.Mystery);
            string json =
                "{\"run\":{\"id\":\"r1\",\"destinationId\":\"forest-ruins\",\"seed\":"
                + seed
                + ",\"roomCount\":8,\"roomId\":\""
                + mystery.Id
                + "\",\"floor\":"
                + mystery.Floor
                + ",\"roomKind\":\"battle\",\"roomCleared\":false,\"route\":[\"entrance\"],"
                + "\"revives\":0,\"reviveCost\":100,\"drops\":[]},\"records\":[],\"runes\":0}";
            var run = AdventureServerSource
                .ToState(JsonUtility.FromJson<AdventureApiClient.State>(json))
                .Run;
            Assert.That(run.Contents.Revealed, Is.EqualTo(AdventureRoomKind.Battle));
            Assert.That(run.Room.Kind, Is.EqualTo(AdventureRoomKind.Battle));
            Assert.That(run.Room.Encounter, Does.StartWith("forest-ruins/pack/"));
            Assert.That(run.InBattle, Is.True);
            Assert.That(run.Map.Find(mystery.Id).Kind, Is.EqualTo(AdventureRoomKind.Mystery));
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
                // 怪しい宝箱は、開けずに通り過ぎて終える。
                if (i < moves - 1)
                    state = Run(
                        source.ClearAsync(
                            room.Id,
                            default,
                            null,
                            new AdventureClearOptions
                            {
                                Pass = room.Kind == AdventureRoomKind.ShadyChest,
                            }
                        )
                    );
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
            Assert.That(
                entrance.Exits.Select(exit => exit.Kind),
                Is.All.EqualTo(AdventureRoomKind.Battle)
            );
            // 進む道は脈打つ道と札で伝えるため、案内は出さない。
            Assert.That(entrance.Prompt, Is.Empty);
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
            // 開けることは、マスに置いた閉じた宝箱で伝えるため、案内は出さない。
            Assert.That(treasure.Prompt, Is.Empty);
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
            // 「？」のマスは魔物にする。
            var source = new AdventureLocalSource(seed: 5)
            {
                MysteryKind = AdventureRoomKind.Battle,
            };
            var state = Walk(source, 9);
            Assert.That(state.Run.Room.Kind, Is.EqualTo(AdventureRoomKind.Boss));
            var walked = state.Run;
            state = Run(source.ClearAsync(state.Run.RoomId, default));
            Assert.That(state.InProgress, Is.False);
            Assert.That(state.Result.Status, Is.EqualTo(AdventureEndStatus.Cleared));
            Assert.That(state.Result.Floor, Is.EqualTo(10));
            Assert.That(state.Result.Clears, Is.EqualTo(1));
            // 通ったマスのうち、物を落とすマス（休憩・行商人と、通り過ぎた怪しい宝箱を除く）で落ちた。
            AdventureRoomKind KindOf(string id) =>
                walked.Map.Find(id).Kind == AdventureRoomKind.Mystery
                    ? AdventureRoomKind.Battle
                    : walked.Map.Find(id).Kind;
            Assert.That(
                state.Result.Drops.Select(drop => drop.RoomId).Distinct(),
                Is.EquivalentTo(
                    walked.Route.Where(id =>
                        KindOf(id) != AdventureRoomKind.ShadyChest
                        && AdventureDrops.RewardOf(KindOf(id)) != null
                    )
                )
            );
            // 最奥の間では2〜4つ落ち、どれも☆3以上で、倍率はその品質の幅に入る。強化素材は別に抽選する
            // （AdventureDrops.RollMaterials）ため、数と☆の検査からは外す。
            var drops = state
                .Drops.Where(drop => drop.Category != AdventureDropCategory.Material)
                .ToArray();
            Assert.That(drops.Length, Is.InRange(2, 4));
            foreach (var drop in drops)
            {
                Assert.That(drop.Stars, Is.GreaterThanOrEqualTo(3), drop.Id);
                AdventureDropsTests.AssertQualityFitsMultiplier(drop);
            }
        }

        // 結果には、持ち帰った物を種類と品質ごとに1枚の札にまとめ、品質の高い順、☆の多い順に並べる。
        [Test]
        public void TheResultGroupsTheDropsKeptBestFirst()
        {
            var drops = new[]
            {
                new AdventureDrop("a", AdventureDropCategory.Item, "herb"),
                new AdventureDrop("a", AdventureDropCategory.Item, "herb"),
                new AdventureDrop("b", AdventureDropCategory.Weapon, "wooden-sword", 1.8f, ItemQuality.S),
                new AdventureDrop("c", AdventureDropCategory.Skill, "Hone", 1.2f, ItemQuality.C),
                new AdventureDrop("d", AdventureDropCategory.Weapon, "wooden-sword", 1.3f, ItemQuality.B),
                new AdventureDrop("e", AdventureDropCategory.Weapon, "wooden-sword", 1.9f, ItemQuality.S),
            };
            var kinds = AdventureResultPanel.Kinds(drops);
            Assert.That(
                kinds.Select(kind => (kind.Drop.Id, kind.Drop.Quality, kind.Count)),
                Is.EqualTo(
                    new (string, ItemQuality?, int)[]
                    {
                        ("wooden-sword", ItemQuality.S, 2),
                        ("wooden-sword", ItemQuality.B, 1),
                        ("Hone", ItemQuality.C, 1),
                        ("herb", null, 2),
                    }
                )
            );
            Assert.That(
                AdventureTexts.ReviveBody(200, 150),
                Does.EndWith("所持ルーン 150\nルーンが 50 足りません")
            );
        }

        // クリア回数の報酬は、主のドロップと並ぶ札にする。物（数つき）とルーンの2枚に「N回目のクリア報酬」を付ける。
        [Test]
        public void AClearRewardBecomesTaggedPlates()
        {
            var second = AdventureClearRewards.At(AdventureCatalog.ForestRuins, 2);
            var plates = AdventureDropItems.FromClearReward(second, null, null);
            Assert.That(plates, Has.Count.EqualTo(2));
            Assert.That(plates.Select(plate => plate.Tag), Is.All.EqualTo("2回目のクリア報酬"));
            Assert.That(plates[0].Name, Does.EndWith("×10"));
            Assert.That(plates[1].Kind, Is.EqualTo(AdventureDropItems.RunesKind));
            Assert.That(plates[1].Name, Is.EqualTo("300"));
            Assert.That(AdventureDropItems.FromClearReward(null, null, null), Is.Empty);
            Assert.That(AdventureClearRewards.At(AdventureCatalog.ForestRuins, 6), Is.Null);
            Assert.That(
                AdventureClearRewards.Of(AdventureCatalog.ForestRuins).Select(reward => reward.Runes),
                Is.All.EqualTo(AdventureClearRewards.Runes)
            );
        }

        // 見出しは「踏破」と言わない。クリアしなければ、着いた階と記録の更新を場所に添える。
        [Test]
        public void TheResultNamesHowItEndedAndWhere()
        {
            AdventureResult Ended(AdventureEndStatus status, bool record) =>
                new("forest-ruins", status, 3, 4, record, 2, 0, System.Array.Empty<AdventureDrop>());

            Assert.That(AdventureTexts.ResultTitle(AdventureEndStatus.Cleared), Is.EqualTo("クリア！"));
            Assert.That(AdventureTexts.ResultTitle(AdventureEndStatus.Defeated), Is.EqualTo("冒険の終わり"));
            Assert.That(AdventureTexts.ResultTitle(AdventureEndStatus.Retreated), Is.EqualTo("帰還"));
            Assert.That(AdventureTexts.ResultPlace(Ended(AdventureEndStatus.Cleared, true)), Is.EqualTo(Forest));
            Assert.That(
                AdventureTexts.ResultPlace(Ended(AdventureEndStatus.Retreated, false)),
                Is.EqualTo(Forest + "　第3層")
            );
            Assert.That(
                AdventureTexts.ResultPlace(Ended(AdventureEndStatus.Defeated, true)),
                Is.EqualTo(Forest + "　第3層　最深記録を更新！")
            );
            Assert.That(AdventureTexts.ClearCount(3), Is.EqualTo("3回目"));
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
