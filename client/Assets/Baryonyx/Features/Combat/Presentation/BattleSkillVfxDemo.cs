using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Plays every skill effect in turn, over and over, for the showcase: the effects only show
    /// in the battle while cards are played, so this lets them be watched without playing.
    /// </summary>
    public sealed class BattleSkillVfxDemo : MonoBehaviour
    {
        /// <summary>One skill to show: its effect, its user and the characters it lands on.</summary>
        [Serializable]
        public sealed class Cast
        {
            public BattleSkillVfxKind Kind;
            public string Name;
            public RectTransform Caster;
            public Texture CasterArt;
            public bool CutIn;
            public RectTransform[] Targets = Array.Empty<RectTransform>();
        }

        public BattleSkillVfx Vfx;
        public Cast[] Casts = Array.Empty<Cast>();

        /// <summary>The characters the effects light up.</summary>
        public RawImage[] Actors = Array.Empty<RawImage>();

        [Tooltip("1つのスキルが終わってから次のスキルまでの間（秒）。")]
        [Min(0f)]
        public float Gap = 0.9f;

        private IEnumerator Start()
        {
            if (Vfx == null || Casts.Length == 0)
                yield break;
            foreach (var actor in Actors)
                Vfx.RegisterActor(actor);
            for (int round = 0; ; round++)
            {
                foreach (var cast in Casts)
                {
                    // Every other round the blows land on a weakness, to show the harder hit.
                    var weight = round % 2 == 1 ? BattleHitWeight.Weak : BattleHitWeight.Normal;
                    yield return Vfx.Play(
                        cast.Kind,
                        cast.Name,
                        cast.Caster,
                        cast.CasterArt,
                        cast.CutIn,
                        cast.Targets,
                        _ => weight
                    );
                    for (float t = 0f; t < Gap; t += Time.deltaTime)
                        yield return null;
                }
            }
        }
    }
}
