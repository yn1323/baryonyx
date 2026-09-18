using Baryonyx.Wireframe;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class WireframeSessionTests
    {
        private WireframeData data;
        private WireframeSession session;

        [SetUp]
        public void SetUp()
        {
            data = ScriptableObject.CreateInstance<WireframeData>();
            session = new WireframeSession(data);
            session.Back();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(data);

        private void Explore()
        {
            session.Open(WireScreen.Destination);
            session.BeginAdventure(0);
        }

        [Test]
        public void AcquisitionCanEquipImmediatelyAndDoesNotGrantTwice()
        {
            Explore();
            session.EnterDoor();
            session.FinishBattle(true);
            session.FinishBattle(true);
            Assert.That(session.Runes, Is.EqualTo(980));
            Assert.That(session.Step, Is.EqualTo(1));
            Assert.That(session.Popup, Is.EqualTo(WirePopup.NewEquipment));
            session.ManageAcquisition();
            session.SelectSlot(2);
            session.ConfirmEquipment();
            session.ConfirmEquipment();
            Assert.That(session.Screen, Is.EqualTo(WireScreen.Explore));
            Assert.That(session.Equipped(2), Is.EqualTo(2));
            Assert.That(session.Step, Is.EqualTo(1));
        }

        [Test]
        public void AcquiredCompanionReplacesSelectedSlotAndAppearsInNextBattle()
        {
            Explore();
            session.OpenChest();
            session.ManageAcquisition();
            session.SelectSlot(1);
            session.ConfirmCharacter();
            Assert.That(session.Screen, Is.EqualTo(WireScreen.Explore));
            Assert.That(session.PartyMember(1), Is.EqualTo(4));
            session.EnterDoor();
            session.SelectSlot(1);
            Assert.That(session.SelectedCharacter, Is.EqualTo(4));
        }

        [Test]
        public void DeferringAcquisitionKeepsItAvailable()
        {
            Explore();
            session.OpenChest();
            session.Back();
            session.Open(WireScreen.Party);
            session.ChooseCharacter(4);
            session.ConfirmCharacter();
            Assert.That(session.PartyMember(0), Is.EqualTo(4));
            session.Back();
            Assert.That(session.Screen, Is.EqualTo(WireScreen.Explore));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NestedManagementReturnsToItsCaller(bool adventure)
        {
            if (adventure)
                Explore();
            session.Open(WireScreen.Party);
            session.SelectSlot(2);
            session.ChooseCharacter(1);
            session.Open(WireScreen.Equipment);
            session.SelectSlot(1);
            session.Back();
            Assert.That(session.Screen, Is.EqualTo(WireScreen.Party));
            Assert.That(session.Slot, Is.EqualTo(2));
            Assert.That(session.Candidate, Is.EqualTo(1));
            session.Back();
            Assert.That(
                session.Screen,
                Is.EqualTo(adventure ? WireScreen.Explore : WireScreen.Home)
            );
        }

        [Test]
        public void DefeatManagementReturnsToDefeatAndRetryResetsTheEnemy()
        {
            Explore();
            session.EnterDoor();
            session.FinishBattle(false);
            session.Open(WireScreen.Party);
            session.Open(WireScreen.Equipment);
            session.Back();
            session.Back();
            Assert.That(session.Screen, Is.EqualTo(WireScreen.Defeat));
            session.ShowRevive();
            session.Back();
            Assert.That(session.Screen, Is.EqualTo(WireScreen.Defeat));
            session.Retry();
            Assert.That(session.EnemyHp, Is.EqualTo(100));
            Assert.That(session.AlliesRecovered, Is.True);
        }

        [Test]
        public void RevivalPreservesEnemyDisplayWithoutSpendingRunes()
        {
            Explore();
            session.EnterDoor();
            session.FinishBattle(false);
            session.ShowRevive();
            session.ConfirmRevive();
            session.ConfirmRevive();
            Assert.That(session.Screen, Is.EqualTo(WireScreen.Battle));
            Assert.That(session.EnemyHp, Is.EqualTo(38));
            Assert.That(session.CombatState, Is.EqualTo(WireCombatState.Weak));
            Assert.That(session.Runes, Is.EqualTo(860));
        }

        [Test]
        public void BattleCannotOpenManagementOrEscapeAndSkillsDoNotDamage()
        {
            Explore();
            session.EnterDoor();
            session.Open(WireScreen.Party);
            session.Open(WireScreen.Equipment);
            session.HomeTab(WireScreen.Home);
            session.Back();
            Assert.That(session.Screen, Is.EqualTo(WireScreen.Battle));
            session.SelectSlot(2);
            session.ChooseSkill(1);
            session.ChooseTarget(2);
            session.UseSkill();
            session.SelectSlot(0);
            Assert.That(session.CastingSlot, Is.EqualTo(2));
            Assert.That(session.EnemyHp, Is.EqualTo(100));
            Assert.That(session.CombatState, Is.EqualTo(WireCombatState.Casting));
        }

        [Test]
        public void EndCancelAndReentryPreserveLocationAndEquipment()
        {
            Explore();
            session.EnterDoor();
            session.FinishBattle(true);
            session.Back();
            session.EndAdventure();
            session.Back();
            Assert.That(session.Screen, Is.EqualTo(WireScreen.Explore));
            Assert.That(session.Step, Is.EqualTo(1));
            session.EndAdventure();
            session.EndAdventure();
            Explore();
            Assert.That(session.Step, Is.EqualTo(1));
            Assert.That(session.HasNewEquipment, Is.True);
        }

        [Test]
        public void BothGoalsCanBeSkippedAndDailyDoesNotConfigureWeekly()
        {
            session.ToggleDebug();
            session.Reset();
            session.StartGoals();
            session.Back();
            Assert.That(session.Screen, Is.EqualTo(WireScreen.Home));
            Assert.That(session.Goal(false), Is.EqualTo(WireGoalState.Unset));
            Assert.That(session.Goal(true), Is.EqualTo(WireGoalState.Unset));
            session.Open(WireScreen.Goals);
            session.ShowGoals(false);
            session.SaveGoal();
            Assert.That(session.Goal(false), Is.EqualTo(WireGoalState.Progress));
            Assert.That(session.Goal(true), Is.EqualTo(WireGoalState.Unset));
        }

        [Test]
        public void DistanceGoalRequiresNoticeAndCancellingScheduleKeepsPreviousCondition()
        {
            session.Open(WireScreen.Goals);
            session.ShowGoals(false);
            session.SaveGoal();
            session.ShowGoals(false);
            session.ToggleGoal(1);
            session.SaveGoal();
            Assert.That(session.Popup, Is.EqualTo(WirePopup.Data));
            Assert.That(session.GoalUsesDistance(false), Is.False);
            session.Back();
            Assert.That(session.Popup, Is.EqualTo(WirePopup.GoalEdit));
            session.SaveGoal();
            session.ConfirmData();
            Assert.That(session.Goal(false), Is.EqualTo(WireGoalState.Scheduled));
            Assert.That(session.SavedGoal(false, true), Does.Contain("3km"));
            session.CancelSchedule();
            Assert.That(session.Goal(false), Is.EqualTo(WireGoalState.Progress));
            Assert.That(session.SavedGoal(false), Does.Contain("5,000歩"));
        }

        [Test]
        public void OverlayBlocksBackgroundTransitionsAndResetClearsTheSession()
        {
            Explore();
            session.OpenChest();
            session.EnterDoor();
            Assert.That(session.Screen, Is.EqualTo(WireScreen.Explore));
            session.Back();
            session.ToggleDebug();
            session.Reset();
            Assert.That(session.Popup, Is.EqualTo(WirePopup.Intro));
            Assert.That(session.HasNewCompanion, Is.False);
            Assert.That(session.Runes, Is.EqualTo(860));
            Assert.That(session.Step, Is.Zero);
        }
    }
}
