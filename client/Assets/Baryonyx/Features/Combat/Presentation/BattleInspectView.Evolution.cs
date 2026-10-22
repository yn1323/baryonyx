using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.SkillEvolution;
using Baryonyx.UI.Cards;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// What the battle needs to let unique skills evolve (doc/features/skill-evolution.md#戦闘中の進化):
    /// the chance for a user's level, the draw, which stages are acquired already, and who is told
    /// of each evolution. The adventure gives it; without it nothing evolves.
    /// </summary>
    public sealed class BattleEvolution
    {
        /// <summary>The chance (0 to 1) that a card evolves when used by an ally of the given level.</summary>
        public Func<int, float> Chance;

        /// <summary>The draw (0 to 1); a card evolves when it comes under the chance.</summary>
        public Func<float> Draw = () => UnityEngine.Random.value;

        /// <summary>Whether a character's unique skill has a stage already (person, unique skill, stage).</summary>
        public Func<string, int, int, bool> Acquired;

        /// <summary>Told of each evolution (person, unique skill, the stage it became).</summary>
        public Action<string, int, int> Evolved;
    }

    /// <summary>
    /// A unique skill's card evolving in battle: when a card of a character's unique skill is
    /// used and the draw comes under its chance, before its effect the card rises and cracks
    /// (<see cref="BattleSkillVfx.EvolutionOmen"/>), the stage it becomes is picked from those it
    /// can (<see cref="Choice"/>; with one, it goes on by itself), and it shatters into that stage,
    /// which is then played. The card stays at that stage for the rest of the battle; a card
    /// evolves once a battle at most. The hand steps aside while the stage is picked.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        [Header("固有スキルの進化")]
        [Tooltip("進化先を選ぶ画面。")]
        public BattleEvolutionChoice Choice;

        /// <summary>How unique skills evolve in this battle, or null when they do not (outside an adventure).</summary>
        public BattleEvolution Evolution;

        // この戦闘で進化したカード（Deck の番号）。デッキが作り直されたら数え直す。
        private readonly HashSet<int> evolvedCards = new();
        private BattleInspectCardData[] evolvedDeck;
        private BattleInspectCardView evolvingFace;
        private CanvasGroup handGroup;

        /// <summary>True from the omen of an evolution until the evolved card has gone into its user.</summary>
        public bool Evolving { get; private set; }

        /// <summary>Whether the card of the deck at <paramref name="deckIndex"/> has evolved in this battle.</summary>
        public bool HasEvolved(int deckIndex) =>
            ReferenceEquals(evolvedDeck, Deck) && evolvedCards.Contains(deckIndex);

        /// <summary>
        /// Draws whether the card used evolves: a unique skill's stage that can evolve, not yet
        /// evolved in this battle, whose draw comes under the chance for its user's level.
        /// </summary>
        private bool DrawEvolution(int deckIndex, BattleInspectCardData data, int caster)
        {
            if (Evolution?.Chance == null || Vfx == null || Choice == null || data == null)
                return false;
            if (string.IsNullOrEmpty(data.Person) || data.Unique < 0)
                return false;
            if (!ReferenceEquals(evolvedDeck, Deck))
            {
                evolvedDeck = Deck;
                evolvedCards.Clear();
            }
            if (evolvedCards.Contains(deckIndex))
                return false;
            if (UniqueSkillEvolution.NextStages(data.Skill).Count == 0)
                return false;
            int level = caster >= 0 && caster < Allies.Length ? Allies[caster].Level : 1;
            float chance = Evolution.Chance(level);
            if (chance <= 0f || (Evolution.Draw?.Invoke() ?? 1f) >= chance)
                return false;
            evolvedCards.Add(deckIndex);
            return true;
        }

        /// <summary>
        /// The evolution of the card used: its omen, the pick of the stage, its burst into that
        /// stage, which becomes the card of the deck. <paramref name="done"/> is told the card
        /// played from now on.
        /// </summary>
        private IEnumerator Evolve(
            int deckIndex,
            BattleInspectCardData data,
            int caster,
            Action<BattleInspectCardData> done
        )
        {
            var face = EvolvingFace();
            if (face == null || !UniqueSkillEvolution.TryFind(data.Skill, out var tree, out _))
                yield break;
            var stages = UniqueSkillEvolution.NextStages(data.Skill);
            Evolving = true;
            ShowFace(face, data);
            var user =
                caster >= 0 && caster < Allies.Length ? Allies[caster].TargetArea : PartyAnchor;
            // From a branch's first step, it evolves into the branch's last.
            bool deep = BattleSkillVfx.TierOf(data.Skill) == 1;
            yield return Vfx.EvolutionOmen((RectTransform)face.transform, user, deep);
            yield return Vfx.EvolutionPresent();

            int pick = -1;
            SetHandAside(true);
            Choice.Open(OptionsOf(data, tree, stages), Vfx.EvolvingScreenPoint(), p => pick = p);
            while (pick < 0)
                yield return null;
            SetHandAside(false);

            int stage = stages[Mathf.Clamp(pick, 0, stages.Count - 1)];
            var skill = tree.StageAt(stage).Card;
            var art = data.ArtOf != null ? data.ArtOf(skill.Id) : null;
            var evolved = CardOf(skill, caster, art != null ? art : data.Art);
            evolved.Person = data.Person;
            evolved.Unique = data.Unique;
            evolved.ArtOf = data.ArtOf;
            Become(deckIndex, evolved);
            Evolution?.Evolved?.Invoke(data.Person, data.Unique, stage);
            yield return Vfx.EvolutionBurst(
                BattleSkillVfx.UniqueColor(skill.Id),
                deep,
                () => ShowFace(face, evolved)
            );
            Evolving = false;
            done(evolved);
        }

        // The card of the deck becomes the evolved stage, in the hand too.
        private void Become(int deckIndex, BattleInspectCardData evolved)
        {
            Deck[deckIndex] = evolved;
            deckKeywords = null;
            for (int i = 0; i < Cards.Length; i++)
                if (Cards[i].DeckIndex == deckIndex && Cards[i].InHand)
                    Show(i, deckIndex);
            Refresh();
        }

        private List<BattleEvolutionChoice.Option> OptionsOf(
            BattleInspectCardData data,
            UniqueSkillTree tree,
            IReadOnlyList<int> stages
        )
        {
            var options = new List<BattleEvolutionChoice.Option>();
            foreach (int stage in stages)
            {
                var skill = tree.StageAt(stage).Card;
                int element = (int)skill.Element;
                options.Add(
                    new BattleEvolutionChoice.Option
                    {
                        Name = skill.Name,
                        Owner = data.Owner,
                        Kind = CardText.KindLine(skill),
                        Description = CardText.Description(skill),
                        Art = data.ArtOf?.Invoke(skill.Id),
                        Frame = At(CardFrames, element),
                        Element =
                            skill.Element == CardElement.None ? null : At(ElementIcons, element),
                        Cost = skill.Cost,
                        Quality = skill.Quality,
                        FoilStyle = skill.FoilStyle,
                        Acquired =
                            Evolution?.Acquired?.Invoke(data.Person, data.Unique, stage) ?? false,
                    }
                );
            }
            return options;
        }

        private static void ShowFace(BattleInspectCardView face, BattleInspectCardData data)
        {
            face.Show(
                data.Name,
                data.Owner,
                data.Kind,
                data.Description,
                data.Art,
                data.Frame,
                data.ElementIcon,
                data.Cost,
                data.Quality,
                data.FoilStyle
            );
            face.SetPlayable(true, 0f);
        }

        // The card that evolves: a copy of a card of the hand on the effects' layer, made once
        // without the hand's input.
        private BattleInspectCardView EvolvingFace()
        {
            if (evolvingFace != null)
                return evolvingFace;
            var layer = Vfx != null ? Vfx.EvolutionLayer : null;
            BattleInspectCardView source = null;
            foreach (var card in Cards)
                if (card?.Face != null)
                {
                    source = card.Face;
                    break;
                }
            if (layer == null || source == null)
                return null;
            // Made under a hidden parent, so the input is gone before anything of it wakes.
            var holder = new GameObject("EvolvingCardHolder");
            holder.SetActive(false);
            var copy = Instantiate(source.gameObject, holder.transform, false);
            foreach (var input in copy.GetComponentsInChildren<BattleInspectCardInput>(true))
                DestroyImmediate(input);
            foreach (var graphic in copy.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphic.raycastTarget = false;
            copy.name = "EvolvingCard";
            copy.transform.SetParent(layer, false);
            copy.SetActive(false);
            Destroy(holder);
            evolvingFace = copy.GetComponent<BattleInspectCardView>();
            return evolvingFace;
        }

        // While the stage is picked, the hand steps aside for the stages offered.
        private void SetHandAside(bool aside)
        {
            if (Hand == null)
                return;
            if (handGroup == null && !Hand.TryGetComponent(out handGroup))
                handGroup = Hand.gameObject.AddComponent<CanvasGroup>();
            handGroup.alpha = aside ? 0f : 1f;
            handGroup.blocksRaycasts = !aside;
        }
    }
}
