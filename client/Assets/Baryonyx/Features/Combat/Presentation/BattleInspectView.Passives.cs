using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The party's passive skills in battle (provisional: doc/features/passives.md). Each works
    /// by itself while its holder stands, and its name pops up over the holder when it does.
    /// The battle counts what they need: the energy carried to the next turn, what has been used
    /// this turn, the ice and thunder cards Ritsu has watched, and the damage the card in play
    /// has dealt.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        private static readonly Color PassiveColor = new(0.92f, 0.82f, 1f);

        // Each place's passives (an index of Allies); empty for the baked mock's party.
        private CombatPassiveKind[][] allyPassives = Array.Empty<CombatPassiveKind[]>();

        private int carriedEnergy;
        private bool coverUsed;
        private bool iaiUsed;
        private bool killEnergyUsed;
        private bool vowUsed;
        private int copycatSeen;
        private bool standingAlone;

        // The card being played: its extra damage (居合の型), the damage it dealt (魔力の盾) and
        // the passives that have shown their names for it already.
        private int cardBonusPercent;
        private int cardDealt;
        private readonly HashSet<(int, CombatPassiveKind)> shownForCard = new();

        /// <summary>The energy 星図の計算 carries into the next party turn.</summary>
        public int CarriedEnergy => carriedEnergy;

        /// <summary>How many ice or thunder cards of the others 見よう見まね has watched this battle.</summary>
        public int CopycatSeen => copycatSeen;

        /// <summary>True when the ally at <paramref name="place"/> stands and has <paramref name="kind"/>.</summary>
        public bool HasPassive(int place, CombatPassiveKind kind) =>
            place >= 0
            && place < Allies.Length
            && place < allyPassives.Length
            && !Allies[place].Down
            && Array.IndexOf(allyPassives[place], kind) >= 0;

        /// <summary>The place of a standing ally with <paramref name="kind"/>, or -1.</summary>
        public int PassiveHolder(CombatPassiveKind kind)
        {
            for (int i = 0; i < Allies.Length; i++)
                if (HasPassive(i, kind))
                    return i;
            return -1;
        }

        /// <summary>Sets each place's passives (in place order) and starts their counts over.</summary>
        private void UsePassives(IReadOnlyList<CombatPassiveKind>[] byPlace)
        {
            allyPassives = new CombatPassiveKind[Allies.Length][];
            for (int i = 0; i < Allies.Length; i++)
            {
                var kinds = i < byPlace.Length ? byPlace[i] : null;
                allyPassives[i] = kinds == null ? Array.Empty<CombatPassiveKind>() : ToArray(kinds);
            }
            ResetPassives();
        }

        private static CombatPassiveKind[] ToArray(IReadOnlyList<CombatPassiveKind> kinds)
        {
            var array = new CombatPassiveKind[kinds.Count];
            for (int i = 0; i < array.Length; i++)
                array[i] = kinds[i];
            return array;
        }

        /// <summary>The battle's counts start over (its cards are dealt anew).</summary>
        private void ResetPassives()
        {
            carriedEnergy = 0;
            copycatSeen = 0;
            standingAlone = false;
            ResetTurnPassives();
        }

        private void ResetTurnPassives()
        {
            coverUsed = iaiUsed = killEnergyUsed = vowUsed = false;
        }

        /// <summary>Pops the passive's name up over its holder.</summary>
        private void ShowPassive(int place, CombatPassiveKind kind, string detail = null)
        {
            var passive = CombatPassives.Find(kind);
            if (passive == null || place < 0 || place >= Allies.Length)
                return;
            string text = detail == null ? passive.Name : $"{passive.Name} {detail}";
            ShowPopup(Allies[place].TargetArea, text, PassiveColor, 36f);
        }

        /// <summary>Pops the name up once for the card being played.</summary>
        private void ShowPassiveForCard(int place, CombatPassiveKind kind)
        {
            if (shownForCard.Add((place, kind)))
                ShowPassive(place, kind);
        }

        // --- Turns and the hand ---------------------------------------------------------------

        /// <summary>The opening hand, with 早駆け's card.</summary>
        private int OpeningDraw()
        {
            int holder = PassiveHolder(CombatPassiveKind.HeadStart);
            if (holder < 0)
                return Settings.OpeningDraw;
            ShowPassive(holder, CombatPassiveKind.HeadStart);
            return Settings.OpeningDraw + CombatPassiveRules.HeadStartCards;
        }

        /// <summary>The turn's energy with what 星図の計算 carried over, and the turn's passives anew.</summary>
        private void StartTurnPassives()
        {
            ResetTurnPassives();
            shownForCard.Clear();
            if (carriedEnergy <= 0)
                return;
            Energy = MaxEnergy + carriedEnergy;
            carriedEnergy = 0;
        }

        /// <summary>星図の計算 keeps some of the energy left as the party's turn ends.</summary>
        private void EndTurnPassives()
        {
            int holder = PassiveHolder(CombatPassiveKind.StarChart);
            carriedEnergy = holder < 0 ? 0 : CombatPassiveRules.Carry(Energy);
            if (carriedEnergy > 0)
                ShowPassive(holder, CombatPassiveKind.StarChart, $"+{carriedEnergy}");
        }

        /// <summary>A card's cost on screen: 独り立ち lowers Ritsu's ice and thunder cards.</summary>
        private int CostWithPassives(BattleInspectCardData data)
        {
            if (
                standingAlone
                && HasPassive(data.Caster, CombatPassiveKind.StandingAlone)
                && CombatPassiveRules.IsCopycatElement(ElementOf(data))
            )
                return Mathf.Max(0, data.Cost - CombatPassiveRules.StandingAloneCostDown);
            return data.Cost;
        }

        private static CardElement ElementOf(BattleInspectCardData data) =>
            SkillOf(data)?.Element ?? CardElement.None;

        // --- A card being played -------------------------------------------------------------

        /// <summary>
        /// Before a card plays: 見よう見まね watches it, 居合の型 sharpens the turn's first slash,
        /// and 連ね矢 adds a hit. Returns the card as it plays.
        /// </summary>
        private CardSkill BeginCardPassives(CardSkill skill, int caster)
        {
            shownForCard.Clear();
            cardBonusPercent = 0;
            cardDealt = 0;
            WatchForCopycat(skill, caster);
            int iai = PassiveHolder(CombatPassiveKind.IaiForm);
            if (iai >= 0 && !iaiUsed && skill.Element == CardElement.Slash && Deals(skill))
            {
                iaiUsed = true;
                cardBonusPercent += CombatPassiveRules.IaiFormPercent;
                ShowPassive(iai, CombatPassiveKind.IaiForm);
            }
            if (
                HasPassive(caster, CombatPassiveKind.ChainedArrows)
                && CombatPassiveRules.HitsMoreThanOnce(skill)
            )
            {
                ShowPassive(caster, CombatPassiveKind.ChainedArrows);
                return CombatPassiveRules.WithExtraHit(skill);
            }
            return skill;
        }

        /// <summary>After a card plays: 魔力の盾 turns part of its thunder into block.</summary>
        private void EndCardPassives(CardSkill skill, int caster)
        {
            if (
                skill.Element == CardElement.Thunder
                && cardDealt > 0
                && HasPassive(caster, CombatPassiveKind.ManaShield)
            )
            {
                int block = AddBlock(caster, CombatPassiveRules.ManaShieldBlock(cardDealt));
                if (block > 0)
                    ShowPassive(caster, CombatPassiveKind.ManaShield, $"ブロック +{block}");
            }
            cardBonusPercent = 0;
            cardDealt = 0;
        }

        private static bool Deals(CardSkill skill)
        {
            foreach (var action in skill.Actions)
                if (action.Kind == CardActionKind.Damage)
                    return true;
            return false;
        }

        private void WatchForCopycat(CardSkill skill, int caster)
        {
            int ritsu = PassiveHolder(CombatPassiveKind.Copycat);
            if (
                ritsu < 0
                || ritsu == caster
                || !CombatPassiveRules.IsCopycatElement(skill.Element)
                || CombatPassiveRules.CopycatFull(copycatSeen)
            )
                return;
            copycatSeen++;
            ShowPassive(
                ritsu,
                CombatPassiveKind.Copycat,
                $"+{CombatPassiveRules.CopycatPercent(copycatSeen)}%"
            );
            if (
                CombatPassiveRules.CopycatFull(copycatSeen)
                && HasPassive(ritsu, CombatPassiveKind.StandingAlone)
            )
                BeginStandingAlone(ritsu);
        }

        /// <summary>独り立ち: Ritsu's ice and thunder cards in hand cost less from now on.</summary>
        private void BeginStandingAlone(int ritsu)
        {
            standingAlone = true;
            ShowPassive(ritsu, CombatPassiveKind.StandingAlone);
            foreach (int index in hand)
            {
                var card = Cards[index];
                card.Cost = CostWithPassives(Deck[card.DeckIndex]);
                if (card.Face != null)
                    card.Face.SetCost(card.Cost);
            }
            Refresh();
        }

        /// <summary>
        /// A card's power on an enemy grown by the passives: the card's own bonus (居合の型),
        /// 寒暖の理 on a burning and chilled enemy, 急所狙い on a wounded one and 見よう見まね.
        /// </summary>
        private int CardPowerWithPassives(int power, CardSkill skill, int enemy, int caster)
        {
            int percent = cardBonusPercent + ThermalPercent(enemy);
            if (HasPassive(caster, CombatPassiveKind.MarkTheWeak) && Wounded(enemy))
            {
                percent += CombatPassiveRules.MarkTheWeakPercent;
                ShowPassiveForCard(caster, CombatPassiveKind.MarkTheWeak);
            }
            if (
                HasPassive(caster, CombatPassiveKind.Copycat)
                && CombatPassiveRules.IsCopycatElement(skill.Element)
                && copycatSeen > 0
            )
                percent += CombatPassiveRules.CopycatPercent(copycatSeen);
            return CombatPassiveRules.Grow(power, percent);
        }

        /// <summary>寒暖の理's extra damage on an enemy both burning and chilled, in percent.</summary>
        private int ThermalPercent(int enemy)
        {
            int toma = PassiveHolder(CombatPassiveKind.ThermalShock);
            if (
                toma < 0
                || !HasStatus(false, enemy, CardStatus.Burn)
                || !HasStatus(false, enemy, CardStatus.Chill)
            )
                return 0;
            ShowPassiveForCard(toma, CombatPassiveKind.ThermalShock);
            return CombatPassiveRules.ThermalShockPercent;
        }

        private bool Wounded(int enemy) => Enemies[enemy].Hp * 2 <= Enemies[enemy].MaxHp;

        /// <summary>
        /// After a card's blow on an enemy: 影の一刺し on a kill, 聖火の鉄槌's burn after a blunt
        /// blow and 傷口を開く on a bleeding enemy. Counts the damage for 魔力の盾.
        /// </summary>
        private void AfterCardBlow(CardSkill skill, int enemy, int caster, int dealt)
        {
            cardDealt += dealt;
            if (
                !Enemies[enemy].Alive
                && !killEnergyUsed
                && HasPassive(caster, CombatPassiveKind.SilentKill)
            )
            {
                killEnergyUsed = true;
                Energy = Mathf.Max(Energy, Mathf.Min(MaxEnergyCap, Energy + 1));
                ShowPassive(caster, CombatPassiveKind.SilentKill, "エネルギー +1");
            }
            if (
                skill.Element == CardElement.Blunt
                && Enemies[enemy].Alive
                && HasPassive(caster, CombatPassiveKind.HolyFlameHammer)
                && UnityEngine.Random.Range(0, 100) < CombatPassiveRules.HolyFlameChance
            )
            {
                ShowPassiveForCard(caster, CombatPassiveKind.HolyFlameHammer);
                AddStatus(
                    false,
                    enemy,
                    CardAction.Apply(
                        CardTarget.OneEnemy,
                        CardStatus.Burn,
                        CombatPassiveRules.HolyFlameTurns
                    ),
                    caster
                );
            }
            OpenWounds(enemy, caster);
        }

        /// <summary>傷口を開く: Rita's blow on a bleeding enemy makes it bleed once more.</summary>
        private void OpenWounds(int enemy, int rita)
        {
            var bleed = Find(false, enemy, CardStatus.Bleed);
            if (
                bleed == null
                || !Enemies[enemy].Alive
                || !HasPassive(rita, CombatPassiveKind.OpenWounds)
            )
                return;
            ShowPassiveForCard(rita, CombatPassiveKind.OpenWounds);
            Hit(enemy, bleed.Power, BattleInspectElement.None);
        }

        // --- Healing and block ---------------------------------------------------------------

        /// <summary>歩み手の加護: Mina's healing grows with today's ACT.</summary>
        private int HealWithPassives(int amount, int healer)
        {
            if (!HasPassive(healer, CombatPassiveKind.WalkersBlessing))
                return amount;
            return CombatPassiveRules.Grow(
                amount,
                CombatPassiveRules.WalkersBonusPercent(TodayAct)
            );
        }

        /// <summary>Heals an ally; あふれる祈り turns what goes past the max HP into its block.</summary>
        private void HealFrom(int target, int amount, int healer)
        {
            var ally = Allies[target];
            int overflow = CombatPassiveRules.Overflow(ally.Hp, ally.MaxHp, amount);
            HealAlly(target, amount);
            if (overflow <= 0 || !HasPassive(healer, CombatPassiveKind.OverflowingPrayer))
                return;
            int gained = AddBlock(target, overflow);
            if (gained > 0)
                ShowPassive(target, CombatPassiveKind.OverflowingPrayer, $"ブロック +{gained}");
        }

        /// <summary>帯電の盾: block on Lutz strikes an enemy at random with a bolt.</summary>
        private void ChargedShield(int lutz)
        {
            if (!HasPassive(lutz, CombatPassiveKind.ChargedShield))
                return;
            var alive = new List<int>();
            for (int i = 0; i < Enemies.Length; i++)
                if (Enemies[i].Alive)
                    alive.Add(i);
            if (alive.Count == 0)
                return;
            int enemy = alive[UnityEngine.Random.Range(0, alive.Count)];
            var ally = Allies[lutz];
            int power = CombatPassiveRules.Percent(
                ally.Stats.MagicAttack,
                CombatPassiveRules.ChargedShieldPercent
            );
            power = CombatPassiveRules.Grow(power, ThermalPercent(enemy));
            power = CombatFormula.Defend(power, Enemies[enemy].MagicDefense, ally.Level);
            ShowPassive(lutz, CombatPassiveKind.ChargedShield);
            BattleHitWeight Land() => Hit(enemy, power, BattleInspectElement.Thunder);
            if (Vfx == null)
                Land();
            else
                StartCoroutine(Vfx.CloudStrike(Enemies[enemy].TargetArea, Land));
        }

        // --- The enemies' blows --------------------------------------------------------------

        /// <summary>かばう: Aria takes a blow aimed at a low ally, once a turn.</summary>
        private int CoverTarget(int target)
        {
            int aria = PassiveHolder(CombatPassiveKind.Cover);
            if (aria < 0)
                return target;
            var low = Allies[target];
            var cover = Allies[aria];
            if (
                !CombatPassiveRules.Covers(
                    target,
                    low.Hp,
                    low.MaxHp,
                    aria,
                    cover.Hp,
                    cover.MaxHp,
                    coverUsed
                )
            )
                return target;
            coverUsed = true;
            ShowPassive(aria, CombatPassiveKind.Cover);
            return aria;
        }

        /// <summary>切り返し: Rita cuts back at the enemy whose blow she took.</summary>
        private void Riposte(int rita, int attacker)
        {
            if (
                attacker < 0
                || !Enemies[attacker].Alive
                || !HasPassive(rita, CombatPassiveKind.Riposte)
            )
                return;
            var ally = Allies[rita];
            var enemy = Enemies[attacker];
            int power = CombatPassiveRules.Percent(
                ally.Stats.PhysicalAttack,
                CombatPassiveRules.RipostePercent
            );
            power = CombatPassiveRules.Grow(power, ThermalPercent(attacker));
            power = CombatFormula.Defend(power, enemy.PhysicalDefense, ally.Level);
            ShowPassive(rita, CombatPassiveKind.Riposte);
            Hit(attacker, power, BattleInspectElement.Slash);
            OpenWounds(attacker, rita);
        }

        /// <summary>浄めの誓い: a bad status on an ally is stopped, once a turn. True when stopped.</summary>
        private bool Purified(int ally, CardStatus status)
        {
            int anselm = PassiveHolder(CombatPassiveKind.PurifyingVow);
            if (anselm < 0 || vowUsed || !CardRules.IsBad(status))
                return false;
            vowUsed = true;
            ShowPassive(anselm, CombatPassiveKind.PurifyingVow);
            return true;
        }
    }
}
