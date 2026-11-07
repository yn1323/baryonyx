using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The enemies' skills in battle (doc/features/combat.md, the enemies' actions): an enemy with
    /// a brain (an adventure's encounter) picks its skill by its pattern and does it — blows on
    /// one ally, the weakest, every ally or allies at random, block, healing, regen, attack up,
    /// protection and the power gathered for its next blow. A big skill is gathered for a turn or
    /// two first: the enemy says so and glows warm until it lands. From its enrage on, and in the
    /// second part of a boss's battle, its skills change as its pattern says. Its block lasts
    /// until its next action. An enemy without a brain (the mock's) keeps its one attack.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        // Attack up on an enemy is the cards' 25%.
        private const int EnemyAttackUp = EnemyActs.AttackUpPercent;

        // The pause between the hits of a skill that lands more than once (each hit is a dash of
        // its own, which spaces them out too).
        private const float EnemyHitGap = 0.04f;

        // How long the enemy holds after its enrage shakes it or as it gathers, before it goes on.
        private const float EnemyCryHold = 0.55f;

        private static readonly Color ChargeTint = new(1f, 0.8f, 0.55f);

        // The tint each enemy's glow put on its sprite last, so a hit's tint is not painted over.
        private Color[] chargeShown = Array.Empty<Color>();

        // The enemies gathering their strength for a big skill, until it lands.
        private bool[] gathering = Array.Empty<bool>();

        /// <summary>The block an enemy holds now.</summary>
        public int EnemyBlockOf(int enemy) =>
            enemy >= 0 && enemy < Enemies.Length ? Enemies[enemy].Block : 0;

        /// <summary>True while the enemy gathers its strength for a big skill.</summary>
        public bool IsCharging(int enemy) =>
            enemy >= 0 && enemy < gathering.Length && gathering[enemy];

        private void StopGathering(int enemy)
        {
            if (enemy >= 0 && enemy < gathering.Length)
                gathering[enemy] = false;
        }

        /// <summary>The enemy's turn: the skill its brain picks, or the mock's one attack.</summary>
        private IEnumerator EnemyAct(int index)
        {
            var enemy = Enemies[index];
            // 次の自分の行動まで持つ効果（すり抜け・身かわし）は、行動の始めに消える。
            effects.OwnerActs(EnemyAt(index));
            if (enemy.Brain == null)
            {
                yield return EnemyAttack(index);
                yield break;
            }
            if (Array.TrueForAll(Allies, ally => ally.Down))
                yield break;
            if (Frozen(index))
            {
                ShowStatusNote(enemy.TargetArea, FrozenText, StatusColor(CardStatus.Freeze));
                yield return Wait(Settings.EnemyGap);
                yield break;
            }
            // The block it put up lasts until it acts again.
            SetEnemyBlock(index, 0);
            for (int n = 0; n < Mathf.Max(1, enemy.Brain.Pattern.ActionsPerTurn); n++)
            {
                if (!enemy.Alive || Array.TrueForAll(Allies, ally => ally.Down))
                    break;
                var step = enemy.Brain.Next(Situation(index));
                yield return Carry(index, step);
                // Gathering takes the rest of its turn, so the big skill never lands in the same one.
                if (step.Charging)
                    break;
            }
            // A bleeding enemy loses blood as it moves.
            yield return Bleed(index);
        }

        /// <summary>What the enemy sees: the turn, its HP, the others' and the party's.</summary>
        private EnemySituation Situation(int index)
        {
            var enemy = Enemies[index];
            bool others = false;
            float othersLowest = 1f;
            for (int i = 0; i < Enemies.Length; i++)
                if (i != index && Enemies[i].Alive)
                {
                    others = true;
                    othersLowest = Mathf.Min(othersLowest, Share(Enemies[i].Hp, Enemies[i].MaxHp));
                }
            float partyLowest = 1f;
            bool guarded = false;
            for (int i = 0; i < Allies.Length; i++)
                if (!Allies[i].Down)
                {
                    partyLowest = Mathf.Min(partyLowest, Share(Allies[i].Hp, Allies[i].MaxHp));
                    guarded |= BlockOf(i) > 0;
                }
            return new EnemySituation(
                BattleTurn,
                Share(enemy.Hp, enemy.MaxHp),
                others,
                othersLowest,
                partyLowest,
                guarded
            );
        }

        private static float Share(int hp, int max) => max > 0 ? hp / (float)max : 0f;

        /// <summary>One action of the enemy: its cry when it changes, then gathering or its skill.</summary>
        private IEnumerator Carry(int index, EnemyStep step)
        {
            var enemy = Enemies[index];
            if (step.Move == null)
            {
                yield return EnemyAttack(index);
                yield break;
            }
            if (step.Awakens || step.Enrages)
            {
                yield return Shake(enemy.Body, enemyBase[index], 6f, 0.3f, Vector2.right * 12f);
                yield return Wait(EnemyCryHold);
            }
            if (step.Charging)
            {
                var with =
                    step.Move.ChargeWith != null
                        ? enemy.Brain.Behavior.Move(step.Move.ChargeWith)
                        : null;
                yield return Perform(
                    with != null && with.Supported ? with.Name : step.Move.Name,
                    new[] { enemy.Body },
                    new[] { enemyBase[index] },
                    Vector2.left,
                    Gather(index, step, with),
                    hold: EnemyActionHold
                );
                yield break;
            }
            yield return Perform(
                step.Move.Name,
                new[] { enemy.Body },
                new[] { enemyBase[index] },
                Vector2.left,
                UseMove(index, step),
                hold: EnemyActionHold
            );
        }

        /// <summary>The enemy gathers its strength and glows; the attack up icon shows it too.</summary>
        private IEnumerator Gather(int index, EnemyStep step, EnemyMove with)
        {
            var enemy = Enemies[index];
            if (gathering.Length != Enemies.Length)
                Array.Resize(ref gathering, Enemies.Length);
            gathering[index] = true;
            if (with != null && with.Supported)
                Support(index, with);
            yield return Wait(EnemyCryHold);
        }

        /// <summary>The skill lands: its blows on the party, then its block, healing and boosts.</summary>
        private IEnumerator UseMove(int index, EnemyStep step)
        {
            var enemy = Enemies[index];
            var move = step.Move;
            if (move.ChargeTurns > 0)
                StopGathering(index);
            if (move.Strikes)
                yield return Blows(index, move, step);
            if (enemy.Alive)
                Support(index, move, step.Percent);
            if (enemy.Alive && move.Effect != null)
                UseMoveEffect(index, move.Effect);
            Refresh();
        }

        private IEnumerator Blows(int index, EnemyMove move, EnemyStep step)
        {
            var enemy = Enemies[index];
            int percent = move.Percent * step.Percent / 100;
            var aim = move.Aim;
            int spread = enemy.Brain.Pattern.SpreadPercent;
            if (spread > 0 && aim is EnemyAim.One or EnemyAim.Weakest or EnemyAim.MostBlock)
            {
                aim = EnemyAim.All;
                percent = percent * spread / 100;
            }
            int power = BlowPower(index, move.Stat, percent);
            // The power gathered by 力を溜める goes into this skill.
            enemy.PowerUp = 0;
            int splash =
                move.SplashPercent > 0
                    ? BlowPower(index, move.Stat, move.SplashPercent * step.Percent / 100)
                    : 0;
            int before = PartyHp();
            int hits = Mathf.Max(1, move.Hits + step.ExtraHits);
            for (int hit = 0; hit < hits; hit++)
            {
                if (hit > 0)
                    yield return Wait(EnemyHitGap);
                var aimed = Aimed(aim, index);
                if (aimed.Count == 0)
                    break;
                // It dashes at the one it aims at (the middle one of several) and strikes.
                yield return Lunge(
                    index,
                    aimed[aimed.Count / 2],
                    move.Stat == CardStat.MagicAttack
                );
                foreach (int target in aimed)
                    yield return Strike(target, power, index, move);
                if (splash > 0 && aimed.Count == 1)
                    for (int i = 0; i < Allies.Length; i++)
                        if (i != aimed[0] && !Allies[i].Down)
                            yield return Strike(i, splash, index, move);
                yield return LungeBack(index);
                // A blow thrown back (反射, 切り返し) may beat it before its next hit.
                if (!enemy.Alive)
                    break;
            }
            int dealt = before - PartyHp();
            if (move.DrainPercent > 0 && dealt > 0 && enemy.Alive)
                HealEnemy(index, Mathf.Max(1, dealt * move.DrainPercent / 100));
            if (move.SelfCostPercent > 0 && enemy.Alive)
            {
                int cost = Mathf.Max(1, enemy.MaxHp * move.SelfCostPercent / 100);
                enemy.Hp = Mathf.Max(1, enemy.Hp - cost);
                RefreshEnemy(enemy);
                ShowNumber(DamageNumber, enemy.TargetArea, cost.ToString());
            }
        }

        private int PartyHp()
        {
            int total = 0;
            foreach (var ally in Allies)
                total += ally.Hp;
            return total;
        }

        /// <summary>
        /// The power of an enemy's blow: <paramref name="percent"/> of its stat, grown by its room,
        /// its attack up and the power it gathered, and weakened while it is paralysed.
        /// </summary>
        private int BlowPower(int index, CardStat stat, int percent)
        {
            var enemy = Enemies[index];
            int value = stat switch
            {
                CardStat.MagicAttack => enemy.MagicAttack,
                CardStat.PhysicalDefense => enemy.PhysicalDefense,
                _ => enemy.PhysicalAttack,
            };
            return EnemyActs.BlowPower(
                value,
                percent,
                enemy.AttackScale,
                Find(false, index, CardStatus.AttackUp)?.Amount,
                enemy.PowerUp,
                HasStatus(false, index, CardStatus.Paralysis)
            );
        }

        /// <summary>The allies standing that one hit of <paramref name="aim"/> lands on.</summary>
        private List<int> Aimed(EnemyAim aim, int attacker)
        {
            var standing = new List<int>();
            for (int i = 0; i < Allies.Length; i++)
                if (!Allies[i].Down)
                    standing.Add(i);
            var aimed = EnemyActs.Aimed(
                aim,
                standing,
                Dice.Below,
                i => Share(Allies[i].Hp, Allies[i].MaxHp),
                BlockOf,
                ally => HasStatus(true, ally, CardStatus.Taunt)
            );
            // Aria may take a blow aimed at one for a low ally (かばう).
            if (aim != EnemyAim.All && aimed.Count == 1)
                aimed[0] = TargetOf(aimed[0], attacker);
            return aimed;
        }

        /// <summary>
        /// The block, healing and boosts of the skill, on those it cares for; its block grows with
        /// <paramref name="percent"/> while it is enraged.
        /// </summary>
        private void Support(int index, EnemyMove move, int percent = 100)
        {
            var enemy = Enemies[index];
            var cared = Cared(index, move.Care);
            if (move.BlockPercent > 0)
            {
                int stat =
                    move.BlockStat == CardStat.MagicAttack
                        ? enemy.MagicAttack
                        : enemy.PhysicalDefense;
                int block = EnemyActs.GuardBlock(stat, move.BlockPercent, percent);
                foreach (int target in cared)
                    SetEnemyBlock(target, Enemies[target].Block + block);
            }
            if (move.HealPercent > 0)
                foreach (int target in cared)
                {
                    HealEnemy(
                        target,
                        EnemyActs.Mend(move, Enemies[target].MaxHp, enemy.MagicAttack)
                    );
                }
            if (move.RegenTurns > 0)
                foreach (int target in cared)
                    PutOnEnemy(
                        target,
                        CardStatus.Regen,
                        move.RegenTurns,
                        0,
                        EnemyActs.RegenPower(move, enemy.MagicAttack)
                    );
            if (move.AttackUpTurns > 0)
                foreach (int target in cared)
                    PutOnEnemy(target, CardStatus.AttackUp, move.AttackUpTurns, EnemyAttackUp, 0);
            if (move.ProtectTurns > 0)
                foreach (int target in cared)
                    PutOnEnemy(target, CardStatus.Protect, move.ProtectTurns, 0, 0);
            if (move.PowerUpPercent > 0)
            {
                enemy.PowerUp = Mathf.Max(enemy.PowerUp, move.PowerUpPercent);
                // The attack up icon shows it, with the note a status gets as it is put on.
                ShowStatusNote(
                    enemy.TargetArea,
                    CardRules.StatusName(CardStatus.AttackUp),
                    StatusColor(CardStatus.AttackUp)
                );
            }
        }

        /// <summary>The enemies standing that a skill of <paramref name="care"/> reaches.</summary>
        private List<int> Cared(int index, EnemyCare care)
        {
            var shares = new float[Enemies.Length];
            for (int i = 0; i < shares.Length; i++)
                shares[i] = Enemies[i].Alive ? Share(Enemies[i].Hp, Enemies[i].MaxHp) : -1f;
            return EnemyActs.Cared(index, care, shares);
        }

        /// <summary>Puts a status on an enemy (the longer turns and the stronger power stay) and shows its name.</summary>
        private void PutOnEnemy(int index, CardStatus status, int turns, int amount, int power)
        {
            var active = Find(false, index, status);
            if (active == null)
            {
                active = new BattleStatusEntry { Status = status, Source = -1 };
                StatusesOf(false, index).Add(active);
            }
            active.Turns = Mathf.Max(active.Turns, turns);
            active.Amount = Mathf.Max(active.Amount, amount);
            active.Power = Mathf.Max(active.Power, power);
            ShowStatusNote(
                Enemies[index].TargetArea,
                CardRules.StatusName(status),
                StatusColor(status)
            );
        }

        private void HealEnemy(int index, int amount)
        {
            var enemy = Enemies[index];
            if (!enemy.Alive)
                return;
            enemy.Hp = Mathf.Min(enemy.MaxHp, enemy.Hp + amount);
            RefreshEnemy(enemy);
            ShowNumber(HealNumber, enemy.TargetArea, amount.ToString());
        }

        /// <summary>Sets an enemy's block, up to its max HP, and shows it over its HP bar.</summary>
        private void SetEnemyBlock(int index, int block)
        {
            var enemy = Enemies[index];
            enemy.Block = Mathf.Clamp(block, 0, enemy.MaxHp);
            if (enemy.Gauge != null)
                enemy.Gauge.SetBlock(enemy.Block, enemy.MaxHp);
        }

        /// <summary>The damage left after an enemy's block takes what it can.</summary>
        private int AbsorbEnemy(int index, int damage)
        {
            var enemy = Enemies[index];
            if (enemy.Block <= 0 || damage <= 0)
                return damage;
            int absorbed = BattleBlows.Soaked(enemy.Block, damage);
            SetEnemyBlock(index, enemy.Block - absorbed);
            return damage - absorbed;
        }

        /// <summary>
        /// Regen heals the enemies as the party's turn comes round: their ticks, to be played
        /// together with the party's.
        /// </summary>
        private List<IEnumerator> EnemyRegen()
        {
            var ticks = new List<IEnumerator>();
            for (int i = 0; i < Enemies.Length; i++)
            {
                var regen = Find(false, i, CardStatus.Regen);
                if (regen == null || !Enemies[i].Alive)
                    continue;
                int enemy = i;
                ticks.Add(
                    Tick(
                        CardStatus.Regen,
                        Enemies[i].TargetArea,
                        () => HealEnemy(enemy, regen.Power)
                    )
                );
            }
            return ticks;
        }

        /// <summary>A gathering enemy glows warm, unless a hit's tint is on it.</summary>
        private void RefreshChargeGlow(float time)
        {
            if (chargeShown.Length != Enemies.Length)
            {
                chargeShown = new Color[Enemies.Length];
                for (int i = 0; i < chargeShown.Length; i++)
                    chargeShown[i] = Color.white;
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 6f);
            for (int i = 0; i < Enemies.Length; i++)
            {
                var sprite = Enemies[i].Sprite;
                if (sprite == null || sprite.color != chargeShown[i])
                    continue;
                var tint =
                    IsCharging(i) && Enemies[i].Alive
                        ? Color.Lerp(Color.white, ChargeTint, pulse)
                        : Color.white;
                sprite.color = tint;
                chargeShown[i] = tint;
            }
        }
    }
}
