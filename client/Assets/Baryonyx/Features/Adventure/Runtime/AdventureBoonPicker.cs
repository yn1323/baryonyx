using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat.Presentation;
using Baryonyx.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The boons after a won battle (doc/features/stage-progression.md, 戦闘後の強化): three cards
    /// shaped as the battle's cards side by side, the frame whitish, gold or rainbow by the step
    /// (no step names), the member grown in the art window (the four for everyone), the member's
    /// name and the boon's name on the bands and what it changes in the description. A tap lifts
    /// a card and lights it, and 選ぶ takes it. It does not close until one is chosen.
    /// AdventureBoonPickerAssets builds it into the battle's overlay and the exploration screen.
    /// </summary>
    public sealed class AdventureBoonPicker : MonoBehaviour
    {
        // 全員の強化で、挿絵の窓に並べる人数。
        public const int Figures = 4;

        // 選んだカードを持ち上げる高さ（設計座標）。
        public const float PickedLift = 28f;

        // 段階ごとの挿絵の窓の背景の光（白・金・虹）。
        private static readonly Color[] Backdrops =
        {
            new(0.85f, 0.9f, 1f, 0.55f),
            new(1f, 0.8f, 0.35f, 0.7f),
            new(0.85f, 0.6f, 1f, 0.75f),
        };

        public RectTransform Panel;

        [Tooltip("3枚のカード（押す範囲）と、その表。")]
        public Button[] Cards = Array.Empty<Button>();
        public BattleInspectCardView[] Faces = Array.Empty<BattleInspectCardView>();

        [Tooltip("選んだカードを囲む光（Cardsと同じ順）。")]
        public Graphic[] Rims = Array.Empty<Graphic>();

        [Tooltip(
            "カードごとの1人の絵と、全員の Figures 人の絵（カード1枚に 1 + Figures 枚、Cardsの順）。"
        )]
        public RawImage[] Members = Array.Empty<RawImage>();

        [Tooltip("1人の絵と全員の絵の、ドット1つの大きさ（設計座標）。")]
        public float SingleDot = 3f;
        public float PartyDot = 2f;

        public Button Confirm;

        [Tooltip("段階ごとのカードの枠（白・金・虹の順）と、挿絵の窓の背景の光。")]
        public Texture[] Frames = Array.Empty<Texture>();
        public Texture Backdrop;

        private IReadOnlyList<AdventureBoon> offers = Array.Empty<AdventureBoon>();
        private IReadOnlyList<BattlePartyMember> members = Array.Empty<BattlePartyMember>();
        private Action<AdventureBoon> chosen;
        private Vector2[] homes = Array.Empty<Vector2>();
        private bool wired;

        public bool IsOpen => Panel != null && Panel.gameObject.activeSelf;

        /// <summary>The card picked (its place among the offers), or -1.</summary>
        public int Picked { get; private set; } = -1;

        public IReadOnlyList<AdventureBoon> Offers => offers;

        private void Awake() => Wire();

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
            for (int i = 0; i < Cards.Length; i++)
            {
                int index = i;
                if (Cards[i] != null)
                    Cards[i].onClick.AddListener(() => Pick(index));
            }
            if (Confirm != null)
                Confirm.onClick.AddListener(Choose);
            homes = Cards
                .Select(card =>
                    card != null ? ((RectTransform)card.transform).anchoredPosition : Vector2.zero
                )
                .ToArray();
        }

        /// <summary>
        /// Opens with the boons offered and the party (slot order, null for an empty slot).
        /// <paramref name="onChosen"/> gets the boon taken.
        /// </summary>
        public void Open(
            IReadOnlyList<AdventureBoon> boons,
            IReadOnlyList<BattlePartyMember> party,
            Action<AdventureBoon> onChosen
        )
        {
            Wire();
            offers = boons ?? Array.Empty<AdventureBoon>();
            members = party ?? Array.Empty<BattlePartyMember>();
            chosen = onChosen;
            Picked = -1;
            for (int i = 0; i < Cards.Length; i++)
            {
                bool used = i < offers.Count;
                if (Cards[i] != null)
                    Cards[i].gameObject.SetActive(used);
                if (used)
                    ShowCard(i, offers[i]);
            }
            Refresh();
            if (Panel != null)
                Panel.gameObject.SetActive(true);
        }

        /// <summary>
        /// Opens the boons <paramref name="run"/> still waits for: those of its room's won battle,
        /// kept before the win was saved (<see cref="AdventureVitals.AwaitBoon"/>). The boon
        /// chosen is kept for the adventure before <paramref name="onChosen"/>. False when the room
        /// waits for none, and then nothing opens.
        /// </summary>
        public bool OpenPending(
            AdventureRun run,
            IReadOnlyList<BattlePartyMember> party,
            Action<AdventureBoon> onChosen
        )
        {
            var pending = AdventureVitals.PendingBoon(run?.Id);
            if (run == null || pending is not { } waiting || waiting.RoomId != run.RoomId)
                return false;
            var boons = AdventureBoons.Offer(
                run.Map?.Seed ?? 0,
                waiting.RoomId,
                waiting.Battle,
                AdventureVitals.BoonPity(run.Id),
                party
            );
            if (boons.Count == 0)
            {
                AdventureVitals.AwaitBoon(run.Id, run.RoomId, AdventureBoonBattle.None);
                return false;
            }
            bool rainbow = boons.Any(boon => boon.Rank == AdventureBoonRank.Rainbow);
            Open(
                boons,
                party,
                boon =>
                {
                    AdventureVitals.ChooseBoon(run.Id, boon, rainbow);
                    onChosen?.Invoke(boon);
                }
            );
            return true;
        }

        public void Close()
        {
            if (Panel != null)
                Panel.gameObject.SetActive(false);
        }

        /// <summary>The device's back key: the cards stay until one is chosen. False when closed.</summary>
        public bool HandleBack() => IsOpen;

        public void Pick(int index)
        {
            if (index < 0 || index >= offers.Count)
                return;
            Picked = index;
            Refresh();
        }

        public void Choose()
        {
            if (Picked < 0 || Picked >= offers.Count)
                return;
            var boon = offers[Picked];
            var done = chosen;
            chosen = null;
            Close();
            done?.Invoke(boon);
        }

        /// <summary>The look of the frame for a step: whitish, gold or rainbow.</summary>
        public static ItemQuality QualityOf(AdventureBoonRank rank) =>
            rank switch
            {
                AdventureBoonRank.Rainbow => ItemQuality.S,
                AdventureBoonRank.Gold => ItemQuality.A,
                _ => ItemQuality.C,
            };

        /// <summary>The name on the card's large band: the member grown, or 全員.</summary>
        public string MemberName(AdventureBoon boon) =>
            boon.Everyone ? "全員" : MemberAt(boon.Slot)?.Name ?? "";

        private void Refresh()
        {
            for (int i = 0; i < Cards.Length; i++)
            {
                bool picked = i == Picked;
                if (i < Rims.Length && Rims[i] != null)
                    Rims[i].gameObject.SetActive(picked);
                if (Cards[i] != null && i < homes.Length)
                    ((RectTransform)Cards[i].transform).anchoredPosition =
                        homes[i] + (picked ? Vector2.up * PickedLift : Vector2.zero);
            }
            if (Confirm != null)
                Confirm.interactable = Picked >= 0;
        }

        private void ShowCard(int index, AdventureBoon boon)
        {
            if (index < Faces.Length && Faces[index] != null)
            {
                var face = Faces[index];
                face.Show(
                    MemberName(boon),
                    "",
                    boon.Template?.Name ?? "",
                    AdventureBoons.Description(boon),
                    Backdrop,
                    (int)boon.Rank < Frames.Length ? Frames[(int)boon.Rank] : null,
                    null,
                    0,
                    QualityOf(boon.Rank)
                );
                face.Art.color = Backdrops[(int)boon.Rank];
                if (face.CostDigit != null && face.CostDigit.transform.parent != null)
                    face.CostDigit.transform.parent.gameObject.SetActive(false);
                face.SetPlayable(true, 0f);
            }
            int first = index * (1 + Figures);
            var people = boon.Everyone
                ? Enumerable
                    .Range(0, members.Count)
                    .Select(MemberAt)
                    .Where(m => m != null)
                    .ToArray()
                : new[] { MemberAt(boon.Slot) };
            Paint(first, boon.Everyone ? null : people[0], SingleDot);
            for (int k = 0; k < Figures; k++)
                Paint(
                    first + 1 + k,
                    boon.Everyone && k < people.Length ? people[k] : null,
                    PartyDot
                );
        }

        // 仲間のドット絵を、ドット1つを dot の大きさで描く。member がなければ隠す。
        private void Paint(int index, BattlePartyMember member, float dot)
        {
            if (index >= Members.Length || Members[index] == null)
                return;
            var image = Members[index];
            bool shown = member != null && member.Art != null;
            image.gameObject.SetActive(shown);
            if (!shown)
                return;
            image.texture = member.Art;
            image.color = member.Tint;
            image.rectTransform.sizeDelta = new Vector2(member.Art.width, member.Art.height) * dot;
        }

        // 停止中の表示と展示室の見本：白（アリアの攻撃）・金（全員の守り）・虹（トーマのガラスの刃）。
        private static readonly string[] SampleNames = { "トーマ", "ルカ", "アリア", "ミナ" };

        /// <summary>
        /// Opens with three sample boons, one of each step, and the four figures the builder put
        /// on the first card as the party (the screen's preview and the showcase).
        /// </summary>
        public void ShowSample()
        {
            var party = new BattlePartyMember[SampleNames.Length];
            for (int slot = 0; slot < party.Length; slot++)
            {
                int index = 1 + slot;
                party[slot] = new BattlePartyMember
                {
                    Name = SampleNames[slot],
                    Art =
                        index < Members.Length && Members[index] != null
                            ? Members[index].texture
                            : null,
                };
            }
            Open(
                new[]
                {
                    new AdventureBoon(
                        "attack",
                        AdventureBoonRank.White,
                        2,
                        Combat.CardStat.PhysicalAttack
                    ),
                    new AdventureBoon(
                        "party-guard",
                        AdventureBoonRank.Gold,
                        -1,
                        Combat.CardStat.PhysicalAttack
                    ),
                    new AdventureBoon(
                        "glass-blade",
                        AdventureBoonRank.Rainbow,
                        0,
                        Combat.CardStat.MagicAttack
                    ),
                },
                party,
                null
            );
        }

        private BattlePartyMember MemberAt(int slot) =>
            slot >= 0 && slot < members.Count ? members[slot] : null;
    }
}
