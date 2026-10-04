using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The enemy lab's way into the mock battle (<see cref="EnemyLab"/>): one enemy behaviour at a
    /// time, on demand, through the battle's own code (its attack, its reactions to blows, the
    /// statuses that change its turn, the enemies' turn), so the lab shows the enemies as the
    /// battle does. Each runs in the battle's action queue, so <see cref="Acting"/> holds while
    /// it plays.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        // A lab blow takes a tenth of the enemy's HP, so a few in a row still leave it standing.
        private const float LabBlowPart = 0.1f;

        // Long enough for a blow's white blink, red shake and number, or a beaten enemy's burst.
        private const float LabReactionTime = 1f;

        // How long a status's name shows over the character before the enemy acts.
        private const float LabStatusHold = 0.6f;

        /// <summary>The damage of the lab's blow on the enemy, before its weakness.</summary>
        public int LabBlowPower(int enemy) =>
            Mathf.Max(1, Mathf.RoundToInt(Enemies[enemy].MaxHp * LabBlowPart));

        /// <summary>The statuses the character has now, in the order they were put on.</summary>
        public List<CardStatus> StatusesNow(bool ally, int index) =>
            StatusesOf(ally, index).ConvertAll(active => active.Status);

        /// <summary>
        /// Plays <paramref name="action"/> of the enemy at <paramref name="enemy"/> after any
        /// action under way. A beaten enemy stands up again first, and so does a beaten party,
        /// so there is always someone to act and someone to hit. The enemies' turn is the
        /// battle's own (<see cref="EndTurn"/>), and is refused while cards are being dealt.
        /// </summary>
        public void LabPlay(int enemy, EnemyLabAction action)
        {
            if (action == EnemyLabAction.EnemyTurn)
            {
                EndTurn();
                return;
            }
            if (enemy < 0 || enemy >= Enemies.Length)
                return;
            Enqueue(LabRun(enemy, action));
        }

        /// <summary>
        /// Puts the battlefield back as it began: every enemy and ally stands with its starting HP,
        /// no status or block is left, and the weaknesses are hidden or shown as at the start.
        /// </summary>
        public void LabRestore()
        {
            ClearStatuses(true);
            for (int i = 0; i < Enemies.Length; i++)
            {
                LabReviveEnemy(i);
                foreach (var weakness in Enemies[i].Weaknesses)
                    weakness.Show(weakness.StartsRevealed);
            }
            ReviveAllies();
            EnsureBlock();
            Array.Clear(allyBlock, 0, allyBlock.Length);
            Refresh();
        }

        private IEnumerator LabRun(int enemy, EnemyLabAction action)
        {
            if (!Enemies[enemy].Alive)
                LabReviveEnemy(enemy);
            if (Array.TrueForAll(Allies, ally => ally.Down))
                ReviveAllies();
            switch (action)
            {
                case EnemyLabAction.Attack:
                    yield return EnemyAttack(enemy);
                    break;
                case EnemyLabAction.Hit:
                    yield return LabBlow(enemy, BattleInspectElement.None, LabBlowPower(enemy));
                    break;
                case EnemyLabAction.WeakHit:
                    yield return LabBlow(enemy, LabWeakness(enemy), LabBlowPower(enemy));
                    break;
                case EnemyLabAction.Defeat:
                    yield return LabBlow(enemy, BattleInspectElement.None, Enemies[enemy].Hp);
                    break;
                case EnemyLabAction.Freeze:
                    yield return LabAfflicted(enemy, CardStatus.Freeze);
                    break;
                case EnemyLabAction.Paralysis:
                    yield return LabAfflicted(enemy, CardStatus.Paralysis);
                    break;
                case EnemyLabAction.Bleed:
                    yield return LabAfflicted(enemy, CardStatus.Bleed);
                    break;
                case EnemyLabAction.Reflect:
                    yield return LabReflected(enemy);
                    break;
            }
            Refresh();
        }

        /// <summary>A blow on the enemy as a card's would land, and its reaction played out.</summary>
        private IEnumerator LabBlow(int enemy, BattleInspectElement element, int power)
        {
            Hit(enemy, power, element);
            yield return Wait(LabReactionTime);
        }

        /// <summary>
        /// The element of the enemy's first weakness still hidden, so the blow reveals it with its
        /// glint, or of its first weakness when all are known.
        /// </summary>
        private BattleInspectElement LabWeakness(int enemy)
        {
            var weaknesses = Enemies[enemy].Weaknesses;
            var hidden = Array.Find(weaknesses, weakness => !weakness.Revealed);
            if (hidden != null)
                return hidden.Element;
            return weaknesses.Length > 0 ? weaknesses[0].Element : BattleInspectElement.None;
        }

        /// <summary>The enemy takes the status as a card puts it on, then takes its turn.</summary>
        private IEnumerator LabAfflicted(int enemy, CardStatus status)
        {
            var (action, caster) = LabCard(status);
            AddStatus(false, enemy, action, caster);
            yield return Wait(LabStatusHold);
            yield return EnemyAttack(enemy);
        }

        /// <summary>
        /// Every ally standing holds up a mirror, so whoever the enemy picks throws the blow back
        /// at it. The mirrors the blow did not reach are taken down after, so the next attack is a
        /// plain one again.
        /// </summary>
        private IEnumerator LabReflected(int enemy)
        {
            var (action, _) = LabCard(CardStatus.Reflect);
            for (int i = 0; i < Allies.Length; i++)
                if (!Allies[i].Down)
                    AddStatus(true, i, action, i);
            yield return Wait(LabStatusHold);
            yield return EnemyAttack(enemy);
            for (int i = 0; i < Allies.Length; i++)
                TakeStatus(true, i, CardStatus.Reflect);
        }

        /// <summary>
        /// The card skill's action that puts on <paramref name="status"/> (its turns and power),
        /// and the ally taken as its user: the one strongest in the stat the status works from,
        /// as the lab does not know who played it.
        /// </summary>
        private (CardAction action, int caster) LabCard(CardStatus status)
        {
            CardAction found = null;
            foreach (var skill in CardSkills.All)
            foreach (var action in skill.Actions)
                if (
                    found == null
                    && action.Kind == CardActionKind.Status
                    && action.Status == status
                )
                    found = action;
            found ??= CardAction.Apply(CardTarget.OneEnemy, status, 1);

            var stat = found.HasPower ? found.Stat : CardRules.TickOf(status).stat;
            int caster = 0;
            for (int i = 1; i < Allies.Length; i++)
                if (StatOf(i, stat) > StatOf(caster, stat))
                    caster = i;
            return (found, caster);
        }

        /// <summary>The enemy stands again with its full HP and no status.</summary>
        private void LabReviveEnemy(int index)
        {
            var enemy = Enemies[index];
            StatusesOf(false, index).Clear();
            enemy.Hp = enemy.MaxHp;
            enemy.Group.alpha = 1f;
            enemy.Sprite.color = Color.white;
            RefreshEnemy(enemy);
            RefreshTurnOrder();
        }
    }
}
