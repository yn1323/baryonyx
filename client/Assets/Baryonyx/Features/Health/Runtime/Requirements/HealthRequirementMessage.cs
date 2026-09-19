namespace Baryonyx.Health
{
    public enum HealthRequirementCode
    {
        Ready,
        HealthUnavailable,
        HealthUpdateRequired,
        CheckFailed,
        StepsPermissionRequired,
        StepSensorUnavailable,
        AndroidVersionUnsupportedForCounting,
        SystemUpdateRequiredForCounting,
        StepsDataPending,
    }

    public enum HealthSettingsDestination
    {
        HealthConnect,
        Device,
    }

    public sealed class HealthRequirementMessage
    {
        public HealthRequirementCode Code { get; }
        public string Text { get; }
        public HealthSettingsDestination Destination { get; }
        public bool HasNotice => Code != HealthRequirementCode.Ready;

        public HealthRequirementMessage(HealthRequirementCode code)
        {
            Code = code;
            Destination =
                code == HealthRequirementCode.AndroidVersionUnsupportedForCounting
                || code == HealthRequirementCode.SystemUpdateRequiredForCounting
                    ? HealthSettingsDestination.Device
                    : HealthSettingsDestination.HealthConnect;
            Text = code switch
            {
                HealthRequirementCode.Ready => "",
                HealthRequirementCode.HealthUnavailable =>
                    "この端末ではHealth Connectを利用できません。端末の対応状況と、Health Connectが有効になっているか確認してください。",
                HealthRequirementCode.HealthUpdateRequired =>
                    "Health Connectのインストールまたは更新が必要です。設定を開いて確認してください。",
                HealthRequirementCode.StepsPermissionRequired =>
                    "歩数の読み取りが許可されていません。Health Connectの設定で、このアプリの「歩数」へのアクセスを許可してください。",
                HealthRequirementCode.StepSensorUnavailable =>
                    "この端末では歩数の自動計測を利用できません。Health Connectへ歩数を送れるアプリや機器を連携してください。",
                HealthRequirementCode.AndroidVersionUnsupportedForCounting =>
                    "このAndroidバージョンではHealth Connectの自動計測を利用できません。対応する記録アプリから歩数を連携してください。端末が対応している場合はAndroidの更新も利用できます。",
                HealthRequirementCode.SystemUpdateRequiredForCounting =>
                    "端末の自動計測を利用するには、設定から「Google Playシステムアップデート」を確認してください。対応する別アプリから歩数を連携する方法も利用できます。",
                HealthRequirementCode.StepsDataPending =>
                    "歩数データがまだありません。歩いたあとに時間を置いて更新してください。記録されない場合は、Health Connectの歩数記録や連携元の設定を確認してください。",
                _ =>
                    "歩数の利用条件を確認できませんでした。時間を置いて、アプリを起動し直してください。",
            };
        }
    }
}
