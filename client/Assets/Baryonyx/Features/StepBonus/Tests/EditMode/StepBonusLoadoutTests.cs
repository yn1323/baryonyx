using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.StepBonus;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class StepBonusLoadoutTests
    {
        internal static StepBonusDefinition[] Definitions() =>
            new[]
            {
                Bonus(
                    "guard",
                    "守り",
                    StepBonusCategory.Battle,
                    "受けるダメージ -{0}%",
                    1,
                    0,
                    2,
                    3,
                    4,
                    5,
                    6,
                    8
                ),
                Bonus(
                    "luck",
                    "幸運",
                    StepBonusCategory.Drop,
                    "ドロップ率 +{0}%",
                    1,
                    0,
                    3,
                    5,
                    7,
                    9,
                    12,
                    15
                ),
                Bonus(
                    "treasure-sight",
                    "宝箱透視",
                    StepBonusCategory.Explore,
                    "確率 {0}%",
                    0,
                    100,
                    15,
                    20,
                    25,
                    30,
                    40,
                    50
                ),
                Bonus(
                    "foresight",
                    "先読み",
                    StepBonusCategory.Explore,
                    "確率 {0}%",
                    0,
                    100,
                    15,
                    20,
                    25,
                    30,
                    40,
                    50
                ),
                Bonus(
                    "omen",
                    "兆し",
                    StepBonusCategory.Explore,
                    "確率 {0}%",
                    0,
                    100,
                    15,
                    20,
                    25,
                    30,
                    40,
                    50
                ),
                Bonus(
                    "appraisal",
                    "目利き",
                    StepBonusCategory.Drop,
                    "レア度アップ +{0}%",
                    1,
                    0,
                    1,
                    2,
                    3,
                    4,
                    5,
                    6
                ),
                Bonus(
                    "fighting",
                    "闘志",
                    StepBonusCategory.Battle,
                    "攻撃力 +{0}%",
                    1,
                    0,
                    2,
                    3,
                    4,
                    5,
                    6,
                    8
                ),
            };

        internal static StepBonusLoadout Create(params string[] loadout) =>
            new(
                new[] { 1000, 2000, 3000, 5000, 8000 },
                new[] { 1f, 1.2f, 1.4f, 1.7f, 2f },
                Definitions(),
                new[]
                {
                    Roll("guard", StepBonusRank.S),
                    Roll("luck", StepBonusRank.A),
                    Roll("treasure-sight", StepBonusRank.B),
                    Roll("appraisal", StepBonusRank.B),
                    Roll("foresight", StepBonusRank.C),
                    Roll("fighting", StepBonusRank.D),
                    Roll("omen", StepBonusRank.E),
                },
                loadout.Length > 0
                    ? loadout
                    : new[] { "foresight", "luck", "treasure-sight", "fighting", "appraisal" },
                20
            );

        [Test]
        public void SlotsOpenWhenTodaysUptReachesTheirTier()
        {
            var loadout = Create();

            Assert.That(loadout.OpenCount(3240), Is.EqualTo(3));
            Assert.That(loadout.IsOpen(2, 3000), Is.True);
            Assert.That(loadout.IsOpen(3, 4999), Is.False);
            Assert.That(loadout.OpenCount(0), Is.Zero);
            Assert.That(loadout.OpenCount(10000), Is.EqualTo(5));
        }

        [Test]
        public void SettingAFreeBonusReplacesTheSlotsBonus()
        {
            var loadout = Create();

            Assert.That(loadout.Preview(3, "guard"), Is.EqualTo(StepBonusChange.Set));
            Assert.That(loadout.Apply(3, "guard"), Is.EqualTo(StepBonusChange.Set));
            Assert.That(loadout.Bonus(3), Is.EqualTo("guard"));
            Assert.That(loadout.SlotOf("fighting"), Is.EqualTo(-1));
        }

        [Test]
        public void ChoosingABonusFromAnotherSlotSwapsTheTwoSlots()
        {
            var loadout = Create();

            Assert.That(loadout.Apply(2, "luck"), Is.EqualTo(StepBonusChange.Swap));
            Assert.That(loadout.Bonus(2), Is.EqualTo("luck"));
            Assert.That(loadout.Bonus(1), Is.EqualTo("treasure-sight"));
            // A bonus never sits in two slots.
            Assert.That(
                Enumerable.Range(0, loadout.SlotCount).Select(loadout.Bonus).Distinct().Count(),
                Is.EqualTo(loadout.SlotCount)
            );
        }

        [Test]
        public void ChoosingTheSlotsOwnBonusOrAnUnknownOneChangesNothing()
        {
            var loadout = Create();

            Assert.That(loadout.Apply(1, "luck"), Is.EqualTo(StepBonusChange.None));
            Assert.That(loadout.Apply(1, "healing"), Is.EqualTo(StepBonusChange.None));
            Assert.That(loadout.Apply(9, "guard"), Is.EqualTo(StepBonusChange.None));
            Assert.That(loadout.Bonus(1), Is.EqualTo("luck"));
        }

        [Test]
        public void TheEffectIsTheRanksValueTimesTheSlotsMultiplierUpToTheCap()
        {
            var loadout = Create();

            // 幸運のA（12%）を2,000の枠（×1.2）に入れると14.4%。
            Assert.That(loadout.Value("luck"), Is.EqualTo(12f));
            Assert.That(loadout.Effective("luck", 1), Is.EqualTo(14.4f).Within(0.001f));
            Assert.That(loadout.Effective("guard", 3), Is.EqualTo(13.6f).Within(0.001f));
            Assert.That(loadout.Effective("treasure-sight", 4), Is.EqualTo(60f).Within(0.001f));
            // A chance stops at 100%.
            loadout.Acquire("treasure-sight", StepBonusRank.S);
            Assert.That(loadout.Effective("treasure-sight", 4), Is.EqualTo(100f));
        }

        [Test]
        public void EachRankHasItsOwnFixedValue()
        {
            var luck = Definitions().Single(bonus => bonus.Id == "luck");

            Assert.That(
                Enum.GetValues(typeof(StepBonusRank)).Cast<StepBonusRank>().Select(luck.Value),
                Is.EqualTo(new[] { 3f, 5f, 7f, 9f, 12f, 15f })
            );
            Assert.That(StepBonusLoadout.Effect(luck, 14.4f), Is.EqualTo("ドロップ率 +14.4%"));
            Assert.That(StepBonusLoadout.Effect(luck, 12f), Is.EqualTo("ドロップ率 +12%"));
        }

        [Test]
        public void GettingTheSameBonusAgainKeepsTheHigherRank()
        {
            var loadout = Create();

            Assert.That(
                loadout.Acquire("luck", StepBonusRank.B),
                Is.EqualTo(StepBonusAcquired.Discarded)
            );
            Assert.That(loadout.Roll("luck").Rank, Is.EqualTo(StepBonusRank.A));

            Assert.That(
                loadout.Acquire("luck", StepBonusRank.S),
                Is.EqualTo(StepBonusAcquired.Updated)
            );
            Assert.That(loadout.Roll("luck").Rank, Is.EqualTo(StepBonusRank.S));
            Assert.That(loadout.Owned.Count(roll => roll.Id == "luck"), Is.EqualTo(1));

            Assert.Throws<ArgumentException>(() => loadout.Acquire("missing", StepBonusRank.E));
        }

        [Test]
        public void TheStartingDataKeepsOneOfEachBonusAndOneSlotPerBonus()
        {
            var loadout = new StepBonusLoadout(
                new[] { 1000, 2000 },
                new[] { 1f, 1.2f },
                Definitions(),
                new[]
                {
                    Roll("luck", StepBonusRank.D),
                    Roll("luck", StepBonusRank.A),
                    Roll("missing", StepBonusRank.S),
                },
                new[] { "luck", "luck" },
                2
            );

            Assert.That(loadout.Owned.Count, Is.EqualTo(1));
            Assert.That(loadout.Roll("luck").Rank, Is.EqualTo(StepBonusRank.A));
            Assert.That(loadout.Bonus(0), Is.EqualTo("luck"));
            Assert.That(loadout.Bonus(1), Is.Null);
        }

        private static StepBonusDefinition Bonus(
            string id,
            string name,
            StepBonusCategory category,
            string effect,
            int decimals,
            float cap,
            params float[] values
        ) =>
            new()
            {
                Id = id,
                Name = name,
                Category = category,
                Effect = effect,
                Decimals = decimals,
                Cap = cap,
                Values = values,
            };

        private static StepBonusRoll Roll(string id, StepBonusRank rank) =>
            new() { Id = id, Rank = rank };
    }

    public sealed class StepBonusSettingsPresenterTests
    {
        private sealed class FakeView : IStepBonusSettingsView
        {
            public readonly List<string> Notices = new();

            public event Action<int> SlotPressed;
            public event Action<string> BonusPressed;
            public event Action<int> TabPressed;

            public StepBonusSettingsState Last { get; private set; }

            public void Render(StepBonusSettingsState state) => Last = state;

            public void ShowNotice(string message) => Notices.Add(message);

            public void PressSlot(int index) => SlotPressed?.Invoke(index);

            public void PressBonus(string id) => BonusPressed?.Invoke(id);

            public void PressTab(int index) => TabPressed?.Invoke(index);
        }

        [Test]
        public void TheSlotsShowTodaysEffects()
        {
            var view = new FakeView();
            using var presenter = new StepBonusSettingsPresenter(
                view,
                StepBonusLoadoutTests.Create(),
                3240
            );

            Assert.That(view.Last.OwnedText, Is.EqualTo("所持 7 / 20"));
            Assert.That(
                view.Last.Slots.Select(slot => slot.Open),
                Is.EqualTo(new[] { true, true, true, false, false })
            );
            Assert.That(view.Last.Slots[1].Tier, Is.EqualTo("2,000 UPT ×1.2"));
            Assert.That(view.Last.Slots[1].Name, Is.EqualTo("幸運"));
            Assert.That(view.Last.Slots[1].Effect, Is.EqualTo("ドロップ率 +14.4%"));
            Assert.That(view.Last.SelectedSlot, Is.Zero);
            // The list runs from the highest rank and marks the bonuses in a slot.
            Assert.That(view.Last.Rows[0].Id, Is.EqualTo("guard"));
            Assert.That(view.Last.Rows[0].Set, Is.False);
            Assert.That(view.Last.Rows.Single(row => row.Id == "luck").Set, Is.True);
        }

        [Test]
        public void TappingAFreeBonusSetsItInTheChosenSlot()
        {
            var view = new FakeView();
            var loadout = StepBonusLoadoutTests.Create();
            using var presenter = new StepBonusSettingsPresenter(view, loadout, 3240);

            view.PressSlot(3);
            view.PressBonus("guard");
            Assert.That(loadout.Bonus(3), Is.EqualTo("guard"));
            Assert.That(view.Notices, Is.EqualTo(new[] { "5,000の枠に「守り」をセットしました" }));
            Assert.That(view.Last.Slots[3].Effect, Is.EqualTo("受けるダメージ -13.6%"));
            Assert.That(view.Last.Rows.Single(row => row.Id == "fighting").Set, Is.False);

            // Tapping it again changes nothing and says nothing.
            view.PressBonus("guard");
            Assert.That(view.Notices.Count, Is.EqualTo(1));
        }

        [Test]
        public void TappingABonusFromAnotherSlotSwapsTheTwo()
        {
            var view = new FakeView();
            var loadout = StepBonusLoadoutTests.Create();
            using var presenter = new StepBonusSettingsPresenter(view, loadout, 3240);

            view.PressSlot(2);
            view.PressBonus("luck");
            Assert.That(loadout.Bonus(2), Is.EqualTo("luck"));
            Assert.That(loadout.Bonus(1), Is.EqualTo("treasure-sight"));
            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { "「幸運」と「宝箱透視」を入れ替えました" })
            );
        }

        [Test]
        public void TabsShowOneCategory()
        {
            var view = new FakeView();
            using var presenter = new StepBonusSettingsPresenter(
                view,
                StepBonusLoadoutTests.Create(),
                3240
            );

            view.PressTab(2);
            Assert.That(
                view.Last.Rows.Where(row => row.Visible).Select(row => row.Id),
                Is.EquivalentTo(new[] { "luck", "appraisal" })
            );

            view.PressTab(0);
            Assert.That(view.Last.Rows.All(row => row.Visible), Is.True);
        }
    }

    public sealed class StepBonusServerTests
    {
        private SynchronizationContext context;

        // The saves resume right where the server answers, not on the editor's next update.
        [SetUp]
        public void RunContinuationsInline()
        {
            context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        [TearDown]
        public void RestoreContext() => SynchronizationContext.SetSynchronizationContext(context);

        private sealed class FakeView : IStepBonusSettingsView
        {
            public readonly List<string> Notices = new();

            public event Action<int> SlotPressed;
            public event Action<string> BonusPressed;
            public event Action<int> TabPressed;

            public StepBonusSettingsState Last { get; private set; }

            public void Render(StepBonusSettingsState state) => Last = state;

            public void ShowNotice(string message) => Notices.Add(message);

            public void PressSlot(int index) => SlotPressed?.Invoke(index);

            public void PressBonus(string id) => BonusPressed?.Invoke(id);

            public void PressTab(int index) => TabPressed?.Invoke(index);
        }

        [Test]
        public void TheServersStateBecomesRollsAndSlots()
        {
            var state = StepBonusServerSource.ToState(
                new StepBonusApiClient.State
                {
                    holdings = new[]
                    {
                        new StepBonusApiClient.Holding { bonusId = "luck", rank = "A" },
                        new StepBonusApiClient.Holding { bonusId = "guard", rank = "Z" },
                        new StepBonusApiClient.Holding { bonusId = "omen", rank = "3" },
                    },
                    slots = new[]
                    {
                        new StepBonusApiClient.Slot { slot = 1, bonusId = "luck" },
                        new StepBonusApiClient.Slot { slot = 0, bonusId = "" },
                        new StepBonusApiClient.Slot { slot = 4, bonusId = null },
                    },
                }
            );

            Assert.That(
                state.Owned.Select(roll => (roll.Id, roll.Rank)),
                Is.EqualTo(new[] { ("luck", StepBonusRank.A) })
            );
            Assert.That(state.Slots, Is.EqualTo(new[] { null, "luck", null, null, null }));
            Assert.That(StepBonusServerSource.ToState(null).Owned, Is.Empty);
        }

        [Test]
        public void AChangeIsShownAfterTheServerSavesIt()
        {
            var view = new FakeView();
            var saved = StepBonusLoadoutTests.Create();
            saved.Apply(3, "guard");
            var calls = new List<(int, string)>();
            var pending = new TaskCompletionSource<StepBonusLoadout>();
            using var presenter = new StepBonusSettingsPresenter(
                view,
                StepBonusLoadoutTests.Create(),
                3240,
                (slot, id, _) =>
                {
                    calls.Add((slot, id));
                    return pending.Task;
                }
            );

            view.PressSlot(3);
            view.PressBonus("guard");
            Assert.That(presenter.Saving, Is.True);
            // Until the server answers, the slot keeps its bonus and taps wait.
            Assert.That(view.Last.Slots[3].Name, Is.EqualTo("闘志"));
            view.PressBonus("omen");
            Assert.That(calls, Is.EqualTo(new[] { (3, "guard") }));

            pending.SetResult(saved);
            Assert.That(presenter.ChooseTask.IsCompleted, Is.True);
            Assert.That(presenter.Saving, Is.False);
            Assert.That(presenter.Loadout, Is.SameAs(saved));
            Assert.That(view.Last.Slots[3].Name, Is.EqualTo("守り"));
            Assert.That(view.Notices, Is.EqualTo(new[] { "5,000の枠に「守り」をセットしました" }));
        }

        [Test]
        public void AFailedSaveKeepsTheSlotsAndSaysSo()
        {
            var view = new FakeView();
            using var presenter = new StepBonusSettingsPresenter(
                view,
                StepBonusLoadoutTests.Create(),
                3240,
                (_, _, _) => Task.FromException<StepBonusLoadout>(new InvalidOperationException())
            );

            view.PressSlot(3);
            view.PressBonus("guard");
            Assert.That(presenter.ChooseTask.IsCompleted, Is.True);
            Assert.That(presenter.Loadout.Bonus(3), Is.EqualTo("fighting"));
            Assert.That(view.Last.Slots[3].Name, Is.EqualTo("闘志"));
            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { StepBonusSettingsPresenter.SaveFailedMessage })
            );
            Assert.That(presenter.Saving, Is.False);
        }

        // 冒険の途中は付け替えを送らず、理由を知らせる。
        [Test]
        public void DuringAnAdventureTheSlotsDoNotChange()
        {
            var view = new FakeView();
            int saves = 0;
            using var presenter = new StepBonusSettingsPresenter(
                view,
                StepBonusLoadoutTests.Create(),
                3240,
                (_, _, _) =>
                {
                    saves++;
                    return Task.FromResult(StepBonusLoadoutTests.Create());
                },
                locked: true
            );

            view.PressSlot(3);
            view.PressBonus("guard");
            Assert.That(saves, Is.Zero);
            Assert.That(presenter.Loadout.Bonus(3), Is.EqualTo("fighting"));
            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { StepBonusSettingsPresenter.LockedMessage })
            );
        }

        // 開いたあとに冒険が始まっていれば、サーバーが断り（409）、同じ理由を知らせる。
        [Test]
        public void AServerRefusalDuringAnAdventureSaysWhy()
        {
            var view = new FakeView();
            using var presenter = new StepBonusSettingsPresenter(
                view,
                StepBonusLoadoutTests.Create(),
                3240,
                (_, _, _) =>
                    Task.FromException<StepBonusLoadout>(
                        new Baryonyx.Networking.ServerApiException(409)
                    )
            );

            view.PressSlot(3);
            view.PressBonus("guard");
            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { StepBonusSettingsPresenter.LockedMessage })
            );
        }

        [Test]
        public void TheServerTellsWhenTheSlotsAreLocked()
        {
            var state = StepBonusServerSource.ToState(
                new StepBonusApiClient.State
                {
                    holdings = Array.Empty<StepBonusApiClient.Holding>(),
                    slots = Array.Empty<StepBonusApiClient.Slot>(),
                    locked = true,
                }
            );
            Assert.That(state.Locked, Is.True);
            Assert.That(StepBonusServerSource.ToState(null).Locked, Is.False);
        }
    }
}
