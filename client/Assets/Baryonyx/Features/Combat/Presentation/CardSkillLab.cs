using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The card skill lab, a debugging room for the card skills' effects: any card skill
    /// (<see cref="CardSkills"/>) picked from a list is played at once by its user on the
    /// battle's party and enemies, with nothing else of the battle (no hand, no turns, no
    /// numbers). A skill picked while another plays cuts it short. The skill last picked, how
    /// hard its blows land, the enemy it aims at and whether it repeats are remembered, so the
    /// next Play after a change to the effect's code shows the same skill again.
    /// </summary>
    public sealed class CardSkillLab : MonoBehaviour
    {
        // What is remembered between Plays (on this machine only).
        public const string SkillKey = "Baryonyx.CardSkillLab.Skill";
        public const string WeightKey = "Baryonyx.CardSkillLab.Weight";
        public const string EnemyKey = "Baryonyx.CardSkillLab.Enemy";
        public const string RepeatKey = "Baryonyx.CardSkillLab.Repeat";

        // The skill picked now, marked in the list in a deep amber the white name still reads on.
        private static readonly Color PickedColor = new(0.5f, 0.34f, 0.08f, 0.98f);

        [Header("戦場")]
        public BattleSkillVfx Vfx;

        [Tooltip("味方の体（CardUserの順：アリア、トーマ、ルカ、ミナ）。スキルはここから出る。")]
        public RectTransform[] Party = new RectTransform[0];

        [Tooltip("敵の体（画面の左から）。")]
        public RectTransform[] Enemies = new RectTransform[0];

        [Tooltip("味方の名前（Partyと同じ順）。")]
        public string[] PartyNames = new string[0];

        [Tooltip("敵の名前（Enemiesと同じ順）。")]
        public string[] EnemyNames = new string[0];

        [Tooltip("エフェクトが照らすキャラの絵。先頭の4つは味方で、Partyと同じ順。")]
        public RawImage[] Actors = new RawImage[0];

        [Header("表示と操作")]
        [Tooltip("いま再生しているスキル（「12/50　メテオ」）。")]
        public TMP_Text Caption;

        [Tooltip("使い手・コスト・種類・対象の範囲。")]
        public TMP_Text Detail;

        public Button PreviousButton;
        public Button NextButton;
        public Button ReplayButton;
        public Button ListButton;
        public Button WeightButton;
        public TMP_Text WeightLabel;
        public Button TargetButton;
        public TMP_Text TargetLabel;
        public Button RepeatButton;
        public TMP_Text RepeatLabel;

        [Header("スキルの一覧")]
        [Tooltip("スキルの一覧。スキルを押すと、それを再生して閉じる。")]
        public GameObject Picker;

        public Button CloseButton;

        [Tooltip("使い手ごとの列（CardUserの順）。スキルのボタンを並べる。")]
        public RectTransform[] Columns = new RectTransform[0];

        [Tooltip("スキルのボタンの見本。使い手の列へ、スキルごとに複製する。")]
        public Button SkillTemplate;

        [Tooltip("くり返すとき、1回の再生が終わってから次の再生までの間（秒）。")]
        [Min(0f)]
        public float Gap = 0.9f;

        private readonly List<Button> skillButtons = new();
        private readonly List<Color> skillColors = new();
        private int index;
        private BattleHitWeight weight;
        private int enemy;
        private bool repeat = true;
        private bool playing;
        private Coroutine routine;

        /// <summary>The skill picked now, its place in <see cref="CardSkills.All"/>.</summary>
        public int Index => index;

        public CardSkill Current => CardSkills.All[index];

        /// <summary>True from a skill's start until its last blow (and what it leaves) has landed.</summary>
        public bool Playing => playing;

        public BattleHitWeight Weight => weight;

        /// <summary>The enemy a card played on one enemy aims at (in <see cref="Enemies"/>).</summary>
        public int Enemy => enemy;

        public bool Repeat => repeat;

        public bool PickerOpen => Picker != null && Picker.activeSelf;

        /// <summary>One button per skill, in <see cref="CardSkills.All"/>'s order.</summary>
        public IReadOnlyList<Button> SkillButtons => skillButtons;

        private void Start()
        {
            if (Vfx == null || Party.Length == 0 || Enemies.Length == 0)
                return;
            foreach (var actor in Actors)
                Vfx.RegisterActor(actor);
            BuildList();
            Listen(PreviousButton, Previous);
            Listen(NextButton, Next);
            Listen(ReplayButton, Replay);
            Listen(ListButton, OpenPicker);
            Listen(WeightButton, CycleWeight);
            Listen(TargetButton, CycleTarget);
            Listen(RepeatButton, () => SetRepeat(!repeat));
            Listen(CloseButton, ClosePicker);
            Load();
            ClosePicker();
            Play(index);
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        /// <summary>Plays the skill at <paramref name="skill"/> (wrapping round), cutting short any playing.</summary>
        public void Play(int skill)
        {
            if (Vfx == null)
                return;
            int count = CardSkills.All.Count;
            index = ((skill % count) + count) % count;
            Save();
            Refresh();
            StopAllCoroutines();
            playing = false;
            Vfx.Clear();
            routine = StartCoroutine(Run());
        }

        public void Replay() => Play(index);

        public void Next() => Play(index + 1);

        public void Previous() => Play(index - 1);

        /// <summary>Plays the skill again with its blows landing harder: normal, a weakness, a finishing blow.</summary>
        public void CycleWeight()
        {
            weight = weight switch
            {
                BattleHitWeight.Normal => BattleHitWeight.Weak,
                BattleHitWeight.Weak => BattleHitWeight.Defeat,
                _ => BattleHitWeight.Normal,
            };
            Replay();
        }

        /// <summary>Plays the skill again aimed at the next enemy (for a card played on one enemy).</summary>
        public void CycleTarget()
        {
            enemy = (enemy + 1) % Enemies.Length;
            Replay();
        }

        /// <summary>Repeats the skill over and over, or plays it once; turned on, it plays at once.</summary>
        public void SetRepeat(bool value)
        {
            repeat = value;
            Save();
            Refresh();
            if (repeat && routine == null)
                Replay();
        }

        public void OpenPicker()
        {
            if (Picker != null)
                Picker.SetActive(true);
        }

        public void ClosePicker()
        {
            if (Picker != null)
                Picker.SetActive(false);
        }

        private IEnumerator Run()
        {
            do
            {
                playing = true;
                yield return BattleSkillVfxDemo.PlaySkill(
                    Vfx,
                    Party,
                    Enemies,
                    Actors,
                    Current,
                    enemy,
                    weight
                );
                playing = false;
                // The embers and smoke drift out before it plays again.
                for (float t = 0f; t < Gap && repeat; t += Time.deltaTime)
                    yield return null;
            } while (repeat);
            routine = null;
        }

        /// <summary>A button for every skill, in its user's column, numbered as in the catalog.</summary>
        private void BuildList()
        {
            if (SkillTemplate == null || Columns.Length == 0)
                return;
            SkillTemplate.gameObject.SetActive(false);
            var all = CardSkills.All;
            for (int i = 0; i < all.Count; i++)
            {
                var skill = all[i];
                var column = Columns[Mathf.Clamp((int)skill.User, 0, Columns.Length - 1)];
                var button = Instantiate(SkillTemplate, column);
                button.name = skill.Id;
                button.gameObject.SetActive(true);
                var labels = button.GetComponentsInChildren<TMP_Text>(true);
                if (labels.Length > 0)
                    labels[0].text = $"{i + 1}　{skill.Name}";
                if (labels.Length > 1)
                    labels[1].text = skill.Cost.ToString();
                int picked = i;
                button.onClick.AddListener(() =>
                {
                    ClosePicker();
                    Play(picked);
                });
                skillButtons.Add(button);
                skillColors.Add(
                    button.targetGraphic != null ? button.targetGraphic.color : Color.white
                );
            }
        }

        private void Refresh()
        {
            var skill = Current;
            int user = (int)skill.User;
            if (Caption != null)
                Caption.text = $"{index + 1}/{CardSkills.All.Count}　{skill.Name}";
            if (Detail != null)
                Detail.text =
                    $"{NameOf(PartyNames, user)}・コスト{skill.Cost}・"
                    + $"{CardRules.KindName(skill.Kind)}・{CardRules.ScopeName(skill)}";
            if (WeightLabel != null)
                WeightLabel.text =
                    "当たり："
                    + weight switch
                    {
                        BattleHitWeight.Weak => "弱点",
                        BattleHitWeight.Defeat => "とどめ",
                        _ => "通常",
                    };
            if (TargetLabel != null)
                TargetLabel.text = "ねらう敵：" + NameOf(EnemyNames, enemy);
            if (RepeatLabel != null)
                RepeatLabel.text = repeat ? "くり返し：入" : "くり返し：切";
            for (int i = 0; i < skillButtons.Count; i++)
                if (skillButtons[i].targetGraphic != null)
                    skillButtons[i].targetGraphic.color = i == index ? PickedColor : skillColors[i];
        }

        private static string NameOf(string[] names, int at) =>
            at >= 0 && at < names.Length ? names[at] : (at + 1).ToString();

        private void Load()
        {
            var id = PlayerPrefs.GetString(SkillKey, "");
            index = 0;
            var all = CardSkills.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Id == id)
                    index = i;
            weight = (BattleHitWeight)
                Mathf.Clamp(PlayerPrefs.GetInt(WeightKey, 0), 0, (int)BattleHitWeight.Defeat);
            enemy = Mathf.Clamp(PlayerPrefs.GetInt(EnemyKey, 0), 0, Enemies.Length - 1);
            repeat = PlayerPrefs.GetInt(RepeatKey, 1) != 0;
        }

        private void Save()
        {
            PlayerPrefs.SetString(SkillKey, Current.Id);
            PlayerPrefs.SetInt(WeightKey, (int)weight);
            PlayerPrefs.SetInt(EnemyKey, enemy);
            PlayerPrefs.SetInt(RepeatKey, repeat ? 1 : 0);
        }
    }
}
