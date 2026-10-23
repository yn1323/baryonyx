using System.Collections;
using System.Linq;
using Baryonyx.Inventory;
using Baryonyx.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Temple
{
    /// <summary>
    /// The summoning moment lab, a debugging room for the summoning's presentation
    /// (doc/features/screens.md): the summoning-once presentation over the cathedral, with
    /// buttons to choose the result's light (white, gold, rainbow), whether the light is raised
    /// from the one below before the window cracks, to play it again and to repeat it. Each play
    /// draws a sample of that light from the summoning's pool and breaks the window in a new
    /// pattern. The choices are remembered between Plays.
    /// </summary>
    public sealed class TempleSummonLab : MonoBehaviour
    {
        public const string TierKey = "Baryonyx.TempleSummonLab.Tier";
        public const string UpgradeKey = "Baryonyx.TempleSummonLab.Upgrade";
        public const string RepeatKey = "Baryonyx.TempleSummonLab.Repeat";
        public const string TenKey = "Baryonyx.TempleSummonLab.Ten";

        private static readonly string[] TierNames = { "白", "金", "虹" };

        [Tooltip("演出。空なら何もしない（展示室のプレビュー）。")]
        public TempleSummonShow Show;

        [Tooltip("10回の演出。")]
        public TempleSummonTenShow TenShow;

        public Button TierButton;
        public TMP_Text TierLabel;
        public Button UpgradeButton;
        public TMP_Text UpgradeLabel;
        public Button RepeatButton;
        public TMP_Text RepeatLabel;
        public Button PlayButton;
        public Button PlayTenButton;

        [Min(0f)]
        public float Gap = 1f;

        private TempleSummonTier tier = TempleSummonTier.Rainbow;
        private bool upgrade = true;
        private bool repeat;
        private bool ten;
        private int plays;
        private readonly System.Random random = new();

        public TempleSummonTier Tier => tier;
        public bool Upgrade => upgrade;
        public bool Repeat => repeat;

        /// <summary>Whether the presentation played is the summoning ten times.</summary>
        public bool Ten => ten;

        /// <summary>How many times the presentation has started, for tests.</summary>
        public int Plays => plays;

        private void Start()
        {
            if (Show == null)
                return;
            Listen(TierButton, () => SetTier((TempleSummonTier)(((int)tier + 1) % 3)));
            Listen(UpgradeButton, () => SetUpgrade(!upgrade));
            Listen(RepeatButton, () => SetRepeat(!repeat));
            Listen(PlayButton, () => PlayCount(false));
            Listen(PlayTenButton, () => PlayCount(true));
            Show.Finished += Finished;
            if (TenShow != null)
                TenShow.Finished += Finished;
            Load();
            Refresh();
            Play();
        }

        private void OnDestroy()
        {
            if (Show != null)
                Show.Finished -= Finished;
            if (TenShow != null)
                TenShow.Finished -= Finished;
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction call)
        {
            if (button != null)
                button.onClick.AddListener(call);
        }

        public void SetTier(TempleSummonTier value)
        {
            tier = value;
            Save();
            Refresh();
            Play();
        }

        public void SetUpgrade(bool value)
        {
            upgrade = value;
            Save();
            Refresh();
            Play();
        }

        public void SetRepeat(bool value)
        {
            repeat = value;
            Save();
            Refresh();
            if (repeat && !Show.Playing && (TenShow == null || !TenShow.Playing))
                Play();
        }

        private void PlayCount(bool tenTimes)
        {
            ten = tenTimes && TenShow != null;
            Save();
            Play();
        }

        /// <summary>Plays the chosen presentation from the start with new samples of the chosen light.</summary>
        public void Play()
        {
            if (Show == null)
                return;
            StopAllCoroutines();
            bool tenTimes = ten && TenShow != null;
            Show.gameObject.SetActive(!tenTimes);
            if (TenShow != null)
                TenShow.gameObject.SetActive(tenTimes);
            if (tenTimes)
            {
                TenShow.Setup(TenSamples(), random.Next(1, 100000));
                TenShow.Play();
                if (repeat)
                    StartCoroutine(TurnCardsOver());
            }
            else
            {
                var start =
                    upgrade && tier != TempleSummonTier.White
                        ? (TempleSummonTier)((int)tier - 1)
                        : tier;
                Show.Setup(SampleOf(tier), start, random.Next(1, 100000));
                Show.Play();
            }
            plays++;
        }

        // くり返しの間は、伏せた札が並んでから少し待って、自動でめくる。
        private IEnumerator TurnCardsOver()
        {
            yield return new WaitForSecondsRealtime(TenShow.Duration + 0.8f);
            if (TenShow.Playing && !TenShow.Flipped)
                TenShow.Tap();
        }

        // 10回の見本：選んだ光を最上にし、虹なら虹1つと金1つ、金なら金2つ、白なら全部白。
        private SummonResult[] TenSamples()
        {
            var lights = Enumerable.Repeat(TempleSummonTier.White, TempleSummon.TenCount).ToArray();
            int at = random.Next(lights.Length);
            if (tier == TempleSummonTier.Rainbow)
            {
                lights[at] = TempleSummonTier.Rainbow;
                lights[(at + 3) % lights.Length] = TempleSummonTier.Gold;
            }
            else if (tier == TempleSummonTier.Gold)
            {
                lights[at] = TempleSummonTier.Gold;
                lights[(at + 5) % lights.Length] = TempleSummonTier.Gold;
            }
            return lights.Select(SampleOf).ToArray();
        }

        private void Finished()
        {
            if (repeat && isActiveAndEnabled)
                StartCoroutine(PlayAfterGap());
        }

        private IEnumerator PlayAfterGap()
        {
            yield return new WaitForSecondsRealtime(Gap);
            Play();
        }

        // 光ごとの見本：虹はキャラ、金は☆4の装備（品質はランダム）、白は☆3以下のアイテム。
        private SummonResult SampleOf(TempleSummonTier light)
        {
            var pool = TempleSummonResults.PoolOf(Show.Data);
            var entries = light switch
            {
                TempleSummonTier.Rainbow => pool.Where(e => e.Kind == LibraryKind.Character),
                TempleSummonTier.Gold => pool.Where(e =>
                    TempleSummon.HasQuality(e.Kind) && e.Stars == 4
                ),
                _ => pool.Where(e => e.Kind == LibraryKind.Item && e.Stars <= 3),
            };
            var list = entries.ToList();
            if (list.Count == 0)
                list = pool.ToList();
            var chosen = list[random.Next(list.Count)];
            ItemQuality? quality = TempleSummon.HasQuality(chosen.Kind)
                ? (ItemQuality)random.Next(0, 3)
                : null;
            return new SummonResult(chosen, quality);
        }

        private void Refresh()
        {
            Set(TierLabel, "光：" + TierNames[(int)tier]);
            Set(UpgradeLabel, "昇格：" + (upgrade ? "入" : "切"));
            Set(RepeatLabel, "くり返し：" + (repeat ? "入" : "切"));
            if (UpgradeButton != null)
                UpgradeButton.interactable = tier != TempleSummonTier.White;
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text;
        }

        private void Load()
        {
            tier = (TempleSummonTier)Mathf.Clamp(PlayerPrefs.GetInt(TierKey, 2), 0, 2);
            upgrade = PlayerPrefs.GetInt(UpgradeKey, 1) != 0;
            repeat = PlayerPrefs.GetInt(RepeatKey, 0) != 0;
            ten = PlayerPrefs.GetInt(TenKey, 0) != 0 && TenShow != null;
        }

        private void Save()
        {
            PlayerPrefs.SetInt(TierKey, (int)tier);
            PlayerPrefs.SetInt(UpgradeKey, upgrade ? 1 : 0);
            PlayerPrefs.SetInt(RepeatKey, repeat ? 1 : 0);
            PlayerPrefs.SetInt(TenKey, ten ? 1 : 0);
        }
    }
}
