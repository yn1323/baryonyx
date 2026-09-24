using UnityEngine;
using UnityEngine.Serialization;

namespace Baryonyx.Health
{
    [CreateAssetMenu(menuName = "Baryonyx/Health Connection Settings")]
    public sealed class HealthConnectionSettings : ScriptableObject
    {
        [Tooltip("Googleログイン用のWebクライアントID。クライアントシークレットは設定しない。")]
        public string GoogleWebClientId = "";

        [FormerlySerializedAs("ServerBaseUrl")]
        [Tooltip(
            "Dev環境のゲームサーバーのURL。環境を指定しないAPKとPreviewのAPKもここへ接続する。"
        )]
        public string DevServerUrl = "";

        [Tooltip("Prod環境のゲームサーバーのURL。未設定ならProd向けのAPKはビルドを止める。")]
        public string ProdServerUrl = "";

        // APKのビルド中だけ、選んだ環境のURLを入れる。ビルド後は空へ戻すため、リポジトリでは常に空。
        [HideInInspector]
        public string BuildServerUrl = "";

        [Tooltip(
            "Editorなどのプレビューで、Health Connect未連携の状態から始める。Android版には影響しない。"
        )]
        public bool PreviewStartsUnlinked;
    }
}
