using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// The back of a glass card (an old book's leather cover with a gold-tooled ornament), shown
    /// over the contents while a list reads what to show (doc/features/screens/guide.md#カードの裏面).
    /// <see cref="FlipUp"/> turns the card face up over <see cref="HalfSeconds"/> twice: it turns
    /// about its upright axis, narrowing to an edge with the back showing and widening with the
    /// face, lifting a little on the way; a <see cref="Vertical"/> card turns about its level
    /// axis instead, narrowing in height. <see cref="FlipAway"/> turns a card that has nothing to
    /// show the same way, to an empty face, then fades it out over <see cref="FadeSeconds"/> and
    /// hides it. A card lying face down or leaving takes no taps. GlassCardAssets.AddBack builds
    /// the back; <see cref="GlassCardCover"/> lays a list's cards down and turns them up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlassCardBack : MonoBehaviour
    {
        // 閉じる時間と開く時間（秒）。合わせて0.5秒（ユーザーの指定、2026-10-27）。
        public const float HalfSeconds = 0.25f;

        // 何も出さないカードが、めくり終えてから消えるまで（秒）。ユーザーの指定で0.5秒から縮めた（2026-10-27）。
        public const float FadeSeconds = 0.3f;

        // 裏返る途中で持ち上がる大きさ（真横を向いたときの拡大の割合）。
        public const float Lift = 0.06f;

        public GlassCard Card;

        // 中身の上に重ねる裏面（革の地・内側の金の線・飾り・左右の線）。
        public GameObject Face;

        // 横の軸で回し、高さを縮めて上下にめくる。カードスキルの横長の行だけ（ユーザーの指定、2026-10-30）。
        public bool Vertical;

        private float delay;
        private float elapsed = float.PositiveInfinity;
        private float fading = float.PositiveInfinity;
        private bool leaving;

        public bool FaceDown => Face != null && Face.activeSelf;

        // めくっている途中（待っている間も含む）。
        public bool Flipping => elapsed < HalfSeconds * 2f;

        // 何も出さずにめくって消えていく途中。
        public bool Leaving => leaving;

        /// <summary>
        /// The card's width, as a part of its own, <paramref name="t"/> seconds into a flip; true
        /// in <paramref name="faceDown"/> while the back still shows. The card turns half a turn,
        /// starting and ending gently, and shows the cosine of its angle as its width.
        /// </summary>
        public static float WidthAt(float t, out bool faceDown)
        {
            float turned = Turned(t);
            faceDown = turned < 0.5f;
            return Mathf.Abs(Mathf.Cos(turned * Mathf.PI));
        }

        /// <summary>The card's size, as a part of its own, <paramref name="t"/> seconds into a flip.</summary>
        public static float LiftAt(float t) => 1f + Lift * Mathf.Sin(Turned(t) * Mathf.PI);

        // 裏返った割合（0で裏、1で表）。ゆっくり始まり、ゆっくり終わる。
        private static float Turned(float t) => Mathf.SmoothStep(0f, 1f, t / (HalfSeconds * 2f));

        /// <summary>Lays the card face down at once, stopping a flip.</summary>
        public void ShowFaceDown() => Settle(true);

        /// <summary>Turns the card face up at once, ending a flip or a leaving.</summary>
        public void ShowFaceUp() => Settle(false);

        /// <summary>
        /// Turns the card face up after <paramref name="after"/> seconds. A card not on screen, or
        /// already face up, shows its face at once.
        /// </summary>
        public void FlipUp(float after = 0f)
        {
            if (!FaceDown || !enabled || !gameObject.activeInHierarchy)
            {
                ShowFaceUp();
                return;
            }
            delay = Mathf.Max(0f, after);
            elapsed = 0f;
        }

        /// <summary>
        /// Shows the card face down, turns it after <paramref name="after"/> seconds to an empty
        /// face, then fades it out and hides it. A card that cannot show hides at once.
        /// </summary>
        public void FlipAway(float after = 0f)
        {
            gameObject.SetActive(true);
            if (!enabled || !gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }
            Settle(true);
            if (Card != null)
                Card.Selected = false;
            leaving = true;
            delay = Mathf.Max(0f, after);
            elapsed = 0f;
        }

        private void Update()
        {
            // ゲームの時間で進める（案内人の画面は時間を止めない。録画でも実際の速さで写る）。
            if (fading < FadeSeconds)
            {
                fading += Time.deltaTime;
                if (Card != null && Card.Group != null)
                    Card.Group.alpha = 1f - Mathf.Clamp01(fading / FadeSeconds);
                if (fading >= FadeSeconds)
                    gameObject.SetActive(false);
                return;
            }
            if (!Flipping)
                return;
            if (delay > 0f)
            {
                delay -= Time.deltaTime;
                return;
            }
            elapsed += Time.deltaTime;
            if (!Flipping)
            {
                if (!leaving)
                {
                    Settle(false);
                    return;
                }
                // めくり終えた空のカードを、押せないまま静かに消す。
                elapsed = float.PositiveInfinity;
                Narrow(1f, 1f);
                fading = 0f;
                return;
            }
            float width = WidthAt(elapsed, out bool faceDown);
            if (Face != null && Face.activeSelf != faceDown)
            {
                Face.SetActive(faceDown);
                if (leaving)
                    ShowContents(false);
            }
            Narrow(width, LiftAt(elapsed));
        }

        // 画面から消えたら、めくり終えた姿（表、中身あり）に戻す。
        private void OnDisable()
        {
            if (Flipping || leaving)
                Settle(false);
        }

        private void Settle(bool faceDown)
        {
            elapsed = float.PositiveInfinity;
            fading = float.PositiveInfinity;
            delay = 0f;
            if (leaving)
            {
                leaving = false;
                ShowContents(true);
            }
            if (Face != null)
                Face.SetActive(faceDown);
            Narrow(1f, 1f);
            if (Card != null && Card.Group != null)
            {
                // 伏せている間は、押せないカードの薄さでも裏を透かさず、下の一覧のスクロールだけを通す。
                Card.Group.alpha = faceDown ? 1f : Card.RestingAlpha;
                Card.Group.blocksRaycasts = !faceDown;
            }
        }

        // 板の上の中身（GlassCardAssets.Build の Content）。
        private void ShowContents(bool shown)
        {
            var contents = Card != null && Card.Body != null ? Card.Body.Find("Content") : null;
            if (contents != null)
                contents.gameObject.SetActive(shown);
        }

        // 板と影と選んだときの縁を、カードの中心を軸に横（Vertical なら縦）へ縮め、lift だけ大きくする。
        private void Narrow(float width, float lift)
        {
            if (Card == null)
                return;
            var scale = Vertical
                ? new Vector3(lift, width * lift, 1f)
                : new Vector3(width * lift, lift, 1f);
            if (Card.Body != null)
                Card.Body.localScale = scale;
            if (Card.Shadow != null)
                Card.Shadow.localScale = scale;
            if (Card.Selection != null)
                Card.Selection.transform.localScale = scale;
        }
    }
}
