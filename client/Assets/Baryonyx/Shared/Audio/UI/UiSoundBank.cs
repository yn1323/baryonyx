using UnityEngine;

namespace Baryonyx.Audio
{
    /// <summary>
    /// The UI sounds and how loud each plays (doc/rules/sound.md#押したときの音). The game loads it
    /// at start (a build puts it in Preloaded Assets, UiSoundBuildStep), so <see cref="UiSounds"/>
    /// finds it without any scene holding it.
    /// </summary>
    public sealed class UiSoundBank : ScriptableObject
    {
        public const string AssetPath = "Assets/Baryonyx/Shared/Audio/UI/UiSoundBank.asset";

        [Tooltip("押せるボタンを押したとき（決定・戻る・選ぶ）。")]
        public AudioClip Press;

        [Tooltip("押せるボタンの音の大きさ。ファイルはピーク−1dBFSにそろえ、ここで下げる。")]
        [Range(0f, 1f)]
        public float PressVolume = 0.5f;

        [Tooltip("押せないボタンを押したとき。")]
        public AudioClip Denied;

        [Tooltip("押せないボタンの音の大きさ。押せる音より詰まって大きく聞こえるため、低くしてある。")]
        [Range(0f, 1f)]
        public float DeniedVolume = 0.3f;

        /// <summary>The bank the game loaded, or null when it is not set up.</summary>
        public static UiSoundBank Find()
        {
#if UNITY_EDITOR
            // Editorでは、Preloaded Assetsの物がPlayの前に読み込まれているとは限らない。
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UiSoundBank>(AssetPath);
            if (asset != null)
                return asset;
#endif
            var loaded = Resources.FindObjectsOfTypeAll<UiSoundBank>();
            return loaded.Length > 0 ? loaded[0] : null;
        }
    }
}
