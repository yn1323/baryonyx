using System.Collections;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The end of a won battle on the battle screen (doc/features/screens/battle.md#勝ったあとの手札):
    /// the hand sinks out of sight and its counters and the end-turn button fade with it, so the
    /// drops and the boons that follow have the screen to themselves; and the "勝利！" band's words
    /// spring in from wide and large. A revive (after a defeat) and the lab's restore bring the
    /// hand back.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        // How far the hand sinks as it goes away, and how long it takes.
        private const float HandAwayDrop = 360f;
        private const float HandAwayTime = 0.35f;

        // How the band's words spring in: from this much larger and this much wider apart.
        private const float PunchScale = 1.7f;
        private const float BossPunchScale = 2.1f;
        private const float PunchSpacing = 40f;
        private const float PunchTime = 0.45f;

        // The boss's win leaves its words a little larger.
        private const float BossWordsScale = 1.15f;

        private bool handAway;
        private Vector2 handHome;
        private float bandSpacing = float.NaN;
        private Coroutine punch;

        /// <summary>True from the win until a revive or the lab's restore: the hand is put away.</summary>
        public bool HandPutAway => handAway;

        /// <summary>The hand and the things that work it, faded and blocked together.</summary>
        private CanvasGroup[] ControlGroups()
        {
            var energy =
                EnergyLabel != null
                    ? EnergyLabel.GetComponentInParent<TranslucentTextPanel>()
                    : null;
            var parts = new Transform[]
            {
                Hand,
                energy != null ? energy.transform : null,
                DeckAnchor,
                DiscardAnchor,
                ExhaustAnchor,
                EndTurnButton != null ? EndTurnButton.transform : null,
            };
            var groups = new CanvasGroup[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                    continue;
                if (!parts[i].TryGetComponent(out groups[i]))
                    groups[i] = parts[i].gameObject.AddComponent<CanvasGroup>();
                if (parts[i] == Hand)
                    handGroup = groups[i];
            }
            return groups;
        }

        /// <summary>The hand sinks and fades away with its counters; nothing of it takes a tap.</summary>
        private IEnumerator PutHandAway()
        {
            if (handAway || Hand == null)
                yield break;
            handAway = true;
            if (held >= 0)
                PutBack();
            if (TapArea != null)
                TapArea.SetActive(false);
            handHome = Hand.anchoredPosition;
            var groups = ControlGroups();
            foreach (var group in groups)
                if (group != null)
                    group.blocksRaycasts = false;
            for (float t = 0f; t < HandAwayTime && handAway; t += Time.deltaTime)
            {
                float k = t / HandAwayTime;
                Hand.anchoredPosition = handHome + Vector2.down * (HandAwayDrop * k * k);
                foreach (var group in groups)
                    if (group != null)
                        group.alpha = 1f - k;
                yield return null;
            }
            if (!handAway)
                yield break;
            Hand.anchoredPosition = handHome + Vector2.down * HandAwayDrop;
            foreach (var group in groups)
                if (group != null)
                    group.alpha = 0f;
        }

        /// <summary>Puts the hand and its counters back where they were, if they were put away.</summary>
        private void BringHandBack()
        {
            if (!handAway || Hand == null)
                return;
            handAway = false;
            Hand.anchoredPosition = handHome;
            foreach (var group in ControlGroups())
            {
                if (group == null)
                    continue;
                group.alpha = 1f;
                group.blocksRaycasts = true;
            }
        }

        /// <summary>Springs the band's words in (see <see cref="PunchBand"/>), cutting short one under way.</summary>
        private void StartPunch(bool boss)
        {
            UnpunchBand();
            if (isActiveAndEnabled)
                punch = StartCoroutine(PunchBand(boss));
        }

        /// <summary>
        /// The band's words spring in from large and wide apart, overshooting a little as they
        /// settle; larger for the <paramref name="boss"/>'s win, whose words stay a little larger.
        /// </summary>
        private IEnumerator PunchBand(bool boss)
        {
            if (TurnBanner == null || TurnBanner.Label == null)
                yield break;
            var label = TurnBanner.Label;
            if (float.IsNaN(bandSpacing))
                bandSpacing = label.characterSpacing;
            float from = boss ? BossPunchScale : PunchScale;
            float rest = boss ? BossWordsScale : 1f;
            for (float t = 0f; t < PunchTime; t += Time.deltaTime)
            {
                float k = t / PunchTime;
                float settle = SpringIn(k);
                float scale = Mathf.LerpUnclamped(from, rest, settle);
                label.rectTransform.localScale = new Vector3(scale, scale, 1f);
                label.characterSpacing = bandSpacing + PunchSpacing * (1f - settle);
                yield return null;
            }
            label.rectTransform.localScale = new Vector3(rest, rest, 1f);
            label.characterSpacing = bandSpacing;
            // The words keep their size while the band shows, then the next band reads as usual.
            while (TurnBanner.gameObject.activeSelf)
                yield return null;
            punch = null;
            ResetBandWords();
        }

        /// <summary>The band's words back at their own size and spacing.</summary>
        private void UnpunchBand()
        {
            if (punch != null)
                StopCoroutine(punch);
            punch = null;
            ResetBandWords();
        }

        private void ResetBandWords()
        {
            if (TurnBanner == null || TurnBanner.Label == null)
                return;
            TurnBanner.Label.rectTransform.localScale = Vector3.one;
            if (!float.IsNaN(bandSpacing))
                TurnBanner.Label.characterSpacing = bandSpacing;
        }

        // An ease-out that overshoots a little and settles back, like a spring.
        private static float SpringIn(float k)
        {
            const float overshoot = 1.7f;
            float t = Mathf.Clamp01(k) - 1f;
            return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
        }
    }
}
