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
        private const int EnemyAttackUp = 25;

        // The pause between the hits of a skill that lands more than once (each hit is a dash of
        // its own, which spaces them out too).
        private const float EnemyHitGap = 0.04f;

        // How long the word of its enrage or of its gathering shows before it acts.
        private const float EnemyCryHold = 0.55f;

        private static readonly Color ChargeColor = new(1f, 0.62f, 0.3f);
        private static readonly Color ChargeTint = new(1f, 0.8f, 0.55f);
        private static readonly Color EnrageColor = new(1f, 0.42f, 0.36f);

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
            if (enemy.Brain == null)
            {
                yield return EnemyAttack(index);
                yield break;
            }
            if (Array.TrueForAll(Allies, ally => ally.Down))
                yield break;
            if (Frozen(index))
            {
                ShowPopup(enemy.TargetArea, "凍結で動けない", StatusColor(CardStatus.Freeze), 40f);
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
                ShowPopup(enemy.TargetArea, "激化！", EnrageColor, 48f);
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

        /// <summary>The enemy gathers its strength, says how many turns are left, and glows.</summary>
        private IEnumerator Gather(int index, EnemyStep step, EnemyMove with)
        {
            var enemy = Enemies[index];
            if (gathering.Length != Enemies.Length)
                Array.Resize(ref gathering, Enemies.Length);
            gathering[index] = true;
            // Two short lines, so the words stay on screen over the big enemy at the right edge.
            ShowPopup(enemy.TargetArea, "力を溜めている", ChargeColor, 40f);
            ShowPopup(enemy.TargetArea, $"あと{step.ChargeLeft}ターン", ChargeColor, 40f);
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
                var aimed = Aimed(aim);
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
            float power = value * percent / 100f * enemy.AttackScale;
            if (Find(false, index, CardStatus.AttackUp) is { } up)
                power *= 1f + up.Amount / 100f;
            if (enemy.PowerUp > 0)
                power *= 1f + enemy.PowerUp / 100f;
            if (HasStatus(false, index, CardStatus.Paralysis))
                power *= 1f - BattleBlows.ParalysisCut;
            return Mathf.Max(1, Mathf.RoundToInt(power));
        }

        /// <summary>The allies standing that one hit of <paramref name="aim"/> lands on.</summary>
        private List<int> Aimed(EnemyAim aim)
        {
            var standing = new List<int>();
            for (int i = 0; i < Allies.Length; i++)
                if (!Allies[i].Down)
                    standing.Add(i);
            if (standing.Count == 0 || aim == EnemyAim.All)
                return standing;
            int target = Dice.Pick(standing);
            if (aim == EnemyAim.Weakest)
                target = standing.Find(i =>
                    standing.TrueForAll(other =>
                        Share(Allies[i].Hp, Allies[i].MaxHp)
                        <= Share(Allies[other].Hp, Allies[other].MaxHp)
                    )
                );
            else if (aim == EnemyAim.MostBlock && standing.Exists(i => BlockOf(i) > 0))
                target = standing.Find(i =>
                    standing.TrueForAll(other => BlockOf(i) >= BlockOf(other))
                );
            // A taunting ally draws the blows aimed at one, and Aria may take them for a low ally.
            target = BattleBlows.Taunted(
                target,
                standing,
                ally => HasStatus(true, ally, CardStatus.Taunt)
            );
            return new List<int> { CoverTarget(target) };
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
                int block = Mathf.Max(1, stat * move.BlockPercent / 100 * percent / 100);
                foreach (int target in cared)
                {
                    SetEnemyBlock(target, Enemies[target].Block + block);
                    ShowPopup(Enemies[target].TargetArea, $"ブロック +{block}", GuardColor, 40f);
                }
            }
            if (move.HealPercent > 0)
                foreach (int target in cared)
                {
                    int amount =
                        move.HealStat == CardStat.None
                            ? Enemies[target].MaxHp * move.HealPercent / 100
                            : enemy.MagicAttack * move.HealPercent / 100;
                    HealEnemy(target, Mathf.Max(1, amount));
                }
            if (move.RegenTurns > 0)
                foreach (int target in cared)
                    PutOnEnemy(
                        target,
                        CardStatus.Regen,
                        move.RegenTurns,
                        0,
                        Mathf.Max(1, enemy.MagicAttack * move.RegenPercent / 100)
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
                ShowPopup(enemy.TargetArea, "次の攻撃が強くなる", ChargeColor, 36f);
            }
        }

        /// <summary>The enemies standing that a skill of <paramref name="care"/> reaches.</summary>
        private List<int> Cared(int index, EnemyCare care)
        {
            var others = new List<int>();
            for (int i = 0; i < Enemies.Length; i++)
                if (i != index && Enemies[i].Alive)
                    others.Add(i);
            switch (care)
            {
                case EnemyCare.Others:
                    return others.Count > 0 ? others : new List<int> { index };
                case EnemyCare.Everyone:
                    others.Insert(0, index);
                    return others;
                case EnemyCare.WeakestOther:
                    if (others.Count == 0)
                        return new List<int> { index };
                    int weakest = others.Find(i =>
                        others.TrueForAll(other =>
                            Share(Enemies[i].Hp, Enemies[i].MaxHp)
                            <= Share(Enemies[other].Hp, Enemies[other].MaxHp)
                        )
                    );
                    return new List<int> { weakest };
                default:
                    return new List<int> { index };
            }
        }

        /// <summary>Puts a status on an enemy (the longer turns and the stronger power stay) and shows its name.</summary>
        private void PutOnEnemy(int index, CardStatus status, int turns, int amount, int power)
        {
            var active = Find(false, index, status);
            if (active == null)
            {
                active = new ActiveStatus { Status = status, Source = -1 };
                StatusesOf(false, index).Add(active);
            }
            active.Turns = Mathf.Max(active.Turns, turns);
            active.Amount = Mathf.Max(active.Amount, amount);
            active.Power = Mathf.Max(active.Power, power);
            ShowPopup(
                Enemies[index].TargetArea,
                CardRules.StatusName(status),
                StatusColor(status),
                40f
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
            ShowPopup(enemy.TargetArea, $"ブロック -{absorbed}", GuardColor, 36f);
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
                    Tick(CardStatus.Regen, Enemies[i].TargetArea, () => HealEnemy(enemy, regen.Power))
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
