using System;
using System.Collections;
using System.Collections.Generic;
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

    [Serializable]
    public sealed class BattleInspectCard
    {
        public RectTransform Body;
        public CanvasGroup Group;
        public int Cost;
        public int Power;
        public BattleInspectElement Element;
        public BattleInspectCardEffect Effect;

        [NonSerialized]
        public bool Used;

        public bool TargetsEnemies =>
            Effect is BattleInspectCardEffect.DamageOne or BattleInspectCardEffect.DamageAll;

        /// <summary>Cards for a whole side need only a swipe, not an exact target.</summary>
        public bool TargetsWholeSide =>
            Effect is BattleInspectCardEffect.DamageAll or BattleInspectCardEffect.Guard;
    }

    [Serializable]
    public sealed class BattleInspectAlly
    {
        public RawImage Sprite;
        public RectTransform TargetArea;
        public RectTransform HpFill;
        public int StartHp;
        public int MaxHp;

        [NonSerialized]
        public int Hp;
    }

    [Serializable]
    public sealed class BattleInspectEnemy
    {
        public RectTransform Body;
        public RectTransform TargetArea;
        public RawImage Sprite;
        public RectTransform HpFill;
        public CanvasGroup Group;
        public int MaxHp;
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

    /// <summary>One box of the turn order: the whole party, or one enemy.</summary>
    [Serializable]
    public sealed class BattleInspectTurnSlot
    {
        public LayoutElement Layout;
        public GameObject Party;
        public RawImage Enemy;
    }

    /// <summary>
    /// Mock of the card battle screen for checking the look and feel. It has no battle rules.
    /// The hand is fanned below the screen with only each card's name and art showing. Pressing
    /// a card springs it up to show its details; swiping it up and dropping it on an enemy or an
    /// ally plays it there (cards for a whole side need only the swipe). While the card is up,
    /// the characters it would hit glow white. "End turn" refills the energy and the hand.
    /// Enemies never act. Hitting a hidden weakness reveals it.
    /// </summary>
    public sealed class BattleInspectView : MonoBehaviour
    {
        // One dot of the 4x pixel art; idle motion moves in whole dots.
        private const float Dot = 4f;
        private const float WeakMultiplier = 1.5f;
        private const int MaxEnergyCap = 9;

        private static readonly Color DamageColor = new(1f, 0.96f, 0.9f);
        private static readonly Color WeakColor = new(1f, 0.82f, 0.25f);
        private static readonly Color HealColor = new(0.55f, 0.95f, 0.55f);
        private static readonly Color GuardColor = new(0.55f, 0.75f, 1f);
        private static readonly Color HitTint = new(1f, 0.45f, 0.4f);

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

        public BattleInspectTurnSlot[] TurnSlots = Array.Empty<BattleInspectTurnSlot>();
        public float PartySlotWidth = 72f;
        public float EnemySlotWidth = 72f;

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
        public TMP_Text EnergyLabel;
        public Button EndTurnButton;

        /// <summary>Material that turns a sprite white; the view pulses a copy of it.</summary>
        public Material TargetFlash;

        [Min(1)]
        public int StartTurn = 3;

        private readonly List<int> targets = new();
        private Vector2[] idleBase = Array.Empty<Vector2>();
        private Vector2[] enemyBase = Array.Empty<Vector2>();
        private CardMotion[] motion = Array.Empty<CardMotion>();
        private Material flash;
        private int held = -1;
        private bool armed;
        private Vector2 pressPoint;
        private int cycleIndex;

        public const int PartyTurn = -1;

        public int Turn { get; private set; }
        public int Energy { get; private set; }
        public int MaxEnergy { get; private set; }

        /// <summary>The card being pressed, or -1.</summary>
        public int HeldCard => held;

        /// <summary>True while the held card is swiped up far enough to be played on release.</summary>
        public bool Armed => armed;

        /// <summary>Indexes of the enemies or allies (see <see cref="TargetsEnemies"/>) that glow.</summary>
        public IReadOnlyList<int> Targets => targets;

        public bool TargetsEnemies => held >= 0 && Cards[held].TargetsEnemies;

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
            for (int i = 0; i < Cards.Length; i++)
            {
                var body = Cards[i].Body;
                UnityEditor.Undo.RecordObject(body, "Apply hand layout");
                var (position, angle) = FanPose(
                    i,
                    Cards.Length,
                    Settings.FanStep,
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
                };
            }

            enemyBase = new Vector2[Enemies.Length];
            for (int i = 0; i < Enemies.Length; i++)
            {
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
            if (PopupTemplate != null)
                PopupTemplate.gameObject.SetActive(false);

            StartOfTurn(StartTurn);
        }

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
                var pose = motion[i].Current;
                var body = Cards[i].Body;
                body.anchoredPosition = pose.Position;
                body.localRotation = Quaternion.Euler(0, 0, pose.Angle);
                body.localScale = Vector3.one * (pose.Scale * Settings.CardScale);
            }

            if (flash != null)
                flash.SetFloat("_FlashAmount", 0.5f + 0.2f * Mathf.Sin(time * 9f));
        }

        // --- Card input -----------------------------------------------------------------

        /// <summary>
        /// Presses a card at a screen point: it springs up and grows by the settings' RaisedScale
        /// to show its details.
        /// </summary>
        public void PressCard(int index, Vector2 screenPoint)
        {
            if (held >= 0 || Cards[index].Used)
                return;
            held = index;
            armed = false;
            pressPoint = ToHand(screenPoint);
            var card = Cards[index];
            card.Body.SetAsLastSibling();
            motion[index].Target = RaisedPose(index);
            ClearTargets();
        }

        /// <summary>Follows the finger of the pressed card and picks the characters it would hit.</summary>
        public void DragCard(Vector2 screenPoint)
        {
            if (held < 0)
                return;
            armed = ToHand(screenPoint).y - pressPoint.y >= Settings.ArmDistance;
            FindTargets(Cards[held], screenPoint);
        }

        /// <summary>Lets go: plays the card on its targets when armed, otherwise returns it.</summary>
        public void ReleaseCard(Vector2 screenPoint)
        {
            if (held < 0)
                return;
            DragCard(screenPoint);
            int index = held;
            var card = Cards[index];
            bool play = armed && targets.Count > 0;
            if (play && card.Cost > Energy)
            {
                ShowPopup(PartyAnchor, "エネルギー不足", GuardColor, 40f);
                play = false;
            }

            var chosen = new List<int>(targets);
            bool onEnemies = card.TargetsEnemies;
            held = -1;
            armed = false;
            ClearTargets();
            card.Body.SetSiblingIndex(index);
            motion[index].Target = motion[index].Rest;
            if (play)
                Play(index, chosen, onEnemies);
        }

        /// <summary>
        /// Lays the cards still in hand out as a fan centred on the screen, so a played card
        /// leaves no gap: the others spring together toward the middle.
        /// </summary>
        private void LayoutHand()
        {
            int count = 0;
            foreach (var card in Cards)
                if (!card.Used)
                    count++;
            int slot = 0;
            for (int i = 0; i < Cards.Length; i++)
            {
                if (Cards[i].Used)
                    continue;
                var (position, angle) = FanPose(
                    slot++,
                    count,
                    Settings.FanStep,
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
            // Straight up from its place in the fan, kept inside the screen.
            var area = ((RectTransform)Hand.parent).rect;
            float scale = Settings.RaisedScale * Settings.CardScale;
            float half = Cards[index].Body.rect.width * 0.5f * scale;
            float limit = Mathf.Max(0f, area.width * 0.5f - half - 16f);
            float x = Mathf.Clamp(motion[index].Rest.Position.x, -limit, limit);
            return new Pose(
                new Vector2(x, Settings.RaisedBottom - Hand.anchoredPosition.y),
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
            if (armed)
            {
                var camera = EventCamera();
                int count = card.TargetsEnemies ? Enemies.Length : Allies.Length;
                int nearest = -1;
                float best = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    if (card.TargetsEnemies && !Enemies[i].Alive)
                        continue;
                    if (card.TargetsWholeSide)
                    {
                        targets.Add(i);
                        continue;
                    }
                    var area = card.TargetsEnemies ? Enemies[i].TargetArea : Allies[i].TargetArea;
                    float distance = DropDistance(area, screenPoint, camera);
                    if (distance < best)
                    {
                        best = distance;
                        nearest = i;
                    }
                }
                if (nearest >= 0)
                    targets.Add(nearest);
            }
            ApplyGlow(card.TargetsEnemies);
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
                Enemies[i].Sprite.material = onEnemies && targets.Contains(i) ? flash : null;
            for (int i = 0; i < Allies.Length; i++)
                Allies[i].Sprite.material = !onEnemies && targets.Contains(i) ? flash : null;
        }

        /// <summary>True when the sprite is glowing as a target of the held card.</summary>
        public bool IsGlowing(RawImage sprite) => flash != null && sprite.material == flash;

        // --- Turn --------------------------------------------------------------------------

        public void EndTurn()
        {
            if (Array.TrueForAll(Enemies, enemy => !enemy.Alive))
                ReviveEnemies();
            // Enemies never act in this mock: their entries are passed over to the next party turn.
            cycleIndex = NextPartyEntry(cycleIndex);
            StartOfTurn(Turn + 1);
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
            foreach (var card in Cards)
                card.Used = false;
            LayoutHand();
            for (int i = 0; i < Cards.Length; i++)
                motion[i].Snap(motion[i].Rest);
            Refresh();
        }

        // --- Effects -----------------------------------------------------------------------

        private void Play(int index, List<int> chosen, bool onEnemies)
        {
            var card = Cards[index];
            Energy -= card.Cost;
            card.Used = true;

            if (onEnemies)
            {
                foreach (int target in chosen)
                    Hit(target, card);
            }
            else if (card.Effect == BattleInspectCardEffect.Heal)
            {
                foreach (int target in chosen)
                {
                    var ally = Allies[target];
                    ally.Hp = Mathf.Min(ally.MaxHp, ally.Hp + card.Power);
                    RefreshAlly(ally);
                    ShowPopup(ally.TargetArea, $"+{card.Power}", HealColor, 64f);
                }
            }
            else
            {
                ShowPopup(PartyAnchor, $"ブロック +{card.Power}", GuardColor, 48f);
            }
            LayoutHand();
            Refresh();
        }

        private void Hit(int index, BattleInspectCard card)
        {
            var enemy = Enemies[index];
            if (!enemy.Alive)
                return;

            var weakness =
                card.Element == BattleInspectElement.None
                    ? null
                    : Array.Find(enemy.Weaknesses, entry => entry.Element == card.Element);
            bool weak = weakness != null;
            weakness?.Show(true);
            int damage = weak ? Mathf.RoundToInt(card.Power * WeakMultiplier) : card.Power;
            enemy.Hp = Mathf.Max(0, enemy.Hp - damage);
            RefreshEnemy(enemy);

            ShowPopup(
                enemy.TargetArea,
                weak ? $"弱点！ {damage}" : damage.ToString(),
                weak ? WeakColor : DamageColor,
                weak ? 76f : 60f
            );
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
            foreach (var card in Cards)
            {
                card.Group.alpha =
                    card.Used ? 0f
                    : card.Cost <= Energy ? 1f
                    : 0.5f;
                card.Group.blocksRaycasts = !card.Used;
            }
            if (EnergyLabel != null)
                EnergyLabel.text = EnergyText(Energy, MaxEnergy);
            foreach (var enemy in Enemies)
                RefreshEnemy(enemy);
            RefreshTurnOrder();
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
        }

        private void ShowPopup(RectTransform anchor, string text, Color color, float size)
        {
            if (PopupTemplate == null || anchor == null)
                return;
            var popup = Instantiate(PopupTemplate, PopupTemplate.transform.parent);
            popup.gameObject.SetActive(true);
            popup.text = text;
            popup.color = color;
            popup.fontSize = size;
            popup.transform.position = anchor.TransformPoint(anchor.rect.center);
            StartCoroutine(Rise(popup));
        }

        private static IEnumerator Rise(TMP_Text popup)
        {
            var rect = (RectTransform)popup.transform;
            var start = rect.anchoredPosition;
            const float duration = 0.9f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                // Jump up quickly, then hang and fade.
                rect.anchoredPosition = start + Vector2.up * (90f * (1f - (1f - k) * (1f - k)));
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

        /// <summary>A card's rest pose in the fan and a spring that carries it to its target.</summary>
        private struct CardMotion
        {
            public Pose Rest;
            public Pose Current;
            public Pose Target;
            private Vector2 velocity;
            private float angularVelocity;
            private float scaleVelocity;

            public void Snap(Pose pose)
            {
                Current = Target = pose;
                velocity = Vector2.zero;
                angularVelocity = scaleVelocity = 0f;
            }

            // An under-damped spring, so a card overshoots a little and settles ("boing").
            public void Step(float dt, float stiffness, float damping)
            {
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
