using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// A choice: a party member says what they found, the answers rise in once the line is
    /// written, and the one chosen is told in a new line while what it gives pops in below.
    /// An answer marked with an element only shows when someone in the party has a card of it.
    /// </summary>
    public sealed class WorldEventChoice : WorldEventPanel
    {
        public EventBubbles Talk = new();
        public CanvasGroup Answers;
        public Button[] Options = System.Array.Empty<Button>();

        // 選んだ答えごとに、もらう物の並び。
        public RectTransform[] Gifts = System.Array.Empty<RectTransform>();

        private const float Rise = 40f;

        private Vector2 answersHome;
        private bool homeKnown;
        private float answersAt = -1f;
        private float chosenAt;

        /// <summary>The answer chosen, or -1.</summary>
        public int Chosen { get; private set; } = -1;

        private void Awake()
        {
            for (int i = 0; i < Options.Length; i++)
            {
                int option = i;
                Options[i].onClick.AddListener(() => Choose(option));
            }
        }

        protected override void Restart()
        {
            var answers = (RectTransform)Answers.transform;
            if (!homeKnown)
            {
                answersHome = answers.anchoredPosition;
                homeKnown = true;
            }
            Talk.Start(new[] { new WorldEventLine(0, WorldEventSamples.ChoicePrompt) }, false);
            Chosen = -1;
            answersAt = -1f;
            Answers.alpha = 0f;
            Answers.interactable = Answers.blocksRaycasts = false;
            foreach (var gift in Gifts)
                gift.gameObject.SetActive(false);
        }

        /// <summary>Chooses an answer (an option's button).</summary>
        public void Choose(int option)
        {
            if (Chosen >= 0 || answersAt < 0f)
                return;
            Chosen = option;
            chosenAt = Clock;
            Answers.interactable = Answers.blocksRaycasts = false;
            Talk.Start(
                new[] { new WorldEventLine(0, WorldEventSamples.ChoiceResults[option]) },
                false
            );
        }

        public override void Advance() => Talk.Complete();

        protected override void Draw(float dt)
        {
            Talk.Tick(dt, true);
            var answers = (RectTransform)Answers.transform;
            if (Chosen < 0)
            {
                if (answersAt < 0f && Talk.Written)
                    answersAt = Clock;
                float rise = answersAt < 0f ? 0f : Mathf.Clamp01((Clock - answersAt) / 0.25f);
                Answers.alpha = rise;
                answers.anchoredPosition =
                    answersHome + new Vector2(0f, -Rise * (1f - EventMotion.Decelerate(rise)));
                Answers.interactable = Answers.blocksRaycasts = rise >= 1f;
                return;
            }
            Answers.alpha = Mathf.MoveTowards(Answers.alpha, 0f, dt / 0.15f);
            bool given = EventMotion.PopRow(Gifts[Chosen], Clock - chosenAt - 0.35f);
            Done = given && Talk.Written;
        }
    }
}
