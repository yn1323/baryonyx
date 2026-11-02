using System;
using Baryonyx.Combat;
using Baryonyx.Party;
using Baryonyx.Training;
using Baryonyx.UI.Cards;
using UnityEngine;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// A skill shown as the battle's deck card in the trading house (the buy's detail, the
    /// enhancement's before and after): the card at a multiplier, its frame by element.
    /// </summary>
    public static class WorkshopSkillCard
    {
        /// <summary>Shows <paramref name="card"/> at <paramref name="multiplier"/>, or hides the widget when null.</summary>
        public static void Show(
            TrainingCardWidget widget,
            CardSkill card,
            float multiplier,
            float max,
            bool unique,
            PartyMockData party,
            Texture[] frames,
            Sprite[] icons
        )
        {
            var root = widget?.Frame != null ? widget.Frame.transform.parent.gameObject : null;
            if (root != null)
                root.SetActive(card != null);
            if (card == null)
                return;
            var scaled = card.WithMultiplier(multiplier);
            int element = (int)card.Element;
            var state = TrainingCardState.Of(
                card.Id,
                unique ? TrainingCardState.UniqueType : TrainingCardState.SkillType,
                scaled.Name,
                CardText.KindLine(scaled),
                CardText.Description(scaled),
                scaled.Cost,
                Inventory.Quality.Of(multiplier, max),
                card.FoilStyle,
                card.Element,
                card.Element != CardElement.None && element < (icons?.Length ?? 0)
                    ? icons[element]
                    : null,
                party != null ? party.ArtOf(card.Id) : null
            );
            TrainingView.ShowCard(
                widget,
                state,
                element < (frames?.Length ?? 0) ? frames[element] : null
            );
        }
    }
}
