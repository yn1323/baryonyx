using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    public enum BattleInspectElement
    {
        None,
        Slash,
        Fire,
        Ice,
        Thunder,
    }

    public enum BattleInspectCardEffect
    {
        /// <summary>Damage to the one enemy the card is dropped on.</summary>
        DamageOne,

        /// <summary>Damage to every enemy.</summary>
        DamageAll,

        /// <summary>Heals the one ally the card is dropped on.</summary>
        Heal,

        /// <summary>Block for the whole party.</summary>
        Guard,
    }

    /// <summary>
    /// Where a card on screen is: unused (put away), in the hand, or played. Played cards are not
    /// kept; the deck is dealt anew when it runs out.
    /// </summary>
    public enum BattleInspectCardPlace
    {
        Deck,
        Hand,
        Discard,
    }

    /// <summary>One card of the deck as data: what a card on screen shows once it is drawn.</summary>
    [Serializable]
    public sealed class BattleInspectCardData
    {
        public string Name;
        public string Owner;

        /// <summary>The kind and the scope, e.g. "攻撃・敵単体" (rich text).</summary>
        public string Kind;

        /// <summary>The effect, with its number in another colour (rich text).</summary>
        public string Description;
        public Texture Art;
        public Texture Frame;

        /// <summary>The element icon, or null for a card without an element.</summary>
        public Texture ElementIcon;
        public int Cost;
        public int Power;
        public BattleInspectElement Element;
        public BattleInspectCardEffect Effect;

        /// <summary>The ally who uses the card (an index of the view's Allies).</summary>
        public int Caster;
    }

    /// <summary>
    /// One card on screen. A drawn card of the deck is put on a card not in the hand, so the same
    /// card of the deck can be in the hand more than once.
    /// </summary>
    [Serializable]
    public sealed class BattleInspectCard
    {
        public RectTransform Body;
        public CanvasGroup Group;
        public BattleInspectCardView Face;

        /// <summary>The back, shown over the face while the card is face down.</summary>
        public GameObject Back;
        public int Cost;
        public int Power;
        public BattleInspectElement Element;
        public BattleInspectCardEffect Effect;

        /// <summary>The ally who uses the card (an index of the view's Allies).</summary>
        public int Caster;

        /// <summary>The card of the deck it shows (its index in the view's Deck).</summary>
        public int DeckIndex;

        [NonSerialized]
        public BattleInspectCardPlace Place;

        public bool InHand => Place == BattleInspectCardPlace.Hand;

        public bool TargetsEnemies =>
            Effect is BattleInspectCardEffect.DamageOne or BattleInspectCardEffect.DamageAll;

        /// <summary>Cards for a whole side need only a swipe, not an exact target.</summary>
        public bool TargetsWholeSide =>
            Effect is BattleInspectCardEffect.DamageAll or BattleInspectCardEffect.Guard;
    }

    [Serializable]
    public sealed class BattleInspectAlly
    {
        /// <summary>The ally's feet: the sprite, its shadow and target area move with it.</summary>
        public RectTransform Body;
        public RawImage Sprite;
        public RectTransform TargetArea;
        public RectTransform HpFill;

        /// <summary>The cursor over the head, shown while a card that can target this ally is up.</summary>
        public RectTransform Marker;
        public int StartHp;
        public int MaxHp;

        [NonSerialized]
        public int Hp;

        /// <summary>The HP bar under the feet; it steps forward with the ally.</summary>
        public RectTransform HpBar => (RectTransform)HpFill.parent;

        public bool Down => Hp <= 0;
    }

    [Serializable]
    public sealed class BattleInspectEnemy
    {
        public RectTransform Body;
        public RectTransform TargetArea;
        public RawImage Sprite;
        public RectTransform HpFill;
        public CanvasGroup Group;

        /// <summary>The cursor over the head, shown while a card that can target this enemy is up.</summary>
        public RectTransform Marker;
        public int MaxHp;

        /// <summary>The name of its attack, shown while it attacks, and its damage to one ally.</summary>
        public string SkillName;
        public int Power;
        public BattleInspectWeakness[] Weaknesses = Array.Empty<BattleInspectWeakness>();

        /// <summary>The face crop of <see cref="Sprite"/> shown in the turn order.</summary>
        public Rect IconUv;

        [NonSerialized]
        public int Hp;

        public bool Alive => Hp > 0;
    }

    /// <summary>
    /// One weakness of an enemy, shown as an icon over its HP bar. A weakness not yet revealed
    /// shows "?" until a card hits it.
    /// </summary>
    [Serializable]
    public sealed class BattleInspectWeakness
    {
        public BattleInspectElement Element;
        public bool StartsRevealed;
        public GameObject Known;
        public GameObject Unknown;

        [NonSerialized]
        public bool Revealed;

        public void Show(bool revealed)
        {
            Revealed = revealed;
            Known.SetActive(revealed);
            Unknown.SetActive(!revealed);
        }
    }

    /// <summary>
    /// One box of the turn order: the whole party (the party crest), or one enemy (its face).
    /// The frame and the inside are tinted by the side that acts.
    /// </summary>
    [Serializable]
    public sealed class BattleInspectTurnSlot
    {
        public LayoutElement Layout;
        public GameObject Party;
        public RawImage Enemy;
        public Graphic Frame;
        public Graphic Fill;
    }

    /// <summary>
    /// Mock of the card battle screen for checking the look and feel. It has no battle rules.
    /// The hand is fanned below the screen with only each card's name and art showing. Pressing
    /// a card springs it up to show its details. It is played in either of two ways: swiping it
    /// up and dropping it on an enemy or an ally (cards for a whole side need only the swipe), or
    /// tapping it, which leaves it up, and then tapping the target. While a card is up, a cursor
    /// bobs over every character it can target; while it is swiped, dots run from the card to
    /// the finger. The characters the card would hit
    /// glow white: all of its side for a whole-side card, the one picked for the others.
    /// The deck is <see cref="Deck"/>, as data; a drawn card is shown on a card of
    /// <see cref="Cards"/> not in the hand. In Play Mode the battle starts with an empty hand,
    /// and the opening hand flies in one by one from the deck, face down, turning face up on the
    /// way, filling the fan from the right; outside Play Mode the prefab shows the opening hand.
    /// "End turn" refills the energy and draws the turn's cards into the left end of the hand,
    /// stopping at the hand limit (the rest stay in the deck). Played cards are gone; when a draw
    /// finds the deck empty, the whole deck is shuffled in anew, so a card in hand may come again.
    /// A card the energy cannot pay for is darkened with a reddish cost. A pressed card grows and
    /// stands in the middle of the bottom of the screen. Hitting a hidden weakness reveals it.
    /// A played card is paid for and leaves the hand at once, and its action is queued: the user
    /// steps one step forward, its name shows at the top middle of the screen, the effect lands,
    /// and the user steps back. "End turn" hands the turn to the enemies before the party's next
    /// turn in the order: each attacks one ally at random the same way.
    /// </summary>
    public sealed class BattleInspectView : MonoBehaviour
    {
        // One dot of the 4x pixel art; idle motion moves in whole dots.
        private const float Dot = 4f;
        private const float WeakMultiplier = 1.5f;

        // Numbers start just under the top of the drawn body, above the character, not at its middle.
        private const float NumberBelowTop = 10f;

        // How far a rising popup travels: the numbers only lift a little, messages further.
        private const float NumberRise = 36f;
        private const float MessageRise = 90f;
        private const int MaxEnergyCap = 9;

        // How long a notice such as "エネルギー不足" stays in place of the skill name.
        private const float NoticeHold = 0.9f;

        private static readonly Color GuardColor = new(0.55f, 0.75f, 1f);
        private static readonly Color HitTint = new(1f, 0.45f, 0.4f);
        private static readonly Color AimIdle = new(0.6f, 0.6f, 0.6f);
        private static readonly Color DownTint = new(0.35f, 0.35f, 0.4f);

        // Room between the deck's counter and a card coming out of it.
        private const float DeckGap = 8f;

        // Room between a dot of the aim and the next.
        private const float AimSpacing = 44f;

        /// <summary>The cards of the deck, as data. It is dealt anew, shuffled, when it runs out.</summary>
        public BattleInspectCardData[] Deck = Array.Empty<BattleInspectCardData>();

        /// <summary>The cards on screen; at least as many as the hand can hold.</summary>
        public BattleInspectCard[] Cards = Array.Empty<BattleInspectCard>();
        public BattleInspectAlly[] Allies = Array.Empty<BattleInspectAlly>();
        public BattleInspectEnemy[] Enemies = Array.Empty<BattleInspectEnemy>();

        /// <summary>Containers of the party and enemy sprites that sway while idle.</summary>
        public RectTransform[] IdleActors = Array.Empty<RectTransform>();

        /// <summary>One dot of each idle actor, so each bobs by exactly one of its own dots.</summary>
        public float[] IdleSteps = Array.Empty<float>();

        /// <summary>
        /// The repeating turn order. <see cref="PartyTurn"/> is a party turn (the party acts as
        /// one side), other values are enemy indexes. A party entry twice in a row is the
        /// consecutive turn that a large speed gap gives.
        /// </summary>
        public int[] TurnCycle = Array.Empty<int>();

        /// <summary>The boxes of the turn order, the current turn first (left end).</summary>
        public BattleInspectTurnSlot[] TurnSlots = Array.Empty<BattleInspectTurnSlot>();
        public float PartySlotWidth = 104f;
        public float EnemySlotWidth = 104f;

        // The current turn has a gold frame; the others are blue for the party and red for an enemy.
        public Color CurrentTurnFrame = new(0.98f, 0.8f, 0.36f);
        public Color PartyTurnFrame = new(0.36f, 0.56f, 0.84f);
        public Color EnemyTurnFrame = new(0.78f, 0.35f, 0.31f);
        public Color PartyTurnFill = new(0.07f, 0.125f, 0.24f, 0.92f);
        public Color EnemyTurnFill = new(0.21f, 0.063f, 0.07f, 0.92f);

        /// <summary>The parent of the cards; card poses and drags are measured in its space.</summary>
        public RectTransform Hand;

        /// <summary>
        /// Sizes, the fan of the hand and the feel of the drag. Read every frame, so it can be
        /// tuned in the Inspector during Play Mode. Played cards leave the fan and the rest close
        /// up around the middle.
        /// </summary>
        public BattleInspectHandSettings Settings;

        public RectTransform PartyAnchor;
        public TMP_Text PopupTemplate;

        /// <summary>
        /// Templates of the rising numbers, styled in the prefab (font, size, gradient and
        /// outline): damage, damage on a weakness, and healing.
        /// </summary>
        public TMP_Text DamageNumber;
        public TMP_Text WeakNumber;
        public TMP_Text HealNumber;
        public TMP_Text EnergyLabel;

        /// <summary>
        /// The deck's counter (its picture and the number of cards left); dealt cards come out
        /// of its right edge.
        /// </summary>
        public RectTransform DeckAnchor;

        /// <summary>The number of cards left in the deck.</summary>
        public TMP_Text DeckLabel;
        public Button EndTurnButton;

        /// <summary>Material that turns a sprite white; the view pulses a copy of it.</summary>
        public Material TargetFlash;

        /// <summary>
        /// Full-screen catcher under the hand, active while a tapped card waits for its target:
        /// a tap on a target plays the card there, a tap anywhere else puts it back.
        /// </summary>
        public GameObject TapArea;

        /// <summary>Holds the dots that run from a swiped card to the finger.</summary>
        public RectTransform Aim;

        public RawImage[] AimDots = Array.Empty<RawImage>();

        /// <summary>
        /// The name of the skill being used, at the top middle of the screen on Top's dark
        /// translucent band; <see cref="SkillBannerGroup"/> fades it.
        /// </summary>
        public TranslucentTextPanel SkillBanner;

        public CanvasGroup SkillBannerGroup;

        [Min(1)]
        public int StartTurn = 3;

        private readonly List<int> targets = new();
        private readonly List<int> hand = new();
        private readonly List<int> drawPile = new();
        private readonly Queue<IEnumerator> actions = new();
        private Coroutine dealing;
        private Coroutine bannerFade;
        private Color skillNameColor = Color.white;

        // The skill being used, shown again when a notice over it ends; null between actions.
        private string skillShown;
        private bool noticeShown;
        private bool acting;

        // Places kept free at the left of the fan for the cards still to be dealt.
        private int reserved;
        private Vector2[] idleBase = Array.Empty<Vector2>();
        private Vector2[] allyMarkerBase = Array.Empty<Vector2>();
        private Vector2[] enemyMarkerBase = Array.Empty<Vector2>();
        private Vector2[] enemyBase = Array.Empty<Vector2>();
        private Vector2[] allyBase = Array.Empty<Vector2>();
        private Vector2[] allyBarBase = Array.Empty<Vector2>();
        private CardMotion[] motion = Array.Empty<CardMotion>();
        private Material flash;
        private int held = -1;
        private bool pressing;
        private bool armed;
        private bool moved;
        private bool reselected;
        private Vector2 pressPoint;
        private Vector2 fingerPoint;
        private int cycleIndex;

        public const int PartyTurn = -1;

        public int Turn { get; private set; }
        public int Energy { get; private set; }
        public int MaxEnergy { get; private set; }

        /// <summary>The card that is up (pressed, or tapped and waiting for a target), or -1.</summary>
        public int HeldCard => held;

        /// <summary>True while a tapped card stays up waiting for a tap on its target.</summary>
        public bool Selected => held >= 0 && !pressing;

        /// <summary>True while the held card is swiped up far enough to be played on release.</summary>
        public bool Armed => armed;

        /// <summary>Indexes of the enemies or allies (see <see cref="TargetsEnemies"/>) that glow.</summary>
        public IReadOnlyList<int> Targets => targets;

        public bool TargetsEnemies => held >= 0 && Cards[held].TargetsEnemies;

        /// <summary>Cards in hand (indexes of <see cref="Cards"/>), left to right.</summary>
        public IReadOnlyList<int> HandCards => hand;

        /// <summary>
        /// Cards left in the deck (indexes of <see cref="Deck"/>); the next draw takes the first.
        /// </summary>
        public IReadOnlyList<int> DrawPile => drawPile;

        /// <summary>True while cards are being dealt; cards cannot be pressed nor the turn ended.</summary>
        public bool Dealing => dealing != null;

        /// <summary>True while played cards or the enemies are acting.</summary>
        public bool Acting => acting;

        /// <summary>
        /// True from "End turn" until the party's next turn begins: the enemies act, and cards
        /// cannot be pressed.
        /// </summary>
        public bool EnemyTurn { get; private set; }

        /// <summary>True while the card flies from the deck to the hand.</summary>
        public bool IsFlying(int index) => motion[index].Flying;

        /// <summary>True while the card shows its back.</summary>
        public bool IsFaceDown(int index) => motion[index].Reveal < 0.5f;

        /// <summary>
        /// The angle between neighbours in a fan of <paramref name="count"/> cards: the settings'
        /// step, narrowed so the two ends are at most <paramref name="maxSpread"/> degrees apart.
        /// </summary>
        public static float FanStepFor(int count, float step, float maxSpread) =>
            count > 1 ? Mathf.Min(step, maxSpread / (count - 1)) : step;

        /// <summary>
        /// Position (of the bottom centre) and tilt in degrees of the card at <paramref name="slot"/>
        /// in a fan of <paramref name="count"/> cards.
        /// </summary>
        public static (Vector2 position, float angle) FanPose(
            int slot,
            int count,
            float step,
            float radius,
            float drop,
            float bottom
        )
        {
            float angle = (slot - (count - 1) * 0.5f) * step * Mathf.Deg2Rad;
            var position = new Vector2(
                Mathf.Sin(angle) * radius,
                bottom + (Mathf.Cos(angle) - 1f) * radius * drop
            );
            return (position, -angle * Mathf.Rad2Deg);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Lays the whole hand out as the settings say, outside Play Mode, to check a change to
        /// the settings in the Scene or Game view.
        /// </summary>
        [ContextMenu("手札の並びを設定に合わせる")]
        private void ApplyHandLayoutInEditor()
        {
            if (Settings == null)
                return;
            // The opening hand: the first cards of the deck.
            int count = Mathf.Min(Settings.OpeningDraw, Settings.HandLimit, Cards.Length);
            for (int i = 0; i < count; i++)
            {
                var body = Cards[i].Body;
                UnityEditor.Undo.RecordObject(body, "Apply hand layout");
                var (position, angle) = FanPose(
                    i,
                    count,
                    FanStepFor(count, Settings.FanStep, Settings.FanMaxSpread),
                    Settings.FanRadius,
                    Settings.FanDrop,
                    Settings.RestBottom
                );
                body.anchoredPosition = position;
                body.localRotation = Quaternion.Euler(0, 0, angle);
                body.localScale = Vector3.one * Settings.CardScale;
            }
        }
#endif

        /// <summary>Where the card rests in the fan now (it springs there when not held).</summary>
        public Vector2 RestPosition(int index) => motion[index].Rest.Position;

        /// <summary>Energy starts at 3 and its maximum grows by one each turn, up to 9.</summary>
        public static int MaxEnergyAt(int turn) => Mathf.Min(MaxEnergyCap, 3 + turn - 1);

        public static string EnergyText(int energy, int max) => $"{energy}/{max}";

        private void Awake()
        {
            idleBase = new Vector2[IdleActors.Length];
            for (int i = 0; i < IdleActors.Length; i++)
                idleBase[i] = IdleActors[i].anchoredPosition;

            motion = new CardMotion[Cards.Length];
            for (int i = 0; i < Cards.Length; i++)
            {
                var body = Cards[i].Body;
                var rest = new Pose(body.anchoredPosition, body.localEulerAngles.z, 1f);
                if (rest.Angle > 180f)
                    rest.Angle -= 360f;
                motion[i] = new CardMotion
                {
                    Rest = rest,
                    Current = rest,
                    Target = rest,
                    Reveal = 1f,
                };
                if (Cards[i].Face == null)
                    Cards[i].Face = body.GetComponent<BattleInspectCardView>();
            }

            allyMarkerBase = new Vector2[Allies.Length];
            allyBase = new Vector2[Allies.Length];
            allyBarBase = new Vector2[Allies.Length];
            for (int i = 0; i < Allies.Length; i++)
            {
                allyMarkerBase[i] = Allies[i].Marker.anchoredPosition;
                allyBase[i] = Allies[i].Body.anchoredPosition;
                allyBarBase[i] = Allies[i].HpBar.anchoredPosition;
            }
            enemyMarkerBase = new Vector2[Enemies.Length];
            enemyBase = new Vector2[Enemies.Length];
            for (int i = 0; i < Enemies.Length; i++)
            {
                enemyMarkerBase[i] = Enemies[i].Marker.anchoredPosition;
                enemyBase[i] = Enemies[i].Body.anchoredPosition;
                Enemies[i].Hp = Enemies[i].MaxHp;
                foreach (var weakness in Enemies[i].Weaknesses)
                    weakness.Show(weakness.StartsRevealed);
            }
            foreach (var ally in Allies)
            {
                ally.Hp = ally.StartHp;
                RefreshAlly(ally);
            }

            if (Settings == null)
                Settings = ScriptableObject.CreateInstance<BattleInspectHandSettings>();
            if (TargetFlash != null)
                flash = new Material(TargetFlash);
            if (EndTurnButton != null)
                EndTurnButton.onClick.AddListener(EndTurn);
            foreach (var template in new[] { PopupTemplate, DamageNumber, WeakNumber, HealNumber })
            {
                if (template != null)
                    template.gameObject.SetActive(false);
            }
            if (SkillBanner != null)
            {
                if (SkillBanner.Label != null)
                    skillNameColor = SkillBanner.Label.color;
                SkillBanner.gameObject.SetActive(false);
            }

            StartOfTurn(StartTurn);
            // The hand baked into the prefab is only for the look outside Play Mode.
            ClearTable();
        }

        private void Start() => RestartDeal();

        private void OnDestroy()
        {
            if (flash != null)
                Destroy(flash);
        }

        private void Update()
        {
            // A two-step idle bob of one dot, like the breathing of 16-bit battle sprites.
            float time = Time.time;
            for (int i = 0; i < IdleActors.Length; i++)
            {
                bool up = Mathf.Repeat(time * 0.9f + i * 0.37f, 1f) < 0.5f;
                float step = i < IdleSteps.Length ? IdleSteps[i] : Dot;
                IdleActors[i].anchoredPosition =
                    idleBase[i] + (up ? Vector2.up * step : Vector2.zero);
            }

            // Laid out every frame so changes to the settings show at once.
            LayoutHand();
            if (held >= 0)
                motion[held].Target = RaisedPose(held);
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            for (int i = 0; i < Cards.Length; i++)
            {
                motion[i].Step(dt, Settings.Stiffness, Settings.Damping);
                ApplyMotion(i);
            }

            if (flash != null)
                flash.SetFloat("_FlashAmount", 0.5f + 0.2f * Mathf.Sin(time * 9f));

            // The cursors bob by one dot, a second up and a second down.
            var bob = Mathf.Repeat(time * 0.5f, 1f) < 0.5f ? Vector2.up * Dot : Vector2.zero;
            for (int i = 0; i < Allies.Length; i++)
                Allies[i].Marker.anchoredPosition = allyMarkerBase[i] + bob;
            for (int i = 0; i < Enemies.Length; i++)
                Enemies[i].Marker.anchoredPosition = enemyMarkerBase[i] + bob;

            if (TapArea != null)
                TapArea.SetActive(Selected);
            RefreshAim();
        }

        /// <summary>Puts the card where its motion is now, face up or down, and shows or hides it.</summary>
        private void ApplyMotion(int index)
        {
            var pose = motion[index].Current;
            var body = Cards[index].Body;
            body.anchoredPosition = pose.Position;
            body.localRotation = Quaternion.Euler(0, 0, pose.Angle);
            // Turning over: the card narrows to an edge on its back and widens on its face.
            float turn = Mathf.Max(0.04f, Mathf.Abs(Mathf.Cos(Mathf.PI * motion[index].Reveal)));
            body.localScale = new Vector3(turn, 1f, 1f) * (pose.Scale * Settings.CardScale);
            var back = Cards[index].Back;
            if (back != null && back.activeSelf != IsFaceDown(index))
                back.SetActive(IsFaceDown(index));
            RefreshCard(index);
        }

        // --- Card input -----------------------------------------------------------------

        /// <summary>
        /// Presses a card at a screen point: it springs up and grows by the settings' RaisedScale
        /// to show its details. Pressing another card while one waits for a target swaps them.
        /// </summary>
        public void PressCard(int index, Vector2 screenPoint)
        {
            if (pressing || Dealing || EnemyTurn || !Cards[index].InHand)
                return;
            if (held >= 0 && held != index)
                PutBack();
            reselected = held == index;
            held = index;
            pressing = true;
            armed = false;
            moved = false;
            pressPoint = ToHand(screenPoint);
            fingerPoint = screenPoint;
            var card = Cards[index];
            card.Body.SetAsLastSibling();
            motion[index].Target = RaisedPose(index);
            // Cards for a whole side show their targets from the press, before any swipe.
            FindTargets(card, screenPoint);
        }

        /// <summary>Follows the finger of the pressed card and picks the characters it would hit.</summary>
        public void DragCard(Vector2 screenPoint)
        {
            if (!pressing)
                return;
            var point = ToHand(screenPoint);
            if ((point - pressPoint).magnitude > Settings.TapSlop)
                moved = true;
            armed = point.y - pressPoint.y >= Settings.ArmDistance;
            fingerPoint = screenPoint;
            FindTargets(Cards[held], screenPoint);
        }

        /// <summary>
        /// Lets go: plays the card on its targets when swiped up to them. A tap (let go where it
        /// was pressed) leaves the card up to pick the target by tapping; a second tap on the card
        /// plays it without one (see <see cref="PlayWithoutTarget"/>). A swipe that ends on no
        /// target puts it back.
        /// </summary>
        public void ReleaseCard(Vector2 screenPoint)
        {
            if (!pressing)
                return;
            DragCard(screenPoint);
            pressing = false;
            if (armed && targets.Count > 0)
                PlayHeld();
            else if (moved)
                PutBack();
            else if (reselected)
                PlayWithoutTarget();
            else
                FindTargets(Cards[held], screenPoint);
        }

        /// <summary>
        /// Plays the held card with no target picked: a card for a whole side on all of it, and a
        /// card for one character on one of its side at random.
        /// </summary>
        private void PlayWithoutTarget()
        {
            var card = Cards[held];
            AddEveryTarget(card);
            if (!card.TargetsWholeSide && targets.Count > 0)
            {
                int pick = targets[UnityEngine.Random.Range(0, targets.Count)];
                targets.Clear();
                targets.Add(pick);
            }
            PlayHeld();
        }

        /// <summary>
        /// A tap on the battlefield while a tapped card waits: plays it on the character tapped
        /// (any of its side for a whole-side card), or puts it back when no target was tapped.
        /// </summary>
        public void TapScreen(Vector2 screenPoint)
        {
            if (!Selected)
                return;
            var card = Cards[held];
            int hit = NearestTarget(card, screenPoint);
            if (hit < 0)
            {
                PutBack();
                return;
            }
            FindTargets(card, screenPoint);
            if (!card.TargetsWholeSide)
            {
                targets.Clear();
                targets.Add(hit);
            }
            PlayHeld();
        }

        private void PlayHeld()
        {
            int index = held;
            var card = Cards[index];
            bool play = targets.Count > 0;
            if (play && card.Cost > Energy)
            {
                ShowNotice("エネルギー不足");
                play = false;
            }
            var chosen = new List<int>(targets);
            bool onEnemies = card.TargetsEnemies;
            PutBack();
            if (play)
                Play(index, chosen, onEnemies);
        }

        /// <summary>Returns the card that is up to its place in the hand.</summary>
        private void PutBack()
        {
            int index = held;
            held = -1;
            pressing = false;
            armed = false;
            ClearTargets();
            if (index < 0)
                return;
            SortCards();
            motion[index].Target = motion[index].Rest;
        }

        /// <summary>Draws the hand's cards left to right, so each overlaps the one on its left.</summary>
        private void SortCards()
        {
            foreach (int index in hand)
                Cards[index].Body.SetAsLastSibling();
        }

        /// <summary>
        /// Lays the cards still in hand out as a fan centred on the screen, so a played card
        /// leaves no gap: the others spring together toward the middle. While cards are dealt,
        /// the fan already has room at its left end for those still to come.
        /// </summary>
        private void LayoutHand()
        {
            int count = reserved + hand.Count;
            float step = FanStepFor(count, Settings.FanStep, Settings.FanMaxSpread);
            for (int slot = 0; slot < hand.Count; slot++)
            {
                int i = hand[slot];
                var (position, angle) = FanPose(
                    reserved + slot,
                    count,
                    step,
                    Settings.FanRadius,
                    Settings.FanDrop,
                    Settings.RestBottom
                );
                motion[i].Rest = new Pose(position, angle, 1f);
                if (i != held)
                    motion[i].Target = motion[i].Rest;
            }
        }

        private Pose RaisedPose(int index)
        {
            // In the middle of the screen from left to right, standing on its bottom edge with a
            // little room. The hand's origin is the bottom centre of the screen area and the card
            // turns about its bottom centre.
            return new Pose(
                new Vector2(0f, Settings.RaisedBottom) - Hand.anchoredPosition,
                0f,
                Settings.RaisedScale
            );
        }

        private Vector2 ToHand(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Hand,
                screenPoint,
                EventCamera(),
                out var local
            );
            return local;
        }

        private Camera EventCamera()
        {
            var canvas = Hand.GetComponentInParent<Canvas>();
            if (canvas == null)
                return null;
            canvas = canvas.rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private void FindTargets(BattleInspectCard card, Vector2 screenPoint)
        {
            targets.Clear();
            if (card.TargetsWholeSide)
                AddEveryTarget(card);
            else if (armed)
            {
                int nearest = NearestTarget(card, screenPoint);
                if (nearest >= 0)
                    targets.Add(nearest);
            }
            ApplyGlow(card.TargetsEnemies);
        }

        /// <summary>Adds every character of the card's side it can be used on to the targets.</summary>
        private void AddEveryTarget(BattleInspectCard card)
        {
            int count = card.TargetsEnemies ? Enemies.Length : Allies.Length;
            for (int i = 0; i < count; i++)
                if (CanTarget(card, i))
                    targets.Add(i);
        }

        private bool CanTarget(BattleInspectCard card, int index) =>
            !card.TargetsEnemies || Enemies[index].Alive;

        /// <summary>The character of the card's side under or near a screen point, or -1.</summary>
        private int NearestTarget(BattleInspectCard card, Vector2 screenPoint)
        {
            var camera = EventCamera();
            int count = card.TargetsEnemies ? Enemies.Length : Allies.Length;
            int nearest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!CanTarget(card, i))
                    continue;
                var area = card.TargetsEnemies ? Enemies[i].TargetArea : Allies[i].TargetArea;
                float distance = DropDistance(area, screenPoint, camera);
                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }
            return nearest;
        }

        /// <summary>
        /// How far a drop is from a character's drawn body in its local units, or
        /// <see cref="float.MaxValue"/> when it is outside the body plus the settings' margin.
        /// </summary>
        private float DropDistance(RectTransform area, Vector2 screenPoint, Camera camera)
        {
            if (
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    area,
                    screenPoint,
                    camera,
                    out var local
                )
            )
                return float.MaxValue;
            var rect = area.rect;
            float dx = Mathf.Max(rect.xMin - local.x, 0f, local.x - rect.xMax);
            float dy = Mathf.Max(rect.yMin - local.y, 0f, local.y - rect.yMax);
            if (dx > Settings.TargetMargin || dy > Settings.TargetMargin)
                return float.MaxValue;
            // Inside the body counts as nearer than any margin, then by distance to the centre.
            return Mathf.Max(dx, dy) * 1000f + (local - rect.center).magnitude;
        }

        private void ClearTargets()
        {
            targets.Clear();
            ApplyGlow(true);
        }

        private void ApplyGlow(bool onEnemies)
        {
            var card = held >= 0 ? Cards[held] : null;
            for (int i = 0; i < Enemies.Length; i++)
            {
                Enemies[i].Sprite.material = onEnemies && targets.Contains(i) ? flash : null;
                Enemies[i]
                    .Marker.gameObject.SetActive(
                        card != null && card.TargetsEnemies && CanTarget(card, i)
                    );
            }
            for (int i = 0; i < Allies.Length; i++)
            {
                Allies[i].Sprite.material = !onEnemies && targets.Contains(i) ? flash : null;
                Allies[i].Marker.gameObject.SetActive(card != null && !card.TargetsEnemies);
            }
        }

        /// <summary>
        /// Runs the dots from the centre of a card swiped at one target to the finger, bending like
        /// Slay the Spire's aim. They are drawn behind the cards, so they seem to come out from
        /// under the card. They light up when the finger is on a target.
        /// </summary>
        private void RefreshAim()
        {
            if (Aim == null)
                return;
            bool show = pressing && armed && !Cards[held].TargetsWholeSide;
            Aim.gameObject.SetActive(show);
            if (!show)
                return;

            var body = Cards[held].Body;
            Vector2 start = Aim.InverseTransformPoint(body.TransformPoint(body.rect.center));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Aim,
                fingerPoint,
                EventCamera(),
                out var end
            );
            var bend = new Vector2(start.x, end.y);
            float length = Vector2.Distance(start, bend) + Vector2.Distance(bend, end);
            int count = Mathf.Clamp(Mathf.RoundToInt(length / AimSpacing), 1, AimDots.Length);
            var tint = targets.Count > 0 ? Color.white : AimIdle;
            for (int i = 0; i < AimDots.Length; i++)
            {
                var dot = AimDots[i];
                dot.gameObject.SetActive(i < count);
                if (i >= count)
                    continue;
                float t = (i + 1f) / count;
                float u = 1f - t;
                var point = u * u * start + 2f * u * t * bend + t * t * end;
                // On whole dots of the pixel art, so the dots never blur.
                dot.rectTransform.anchoredPosition = new Vector2(
                    Mathf.Round(point.x / Dot) * Dot,
                    Mathf.Round(point.y / Dot) * Dot
                );
                dot.color = tint;
            }
        }

        /// <summary>True when the sprite is glowing as a target of the held card.</summary>
        public bool IsGlowing(RawImage sprite) => flash != null && sprite.material == flash;

        // --- Turn --------------------------------------------------------------------------

        /// <summary>
        /// Ends the party's turn: once the played cards have acted, the enemies up to the party's
        /// next turn in the order attack, then the next turn begins and its cards are drawn.
        /// </summary>
        public void EndTurn()
        {
            if (Dealing || EnemyTurn)
                return;
            PutBack();
            EnemyTurn = true;
            Refresh();
            Enqueue(EnemyPhase());
        }

        private IEnumerator EnemyPhase()
        {
            for (int step = 0; step < TurnCycle.Length; step++)
            {
                cycleIndex = (cycleIndex + 1) % TurnCycle.Length;
                int entry = TurnCycle[cycleIndex];
                if (entry == PartyTurn)
                    break;
                if (!Enemies[entry].Alive)
                    continue;
                RefreshTurnOrder();
                yield return Wait(Settings.EnemyGap);
                yield return EnemyAttack(entry);
            }

            // The mock goes on for ever: a beaten side comes back for the next turn.
            if (Array.TrueForAll(Enemies, enemy => !enemy.Alive))
                ReviveEnemies();
            if (Array.TrueForAll(Allies, ally => ally.Down))
                ReviveAllies();
            EnemyTurn = false;
            StartOfTurn(Turn + 1);
            dealing = StartCoroutine(Deal(Settings.TurnDraw, 0f));
        }

        /// <summary>The enemy steps forward, hits one ally still standing at random, and steps back.</summary>
        private IEnumerator EnemyAttack(int index)
        {
            var standing = new List<int>();
            for (int i = 0; i < Allies.Length; i++)
                if (!Allies[i].Down)
                    standing.Add(i);
            if (standing.Count == 0)
                yield break;
            var enemy = Enemies[index];
            int target = standing[UnityEngine.Random.Range(0, standing.Count)];
            yield return Perform(
                enemy.SkillName,
                new[] { enemy.Body },
                new[] { enemyBase[index] },
                Vector2.left,
                () => HitAlly(target, enemy.Power)
            );
        }

        // --- Deck ----------------------------------------------------------------------------

        /// <summary>
        /// Starts the battle's cards over: the hand is put away and the deck is shuffled anew (or
        /// put in <paramref name="order"/>, indexes of <see cref="Deck"/> drawn first to last),
        /// and the opening hand is dealt. Dealt <paramref name="instant"/>ly, the cards are in
        /// place at once, face up.
        /// </summary>
        public void RestartDeal(IReadOnlyList<int> order = null, bool instant = false)
        {
            if (dealing != null)
                StopCoroutine(dealing);
            dealing = null;
            ClearTable();
            if (order != null)
            {
                drawPile.Clear();
                drawPile.AddRange(order);
            }
            RefreshDeck();
            if (instant)
            {
                for (int i = 0; i < Settings.OpeningDraw && Draw(false); i++) { }
                Refresh();
            }
            else
                dealing = StartCoroutine(Deal(Settings.OpeningDraw, Settings.DealDelay));
        }

        /// <summary>Puts every card on screen away, hidden, and shuffles the whole deck anew.</summary>
        private void ClearTable()
        {
            PutBack();
            reserved = 0;
            hand.Clear();
            foreach (var card in Cards)
                card.Place = BattleInspectCardPlace.Deck;
            NewDeck();
            Refresh();
        }

        /// <summary>
        /// Fills the deck with every card of <see cref="Deck"/>, shuffled, whatever is in the hand:
        /// a card in hand can be drawn again.
        /// </summary>
        private void NewDeck()
        {
            drawPile.Clear();
            for (int i = 0; i < Deck.Length; i++)
                drawPile.Add(i);
            Shuffle(drawPile);
            RefreshDeck();
        }

        /// <summary>
        /// Deals up to <paramref name="count"/> cards one by one, stopping at the hand limit, then
        /// waits for the last to land. The fan is laid out for the whole deal first, so the cards
        /// in hand move right once to make room, and each dealt card flies right from the deck
        /// into the place left of the one before; nothing moves back left.
        /// </summary>
        private IEnumerator Deal(int count, float delay)
        {
            Refresh();
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            // The deck never runs dry (it is dealt anew), so only the hand limit and the cards on
            // screen bound the deal.
            int room = Mathf.Min(Settings.HandLimit, Cards.Length) - hand.Count;
            reserved = Deck.Length > 0 ? Mathf.Max(0, Mathf.Min(count, room)) : 0;
            while (reserved > 0 && Draw(true))
                yield return new WaitForSeconds(Settings.DealInterval);
            reserved = 0;
            while (hand.Exists(IsFlying))
                yield return null;
            dealing = null;
            Refresh();
        }

        /// <summary>
        /// Draws the top card of the deck onto a card not in the hand and puts it at the left end
        /// of the hand (the side of the deck). An empty deck is first dealt anew. Returns false,
        /// drawing nothing, when the hand is full.
        /// </summary>
        private bool Draw(bool fly)
        {
            int index = Array.FindIndex(Cards, card => !card.InHand);
            if (hand.Count >= Settings.HandLimit || index < 0 || Deck.Length == 0)
                return false;
            if (drawPile.Count == 0)
                NewDeck();
            Show(index, drawPile[0]);
            drawPile.RemoveAt(0);
            Cards[index].Place = BattleInspectCardPlace.Hand;
            hand.Insert(0, index);
            if (reserved > 0)
                reserved--;
            LayoutHand();
            if (fly)
                motion[index].Launch(DeckPose(index), Settings.DealFlight);
            else
                motion[index].Snap(motion[index].Rest);
            SortCards();
            // At once: this runs after Update, so the card would otherwise show for a frame
            // where it last was (face up in the fan, or where it was played).
            ApplyMotion(index);
            RefreshDeck();
            return true;
        }

        /// <summary>A small, tilted card over the deck's picture, where a dealt card starts.</summary>
        private Pose DeckPose(int index)
        {
            float scale = Settings.DealStartScale;
            float angle = Settings.DealStartAngle;
            if (DeckAnchor == null)
                return new Pose(motion[index].Rest.Position, angle, scale);
            // Just right of the deck's counter, level with its middle, so the card never
            // covers the number as it comes out.
            var area = DeckAnchor.rect;
            Vector2 edge = Hand.InverseTransformPoint(
                DeckAnchor.TransformPoint(new Vector2(area.xMax, area.center.y))
            );
            var size = Cards[index].Body.rect.size * (scale * Settings.CardScale);
            float a = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(a);
            float sin = Mathf.Sin(a);
            // The card turns about its bottom centre: find its left end and its middle.
            float left = Mathf.Min(
                Mathf.Min(-size.x * 0.5f * cos, size.x * 0.5f * cos),
                Mathf.Min(-size.x * 0.5f * cos - size.y * sin, size.x * 0.5f * cos - size.y * sin)
            );
            var middle = new Vector2(-size.y * 0.5f * sin, size.y * 0.5f * cos);
            var pivot = new Vector2(edge.x + DeckGap - left, edge.y - middle.y);
            return new Pose(pivot, angle, scale);
        }

        private static void Shuffle(List<int> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }

        /// <summary>Puts a card of the deck on a card on screen: its face and its rules.</summary>
        private void Show(int index, int deckIndex)
        {
            var card = Cards[index];
            var data = Deck[deckIndex];
            card.DeckIndex = deckIndex;
            card.Cost = data.Cost;
            card.Power = data.Power;
            card.Element = data.Element;
            card.Effect = data.Effect;
            card.Caster = data.Caster;
            if (card.Face != null)
                card.Face.Show(
                    data.Name,
                    data.Owner,
                    data.Kind,
                    data.Description,
                    data.Art,
                    data.Frame,
                    data.ElementIcon,
                    data.Cost
                );
        }

        private void RefreshDeck()
        {
            if (DeckLabel != null)
                DeckLabel.text = drawPile.Count.ToString();
        }

        /// <summary>The upcoming turns from the current one, skipping defeated enemies.</summary>
        public int[] UpcomingTurns(int count)
        {
            var turns = new int[count];
            if (TurnCycle.Length == 0)
                return turns;
            int filled = 0;
            for (int step = 0; filled < count && step < TurnCycle.Length * (count + 1); step++)
            {
                int entry = TurnCycle[(cycleIndex + step) % TurnCycle.Length];
                if (entry == PartyTurn || Enemies[entry].Alive)
                    turns[filled++] = entry;
            }
            return turns;
        }

        private int NextPartyEntry(int from)
        {
            for (int step = 1; step <= TurnCycle.Length; step++)
            {
                int index = (from + step) % TurnCycle.Length;
                if (TurnCycle[index] == PartyTurn)
                    return index;
            }
            return from;
        }

        private void RefreshTurnOrder()
        {
            var turns = UpcomingTurns(TurnSlots.Length);
            for (int i = 0; i < TurnSlots.Length; i++)
            {
                var slot = TurnSlots[i];
                bool party = turns[i] == PartyTurn;
                slot.Party.SetActive(party);
                slot.Enemy.gameObject.SetActive(!party);
                slot.Layout.preferredWidth = party ? PartySlotWidth : EnemySlotWidth;
                slot.Frame.color =
                    i == 0 ? CurrentTurnFrame
                    : party ? PartyTurnFrame
                    : EnemyTurnFrame;
                slot.Fill.color = party ? PartyTurnFill : EnemyTurnFill;
                if (!party)
                {
                    var enemy = Enemies[turns[i]];
                    slot.Enemy.texture = enemy.Sprite.texture;
                    slot.Enemy.uvRect = enemy.IconUv;
                }
            }
        }

        private void StartOfTurn(int turn)
        {
            Turn = turn;
            MaxEnergy = MaxEnergyAt(turn);
            Energy = MaxEnergy;
            Refresh();
        }

        // --- Effects -----------------------------------------------------------------------

        /// <summary>
        /// Pays for the card and takes it out of the hand at once, then queues its action: the
        /// user steps forward, the effect lands, and the user steps back.
        /// </summary>
        private void Play(int index, List<int> chosen, bool onEnemies)
        {
            var card = Cards[index];
            Energy -= card.Cost;
            card.Place = BattleInspectCardPlace.Discard;
            hand.Remove(index);
            LayoutHand();
            Refresh();

            // The card on screen is reused by the next draw, so the action keeps its own copy.
            var data = Deck[card.DeckIndex];
            int caster = card.Caster;
            var parts = Array.Empty<RectTransform>();
            var bases = Array.Empty<Vector2>();
            if (caster >= 0 && caster < Allies.Length)
            {
                parts = new[] { Allies[caster].Body, Allies[caster].HpBar };
                bases = new[] { allyBase[caster], allyBarBase[caster] };
            }
            Enqueue(
                Perform(
                    data.Name,
                    parts,
                    bases,
                    Vector2.right,
                    () => ApplyCard(data, chosen, onEnemies)
                )
            );
        }

        private void ApplyCard(BattleInspectCardData card, List<int> chosen, bool onEnemies)
        {
            if (onEnemies)
            {
                foreach (int target in chosen)
                    Hit(target, card.Power, card.Element);
            }
            else if (card.Effect == BattleInspectCardEffect.Heal)
            {
                foreach (int target in chosen)
                {
                    var ally = Allies[target];
                    ally.Hp = Mathf.Min(ally.MaxHp, ally.Hp + card.Power);
                    RefreshAlly(ally);
                    ShowNumber(HealNumber, ally.TargetArea, card.Power.ToString());
                }
            }
            else
            {
                ShowPopup(PartyAnchor, $"ブロック +{card.Power}", GuardColor, 48f);
            }
            Refresh();
        }

        /// <summary>Runs actions one after another, so each character's step ends before the next.</summary>
        private void Enqueue(IEnumerator action)
        {
            actions.Enqueue(action);
            if (acting)
                return;
            acting = true;
            StartCoroutine(RunActions());
        }

        private IEnumerator RunActions()
        {
            while (actions.Count > 0)
                yield return actions.Dequeue();
            acting = false;
            Refresh();
        }

        /// <summary>
        /// One action: the skill's name shows at the top, the character (<paramref name="parts"/>,
        /// resting at <paramref name="bases"/>) steps one step toward <paramref name="forward"/>,
        /// the effect lands, and after a moment the character steps back.
        /// </summary>
        private IEnumerator Perform(
            string skill,
            RectTransform[] parts,
            Vector2[] bases,
            Vector2 forward,
            Action effect
        )
        {
            ShowSkillName(skill);
            var front = forward * (Mathf.Round(Settings.StepDistance / Dot) * Dot);
            yield return Slide(parts, bases, Vector2.zero, front);
            effect();
            yield return Wait(Settings.ActionHold);
            yield return Slide(parts, bases, front, Vector2.zero);
            HideSkillName();
        }

        /// <summary>Moves the parts from one offset to another on an ease-out, in whole dots.</summary>
        private IEnumerator Slide(RectTransform[] parts, Vector2[] bases, Vector2 from, Vector2 to)
        {
            float duration = Settings.StepTime;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = 1f - (1f - t / duration) * (1f - t / duration);
                var offset = Vector2.Lerp(from, to, k);
                offset =
                    new Vector2(Mathf.Round(offset.x / Dot), Mathf.Round(offset.y / Dot)) * Dot;
                for (int i = 0; i < parts.Length; i++)
                    parts[i].anchoredPosition = bases[i] + offset;
                yield return null;
            }
            for (int i = 0; i < parts.Length; i++)
                parts[i].anchoredPosition = bases[i] + to;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
                yield return null;
        }

        private void ShowSkillName(string skill)
        {
            skillShown = skill;
            ShowBanner(skill, skillNameColor);
        }

        private void HideSkillName()
        {
            skillShown = null;
            // A notice fades by itself when its time is up.
            if (!noticeShown)
                FadeBanner();
        }

        /// <summary>
        /// Shows a warning where the skill names are, in the red of a cost the energy cannot pay,
        /// for a moment; then the skill being used, if any, comes back.
        /// </summary>
        private void ShowNotice(string text)
        {
            if (SkillBanner == null)
                return;
            ShowBanner(text, BattleInspectCardView.ShortCost);
            noticeShown = true;
            bannerFade = StartCoroutine(EndNotice());
        }

        private IEnumerator EndNotice()
        {
            yield return Wait(NoticeHold);
            noticeShown = false;
            bannerFade = null;
            if (skillShown != null)
                ShowBanner(skillShown, skillNameColor);
            else
                FadeBanner();
        }

        private void ShowBanner(string text, Color color)
        {
            if (SkillBanner == null)
                return;
            if (bannerFade != null)
                StopCoroutine(bannerFade);
            bannerFade = null;
            noticeShown = false;
            SkillBanner.SetText(text);
            if (SkillBanner.Label != null)
                SkillBanner.Label.color = color;
            SkillBanner.gameObject.SetActive(true);
            if (SkillBannerGroup != null)
                SkillBannerGroup.alpha = 1f;
        }

        private void FadeBanner()
        {
            if (SkillBanner == null || !SkillBanner.gameObject.activeSelf)
                return;
            if (bannerFade != null)
                StopCoroutine(bannerFade);
            bannerFade = StartCoroutine(FadeSkillName());
        }

        private IEnumerator FadeSkillName()
        {
            const float duration = 0.2f;
            for (float t = 0f; t < duration && SkillBannerGroup != null; t += Time.deltaTime)
            {
                SkillBannerGroup.alpha = 1f - t / duration;
                yield return null;
            }
            SkillBanner.gameObject.SetActive(false);
            bannerFade = null;
        }

        private void Hit(int index, int power, BattleInspectElement element)
        {
            var enemy = Enemies[index];
            // An earlier card of the queue may have beaten it already.
            if (!enemy.Alive)
                return;

            var weakness =
                element == BattleInspectElement.None
                    ? null
                    : Array.Find(enemy.Weaknesses, entry => entry.Element == element);
            bool weak = weakness != null;
            weakness?.Show(true);
            int damage = weak ? Mathf.RoundToInt(power * WeakMultiplier) : power;
            enemy.Hp = Mathf.Max(0, enemy.Hp - damage);
            RefreshEnemy(enemy);

            // A weakness shows in the number's colour and size, without words.
            ShowNumber(weak ? WeakNumber : DamageNumber, enemy.TargetArea, damage.ToString());
            StartCoroutine(HitReaction(index));
        }

        private IEnumerator HitReaction(int index)
        {
            var enemy = Enemies[index];
            enemy.Sprite.color = HitTint;
            yield return Shake(enemy.Body, enemyBase[index], 10f, 0.24f);
            enemy.Sprite.color = Color.white;

            if (enemy.Alive)
                yield break;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                enemy.Group.alpha = 1f - t / 0.5f;
                yield return null;
            }
            enemy.Group.alpha = 0f;
            RefreshTurnOrder();
        }

        private void HitAlly(int index, int damage)
        {
            var ally = Allies[index];
            ally.Hp = Mathf.Max(0, ally.Hp - damage);
            RefreshAlly(ally);
            ShowNumber(DamageNumber, ally.TargetArea, damage.ToString());
            StartCoroutine(AllyHitReaction(index));
        }

        private IEnumerator AllyHitReaction(int index)
        {
            var ally = Allies[index];
            ally.Sprite.color = HitTint;
            yield return Shake(ally.Body, allyBase[index], 10f, 0.24f);
            RefreshAlly(ally);
        }

        private void ReviveAllies()
        {
            foreach (var ally in Allies)
            {
                ally.Hp = ally.StartHp;
                RefreshAlly(ally);
            }
        }

        private void ReviveEnemies()
        {
            foreach (var enemy in Enemies)
            {
                enemy.Hp = enemy.MaxHp;
                enemy.Group.alpha = 1f;
                RefreshEnemy(enemy);
            }
        }

        private void Refresh()
        {
            for (int i = 0; i < Cards.Length; i++)
                RefreshCard(i);
            if (EndTurnButton != null)
                EndTurnButton.interactable = !Dealing && !EnemyTurn;
            if (EnergyLabel != null)
                EnergyLabel.text = EnergyText(Energy, MaxEnergy);
            foreach (var enemy in Enemies)
                RefreshEnemy(enemy);
            RefreshTurnOrder();
        }

        /// <summary>Hides cards off the hand and darkens those the energy cannot pay for.</summary>
        private void RefreshCard(int index)
        {
            var card = Cards[index];
            // A card darkens only once it has landed, so the backs fly in at full brightness.
            bool playable = card.Cost <= Energy || motion[index].Flying;
            // A card the energy cannot pay for is see-through in the hand, but not once raised,
            // so its details can still be read.
            float alpha =
                !card.InHand ? 0f
                : playable || index == held ? 1f
                : Settings.UnplayableAlpha;
            // The raised card takes taps too: a second tap on it plays it without a target.
            bool blocks = card.InHand;
            // Set only on a change, as this runs every frame and a set dirties the canvas.
            if (card.Group.alpha != alpha)
                card.Group.alpha = alpha;
            if (card.Group.blocksRaycasts != blocks)
                card.Group.blocksRaycasts = blocks;
            if (card.Face != null)
            {
                // The group's alpha thins the veil too, so it is made thicker by as much to keep
                // the settings' darkness.
                float darkness =
                    alpha > 0f ? Mathf.Min(1f, Settings.UnplayableDarkness / alpha) : 0f;
                card.Face.SetPlayable(playable, darkness);
            }
        }

        private static void RefreshEnemy(BattleInspectEnemy enemy)
        {
            float ratio = enemy.MaxHp > 0 ? enemy.Hp / (float)enemy.MaxHp : 0f;
            enemy.HpFill.anchorMax = new Vector2(ratio, 1f);
        }

        private static void RefreshAlly(BattleInspectAlly ally)
        {
            float ratio = ally.MaxHp > 0 ? ally.Hp / (float)ally.MaxHp : 0f;
            ally.HpFill.anchorMax = new Vector2(ratio, 1f);
            // A downed ally is greyed out until healed.
            ally.Sprite.color = ally.Down ? DownTint : Color.white;
        }

        private void ShowPopup(RectTransform anchor, string text, Color color, float size)
        {
            if (PopupTemplate == null || anchor == null)
                return;
            var popup = Spawn(
                PopupTemplate,
                anchor.TransformPoint(anchor.rect.center),
                text,
                MessageRise
            );
            popup.color = color;
            popup.fontSize = size;
        }

        private void ShowNumber(TMP_Text template, RectTransform anchor, string text)
        {
            if (template == null || anchor == null)
                return;
            var rect = anchor.rect;
            var start = new Vector2(rect.center.x, rect.yMax - NumberBelowTop);
            Spawn(template, anchor.TransformPoint(start), text, NumberRise);
        }

        private TMP_Text Spawn(TMP_Text template, Vector3 position, string text, float rise)
        {
            var popup = Instantiate(template, template.transform.parent);
            popup.gameObject.SetActive(true);
            popup.text = text;
            popup.transform.position = position;
            StartCoroutine(Rise(popup, rise));
            return popup;
        }

        private static IEnumerator Rise(TMP_Text popup, float distance)
        {
            var rect = (RectTransform)popup.transform;
            var start = rect.anchoredPosition;
            const float duration = 0.9f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                // Jump up quickly, then hang and fade.
                rect.anchoredPosition =
                    start + Vector2.up * (distance * (1f - (1f - k) * (1f - k)));
                popup.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                yield return null;
            }
            Destroy(popup.gameObject);
        }

        private static IEnumerator Shake(
            RectTransform rect,
            Vector2 basePosition,
            float distance,
            float duration
        )
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float side = Mathf.Repeat(t * 30f, 2f) < 1f ? 1f : -1f;
                float fade = 1f - t / duration;
                rect.anchoredPosition =
                    basePosition + Vector2.right * Mathf.Round(side * distance * fade / Dot) * Dot;
                yield return null;
            }
            rect.anchoredPosition = basePosition;
        }

        private struct Pose
        {
            public Vector2 Position;
            public float Angle;
            public float Scale;

            public Pose(Vector2 position, float angle, float scale)
            {
                Position = position;
                Angle = angle;
                Scale = scale;
            }
        }

        /// <summary>
        /// A card's rest pose in the fan and a spring that carries it to its target. A dealt card
        /// first flies from the deck on an ease-out (a spring would overshoot far on so long a
        /// way), turning face up on the way; then the spring takes over.
        /// </summary>
        private struct CardMotion
        {
            public Pose Rest;
            public Pose Current;
            public Pose Target;

            /// <summary>0 on its back, 1 face up; the card is edge-on at 0.5.</summary>
            public float Reveal;
            private Vector2 velocity;
            private float angularVelocity;
            private float scaleVelocity;
            private Pose from;
            private float flightTime;
            private float flightDuration;

            public bool Flying => flightTime < flightDuration;

            public void Snap(Pose pose)
            {
                Current = Target = pose;
                velocity = Vector2.zero;
                angularVelocity = scaleVelocity = 0f;
                flightTime = flightDuration = 0f;
                Reveal = 1f;
            }

            public void Launch(Pose start, float duration)
            {
                Snap(start);
                from = start;
                flightDuration = duration;
                Reveal = 0f;
            }

            // An under-damped spring, so a card overshoots a little and settles ("boing").
            public void Step(float dt, float stiffness, float damping)
            {
                if (Flying)
                {
                    flightTime += dt;
                    float k = Mathf.Clamp01(flightTime / flightDuration);
                    float ease = 1f - (1f - k) * (1f - k) * (1f - k);
                    // The target moves while the others make room, so follow it every frame.
                    Current = new Pose(
                        Vector2.LerpUnclamped(from.Position, Target.Position, ease),
                        Mathf.LerpUnclamped(from.Angle, Target.Angle, ease),
                        Mathf.LerpUnclamped(from.Scale, Target.Scale, ease)
                    );
                    // Face up a little before landing.
                    Reveal = Mathf.Clamp01(k * 1.25f);
                    return;
                }
                velocity +=
                    (stiffness * (Target.Position - Current.Position) - damping * velocity) * dt;
                angularVelocity +=
                    (stiffness * (Target.Angle - Current.Angle) - damping * angularVelocity) * dt;
                scaleVelocity +=
                    (stiffness * (Target.Scale - Current.Scale) - damping * scaleVelocity) * dt;
                Current.Position += velocity * dt;
                Current.Angle += angularVelocity * dt;
                Current.Scale += scaleVelocity * dt;
            }
        }
    }
}
