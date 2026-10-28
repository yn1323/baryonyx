using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// A battle: the enemies drop in and breathe, and the fight button swells the window into
    /// the white flash that leads to the battle screen. The cross lets the party back away.
    /// </summary>
    public sealed class WorldEventBattle : WorldEventPanel
    {
        public RectTransform[] Enemies = System.Array.Empty<RectTransform>();
        public Button Fight;
        public CanvasGroup FightGroup;

        private void Awake() => Fight.onClick.AddListener(() => Host.Depart());

        protected override void Restart()
        {
            FightGroup.alpha = 0f;
            FightGroup.interactable = FightGroup.blocksRaycasts = false;
            Done = true;
        }

        protected override void Draw(float dt)
        {
            for (int i = 0; i < Enemies.Length; i++)
            {
                float breath = Mathf.Sin(Clock * 2.4f + i * 1.7f) * 0.025f;
                Enemies[i].localScale = new Vector3(1f - breath * 0.5f, 1f + breath, 1f);
            }
            FightGroup.alpha = Mathf.Clamp01(Clock / 0.25f);
            FightGroup.interactable = FightGroup.blocksRaycasts = FightGroup.alpha >= 1f;
        }
    }
}
