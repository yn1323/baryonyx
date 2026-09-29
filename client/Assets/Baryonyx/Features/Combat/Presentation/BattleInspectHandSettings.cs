using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Sizes and motion of the mock battle's hand, kept in an asset so they can be tuned in the
    /// Inspector while watching the screen. The view reads them every frame, so edits made in
    /// Play Mode show at once and, being an asset, stay after Play Mode ends.
    /// </summary>
    [CreateAssetMenu(menuName = "Baryonyx/Combat/Battle Inspect Hand Settings")]
    public sealed class BattleInspectHandSettings : ScriptableObject
    {
        [Header("カードの大きさ")]
        [Tooltip("手札のカードの倍率。1でカードのPrefabどおり（1ドット5px）。")]
        [Min(0.1f)]
        public float CardScale = 1f;

        [Tooltip("押したカードの倍率（CardScaleに掛ける）。")]
        [Min(0.1f)]
        public float RaisedScale = 1.5f;

        [Tooltip("押したカードの下端の、画面下端からの高さ（px）。")]
        public float RaisedBottom = 16f;

        [Header("手札の扇")]
        [Tooltip("隣のカードとの角度の差（度）。")]
        public float FanStep = 7f;

        [Tooltip("扇の円の半径（px）。大きいほどカードの間隔が広がる。")]
        [Min(1f)]
        public float FanRadius = 1300f;

        [Tooltip("端のカードの沈み方。1で円のとおり、0で横一列。")]
        [Range(0f, 1f)]
        public float FanDrop = 0.5f;

        [Tooltip(
            "真ん中のカードの下端の、画面下端からの高さ（px）。マイナスで画面の外へはみ出す。"
        )]
        public float RestBottom = -122f;

        [Header("操作")]
        [Tooltip("押してからこれだけ上へ動かすと、対象を選ぶ状態になる（px）。")]
        [Min(0f)]
        public float ArmDistance = 100f;

        [Tooltip("キャラの絵の周りで、ドロップを受け付ける余白（px）。")]
        [Min(0f)]
        public float TargetMargin = 72f;

        [Header("ばね")]
        [Tooltip("カードが目標へ向かう強さ。大きいほど速い。")]
        [Min(1f)]
        public float Stiffness = 320f;

        [Tooltip("揺れの収まり方。小さいほど大きく弾む。")]
        [Min(0f)]
        public float Damping = 17f;
    }
}
