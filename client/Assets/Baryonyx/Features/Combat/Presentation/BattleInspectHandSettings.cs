using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Sizes and motion of the mock battle's hand (and the characters' steps when they act), kept in an asset so they can be tuned in the
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
        public float RaisedScale = 1.6f;

        [Tooltip("押したカードは画面の左右中央・下寄せに出す。その下端と画面下端のすき間（px）。")]
        public float RaisedBottom = 24f;

        [Header("使えないカード")]
        [Tooltip(
            "エネルギーが足りない手札のカードの不透明度。1で透けない。押して上げたカードは説明を読めるよう透けさせない。"
        )]
        [Range(0f, 1f)]
        public float UnplayableAlpha = 0.7f;

        [Tooltip(
            "エネルギーが足りないカードに重ねる黒の濃さ。0で暗くしない。透けさせた分を差し引いた見た目の濃さで、上限はUnplayableAlpha（手札にある間）。コストの数字も暗くする。"
        )]
        [Range(0f, 1f)]
        public float UnplayableDarkness = 0.7f;

        [Header("手札の扇")]
        [Tooltip("隣のカードとの角度の差（度）。")]
        public float FanStep = 7f;

        [Tooltip(
            "両端のカードの角度の差の上限（度）。枚数が多くてこれを超えるときは、隣との角度の差を詰める。"
        )]
        [Min(0f)]
        public float FanMaxSpread = 42f;

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

        [Header("山札と配り方")]
        [Tooltip("戦闘の開始時に配る枚数。")]
        [Min(0)]
        public int OpeningDraw = 6;

        [Tooltip("2ターン目から、ターンの開始時に引く枚数。")]
        [Min(0)]
        public int TurnDraw = 3;

        [Tooltip("手札の上限。上限に届いたら、残りは引かずに山札に残す。")]
        [Min(1)]
        public int HandLimit = 9;

        [Tooltip("戦闘の開始から、1枚目を配り始めるまでの待ち（秒）。")]
        [Min(0f)]
        public float DealDelay = 0.3f;

        [Tooltip("1枚配ってから次の1枚を出すまでの間隔（秒）。")]
        [Min(0f)]
        public float DealInterval = 0.12f;

        [Tooltip("山札から手札の位置まで飛ぶ時間（秒）。この間に裏から表へ返る。")]
        [Min(0.01f)]
        public float DealFlight = 0.35f;

        [Tooltip("山札を出るときのカードの倍率（手札での大きさに対して）。")]
        [Range(0.05f, 1f)]
        public float DealStartScale = 0.2f;

        [Tooltip("山札を出るときの傾き（度）。手札の傾きへ戻りながら飛ぶ。")]
        public float DealStartAngle = 24f;

        [Header("操作")]
        [Tooltip("押してからこれだけ上へ動かすと、対象を選ぶ状態になる（px）。")]
        [Min(0f)]
        public float ArmDistance = 100f;

        [Tooltip(
            "押してから離すまでの動きがこれ以下なら、タップとして扱い、カードを上げたまま対象のタップを待つ（px）。"
        )]
        [Min(0f)]
        public float TapSlop = 24f;

        [Tooltip("キャラの絵の周りで、ドロップを受け付ける余白（px）。")]
        [Min(0f)]
        public float TargetMargin = 72f;

        [Header("行動の演出")]
        [Tooltip(
            "カードを使った味方と、攻撃する敵が、相手の側へ出る距離（px）。1ドット（4px）単位に丸める。"
        )]
        [Min(0f)]
        public float StepDistance = 64f;

        [Tooltip("1歩出る時間と、元の場所へ戻る時間（秒）。")]
        [Min(0.01f)]
        public float StepTime = 0.12f;

        [Tooltip("効果が出てから、元の場所へ戻り始めるまでの待ち（秒）。")]
        [Min(0f)]
        public float ActionHold = 0.45f;

        [Tooltip("敵のターンで、敵が1体ずつ動き出す前の間（秒）。")]
        [Min(0f)]
        public float EnemyGap = 0.25f;

        [Header("ばね")]
        [Tooltip("カードが目標へ向かう強さ。大きいほど速い。")]
        [Min(1f)]
        public float Stiffness = 320f;

        [Tooltip("揺れの収まり方。小さいほど大きく弾む。")]
        [Min(0f)]
        public float Damping = 17f;
    }
}
