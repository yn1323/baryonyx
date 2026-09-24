namespace Baryonyx.Health
{
    // 連携モーダルの本文と主ボタンの文言。
    public readonly struct HealthLinkMessage
    {
        public const string Title = "Health Connectと連携";
        public const string LaterLabel = "あとで";

        private HealthLinkMessage(string body, string action)
        {
            Body = body;
            Action = action;
        }

        public string Body { get; }
        public string Action { get; }

        public static HealthLinkMessage For(HealthLinkStatus status) =>
            status switch
            {
                HealthLinkStatus.SettingsRequired => new HealthLinkMessage(
                    "許可画面を表示できません。Health Connectの設定を開き、このアプリに歩数の読み取りを許可してください。",
                    "Health Connectの設定を開く"
                ),
                HealthLinkStatus.InstallRequired => new HealthLinkMessage(
                    "この端末でHealth Connectを利用できません。Health Connectを入手してから連携してください。",
                    "Health Connectを入手"
                ),
                HealthLinkStatus.UpdateRequired => new HealthLinkMessage(
                    "Health Connectの更新が必要です。更新してから連携してください。",
                    "Health Connectを更新"
                ),
                _ => new HealthLinkMessage(
                    "歩いた分だけ冒険が進みます。\nHealth Connectから歩数などの運動データを読み取ります。",
                    "Health Connectと連携"
                ),
            };
    }
}
