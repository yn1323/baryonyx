using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.SkillEvolution;
using Baryonyx.UI;
using Baryonyx.UI.Cards;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The card skill lab, a debugging room for the card skills' effects: any card skill
    /// (<see cref="CardSkills"/>) picked from a list is played at once by its user on the
    /// battle's party and enemies, with nothing else of the battle (no hand, no turns, no
    /// numbers). A skill picked while another plays cuts it short. With evolution turned on, a
    /// unique skill's stage that can evolve is played as the battle plays an evolution
    /// (doc/features/skill-evolution.md#戦闘中の覚醒): its card rises and cracks, the stage it
    /// becomes is picked from those it can (<see cref="BattleEvolutionChoice"/>), the card shatters
    /// into it, and the stage picked plays. The skill last picked,
    /// how hard its blows land, the enemy it aims at, whether it evolves and whether it repeats
    /// are remembered, so the next Play after a change to the effect's code shows the same skill
    /// again.
    /// The list shows every skill as its card, on two tabs (the card skills, and the unique
    /// skills' stages a tree to a row), scrolled up and down with a swipe; a third tab plays the
    /// wind-ups the skills open with on their own (<see cref="BattleWindUp"/>). While stopped, it
    /// can be shown closed, or open on any tab (doc/rules/ui-design.md#停止中の表示の切り替え).
    /// </summary>
    public sealed class CardSkillLab : MonoBehaviour, IEditModePreview
    {
        // What is remembered between Plays (on this machine only).
        public const string SkillKey = "Baryonyx.CardSkillLab.Skill";
        public const string WeightKey = "Baryonyx.CardSkillLab.Weight";
        public const string EnemyKey = "Baryonyx.CardSkillLab.Enemy";
        public const string RepeatKey = "Baryonyx.CardSkillLab.Repeat";
        public const string EvolveKey = "Baryonyx.CardSkillLab.Evolve";

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

        [Tooltip(
            "入にすると、覚醒できる固有スキル（最初の段階と1段目）を、覚醒先を選んで覚醒させてから再生する。ほかのスキルには効かない。初めは切。"
        )]
        public Button EvolveButton;
        public TMP_Text EvolveLabel;

        [Tooltip("覚醒先を選ぶ画面。")]
        public BattleEvolutionChoice Choice;

        [Tooltip(
            "覚醒先を選ぶ画面がないときの、前触れのあと割れるまでの間（秒）。1つ目の覚醒先へ進む。"
        )]
        [Min(0f)]
        public float ChoosePause = 0.4f;

        [Header("ホームへ戻る")]
        [Tooltip(
            "ホームへ戻るボタン。一覧を開いているあいだと、戻り先のシーンがないとき（展示室のPrefabなど）は隠す。"
        )]
        public Button HomeButton;

        [Tooltip("戻るときにシーンを覆う遷移の演出。")]
        public SceneTransitionController Transition;

        [Tooltip("戻り先のシーンの名前。空なら戻るボタンを隠す。")]
        public string HomeSceneName = "";

        [Header("スキルの一覧")]
        [Tooltip("スキルの一覧。スキルを押すと、それを再生して閉じる。")]
        public GameObject Picker;

        public Button CloseButton;

        [Tooltip(
            "一覧を開いているあいだ隠す、上の表示と下の操作の列。一覧に重なって読みにくくなるため。"
        )]
        public GameObject[] HiddenWhilePicking = new GameObject[0];

        [Tooltip("一覧のタブ（カードスキル、固有スキル、溜め）。")]
        public GuideTab[] Tabs = new GuideTab[0];

        [Tooltip("タブごとのカードの並び（Tabsと同じ順）。開いているタブの並びだけを出す。")]
        public RectTransform[] TabContents = new RectTransform[0];

        [Tooltip("一覧を上下にスワイプで動かす。中身は開いているタブの並び。")]
        public ScrollRect Scroll;

        [Tooltip(
            "一覧のカードの見本（押せる枠、選んだときの金の光、戦闘と同じカード）。スキルごとに複製する。"
        )]
        public Button CardTemplate;

        [Tooltip("溜めのボタンの見本（押せる枠、選んだときの金の光、名前）。溜めごとに複製する。")]
        public Button WindUpTemplate;

        [Tooltip("カードの挿絵（Skillsと同じ順）。")]
        public Texture[] CardArts = new Texture[0];

        [Tooltip("カードの枠と属性のアイコン（CardElementの順。属性なしのアイコンは空）。")]
        public Texture[] CardFrames = new Texture[0];
        public Texture[] ElementIcons = new Texture[0];

        [Tooltip("くり返すとき、1回の再生が終わってから次の再生までの間（秒）。")]
        [Min(0f)]
        public float Gap = 0.9f;

        private readonly List<Button> skillButtons = new();
        private readonly List<Button> windUpButtons = new();
        private int index;
        private BattleHitWeight weight;
        private int enemy;
        private bool repeat = true;
        private bool evolve;
        private bool choosing;
        private bool playing;
        private readonly HashSet<string> acquired = new();
        private BattleInspectCardView evolutionCard;
        private Coroutine routine;

        /// <summary>
        /// The skills the room plays: the card skills, then every stage of the unique skills'
        /// evolution trees (doc/features/skill-evolution.md).
        /// </summary>
        public static readonly IReadOnlyList<CardSkill> Skills = CardSkills
            .All.Concat(UniqueSkillTrees.Stages().Select(stage => stage.Card))
            .ToArray();

        /// <summary>The wind-ups the room plays on their own, after the skills.</summary>
        public static readonly IReadOnlyList<BattleWindUp> WindUps = (BattleWindUp[])
            System.Enum.GetValues(typeof(BattleWindUp));

        /// <summary>How many things the room plays: the skills, then the wind-ups.</summary>
        public static int Count => Skills.Count + WindUps.Count;

        /// <summary>
        /// The tab a thing played is on: 0 for a card skill, 1 for a unique skill's stage, 2 for a
        /// wind-up.
        /// </summary>
        public static int TabOf(int skill) =>
            skill < CardSkills.All.Count ? 0
            : skill < Skills.Count ? 1
            : 2;

        /// <summary>The thing picked now: a skill's place in <see cref="Skills"/>, then the wind-ups'.</summary>
        public int Index => index;

        /// <summary>The skill picked now, or null while a wind-up is.</summary>
        public CardSkill Current => index < Skills.Count ? Skills[index] : null;

        /// <summary>Whether a wind-up is picked now (<see cref="CurrentWindUp"/>).</summary>
        public bool IsWindUp => index >= Skills.Count;

        public BattleWindUp CurrentWindUp => WindUps[Mathf.Max(0, index - Skills.Count)];

        /// <summary>The buttons of the wind-ups' tab, in <see cref="WindUps"/>' order.</summary>
        public IReadOnlyList<Button> WindUpButtons => windUpButtons;

        /// <summary>A wind-up's name in the list and at the top.</summary>
        public static string WindUpName(BattleWindUp windUp) =>
            windUp switch
            {
                BattleWindUp.BigSpell => "魔法の溜め・大技",
                BattleWindUp.UniqueFirstStep => "固有スキルの溜め・1段目",
                BattleWindUp.UniqueLastStep => "固有スキルの溜め・最終段",
                BattleWindUp.FireSpell => "炎の溜め（層に分けた魔法陣）",
                BattleWindUp.Inhale => "吸い込む溜め",
                BattleWindUp.Aura => "立ち上るオーラ",
                BattleWindUp.BladeGlint => "刃のきらめきと踏み込み",
                BattleWindUp.ColdFront => "氷の前触れ（床を這う冷気）",
                BattleWindUp.StaticCharge => "雷の前触れ（帯電）",
                BattleWindUp.Tremor => "土の前触れ（地鳴り）",
                BattleWindUp.Tiered => "段階で変わる溜め",
                _ => "魔法の溜め",
            };

        // The wind-ups are played by the fire mage, whose spells open with them.
        private static int WindUpUser =>
            (int)CardSkills.All.First(skill => skill.Id == "Fire").User;

        /// <summary>True from a skill's start until its last blow (and what it leaves) has landed.</summary>
        public bool Playing => playing;

        public BattleHitWeight Weight => weight;

        /// <summary>The enemy a card played on one enemy aims at (in <see cref="Enemies"/>).</summary>
        public int Enemy => enemy;

        public bool Repeat => repeat;

        /// <summary>Whether a unique skill's stage that can evolve evolves before it plays.</summary>
        public bool Evolve => evolve;

        /// <summary>True while the stage to evolve into is being picked.</summary>
        public bool Choosing => choosing;

        /// <summary>
        /// The stages a unique skill's stage can evolve into (doc/features/skill-evolution.md):
        /// three from a first stage, one from a branch's first step, none from a last step or a
        /// card skill.
        /// </summary>
        public static IReadOnlyList<CardSkill> EvolvesInto(CardSkill skill)
        {
            var into = new List<CardSkill>();
            if (skill == null || !UniqueSkillEvolution.TryFind(skill.Id, out var tree, out _))
                return into;
            foreach (int stage in UniqueSkillEvolution.NextStages(skill.Id))
                into.Add(tree.StageAt(stage).Card);
            return into;
        }

        public bool PickerOpen => Picker != null && Picker.activeSelf;

        /// <summary>The tab shown in the list.</summary>
        public int Tab { get; private set; }

        /// <summary>One button per skill, in <see cref="Skills"/>'s order.</summary>
        public IReadOnlyList<Button> SkillButtons => skillButtons;

        private bool HasHome => !string.IsNullOrWhiteSpace(HomeSceneName);

        /// <summary>Whether the home button was pressed and the lab is leaving for home.</summary>
        public bool Leaving { get; private set; }

        private void Start()
        {
            Listen(HomeButton, GoHome);
            if (HomeButton != null)
                HomeButton.gameObject.SetActive(!PickerOpen && HasHome);
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
            Listen(EvolveButton, () => SetEvolve(!evolve));
            Listen(CloseButton, ClosePicker);
            for (int t = 0; t < Tabs.Length; t++)
            {
                int tab = t;
                if (Tabs[t] != null)
                    Listen(Tabs[t].Button, () => ShowTab(tab));
            }
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
            int count = Count;
            index = ((skill % count) + count) % count;
            Save();
            Refresh();
            StopAllCoroutines();
            playing = false;
            EndChoosing();
            Vfx.Clear();
            routine = StartCoroutine(Run());
        }

        public void Replay() => Play(index);

        /// <summary>
        /// Goes back to home behind the shutter, ending the skill playing so it does not repeat
        /// while the shutter closes. If home cannot be loaded, the lab stays as it was.
        /// </summary>
        public void GoHome()
        {
            if (Leaving)
                return;
            Leaving = SceneLoader.Load(HomeSceneName, Transition, this, () => Leaving = false);
            if (!Leaving)
                return;
            StopAllCoroutines();
            routine = null;
            playing = false;
            EndChoosing();
            if (Vfx != null)
                Vfx.Clear();
        }

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

        /// <summary>Evolves a unique skill's stage that can evolve before it plays, or not; either way it plays again.</summary>
        public void SetEvolve(bool value)
        {
            evolve = value;
            Replay();
        }

        /// <summary>Opens the list on the tab of the skill playing, scrolled to its card.</summary>
        public void OpenPicker()
        {
            SetPicking(true);
            ShowTab(TabOf(index));
            ScrollTo(index);
        }

        /// <summary>Shows the cards of <paramref name="tab"/>, from the top.</summary>
        public void ShowTab(int tab)
        {
            if (TabContents.Length == 0)
                return;
            Tab = Mathf.Clamp(tab, 0, TabContents.Length - 1);
            for (int i = 0; i < TabContents.Length; i++)
                if (TabContents[i] != null)
                    TabContents[i].gameObject.SetActive(i == Tab);
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i == Tab;
            if (Scroll == null)
                return;
            Scroll.StopMovement();
            Scroll.content = TabContents[Tab];
            Canvas.ForceUpdateCanvases();
            Scroll.verticalNormalizedPosition = 1f;
        }

        // 一覧を開いたとき、選んでいるカードが見える所までスクロールする。
        private void ScrollTo(int skill)
        {
            if (Scroll == null || skill < 0 || skill >= skillButtons.Count || TabOf(skill) != Tab)
                return;
            Canvas.ForceUpdateCanvases();
            var content = Scroll.content;
            float overflow = content.rect.height - Scroll.viewport.rect.height;
            if (overflow <= 0f)
                return;
            var card = (RectTransform)skillButtons[skill].transform;
            float top = -card.anchoredPosition.y - card.rect.height * (1f - card.pivot.y);
            Scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(top / overflow);
        }

        public void ClosePicker() => SetPicking(false);

        private void SetPicking(bool open)
        {
            if (Picker != null)
                Picker.SetActive(open);
            foreach (var hidden in HiddenWhilePicking)
                if (hidden != null)
                    hidden.SetActive(!open);
            // 一覧の見出しと重なるため、開いているあいだは隠す。戻り先がなければ出さない。
            if (HomeButton != null)
                HomeButton.gameObject.SetActive(!open && HasHome);
        }

        private IEnumerator Run()
        {
            do
            {
                playing = true;
                if (IsWindUp)
                {
                    int user = Mathf.Clamp(WindUpUser, 0, Party.Length - 1);
                    yield return Vfx.PlayWindUp(CurrentWindUp, Party[user]);
                    playing = false;
                    for (float t = 0f; t < Gap && repeat; t += Time.deltaTime)
                        yield return null;
                    continue;
                }
                var played = Current;
                var into = evolve ? EvolvesInto(played) : System.Array.Empty<CardSkill>();
                if (into.Count > 0)
                {
                    CardSkill picked = null;
                    yield return Evolution(played, into, stage => picked = stage);
                    if (picked != null)
                        played = picked;
                }
                yield return BattleSkillVfxDemo.PlaySkill(
                    Vfx,
                    Party,
                    Enemies,
                    Actors,
                    played,
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

        /// <summary>
        /// The card played rises as the stage <paramref name="from"/> and cracks, as in battle;
        /// the stage it becomes is picked from <paramref name="into"/> (each acquired once evolved
        /// into in this Play), and it shatters into that stage, which <paramref name="done"/> is
        /// told.
        /// </summary>
        private IEnumerator Evolution(
            CardSkill from,
            IReadOnlyList<CardSkill> into,
            System.Action<CardSkill> done
        )
        {
            var card = EvolutionCard();
            if (card == null)
                yield break;
            ShowCard(card, from);
            int user = Mathf.Clamp((int)from.User, 0, Party.Length - 1);
            // From a branch's first step, it evolves into the branch's last.
            bool deep = BattleSkillVfx.TierOf(from.Id) == 1;
            yield return Vfx.EvolutionOmen((RectTransform)card.transform, Party[user], deep);
            int pick = 0;
            if (Choice != null)
            {
                yield return Vfx.EvolutionPresent();
                pick = -1;
                SetChoosing(true);
                // The lab's controls come back as soon as the stage is confirmed.
                Choice.Open(
                    OptionsOf(into),
                    Vfx.EvolvingScreenPoint(),
                    picked =>
                    {
                        pick = picked;
                        SetChoosing(false);
                    }
                );
                while (pick < 0)
                    yield return null;
            }
            else
                for (float t = 0f; t < ChoosePause; t += Time.deltaTime)
                    yield return null;
            var to = into[Mathf.Clamp(pick, 0, into.Count - 1)];
            acquired.Add(to.Id);
            done(to);
            yield return Vfx.EvolutionBurst(
                BattleSkillVfx.UniqueColor(to.Id),
                deep,
                () => ShowCard(card, to)
            );
        }

        private List<BattleEvolutionChoice.Option> OptionsOf(IReadOnlyList<CardSkill> into)
        {
            var options = new List<BattleEvolutionChoice.Option>();
            foreach (var skill in into)
            {
                int at = IndexOf(skill);
                options.Add(
                    new BattleEvolutionChoice.Option
                    {
                        Name = skill.Name,
                        Owner = NameOf(PartyNames, (int)skill.User),
                        Kind = CardText.KindLine(skill),
                        Description = CardText.Description(skill),
                        Art = At(CardArts, at),
                        Frame = At(CardFrames, (int)skill.Element),
                        Element = At(ElementIcons, (int)skill.Element),
                        Cost = skill.Cost,
                        Quality = skill.Quality,
                        FoilStyle = skill.FoilStyle,
                        Acquired = acquired.Contains(skill.Id),
                    }
                );
            }
            return options;
        }

        // While a stage is picked, the lab's own controls step aside for the stages and the button.
        private void SetChoosing(bool value)
        {
            choosing = value;
            foreach (var hidden in HiddenWhilePicking)
                if (hidden != null)
                    hidden.SetActive(!value);
            if (HomeButton != null)
                HomeButton.gameObject.SetActive(!value && HasHome);
        }

        private void EndChoosing()
        {
            if (Choice != null)
                Choice.Close();
            if (choosing)
                SetChoosing(false);
        }

        // The card that evolves: a copy of the list's card on the effects' layer, made once.
        private BattleInspectCardView EvolutionCard()
        {
            if (evolutionCard != null)
                return evolutionCard;
            var layer = Vfx.EvolutionLayer;
            var face =
                CardTemplate != null
                    ? CardTemplate.GetComponentInChildren<BattleInspectCardView>(true)
                    : null;
            if (layer == null || face == null)
                return null;
            var copy = Instantiate(face.gameObject, layer, false);
            copy.name = "EvolvingCard";
            copy.SetActive(false);
            evolutionCard = copy.GetComponent<BattleInspectCardView>();
            return evolutionCard;
        }

        private void ShowCard(BattleInspectCardView card, CardSkill skill)
        {
            int at = IndexOf(skill);
            card.Show(
                skill.Name,
                NameOf(PartyNames, (int)skill.User),
                CardText.KindLine(skill),
                CardText.Description(skill),
                At(CardArts, at),
                At(CardFrames, (int)skill.Element),
                At(ElementIcons, (int)skill.Element),
                skill.Cost,
                skill.Quality,
                skill.FoilStyle
            );
            card.SetPlayable(true, 0f);
        }

        private static int IndexOf(CardSkill skill)
        {
            var all = Skills;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Id == skill.Id)
                    return i;
            return -1;
        }

        /// <summary>A card for every skill on its tab, numbered as in the catalog.</summary>
        private void BuildList()
        {
            if (CardTemplate == null || TabContents.Length == 0)
                return;
            CardTemplate.gameObject.SetActive(false);
            var all = Skills;
            for (int i = 0; i < all.Count; i++)
            {
                var skill = all[i];
                var content = TabContents[Mathf.Min(TabOf(i), TabContents.Length - 1)];
                var button = Instantiate(CardTemplate, content);
                button.name = skill.Id;
                button.gameObject.SetActive(true);
                var face = button.GetComponentInChildren<BattleInspectCardView>(true);
                if (face != null)
                {
                    face.Show(
                        skill.Name,
                        $"{i + 1}　{NameOf(PartyNames, (int)skill.User)}",
                        CardText.KindLine(skill),
                        CardText.Description(skill),
                        At(CardArts, i),
                        At(CardFrames, (int)skill.Element),
                        At(ElementIcons, (int)skill.Element),
                        skill.Cost,
                        skill.Quality,
                        skill.FoilStyle
                    );
                    face.SetPlayable(true, 0f);
                }
                int picked = i;
                button.onClick.AddListener(() =>
                {
                    ClosePicker();
                    Play(picked);
                });
                skillButtons.Add(button);
            }
            BuildWindUps();
        }

        /// <summary>A button for every wind-up on its own tab, named as at the top.</summary>
        private void BuildWindUps()
        {
            if (WindUpTemplate == null || TabContents.Length < 3)
                return;
            WindUpTemplate.gameObject.SetActive(false);
            for (int i = 0; i < WindUps.Count; i++)
            {
                var button = Instantiate(WindUpTemplate, TabContents[2]);
                button.name = WindUps[i].ToString();
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = WindUpName(WindUps[i]);
                int picked = Skills.Count + i;
                button.onClick.AddListener(() =>
                {
                    ClosePicker();
                    Play(picked);
                });
                windUpButtons.Add(button);
            }
        }

        private static Texture At(Texture[] textures, int at) =>
            textures != null && at >= 0 && at < textures.Length ? textures[at] : null;

        private void Refresh()
        {
            var skill = Current;
            if (skill == null)
            {
                if (Caption != null)
                    Caption.text = $"{index + 1}/{Count}　{WindUpName(CurrentWindUp)}";
                if (Detail != null)
                    Detail.text = $"{NameOf(PartyNames, WindUpUser)}・溜めだけ（当てる相手なし）";
            }
            else
            {
                int user = (int)skill.User;
                if (Caption != null)
                    Caption.text = $"{index + 1}/{Count}　{skill.Name}";
                bool evolves = evolve && EvolvesInto(skill).Count > 0;
                if (Detail != null)
                    Detail.text =
                        $"{NameOf(PartyNames, user)}・コスト{skill.Cost}・"
                        + $"{CardRules.KindName(skill.Kind)}・{CardRules.ScopeName(skill)}"
                        + (evolves ? "　覚醒して使う" : "");
            }
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
            if (EvolveLabel != null)
                EvolveLabel.text = evolve ? "覚醒：入" : "覚醒：切";
            // 選んでいるスキルは、カードの後ろの金の光で示す。
            for (int i = 0; i < skillButtons.Count; i++)
            {
                var selected = skillButtons[i].transform.Find(SelectedName);
                if (selected != null)
                    selected.gameObject.SetActive(i == index);
            }
            for (int i = 0; i < windUpButtons.Count; i++)
            {
                var selected = windUpButtons[i].transform.Find(SelectedName);
                if (selected != null)
                    selected.gameObject.SetActive(Skills.Count + i == index);
            }
        }

        /// <summary>The name of the gold glow behind a card of the list, shown while it is picked.</summary>
        public const string SelectedName = "Selected";

        // 停止中の表示の切り替え：一覧を閉じた画面と、Play中と同じくカードを並べて開いた一覧の各タブ。
        // 停止中は Start が動かず、一覧は開いたまま中身が空なので、並べるところから行う。
        public IReadOnlyList<string> PreviewStates =>
            new[]
            {
                "一覧を閉じた",
                "一覧を開いた（カードスキル）",
                "一覧を開いた（固有スキル）",
                "一覧を開いた（溜め）",
            };

        public void ShowPreviewState(int state)
        {
            if (state == 0)
            {
                ClosePicker();
                return;
            }
            if (skillButtons.Count == 0)
                BuildList();
            // 足したボタンも、保存しない表示の一部にする。
            foreach (var button in skillButtons.Concat(windUpButtons))
            {
                foreach (var part in button.GetComponentsInChildren<Component>(true))
                {
                    part.gameObject.hideFlags |= gameObject.hideFlags;
                    part.hideFlags |= gameObject.hideFlags;
                }
            }
            Refresh();
            SetPicking(true);
            ShowTab(state - 1);
        }

        private static string NameOf(string[] names, int at) =>
            at >= 0 && at < names.Length ? names[at] : (at + 1).ToString();

        private void Load()
        {
            var id = PlayerPrefs.GetString(SkillKey, "");
            index = 0;
            for (int i = 0; i < Count; i++)
                if (IdOf(i) == id)
                    index = i;
            weight = (BattleHitWeight)
                Mathf.Clamp(PlayerPrefs.GetInt(WeightKey, 0), 0, (int)BattleHitWeight.Defeat);
            enemy = Mathf.Clamp(PlayerPrefs.GetInt(EnemyKey, 0), 0, Enemies.Length - 1);
            repeat = PlayerPrefs.GetInt(RepeatKey, 1) != 0;
            evolve = PlayerPrefs.GetInt(EvolveKey, 0) != 0;
        }

        // The name a thing played is remembered by: a skill's id, or a wind-up's.
        private static string IdOf(int at) =>
            at < Skills.Count ? Skills[at].Id : "WindUp." + WindUps[at - Skills.Count];

        private void Save()
        {
            PlayerPrefs.SetString(SkillKey, IdOf(index));
            PlayerPrefs.SetInt(WeightKey, (int)weight);
            PlayerPrefs.SetInt(EnemyKey, enemy);
            PlayerPrefs.SetInt(RepeatKey, repeat ? 1 : 0);
            PlayerPrefs.SetInt(EvolveKey, evolve ? 1 : 0);
        }
    }
}
