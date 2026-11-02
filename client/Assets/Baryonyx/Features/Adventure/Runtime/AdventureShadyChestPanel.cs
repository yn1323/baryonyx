using System;
using Baryonyx.WorldMap;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The shady chest's room, in the event window (doc/features/screens/adventure.md): the
    /// chest sits in the middle in a faint violet glow and now and then rattles as if something
    /// inside stirred, and under it the two answers, open or pass by. The × closes without
    /// choosing, and a tap on the chest on the map asks again.
    /// </summary>
    public sealed class AdventureShadyChestPanel : WorldEventPanel
    {
        public RawImage Chest;
        public RawImage Aura;
        public Button OpenButton;
        public Button PassButton;

        public event Action Opened;
        public event Action Passed;

        private bool wired;

        private void Awake() => Wire();

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
            if (OpenButton != null)
                OpenButton.onClick.AddListener(() => Opened?.Invoke());
            if (PassButton != null)
                PassButton.onClick.AddListener(() => Passed?.Invoke());
        }

        protected override void Restart()
        {
            Wire();
            if (Aura != null)
                Aura.texture = WorldMapEventWindow.GlowTexture;
            if (Chest != null)
                Chest.rectTransform.localRotation = Quaternion.identity;
            Done = true;
        }

        protected override void Draw(float dt)
        {
            // ときどき、中の何かが動いたように2度小さく跳ねて揺れる。
            float p = Clock % 2.2f;
            float rattle = p < 0.5f ? Mathf.Sin(p / 0.5f * Mathf.PI * 4f) * (1f - p / 0.5f) : 0f;
            if (Chest != null)
            {
                var chest = Chest.rectTransform;
                chest.localRotation = Quaternion.Euler(0f, 0f, rattle * 4f);
                chest.anchoredPosition = new Vector2(0f, Mathf.Abs(rattle) * 10f);
            }
            if (Aura != null)
                Aura.color = EventMotion.WithAlpha(
                    Aura.color,
                    0.45f + 0.15f * Mathf.Sin(Clock * 2.2f) + 0.2f * Mathf.Abs(rattle)
                );
        }
    }
}
