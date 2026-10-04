using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>What the enemy lab makes the chosen enemy do (<see cref="EnemyLab"/>).</summary>
    public enum EnemyLabAction
    {
        /// <summary>Its attack, as on the enemies' turn: a step forward, its skill on one ally at random, a step back.</summary>
        Attack,

        /// <summary>A plain blow on it: the white blink, the red shake, the number and the HP bar.</summary>
        Hit,

        /// <summary>A blow on a weakness: one still hidden glints as it is revealed.</summary>
        WeakHit,

        /// <summary>A blow that beats it: it bursts apart and leaves the turn order.</summary>
        Defeat,

        /// <summary>Frozen, then its turn: it cannot move.</summary>
        Freeze,

        /// <summary>Paralysed, then its attack, a quarter weaker.</summary>
        Paralysis,

        /// <summary>Bleeding, then its attack, after which it bleeds.</summary>
        Bleed,

        /// <summary>Its attack on a party holding up mirrors: the blow is thrown back at it.</summary>
        Reflect,

        /// <summary>The enemies' turn of the battle: every enemy before the party's next turn, in order.</summary>
        EnemyTurn,
    }

    /// <summary>
    /// The enemy lab, a debugging room for the enemies' behaviour: the mock battle
    /// (<see cref="BattleInspectView"/>) with its cards put away, where the chosen enemy does what
    /// a button says at once, through the battle's own code: attacks, takes blows, is beaten, acts
    /// under a status, or the enemies take their turn. Repeated, each run starts from the
    /// battlefield as it began, so every run is alike. The enemy, the action and whether it
    /// repeats are remembered, so the next Play after a change to the enemies' code shows the
    /// same again.
    /// </summary>
    public sealed class EnemyLab : MonoBehaviour
    {
        // What is remembered between Plays (on this machine only).
        public const string EnemyKey = "Baryonyx.EnemyLab.Enemy";
        public const string ActionKey = "Baryonyx.EnemyLab.Action";
        public const string RepeatKey = "Baryonyx.EnemyLab.Repeat";

        // The action last pressed is a gold button with dark words, to read on the gold.
        private static readonly Color PickedColor = new(0.98f, 0.8f, 0.36f, 0.95f);
        private static readonly Color PickedText = new(0.16f, 0.11f, 0.04f);

        [Tooltip(
            "敵を動かす戦闘画面（BattleInspectScreen）。空なら何もしない（展示室のプレビュー）。"
        )]
        public BattleInspectView View;

        [Tooltip("敵の名前（ViewのEnemiesと同じ順）。")]
        public string[] EnemyNames = new string[0];

        [Tooltip("選んでいる敵の名前・攻撃・HP・状態。")]
        public TMP_Text EnemyLabel;

        public Button PreviousButton;
        public Button NextButton;
        public Button RepeatButton;
        public TMP_Text RepeatLabel;
        public Button ResetButton;

        [Tooltip("行動のボタン（EnemyLabActionの順）。")]
        public Button[] ActionButtons = new Button[0];

        [Tooltip("くり返すとき、1回の行動が終わってから次の行動までの間（秒）。")]
        [Min(0f)]
        public float Gap = 0.8f;

        private readonly List<Color> actionColors = new();
        private readonly List<TMP_Text> actionLabels = new();
        private readonly List<Color> actionTextColors = new();
        private int enemy;
        private EnemyLabAction action;
        private bool repeat;
        private Coroutine routine;

        /// <summary>The chosen enemy (an index of the view's Enemies).</summary>
        public int Enemy => enemy;

        /// <summary>The action last pressed, the one a repeat plays.</summary>
        public EnemyLabAction Action => action;

        public bool Repeat => repeat;

        /// <summary>
        /// True while the battle is acting or dealing: a press then is not taken (but, while
        /// repeating, chooses what plays next).
        /// </summary>
        public bool Busy => View != null && (View.Acting || View.EnemyTurn || View.Dealing);

        private void Start()
        {
            if (View == null || View.Enemies.Length == 0)
                return;
            Listen(PreviousButton, () => Select(enemy - 1));
            Listen(NextButton, () => Select(enemy + 1));
            Listen(RepeatButton, () => SetRepeat(!repeat));
            Listen(ResetButton, ResetRoom);
            for (int i = 0; i < ActionButtons.Length; i++)
            {
                var pressed = (EnemyLabAction)i;
                Listen(ActionButtons[i], () => Run(pressed));
                actionColors.Add(
                    ActionButtons[i].targetGraphic != null
                        ? ActionButtons[i].targetGraphic.color
                        : Color.white
                );
                var label = ActionButtons[i].GetComponentInChildren<TMP_Text>(true);
                actionLabels.Add(label);
                actionTextColors.Add(label != null ? label.color : Color.white);
            }
            Load();
            Refresh();
            if (repeat)
                routine = StartCoroutine(Repeating());
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction call)
        {
            if (button != null)
                button.onClick.AddListener(call);
        }

        private void Update()
        {
            if (View == null || View.Enemies.Length == 0)
                return;
            bool idle = !Busy;
            // While repeating, a press only chooses what plays next, so it is always taken.
            foreach (var button in ActionButtons)
                SetInteractable(button, idle || repeat);
            SetInteractable(ResetButton, idle && !repeat);
            // Every frame, as the battle hides the cursors when the turn ends.
            ShowChosen();
        }

        /// <summary>
        /// The cursor over the chosen enemy (bobbing as the battle's cursors do), and its name,
        /// attack, HP and statuses in the label.
        /// </summary>
        private void ShowChosen()
        {
            for (int i = 0; i < View.Enemies.Length; i++)
            {
                var marker = View.Enemies[i].Marker.gameObject;
                bool shown = i == enemy;
                if (marker.activeSelf != shown)
                    marker.SetActive(shown);
            }
            if (EnemyLabel != null)
            {
                var text = Describe();
                if (EnemyLabel.text != text)
                    EnemyLabel.text = text;
            }
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null && button.interactable != value)
                button.interactable = value;
        }

        /// <summary>Chooses the enemy at <paramref name="index"/>, wrapping round.</summary>
        public void Select(int index)
        {
            int count = View.Enemies.Length;
            enemy = ((index % count) + count) % count;
            Save();
            Refresh();
            ShowChosen();
        }

        /// <summary>
        /// The chosen enemy does <paramref name="pressed"/> now. While repeating, it becomes what
        /// plays from the next run on instead; while the battle is busy, nothing happens.
        /// </summary>
        public void Run(EnemyLabAction pressed)
        {
            action = pressed;
            Save();
            Refresh();
            if (repeat || Busy)
                return;
            View.LabPlay(enemy, action);
        }

        /// <summary>Repeats the action over and over, or stops after the run under way.</summary>
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
            if (!Busy)
                View.LabRestore();
        }

        private IEnumerator Repeating()
        {
            while (repeat)
            {
                while (Busy)
                    yield return null;
                if (!repeat)
                    break;
                View.LabRestore();
                View.LabPlay(enemy, action);
                // The action shows as busy at once; one frame for safety all the same.
                yield return null;
                while (Busy)
                    yield return null;
                // The numbers and the dust fade before the next run.
                for (float t = 0f; t < Gap && repeat; t += Time.deltaTime)
                    yield return null;
            }
            routine = null;
        }

        /// <summary>The chosen enemy's name, attack (and its power), HP and statuses.</summary>
        private string Describe()
        {
            var target = View.Enemies[enemy];
            string name = enemy < EnemyNames.Length ? EnemyNames[enemy] : (enemy + 1).ToString();
            var text =
                $"{name}（{target.SkillName}・威力{target.Power}）　HP {target.Hp}/{target.MaxHp}";
            var statuses = View.StatusesNow(false, enemy);
            if (statuses.Count > 0)
                text += "　" + string.Join("・", statuses.ConvertAll(CardRules.StatusName));
            return text;
        }

        private void Refresh()
        {
            if (RepeatLabel != null)
                RepeatLabel.text = repeat ? "くり返し：入" : "くり返し：切";
            for (int i = 0; i < ActionButtons.Length && i < actionColors.Count; i++)
            {
                bool picked = i == (int)action;
                if (ActionButtons[i].targetGraphic != null)
                    ActionButtons[i].targetGraphic.color = picked ? PickedColor : actionColors[i];
                if (actionLabels[i] != null)
                    actionLabels[i].color = picked ? PickedText : actionTextColors[i];
            }
        }

        private void Load()
        {
            enemy = Mathf.Clamp(PlayerPrefs.GetInt(EnemyKey, 0), 0, View.Enemies.Length - 1);
            action = (EnemyLabAction)
                Mathf.Clamp(PlayerPrefs.GetInt(ActionKey, 0), 0, (int)EnemyLabAction.EnemyTurn);
            repeat = PlayerPrefs.GetInt(RepeatKey, 0) != 0;
        }

        private void Save()
        {
            PlayerPrefs.SetInt(EnemyKey, enemy);
            PlayerPrefs.SetInt(ActionKey, (int)action);
            PlayerPrefs.SetInt(RepeatKey, repeat ? 1 : 0);
        }
    }
}
