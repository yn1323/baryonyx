namespace Baryonyx.Health
{
    public enum HealthStepsDataState
    {
        NotChecked,
        Present,
        Empty,
        Failed,
    }

    // An immutable snapshot of facts, not a claim that a tracker is still syncing.
    public sealed class HealthRequirementState
    {
        public HealthAvailability? Availability { get; }
        public HealthPermission StepsPermission { get; }
        public HealthStepsDataState StepsData { get; }
        public int? AndroidApiLevel { get; }
        public int? SdkExtensionVersion { get; }
        public bool? HasStepCounter { get; }

        public HealthRequirementState(
            HealthAvailability? availability = null,
            HealthPermission stepsPermission = HealthPermission.Unknown,
            HealthStepsDataState stepsData = HealthStepsDataState.NotChecked,
            int? androidApiLevel = null,
            int? sdkExtensionVersion = null,
            bool? hasStepCounter = null
        )
        {
            Availability = availability;
            StepsPermission = stepsPermission;
            StepsData = stepsData;
            AndroidApiLevel = androidApiLevel;
            SdkExtensionVersion = sdkExtensionVersion;
            HasStepCounter = hasStepCounter;
        }

        public HealthRequirementState WithHealthState(
            HealthAvailability? availability,
            HealthPermission permission,
            HealthStepsDataState data
        ) =>
            new(
                availability,
                permission,
                data,
                AndroidApiLevel,
                SdkExtensionVersion,
                HasStepCounter
            );
    }

    public static class HealthRequirementCheck
    {
        public static HealthRequirementMessage Evaluate(HealthRequirementState state) =>
            new(Code(state));

        private static HealthRequirementCode Code(HealthRequirementState state)
        {
            if (state == null || !state.Availability.HasValue)
                return HealthRequirementCode.CheckFailed;
            if (state.Availability == HealthAvailability.Unavailable)
                return HealthRequirementCode.HealthUnavailable;
            if (state.Availability == HealthAvailability.UpdateRequired)
                return HealthRequirementCode.HealthUpdateRequired;
            if (state.StepsPermission == HealthPermission.NotGranted)
                return HealthRequirementCode.StepsPermissionRequired;
            if (state.StepsPermission != HealthPermission.Granted)
                return HealthRequirementCode.CheckFailed;
            if (state.StepsData == HealthStepsDataState.Present)
                return HealthRequirementCode.Ready;
            if (state.StepsData != HealthStepsDataState.Empty)
                return HealthRequirementCode.CheckFailed;
            if (!state.AndroidApiLevel.HasValue || !state.HasStepCounter.HasValue)
                return HealthRequirementCode.CheckFailed;
            if (!state.HasStepCounter.Value)
                return HealthRequirementCode.StepSensorUnavailable;
            if (state.AndroidApiLevel.Value < 34)
                return HealthRequirementCode.AndroidVersionUnsupportedForCounting;
            if (!state.SdkExtensionVersion.HasValue)
                return HealthRequirementCode.CheckFailed;
            if (state.SdkExtensionVersion.Value < 20)
                return HealthRequirementCode.SystemUpdateRequiredForCounting;
            return HealthRequirementCode.StepsDataPending;
        }
    }
}
