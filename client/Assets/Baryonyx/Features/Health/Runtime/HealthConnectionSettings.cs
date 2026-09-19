using UnityEngine;

namespace Baryonyx.Health
{
    [CreateAssetMenu(menuName = "Baryonyx/Health Connection Settings")]
    public sealed class HealthConnectionSettings : ScriptableObject
    {
        [Tooltip("Googleログイン用のWebクライアントID。クライアントシークレットは設定しない。")]
        public string GoogleWebClientId = "";
    }
}
