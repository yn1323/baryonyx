using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.Inventory;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>The turning points of a battle the battle moment lab plays (<see cref="BattleMomentLab"/>).</summary>
    public enum BattleMoment
    {
        /// <summary>The enemies step in out of the dark, the "魔物が現れた！" band, then the party's first turn.</summary>
        Encounter,

        /// <summary>The same with the boss coming on last, alone and heavily: "強敵が現れた！".</summary>
        BossEncounter,

        /// <summary>The band of the party's turn.</summary>
        PartyTurn,

        /// <summary>The band of the party acting again with no enemy between.</summary>
        PartyTurnAgain,

        /// <summary>The enemies' turn of the battle, every enemy before the party's next turn.</summary>
        EnemyTurn,

        /// <summary>The warning that the energy cannot pay for a card.</summary>
        NoEnergy,

        /// <summary>The last enemies fall, the "勝利！" band, the party's light and hops.</summary>
        Victory,

        /// <summary>The win after the boss's battle: a hush in the dark, then the bigger light.</summary>
        BossVictory,

        /// <summary>Drops of the chosen quality fall together from the last enemy beaten and open.</summary>
        Drop,

        /// <summary>The win, then the drop, as a battle ends.</summary>
        VictoryAndDrop,

        /// <summary>Every ally falls and the party is wiped out.</summary>
        Wipeout,

        /// <summary>The fallen party stands up again, as on a revive paid with runes.</summary>
        Revive,
    }

    /// <summary>
    /// The battle moment lab, a debugging room for the turning points of a battle: the mock battle
    /// (<see cref="BattleInspectView"/>) with its cards put away, where a button plays one moment
    /// at once through the battle's own code (doc/features/screens/debug-rooms.md). Each run starts from the
    /// battlefield that moment needs (the enemies standing for their entrance and the win, gone
    /// for a drop, the party fallen for a revive). The drop's quality can be chosen, and the
    /// moment, the quality and whether it repeats are remembered, so the next Play after a change
    /// shows the same again.
    /// </summary>
    public sealed class BattleMomentLab : MonoBehaviour
    {
        // What is remembered between Plays (on this machine only).
        public const string MomentKey = "Baryonyx.BattleMomentLab.Moment";
        public const string QualityKey = "Baryonyx.BattleMomentLab.Quality";
        public const string RepeatKey = "Baryonyx.BattleMomentLab.Repeat";
        public const string KindKey = "Baryonyx.BattleMomentLab.Kind";
        public const string CountKey = "Baryonyx.BattleMomentLab.Count";

        /// <summary>The drop's qualities in the order the button steps through, then nothing found.</summary>
        public static readonly ItemQuality?[] DropQualities =
        {
            ItemQuality.C,
            ItemQuality.B,
            ItemQuality.A,
            ItemQuality.S,
            null,
        };

        [Tooltip(
            "演出を再生する戦闘画面（BattleInspectScreen）。空なら何もしない（展示室のプレビュー）。"
        )]
        public BattleInspectView View;

        [Tooltip("ドロップした物を見せる札。")]
        public RewardReveal Reveal;

        [Tooltip("ドロップの見本（武器・防具・アクセサリー・アイテム・スキル・強化素材）。種類ごとに順に使う。")]
        public RewardSampleSet[] SampleSets = Array.Empty<RewardSampleSet>();

        public Button QualityButton;
        public TMP_Text QualityLabel;

        [Tooltip("ドロップの中身の種類を切り替えるボタン（種類のあとに「混ぜる」）。")]
        public Button KindButton;
        public TMP_Text KindLabel;

        [Tooltip("1回のドロップの数を切り替えるボタン。")]
        public Button CountButton;
        public TMP_Text CountLabel;
        public Button RepeatButton;
        public TMP_Text RepeatLabel;
        public Button ResetButton;

        [Tooltip("探索の演出デバッグルームへ移るボタン。")]
        public Button OtherLabButton;

        [Tooltip("移る先のシーンの名前。")]
        public string OtherLabScene = "ExplorationMomentLab";

        [Tooltip("演出のボタン（BattleMomentの順）。")]
        public Button[] MomentButtons = Array.Empty<Button>();

        [Tooltip("くり返すとき、1回の演出が終わってから次までの間（秒）。")]
        [Min(0f)]
        public float Gap = 1f;

        private BattleMoment moment;
        private int quality = 3;
        private int kind;
        private int count = 2;
        private int sample;
        private bool repeat;
        private Coroutine routine;

        public BattleMoment Moment => moment;

        /// <summary>The quality of the next drop, or null for nothing found.</summary>
        public ItemQuality? Quality => DropQualities[quality];

        /// <summary>The kind of the drops (in <see cref="SampleSets"/>; the mix after them).</summary>
        public int Kind => kind;

        /// <summary>How many things a drop holds.</summary>
        public int Count => RewardLabSamples.Counts[count];

        public bool Repeat => repeat;

        /// <summary>True while a moment plays: a press then is not taken (but, while repeating, chooses what plays next).</summary>
        public bool Busy => View != null && (View.Acting || View.EnemyTurn || View.Dealing);

        private void Start()
        {
            if (View == null)
                return;
            Listen(QualityButton, () => SetQuality(quality + 1));
            Listen(KindButton, () => SetKind(kind + 1));
            Listen(CountButton, () => SetCount(count + 1));
            Listen(RepeatButton, () => SetRepeat(!repeat));
            Listen(ResetButton, ResetRoom);
            Listen(OtherLabButton, () => SceneManager.LoadScene(OtherLabScene));
            for (int i = 0; i < MomentButtons.Length; i++)
            {
                var pressed = (BattleMoment)i;
                Listen(MomentButtons[i], () => Run(pressed));
            }
            Load();
            Refresh();
            routine = StartCoroutine(repeat ? Repeating() : Opening());
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction call)
        {
            if (button != null)
                button.onClick.AddListener(call);
        }

        private void Update()
        {
            if (View == null)
                return;
            bool idle = !Busy;
            foreach (var button in MomentButtons)
                SetInteractable(button, idle || repeat);
            SetInteractable(ResetButton, idle && !repeat);
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null && button.interactable != value)
                button.interactable = value;
        }

        /// <summary>
        /// Plays <paramref name="pressed"/> now. While repeating, it becomes what plays from the
        /// next run on instead; while a moment plays, nothing happens.
        /// </summary>
        public void Run(BattleMoment pressed)
        {
            moment = pressed;
            Save();
            Refresh();
            if (repeat || Busy)
                return;
            Play(moment);
        }

        /// <summary>Chooses the drop's quality at <paramref name="index"/> of <see cref="DropQualities"/>, wrapping round.</summary>
        public void SetQuality(int index)
        {
            int count = DropQualities.Length;
            quality = ((index % count) + count) % count;
            Save();
            Refresh();
        }

        /// <summary>Chooses the drops' kind at <paramref name="index"/> (the mix after the kinds), wrapping round.</summary>
        public void SetKind(int index)
        {
            int kinds = RewardLabSamples.KindCount(SampleSets);
            kind = ((index % kinds) + kinds) % kinds;
            sample = 0;
            Save();
            Refresh();
        }

        /// <summary>Chooses how many things a drop holds, at <paramref name="index"/> of <see cref="RewardLabSamples.Counts"/>, wrapping round.</summary>
        public void SetCount(int index)
        {
            int counts = RewardLabSamples.Counts.Length;
            count = ((index % counts) + counts) % counts;
            Save();
            Refresh();
        }

        /// <summary>Repeats the moment over and over, or stops after the run under way.</summary>
        public void SetRepeat(bool value)
        {
            repeat = value;
            Save();
            Refresh();
            if (repeat && routine == null)
                routine = StartCoroutine(Repeating());
        }

        /// <summary>Puts every enemy and ally back as the battle began.</summary>
        public void ResetRoom()
        {
            if (Busy)
                return;
            if (Reveal != null)
                Reveal.HideNow();
            View.MomentRestore();
        }

        /// <summary>
        /// The next drops of a battle: <see cref="Count"/> things of the chosen kind in turn, at
        /// the chosen quality where they have one, best first; none for nothing found.
        /// </summary>
        public List<RewardItem> NextDrops() =>
            RewardLabSamples.Pick(SampleSets, kind, Count, Quality, ref sample);

        /// <summary>Sets the battlefield the moment starts from, then plays it.</summary>
        private void Play(BattleMoment which)
        {
            if (Reveal != null)
                Reveal.HideNow();
            // A drop and a revive start where the battle left them; the others from the start.
            if (which != BattleMoment.Drop && which != BattleMoment.Revive)
                View.MomentRestore();
            switch (which)
            {
                case BattleMoment.Encounter:
                    View.PlayEncounter(boss: false);
                    break;
                case BattleMoment.BossEncounter:
                    View.PlayEncounter(boss: true);
                    break;
                case BattleMoment.PartyTurn:
                    View.PlayPartyTurnBanner(again: false);
                    break;
                case BattleMoment.PartyTurnAgain:
                    View.PlayPartyTurnBanner(again: true);
                    break;
                case BattleMoment.EnemyTurn:
                    View.EndTurn();
                    break;
                case BattleMoment.NoEnergy:
                    View.PlayNoEnergy();
                    break;
                case BattleMoment.Victory:
                    View.PlayVictory(boss: false);
                    break;
                case BattleMoment.BossVictory:
                    View.PlayVictory(boss: true);
                    break;
                case BattleMoment.Drop:
                    View.PlayDrops(NextDrops(), Reveal);
                    break;
                case BattleMoment.VictoryAndDrop:
                    View.PlayVictoryAndDrops(NextDrops(), Reveal);
                    break;
                case BattleMoment.Wipeout:
                    View.PlayWipeout();
                    break;
                case BattleMoment.Revive:
                    View.PlayRevive();
                    break;
            }
        }

        /// <summary>The moment last chosen plays once the battle has opened.</summary>
        private IEnumerator Opening()
        {
            // The battle screen starts its own opening (the deal) first.
            yield return null;
            while (Busy)
                yield return null;
            routine = null;
            if (!repeat)
                Play(moment);
        }

        private IEnumerator Repeating()
        {
            yield return null;
            while (repeat)
            {
                while (Busy)
                    yield return null;
                if (!repeat)
                    break;
                Play(moment);
                yield return null;
                while (Busy)
                    yield return null;
                for (float t = 0f; t < Gap && repeat; t += Time.deltaTime)
                    yield return null;
            }
            routine = null;
        }

        private void Refresh()
        {
            if (RepeatLabel != null)
                RepeatLabel.text = repeat ? "くり返し：入" : "くり返し：切";
            if (QualityLabel != null)
                QualityLabel.text = QualityText(Quality);
            if (KindLabel != null)
                KindLabel.text = "中身：" + RewardLabSamples.KindName(SampleSets, kind);
            if (CountLabel != null)
                CountLabel.text = "数：" + Count;
            // 選んでいる演出は、すりガラスのカードの金の縁と光で示す。
            for (int i = 0; i < MomentButtons.Length; i++)
                if (
                    MomentButtons[i] != null
                    && MomentButtons[i].TryGetComponent(out GlassCard card)
                )
                    card.Selected = i == (int)moment;
        }

        /// <summary>The quality button's words: "ドロップ：S", or "ドロップ：なし" for nothing found.</summary>
        public static string QualityText(ItemQuality? quality) =>
            "ドロップ：" + (quality.HasValue ? QualityPalette.Painted(quality.Value) : "なし");

        private void Load()
        {
            moment = (BattleMoment)
                Mathf.Clamp(PlayerPrefs.GetInt(MomentKey, 0), 0, (int)BattleMoment.Revive);
            quality = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, 3), 0, DropQualities.Length - 1);
            repeat = PlayerPrefs.GetInt(RepeatKey, 0) != 0;
            kind = Mathf.Clamp(PlayerPrefs.GetInt(KindKey, 0), 0, RewardLabSamples.KindCount(SampleSets) - 1);
            count = Mathf.Clamp(PlayerPrefs.GetInt(CountKey, 2), 0, RewardLabSamples.Counts.Length - 1);
        }

        private void Save()
        {
            PlayerPrefs.SetInt(MomentKey, (int)moment);
            PlayerPrefs.SetInt(QualityKey, quality);
            PlayerPrefs.SetInt(RepeatKey, repeat ? 1 : 0);
            PlayerPrefs.SetInt(KindKey, kind);
            PlayerPrefs.SetInt(CountKey, count);
        }
    }
}
