using System;
using System.Collections.Generic;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using Baryonyx.UI;
using Baryonyx.WorldMap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The altar of evolution, and the evolution a chest gives, in the event window
    /// (doc/features/screens/adventure.md): the altar's hall behind, and along the bottom the
    /// party members whose unique skill can evolve, as the formation's cards (the figure, the
    /// name and the level). A member with one such skill goes on at once; one with two lights
    /// up and their two skills rise above the row as battle cards to pick from. What is picked
    /// goes on to the awakening scene (<see cref="AdventureAwakening"/>). The altar closes with
    /// the ×; the chest's evolution does not close until a skill is picked.
    /// </summary>
    public sealed class AdventureAltarPanel : WorldEventPanel
    {
        /// <summary>A member who can evolve: how their card shows and the skills that can evolve.</summary>
        public sealed class Person
        {
            public string Name = "";
            public int Level = 1;
            public Texture Art;
            public Color Tint = Color.white;
            public CharacterStats Stats;
            public IReadOnlyList<Skill> Skills = Array.Empty<Skill>();
        }

        /// <summary>A unique skill at the stage it is set to, and its art.</summary>
        public sealed class Skill
        {
            public CardSkill Card;
            public Texture Art;
        }

        public RawImage Backdrop;

        [Tooltip("仲間の札（編成の冒険者の札と同じ形）。")]
        public Button[] Members = Array.Empty<Button>();
        public RawImage[] Figures = Array.Empty<RawImage>();
        public TMP_Text[] Names = Array.Empty<TMP_Text>();
        public TMP_Text[] Levels = Array.Empty<TMP_Text>();
        public GameObject[] Picked = Array.Empty<GameObject>();

        [Tooltip("選んだ仲間の固有スキル（2つまで）。")]
        public Button[] Skills = Array.Empty<Button>();
        public BattleInspectCardView[] Faces = Array.Empty<BattleInspectCardView>();

        [Tooltip("属性ごとのカードの枠と、属性のアイコン（CardElementの順）。")]
        public Texture[] CardFrames = Array.Empty<Texture>();
        public Texture[] ElementIcons = Array.Empty<Texture>();

        private readonly List<Person> people = new();
        private int person = -1;
        private bool closable = true;
        private bool wired;

        /// <summary>A skill was picked: the member's place in the list, and which of their skills.</summary>
        public event Action<int, int> Chosen;

        public IReadOnlyList<Person> People => people;

        /// <summary>The member lit up, whose skills are shown, or -1.</summary>
        public int Current => person;

        private void Awake() => Wire();

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
            for (int i = 0; i < Members.Length; i++)
            {
                int slot = i;
                Members[i].onClick.AddListener(() => PickPerson(slot));
            }
            for (int i = 0; i < Skills.Length; i++)
            {
                int skill = i;
                Skills[i].onClick.AddListener(() => PickSkill(skill));
            }
        }

        /// <summary>
        /// Lays out <paramref name="candidates"/> before the window opens.
        /// <paramref name="canClose"/> is false for the chest's evolution, which waits for a pick.
        /// </summary>
        public void Use(IReadOnlyList<Person> candidates, bool canClose)
        {
            Wire();
            people.Clear();
            if (candidates != null)
                people.AddRange(candidates);
            closable = canClose;
            Rule = canClose
                ? WorldMapEventWindow.CloseRule.Anytime
                : WorldMapEventWindow.CloseRule.AfterDone;
            person = -1;
            for (int i = 0; i < Members.Length; i++)
            {
                var member = i < people.Count ? people[i] : null;
                Members[i].gameObject.SetActive(member != null);
                if (member == null)
                    continue;
                // 絵がなければ（停止中の見本）、札に作り込んだ絵のままにする。
                if (i < Figures.Length && Figures[i] != null && member.Art != null)
                {
                    Figures[i].texture = member.Art;
                    Figures[i].color = member.Tint;
                }
                if (i < Names.Length && Names[i] != null)
                    Names[i].text = member.Name;
                if (i < Levels.Length && Levels[i] != null)
                    Levels[i].text = member.Level.ToString();
            }
            ShowSkills();
        }

        /// <summary>Lights up the member at <paramref name="slot"/>; one with a single skill goes on with it.</summary>
        public void PickPerson(int slot)
        {
            if (slot < 0 || slot >= people.Count)
                return;
            if (people[slot].Skills.Count == 1)
            {
                person = slot;
                ShowSkills();
                Chosen?.Invoke(slot, 0);
                return;
            }
            person = slot;
            ShowSkills();
        }

        private void PickSkill(int skill)
        {
            if (person < 0 || skill >= people[person].Skills.Count)
                return;
            Chosen?.Invoke(person, skill);
        }

        // 選んだ仲間に金の縁を灯し、その固有スキルをカードで出す。
        private void ShowSkills()
        {
            for (int i = 0; i < Picked.Length; i++)
                if (Picked[i] != null)
                    Picked[i].SetActive(i == person);
            var skills =
                person >= 0 && person < people.Count && people[person].Skills.Count > 1
                    ? people[person].Skills
                    : Array.Empty<Skill>();
            for (int i = 0; i < Skills.Length; i++)
            {
                bool used = i < skills.Count;
                Skills[i].gameObject.SetActive(used);
                if (used && i < Faces.Length)
                    Paint(Faces[i], skills[i].Card, people[person], skills[i].Art);
            }
        }

        private void Paint(BattleInspectCardView face, CardSkill skill, Person owner, Texture art)
        {
            if (face == null || skill == null)
                return;
            int element = (int)skill.Element;
            var data = BattleInspectView.CardData(
                skill,
                0,
                owner.Name,
                owner.Stats,
                art,
                element >= 0 && element < CardFrames.Length ? CardFrames[element] : null,
                element >= 0 && element < ElementIcons.Length ? ElementIcons[element] : null,
                0
            );
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

        /// <summary>Lets the window close once a skill is picked (the chest's evolution waits for it).</summary>
        public void Release() => Done = true;

        protected override void Restart()
        {
            Wire();
            Done = closable;
        }

        protected override void Draw(float dt) { }
    }
}
