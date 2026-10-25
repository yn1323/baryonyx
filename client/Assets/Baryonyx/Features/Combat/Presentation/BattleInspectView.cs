using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.Inventory;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>A card's or a weakness's element; the same order as <see cref="CardElement"/>.</summary>
    public enum BattleInspectElement
    {
        None,
        Slash,
        Fire,
        Ice,
        Thunder,
        Blunt,
        Pierce,
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

        /// <summary>Played on the one ally it is dropped on, without healing it (a buff, a guard).</summary>
        OneAlly,

        /// <summary>Played for the whole party: on itself, on every ally, or on no one (the hand, the energy).</summary>
        Party,
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

        /// <summary>
        /// The card skill's id (<see cref="CardSkills"/>): what it does and how its effect plays.
        /// Empty for a card of the mock's own (its damage, heal or block of <see cref="Power"/>).
        /// </summary>
        public string Skill;

        /// <summary>The kind and the scope, e.g. "攻撃・敵単体" (rich text).</summary>
        public string Kind;

        /// <summary>The effect, with its number in another colour (rich text).</summary>
        public string Description;
        public Texture Art;
        public Texture Frame;

        /// <summary>The element icon, or null for a card without an element.</summary>
        public Texture ElementIcon;
        public int Cost;

        /// <summary>The quality (S, A, B or C): how much the card sparkles.</summary>
        public ItemQuality Quality = ItemQuality.C;

        /// <summary>How the card sparkles: its kind's style (<see cref="CardSkill.FoilStyle"/>).</summary>
        public int FoilStyle;
        public int Power;
        public BattleInspectElement Element;
        public BattleInspectCardEffect Effect;

        /// <summary>The ally who uses the card (an index of the view's Allies).</summary>
        public int Caster;

        /// <summary>
        /// The card skill itself, with its multiplier, when the deck was made from a party
        /// (<see cref="BattleInspectView.UseParty"/>); null for the baked deck, whose skill is
        /// found by <see cref="Skill"/>.
        /// </summary>
        [NonSerialized]
        public CardSkill Card;

        /// <summary>The character whose unique skill the card is (<see cref="BattlePartyCard.Person"/>).</summary>
        [NonSerialized]
        public string Person = "";

        /// <summary>Which of the character's unique skills it is, or -1 (<see cref="BattlePartyCard.Unique"/>).</summary>
        [NonSerialized]
        public int Unique = -1;

        /// <summary>The owned skill card it is, or empty (<see cref="BattlePartyCard.OwnedCard"/>).</summary>
        [NonSerialized]
        public string OwnedCard = "";

        /// <summary>The art of another card skill by its id (<see cref="BattlePartyCard.ArtOf"/>).</summary>
        [NonSerialized]
        public Func<string, Texture> ArtOf;
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
            Effect
                is BattleInspectCardEffect.DamageAll
                    or BattleInspectCardEffect.Guard
                    or BattleInspectCardEffect.Party;
    }

    [Serializable]
    public sealed class BattleInspectAlly
    {
        /// <summary>The ally's feet: the sprite, its shadow and target area move with it.</summary>
        public RectTransform Body;
        public RawImage Sprite;
        public RectTransform TargetArea;

        /// <summary>The HP bar under the feet, with the ally's block over it.</summary>
        public BattleHpGauge Gauge;

        /// <summary>The cursor over the head, shown while a card that can target this ally is up.</summary>
        public RectTransform Marker;

        /// <summary>The gold ring of light at the feet, shown while a card this ally uses is up.</summary>
        public GameObject CasterRing;

        /// <summary>The ally's name under the HP bar, shown with the ring.</summary>
        public GameObject NameTag;

        /// <summary>The ally's name, shown on the name tag and as the owner of its cards.</summary>
        public string Name;

        /// <summary>The party slot (0 to 3) whose member stands here (<see cref="BattleInspectView.UseParty"/>).</summary>
        public int Slot;
        public int StartHp;
        public int MaxHp;

        /// <summary>The level, whose growth sets the defense constant of the ally's blows.</summary>
        public int Level;

        /// <summary>
        /// The ally's stats at its level (doc/features/party.md, the stats): the powers of its
        /// cards are parts of them, and enemies' blows are softened by its 物防.
        /// </summary>
        public CharacterStats Stats;

        /// <summary>
        /// Boosts that work while their condition holds, the stats before any boon and the
        /// percents the boons grew <see cref="Stats"/> by (BattlePartyMember). The battle reads
        /// the stats through <see cref="BattleInspectView.StatsNow"/>.
        /// </summary>
        [NonSerialized]
        public IReadOnlyList<BattleStatBoost> Boosts = Array.Empty<BattleStatBoost>();

        [NonSerialized]
        public CharacterStats Unboosted;

        [NonSerialized]
        public IReadOnlyList<float> Percents = Array.Empty<float>();

        /// <summary>The chance in percent that the ally's attack card plays once more (BattlePartyMember).</summary>
        [NonSerialized]
        public int TwiceChance;

        [NonSerialized]
        public int Hp;

        [NonSerialized]
        private Color? tint;

        /// <summary>The colour the sprite is drawn in (a stand-in character's tint), white unless set.</summary>
        public Color Tint
        {
            get => tint ?? Color.white;
            set => tint = value;
        }

        /// <summary>No one stands here: the party's slot is empty. It is down and hidden for the whole battle.</summary>
        [NonSerialized]
        public bool Absent;

        /// <summary>The HP bar under the feet; it steps forward with the ally.</summary>
        public RectTransform HpBar => (RectTransform)Gauge.transform;

        public bool Down => Hp <= 0;
    }

    [Serializable]
    public sealed class BattleInspectEnemy
    {
        /// <summary>Its kind (<see cref="Baryonyx.Combat.CombatEnemies"/>), which its revealed weaknesses are kept under.</summary>
        public string Id;
        public RectTransform Body;
        public RectTransform TargetArea;
        public RawImage Sprite;
        public BattleHpGauge Gauge;
        public CanvasGroup Group;

        /// <summary>The cursor over the head, shown while a card that can target this enemy is up.</summary>
        public RectTransform Marker;
        public int MaxHp;

        /// <summary>The name of its attack, shown while it attacks, and its power before the ally's defense.</summary>
        public string SkillName;
        public int Power;

        /// <summary>The level it shows up at, and its defenses against blows (物防) and spells (属防).</summary>
        public int Level;
        public int PhysicalDefense;
        public int MagicDefense;

        /// <summary>How often its blows land as critical hits, and how well it keeps statuses off.</summary>
        public int Critical;
        public int Luck;

        /// <summary>It attacks with spells, softened by the ally's magic defense instead of the physical.</summary>
        public bool Magic;
        public BattleInspectWeakness[] Weaknesses = Array.Empty<BattleInspectWeakness>();

        /// <summary>
        /// An icon for every element in the row over its HP bar, so another kind of enemy can take
        /// this place (<see cref="BattleInspectView.UseEncounter(System.Collections.Generic.IReadOnlyList{string}, int, float)"/>):
        /// only its weaknesses (<see cref="Weaknesses"/>) are shown.
        /// </summary>
        public BattleInspectWeakness[] WeaknessSlots = Array.Empty<BattleInspectWeakness>();

        /// <summary>The shadow at its feet, as wide as its picture.</summary>
        public RectTransform Shadow;

        /// <summary>The face crop of <see cref="Sprite"/> shown in the turn order.</summary>
        public Rect IconUv;

        /// <summary>A boss: its defeat holds longer, flashes the screen and crumbles slower.</summary>
        public bool Boss;

        /// <summary>
        /// Its speed for the turn order; 0 (the mock's enemies) keeps <see cref="BattleInspectView.TurnCycle"/>.
        /// </summary>
        [NonSerialized]
        public int Speed;

        /// <summary>Its 物攻 and 属攻 at its level, which its skills' blows are parts of, and how much stronger the room makes them.</summary>
        [NonSerialized]
        public int PhysicalAttack;

        [NonSerialized]
        public int MagicAttack;

        [NonSerialized]
        public float AttackScale = 1f;

        /// <summary>Its choice of skills (doc/catalog/enemies); null for the mock's one attack.</summary>
        [NonSerialized]
        public EnemyBrain Brain;

        /// <summary>The block it holds; it lasts until its next action.</summary>
        [NonSerialized]
        public int Block;

        /// <summary>The percent its next attacking skill is stronger by (力を溜める).</summary>
        [NonSerialized]
        public int PowerUp;

        [NonSerialized]
        public int Hp;

        public bool Alive => Hp > 0;
    }

    /// <summary>
    /// One weakness of an enemy, shown as an icon over its HP bar. A weakness not yet revealed
    /// shows "?" until a card hits it, and then glints as it turns into the element's icon
    /// (<see cref="Reveal"/>).
    /// </summary>
    [Serializable]
    public sealed class BattleInspectWeakness
    {
        // The glint: the ? swells and whitens, turns into the element's icon at the peak of the
        // light, then the icon settles back with a little bounce as a band of light runs over it.
        public const float Swell = 0.1f;
        public const float Settle = 0.45f;
        private const float SwellScale = 0.15f;

        // How bright the light on the icon is drawn: past white, so Bloom spreads it.
        private const float GlintBright = 1.6f;

        public BattleInspectElement Element;
        public bool StartsRevealed;
        public GameObject Known;
        public GameObject Unknown;

        /// <summary>
        /// The light drawn over the icon in its own shape while it glints (Battle Weakness
        /// Glint shader); hidden otherwise.
        /// </summary>
        public BattleVfxImage Glint;

        [NonSerialized]
        public bool Revealed;

        /// <summary>True while the icon glints.</summary>
        public bool Glinting => Glint != null && Glint.gameObject.activeSelf;

        public void Show(bool revealed)
        {
            Revealed = revealed;
            Known.SetActive(revealed);
            Unknown.SetActive(!revealed);
        }

        /// <summary>
        /// Reveals the weakness with a glint, in real time so it plays through a hit stop. It
        /// counts as revealed at once; the icon turns at the peak of the light, when the twinkle
        /// of <paramref name="vfx"/> flares at its corner, unless <paramref name="shown"/> says
        /// the enemy is gone by then. Without <see cref="Glint"/> it turns at once.
        /// </summary>
        public IEnumerator Reveal(BattleSkillVfx vfx, Func<bool> shown = null)
        {
            Revealed = true;
            if (Glint == null)
            {
                Show(true);
                yield break;
            }
            BattleVfxImage.EnableShapeChannel(Glint.transform);
            bool turned = false;
            for (float t = 0f; t < Swell + Settle; t += Time.unscaledDeltaTime)
            {
                if (!turned && t >= Swell)
                {
                    turned = true;
                    if (vfx != null && (shown == null || shown()))
                        vfx.Glint(
                            (RectTransform)Known.transform.parent,
                            BattleSkillVfx.ColorOf(
                                BattleSkillVfx.KindFor(Element, BattleInspectCardEffect.DamageOne)
                            )
                        );
                }
                Pose(t);
                yield return null;
            }
            Pose(Swell + Settle);
        }

        /// <summary>
        /// Draws the icon <paramref name="t"/> seconds into its glint: the ? swelling and
        /// whitening until <see cref="Swell"/>, then the element's icon, white at first, settling
        /// back with a little bounce while the band of light crosses it; at rest after
        /// <see cref="Settle"/>. Only the icon: the twinkle is <see cref="BattleSkillVfx.Glint"/>.
        /// </summary>
        public void Pose(float t)
        {
            var slot = (RectTransform)Known.transform.parent;
            bool turned = t >= Swell;
            Known.SetActive(turned);
            Unknown.SetActive(!turned);
            if (t >= Swell + Settle)
            {
                slot.localScale = Vector3.one;
                Glint.gameObject.SetActive(false);
                return;
            }
            Glint.gameObject.SetActive(true);
            Glint.color = Color.white;
            Glint.texture = (turned ? Known : Unknown).GetComponent<RawImage>().texture;
            if (!turned)
            {
                float k = t / Swell;
                slot.localScale = Vector3.one * (1f + SwellScale * k * k);
                Glint.Shape = new Vector4(k * k, 0f, 0f, GlintBright);
                return;
            }
            float after = t - Swell;
            // The white clears off the new icon fast, and the band crosses it after.
            float fill = Mathf.Clamp01(1f - after / 0.12f);
            float band = Mathf.Clamp01((after - 0.08f) / 0.24f);
            Glint.Shape = new Vector4(fill * fill, band, 0f, GlintBright);
            // A spring back to its size, overshooting a little below it once.
            float spring = Mathf.Exp(-after * 9f) * Mathf.Cos(after * 22f);
            slot.localScale = Vector3.one * (1f + SwellScale * spring);
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
    /// stands in the middle of the bottom of the screen. Hitting a hidden weakness reveals it: the
    /// ? glints and turns into the element's icon.
    /// A played card is paid for and leaves the hand at once, and its action is queued: the user
    /// steps one step forward, its name shows at the top middle of the screen, the effect lands,
    /// and the user steps back. "End turn" hands the turn to the enemies before the party's next
    /// turn in the order: each attacks one ally at random the same way. Spending the last of the
    /// energy ends the turn by itself, once the played cards have acted. A band across the middle
    /// of the screen tells whose turn begins.
    /// </summary>
    public sealed partial class BattleInspectView : MonoBehaviour
    {
        // One dot of the 4x pixel art; idle motion moves in whole dots.
        private const float Dot = 4f;
        private const float WeakMultiplier = 1.5f;

        /// <summary>The word over a critical hit, its colour, and how much larger its number shows.</summary>
        public const string CriticalText = "会心！";
        public static readonly Color CriticalColor = new(1f, 0.9f, 0.3f);
        public const float CriticalNumberScale = 1.3f;

        // Numbers start just under the top of the drawn body, above the character, not at its middle.
        private const float NumberBelowTop = 10f;

        // How far a rising popup travels: the numbers only lift a little, messages further.
        private const float NumberRise = 36f;
        private const float MessageRise = 90f;

        // How long a popup shows, and the line it takes in a stack of popups, by its font size.
        private const float PopupDuration = 1.3f;
        private const float PopupLineHeight = 1.15f;
        private const int MaxEnergyCap = 9;

        // How long a notice such as "エネルギー不足" stays in place of the skill name.
        private const float NoticeHold = 0.9f;

        private static readonly Color GuardColor = new(0.55f, 0.75f, 1f);
        private static readonly Color HitTint = new(1f, 0.45f, 0.4f);

        // An enemy caught in ice goes pale and cold instead of red.
        private static readonly Color FrozenTint = new(0.62f, 0.82f, 1f);
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
        /// The repeating turn order of the mock, whose enemies have no speed. <see cref="PartyTurn"/>
        /// is a party turn (the party acts as one side), other values are enemy indexes. A party
        /// entry twice in a row is the consecutive turn that a large speed gap gives. Enemies with
        /// a speed (an adventure's encounter) take their order from it instead (<see cref="CombatTurnOrder"/>).
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
        /// The material of the party and enemy sprites when they are not flashing: pixel art that
        /// keeps sharp while the camera's slow circle carries it between screen pixels.
        /// </summary>
        public Material ActorMaterial;

        /// <summary>Every kind of enemy's picture, for a battle that puts other enemies on the field.</summary>
        public BattleEnemyLooks EnemyLooks;

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

        /// <summary>
        /// The band across the middle of the screen that tells whose turn begins ("敵のターン",
        /// "味方のターン"); <see cref="TurnBannerGroup"/> fades it.
        /// </summary>
        public TranslucentTextPanel TurnBanner;

        public CanvasGroup TurnBannerGroup;

        /// <summary>The lines over and under the turn band, in the colour of the side whose turn it is.</summary>
        public Graphic[] TurnBannerLines = Array.Empty<Graphic>();

        // The turn band's lines and words: red for the enemies and blue for the party, like the
        // frames of the turn order, but brighter to read over the battlefield.
        public Color EnemyBannerLine = new(0.86f, 0.36f, 0.31f);
        public Color EnemyBannerText = new(1f, 0.66f, 0.6f);
        public Color PartyBannerLine = new(0.4f, 0.62f, 0.92f);
        public Color PartyBannerText = new(0.7f, 0.84f, 1f);

        /// <summary>
        /// The skill effects: a played card's effect lands when its blow does, and enemy attacks
        /// and defeats get their bursts. Without it the effects land at once, with no show.
        /// </summary>
        public BattleSkillVfx Vfx;

        [Min(1)]
        public int StartTurn = 1;

        private readonly List<int> targets = new();
        private readonly List<int> hand = new();
        private readonly List<int> drawPile = new();
        private readonly Queue<IEnumerator> actions = new();
        private readonly BattlePopupStack popupStack = new();
        private Coroutine dealing;
        private Coroutine bannerFade;
        private Coroutine turnBannerShow;
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

        // How far each ally has stepped forward for the raised card, from 0 (home) to 1 (one step).
        private float[] allyStance = Array.Empty<float>();

        // Actions and hits under way on each ally; while any is, they move the ally, not the stance.
        private int[] allyBusy = Array.Empty<int>();
        private CardMotion[] motion = Array.Empty<CardMotion>();
        private Material flash;

        // The blink of white on a character the moment a blow lands.
        private Material hitFlash;
        private int held = -1;
        private bool pressing;
        private bool armed;
        private bool moved;
        private bool reselected;
        private Vector2 pressPoint;
        private Vector2 fingerPoint;

        // The enemies acting in the enemies' turn under way, and the place of the one acting (-1
        // before the first acts and between the enemies' turns).
        private List<int> roundOrder = new();
        private int roundStep = -1;

        // 戦闘の最初に、パーティより速い敵が味方の番より先に動いている間（ターンはまだ進めない）。
        private bool openingPhase;

        public const int PartyTurn = -1;

        public int Turn { get; private set; }

        /// <summary>The battle's turn counted from 1, as the enemies' patterns count it.</summary>
        public int BattleTurn => Turn - StartTurn + 1;
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
                // Greyed out and lowered as in Play Mode when the opening energy cannot pay for it.
                var card = Cards[i];
                bool playable = card.Cost <= MaxEnergyAt(StartTurn);
                position.y -= RestDrop(Settings, playable);
                body.anchoredPosition = position;
                body.localRotation = Quaternion.Euler(0, 0, angle);
                body.localScale = Vector3.one * Settings.CardScale;

                float alpha = playable ? 1f : UnplayableAlpha(Settings, raised: false);
                UnityEditor.Undo.RecordObject(card.Group, "Apply hand layout");
                card.Group.alpha = alpha;
                if (card.Face != null)
                {
                    UnityEditor.Undo.RecordObject(card.Face.Shade, "Apply hand layout");
                    UnityEditor.Undo.RecordObject(card.Face.CostDigit, "Apply hand layout");
                    card.Face.SetPlayable(playable, VeilDarkness(Settings, alpha));
                }
            }
        }
#endif

        /// <summary>Where the card rests in the fan now (it springs there when not held).</summary>
        public Vector2 RestPosition(int index) => motion[index].Rest.Position;

        /// <summary>Energy starts at 5 and its maximum grows by one each turn, up to 9.</summary>
        public static int MaxEnergyAt(int turn) => Mathf.Min(MaxEnergyCap, 5 + turn - 1);

        public static string EnergyText(int energy, int max) => $"{energy}/{max}";

        /// <summary>The words of the turn band when the enemies' turn begins.</summary>
        public const string EnemyTurnText = "敵のターン";

        /// <summary>The warning in place of the skill name when the energy cannot pay for a card.</summary>
        public const string NoEnergyText = "エネルギー不足";

        /// <summary>
        /// The words of the turn band when a party turn begins: the turn's number after them, or,
        /// when the party acts <paramref name="again"/> with no enemy in between, that it does.
        /// </summary>
        public static string PartyTurnText(int turn, bool again) =>
            again
                ? "<size=62%>連続！</size> もう一度 味方のターン"
                : $"味方のターン<size=62%>　ターン{turn}</size>";

        /// <summary>
        /// The height of the end-turn button's middle above the bottom of the safe area: halfway
        /// up the part of the middle card of the hand that is on screen, so the thumb finds it
        /// beside the cards.
        /// </summary>
        public static float EndTurnMiddle(BattleInspectHandSettings settings, float cardHeight)
        {
            float top = settings.RestBottom + cardHeight * settings.CardScale;
            return (Mathf.Max(0f, settings.RestBottom) + top) / 2f;
        }

        /// <summary>
        /// How see-through a card the energy cannot pay for is: the settings' opacity in the hand,
        /// none once raised so its details can be read.
        /// </summary>
        public static float UnplayableAlpha(BattleInspectHandSettings settings, bool raised) =>
            raised ? 1f : settings.UnplayableAlpha;

        /// <summary>
        /// How far below its place in the fan a card rests: the settings' drop when the energy
        /// cannot pay for it, so it stands lower than the cards that can be played.
        /// </summary>
        public static float RestDrop(BattleInspectHandSettings settings, bool playable) =>
            playable ? 0f : settings.UnplayableDrop;

        /// <summary>
        /// The veil's own darkness on a card of <paramref name="alpha"/>: the card's alpha thins
        /// the veil too, so it is made thicker by as much to keep the settings' darkness.
        /// </summary>
        public static float VeilDarkness(BattleInspectHandSettings settings, float alpha) =>
            alpha > 0f ? Mathf.Min(1f, settings.UnplayableDarkness / alpha) : 0f;

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
            allyStance = new float[Allies.Length];
            allyBusy = new int[Allies.Length];
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

            if (Vfx != null)
            {
                // The effects light the characters near them.
                foreach (var ally in Allies)
                    Vfx.RegisterActor(ally.Sprite);
                foreach (var enemy in Enemies)
                    Vfx.RegisterActor(enemy.Sprite);
            }

            if (Settings == null)
                Settings = ScriptableObject.CreateInstance<BattleInspectHandSettings>();
            if (TargetFlash != null)
            {
                flash = new Material(TargetFlash);
                hitFlash = new Material(TargetFlash);
                hitFlash.SetFloat("_FlashAmount", 1f);
            }
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
            if (TurnBanner != null)
                TurnBanner.gameObject.SetActive(false);

            StartOfTurn(StartTurn);
            // The hand baked into the prefab is only for the look outside Play Mode.
            ClearTable();
        }

        private void Start()
        {
            // The adventure's battle opens with the enemies appearing, and deals after them.
            if (EndsWithOutcome)
            {
                Enqueue(Opening());
                return;
            }
            RestartDeal();
            ShowPartyTurnBanner(again: false);
        }

        private void OnDestroy()
        {
            if (flash != null)
                Destroy(flash);
            if (hitFlash != null)
                Destroy(hitFlash);
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

            // An ally felled or raised by anything (a lab, a moment) takes its cards with it.
            RefreshFallen();
            // Laid out every frame so changes to the settings show at once.
            LayoutHand();
            PlaceEndTurn();
            if (held >= 0)
                motion[held].Target = RaisedPose(held);
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            for (int i = 0; i < Cards.Length; i++)
            {
                motion[i].Step(dt, Settings.Stiffness, Settings.Damping);
                ApplyMotion(i);
            }
            RefreshCaster(dt);
            RefreshKeywordTips();

            if (flash != null)
                flash.SetFloat("_FlashAmount", 0.5f + 0.2f * Mathf.Sin(time * 9f));
            RefreshChargeGlow(time);

            // The cursors bob by one dot, a second up and a second down.
            var bob = Mathf.Repeat(time * 0.5f, 1f) < 0.5f ? Vector2.up * Dot : Vector2.zero;
            for (int i = 0; i < Allies.Length; i++)
                Allies[i].Marker.anchoredPosition = allyMarkerBase[i] + bob;
            for (int i = 0; i < Enemies.Length; i++)
                Enemies[i].Marker.anchoredPosition = enemyMarkerBase[i] + bob;

            // With no card pressed, a tap on the battlefield lists a character's statuses.
            if (TapArea != null)
                TapArea.SetActive(!pressing);
            RefreshAim();
        }

        /// <summary>Keeps the end-turn button halfway up the cards on screen as the settings change.</summary>
        private void PlaceEndTurn()
        {
            if (EndTurnButton == null || Cards.Length == 0)
                return;
            var button = (RectTransform)EndTurnButton.transform;
            var position = button.anchoredPosition;
            position.y =
                EndTurnMiddle(Settings, Cards[0].Body.rect.height)
                + (button.pivot.y - 0.5f) * button.rect.height;
            button.anchoredPosition = position;
        }

        /// <summary>Puts the card where its motion is now, face up or down, and shows or hides it.</summary>
        private void ApplyMotion(int index)
        {
            PlaceCard(Cards[index].Body, motion[index].Current, motion[index].Reveal);
            var back = Cards[index].Back;
            if (back != null && back.activeSelf != IsFaceDown(index))
                back.SetActive(IsFaceDown(index));
            RefreshCard(index);
        }

        /// <summary>Puts a card's body at <paramref name="pose"/>, turned over as far as <paramref name="reveal"/> (0 back, 1 face).</summary>
        private void PlaceCard(RectTransform body, Pose pose, float reveal)
        {
            body.anchoredPosition = pose.Position;
            body.localRotation = Quaternion.Euler(0, 0, pose.Angle);
            // Turning over: the card narrows to an edge on its back and widens on its face.
            float turn = Mathf.Max(0.04f, Mathf.Abs(Mathf.Cos(Mathf.PI * reveal)));
            body.localScale = new Vector3(turn, 1f, 1f) * (pose.Scale * Settings.CardScale);
        }

        /// <summary>
        /// While a card is up, its user steps one step toward the enemies with a gold ring at its
        /// feet and its name under the HP bar, so the screen tells who will use the card. The
        /// others go back home. An ally in the middle of an action or a hit is left to it.
        /// </summary>
        private void RefreshCaster(float dt)
        {
            int caster = held >= 0 ? Cards[held].Caster : -1;
            float speed = Settings.StepTime > 0f ? dt / Settings.StepTime : 1f;
            for (int i = 0; i < Allies.Length; i++)
            {
                var ally = Allies[i];
                bool up = i == caster;
                SetShown(ally.CasterRing, up);
                SetShown(ally.NameTag, up);
                if (allyBusy[i] > 0)
                    continue;
                float stance = Mathf.MoveTowards(allyStance[i], up ? 1f : 0f, speed);
                // Set only on a change, as this runs every frame and a set dirties the canvas.
                if (stance == allyStance[i])
                    continue;
                allyStance[i] = stance;
                var offset = StanceOffset(i);
                ally.Body.anchoredPosition = allyBase[i] + offset;
                ally.HpBar.anchoredPosition = allyBarBase[i] + offset;
            }
        }

        /// <summary>Where the ally's stance puts it from home, eased and in whole dots.</summary>
        private Vector2 StanceOffset(int ally) =>
            ToDots(StepOffset(Vector2.right) * Mathf.SmoothStep(0f, 1f, allyStance[ally]));

        /// <summary>One step toward <paramref name="forward"/>, in whole dots.</summary>
        private Vector2 StepOffset(Vector2 forward) =>
            forward * (Mathf.Round(Settings.StepDistance / Dot) * Dot);

        private static Vector2 ToDots(Vector2 offset) =>
            new Vector2(Mathf.Round(offset.x / Dot), Mathf.Round(offset.y / Dot)) * Dot;

        private static void SetShown(GameObject target, bool shown)
        {
            if (target != null && target.activeSelf != shown)
                target.SetActive(shown);
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
            // A card in the hand takes the place of the status list.
            HideStatusList();
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
            // A whole-side card already holds its targets from the press; start afresh so none
            // is counted twice.
            targets.Clear();
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
        /// With no card up, it opens or closes the status list instead.
        /// </summary>
        public void TapScreen(Vector2 screenPoint)
        {
            if (!Selected)
            {
                if (!pressing)
                    TapCharacter(screenPoint);
                return;
            }
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
            if (play && CostOf(card) > Energy)
            {
                ShowNotice(NoEnergyText);
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
                // A card the energy cannot pay for flies straight to its lowered place, so it does not
                // sink after it lands.
                position.y -= RestDrop(Settings, Affordable(i));
                motion[i].Rest = new Pose(position, angle, 1f);
                if (i != held)
                    motion[i].Target = motion[i].Rest;
            }
        }

        private Pose RaisedPose(int index)
        {
            // In the middle of the screen from left to right, a little below the middle from top
            // to bottom, its bottom edge the settings' RaisedBottom up. The hand's origin is the bottom centre of the screen area and the card
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

        private Camera EventCamera() => CameraOf(Hand);

        /// <summary>
        /// The camera that draws <paramref name="rect"/>'s canvas, for turning screen points into
        /// its space: none for the controls on the overlay canvas, the battlefield camera for the
        /// characters on the stage canvas.
        /// </summary>
        public static Camera CameraOf(RectTransform rect)
        {
            var canvas = rect != null ? rect.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
                return null;
            canvas = canvas.rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        /// <summary>Where a point of a character (in its local space) is on the screen.</summary>
        public static Vector2 ScreenPointOf(RectTransform rect, Vector2 local) =>
            RectTransformUtility.WorldToScreenPoint(CameraOf(rect), rect.TransformPoint(local));

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

        // A fallen ally takes only a card that revives (Fallen).
        private bool CanTarget(BattleInspectCard card, int index) =>
            card.TargetsEnemies
                ? Enemies[index].Alive
                : !Allies[index].Absent && (!Allies[index].Down || Revives(card));

        /// <summary>The character of the card's side under or near a screen point, or -1.</summary>
        private int NearestTarget(BattleInspectCard card, Vector2 screenPoint)
        {
            int count = card.TargetsEnemies ? Enemies.Length : Allies.Length;
            int nearest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!CanTarget(card, i))
                    continue;
                var area = card.TargetsEnemies ? Enemies[i].TargetArea : Allies[i].TargetArea;
                float distance = DropDistance(area, screenPoint, CameraOf(area));
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
            for (int i = 0; i < Enemies.Length; i++)
                Enemies[i].Sprite.material =
                    onEnemies && targets.Contains(i) ? flash : ActorMaterial;
            for (int i = 0; i < Allies.Length; i++)
                Allies[i].Sprite.material =
                    !onEnemies && targets.Contains(i) ? flash : ActorMaterial;
            ShowMarkers(held >= 0 ? Cards[held] : null);
        }

        /// <summary>
        /// Puts the cursor over every character <paramref name="card"/> can target, and its
        /// forecast on their bars; or none without a card.
        /// </summary>
        private void ShowMarkers(BattleInspectCard card)
        {
            ShowForecast(card);
            for (int i = 0; i < Enemies.Length; i++)
                Enemies[i]
                    .Marker.gameObject.SetActive(
                        card != null && card.TargetsEnemies && CanTarget(card, i)
                    );
            for (int i = 0; i < Allies.Length; i++)
                Allies[i]
                    .Marker.gameObject.SetActive(
                        card != null && !card.TargetsEnemies && CanTarget(card, i)
                    );
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
            LayoutAim(start, end, targets.Count > 0);
        }

        /// <summary>
        /// Lays the aim's dots on a bend from <paramref name="start"/> to <paramref name="end"/>
        /// (in the aim's space), white when the finger is <paramref name="onTarget"/>.
        /// </summary>
        private void LayoutAim(Vector2 start, Vector2 end, bool onTarget)
        {
            var bend = new Vector2(start.x, end.y);
            float length = Vector2.Distance(start, bend) + Vector2.Distance(bend, end);
            int count = Mathf.Clamp(Mathf.RoundToInt(length / AimSpacing), 1, AimDots.Length);
            var tint = onTarget ? Color.white : AimIdle;
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
            EndTurnPassives();
            Refresh();
            Enqueue(EnemyPhase());
        }

        /// <summary>
        /// Ends the party's turn by itself when no card in hand can be paid for, whether the
        /// energy is spent or what is left is too little: once the played cards have acted (one
        /// may give energy back) and the cards they drew have landed. Not where the hand is put
        /// away (the labs): no card is played there, and the lab ends the turn by its own button.
        /// </summary>
        private void EndTurnIfSpent()
        {
            if (acting || Dealing || EnemyTurn || AnyAffordable())
                return;
            if (!Hand.gameObject.activeInHierarchy)
                return;
            EndTurn();
        }

        /// <summary>
        /// True when the energy can pay for a card in hand (a lowered cost, or a free next card).
        /// </summary>
        private bool AnyAffordable() => hand.Exists(Affordable);

        private IEnumerator EnemyPhase(List<int> opening = null)
        {
            // The band tells that the enemies' turn has come, and they wait for it, unless none
            // of them acts before the party's next turn. At the battle's start, the enemies
            // faster than the party (opening) act before its first turn.
            openingPhase = opening != null;
            roundOrder = opening ?? EnemiesAfter(BattleTurn);
            bool enemiesAct = roundOrder.Count > 0;
            if (enemiesAct)
            {
                ShowTurnBanner(EnemyTurnText, enemies: true);
                yield return Wait(Settings.TurnBannerHold);
            }
            for (roundStep = 0; roundStep < roundOrder.Count; roundStep++)
            {
                int entry = roundOrder[roundStep];
                if (!Enemies[entry].Alive)
                    continue;
                RefreshTurnOrder();
                yield return Wait(Settings.EnemyGap);
                yield return EnemyAct(entry);
            }
            roundStep = -1;

            // Burns, poison, regen, sigils and clouds work as the party's turn comes round.
            yield return PartyTurnStatuses();

            // The adventure's battle ends here when a side is beaten (see Outcome).
            if (Settle())
                yield break;
            if (!EndsWithOutcome)
            {
                // The mock goes on for ever: a beaten side comes back for the next turn.
                if (Array.TrueForAll(Enemies, enemy => !enemy.Alive))
                    ReviveEnemies();
                if (Array.TrueForAll(Allies, ally => ally.Down))
                    ReviveAllies();
            }
            if (opening != null)
            {
                openingPhase = false;
                EnemyTurn = false;
                DealOpeningHand();
                yield break;
            }
            BeginPartyTurn(again: !enemiesAct);
        }

        /// <summary>
        /// The enemies faster than the party, which act before its first turn: only when the
        /// enemies have speeds (the adventure's battle), the faster first.
        /// </summary>
        public List<int> OpeningEnemies()
        {
            if (!Array.Exists(Enemies, enemy => enemy.Alive && enemy.Speed > 0))
                return new List<int>();
            return CombatTurnOrder.Opening(PartySpeed(), EnemySpeeds());
        }

        // 敵の速度（倒れた敵は0）。速度アップ・凍えを当てる。
        private double[] EnemySpeeds()
        {
            var speeds = new double[Enemies.Length];
            for (int i = 0; i < Enemies.Length; i++)
                speeds[i] = Enemies[i].Alive
                    ? SpeedWithStatuses(false, i, Math.Max(1, Enemies[i].Speed))
                    : 0;
            return speeds;
        }

        /// <summary>The part (percent) a chill takes off the speed (doc/features/combat.md, 凍え).</summary>
        public const int ChillSpeedPercent = 30;

        /// <summary>The part (percent) a speed up adds when the card gives none.</summary>
        public const int SpeedUpPercent = 30;

        /// <summary>A speed with the ally's (or enemy's) speed up and chill on it.</summary>
        public double SpeedWithStatuses(bool ally, int index, double speed)
        {
            var up = Find(ally, index, CardStatus.SpeedUp);
            if (up != null)
                speed *= 1 + (up.Amount > 0 ? up.Amount : SpeedUpPercent) / 100.0;
            if (HasStatus(ally, index, CardStatus.Chill))
                speed *= 1 - ChillSpeedPercent / 100.0;
            return speed;
        }

        /// <summary>
        /// The party's next turn begins: the cards are dealt under the band of the party's turn
        /// (of a turn <paramref name="again"/>, when no enemy acted in between).
        /// </summary>
        private void BeginPartyTurn(bool again)
        {
            EnemyTurn = false;
            // Block carries over, but only half of it, so it does not pile up turn after turn.
            HalveBlock();
            StartOfTurn(Turn + 1);
            dealing = StartCoroutine(Deal(TurnDraw(), 0f));
            // Over the deal, so the party's turn begins with no wait.
            ShowPartyTurnBanner(again);
        }

        /// <summary>
        /// The cards dealt at the start of a turn: <see cref="BattleInspectHandSettings.TurnDraw"/>,
        /// or more when the hand would still hold fewer than
        /// <see cref="BattleInspectHandSettings.RefillTo"/>, to fill it up to that.
        /// </summary>
        public int TurnDraw() => Mathf.Max(Settings.TurnDraw, Settings.RefillTo - hand.Count);

        /// <summary>
        /// The enemies standing that act after the party's turn <paramref name="round"/> (1 for
        /// the battle's first), in order: from their speeds and the party's when they have speeds,
        /// or else from <see cref="TurnCycle"/>, whose runs of enemies follow its party turns in turn.
        /// </summary>
        public List<int> EnemiesAfter(int round)
        {
            if (Array.Exists(Enemies, enemy => enemy.Alive && enemy.Speed > 0))
                return CombatTurnOrder.After(round, PartySpeed(), EnemySpeeds());
            var runs = new List<List<int>>();
            int first = Array.IndexOf(TurnCycle, PartyTurn);
            if (first < 0)
                return new List<int>();
            for (int step = 0; step < TurnCycle.Length; step++)
            {
                int entry = TurnCycle[(first + step) % TurnCycle.Length];
                if (entry == PartyTurn)
                    runs.Add(new List<int>());
                else if (entry >= 0 && entry < Enemies.Length)
                    runs[^1].Add(entry);
            }
            var run = runs[(Mathf.Max(1, round) - 1) % runs.Count];
            return run.FindAll(enemy => Enemies[enemy].Alive);
        }

        /// <summary>
        /// The party's speed in the turn order: the average of the allies standing, each with its
        /// speed up and chill.
        /// </summary>
        public double PartySpeed()
        {
            var speeds = new List<double>();
            for (int i = 0; i < Allies.Length; i++)
                if (!Allies[i].Down)
                    speeds.Add(SpeedWithStatuses(true, i, StatsNow(i).Speed));
            return CombatTurnOrder.PartySpeed(speeds);
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
            if (Frozen(index))
            {
                ShowPopup(enemy.TargetArea, "凍結で動けない", StatusColor(CardStatus.Freeze), 40f);
                yield return Wait(Settings.EnemyGap);
                yield break;
            }
            int target = standing[UnityEngine.Random.Range(0, standing.Count)];
            int taunting = standing.Find(ally => HasStatus(true, ally, CardStatus.Taunt));
            if (HasStatus(true, taunting, CardStatus.Taunt))
                target = taunting;
            target = CoverTarget(target);
            yield return Perform(
                enemy.SkillName,
                new[] { enemy.Body },
                new[] { enemyBase[index] },
                Vector2.left,
                Strike(target, EnemyPower(index), index)
            );
            // A bleeding enemy loses blood as it moves.
            yield return Bleed(index);
        }

        /// <summary>An enemy's blow landing on an ally, with its burst when there are effects.</summary>
        private IEnumerator Strike(int target, int power, int attacker = -1, EnemyMove move = null)
        {
            if (Guarded(target, ref power, attacker))
                yield break;
            // The ally's defense softens the blow: magic defense against an enemy's spell,
            // physical defense against its blow. A skill's spell is one of 属攻.
            bool magic =
                move != null
                    ? move.Stat == CardStat.MagicAttack
                    : attacker >= 0 && Enemies[attacker].Magic;
            if (attacker >= 0 && move is not { IgnoreDefense: true })
                power = CombatFormula.Defend(
                    power,
                    magic ? StatsNow(target).MagicDefense : StatsNow(target).PhysicalDefense,
                    Enemies[attacker].Level
                );
            if (move is { BreakBlock: true } && BlockOf(target) > 0)
            {
                ShowPopup(Allies[target].TargetArea, "ブロックを砕かれた", GuardColor, 36f);
                allyBlock[target] = 0;
                RefreshBlock(target);
            }
            // An enemy's blow may land as a critical hit, by its critical stat (doc/features/combat.md).
            bool critical =
                attacker >= 0
                && BattleOdds.Critical(
                    CombatFormula.CriticalChance(
                        Enemies[attacker].Critical,
                        Enemies[attacker].Level
                    )
                );
            if (critical)
                power = CombatFormula.Critical(power);
            HitAlly(target, power, pastBlock: move is { IgnoreBlock: true }, critical: critical);
            if (Vfx != null)
                Vfx.Strike(
                    Allies[target].TargetArea,
                    move != null ? move.Name
                        : attacker >= 0 ? Enemies[attacker].SkillName
                        : null,
                    attacker >= 0 ? Enemies[attacker].Body : null,
                    magic
                );
            Riposte(target, attacker);
            yield break;
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
                foreach (int deckIndex in order)
                    if (!Benched(deckIndex))
                        drawPile.Add(deckIndex);
            }
            RefreshDeck();
            int opening = OpeningDraw();
            if (instant)
            {
                for (int i = 0; i < opening && Draw(false); i++) { }
                Refresh();
            }
            else
                dealing = StartCoroutine(Deal(opening, Settings.DealDelay));
        }

        /// <summary>Puts every card on screen away, hidden, and shuffles the whole deck anew.</summary>
        private void ClearTable()
        {
            PutBack();
            reserved = 0;
            hand.Clear();
            ClearOmens();
            ResetPassives();
            foreach (var card in Cards)
                card.Place = BattleInspectCardPlace.Deck;
            NoteFallen();
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
            // A fallen ally's cards stay out (Fallen).
            for (int i = 0; i < Deck.Length; i++)
                if (!Benched(i))
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
            // The cards a played card drew may cost nothing; with none, the spent energy ends the turn.
            EndTurnIfSpent();
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
            // Every card left is a fallen ally's.
            if (drawPile.Count == 0)
                return false;
            Show(index, drawPile[0]);
            // A unique skill's card may light up as it comes into the hand, to evolve when used.
            DrawOmen(drawPile[0]);
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
            card.Cost = CostWithPassives(data);
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
                    card.Cost,
                    data.Quality,
                    data.FoilStyle
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
            var turns = new List<int>();
            int round = BattleTurn;
            if (EnemyTurn && roundStep >= 0)
            {
                for (int k = roundStep; k < roundOrder.Count; k++)
                    if (Enemies[roundOrder[k]].Alive)
                        turns.Add(roundOrder[k]);
                // 最初の敵の番のあとは、まだ1つ目の味方の番。
                if (!openingPhase)
                    round++;
            }
            while (turns.Count < count)
            {
                turns.Add(PartyTurn);
                turns.AddRange(EnemiesAfter(round++));
            }
            return turns.GetRange(0, count).ToArray();
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
            StartTurnPassives();
            ShowBoostsTurnedOn();
            Refresh();
        }

        // --- Effects -----------------------------------------------------------------------

        /// <summary>
        /// Pays for the card and takes it out of the hand at once, then queues its action: the
        /// user steps forward (on from where it stepped while the card was up), the effect lands,
        /// and the user steps back.
        /// </summary>
        private void Play(int index, List<int> chosen, bool onEnemies)
        {
            var card = Cards[index];
            Energy -= CostOf(card);
            // The free card is used up by the card that took it.
            nextCardFree = false;
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
            else
                caster = -1;
            // A unique skill's card lit in the hand evolves before its effect (doc/features/skill-evolution.md).
            if (TakeOmen(card.DeckIndex))
            {
                Enqueue(
                    EvolveThenPerform(card.DeckIndex, data, parts, bases, chosen, onEnemies, caster)
                );
                return;
            }
            Enqueue(
                Perform(
                    data.Name,
                    parts,
                    bases,
                    Vector2.right,
                    UseCard(data, chosen, onEnemies, caster),
                    caster
                )
            );
            if (PlaysTwice(data, onEnemies, caster))
                Enqueue(PerformAgain(data, parts, bases, chosen, caster));
        }

        public const string TwiceText = "2回発動";

        // 装備の効果で、攻撃のカードはその確率でもう一度効果を起こす（doc/features/equipment.md#装備の効果）。
        private bool PlaysTwice(BattleInspectCardData data, bool onEnemies, int caster) =>
            onEnemies
            && caster >= 0
            && data.Effect
                is (BattleInspectCardEffect.DamageOne or BattleInspectCardEffect.DamageAll)
            && Allies[caster].TwiceChance > 0
            && UnityEngine.Random.Range(0, 100) < Allies[caster].TwiceChance;

        // 2回目は1回目と同じ相手を狙い、その相手が倒れていれば何も起きない。全体・ランダムの攻撃は残っている敵へ。
        private IEnumerator PerformAgain(
            BattleInspectCardData data,
            RectTransform[] parts,
            Vector2[] bases,
            List<int> chosen,
            int caster
        )
        {
            bool alive = false;
            foreach (var enemy in Enemies)
                alive |= enemy.Alive;
            bool single = data.Effect == BattleInspectCardEffect.DamageOne && chosen.Count > 0;
            if (!alive || (single && !Enemies[chosen[0]].Alive))
                yield break;
            ShowPopup(Allies[caster].TargetArea, TwiceText, CriticalColor, 44f);
            yield return Perform(
                data.Name,
                parts,
                bases,
                Vector2.right,
                UseCard(data, chosen, true, caster),
                caster
            );
        }

        // The card evolves (its omen, the stage picked, its burst), and then the stage it became
        // is played as the card was: the user steps forward and its effect lands.
        private IEnumerator EvolveThenPerform(
            int deckIndex,
            BattleInspectCardData data,
            RectTransform[] parts,
            Vector2[] bases,
            List<int> chosen,
            bool onEnemies,
            int caster
        )
        {
            var played = data;
            yield return Evolve(deckIndex, data, caster, evolved => played = evolved);
            // A stage that works on the other side (a heal become an attack) picks its own target.
            bool side =
                played.Effect
                is BattleInspectCardEffect.DamageOne
                    or BattleInspectCardEffect.DamageAll;
            if (side != onEnemies)
            {
                chosen = new List<int>();
                onEnemies = side;
            }
            yield return Perform(
                played.Name,
                parts,
                bases,
                Vector2.right,
                UseCard(played, chosen, onEnemies, caster),
                caster
            );
        }

        /// <summary>
        /// The card's effect: its skill effect plays from the user to the targets, and each
        /// target takes the effect as the blow lands there. Without effects, all land at once.
        /// </summary>
        private IEnumerator UseCard(
            BattleInspectCardData card,
            List<int> chosen,
            bool onEnemies,
            int caster
        )
        {
            var skill = SkillOf(card);
            if (skill != null)
            {
                yield return UseSkill(skill, card, chosen, onEnemies, caster);
                yield break;
            }
            if (Vfx == null)
            {
                for (int i = 0; i < chosen.Count; i++)
                    Land(card, chosen[i], onEnemies, i == 0);
                Refresh();
                yield break;
            }
            var areas = new List<RectTransform>();
            foreach (int target in chosen)
                areas.Add(onEnemies ? Enemies[target].TargetArea : Allies[target].TargetArea);
            var user = caster >= 0 ? Allies[caster] : null;
            yield return Vfx.Play(
                BattleSkillVfx.KindFor(card.Element, card.Effect),
                card.Name,
                user != null ? user.TargetArea : PartyAnchor,
                user != null ? user.Sprite.texture : null,
                card.Cost >= Vfx.CutInCost,
                areas,
                i => Land(card, chosen[i], onEnemies, i == 0)
            );
            Refresh();
        }

        /// <summary>
        /// The card's effect on one target: damage, healing, or (once for the party) the block.
        /// Tells how hard it hit, so the effect can hit as hard.
        /// </summary>
        private BattleHitWeight Land(
            BattleInspectCardData card,
            int target,
            bool onEnemies,
            bool first
        )
        {
            BattleHitWeight weight = BattleHitWeight.Normal;
            if (onEnemies)
                weight = Hit(target, card.Power, card.Element);
            else if (card.Effect == BattleInspectCardEffect.Heal)
            {
                var ally = Allies[target];
                ally.Hp = Mathf.Min(ally.MaxHp, ally.Hp + card.Power);
                RefreshAlly(ally);
                ShowNumber(HealNumber, ally.TargetArea, card.Power.ToString());
            }
            else if (first)
                ShowPopup(PartyAnchor, $"ブロック +{card.Power}", GuardColor, 48f);
            Refresh();
            return weight;
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
            // A card that beat the last enemy ends the adventure's battle; otherwise the spent
            // energy may end the turn.
            if (!EnemyTurn && !Settle())
                EndTurnIfSpent();
        }

        /// <summary>
        /// One action: the skill's name shows at the top, the character (<paramref name="parts"/>,
        /// resting at <paramref name="bases"/>) steps one step toward <paramref name="forward"/>,
        /// the effect plays and lands, and after a moment the character steps back. An
        /// <paramref name="ally"/> (an index of <see cref="Allies"/>) sets out from where it stands
        /// for the raised card, and is home with no stance when done.
        /// </summary>
        private IEnumerator Perform(
            string skill,
            RectTransform[] parts,
            Vector2[] bases,
            Vector2 forward,
            IEnumerator effect,
            int ally = -1
        )
        {
            // An ally fallen after its card was played does not act.
            if (ally >= 0 && Allies[ally].Down)
                yield break;
            var from = Vector2.zero;
            if (ally >= 0)
            {
                allyBusy[ally]++;
                from = StanceOffset(ally);
            }
            ShowSkillName(skill);
            var front = StepOffset(forward);
            if (from != front)
                yield return Slide(parts, bases, from, front);
            yield return effect;
            yield return Wait(Settings.ActionHold);
            yield return Slide(parts, bases, front, Vector2.zero);
            if (ally >= 0)
            {
                allyStance[ally] = 0f;
                allyBusy[ally]--;
            }
            HideSkillName();
        }

        /// <summary>Moves the parts from one offset to another on an ease-out, in whole dots.</summary>
        private IEnumerator Slide(RectTransform[] parts, Vector2[] bases, Vector2 from, Vector2 to)
        {
            float duration = Settings.StepTime;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = 1f - (1f - t / duration) * (1f - t / duration);
                var offset = ToDots(Vector2.Lerp(from, to, k));
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

        /// <summary>
        /// Shows the turn band with <paramref name="text"/> in the colours of the enemies or the
        /// party, then fades it.
        /// </summary>
        private void ShowTurnBanner(string text, bool enemies) =>
            ShowBand(
                text,
                enemies ? EnemyBannerLine : PartyBannerLine,
                enemies ? EnemyBannerText : PartyBannerText,
                Settings.TurnBannerHold
            );

        /// <summary>The band of the party's turn, or of a turn <paramref name="again"/>.</summary>
        private void ShowPartyTurnBanner(bool again) =>
            ShowTurnBanner(PartyTurnText(Turn, again), enemies: false);

        /// <summary>
        /// Shows the turn band with <paramref name="text"/> in <paramref name="words"/>, between
        /// lines of <paramref name="line"/>, for <paramref name="hold"/> seconds, then fades it.
        /// </summary>
        private void ShowBand(string text, Color line, Color words, float hold)
        {
            if (TurnBanner == null)
                return;
            if (turnBannerShow != null)
                StopCoroutine(turnBannerShow);
            PaintBand(text, line, words);
            SetTurnBanner(0f, 0f);
            turnBannerShow = StartCoroutine(PlayTurnBanner(hold));
        }

        /// <summary>Puts <paramref name="text"/> on the turn band in the colours of the enemies or the party, and shows it.</summary>
        private void PaintTurnBanner(string text, bool enemies) =>
            PaintBand(
                text,
                enemies ? EnemyBannerLine : PartyBannerLine,
                enemies ? EnemyBannerText : PartyBannerText
            );

        private void PaintBand(string text, Color line, Color words)
        {
            TurnBanner.SetText(text);
            if (TurnBanner.Label != null)
                TurnBanner.Label.color = words;
            foreach (var graphic in TurnBannerLines)
                graphic.color = line;
            TurnBanner.gameObject.SetActive(true);
        }

        /// <summary>The band fades in as its lines open from the middle, holds, and fades out.</summary>
        private IEnumerator PlayTurnBanner(float holdFor)
        {
            float fade = Settings.TurnBannerFade;
            float hold = Mathf.Max(fade, holdFor);
            for (float t = 0f; t < hold + fade; t += Time.deltaTime)
            {
                float alpha =
                    t < fade ? t / fade
                    : t < hold ? 1f
                    : 1f - (t - hold) / fade;
                SetTurnBanner(alpha, t / fade);
                yield return null;
            }
            TurnBanner.gameObject.SetActive(false);
            turnBannerShow = null;
        }

        /// <summary>Sets the band's alpha, and how far its lines have opened (0 to 1, eased out).</summary>
        private void SetTurnBanner(float alpha, float open)
        {
            if (TurnBannerGroup != null)
                TurnBannerGroup.alpha = alpha;
            float k = Mathf.Clamp01(open);
            float width = Mathf.Lerp(0.6f, 1f, 1f - (1f - k) * (1f - k));
            foreach (var line in TurnBannerLines)
                line.rectTransform.localScale = new Vector3(width, 1f, 1f);
        }

        /// <summary>
        /// Damages an enemy and tells how hard the blow hit it. A <paramref name="critical"/> hit
        /// shows a larger number under the word for it.
        /// </summary>
        private BattleHitWeight Hit(
            int index,
            int power,
            BattleInspectElement element,
            BattleInspectElement extra = BattleInspectElement.None,
            bool lasting = false,
            bool critical = false
        )
        {
            var enemy = Enemies[index];
            // An earlier card of the queue may have beaten it already.
            if (!enemy.Alive)
                return BattleHitWeight.Normal;

            var weakness =
                element == BattleInspectElement.None
                    ? null
                    : Array.Find(enemy.Weaknesses, entry => entry.Element == element);
            if (weakness == null && extra != BattleInspectElement.None)
                weakness = Array.Find(enemy.Weaknesses, entry => entry.Element == extra);
            bool weak = weakness != null;
            // A weakness found by this blow glints as it turns from ? into its icon.
            if (weak && !weakness.Revealed)
            {
                StartCoroutine(weakness.Reveal(Vfx, () => enemy.Alive));
                OnWeaknessRevealed(enemy, weakness);
            }
            int damage = weak ? Mathf.RoundToInt(power * WeakMultiplier) : power;
            // Its protection and block soften a blow, but not a burn, poison nor bleeding (lasting).
            if (!lasting)
            {
                if (HasStatus(false, index, CardStatus.Protect))
                    damage = Mathf.RoundToInt(damage * (1f - ProtectCut));
                damage = AbsorbEnemy(index, damage);
            }
            enemy.Hp = Mathf.Max(0, enemy.Hp - damage);
            RefreshEnemy(enemy);

            // A weakness shows in the number's colour and size, without words.
            if (critical)
                ShowCritical(enemy.TargetArea);
            var number = ShowNumber(
                weak ? WeakNumber : DamageNumber,
                enemy.TargetArea,
                damage.ToString()
            );
            if (critical && number != null)
                number.fontSize *= CriticalNumberScale;
            StartCoroutine(
                HitReaction(index, element == BattleInspectElement.Ice ? FrozenTint : HitTint)
            );
            return !enemy.Alive ? BattleHitWeight.Defeat
                : weak ? BattleHitWeight.Weak
                : BattleHitWeight.Normal;
        }

        /// <summary>
        /// The enemy blinks white as the blow lands, then reels back in <paramref name="tint"/> (red,
        /// or a cold blue for ice) and trembles, through the hit stop too. A beaten enemy crumbles
        /// away dot by dot (or, with no effects, fades away).
        /// </summary>
        private IEnumerator HitReaction(int index, Color tint)
        {
            var enemy = Enemies[index];
            yield return Blink(enemy.Sprite);
            if (!enemy.Alive)
                lastBeaten = index;
            if (!enemy.Alive && Vfx != null)
            {
                enemy.Sprite.color = Color.white;
                Vfx.Crumble(enemy.Sprite, Vector2.right, enemy.Boss);
                enemy.Group.alpha = 0f;
                RefreshTurnOrder();
                yield break;
            }
            enemy.Sprite.color = tint;
            yield return Shake(enemy.Body, enemyBase[index], 10f, 0.3f, Vector2.right * 24f);
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

        /// <summary>Turns the sprite white for a few frames of real time, so it shows in a hit stop.</summary>
        private IEnumerator Blink(RawImage sprite)
        {
            if (hitFlash == null)
                yield break;
            sprite.material = hitFlash;
            for (float t = 0f; t < 0.06f; t += Time.unscaledDeltaTime)
                yield return null;
            if (sprite.material == hitFlash)
                sprite.material = ActorMaterial;
        }

        private void HitAlly(int index, int damage, bool pastBlock = false, bool critical = false)
        {
            var ally = Allies[index];
            if (!pastBlock)
                damage = Absorb(index, damage);
            ally.Hp = Mathf.Max(0, ally.Hp - damage);
            RefreshAlly(ally);
            // Its cards leave the hand and the deck the moment it falls.
            RefreshFallen();
            ShowBoostsTurnedOn();
            if (critical)
                ShowCritical(ally.TargetArea);
            var number = ShowNumber(DamageNumber, ally.TargetArea, damage.ToString());
            if (critical && number != null)
                number.fontSize *= CriticalNumberScale;
            StartCoroutine(AllyHitReaction(index));
        }

        private IEnumerator AllyHitReaction(int index)
        {
            var ally = Allies[index];
            // The shake starts from home, so any stance is dropped.
            allyBusy[index]++;
            allyStance[index] = 0f;
            ally.HpBar.anchoredPosition = allyBarBase[index];
            yield return Blink(ally.Sprite);
            ally.Sprite.color = HitTint;
            yield return Shake(ally.Body, allyBase[index], 10f, 0.3f, Vector2.left * 24f);
            allyBusy[index]--;
            RefreshAlly(ally);
        }

        private void ReviveAllies()
        {
            ClearBlock();
            foreach (var ally in Allies)
            {
                if (ally.Absent)
                    continue;
                ally.Hp = ally.StartHp;
                RefreshAlly(ally);
            }
        }

        private void ReviveEnemies()
        {
            ClearStatuses(false);
            for (int i = 0; i < Enemies.Length; i++)
            {
                var enemy = Enemies[i];
                enemy.Hp = enemy.MaxHp;
                enemy.Group.alpha = 1f;
                SetEnemyBlock(i, 0);
                StopGathering(i);
                RefreshEnemy(enemy);
            }
        }

        private void Refresh()
        {
            RefreshFallen();
            for (int i = 0; i < Cards.Length; i++)
                RefreshCard(i);
            if (EndTurnButton != null)
                EndTurnButton.interactable = !Dealing && !EnemyTurn && AnyAffordable();
            if (EnergyLabel != null)
                EnergyLabel.text = EnergyText(Energy, MaxEnergy);
            foreach (var enemy in Enemies)
                RefreshEnemy(enemy);
            RefreshTurnOrder();
            RefreshStatusList();
            // A card held up while another plays forecasts from the HP as it is now.
            if (held >= 0)
                ShowForecast(Cards[held]);
        }

        /// <summary>
        /// Whether the energy can pay for the card. A card darkens and sinks only once it has
        /// landed, so the backs fly in at full brightness straight to their place in the fan.
        /// </summary>
        private bool Affordable(int index) => CostOf(Cards[index]) <= Energy;

        private bool Playable(int index) => Affordable(index) || motion[index].Flying;

        /// <summary>Hides cards off the hand and darkens those the energy cannot pay for.</summary>
        private void RefreshCard(int index)
        {
            var card = Cards[index];
            bool playable = Playable(index);
            // A card the energy cannot pay for is see-through in the hand, but not once raised,
            // so its details can still be read.
            float alpha =
                !card.InHand ? 0f
                : playable ? 1f
                : UnplayableAlpha(Settings, index == held);
            // The raised card takes taps too: a second tap on it plays it without a target.
            bool blocks = card.InHand;
            // Set only on a change, as this runs every frame and a set dirties the canvas.
            if (card.Group.alpha != alpha)
                card.Group.alpha = alpha;
            if (card.Group.blocksRaycasts != blocks)
                card.Group.blocksRaycasts = blocks;
            if (card.Face != null)
                card.Face.SetPlayable(playable, VeilDarkness(Settings, alpha));
            // A card that evolves when used lights up once it is face up in the hand.
            ShowGlint(index, card.InHand && !IsFaceDown(index) && HasOmen(card.DeckIndex));
        }

        private static void RefreshEnemy(BattleInspectEnemy enemy)
        {
            enemy.Gauge.SetHp(enemy.Hp, enemy.MaxHp);
        }

        private static void RefreshAlly(BattleInspectAlly ally)
        {
            ally.Gauge.SetHp(ally.Hp, ally.MaxHp);
            // A downed ally is greyed out until healed.
            ally.Sprite.color = ally.Down ? DownTint * ally.Tint : ally.Tint;
        }

        private void ShowPopup(RectTransform anchor, string text, Color color, float size)
        {
            if (PopupTemplate == null || anchor == null)
                return;
            var popup = Spawn(
                PopupTemplate,
                anchor,
                anchor.rect.center,
                text,
                MessageRise,
                size * PopupLineHeight
            );
            popup.color = color;
            popup.fontSize = size;
        }

        private TMP_Text ShowNumber(TMP_Text template, RectTransform anchor, string text)
        {
            if (template == null || anchor == null)
                return null;
            var rect = anchor.rect;
            var start = new Vector2(rect.center.x, rect.yMax - NumberBelowTop);
            return Spawn(
                template,
                anchor,
                start,
                text,
                NumberRise,
                template.fontSize * PopupLineHeight
            );
        }

        /// <summary>The word over a critical hit, shown just before its number.</summary>
        private void ShowCritical(RectTransform anchor) =>
            ShowPopup(anchor, CriticalText, CriticalColor, 44f);

        /// <summary>
        /// Puts a copy of a popup template over <paramref name="anchor"/>, starting at
        /// <paramref name="start"/> in the anchor's space. The characters are on the stage canvas
        /// and the popups on the overlay canvas over it, so the point is carried across in screen
        /// space. Numbers and names over the same character at once line up under one another
        /// where they stop, and stay apart on their way up (<see cref="BattlePopupStack"/>).
        /// </summary>
        private TMP_Text Spawn(
            TMP_Text template,
            RectTransform anchor,
            Vector2 start,
            string text,
            float rise,
            float lineHeight
        )
        {
            var popup = Instantiate(template, template.transform.parent);
            popup.gameObject.SetActive(true);
            popup.text = text;
            var parent = (RectTransform)popup.transform.parent;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                parent,
                ScreenPointOf(anchor, start),
                CameraOf(parent),
                out var world
            );
            popup.transform.position = world;
            var rect = (RectTransform)popup.transform;
            float drop = popupStack.Place(
                anchor,
                rect.anchoredPosition.y + rise,
                lineHeight,
                rise,
                Time.unscaledTime,
                PopupDuration
            );
            rect.anchoredPosition += Vector2.down * drop;
            StartCoroutine(Rise(popup, rise));
            return popup;
        }

        /// <summary>
        /// A popup bursts out large and snaps to its size, jumps up, hangs and fades. It runs on
        /// real time, so it pops even while a hit stop holds the battle.
        /// </summary>
        private static IEnumerator Rise(TMP_Text popup, float distance)
        {
            var rect = (RectTransform)popup.transform;
            var start = rect.anchoredPosition;
            const float duration = PopupDuration;
            const float pop = 0.16f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float rise = Mathf.Min(1f, t / BattlePopupStack.RiseTime);
                rect.anchoredPosition =
                    start + Vector2.up * (distance * (1f - (1f - rise) * (1f - rise)));
                rect.localScale = Vector3.one * PopScale(t / pop);
                popup.alpha = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                yield return null;
            }
            Destroy(popup.gameObject);
        }

        /// <summary>
        /// A number's size as it pops: from 1.8 times down past its size and back to it, over
        /// <paramref name="k"/> from 0 to 1, then its size.
        /// </summary>
        public static float PopScale(float k)
        {
            if (k >= 1f)
                return 1f;
            if (k < 0.6f)
                return Mathf.Lerp(1.8f, 0.9f, k / 0.6f);
            return Mathf.Lerp(0.9f, 1f, (k - 0.6f) / 0.4f);
        }

        /// <summary>
        /// Shakes a character side to side, fading out, after a knock back of <paramref name="kick"/>
        /// that eases home. It runs on real time so the character trembles through a hit stop.
        /// </summary>
        private static IEnumerator Shake(
            RectTransform rect,
            Vector2 basePosition,
            float distance,
            float duration,
            Vector2 kick
        )
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float side = Mathf.Repeat(t * 30f, 2f) < 1f ? 1f : -1f;
                float fade = 1f - t / duration;
                var offset = Vector2.right * (side * distance * fade) + kick * (fade * fade);
                rect.anchoredPosition = basePosition + ToDots(offset);
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
