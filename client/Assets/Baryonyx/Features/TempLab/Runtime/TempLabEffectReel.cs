using UnityEngine;

namespace Baryonyx.TempLab
{
    /// <summary>
    /// Plays TempLab's trial effects one after another and over again, each from its start after
    /// the one before has gone, so the room shows them all without any taps.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TempLabEffectReel : MonoBehaviour
    {
        [Tooltip("順に再生する試作のエフェクト。")]
        public TempLabEffect[] Effects = new TempLabEffect[0];

        [Tooltip("1つが消えてから次を始めるまでの間（秒）。")]
        public float Gap = 0.5f;

        private int index;

        private void Start()
        {
            foreach (var effect in Effects)
                if (effect != null)
                    effect.Hide();
            index = 0;
            if (Current != null)
                Current.Restart();
        }

        private TempLabEffect Current =>
            Effects.Length > 0 ? Effects[index % Effects.Length] : null;

        private void Update()
        {
            var current = Current;
            if (current == null)
                return;
            current.Play(Time.deltaTime);
            if (current.Clock < current.Duration + Gap)
                return;
            current.Hide();
            index = (index + 1) % Effects.Length;
            Current.Restart();
        }

        private void OnDisable()
        {
            foreach (var effect in Effects)
                if (effect != null)
                    effect.Hide();
        }
    }
}
