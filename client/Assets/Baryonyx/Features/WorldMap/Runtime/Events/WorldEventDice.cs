using System.Collections.Generic;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// A bet played as high and low with one die: paying the stake puts it in the pot and rolls
    /// the die. Each right guess of whether the next roll is higher or lower doubles the pot, a
    /// same number is a draw, and a wrong guess loses it all. After a right guess the player may
    /// stop and take the pot. A party member good at it may roll the shown number again, once.
    /// </summary>
    public sealed class WorldEventDice : WorldEventPanel
    {
        // 目ごとに点る目の点。点は左上・右上・左中・中央・右中・左下・右下の順。
        private static readonly bool[][] Faces =
        {
            new[] { false, false, false, true, false, false, false },
            new[] { true, false, false, false, false, false, true },
            new[] { true, false, false, true, false, false, true },
            new[] { true, true, false, false, false, true, true },
            new[] { true, true, false, true, false, true, true },
            new[] { true, true, true, false, true, true, true },
        };

        public RectTransform Die;
        public Graphic[] Pips = System.Array.Empty<Graphic>();
        public TMP_Text Runes;
        public TMP_Text Pot;
        public RectTransform PotPlate;
        public Button Play;
        public CanvasGroup PlayGroup;
        public Button High;
        public Button Low;
        public Button Stop;
        public CanvasGroup GuessGroup;
        public CanvasGroup StopGroup;
        public Button Reroll;
        public CanvasGroup RerollGroup;
        public RectTransform Win;
        public TMP_Text WinLabel;
        public RectTransform Lose;

        private const float RollSeconds = 0.62f;
        private const float FlickerSeconds = 0.07f;
        private const float Hop = 120f;

        private enum Step
        {
            Ready,
            Rolling,
            Guess,
            Won,
            Lost,
        }

        // 振った目を、当てたあとにどうするか。
        private enum After
        {
            Guess,
            Judge,
        }

        private readonly Queue<int> rigged = new();
        private System.Random random;
        private Step step;
        private float stepAt;
        private After after;
        private bool guessHigh;
        private int shownFace;
        private float shownBalance;
        private float shownPot;
        private bool rerolled;

        public int Balance { get; private set; }

        /// <summary>The runes piled up by this bet (the stake and what right guesses doubled).</summary>
        public int PotRunes { get; private set; }

        /// <summary>The number the die shows (1 to 6).</summary>
        public int Face { get; private set; } = 3;

        /// <summary>Right guesses so far.</summary>
        public int Wins { get; private set; }

        /// <summary>A guess (or stopping) can be made now.</summary>
        public bool Guessing => step == Step.Guess;

        public bool Lost => step == Step.Lost;

        private void Awake()
        {
            Play.onClick.AddListener(Begin);
            High.onClick.AddListener(() => Guess(true));
            Low.onClick.AddListener(() => Guess(false));
            Stop.onClick.AddListener(Quit);
            Reroll.onClick.AddListener(RollAgain);
        }

        /// <summary>The next rolls give these numbers (tests), before random ones.</summary>
        public void Rig(params int[] faces)
        {
            foreach (int face in faces)
                rigged.Enqueue(Mathf.Clamp(face, 1, 6));
        }

        protected override void Restart()
        {
            random ??= new System.Random(System.Environment.TickCount);
            step = Step.Ready;
            stepAt = 0f;
            Balance = WorldEventSamples.PeddlerRunes;
            shownBalance = Balance;
            PotRunes = 0;
            shownPot = 0f;
            Wins = 0;
            rerolled = false;
            Face = 3;
            ShowFace(Face);
            Die.anchoredPosition = Vector2.zero;
            Die.localRotation = Quaternion.identity;
            Die.localScale = Vector3.one;
            Runes.text = Balance.ToString("N0");
            Pot.text = "0";
            Pot.color = UiPalette.GoldLight;
            PotPlate.gameObject.SetActive(false);
            PotPlate.localRotation = Quaternion.identity;
            PotPlate.localScale = Vector3.one;
            Show(PlayGroup, true);
            Show(GuessGroup, false);
            Show(StopGroup, false);
            Show(RerollGroup, false);
            Win.gameObject.SetActive(false);
            Lose.gameObject.SetActive(false);
        }

        /// <summary>Pays the stake into the pot and rolls the first number (the play button).</summary>
        public void Begin()
        {
            if (step != Step.Ready)
                return;
            Balance -= WorldEventSamples.BetStake;
            PotRunes = WorldEventSamples.BetStake;
            PotPlate.gameObject.SetActive(true);
            Show(PlayGroup, false);
            Roll(After.Guess);
        }

        /// <summary>Guesses whether the next number is higher, and rolls it.</summary>
        public void Guess(bool higher)
        {
            if (step != Step.Guess)
                return;
            guessHigh = higher;
            Roll(After.Judge);
        }

        /// <summary>Takes the pot (only after a right guess).</summary>
        public void Quit()
        {
            if (step != Step.Guess || Wins == 0)
                return;
            Cash();
        }

        /// <summary>The party member's help: rolls the shown number again, once.</summary>
        public void RollAgain()
        {
            if (step != Step.Guess || rerolled || !Host.Helped)
                return;
            rerolled = true;
            Roll(After.Guess);
        }

        private void Roll(After then)
        {
            after = then;
            shownFace = Face;
            Face = rigged.Count > 0 ? rigged.Dequeue() : random.Next(1, 7);
            Show(GuessGroup, false);
            Show(StopGroup, false);
            Show(RerollGroup, false);
            Go(Step.Rolling);
        }

        private void Landed()
        {
            ShowFace(Face);
            if (after == After.Guess)
            {
                AskGuess();
                return;
            }
            // 同じ目は引き分けで、今の目のまま、もう一度当てる。
            if (Face == shownFace)
            {
                AskGuess();
                return;
            }
            if (Face > shownFace != guessHigh)
            {
                PotRunes = 0;
                Pot.color = new Color(0.95f, 0.38f, 0.32f, 1f);
                Lose.gameObject.SetActive(true);
                Go(Step.Lost);
                return;
            }
            Wins++;
            PotRunes *= 2;
            if (Wins >= WorldEventSamples.DiceWins)
                Cash();
            else
                AskGuess();
        }

        private void AskGuess()
        {
            Go(Step.Guess);
            Show(GuessGroup, true);
            Show(StopGroup, Wins > 0);
            Show(RerollGroup, Host.Helped && !rerolled);
            // 1より低い目・6より高い目はないので、当たらない方は押せなくする。
            High.interactable = Face < 6;
            Low.interactable = Face > 1;
        }

        private void Cash()
        {
            Balance += PotRunes;
            WinLabel.text = $"{PotRunes:N0}ルーン";
            Show(GuessGroup, false);
            Show(StopGroup, false);
            Show(RerollGroup, false);
            Go(Step.Won);
        }

        private void Go(Step next)
        {
            step = next;
            stepAt = Clock;
        }

        protected override void Draw(float dt)
        {
            float t = Clock - stepAt;
            shownBalance = Mathf.MoveTowards(shownBalance, Balance, 2400f * dt);
            Runes.text = Mathf.RoundToInt(shownBalance).ToString("N0");
            shownPot = Mathf.MoveTowards(shownPot, PotRunes, Mathf.Max(800f, PotRunes * 3f) * dt);
            Pot.text = Mathf.RoundToInt(shownPot).ToString("N0");
            switch (step)
            {
                case Step.Ready:
                    // 遊ぶ前は、ときどき小さく傾く。
                    float p = Clock % 1.6f;
                    float rock =
                        p < 0.4f ? Mathf.Sin(p / 0.4f * Mathf.PI * 2f) * (1f - p / 0.4f) : 0f;
                    Die.localRotation = Quaternion.Euler(0f, 0f, rock * 6f);
                    break;
                case Step.Rolling:
                    DrawRoll(t);
                    break;
                case Step.Guess:
                    // 当てるのを待つ間、積んだルーンが小さく息をする。
                    PotPlate.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(t * 5f));
                    break;
                case Step.Won:
                    PotPlate.localScale = Vector3.one;
                    Done = EventMotion.PopRow(Win, t - 0.1f);
                    break;
                case Step.Lost:
                    // 積んだルーンの札が揺れて、赤い0になる。
                    PotPlate.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        Mathf.Sin(t * 60f) * 5f * Mathf.Clamp01(1f - t / 0.4f)
                    );
                    Done = EventMotion.PopRow(Lose, t - 0.2f);
                    break;
            }
        }

        // 跳ねながら回り、目が次々に替わって、着地で少しつぶれる。
        private void DrawRoll(float t)
        {
            float x = Mathf.Clamp01(t / RollSeconds);
            if (x < 1f)
            {
                int flicker = Mathf.FloorToInt(t / FlickerSeconds);
                ShowFace(1 + (flicker * 5 + shownFace) % 6);
            }
            float air = Mathf.Clamp01(x / 0.8f);
            Die.anchoredPosition = new Vector2(0f, Hop * Mathf.Sin(Mathf.PI * air));
            Die.localRotation = Quaternion.Euler(0f, 0f, -720f * EventMotion.Decelerate(air));
            float land = Mathf.Clamp01((x - 0.8f) / 0.2f);
            float squash = Mathf.Sin(Mathf.PI * land) * 0.12f;
            Die.localScale = new Vector3(1f + squash, 1f - squash, 1f);
            if (x >= 1f)
            {
                Die.anchoredPosition = Vector2.zero;
                Die.localRotation = Quaternion.identity;
                Die.localScale = Vector3.one;
                Landed();
            }
        }

        private void ShowFace(int face)
        {
            var lit = Faces[face - 1];
            for (int i = 0; i < Pips.Length && i < lit.Length; i++)
                Pips[i].enabled = lit[i];
        }

        private static void Show(CanvasGroup group, bool shown)
        {
            group.alpha = shown ? 1f : 0f;
            group.interactable = group.blocksRaycasts = shown;
        }
    }
}
