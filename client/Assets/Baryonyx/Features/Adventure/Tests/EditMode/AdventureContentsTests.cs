using System.Collections.Generic;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Inventory;
using Baryonyx.Networking;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.SkillEvolution;
using Baryonyx.Training;
using Baryonyx.Training.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 新しいマスの中身（doc/features/stage-progression.md のマスの中身）：怪しい宝箱・試練・歩数の扉・行商人。
    // アプリの中だけの冒険でサーバーと同じ決まりを確かめ、サーバーの応答の読み方を確かめる。
    public sealed class AdventureContentsTests
    {
        private static T Run<T>(System.Threading.Tasks.Task<T> task) =>
            task.GetAwaiter().GetResult();

        private static long Fails(System.Func<object> call)
        {
            try
            {
                call();
            }
            catch (ServerApiException exception)
            {
                return exception.StatusCode;
            }
            return 0;
        }

        // その種類のマスがある冒険を始め、そこまで道を進む（途中のマスは終えて進む）。
        private static (AdventureLocalSource Source, AdventureState State) WalkTo(
            AdventureRoomKind kind,
            long runes = 100000
        )
        {
            for (int seed = 1; seed < 400; seed++)
            {
                var source = new AdventureLocalSource(runes, seed);
                var state = Run(source.StartAsync(AdventureCatalog.ForestRuins, default));
                var map = state.Run.Map;
                var target = map.Rooms.FirstOrDefault(room => room.Kind == kind);
                if (target == null)
                    continue;
                var path = PathTo(map, target.Id);
                for (int i = 0; i < path.Count; i++)
                {
                    state = Run(source.MoveAsync(map.Find(path[i]), default));
                    if (i == path.Count - 1)
                        break;
                    var here = map.Find(path[i]);
                    state = here.Kind switch
                    {
                        AdventureRoomKind.ShadyChest => Run(
                            source.ClearAsync(
                                here.Id,
                                default,
                                null,
                                new AdventureClearOptions { Pass = true }
                            )
                        ),
                        _ => Run(source.ClearAsync(here.Id, default)),
                    };
                }
                return (source, state);
            }
            Assert.Fail("No route had a room of " + kind);
            return default;
        }

        private static List<string> PathTo(AdventureRouteMap map, string target)
        {
            var back = new Dictionary<string, string>();
            var queue = new Queue<string>(new[] { AdventureRouteMap.EntranceId });
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (id == target)
                    break;
                foreach (var next in map.Find(id).Next)
                    if (!back.ContainsKey(next))
                    {
                        back[next] = id;
                        queue.Enqueue(next);
                    }
            }
            var path = new List<string>();
            for (var id = target; id != AdventureRouteMap.EntranceId; id = back[id])
                path.Insert(0, id);
            return path;
        }

        // 本物の怪しい宝箱は、開けるとその場で終え、宝箱より1つ多く落とす。
        [Test]
        public void ARealShadyChestDropsOneMoreThanAChest()
        {
            var (source, state) = WalkTo(AdventureRoomKind.ShadyChest);
            source.NextMimic = false;
            var opened = Run(source.OpenAsync(state.Run.RoomId, default));
            Assert.That(opened.Run.RoomCleared, Is.True);
            Assert.That(opened.Run.InBattle, Is.False);
            Assert.That(opened.Drops.Count, Is.InRange(2, 4));
            // 開けて終えたあとの通り過ぎる要求は、送り直しと同じく終えた結果を返す。
            Assert.That(
                Fails(() =>
                    Run(
                        source.ClearAsync(
                            state.Run.RoomId,
                            default,
                            null,
                            new AdventureClearOptions { Pass = true }
                        )
                    )
                ),
                Is.EqualTo(0)
            );
        }

        // ミミックは戦闘のマスになり、復活もでき、勝つと最大の報酬（☆3以上、主の物を除く）を落とす。
        [Test]
        public void AMimicIsABattleThatGivesTheGreatestReward()
        {
            var (source, state) = WalkTo(AdventureRoomKind.ShadyChest);
            source.NextMimic = true;
            var opened = Run(source.OpenAsync(state.Run.RoomId, default));
            Assert.That(opened.Run.Contents.Mimic, Is.True);
            Assert.That(opened.Run.InBattle, Is.True);
            Assert.That(opened.Drops, Is.Null.Or.Empty);
            var encounter = AdventureCatalog.Encounter(opened.Run.Room.Encounter);
            Assert.That(encounter.Kinds, Does.Contain(AdventureCatalog.MimicId));
            // 開け直しても、ミミックのまま。
            Assert.That(
                Run(source.OpenAsync(state.Run.RoomId, default)).Run.Contents.Mimic,
                Is.True
            );
            Assert.That(
                Run(source.ReviveAsync(state.Run.RoomId, default)).RevivedFor,
                Is.GreaterThan(0)
            );
            var won = Run(source.ClearAsync(state.Run.RoomId, default));
            Assert.That(won.Drops.Count, Is.InRange(2, 4));
            Assert.That(won.Drops.Select(drop => drop.Stars), Is.All.GreaterThanOrEqualTo(3));
        }

        // 開けずに通り過ぎると、何も落とさずに終える。開けていない怪しい宝箱は、通り過ぎるほかに終えられない。
        [Test]
        public void PassingAShadyChestDropsNothing()
        {
            var (source, state) = WalkTo(AdventureRoomKind.ShadyChest);
            Assert.That(
                Fails(() => Run(source.ClearAsync(state.Run.RoomId, default))),
                Is.EqualTo(409)
            );
            var passed = Run(
                source.ClearAsync(
                    state.Run.RoomId,
                    default,
                    null,
                    new AdventureClearOptions { Pass = true }
                )
            );
            Assert.That(passed.Run.RoomCleared, Is.True);
            Assert.That(passed.Drops, Is.Empty);
            Assert.That(
                Fails(() => Run(source.OpenAsync(state.Run.RoomId, default))),
                Is.EqualTo(409)
            );
        }

        // 試練は、入ると条件が決まり、強い魔物と同じ強さで戦う。条件を満たせば最大の報酬、満たさなければ魔物の報酬。
        [Test]
        public void ATrialMetGivesTheGreatestReward()
        {
            var (source, state) = WalkTo(AdventureRoomKind.Trial);
            Assert.That(state.Run.Contents.Trial, Is.Not.EqualTo(AdventureTrial.None));
            Assert.That(state.Run.InBattle, Is.True);
            Assert.That(
                AdventureCatalog.Encounter(state.Run.Room.Encounter).Level,
                Is.EqualTo(
                    AdventureCatalog.Area(AdventureCatalog.ForestRuins).Level
                        + AdventureCatalog.EliteLevelUp
                )
            );
            var met = Run(
                source.ClearAsync(
                    state.Run.RoomId,
                    default,
                    null,
                    new AdventureClearOptions { TrialMet = true }
                )
            );
            Assert.That(met.Drops.Count, Is.InRange(2, 4));
            Assert.That(met.Drops.Select(drop => drop.Stars), Is.All.GreaterThanOrEqualTo(3));

            var (other, missedAt) = WalkTo(AdventureRoomKind.Trial);
            var missed = Run(other.ClearAsync(missedAt.Run.RoomId, default));
            Assert.That(missed.Drops.Count, Is.InRange(1, 2));
        }

        [Test]
        public void TheTrialsAreMetByHowTheBattleWasWon()
        {
            var full = new[] { 1f, 0.6f };
            Assert.That(AdventureTrials.Met(AdventureTrial.WithinSixTurns, 6, true, full), Is.True);
            Assert.That(
                AdventureTrials.Met(AdventureTrial.WithinSixTurns, 7, false, full),
                Is.False
            );
            Assert.That(AdventureTrials.Met(AdventureTrial.NoOneFalls, 12, false, full), Is.True);
            Assert.That(AdventureTrials.Met(AdventureTrial.NoOneFalls, 1, true, full), Is.False);
            Assert.That(AdventureTrials.Met(AdventureTrial.HalfHp, 9, false, full), Is.True);
            Assert.That(
                AdventureTrials.Met(AdventureTrial.HalfHp, 9, false, new[] { 1f, 0.49f }),
                Is.False
            );
            foreach (
                var trial in new[]
                {
                    AdventureTrial.WithinSixTurns,
                    AdventureTrial.NoOneFalls,
                    AdventureTrial.HalfHp,
                }
            )
            {
                Assert.That(AdventureTrials.Parse(AdventureTrials.KeyOf(trial)), Is.EqualTo(trial));
                Assert.That(AdventureTrials.NameOf(trial), Is.Not.Empty);
            }
        }

        // 歩数の扉は、今日の歩数で報酬の段が決まる。
        [Test]
        public void AStepDoorOpensByTodaysSteps()
        {
            Assert.That(AdventureStepDoor.TierOf(2999), Is.EqualTo(0));
            Assert.That(AdventureStepDoor.TierOf(3000), Is.EqualTo(1));
            Assert.That(AdventureStepDoor.TierOf(5000), Is.EqualTo(2));
            Assert.That(AdventureStepDoor.TierOf(8000), Is.EqualTo(3));
            var (source, state) = WalkTo(AdventureRoomKind.StepDoor);
            source.TodaySteps = 8000;
            var opened = Run(source.ClearAsync(state.Run.RoomId, default));
            Assert.That(opened.StepDoor.Steps, Is.EqualTo(8000));
            Assert.That(opened.StepDoor.Tier, Is.EqualTo(3));
            Assert.That(opened.Drops.Count, Is.InRange(2, 4));
            Assert.That(opened.Drops.Select(drop => drop.Stars), Is.All.GreaterThanOrEqualTo(3));
        }

        // 行商人は、スキル3つと装備2つを並べ、☆と品質の値段で売る。売れた物と、ルーンが足りない物は買えない。
        [Test]
        public void AMerchantSellsSkillsAndGearForRunes()
        {
            var (source, state) = WalkTo(AdventureRoomKind.Merchant);
            var wares = state.Run.Contents.Wares;
            Assert.That(wares.Count, Is.EqualTo(5));
            Assert.That(
                wares.Take(3).Select(ware => ware.Category),
                Is.All.EqualTo(AdventureDropCategory.Skill)
            );
            Assert.That(
                wares.Skip(3).Select(ware => ware.Category),
                Has.None.EqualTo(AdventureDropCategory.Item)
            );
            foreach (var ware in wares)
                Assert.That(
                    ware.Price,
                    Is.EqualTo(AdventureWares.PriceOf(ware.Stars, ware.Quality))
                );
            // 同じ物は並べない。
            Assert.That(wares.Select(ware => ware.Category + "/" + ware.Id), Is.Unique);
            var first = wares[0];
            long before = state.Runes;
            var bought = Run(source.BuyAsync(state.Run.RoomId, first.Index, default));
            Assert.That(bought.Runes, Is.EqualTo(before - first.Price));
            Assert.That(bought.Run.Contents.Wares[0].Sold, Is.True);
            Assert.That(
                Fails(() => Run(source.BuyAsync(state.Run.RoomId, first.Index, default))),
                Is.EqualTo(409)
            );
            var left = Run(source.ClearAsync(state.Run.RoomId, default));
            Assert.That(left.Run.RoomCleared, Is.True);
            Assert.That(left.Drops, Is.Empty);

            var (poor, at) = WalkTo(AdventureRoomKind.Merchant, runes: 0);
            Assert.That(
                Fails(() =>
                    Run(poor.BuyAsync(at.Run.RoomId, at.Run.Contents.Wares[0].Index, default))
                ),
                Is.EqualTo(409)
            );
        }

        // 値段：☆1が300、☆が1つ増えるごとに倍、品質C1.0・B1.25・A1.5・S2.0、10の位で丸める（サーバーの merchant.ts と同じ）。
        [Test]
        public void ThePricesFollowTheStarsAndTheQuality()
        {
            Assert.That(AdventureWares.PriceOf(1, ItemQuality.C), Is.EqualTo(300));
            Assert.That(AdventureWares.PriceOf(1, ItemQuality.B), Is.EqualTo(380));
            Assert.That(AdventureWares.PriceOf(3, ItemQuality.A), Is.EqualTo(1800));
            Assert.That(AdventureWares.PriceOf(5, ItemQuality.S), Is.EqualTo(9600));
        }

        // サーバーの応答の、ミミック・試練の条件・行商人の品物・歩数の扉を読む。
        [Test]
        public void TheServersAnswerCarriesTheRoomsContents()
        {
            const string json =
                "{\"run\":{\"id\":\"r1\",\"destinationId\":\"forest-ruins\",\"seed\":12345,\"roomCount\":8,"
                + "\"roomId\":\"f4-0\",\"floor\":4,\"roomKind\":\"merchant\",\"roomCleared\":false,"
                + "\"route\":[\"entrance\"],\"revives\":0,\"reviveCost\":100,\"drops\":[],"
                + "\"mimic\":true,\"trial\":\"half-hp\","
                + "\"merchant\":[{\"index\":0,\"category\":\"skill\",\"id\":\"Slash\",\"multiplierPercent\":137,\"quality\":\"B\",\"price\":380,\"sold\":false},"
                + "{\"index\":3,\"category\":\"weapon\",\"id\":\"wooden-sword\",\"multiplierPercent\":100,\"quality\":\"C\",\"price\":300,\"sold\":true},"
                + "{\"index\":4,\"category\":\"ring\",\"id\":\"x\",\"multiplierPercent\":100,\"quality\":\"C\",\"price\":300,\"sold\":false}]},"
                + "\"records\":[],\"runes\":900,\"stepDoor\":{\"steps\":5200,\"tier\":2}}";
            var state = AdventureServerSource.ToState(
                JsonUtility.FromJson<AdventureApiClient.State>(json)
            );
            var contents = state.Run.Contents;
            Assert.That(contents.Mimic, Is.True);
            Assert.That(contents.Trial, Is.EqualTo(AdventureTrial.HalfHp));
            Assert.That(contents.Wares.Count, Is.EqualTo(2));
            Assert.That(contents.Wares[0].Multiplier, Is.EqualTo(1.37f).Within(0.0001f));
            Assert.That(contents.Wares[0].Quality, Is.EqualTo(ItemQuality.B));
            Assert.That(contents.Wares[1].Sold, Is.True);
            Assert.That(state.StepDoor.Steps, Is.EqualTo(5200));
            Assert.That(state.StepDoor.Tier, Is.EqualTo(2));
        }

        // 進化の祭壇：編成の解放済みの固有スキルのうち、セットした段階に次の段階があるものを選べる。
        // 進化させると、その段階を取得してセットする。
        [Test]
        public void TheAltarEvolvesAUniqueSkillOfTheParty()
        {
            var party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            var training = AssetDatabase.LoadAssetAtPath<TrainingMockData>(TrainingAssets.DataPath);
            UniqueSkillSession.Reset();
            try
            {
                var choices = AdventureAltar.ChoicesOf(
                    PartySession.Formation(party),
                    party,
                    training
                );
                Assert.That(choices, Is.Not.Empty);
                foreach (var choice in choices)
                {
                    Assert.That(
                        choice.Next,
                        Is.EqualTo(UniqueSkillTreeShape.Children(choice.From))
                    );
                    Assert.That(choice.CardName(choice.From), Is.Not.Empty);
                }
                var first = choices[0];
                int stage = first.Next[0];
                AdventureAltar.Evolve(first, stage, training);
                var progress = UniqueSkillSession.ProgressOf(
                    first.Person,
                    first.Index,
                    training.Find(first.Person)?.MockProgressOf(first.Index)
                );
                Assert.That(progress.Has(stage), Is.True);
                Assert.That(progress.SetStage, Is.EqualTo(stage));
            }
            finally
            {
                UniqueSkillSession.Reset();
            }
        }

        // 祭壇で進化させたら物を落とさず、進められる固有スキルがなければ宝箱と同じに落とす。
        [Test]
        public void AnAltarDropsNothingOnceItEvolved()
        {
            var (source, state) = WalkTo(AdventureRoomKind.Altar);
            var evolved = Run(
                source.ClearAsync(
                    state.Run.RoomId,
                    default,
                    null,
                    new AdventureClearOptions { Evolved = true }
                )
            );
            Assert.That(evolved.Run.RoomCleared, Is.True);
            Assert.That(evolved.Drops, Is.Empty);
            var (other, at) = WalkTo(AdventureRoomKind.Altar);
            Assert.That(
                Run(other.ClearAsync(at.Run.RoomId, default)).Drops.Count,
                Is.InRange(1, 3)
            );
        }
    }
}
