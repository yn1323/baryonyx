using System;
using System.Collections;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Reveals the weaknesses one by one with their glint, then hides them all as "?" again, over
    /// and over, for the showcase: the glint only shows in the battle the first time a card hits
    /// a hidden weakness, so this lets it be watched again and again.
    /// </summary>
    public sealed class BattleWeaknessRevealDemo : MonoBehaviour
    {
        public BattleSkillVfx Vfx;
        public BattleInspectWeakness[] Weaknesses = Array.Empty<BattleInspectWeakness>();

        [Tooltip("1つの弱点の開示から次の開示までの間（秒）。")]
        [Min(0f)]
        public float Gap = 0.8f;

        [Tooltip("全部を開示してから「？」へ戻すまでの間（秒）。")]
        [Min(0f)]
        public float Hold = 1.4f;

        private IEnumerator Start()
        {
            if (Weaknesses.Length == 0)
                yield break;
            while (true)
            {
                foreach (var weakness in Weaknesses)
                    weakness.Show(false);
                yield return Wait(Gap);
                foreach (var weakness in Weaknesses)
                {
                    StartCoroutine(weakness.Reveal(Vfx));
                    yield return Wait(Gap);
                }
                yield return Wait(Hold);
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                yield return null;
        }
    }
}
