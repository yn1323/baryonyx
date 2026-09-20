using UnityEngine;

namespace Baryonyx.Health
{
    [CreateAssetMenu(menuName = "Baryonyx/Health Connection Settings")]
    public sealed class HealthConnectionSettings : ScriptableObject
    {
        [Tooltip("Googleログイン用のWebクライアントID。クライアントシークレットは設定しない。")]
        public string GoogleWebClientId = "";

        [Tooltip("運動報酬APIのベースURL。未設定の場合、Health Connectのローカル表示だけを使う。")]
        public string ServerBaseUrl = "";
    }
}
