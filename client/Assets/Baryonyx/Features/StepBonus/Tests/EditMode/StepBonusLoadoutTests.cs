using System;
using System.Collections.Generic;
using System.Linq;
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
                    3,
                    8,
                    1,
                    0
                ),
                Bonus("luck", "幸運", StepBonusCategory.Drop, "ドロップ率 +{0}%", 5, 15, 1, 0),
                Bonus(
                    "treasure-sight",
                    "宝箱透視",
                    StepBonusCategory.Explore,
                    "確率 {0}%",
                    20,
                    60,
                    0,
                    100
                ),
                Bonus(
                    "foresight",
                    "先読み",
                    StepBonusCategory.Explore,
                    "確率 {0}%",
                    20,
                    50,
                    0,
                    100
                ),
                Bonus("omen", "兆し", StepBonusCategory.Explore, "確率 {0}%", 30, 80, 0, 100),
                Bonus(
                    "appraisal",
                    "目利き",
                    StepBonusCategory.Drop,
                    "レア度アップ +{0}%",
                    2,
                    5,
                    1,
                    0
                ),
                Bonus("fighting", "闘志", StepBonusCategory.Battle, "攻撃力 +{0}%", 3, 8, 1, 0),
            };

        internal static StepBonusLoadout Create(params string[] loadout) =>
            new(
                new[] { 1000, 2000, 3000, 5000, 8000 },
                new[] { 1f, 1.2f, 1.4f, 1.7f, 2f },
                Definitions(),
                new[]
                {
                    Roll("guard", 7.6f, updated: true),
                    Roll("luck", 11.6f),
                    Roll("treasure-sight", 46f),
                    Roll("foresight", 36f),
                    Roll("omen", 55f),
                    Roll("appraisal", 3.4f),
                    Roll("fighting", 4.1f),
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
        public void TheEffectIsTheRolledValueTimesTheSlotsMultiplierUpToTheCap()
        {
            var loadout = Create();

            Assert.That(loadout.Effective("luck", 0), Is.EqualTo(11.6f).Within(0.001f));
            Assert.That(loadout.Effective("guard", 3), Is.EqualTo(12.92f).Within(0.001f));
            Assert.That(loadout.Effective("treasure-sight", 4), Is.EqualTo(92f).Within(0.001f));
            // A chance stops at 100%.
            Assert.That(loadout.Effective("omen", 4), Is.EqualTo(100f));
        }

        [Test]
        public void TheRankTellsWhereTheRolledValueFellInTheRange()
        {
            var bonuses = Definitions().ToDictionary(bonus => bonus.Id);

            Assert.That(
                StepBonusLoadout.RankOf(bonuses["guard"], 7.6f),
                Is.EqualTo(StepBonusRank.S)
            );
            Assert.That(
                StepBonusLoadout.RankOf(bonuses["luck"], 11.6f),
                Is.EqualTo(StepBonusRank.A)
            );
            Assert.That(
                StepBonusLoadout.RankOf(bonuses["foresight"], 36f),
                Is.EqualTo(StepBonusRank.B)
            );
            Assert.That(
                StepBonusLoadout.RankOf(bonuses["fighting"], 4.1f),
                Is.EqualTo(StepBonusRank.C)
            );
            Assert.That(StepBonusLoadout.Range(bonuses["guard"]), Is.EqualTo("3〜8%"));
            Assert.That(
                StepBonusLoadout.Effect(bonuses["guard"], 12.92f),
                Is.EqualTo("受けるダメージ -12.9%")
            );
        }

        [Test]
        public void GettingTheSameBonusAgainKeepsTheHigherValue()
        {
            var loadout = Create();

            Assert.That(loadout.Acquire("luck", 9f), Is.EqualTo(StepBonusAcquired.Discarded));
            Assert.That(loadout.Roll("luck").Value, Is.EqualTo(11.6f));
            Assert.That(loadout.Roll("luck").Updated, Is.False);

            Assert.That(loadout.Acquire("luck", 13.2f), Is.EqualTo(StepBonusAcquired.Updated));
            Assert.That(loadout.Roll("luck").Value, Is.EqualTo(13.2f));
            Assert.That(loadout.Roll("luck").Updated, Is.True);
            Assert.That(loadout.Owned.Count(roll => roll.Id == "luck"), Is.EqualTo(1));

            Assert.Throws<ArgumentException>(() => loadout.Acquire("missing", 1f));
        }

        [Test]
        public void TheStartingDataKeepsOneOfEachBonusAndOneSlotPerBonus()
        {
            var loadout = new StepBonusLoadout(
                new[] { 1000, 2000 },
                new[] { 1f, 1.2f },
                Definitions(),
                new[] { Roll("luck", 6f), Roll("luck", 12f), Roll("missing", 3f) },
                new[] { "luck", "luck" },
                2
            );

            Assert.That(loadout.Owned.Count, Is.EqualTo(1));
            Assert.That(loadout.Roll("luck").Value, Is.EqualTo(12f));
            Assert.That(loadout.Bonus(0), Is.EqualTo("luck"));
            Assert.That(loadout.Bonus(1), Is.Null);
        }

        private static StepBonusDefinition Bonus(
            string id,
            string name,
            StepBonusCategory category,
            string effect,
            float min,
            float max,
            int decimals,
            float cap
        ) =>
            new()
            {
                Id = id,
                Name = name,
                Category = category,
                Effect = effect,
                Min = min,
                Max = max,
                Decimals = decimals,
                Cap = cap,
            };

        private static StepBonusRoll Roll(string id, float value, bool updated = false) =>
            new()
            {
                Id = id,
                Value = value,
                Updated = updated,
            };
    }

    public sealed class StepBonusSettingsPresenterTests
    {
        private sealed class FakeView : IStepBonusSettingsView
        {
            public readonly List<string> Toasts = new();

            public event Action<int> SlotPressed;
            public event Action<string> BonusPressed;
            public event Action<int> TabPressed;
            public event Action ConfirmPressed;

            public StepBonusSettingsState Last { get; private set; }

            public void Render(StepBonusSettingsState state) => Last = state;

            public void ShowToast(string message) => Toasts.Add(message);

            public void PressSlot(int index) => SlotPressed?.Invoke(index);

            public void PressBonus(string id) => BonusPressed?.Invoke(id);

            public void PressTab(int index) => TabPressed?.Invoke(index);

            public void PressConfirm() => ConfirmPressed?.Invoke();
        }

        [Test]
        public void ShowsTheOpenSlotsAndWhereEachBonusIsSet()
        {
            var view = new FakeView();
            using var presenter = new StepBonusSettingsPresenter(
                view,
                StepBonusLoadoutTests.Create(),
                3240
            );

            Assert.That(view.Last.HeaderText, Is.EqualTo("今日 3,240 UPT・翌朝4:00まで有効"));
            Assert.That(view.Last.OwnedText, Is.EqualTo("所持 7 / 20"));
            Assert.That(
                view.Last.Slots.Select(slot => slot.Open),
                Is.EqualTo(new[] { true, true, true, false, false })
            );
            Assert.That(view.Last.Slots[3].Label, Is.EqualTo("5,000 ×1.7"));
            Assert.That(view.Last.Rows[0].Id, Is.EqualTo("guard"));
            Assert.That(view.Last.Rows[0].Note, Is.EqualTo("UP　前回の冒険で更新"));
            Assert.That(
                view.Last.Rows.Single(row => row.Id == "luck").Note,
                Is.EqualTo("2,000でセット中")
            );
            Assert.That(view.Last.FooterTitle, Is.EqualTo("1,000の枠（×1.0）"));
            Assert.That(view.Last.CanConfirm, Is.False);
        }

        [Test]
        public void SetsAFreeBonusInTheChosenSlot()
        {
            var view = new FakeView();
            var loadout = StepBonusLoadoutTests.Create();
            using var presenter = new StepBonusSettingsPresenter(view, loadout, 3240);

            view.PressSlot(3);
            view.PressBonus("guard");
            Assert.That(view.Last.FooterTitle, Is.EqualTo("5,000の枠：闘志 → 守り"));
            Assert.That(view.Last.FooterDetail, Is.EqualTo("受けるダメージ -12.9%（7.6×1.7）"));
            Assert.That(view.Last.ConfirmLabel, Is.EqualTo("セットする"));
            Assert.That(view.Last.CanConfirm, Is.True);

            view.PressConfirm();
            Assert.That(loadout.Bonus(3), Is.EqualTo("guard"));
            Assert.That(view.Toasts, Is.EqualTo(new[] { "5,000の枠に「守り」をセットしました" }));
            Assert.That(view.Last.ConfirmLabel, Is.EqualTo("セット中"));
            Assert.That(view.Last.CanConfirm, Is.False);
        }

        [Test]
        public void SwapsWithTheSlotThatHoldsTheChosenBonus()
        {
            var view = new FakeView();
            var loadout = StepBonusLoadoutTests.Create();
            using var presenter = new StepBonusSettingsPresenter(view, loadout, 3240);

            view.PressSlot(2);
            view.PressBonus("luck");
            Assert.That(
                view.Last.FooterTitle,
                Is.EqualTo("3,000の枠：宝箱透視 ⇔ 幸運（2,000の枠）")
            );
            Assert.That(view.Last.ConfirmLabel, Is.EqualTo("入れ替える"));
            Assert.That(view.Last.Slots[1].Partner, Is.True);

            view.PressConfirm();
            Assert.That(loadout.Bonus(2), Is.EqualTo("luck"));
            Assert.That(loadout.Bonus(1), Is.EqualTo("treasure-sight"));
            Assert.That(
                view.Toasts,
                Is.EqualTo(new[] { "「幸運」と「宝箱透視」を入れ替えました" })
            );
        }

        [Test]
        public void TabsShowOneCategoryAndDropAHiddenChoice()
        {
            var view = new FakeView();
            using var presenter = new StepBonusSettingsPresenter(
                view,
                StepBonusLoadoutTests.Create(),
                3240
            );

            view.PressBonus("guard");
            view.PressTab(2);
            Assert.That(
                view.Last.Rows.Where(row => row.Visible).Select(row => row.Id),
                Is.EquivalentTo(new[] { "luck", "appraisal" })
            );
            Assert.That(view.Last.SelectedBonus, Is.Null);

            view.PressTab(0);
            Assert.That(view.Last.Rows.All(row => row.Visible), Is.True);
        }
    }
}
