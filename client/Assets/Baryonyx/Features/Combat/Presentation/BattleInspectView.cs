using System;
using System.Collections;
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
        DamageOne,
        DamageAll,
        Heal,
        Guard,
    }

    [Serializable]
    public sealed class BattleInspectCard
    {
        public Button Button;
        public RectTransform Body;
        public CanvasGroup Group;
        public GameObject Highlight;
        public int Cost;
        public int Power;
        public BattleInspectElement Element;
        public BattleInspectCardEffect Effect;

        [NonSerialized]
        public bool Used;
    }

    [Serializable]
    public sealed class BattleInspectEnemy
    {
        public Button Button;
        public RectTransform Body;
        public RectTransform TargetArea;
        public RawImage Sprite;
        public RectTransform HpFill;
        public TMP_Text HpLabel;
        public CanvasGroup Group;
        public int MaxHp;
        public BattleInspectElement Weakness;

        /// <summary>The face crop of <see cref="Sprite"/> shown in the turn order.</summary>
        public Rect IconUv;

        [NonSerialized]
        public int Hp;

        public bool Alive => Hp > 0;
    }

    /// <summary>One box of the turn order: the whole party, or one enemy.</summary>
    [Serializable]
    public sealed class BattleInspectTurnSlot
    {
        public LayoutElement Layout;
        public GameObject Party;
        public RawImage Enemy;
        public GameObject Now;
    }

    /// <summary>
    /// Mock of the card battle screen for checking the look and feel. It has no battle rules:
    /// tapping a card and then an enemy only plays the hit reaction and lowers the shown HP, and
    /// "End turn" refills the energy and the hand. Enemies never act.
    /// </summary>
    public sealed class BattleInspectView : MonoBehaviour
    {
        // One dot of the 4x pixel art; idle motion moves in whole dots.
        private const float Dot = 4f;
        private const float CardLift = 32f;
        private const float WeakMultiplier = 1.5f;
        private const int MaxEnergyCap = 9;

        private static readonly Color DamageColor = new(1f, 0.96f, 0.9f);
        private static readonly Color WeakColor = new(1f, 0.82f, 0.25f);
        private static readonly Color HealColor = new(0.55f, 0.95f, 0.55f);
        private static readonly Color GuardColor = new(0.55f, 0.75f, 1f);
        private static readonly Color HitTint = new(1f, 0.45f, 0.4f);

        public BattleInspectCard[] Cards = Array.Empty<BattleInspectCard>();
        public BattleInspectEnemy[] Enemies = Array.Empty<BattleInspectEnemy>();

        /// <summary>Containers of the party and enemy sprites that sway while idle.</summary>
        public RectTransform[] IdleActors = Array.Empty<RectTransform>();

        /// <summary>
        /// The repeating turn order. <see cref="PartyTurn"/> is a party turn (the party acts as
        /// one side), other values are enemy indexes. A party entry twice in a row is the
        /// consecutive turn that a large speed gap gives.
        /// </summary>
        public int[] TurnCycle = Array.Empty<int>();

        public BattleInspectTurnSlot[] TurnSlots = Array.Empty<BattleInspectTurnSlot>();
        public float PartySlotWidth = 176f;
        public float EnemySlotWidth = 72f;

        public RectTransform PartyAnchor;
        public RectTransform TargetFrame;
        public TMP_Text PopupTemplate;
        public TMP_Text EnergyLabel;
        public TMP_Text TurnLabel;
        public Button EndTurnButton;

        [Min(1)]
        public int StartTurn = 3;

        private Vector2[] idleBase = Array.Empty<Vector2>();
        private Vector2[] cardBase = Array.Empty<Vector2>();
        private Vector2[] enemyBase = Array.Empty<Vector2>();
        private int selected = -1;
        private int cycleIndex;

        public const int PartyTurn = -1;

        public int Turn { get; private set; }
        public int Energy { get; private set; }
        public int MaxEnergy { get; private set; }
        public int SelectedCard => selected;
        public int Target { get; private set; }

        private void Awake()
        {
            idleBase = new Vector2[IdleActors.Length];
            for (int i = 0; i < IdleActors.Length; i++)
                idleBase[i] = IdleActors[i].anchoredPosition;

            cardBase = new Vector2[Cards.Length];
            for (int i = 0; i < Cards.Length; i++)
            {
                int index = i;
                cardBase[i] = Cards[i].Body.anchoredPosition;
                Cards[i].Button.onClick.AddListener(() => TapCard(index));
            }

            enemyBase = new Vector2[Enemies.Length];
            for (int i = 0; i < Enemies.Length; i++)
            {
                int index = i;
                enemyBase[i] = Enemies[i].Body.anchoredPosition;
                Enemies[i].Button.onClick.AddListener(() => TapEnemy(index));
                Enemies[i].Hp = Enemies[i].MaxHp;
            }

            if (EndTurnButton != null)
                EndTurnButton.onClick.AddListener(EndTurn);
            if (PopupTemplate != null)
                PopupTemplate.gameObject.SetActive(false);

            StartOfTurn(StartTurn);
            SetTarget(Target);
        }

        private void Update()
        {
            // A two-step idle bob of one dot, like the breathing of 16-bit battle sprites.
            float time = Time.time;
            for (int i = 0; i < IdleActors.Length; i++)
            {
                bool up = Mathf.Repeat(time * 0.9f + i * 0.37f, 1f) < 0.5f;
                IdleActors[i].anchoredPosition =
                    idleBase[i] + (up ? Vector2.up * Dot : Vector2.zero);
            }
        }

        public void TapCard(int index)
        {
            var card = Cards[index];
            if (card.Used)
                return;

            if (selected == index)
            {
                // A second tap plays cards that need no enemy target.
                if (card.Effect != BattleInspectCardEffect.DamageOne)
                    Play(index, Target);
                else
                    Select(-1);
                return;
            }

            if (card.Cost > Energy)
            {
                StartCoroutine(Shake(card.Body, cardBase[index], 6f, 0.2f));
                ShowPopup(card.Body, "エネルギー不足", GuardColor, 34f);
                return;
            }
            Select(index);
        }

        public void TapEnemy(int index)
        {
            if (!Enemies[index].Alive)
                return;
            SetTarget(index);
            if (selected >= 0)
                Play(selected, index);
        }

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
                if (slot.Now != null)
                    slot.Now.SetActive(i == 0);
            }
        }

        private void StartOfTurn(int turn)
        {
            // Energy starts at 3 and its maximum grows by one each turn, up to 9.
            Turn = turn;
            MaxEnergy = Mathf.Min(MaxEnergyCap, 3 + turn - 1);
            Energy = MaxEnergy;
            foreach (var card in Cards)
                card.Used = false;
            Select(-1);
            if (TurnLabel != null)
                TurnLabel.text = $"ターン {Turn}";
        }

        private void Select(int index)
        {
            selected = index;
            Refresh();
        }

        private void Play(int index, int target)
        {
            var card = Cards[index];
            Energy -= card.Cost;
            card.Used = true;
            selected = -1;

            switch (card.Effect)
            {
                case BattleInspectCardEffect.DamageOne:
                    Hit(target, card);
                    break;
                case BattleInspectCardEffect.DamageAll:
                    for (int i = 0; i < Enemies.Length; i++)
                        Hit(i, card);
                    break;
                case BattleInspectCardEffect.Heal:
                    ShowPopup(PartyAnchor, $"+{card.Power}", HealColor, 64f);
                    break;
                case BattleInspectCardEffect.Guard:
                    ShowPopup(PartyAnchor, $"ブロック +{card.Power}", GuardColor, 48f);
                    break;
            }
            Refresh();
        }

        private void Hit(int index, BattleInspectCard card)
        {
            var enemy = Enemies[index];
            if (!enemy.Alive)
                return;

            bool weak = card.Element != BattleInspectElement.None && card.Element == enemy.Weakness;
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
            enemy.Button.interactable = false;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                enemy.Group.alpha = 1f - t / 0.5f;
                yield return null;
            }
            enemy.Group.alpha = 0f;
            RefreshTurnOrder();
            if (Target == index)
                SetTarget(NextAlive(index));
        }

        private void ReviveEnemies()
        {
            foreach (var enemy in Enemies)
            {
                enemy.Hp = enemy.MaxHp;
                enemy.Group.alpha = 1f;
                enemy.Button.interactable = true;
                RefreshEnemy(enemy);
            }
            SetTarget(0);
        }

        private int NextAlive(int from)
        {
            for (int step = 1; step <= Enemies.Length; step++)
            {
                int index = (from + step) % Enemies.Length;
                if (Enemies[index].Alive)
                    return index;
            }
            return -1;
        }

        private void SetTarget(int index)
        {
            Target = index;
            if (TargetFrame == null)
                return;
            bool visible = index >= 0 && index < Enemies.Length && Enemies[index].Alive;
            TargetFrame.gameObject.SetActive(visible);
            if (!visible)
                return;
            TargetFrame.SetParent(Enemies[index].TargetArea, false);
            TargetFrame.anchorMin = Vector2.zero;
            TargetFrame.anchorMax = Vector2.one;
            TargetFrame.offsetMin = TargetFrame.offsetMax = Vector2.zero;
        }

        private void Refresh()
        {
            for (int i = 0; i < Cards.Length; i++)
            {
                var card = Cards[i];
                bool isSelected = i == selected;
                card.Body.anchoredPosition =
                    cardBase[i] + (isSelected ? Vector2.up * CardLift : Vector2.zero);
                card.Highlight.SetActive(isSelected);
                card.Group.alpha =
                    card.Used ? 0f
                    : card.Cost <= Energy ? 1f
                    : 0.5f;
                card.Group.blocksRaycasts = !card.Used;
            }
            if (EnergyLabel != null)
                EnergyLabel.text = $"{Energy}/{MaxEnergy}";
            foreach (var enemy in Enemies)
                RefreshEnemy(enemy);
            RefreshTurnOrder();
        }

        private static void RefreshEnemy(BattleInspectEnemy enemy)
        {
            float ratio = enemy.MaxHp > 0 ? enemy.Hp / (float)enemy.MaxHp : 0f;
            enemy.HpFill.anchorMax = new Vector2(ratio, 1f);
            enemy.HpLabel.text = $"{enemy.Hp}/{enemy.MaxHp}";
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
    }
}
