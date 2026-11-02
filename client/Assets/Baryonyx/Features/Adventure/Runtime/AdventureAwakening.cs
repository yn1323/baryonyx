using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The awakening scene of the altar and the chest's evolution, the same as the workshop's
    /// skill awakening (doc/features/screens/shop.md): the screen darkens, the unique skill's
    /// card rises from the member it belongs to and cracks with the battle's omen of awakening,
    /// the stages it can become open below as large cards to pick, and the round check decides.
    /// Once the flow has saved the pick (<see cref="Burst"/>), the card bursts into the stage
    /// chosen. The arrow at the top left goes back before a stage is decided. AdventureEventAssets
    /// builds it.
    /// </summary>
    public sealed class AdventureAwakening : MonoBehaviour
    {
        /// <summary>A card to show: the skill, whose it is and the stats its words read, and its art.</summary>
        public sealed class Face
        {
            public CardSkill Skill;
            public string Owner = "";
            public CharacterStats Stats;
            public Texture Art;
        }

        // 覚醒した段階を、読める間だけ大きく見せる（商会の覚醒と同じ）。
        public const float RevealSeconds = 1.6f;
        public const float RevealScale = 2.5f;

        public GameObject Scene;
        public BattleSkillVfx Vfx;
        public BattleInspectCardView Origin;
        public BattleEvolutionChoice Choice;
        public Button Back;

        [Tooltip("属性ごとのカードの枠と、属性のアイコン（CardElementの順）。")]
        public Texture[] CardFrames = Array.Empty<Texture>();
        public Texture[] ElementIcons = Array.Empty<Texture>();

        private RectTransform originParent;
        private Vector2 originPosition;
        private Vector3 originScale;
        private Coroutine running;
        private Action back;
        private bool decided;
        private bool wired;

        public bool IsOpen => Scene != null && Scene.activeSelf;

        /// <summary>A stage is decided and the scene waits for <see cref="Burst"/> or <see cref="Close"/>.</summary>
        public bool Decided => IsOpen && decided;

        private void Awake() => Wire();

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
            if (Back != null)
                Back.onClick.AddListener(() => HandleBack());
            if (Origin != null)
            {
                var rect = (RectTransform)Origin.transform;
                originParent = (RectTransform)rect.parent;
                originPosition = rect.anchoredPosition;
                originScale = rect.localScale;
            }
        }

        /// <summary>
        /// Opens the scene on <paramref name="from"/>, rising from <paramref name="caster"/> with
        /// the omen, and offers <paramref name="into"/>. <paramref name="picked"/> is told the
        /// stage decided (its place in <paramref name="into"/>); <paramref name="goBack"/> runs
        /// when the arrow closes the scene first (none for the chest's evolution).
        /// </summary>
        public void Open(
            Face from,
            IReadOnlyList<Face> into,
            RectTransform caster,
            bool deep,
            Action<int> picked,
            Action goBack
        )
        {
            Wire();
            Stop();
            back = goBack;
            decided = false;
            if (Scene != null)
                Scene.SetActive(true);
            if (Back != null)
                Back.gameObject.SetActive(goBack != null);
            Show(Origin, from);
            if (!Application.isPlaying || Vfx == null || Origin == null)
            {
                Choice.Open(OptionsOf(into), OriginPoint(), pick => Decide(pick, picked));
                return;
            }
            running = StartCoroutine(Run(from, into, caster, deep, picked));
        }

        private IEnumerator Run(
            Face from,
            IReadOnlyList<Face> into,
            RectTransform caster,
            bool deep,
            Action<int> picked
        )
        {
            var card = (RectTransform)Origin.transform;
            card.SetParent(Vfx.EvolutionLayer, false);
            Vfx.GroundUnderCaster = false;
            Vfx.EvolveAtCenter = true;
            Vfx.RevealSeconds = RevealSeconds;
            Vfx.RevealScale = RevealScale;
            yield return Vfx.EvolutionOmen(card, caster, deep, ColorOf(from));
            yield return Vfx.EvolutionPresent();
            running = null;
            Choice.Open(OptionsOf(into), Vfx.EvolvingScreenPoint(), pick => Decide(pick, picked));
        }

        private void Decide(int pick, Action<int> picked)
        {
            if (decided)
                return;
            decided = true;
            if (Back != null)
                Back.gameObject.SetActive(false);
            picked?.Invoke(pick);
        }

        /// <summary>The pick was saved: the card bursts into <paramref name="evolved"/>, then the scene closes and <paramref name="done"/> runs.</summary>
        public void Burst(Face evolved, bool deep, Action done)
        {
            if (!IsOpen)
            {
                done?.Invoke();
                return;
            }
            if (!Application.isPlaying || Vfx == null)
            {
                Close();
                done?.Invoke();
                return;
            }
            running = StartCoroutine(RunBurst(evolved, deep, done));
        }

        private IEnumerator RunBurst(Face evolved, bool deep, Action done)
        {
            yield return Vfx.EvolutionBurst(ColorOf(evolved), deep, () => Show(Origin, evolved));
            running = null;
            Close();
            done?.Invoke();
        }

        /// <summary>Closes the scene at once, without awakening.</summary>
        public void Close()
        {
            Stop();
            back = null;
            decided = false;
            if (Scene != null)
                Scene.SetActive(false);
        }

        /// <summary>The device's back key and the arrow: back to the pick, until a stage is decided.</summary>
        public bool HandleBack()
        {
            if (!IsOpen)
                return false;
            if (decided || back == null)
                return true;
            var then = back;
            Close();
            then();
            return true;
        }

        private void Stop()
        {
            if (running != null)
                StopCoroutine(running);
            running = null;
            if (Choice != null)
                Choice.Close();
            if (Vfx != null && Application.isPlaying)
                Vfx.Clear();
            // 演出の層へ移したカードを、場面の上の元の所へ戻す。
            if (Origin != null && originParent != null)
            {
                var rect = (RectTransform)Origin.transform;
                rect.SetParent(originParent, false);
                rect.anchoredPosition = originPosition;
                rect.localScale = originScale;
                rect.localRotation = Quaternion.identity;
            }
        }

        private List<BattleEvolutionChoice.Option> OptionsOf(IReadOnlyList<Face> into)
        {
            var options = new List<BattleEvolutionChoice.Option>();
            foreach (var face in into ?? Array.Empty<Face>())
            {
                var data = DataOf(face);
                options.Add(
                    new BattleEvolutionChoice.Option
                    {
                        Name = data.Name,
                        Owner = data.Owner,
                        Kind = data.Kind,
                        Description = data.Description,
                        Art = data.Art,
                        Frame = data.Frame,
                        Element = data.ElementIcon,
                        Cost = data.Cost,
                        Quality = data.Quality,
                        FoilStyle = data.FoilStyle,
                    }
                );
            }
            return options;
        }

        private BattleInspectCardData DataOf(Face face)
        {
            int element = (int)face.Skill.Element;
            return BattleInspectView.CardData(
                face.Skill,
                0,
                face.Owner,
                face.Stats,
                face.Art,
                element >= 0 && element < CardFrames.Length ? CardFrames[element] : null,
                element >= 0 && element < ElementIcons.Length ? ElementIcons[element] : null,
                0
            );
        }

        private void Show(BattleInspectCardView view, Face face)
        {
            if (view == null || face?.Skill == null)
                return;
            var data = DataOf(face);
            view.Show(
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
            view.SetPlayable(true, 0f);
        }

        // 演出の光の色：カードの挿絵の色（挿絵がなければスキルごとの色）。
        private static Color ColorOf(Face face) =>
            CardArtColor.Of(face?.Art, BattleSkillVfx.ColorOf(face?.Skill?.Id ?? ""));

        // 覚醒先の光の線は、上に浮かぶ今のカードの下端から伸ばす。
        private Vector2 OriginPoint()
        {
            if (Origin == null)
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.8f);
            var rect = (RectTransform)Origin.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bottom = (corners[0] + corners[3]) * 0.5f;
            var canvas = rect.GetComponentInParent<Canvas>();
            var eye =
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            return RectTransformUtility.WorldToScreenPoint(eye, bottom);
        }
    }
}
