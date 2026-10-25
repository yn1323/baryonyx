using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat.Presentation;
using Baryonyx.Equipment;
using Baryonyx.Items;
using Baryonyx.Party;
using Baryonyx.Training;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The party an automatic battle run fights with: the formation as the adventure's battle takes
    /// it (<see cref="AdventureParty.Of"/>, so whatever the battle's party gains later works here
    /// too), at the levels asked for, wearing the equipment asked for (passed as the battle scene
    /// passes what the player wears), with every custom skill card at the quality and awakening
    /// asked for (as an owned card plays: <see cref="PartySkillCard.Skill"/>). The levels are set
    /// as the editor without a server keeps them (<see cref="PartySession.SetLevel"/>), so the
    /// unique skills and passives the level unlocks come with them.
    /// </summary>
    public static class BattleBalanceParty
    {
        /// <summary>
        /// The members in slot order (null for an empty slot), each starting at the share of its
        /// maximum HP in <paramref name="shares"/> (slot order; missing ones start full).
        /// </summary>
        public static IReadOnlyList<BattlePartyMember> Of(
            BattleBalanceRequest request,
            PartyMockData party,
            TrainingMockData training,
            IReadOnlyList<float> shares = null
        )
        {
            request ??= new BattleBalanceRequest();
            var formation = PartySession.Formation(party);
            var slots = PartySession.Members(formation);
            for (int slot = 0; slot < slots.Count; slot++)
                if (slots[slot] != null && request.LevelOf(slot) > 0)
                    PartySession.SetLevel(slots[slot].Id, request.LevelOf(slot));
            var members = AdventureParty.Of(
                formation,
                party,
                training,
                equipment: Gear(request, slots)
            );
            for (int slot = 0; slot < members.Count; slot++)
            {
                var member = members[slot];
                if (member == null)
                    continue;
                if (shares != null && slot < shares.Count)
                    member.HpShare = shares[slot];
                foreach (var card in member.Cards)
                    if (
                        card?.Skill != null
                        && card.Unique < 0
                        && ItemCards.Find(card.Skill.Id) == null
                    )
                        card.Skill = Owned(card.Skill.Id, request) ?? card.Skill;
            }
            return members;
        }

        /// <summary>
        /// The equipment the request has the members wear, as the server's equipment state would
        /// hold it (null when none). An unknown kind of equipment stops the run.
        /// </summary>
        public static EquipmentState Gear(
            BattleBalanceRequest request,
            IReadOnlyList<PartyMember> slots
        )
        {
            if (request?.equipment == null || request.equipment.Length == 0)
                return null;
            var owned = new List<EquipmentItem>();
            var worn = new List<(string, EquipmentPlace, string)>();
            for (int i = 0; i < request.equipment.Length; i++)
            {
                var gear = request.equipment[i];
                var kind =
                    EquipmentCatalog.Find(gear.equipment)
                    ?? throw new ArgumentException($"Unknown equipment \"{gear.equipment}\".");
                var member =
                    !string.IsNullOrEmpty(gear.character)
                        ? slots.FirstOrDefault(each => each?.Id == gear.character)
                    : gear.slot >= 0 && gear.slot < slots.Count ? slots[gear.slot]
                    : null;
                if (member == null)
                    continue;
                string id = $"balance-{i}";
                owned.Add(new EquipmentItem(id, kind.Id, gear.multiplier));
                var place =
                    kind.Slot == EquipmentSlot.Accessory
                        ? EquipmentPlace.Accessory(gear.accessory)
                        : new EquipmentPlace(kind.Slot);
                worn.Add((member.Id, place, id));
            }
            return new EquipmentState(owned, worn);
        }

        // 持っているカードと同じ作り方で、品質と覚醒を当てる。その覚醒がないカードは品質だけを当てる。
        private static Combat.CardSkill Owned(string skillId, BattleBalanceRequest request) =>
            new PartySkillCard("", skillId, request.cardMultiplier, request.awakenedBranch).Skill
            ?? new PartySkillCard("", skillId, request.cardMultiplier).Skill;
    }
}
