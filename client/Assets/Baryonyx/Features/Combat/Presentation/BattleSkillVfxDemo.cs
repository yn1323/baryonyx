using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Plays every card skill's effect in turn (<see cref="CardSkills"/>), over and over, for the
    /// showcase: the effects only show in the battle while cards are played, so this lets them be
    /// watched without playing. Each card is played by its user on its kind of target; a card
    /// whose status works later (a burn, poison, a wound, regen, a sigil, a cloud) shows that too.
    /// Every other round the blows land on a weakness, to show the harder hit.
    /// </summary>
    public sealed class BattleSkillVfxDemo : MonoBehaviour
    {
        public BattleSkillVfx Vfx;

        /// <summary>The party's bodies in the order of <see cref="CardUser"/> (Aria, Toma, Luka, Mina).</summary>
        public RectTransform[] Party = new RectTransform[0];

        public RectTransform[] Enemies = new RectTransform[0];

        /// <summary>The characters the effects light up.</summary>
        public RawImage[] Actors = new RawImage[0];

        /// <summary>Shows which card plays now ("12/50 メテオ").</summary>
        public TMP_Text Caption;

        [Tooltip("1つのスキルが終わってから次のスキルまでの間（秒）。")]
        [Min(0f)]
        public float Gap = 0.9f;

        [Tooltip("最初に再生するカードの番号（1から50）。")]
        [Min(1)]
        public int First = 1;

        [Tooltip(
            "カードのID（例：Meteor）を入れると、そのカードだけを繰り返す。空ならすべてを順に再生する。"
        )]
        public string Only = "";

        private IEnumerator Start()
        {
            if (Vfx == null || Party.Length == 0 || Enemies.Length == 0)
                yield break;
            foreach (var actor in Actors)
                Vfx.RegisterActor(actor);
            var all = CardSkills.All;
            int next = Mathf.Clamp(First - 1, 0, all.Count - 1);
            for (int round = 0; ; round++)
            {
                for (int n = 0; n < all.Count; n++)
                {
                    var skill = all[(next + n) % all.Count];
                    if (!string.IsNullOrEmpty(Only) && skill.Id != Only)
                        continue;
                    if (Caption != null)
                        Caption.text = $"{IndexOf(skill) + 1}/{all.Count}　{skill.Name}";
                    var weight = round % 2 == 1 ? BattleHitWeight.Weak : BattleHitWeight.Normal;
                    yield return PlaySkill(Vfx, Party, Enemies, Actors, skill, n, weight);
                    for (float t = 0f; t < Gap; t += Time.deltaTime)
                        yield return null;
                }
            }
        }

        private static int IndexOf(CardSkill skill)
        {
            var all = CardSkills.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] == skill)
                    return i;
            return -1;
        }

        /// <summary>
        /// Plays one card skill as the battle does: from its user in <paramref name="party"/>
        /// (the bodies in <see cref="CardUser"/> order) on its kind of target, the
        /// <paramref name="enemy"/>th enemy (counted round) for a card played on one enemy and the
        /// next ally along for one played on an ally, and then what it leaves to work later (a
        /// burn's tick, a sigil's blast, a cloud's bolt). Every blow lands with
        /// <paramref name="weight"/>. The user's picture in the cut-in is the texture of
        /// <paramref name="actors"/> at the user's place.
        /// </summary>
        public static IEnumerator PlaySkill(
            BattleSkillVfx vfx,
            RectTransform[] party,
            RectTransform[] enemies,
            RawImage[] actors,
            CardSkill skill,
            int enemy,
            BattleHitWeight weight
        )
        {
            int user = Mathf.Clamp((int)skill.User, 0, party.Length - 1);
            var effect = BattleCardText.EffectOf(skill);
            bool onEnemies =
                effect is BattleInspectCardEffect.DamageOne or BattleInspectCardEffect.DamageAll;
            int aimed = ((enemy % enemies.Length) + enemies.Length) % enemies.Length;
            int picked = onEnemies ? aimed : (user + 1) % party.Length;
            var enemyIndices = new List<int>();
            for (int i = 0; i < enemies.Length; i++)
                enemyIndices.Add(i);
            var allyIndices = new List<int>();
            for (int i = 0; i < party.Length; i++)
                allyIndices.Add(i);
            var plan = BattleCardPlan.Plan(
                skill,
                enemyIndices,
                allyIndices,
                picked,
                onEnemies,
                user,
                count => Random.Range(0, count)
            );
            var hits = new List<BattleSkillHit>();
            foreach (var hit in plan)
                hits.Add(
                    new BattleSkillHit(
                        hit.Ally ? party[hit.Index] : enemies[hit.Index],
                        hit.Ally,
                        hit.Wave
                    )
                );
            var art = user < actors.Length && actors[user] != null ? actors[user].texture : null;
            yield return vfx.PlayCard(
                skill.Id,
                skill.Name,
                party[user],
                art,
                skill.Cost >= vfx.CutInCost,
                hits,
                _ => weight
            );

            // What the card leaves to work later.
            var target =
                plan.Count > 0
                    ? (plan[0].Ally ? party[plan[0].Index] : enemies[plan[0].Index])
                    : null;
            foreach (var action in skill.Actions)
            {
                if (action.Kind != CardActionKind.Status)
                    continue;
                switch (action.Status)
                {
                    case CardStatus.Burn:
                    case CardStatus.Poison:
                    case CardStatus.Bleed:
                    case CardStatus.Regen:
                        yield return Pause(0.4f);
                        yield return vfx.StatusTick(action.Status, target, null);
                        break;
                    case CardStatus.BlastSigil:
                        yield return Pause(0.5f);
                        yield return vfx.Detonate(target, enemies, _ => weight);
                        break;
                    case CardStatus.Thundercloud:
                        yield return Pause(0.4f);
                        yield return vfx.CloudStrike(enemies[aimed], () => weight);
                        break;
                }
            }
        }

        private static IEnumerator Pause(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
                yield return null;
        }
    }
}
