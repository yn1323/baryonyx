using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The rest's campfire (doc/features/stage-progression.md, 休憩の鍛える): the night camp with the
    /// party round the fire, where a tap on the tent rests, a tap on the stump and the firewood
    /// trains a member and a tap on the book on the crate studies a card. Training stands the
    /// party on pedestals to pick one; studying lays out each member's card skills as battle cards
    /// to pick one, with what it grows into. The camp itself does not close: one of the three is
    /// chosen to go on. AdventureAssets builds it into the exploration screen; the exploration's
    /// flow opens it in a rest and saves what was chosen.
    /// </summary>
    public sealed class ExplorationCampfire : MonoBehaviour
    {
        // 研鑽で、1人のカードを並べる数。
        public const int CardsPerMember = 2;

        // 選んだカードを持ち上げる高さ（設計座標）。
        public const float PickedLift = 18f;

        // 夜の野営地に立つ4人と薪の色：焚き火に照らされた暖かい色で、少し暗く。
        public static readonly Color NightTint = new(0.86f, 0.72f, 0.62f);

        private static readonly Color PedestalIdle = new(0.5f, 0.7f, 1f, 0.3f);
        private static readonly Color PedestalPicked = new(1f, 0.82f, 0.36f, 0.85f);
        private static readonly Color GlowColor = new(1f, 0.82f, 0.42f);

        [Header("焚き火")]
        public RectTransform Camp;
        public RawImage Scene;

        [Tooltip("焚き火を囲む4人（編成の枠の順）。")]
        public RawImage[] Party = Array.Empty<RawImage>();
        public Graphic[] PartyShadows = Array.Empty<Graphic>();

        [Tooltip("絵の中の物と、その札。押すと休む・鍛える・研鑽になる。")]
        public Button RestButton;
        public Button TrainButton;
        public Button StudyButton;

        [Tooltip("押せる物の後ろで脈打つ光（休む・鍛える・研鑽の順）。")]
        public Graphic[] Glows = Array.Empty<Graphic>();

        [Header("鍛える")]
        public RectTransform Train;
        public Button[] TrainSlots = Array.Empty<Button>();
        public RawImage[] TrainSprites = Array.Empty<RawImage>();
        public Graphic[] TrainPedestals = Array.Empty<Graphic>();
        public TMP_Text[] TrainNames = Array.Empty<TMP_Text>();

        [Tooltip("選んだ仲間の上に出す、上がる能力値の札。")]
        public RectTransform TrainTag;
        public Button TrainConfirm;
        public Button TrainBack;

        [Header("研鑽")]
        public RectTransform Study;
        public RawImage[] StudySprites = Array.Empty<RawImage>();
        public TMP_Text[] StudyNames = Array.Empty<TMP_Text>();

        [Tooltip("仲間ごとに CardsPerMember 枚ずつ、枠の順に並べたカード。")]
        public Button[] StudyCards = Array.Empty<Button>();
        public BattleInspectCardView[] StudyFaces = Array.Empty<BattleInspectCardView>();

        [Tooltip("選んだカードを囲む光（StudyCardsと同じ順）。")]
        public Graphic[] StudyRims = Array.Empty<Graphic>();

        [Tooltip("選んだカードが研鑽でどうなるか。")]
        public TMP_Text StudyLine;
        public Button StudyConfirm;
        public Button StudyBack;

        [Tooltip("属性ごとのカードの枠と、属性のアイコン（CardElementの順）。")]
        public Texture[] CardFrames = Array.Empty<Texture>();
        public Texture[] ElementIcons = Array.Empty<Texture>();

        private IReadOnlyList<BattlePartyMember> members = Array.Empty<BattlePartyMember>();
        private readonly List<(int Slot, BattlePartyCard Card)> cards = new();
        private HashSet<string> honed = new();
        private Action rest;
        private Action<int> train;
        private Action<int, CardSkill> study;
        private Vector2[] cardHomes = Array.Empty<Vector2>();
        private bool wired;

        public bool IsOpen => Shown(Camp) || Shown(Train) || Shown(Study);
        public bool CampShown => Shown(Camp);
        public bool TrainShown => Shown(Train);
        public bool StudyShown => Shown(Study);

        /// <summary>The member picked to train (a party slot), or -1.</summary>
        public int PickedMember { get; private set; } = -1;

        /// <summary>The card picked to study (its place in <see cref="StudyCards"/>), or -1.</summary>
        public int PickedCard { get; private set; } = -1;

        /// <summary>The party the camp was opened with (slot order, null for an empty slot).</summary>
        public IReadOnlyList<BattlePartyMember> Members => members;

        /// <summary>The cards laid out to study, in the order of <see cref="StudyCards"/>.</summary>
        public IReadOnlyList<(int Slot, BattlePartyCard Card)> StudiedCards => cards;

        private static bool Shown(RectTransform panel) =>
            panel != null && panel.gameObject.activeSelf;

        private void Awake() => Wire();

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
            if (RestButton != null)
                RestButton.onClick.AddListener(PressRest);
            if (TrainButton != null)
                TrainButton.onClick.AddListener(OpenTrain);
            if (StudyButton != null)
                StudyButton.onClick.AddListener(OpenStudy);
            foreach (var button in new[] { RestButton, TrainButton, StudyButton })
                ForwardTags(button);
            for (int i = 0; i < TrainSlots.Length; i++)
            {
                int slot = i;
                if (TrainSlots[i] != null)
                    TrainSlots[i].onClick.AddListener(() => PickMember(slot));
            }
            if (TrainConfirm != null)
                TrainConfirm.onClick.AddListener(ConfirmTrain);
            if (TrainBack != null)
                TrainBack.onClick.AddListener(ShowCamp);
            for (int i = 0; i < StudyCards.Length; i++)
            {
                int index = i;
                if (StudyCards[i] != null)
                    StudyCards[i].onClick.AddListener(() => PickCard(index));
            }
            if (StudyConfirm != null)
                StudyConfirm.onClick.AddListener(ConfirmStudy);
            if (StudyBack != null)
                StudyBack.onClick.AddListener(ShowCamp);
            cardHomes = StudyCards
                .Select(card =>
                    card != null ? ((RectTransform)card.transform).anchoredPosition : Vector2.zero
                )
                .ToArray();
        }

        // 押せる物の札（すりガラスのカード）は押されたことを自分で受け取り、下のボタンへ渡さないため、
        // 札を押しても、その物を押したことにする。
        private static void ForwardTags(Button button)
        {
            if (button == null)
                return;
            foreach (var tag in button.GetComponentsInChildren<GlassCard>(true))
                tag.Clicked += () =>
                {
                    if (button.IsActive() && button.IsInteractable())
                        button.onClick.Invoke();
                };
        }

        /// <summary>
        /// Opens the camp with the party (in slot order, null for an empty slot) round the fire.
        /// <paramref name="honedCards"/> are the cards studied already in this adventure
        /// (<see cref="AdventureVitals.CardKey"/>), which are not laid out again.
        /// </summary>
        public void Open(
            IReadOnlyList<BattlePartyMember> party,
            IReadOnlyCollection<string> honedCards,
            Action onRest,
            Action<int> onTrain,
            Action<int, CardSkill> onStudy
        )
        {
            Wire();
            members = party ?? Array.Empty<BattlePartyMember>();
            honed = new HashSet<string>(honedCards ?? Array.Empty<string>());
            rest = onRest;
            train = onTrain;
            study = onStudy;
            for (int slot = 0; slot < Party.Length; slot++)
            {
                var member = MemberAt(slot);
                Paint(Party[slot], member, NightTint);
                if (slot < PartyShadows.Length && PartyShadows[slot] != null)
                    PartyShadows[slot].gameObject.SetActive(member != null);
            }
            ShowCamp();
        }

        public void Close()
        {
            Show(Camp, false);
            Show(Train, false);
            Show(Study, false);
        }

        /// <summary>
        /// The device's back key: from training or studying back to the camp. The camp itself
        /// stays open until one of the three is chosen. False when nothing is open.
        /// </summary>
        public bool HandleBack()
        {
            if (Shown(Train) || Shown(Study))
            {
                ShowCamp();
                return true;
            }
            return Shown(Camp);
        }

        public void ShowCamp()
        {
            Show(Camp, true);
            Show(Train, false);
            Show(Study, false);
        }

        public void PressRest()
        {
            var chosen = rest;
            Close();
            chosen?.Invoke();
        }

        // --- Train ---------------------------------------------------------------------------

        public void OpenTrain()
        {
            PickedMember = -1;
            for (int slot = 0; slot < TrainSlots.Length; slot++)
            {
                var member = MemberAt(slot);
                if (TrainSlots[slot] != null)
                    TrainSlots[slot].gameObject.SetActive(member != null);
                if (member == null)
                    continue;
                if (slot < TrainSprites.Length)
                    Paint(TrainSprites[slot], member, Color.white);
                if (slot < TrainNames.Length && TrainNames[slot] != null)
                    TrainNames[slot].text = member.Name;
            }
            RefreshTrain();
            Show(Camp, false);
            Show(Study, false);
            Show(Train, true);
        }

        public void PickMember(int slot)
        {
            if (MemberAt(slot) == null)
                return;
            PickedMember = slot;
            RefreshTrain();
        }

        public void ConfirmTrain()
        {
            if (PickedMember < 0)
                return;
            int slot = PickedMember;
            var chosen = train;
            Close();
            chosen?.Invoke(slot);
        }

        private void RefreshTrain()
        {
            for (int slot = 0; slot < TrainPedestals.Length; slot++)
                if (TrainPedestals[slot] != null)
                    TrainPedestals[slot].color =
                        slot == PickedMember ? PedestalPicked : PedestalIdle;
            if (TrainTag != null)
            {
                TrainTag.gameObject.SetActive(PickedMember >= 0);
                if (PickedMember >= 0 && PickedMember < TrainSlots.Length)
                    TrainTag.anchoredPosition = new Vector2(
                        ((RectTransform)TrainSlots[PickedMember].transform).anchoredPosition.x,
                        TrainTag.anchoredPosition.y
                    );
            }
            if (TrainConfirm != null)
                TrainConfirm.interactable = PickedMember >= 0;
        }

        // --- Study ---------------------------------------------------------------------------

        public void OpenStudy()
        {
            PickedCard = -1;
            cards.Clear();
            for (int slot = 0; slot < StudySprites.Length; slot++)
            {
                var member = MemberAt(slot);
                StudySprites[slot].gameObject.SetActive(member != null);
                if (slot < StudyNames.Length && StudyNames[slot] != null)
                    StudyNames[slot].text = member?.Name ?? "";
                if (member != null)
                    Paint(StudySprites[slot], member, Color.white);
                var studied = AdventureParty
                    .HonableCards(member)
                    .Where(card => !honed.Contains(AdventureVitals.CardKey(slot, card.Skill.Id)))
                    .Take(CardsPerMember)
                    .ToArray();
                for (int k = 0; k < CardsPerMember; k++)
                {
                    int index = slot * CardsPerMember + k;
                    if (index >= StudyCards.Length)
                        continue;
                    bool used = k < studied.Length;
                    StudyCards[index].gameObject.SetActive(used);
                    cards.Add(used ? (slot, studied[k]) : (slot, null));
                    if (used)
                        ShowCard(index, member, studied[k]);
                }
            }
            RefreshStudy();
            Show(Camp, false);
            Show(Train, false);
            Show(Study, true);
        }

        public void PickCard(int index)
        {
            if (index < 0 || index >= cards.Count || cards[index].Card == null)
                return;
            PickedCard = index;
            RefreshStudy();
        }

        public void ConfirmStudy()
        {
            if (PickedCard < 0)
                return;
            var (slot, card) = cards[PickedCard];
            var chosen = study;
            Close();
            chosen?.Invoke(slot, card.Skill);
        }

        /// <summary>What studying a card makes of it, as the line under the cards shows it.</summary>
        public static string StudyText(CardSkill skill)
        {
            if (skill == null)
                return "";
            var honedSkill = AdventureParty.Honed(skill);
            var from = skill.Powered().FirstOrDefault();
            var to = honedSkill.Powered().FirstOrDefault();
            string power =
                from == null || to == null ? "" : $"　威力 {from.Percent}% → {to.Percent}%";
            return $"{skill.Name} → {honedSkill.Name}{power}";
        }

        private void RefreshStudy()
        {
            for (int i = 0; i < StudyCards.Length; i++)
            {
                bool picked = i == PickedCard;
                if (i < StudyRims.Length && StudyRims[i] != null)
                    StudyRims[i].gameObject.SetActive(picked);
                if (StudyCards[i] != null && i < cardHomes.Length)
                    ((RectTransform)StudyCards[i].transform).anchoredPosition =
                        cardHomes[i] + (picked ? Vector2.up * PickedLift : Vector2.zero);
            }
            if (StudyLine != null)
                StudyLine.text = PickedCard >= 0 ? StudyText(cards[PickedCard].Card.Skill) : "";
            if (StudyConfirm != null)
                StudyConfirm.interactable = PickedCard >= 0;
        }

        private void ShowCard(int index, BattlePartyMember member, BattlePartyCard card)
        {
            if (index >= StudyFaces.Length || StudyFaces[index] == null)
                return;
            var skill = card.Skill;
            int element = (int)skill.Element;
            var data = BattleInspectView.CardData(
                skill,
                0,
                member.Name,
                member.Stats,
                card.Art,
                element >= 0 && element < CardFrames.Length ? CardFrames[element] : null,
                skill.Element == CardElement.None || element >= ElementIcons.Length
                    ? null
                    : ElementIcons[element],
                0
            );
            var face = StudyFaces[index];
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

        // --- Shared --------------------------------------------------------------------------

        private BattlePartyMember MemberAt(int slot) =>
            slot >= 0 && slot < members.Count ? members[slot] : null;

        private static void Paint(RawImage image, BattlePartyMember member, Color tint)
        {
            if (image == null)
                return;
            image.gameObject.SetActive(member != null);
            if (member == null)
                return;
            image.texture = member.Art;
            image.color = member.Tint * tint;
        }

        private static void Show(RectTransform panel, bool shown)
        {
            if (panel != null)
                panel.gameObject.SetActive(shown);
        }

        // 押せる物の後ろの光を、ゆっくり脈打たせる。
        private void Update()
        {
            if (!Shown(Camp))
                return;
            float beat = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            foreach (var glow in Glows)
                if (glow != null)
                    glow.color = new Color(
                        GlowColor.r,
                        GlowColor.g,
                        GlowColor.b,
                        0.08f + 0.22f * beat
                    );
        }
    }
}
