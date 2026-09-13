using Baryonyx.Health;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class HealthRequirementCheckTests
    {
        [TestCase(33, 0, true, HealthRequirementCode.AndroidVersionUnsupportedForCounting)]
        [TestCase(34, 19, true, HealthRequirementCode.SystemUpdateRequiredForCounting)]
        [TestCase(34, 20, true, HealthRequirementCode.StepsDataPending)]
        [TestCase(36, 20, true, HealthRequirementCode.StepsDataPending)]
        [TestCase(36, 19, true, HealthRequirementCode.SystemUpdateRequiredForCounting)]
        [TestCase(34, 20, false, HealthRequirementCode.StepSensorUnavailable)]
        [TestCase(33, 0, false, HealthRequirementCode.StepSensorUnavailable)]
        public void MissingStepsExplainsTheActualDeviceCondition(
            int api,
            int extension,
            bool sensor,
            HealthRequirementCode expected
        )
        {
            var state = new HealthRequirementState(
                HealthAvailability.Available,
                HealthPermission.Granted,
                HealthStepsDataState.Empty,
                api,
                extension,
                sensor
            );
            Assert.That(HealthRequirementCheck.Evaluate(state).Code, Is.EqualTo(expected));
        }

        [TestCase(HealthAvailability.Unavailable, HealthRequirementCode.HealthUnavailable)]
        [TestCase(HealthAvailability.UpdateRequired, HealthRequirementCode.HealthUpdateRequired)]
        public void ProviderAvailabilityTakesPriority(
            HealthAvailability availability,
            HealthRequirementCode code
        )
        {
            var state = new HealthRequirementState(
                availability,
                HealthPermission.NotGranted,
                HealthStepsDataState.NotChecked,
                33,
                null,
                false
            );
            Assert.That(HealthRequirementCheck.Evaluate(state).Code, Is.EqualTo(code));
        }

        [Test]
        public void StepPermissionCannotBeReplacedByDeviceSupportOrOldData()
        {
            var state = new HealthRequirementState(
                HealthAvailability.Available,
                HealthPermission.NotGranted,
                HealthStepsDataState.Present,
                36,
                20,
                true
            );
            Assert.That(
                HealthRequirementCheck.Evaluate(state).Code,
                Is.EqualTo(HealthRequirementCode.StepsPermissionRequired)
            );
        }

        [Test]
        public void ReadableExternalStepsWorkWithoutNativeCounting()
        {
            var state = new HealthRequirementState(
                HealthAvailability.Available,
                HealthPermission.Granted,
                HealthStepsDataState.Present,
                28,
                null,
                false
            );
            var message = HealthRequirementCheck.Evaluate(state);
            Assert.That(message.Code, Is.EqualTo(HealthRequirementCode.Ready));
            Assert.That(message.HasNotice, Is.False);
            Assert.That(message.Text, Is.Empty);
        }

        [Test]
        public void UnknownFactsAndFailedQueriesNeverMeanUnsupportedOrEmpty()
        {
            foreach (
                var state in new[]
                {
                    new HealthRequirementState(),
                    new HealthRequirementState(HealthAvailability.Available),
                    new HealthRequirementState(
                        HealthAvailability.Available,
                        HealthPermission.Granted,
                        HealthStepsDataState.NotChecked,
                        34,
                        20,
                        true
                    ),
                    new HealthRequirementState(
                        HealthAvailability.Available,
                        HealthPermission.Granted,
                        HealthStepsDataState.Failed,
                        33,
                        null,
                        false
                    ),
                    new HealthRequirementState(
                        HealthAvailability.Available,
                        HealthPermission.Granted,
                        HealthStepsDataState.Empty,
                        34,
                        null,
                        true
                    ),
                    new HealthRequirementState(
                        HealthAvailability.Available,
                        HealthPermission.Granted,
                        HealthStepsDataState.Empty,
                        34,
                        20,
                        null
                    ),
                }
            )
                Assert.That(
                    HealthRequirementCheck.Evaluate(state).Code,
                    Is.EqualTo(HealthRequirementCode.CheckFailed)
                );
        }

        [Test]
        public void UpdatingHealthFactsPreservesDeviceSnapshotAndRoutesSettings()
        {
            var state = new HealthRequirementState(
                HealthAvailability.Available,
                HealthPermission.NotGranted,
                HealthStepsDataState.NotChecked,
                34,
                19,
                true
            );
            var permitted = state.WithHealthState(
                HealthAvailability.Available,
                HealthPermission.Granted,
                HealthStepsDataState.Empty
            );
            Assert.That(state.StepsPermission, Is.EqualTo(HealthPermission.NotGranted));
            Assert.That(
                HealthRequirementCheck.Evaluate(permitted).Destination,
                Is.EqualTo(HealthSettingsDestination.Device)
            );
            Assert.That(
                HealthRequirementCheck.Evaluate(state).Destination,
                Is.EqualTo(HealthSettingsDestination.HealthConnect)
            );
        }
    }
}
