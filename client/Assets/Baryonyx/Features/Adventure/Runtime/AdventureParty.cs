using System.Collections.Generic;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.Items;
using Baryonyx.Party;
using Baryonyx.Training;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The party an adventure's battle is fought with (doc/features/party.md): the formation's
    /// members in slot order, each at the level kept for them with the stats of that level raised
    /// by what they wear (doc/features/equipment.md#装備の効果), and the cards they bring to the
    /// deck. A unique skill brings the card of the stage the formation
    /// sets, once the level has unlocked it; a custom skill brings the owned card with its
    /// multiplier, and an item brings its card (doc/features/items.md#戦闘での使い方); an empty
    /// slot brings nothing. Each member brings the passive skills its level has unlocked
    /// (doc/features/passives.md).
    /// </summary>
    public static class AdventureParty
    {
        // 休憩で鍛えると、仲間は物攻・属攻が1回ごとに15%上がり、カードは威力が30%上がる（仮。
        // doc/features/stage-progression.md のマスの中身）。どちらもその冒険のあいだだけ続く。
        public const float HoneStat = 0.15f;
        public const float HoneCard = 0.3f;

        // 鍛えたカードの名前に付ける印。
        public const string HonedMark = "+";

        /// <summary>
        /// The formation's members as the battle takes them. <paramref name="itemArt"/> draws the
        /// cards of the items in the custom skill slots (without it, they have no art), and
        /// <paramref name="equipment"/> raises their stats (without it, they wear nothing).
        /// </summary>
        public static IReadOnlyList<BattlePartyMember> Of(
            PartyFormation formation,
            PartyMockData party,
            TrainingMockData training,
            ItemArt itemArt = null,
            EquipmentState equipment = null
        )
        {
            var members = PartySession.Members(formation);
            var store = new CardLoadoutSessionStore(party, training);
            var result = new BattlePartyMember[members.Count];
            for (int slot = 0; slot < members.Count; slot++)
                result[slot] = Member(members[slot], party, training, store, itemArt, equipment);
            return result;
        }

        private static BattlePartyMember Member(
            PartyMember member,
            PartyMockData party,
            TrainingMockData training,
            CardLoadoutSessionStore store,
            ItemArt itemArt,
            EquipmentState equipment
        )
        {
            if (member == null)
                return null;
            int level = PartySession.LevelOf(party, member.Id);
            var growth = training != null ? training.Find(member.Id) : null;
            var cards = new List<BattlePartyCard>();
            var uniques = store.UniquesOf(member.Id);
            for (int i = 0; i < uniques.Count; i++)
                if (uniques[i] is { Unlocked: true })
                {
                    var skill = store.UniqueCardOf(member.Id, i);
                    if (skill == null)
                        continue;
                    // 戦闘で進化できるよう、誰の何番目の固有スキルかと、ほかの段階の挿絵の引き方を持たせる。
                    cards.Add(
                        new BattlePartyCard
                        {
                            Skill = skill,
                            Art = party.ArtOf(skill.Id),
                            Person = member.Id,
                            Unique = i,
                            ArtOf = party.ArtOf,
                        }
                    );
                }
            foreach (string key in PartySession.CardsOf(party, member.Id))
                if (PartyCardKey.ItemOf(key) is { } item)
                    AddItem(cards, item, itemArt);
                else
                    Add(cards, key, PartySession.SkillOf(key), party);
            var passives = new List<Combat.CombatPassiveKind>();
            foreach (var passive in Combat.CombatPassives.UnlockedAt(member.Id, level))
                passives.Add(passive.Kind);
            return new BattlePartyMember
            {
                Name = member.Name,
                Art = member.Art,
                Tint = member.Tint,
                Level = level,
                Stats = EquipmentStats.With(
                    TrainingBond.StatsOf(growth, level),
                    equipment,
                    member.Id
                ),
                TwiceChance = EquipmentStats.TwiceChance(equipment, member.Id),
                Cards = cards,
                Passives = passives,
            };
        }

        /// <summary>
        /// The members as the adventure's rests and won battles grew them: each slot's physical
        /// and magic attack grown for each time it was honed, the boons chosen after won battles
        /// (<see cref="AdventureBoons"/>, added up with the honing), and its honed card skills
        /// stronger. <paramref name="strongBattle"/> is whether the battle about to be fought is a
        /// strong one, for the boons that work in those only.
        /// </summary>
        public static void Hone(
            IReadOnlyList<BattlePartyMember> members,
            string runId,
            bool strongBattle = false
        )
        {
            if (members == null)
                return;
            AdventureBoons.Apply(
                members,
                AdventureVitals.Boons(runId),
                AdventureVitals.HonedSlots(runId),
                HoneStat,
                strongBattle
            );
            var cards = new HashSet<string>(AdventureVitals.HonedCards(runId));
            for (int slot = 0; slot < members.Count; slot++)
            {
                var member = members[slot];
                if (member == null)
                    continue;
                foreach (var card in member.Cards)
                    if (
                        card?.Skill != null
                        && card.Unique < 0
                        && cards.Contains(AdventureVitals.CardKey(slot, card.Skill.Id))
                    )
                        card.Skill = Honed(card.Skill);
            }
        }

        /// <summary>
        /// The cards of a member a rest can study: the member's own card skills, each kind once,
        /// without the unique skills (they change into another stage when they awaken) and the
        /// items (they are not skills).
        /// </summary>
        public static IReadOnlyList<BattlePartyCard> HonableCards(BattlePartyMember member)
        {
            var result = new List<BattlePartyCard>();
            if (member?.Cards == null)
                return result;
            foreach (var card in member.Cards)
                if (
                    card?.Skill != null
                    && card.Unique < 0
                    && ItemCards.Find(card.Skill.Id) == null
                    && result.TrueForAll(other => other.Skill.Id != card.Skill.Id)
                )
                    result.Add(card);
            return result;
        }

        /// <summary>A card skill honed: its powers grown by <see cref="HoneCard"/> and its name marked.</summary>
        public static CardSkill Honed(CardSkill skill)
        {
            if (skill == null)
                return null;
            var honed = skill.Copy();
            var actions = new CardAction[skill.Actions.Count];
            for (int i = 0; i < actions.Length; i++)
            {
                actions[i] = skill.Actions[i].Copy();
                if (actions[i].HasPower)
                    actions[i].Percent = (int)
                        System.Math.Round(actions[i].Percent * (1f + HoneCard));
            }
            honed.Actions = actions;
            honed.Name = skill.Name + HonedMark;
            return honed;
        }

        // 枠に入れたアイテムは、スキルと同じくデッキのカードになる。使っても持っている数は減らず、
        // 品質を持たないため倍率は×1.00のまま、覚醒もしない。
        private static void AddItem(List<BattlePartyCard> cards, ItemCard item, ItemArt itemArt) =>
            cards.Add(
                new BattlePartyCard
                {
                    Skill = item.Card,
                    Art = AdventureItemCardArt.Of(itemArt, item.Id),
                }
            );

        // 持っているスキルの1枚は、戦闘で覚醒できるよう、どの1枚かと覚醒版の挿絵の引き方を持たせる
        // （doc/features/skill-awakening.md）。
        private static void Add(
            List<BattlePartyCard> cards,
            string key,
            Combat.CardSkill skill,
            PartyMockData party
        )
        {
            if (skill == null)
                return;
            cards.Add(
                new BattlePartyCard
                {
                    Skill = skill,
                    Art = party.ArtOf(skill.Id),
                    ArtOf = party.ArtOf,
                    OwnedCard = PartyCardKey.IsCard(key) ? PartyCardKey.IdOf(key) : "",
                }
            );
        }
    }
}
