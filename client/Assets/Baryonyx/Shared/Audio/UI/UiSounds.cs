using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Audio
{
    public enum UiSound
    {
        Press,
        Denied,
    }

    /// <summary>
    /// Plays the UI sounds on every screen (doc/rules/sound.md#押したときの音). <see cref="Install"/>
    /// puts one player in the game when it starts and keeps it across scenes. It gives every button
    /// and toggle a <see cref="UiClickSound"/> as they appear, so the screens need no changes. Glass
    /// cards play their own sound (Baryonyx.UI.GlassCard), since lifting the finger after a hold on
    /// them is not a press.
    /// </summary>
    public sealed class UiSounds : MonoBehaviour
    {
        private static UiSounds installed;

        private readonly HashSet<Selectable> seen = new();
        private Selectable[] buffer = new Selectable[64];
        private AudioSource source;
        private UiSoundBank bank;

        /// <summary>Raised for every sound asked for, with or without a player (for tests).</summary>
        public static event Action<UiSound> Played;

        /// <summary>The player in the game, once it has started.</summary>
        public static UiSounds Installed => installed;

        // Domain Reloadを省略したPlay開始でも、前回の部品と聞き手を持ち越さない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            installed = null;
            Played = null;
        }

        /// <summary>Puts the player in the game, once, and keeps it across scenes.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (installed != null)
                return;
            var found = UiSoundBank.Find();
            if (found == null)
            {
                Debug.LogWarning(
                    "The UI sounds are off: run Baryonyx > Audio > Create UI Sound Assets."
                );
                return;
            }
            var root = new GameObject("UiSounds");
            installed = root.AddComponent<UiSounds>();
            installed.bank = found;
            installed.source = root.AddComponent<AudioSource>();
            installed.source.playOnAwake = false;
            installed.source.spatialBlend = 0f;
            DontDestroyOnLoad(root);
        }

        /// <summary>Plays a UI sound, over any still sounding.</summary>
        public static void Play(UiSound sound)
        {
            Played?.Invoke(sound);
            if (installed != null)
                installed.PlayClip(sound);
        }

        private void PlayClip(UiSound sound)
        {
            var (clip, volume) =
                sound == UiSound.Press
                    ? (bank.Press, bank.PressVolume)
                    : (bank.Denied, bank.DeniedVolume);
            if (clip != null)
                source.PlayOneShot(clip, volume);
        }

        private void Update()
        {
            // 画面を移るたびに消えたボタンが残るため、ときどき捨てる。
            if (seen.Count > 1024)
                seen.RemoveWhere(s => s == null);
            if (buffer.Length < Selectable.allSelectableCount)
                buffer = new Selectable[Mathf.NextPowerOfTwo(Selectable.allSelectableCount)];
            int count = Selectable.AllSelectablesNoAlloc(buffer);
            for (int i = 0; i < count; i++)
            {
                var selectable = buffer[i];
                buffer[i] = null;
                if (!seen.Add(selectable))
                    continue;
                if (selectable is not (Button or Toggle))
                    continue;
                if (
                    selectable.TryGetComponent(out UI.GlassCard _)
                    || selectable.TryGetComponent(out UiClickSound _)
                )
                    continue;
                selectable.gameObject.AddComponent<UiClickSound>();
            }
        }
    }
}
