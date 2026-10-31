using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Inventory;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Training;

namespace Baryonyx.Enhancement
{
    /// <summary>
    /// One row of the trading house's awakening list (doc/features/enhancement.md#覚醒の画面): a
    /// unique skill a character can still awaken, or an owned skill card that has not awakened,
    /// with the stages it could become.
    /// </summary>
    public sealed class AwakeningRow
    {
        /// <summary>Who holds it: the unique skill's owner or the person with the card in a slot; null for a card no one has.</summary>
        public PartyMember Character { get; set; }

        /// <summary>The card shown: the unique skill's stage set, or the owned card, at its multiplier.</summary>
        public CardSkill Card { get; set; }

        public float Multiplier { get; set; } = Inventory.Quality.MinMultiplier;
        public float MaxMultiplier { get; set; } = Inventory.Quality.MinMultiplier;
        public ItemQuality Quality => Inventory.Quality.Of(Multiplier, MaxMultiplier);

        /// <summary>The stages it could awaken into (one to three for a unique skill, two for a card).</summary>
        public IReadOnlyList<AwakeningOption> Options { get; set; } =
            Array.Empty<AwakeningOption>();

        public bool IsSkillCard => Options.Count > 0 && Options[0].IsSkillCard;
    }

    /// <summary>
    /// The trading house's awakening list in its order: person by person (the party first), their
    /// unique skills and then the cards in their custom skill slots; after everyone, the cards no
    /// one holds, the better quality first. Only what can still awaken is listed.
    /// </summary>
    public static class AwakeningRows
    {
        /// <summary>
        /// The rows. <paramref name="cardsOf"/> gives the ids of the owned cards in a person's
        /// slots, in slot order; <paramref name="progressOf"/> each unique skill's progress.
        /// </summary>
        public static IReadOnlyList<AwakeningRow> Of(
            IEnumerable<PartyMember> people,
            TrainingMockData training,
            Func<string, int, UniqueSkillProgress> progressOf,
            IEnumerable<PartySkillCard> cards,
            Func<string, IEnumerable<string>> cardsOf
        )
        {
            var members = (people ?? Array.Empty<PartyMember>())
                .Where(member => member != null)
                .ToArray();
            var owned = (cards ?? Array.Empty<PartySkillCard>()).ToArray();
            var byId = owned.GroupBy(card => card.Id).ToDictionary(g => g.Key, g => g.First());
            var held = new HashSet<string>();
            var rows = new List<AwakeningRow>();
            foreach (var member in members)
            {
                rows.AddRange(UniqueRows(member, training, progressOf));
                foreach (string id in cardsOf?.Invoke(member.Id) ?? Array.Empty<string>())
                {
                    if (id == null || !byId.TryGetValue(id, out var card) || !held.Add(id))
                        continue;
                    var row = CardRow(card, member);
                    if (row != null)
                        rows.Add(row);
                }
            }
            // 誰にも付けていない1枚は、品質の高い順。同じ品質はスキルの定義の順、同じ種類は倍率の高い順。
            rows.AddRange(
                owned
                    .Where(card => !held.Contains(card.Id))
                    .Select(card => CardRow(card, null))
                    .Where(row => row != null)
                    .OrderByDescending(row => row.Quality)
                    .ThenBy(row => Order(row.Options[0].SkillId))
                    .ThenByDescending(row => row.Multiplier)
            );
            return rows;
        }

        // その人の解放した固有スキルのうち、まだ覚醒できる段階のある物。今セットしている段階のカードで出す。
        private static IEnumerable<AwakeningRow> UniqueRows(
            PartyMember member,
            TrainingMockData training,
            Func<string, int, UniqueSkillProgress> progressOf
        )
        {
            foreach (
                var option in EnhancementTargets
                    .Awakenings(new[] { member }, training, progressOf)
                    .GroupBy(option => option.Index)
            )
            {
                var first = option.First();
                var progress =
                    progressOf?.Invoke(member.Id, first.Index) ?? new UniqueSkillProgress();
                var root = first.Tree.Root?.Card;
                var set = first.Tree.StageAt(progress.SetStage)?.Card ?? root;
                float limit = root != null ? CardSkillQuality.LimitOf(root) : Quality.MinMultiplier;
                yield return new AwakeningRow
                {
                    Character = member,
                    Card = set?.WithMultiplier(progress.Multiplier),
                    Multiplier = progress.Multiplier,
                    MaxMultiplier = limit,
                    Options = option.ToArray(),
                };
            }
        }

        // まだ覚醒していない1枚と、その2つの枝。覚醒した1枚や、枝のないスキルは出さない。
        private static AwakeningRow CardRow(PartySkillCard card, PartyMember holder)
        {
            var options = EnhancementTargets.SkillAwakenings(
                new[] { card },
                holder != null ? new Dictionary<string, string> { [card.Id] = holder.Name } : null
            );
            if (options.Count == 0)
                return null;
            return new AwakeningRow
            {
                Character = holder,
                Card = card.Skill,
                Multiplier = options[0].Multiplier,
                MaxMultiplier = options[0].MaxMultiplier,
                Options = options,
            };
        }

        private static int Order(string skillId)
        {
            for (int i = 0; i < CardSkills.All.Count; i++)
                if (CardSkills.All[i].Id == skillId)
                    return i;
            return int.MaxValue;
        }
    }
}
