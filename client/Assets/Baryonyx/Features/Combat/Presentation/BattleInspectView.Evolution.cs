using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.SkillEvolution;
using Baryonyx.UI.Cards;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// What the battle needs to let cards awaken: unique skills evolve
    /// (doc/features/skill-evolution.md#戦闘中の覚醒) and owned skill cards awaken
    /// (doc/features/skill-awakening.md). For each, the chance for a user's level, and who is told
    /// of each; the draw, and which unique stages are acquired already. The adventure gives it;
    /// without it nothing awakens.
    /// </summary>
    public sealed class BattleEvolution
    {
        /// <summary>The chance (0 to 1) that a unique skill's card evolves when drawn for an ally of the given level.</summary>
        public Func<int, float> Chance;

        /// <summary>The chance (0 to 1) that an owned skill card awakens when drawn for an ally of the given level; none when null.</summary>
        public Func<int, float> AwakenChance;

        /// <summary>The draw (0 to 1); a card evolves when it comes under the chance.</summary>
        public Func<float> Draw = () => UnityEngine.Random.value;

        /// <summary>Whether a character's unique skill has a stage already (person, unique skill, stage).</summary>
        public Func<string, int, int, bool> Acquired;

        /// <summary>Told of each evolution (person, unique skill, the stage it became).</summary>
        public Action<string, int, int> Evolved;

        /// <summary>Told of each owned skill card awakening (the owned card, the branch it became: 1 or 2).</summary>
        public Action<string, int> Awakened;
    }

    /// <summary>
    /// A card awakening in battle: when a card of a character's unique skill, or an owned skill
    /// card that has not awakened, comes into the hand and the draw comes under its chance, it
    /// lights up in the hand (<see cref="BattleEvolutionGlint"/>) and awakens when used. Before
    /// its effect the card rises and cracks (<see cref="BattleSkillVfx.EvolutionOmen"/>), the
    /// stage it becomes is picked from those it can (<see cref="Choice"/>; with one, it is
    /// picked too), and it shatters into that stage, which is then played. An owned skill card
    /// never goes back, so it may also be left as it is ("覚醒しない"): its light settles, the
    /// card goes back to its user, and it is played as it was. The card stays at its stage for
    /// the rest of the battle; a card awakens (or is left) once a battle at most. The hand steps
    /// aside while the stage is picked.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        [Header("固有スキルとスキルの覚醒")]
        [Tooltip("覚醒先を選ぶ画面。")]
        public BattleEvolutionChoice Choice;

        /// <summary>How cards awaken in this battle, or null when they do not (outside an adventure).</summary>
        public BattleEvolution Evolution;

        // この戦闘で覚醒した（または覚醒しないを選んだ）カード（Deck の番号）。デッキが作り直されたら数え直す。
        private readonly HashSet<int> evolvedCards = new();

        // この戦闘で覚醒が当たり、使えば覚醒するカード（Deck の番号）。手札では光る。
        private readonly HashSet<int> omenCards = new();
        private BattleInspectCardData[] evolvedDeck;
        private BattleInspectCardView evolvingFace;
        private BattleEvolutionGlint[] glints = Array.Empty<BattleEvolutionGlint>();
        private CanvasGroup handGroup;

        /// <summary>True from the omen of an evolution until the evolved card has gone into its user.</summary>
        public bool Evolving { get; private set; }

        /// <summary>Whether the card of the deck at <paramref name="deckIndex"/> has evolved in this battle.</summary>
        public bool HasEvolved(int deckIndex) =>
            ReferenceEquals(evolvedDeck, Deck) && evolvedCards.Contains(deckIndex);

        /// <summary>
        /// Whether the card of the deck at <paramref name="deckIndex"/> evolves when used: its
        /// draw came under the chance as it came into the hand, and it lights up there.
        /// </summary>
        public bool HasOmen(int deckIndex) =>
            ReferenceEquals(evolvedDeck, Deck) && omenCards.Contains(deckIndex);

        /// <summary>Whether the card on screen at <paramref name="index"/> shows its light.</summary>
        public bool Glints(int index) =>
            index >= 0 && index < glints.Length && glints[index] != null && glints[index].Lit;

        // An owned skill card that may still awaken (a unique skill's card is not one).
        private static bool IsOwnedCard(BattleInspectCardData data) =>
            data != null && data.Unique < 0 && !string.IsNullOrEmpty(data.OwnedCard);

        // A unique skill's card (of a character).
        private static bool IsUniqueCard(BattleInspectCardData data) =>
            data != null && !string.IsNullOrEmpty(data.Person) && data.Unique >= 0;

        /// <summary>
        /// Draws, as the card of the deck at <paramref name="deckIndex"/> comes into the hand,
        /// whether it awakens when used: a unique skill's stage that can evolve, or an owned skill
        /// card not awakened yet, not awakened (nor left) in this battle nor lit already, whose
        /// draw comes under the chance of its kind for its user's level.
        /// </summary>
        private void DrawOmen(int deckIndex)
        {
            if (Evolution == null || Vfx == null || Choice == null)
                return;
            if (deckIndex < 0 || deckIndex >= Deck.Length)
                return;
            var data = Deck[deckIndex];
            bool unique = IsUniqueCard(data);
            var chanceOf =
                unique ? Evolution.Chance
                : IsOwnedCard(data) ? Evolution.AwakenChance
                : null;
            if (chanceOf == null)
                return;
            if (!ReferenceEquals(evolvedDeck, Deck))
            {
                evolvedDeck = Deck;
                evolvedCards.Clear();
                omenCards.Clear();
            }
            if (evolvedCards.Contains(deckIndex) || omenCards.Contains(deckIndex))
                return;
            int next = unique
                ? UniqueSkillEvolution.NextStages(data.Skill).Count
                : CardSkillAwakenings.NextStages(data.Skill).Count;
            if (next == 0)
                return;
            int caster = data.Caster;
            int level = caster >= 0 && caster < Allies.Length ? Allies[caster].Level : 1;
            float chance = chanceOf(level);
            if (chance <= 0f || (Evolution.Draw?.Invoke() ?? 1f) >= chance)
                return;
            omenCards.Add(deckIndex);
        }

        /// <summary>
        /// Whether the card used evolves: one lit in the hand. Its light is spent on it, as a
        /// card evolves once a battle at most.
        /// </summary>
        private bool TakeOmen(int deckIndex)
        {
            if (Evolution == null || Vfx == null || Choice == null || !HasOmen(deckIndex))
                return false;
            omenCards.Remove(deckIndex);
            evolvedCards.Add(deckIndex);
            return true;
        }

        // Cards put away to start over lose their light; what has evolved stays so.
        private void ClearOmens() => omenCards.Clear();

        // The light of the card on screen at index, made the first time it is lit.
        private void ShowGlint(int index, bool lit)
        {
            if (glints.Length != Cards.Length)
                Array.Resize(ref glints, Cards.Length);
            var glint = glints[index];
            if (glint == null)
            {
                if (!lit || (glint = MakeGlint(index)) == null)
                    return;
                glints[index] = glint;
            }
            glint.SetLit(lit);
        }

        private BattleEvolutionGlint MakeGlint(int index) =>
            Vfx == null
                ? null
                : BattleEvolutionGlint.Make(
                    Cards[index].Body,
                    Vfx.MaterialOf(BattleVfxShape.Glow),
                    Vfx.MaterialOf(BattleVfxShape.Beam),
                    Vfx.MaterialOf(BattleVfxShape.Sparkle)
                );

        /// <summary>
        /// The awakening of the card used: its omen, the pick of the stage, its burst into that
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
            bool owned = IsOwnedCard(data);
            UniqueSkillTree tree = null;
            List<int> stages = new();
            var into = new List<CardSkill>();
            if (owned)
                into.AddRange(CardSkillAwakenings.NextStages(data.Skill));
            else if (UniqueSkillEvolution.TryFind(data.Skill, out tree, out _))
                foreach (int stage in UniqueSkillEvolution.NextStages(data.Skill))
                {
                    stages.Add(stage);
                    into.Add(tree.StageAt(stage).Card);
                }
            if (face == null || into.Count == 0)
            {
                done(data);
                yield break;
            }
            Evolving = true;
            ShowFace(face, data);
            var user =
                caster >= 0 && caster < Allies.Length ? Allies[caster].TargetArea : PartyAnchor;
            // From a branch's first step, it evolves into the branch's last.
            bool deep = !owned && BattleSkillVfx.TierOf(data.Skill) == 1;
            yield return Vfx.EvolutionOmen((RectTransform)face.transform, user, deep);
            yield return Vfx.EvolutionPresent();

            const int none = -1;
            int pick = none;
            SetHandAside(true);
            Choice.Open(OptionsOf(data, into, stages), Vfx.EvolvingScreenPoint(), p => pick = p);
            while (pick == none)
                yield return null;
            SetHandAside(false);

            pick = Mathf.Clamp(pick, 0, into.Count - 1);
            // 覚醒しても、品質強化で上げた倍率を引き継ぐ。
            var skill = into[pick]
                .WithMultiplier(data.Card?.Multiplier ?? Inventory.Quality.MinMultiplier);
            var art = data.ArtOf != null ? data.ArtOf(skill.Id) : null;
            var evolved = CardOf(skill, caster, art != null ? art : data.Art);
            evolved.Person = data.Person;
            evolved.Unique = data.Unique;
            evolved.ArtOf = data.ArtOf;
            evolved.OwnedCard = data.OwnedCard;
            Become(deckIndex, evolved);
            if (owned)
                Evolution?.Awakened?.Invoke(data.OwnedCard, pick + 1);
            else
                Evolution?.Evolved?.Invoke(data.Person, data.Unique, stages[pick]);
            yield return Vfx.EvolutionBurst(
                BattleSkillVfx.ColorOf(skill.Id),
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

        // The stages offered: a unique skill's (with whether each is acquired) or an owned card's two branches.
        private List<BattleEvolutionChoice.Option> OptionsOf(
            BattleInspectCardData data,
            IReadOnlyList<CardSkill> into,
            IReadOnlyList<int> stages
        )
        {
            var options = new List<BattleEvolutionChoice.Option>();
            for (int i = 0; i < into.Count; i++)
            {
                var skill = into[i];
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
                            i < stages.Count
                            && (
                                Evolution?.Acquired?.Invoke(data.Person, data.Unique, stages[i])
                                ?? false
                            ),
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
            BattleEvolutionGlint.StripFrom(copy);
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

        // While the stage is picked, the hand and its counters step aside for the stages offered,
        // and the end of the turn for the check that confirms in its place.
        private void SetHandAside(bool aside)
        {
            foreach (var group in ControlGroups())
                if (group != null)
                {
                    group.alpha = aside ? 0f : 1f;
                    group.blocksRaycasts = !aside;
                }
        }
    }
}
