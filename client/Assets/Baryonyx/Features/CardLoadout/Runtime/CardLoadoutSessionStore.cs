using System.Collections.Generic;
using Baryonyx.Combat;
using Baryonyx.Party;
using Baryonyx.StepBonus;
using Baryonyx.Training;

namespace Baryonyx.CardLoadout
{
    /// <summary>
    /// The store of the running app: the party's cards and levels, the training's mock growth
    /// for the stats, and the UPT Home read today (none when the screen opens on its own).
    /// </summary>
    public sealed class CardLoadoutSessionStore : ICardLoadoutStore
    {
        private readonly PartyMockData party;
        private readonly TrainingMockData training;

        public CardLoadoutSessionStore(PartyMockData party, TrainingMockData training)
        {
            this.party = party;
            this.training = training;
        }

        public IReadOnlyList<string> CardsOf(string id) => PartySession.CardsOf(party, id);

        public void SetCards(string id, IReadOnlyList<string> skills) =>
            PartySession.SetCards(id, skills);

        public int StatOf(string id, CardStat stat)
        {
            var growth = training != null ? training.Find(id) : null;
            if (growth == null)
                return 0;
            var stats = growth.StatsAt(PartySession.LevelOf(party, id));
            return stat switch
            {
                CardStat.Strength => stats.Strength,
                CardStat.Magic => stats.Magic,
                CardStat.Defense => stats.Defense,
                _ => 0,
            };
        }

        public int Upt => StepBonusSession.TodayUpt ?? 0;

        public string Selected
        {
            get => CardLoadoutSession.Selected;
            set => CardLoadoutSession.Selected = value;
        }
    }
}
