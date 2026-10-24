using System;
using System.Collections;
using Baryonyx.Combat.Presentation;
using Baryonyx.Inventory;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>The turning points of the exploration the exploration moment lab plays (<see cref="ExplorationMomentLab"/>).</summary>
    public enum ExplorationMoment
    {
        /// <summary>The monsters of a monster room: "魔物が現れた！".</summary>
        Encounter,

        /// <summary>A strong monster's room: "強い魔物が現れた！", redder and harder.</summary>
        StrongEncounter,

        /// <summary>The deepest room: the map darkens, "最奥の主が現れた！".</summary>
        MasterEncounter,

        /// <summary>A chest of the chosen quality opens.</summary>
        Chest,

        /// <summary>The party walks to the next room and the rooms newly in sight appear.</summary>
        Walk,
    }

    /// <summary>
    /// The exploration moment lab, a debugging room for the turning points of the exploration
    /// (doc/features/screens.md): the exploration screen on its sample route, with no adventure
    /// behind it, where a button plays one moment at once (<see cref="ExplorationMoments"/>).
    /// Each run starts from the sample route's entrance. The chest's quality can be chosen, and
    /// the moment, the quality and whether it repeats are remembered between Plays.
    /// </summary>
    public sealed class ExplorationMomentLab : MonoBehaviour
    {
        public const string MomentKey = "Baryonyx.ExplorationMomentLab.Moment";
        public const string QualityKey = "Baryonyx.ExplorationMomentLab.Quality";
        public const string RepeatKey = "Baryonyx.ExplorationMomentLab.Repeat";

        [Tooltip("演出を重ねる探索画面。空なら何もしない（展示室のプレビュー）。")]
        public ExplorationView View;

        public ExplorationMoments Moments;

        [Tooltip("宝箱の中身の見本（装備の名前・アイコン・光り方）。1回ごとに順に使う。")]
        public RewardItem[] Samples = Array.Empty<RewardItem>();

        [Tooltip("宝箱の中身の下に出す一言。")]
        public string SampleNote = "持ち物に加えました";

        public Button QualityButton;
        public TMP_Text QualityLabel;
        public Button RepeatButton;
        public TMP_Text RepeatLabel;
        public Button ResetButton;

        [Tooltip("戦闘の演出デバッグルームへ移るボタン。")]
        public Button OtherLabButton;

        [Tooltip("移る先のシーンの名前。")]
        public string OtherLabScene = "BattleMomentLab";

        [Tooltip("演出のボタン（ExplorationMomentの順）。")]
        public Button[] MomentButtons = Array.Empty<Button>();

        [Min(0f)]
        public float Gap = 1f;

        private ExplorationMoment moment;
        private int quality = 3;
        private int sample;
        private bool repeat;
        private Coroutine routine;

        public ExplorationMoment Moment => moment;

        /// <summary>The quality of the next chest, or null for nothing found.</summary>
        public ItemQuality? Quality => BattleMomentLab.DropQualities[quality];

        public bool Repeat => repeat;

        public bool Busy => Moments != null && Moments.Busy;

        private void Start()
        {
            if (View == null || Moments == null)
                return;
            Listen(QualityButton, () => SetQuality(quality + 1));
            Listen(RepeatButton, () => SetRepeat(!repeat));
            Listen(ResetButton, ResetRoom);
            Listen(OtherLabButton, () => SceneManager.LoadScene(OtherLabScene));
            for (int i = 0; i < MomentButtons.Length; i++)
            {
                var pressed = (ExplorationMoment)i;
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
            if (View == null || Moments == null)
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

        /// <summary>Plays <paramref name="pressed"/> now, or from the next run while repeating.</summary>
        public void Run(ExplorationMoment pressed)
        {
            moment = pressed;
            Save();
            Refresh();
            if (repeat || Busy)
                return;
            Play(moment);
        }

        public void SetQuality(int index)
        {
            int count = BattleMomentLab.DropQualities.Length;
            quality = ((index % count) + count) % count;
            Save();
            Refresh();
        }

        public void SetRepeat(bool value)
        {
            repeat = value;
            Save();
            Refresh();
            if (repeat && routine == null)
                routine = StartCoroutine(Repeating());
        }

        /// <summary>Back to the sample route's entrance, with no moment left on screen.</summary>
        public void ResetRoom()
        {
            if (Busy)
                return;
            Restore();
        }

        private void Restore()
        {
            Moments.Clear();
            View.HideChest();
            View.RenderSample(View.SampleSeed);
        }

        /// <summary>The next chest's content: a sample in turn, at the chosen quality, or null for nothing found.</summary>
        public RewardItem NextItem()
        {
            var chosen = Quality;
            if (chosen == null || Samples.Length == 0)
                return null;
            var source = Samples[sample % Samples.Length];
            sample++;
            return new RewardItem
            {
                Kind = source.Kind,
                Name = source.Name,
                Icon = source.Icon,
                FoilStyle = source.FoilStyle,
                Quality = chosen.Value,
                Note = SampleNote,
            };
        }

        private void Play(ExplorationMoment which)
        {
            Restore();
            switch (which)
            {
                case ExplorationMoment.Encounter:
                    Moments.PlayEncounter(AdventureRoomKind.Battle);
                    break;
                case ExplorationMoment.StrongEncounter:
                    Moments.PlayEncounter(AdventureRoomKind.Elite);
                    break;
                case ExplorationMoment.MasterEncounter:
                    Moments.PlayEncounter(AdventureRoomKind.Boss);
                    break;
                case ExplorationMoment.Chest:
                    Moments.PlayChest(NextItem());
                    break;
                case ExplorationMoment.Walk:
                    var exits = View.ShownRun?.Exits;
                    if (exits != null && exits.Count > 0)
                        View.WalkSample(exits[0].Id);
                    break;
            }
        }

        private IEnumerator Opening()
        {
            // The exploration screen draws its sample route on its own first frame.
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
            for (int i = 0; i < MomentButtons.Length; i++)
                if (
                    MomentButtons[i] != null
                    && MomentButtons[i].TryGetComponent(out GlassCard card)
                )
                    card.Selected = i == (int)moment;
        }

        /// <summary>The quality button's words: "宝箱：S", or "宝箱：なし" for nothing found.</summary>
        public static string QualityText(ItemQuality? quality) =>
            "宝箱：" + (quality.HasValue ? QualityPalette.Painted(quality.Value) : "なし");

        private void Load()
        {
            moment = (ExplorationMoment)
                Mathf.Clamp(PlayerPrefs.GetInt(MomentKey, 0), 0, (int)ExplorationMoment.Walk);
            quality = Mathf.Clamp(
                PlayerPrefs.GetInt(QualityKey, 3),
                0,
                BattleMomentLab.DropQualities.Length - 1
            );
            repeat = PlayerPrefs.GetInt(RepeatKey, 0) != 0;
        }

        private void Save()
        {
            PlayerPrefs.SetInt(MomentKey, (int)moment);
            PlayerPrefs.SetInt(QualityKey, quality);
            PlayerPrefs.SetInt(RepeatKey, repeat ? 1 : 0);
        }
    }
}
