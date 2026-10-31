using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat.Presentation;
using Baryonyx.Inventory;
using Baryonyx.UI.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The boons after a won battle (doc/features/stage-progression.md, 戦闘後の強化): the screen
    /// split into one column a member, the member standing large at the top of each and the two
    /// boons offered for the member below. The steps show as the cards' quality does (no step
    /// names): a silver frame for white, the gold frame and A's sparkle for gold, the rainbow
    /// frame and S's sparkle for rainbow. A tap picks a boon: its rim lights, and the member it grows (the four for
    /// everyone) hops and stands in a brighter light. The round check at the bottom right takes
    /// it: the other columns darken and a pillar of light rises over the member it grows (each
    /// member, for everyone), then the boons close. It does not close until one is chosen.
    /// AdventureBoonPickerAssets builds it into the battle's overlay and the exploration screen.
    /// </summary>
    public sealed class AdventureBoonPicker : MonoBehaviour
    {
        // 列の数（編成の人数）。
        public const int Columns = 4;

        // 開くとき、列が順に浮かび上がる間隔と長さ（秒）。
        public const float RiseStagger = 0.07f;
        public const float RiseSeconds = 0.28f;
        public const float RiseFrom = 36f;

        // 選んだ仲間が跳ねる高さ（設計座標）と長さ（秒）。
        public const float HopHeight = 26f;
        public const float HopSeconds = 0.3f;

        // 決めてから閉じるまで、選んだ仲間の列に光の柱が立つ長さ（秒）。
        public const float TakeSeconds = AdventureBoonPillar.Seconds;

        // 決めたとき、ほかの列を暗くする濃さと、暗くなるまで（秒）。
        public const float DimAlpha = 0.35f;
        public const float DimSeconds = 0.15f;

        // 光の柱が伸び切り、選んだ仲間が最も強く光る時刻（秒、決めてから）。
        public const float TakePeak = AdventureBoonPillar.RiseSeconds;

        // 札の枠の画像の大きさ（ドット）。カードの枠と同じ作りで、外側1ドットの暗い縁と、その内側2ドットの金属の線。
        public const int FrameWidth = 117;
        public const int FrameHeight = 50;

        // 段階ごとのキラキラの光り方（CardFoil の番号）と濃さ。金は星屑・金、虹はコスモ・虹。
        private const int GoldStyle = 0;
        private const int RainbowStyle = 7 * 7 + 1;
        private const float GoldFoil = 0.35f;
        private const float RainbowFoil = 0.45f;

        // 枠の金属の線（外側・内側）と暗い縁。白の段階はこのままの銀で描かれる。
        private static readonly Color FrameEdge = new(0.04f, 0.05f, 0.1f, 1f);
        private static readonly Color FrameOuter = new(0.86f, 0.9f, 0.98f, 1f);
        private static readonly Color FrameInner = new(0.62f, 0.68f, 0.82f, 1f);
        private static readonly Color FoilArea = new(0.05f, 0.07f, 0.14f, 1f);
        private static Texture2D frameTexture;
        private static Texture2D foilTexture;

        // 足元の光：ふだんと、選んだ仲間。
        private static readonly Color FloorIdle = new(0.6f, 0.75f, 1f, 0.22f);
        private static readonly Color FloorPicked = new(1f, 0.86f, 0.5f, 0.9f);

        public RectTransform Panel;

        [Tooltip("仲間ごとの列（Columns 個）。開くときに順に浮かび上がる。")]
        public CanvasGroup[] Lanes = Array.Empty<CanvasGroup>();

        [Tooltip("列ごとの仲間の戦闘のドット絵と、足元の光。")]
        public RawImage[] Members = Array.Empty<RawImage>();
        public Graphic[] Floors = Array.Empty<Graphic>();

        [Tooltip("1人のドット絵の、ドット1つの大きさ（設計座標）。")]
        public float Dot = 4f;

        [Tooltip(
            "強化の札（押す範囲。列ごとに PerMember 枚、列の順）と、その文・選んだときの光。"
        )]
        public Button[] Cards = Array.Empty<Button>();
        public TMP_Text[] Texts = Array.Empty<TMP_Text>();
        public GameObject[] Selections = Array.Empty<GameObject>();

        [Tooltip("札の枠（段階で銀・金・虹）と、板の上のキラキラ（金と虹）。")]
        public CardFrameFoil[] Frames = Array.Empty<CardFrameFoil>();
        public CardFoil[] Foils = Array.Empty<CardFoil>();

        [Tooltip("列ごとの、決めたときに仲間の足元から立つ光の柱。")]
        public AdventureBoonPillar[] Pillars = Array.Empty<AdventureBoonPillar>();

        public Button Confirm;

        private IReadOnlyList<AdventureBoon> offers = Array.Empty<AdventureBoon>();
        private IReadOnlyList<BattlePartyMember> members = Array.Empty<BattlePartyMember>();
        private int[] present = Array.Empty<int>();
        private Action<AdventureBoon> chosen;
        private Vector2[] laneHomes = Array.Empty<Vector2>();
        private Vector2[] memberHomes = Array.Empty<Vector2>();
        private float[] hops = Array.Empty<float>();
        private float openedAt;
        private float takenAt;
        private bool taking;
        private bool wired;

        public bool IsOpen => Panel != null && Panel.gameObject.activeSelf;

        /// <summary>The boon picked (its place among the offers), or -1.</summary>
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
            laneHomes = Lanes
                .Select(lane =>
                    lane != null ? ((RectTransform)lane.transform).anchoredPosition : Vector2.zero
                )
                .ToArray();
            memberHomes = Members
                .Select(member => member != null ? member.rectTransform.anchoredPosition : Vector2.zero)
                .ToArray();
            hops = Enumerable.Repeat(-1f, Members.Length).ToArray();
        }

        /// <summary>
        /// Opens with the boons offered (<see cref="AdventureBoons.PerMember"/> for each member
        /// there, in slot order) and the party (slot order, null for an empty slot).
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
            present = AdventureBoons.Present(members);
            chosen = onChosen;
            Picked = -1;
            taking = false;
            for (int column = 0; column < Lanes.Length; column++)
            {
                bool used = column < present.Length;
                if (Lanes[column] != null)
                    Lanes[column].gameObject.SetActive(used);
                if (used)
                    Paint(column, MemberAt(present[column]));
            }
            for (int i = 0; i < Cards.Length; i++)
            {
                bool used = i < offers.Count;
                if (Cards[i] != null)
                    Cards[i].gameObject.SetActive(used);
                if (used)
                    ShowCard(i, offers[i]);
            }
            for (int k = 0; k < hops.Length; k++)
                hops[k] = -1f;
            foreach (var pillar in Pillars)
                if (pillar != null)
                    pillar.Hide();
            openedAt = Time.unscaledTime;
            Refresh();
            if (Panel != null)
                Panel.gameObject.SetActive(true);
            Animate();
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
            StopAllCoroutines();
            taking = false;
            if (Panel != null)
                Panel.gameObject.SetActive(false);
        }

        /// <summary>The device's back key: the boons stay until one is chosen. False when closed.</summary>
        public bool HandleBack() => IsOpen;

        public void Pick(int index)
        {
            if (taking || index < 0 || index >= offers.Count)
                return;
            bool again = index == Picked;
            Picked = index;
            if (!again)
                foreach (int column in ColumnsOf(index))
                    if (column < hops.Length)
                        hops[column] = Time.unscaledTime;
            Refresh();
        }

        /// <summary>
        /// Takes the boon picked: the member it grows flashes, then the boons close and the boon
        /// goes on. With no frames to wait (the screen inactive) it goes on at once.
        /// </summary>
        public void Choose()
        {
            if (taking || Picked < 0 || Picked >= offers.Count)
                return;
            taking = true;
            if (Confirm != null)
                Confirm.interactable = false;
            if (isActiveAndEnabled)
                StartCoroutine(Take(Picked));
            else
                Done(offers[Picked]);
        }

        private IEnumerator Take(int index)
        {
            takenAt = Time.unscaledTime;
            while (Time.unscaledTime - takenAt < TakeSeconds)
            {
                Animate();
                yield return null;
            }
            if (index < Cards.Length && Cards[index] != null)
                Cards[index].transform.localScale = Vector3.one;
            Done(offers[index]);
        }

        /// <summary>
        /// Shows the take of the boon at <paramref name="index"/> as it is
        /// <paramref name="seconds"/> after the check (the screen's preview and stills); nothing
        /// is chosen.
        /// </summary>
        public void ShowTake(int index, float seconds)
        {
            Pick(index);
            if (Picked != index)
                return;
            taking = true;
            takenAt = Time.unscaledTime - seconds;
            Animate();
        }

        // 決めてからの演出：ほかの列を暗くし、上がる仲間の列に光の柱を立て、仲間を白く光らせ、札を少し膨らませる。
        private void AnimateTake(float now)
        {
            float seconds = now - takenAt;
            var lit = ColumnsOf(Picked).ToHashSet();
            float dim = Mathf.Lerp(1f, DimAlpha, Mathf.Clamp01(seconds / DimSeconds));
            for (int column = 0; column < Lanes.Length; column++)
                if (Lanes[column] != null && !lit.Contains(column))
                    Lanes[column].alpha *= dim;
            var rank = offers[Picked].Rank;
            // 柱が伸び切るころに最も強く光り、そのあと元の色へ戻る。
            float flash =
                seconds < TakePeak
                    ? Mathf.Clamp01(seconds / TakePeak)
                    : Mathf.Clamp01(1f - (seconds - TakePeak) / (TakeSeconds - TakePeak));
            foreach (int column in lit)
            {
                if (column < Pillars.Length && Pillars[column] != null)
                    Pillars[column].Show(seconds, rank);
                if (column < Members.Length && Members[column] != null)
                {
                    var tint = MemberAt(present[column])?.Tint ?? Color.white;
                    Members[column].color = Color.Lerp(tint, Color.white * 2f, flash * 0.55f);
                }
            }
            if (Picked < Cards.Length && Cards[Picked] != null)
                Cards[Picked].transform.localScale = Vector3.one * (1f + 0.05f * flash);
        }

        private void Done(AdventureBoon boon)
        {
            var done = chosen;
            chosen = null;
            Close();
            done?.Invoke(boon);
        }

        // その強化で上がる仲間の列：1人ならその仲間の列、全員なら全部の列。
        private IEnumerable<int> ColumnsOf(int index) =>
            offers[index].Everyone
                ? Enumerable.Range(0, present.Length)
                : new[] { Array.IndexOf(present, offers[index].Slot) }.Where(column => column >= 0);

        /// <summary>The words on a boon's card: 全員 first for everyone, then what it changes.</summary>
        public static string TextOf(AdventureBoon boon)
        {
            string description = AdventureBoons.Description(boon);
            return boon.Everyone
                ? $"<color={UiGold}>{AdventureTexts.BoonEveryone}</color>\n{description}"
                : description;
        }

        private const string UiGold = "#FFD27A";

        /// <summary>The cards' quality a step shows as: white C (no sparkle), gold A, rainbow S.</summary>
        public static ItemQuality QualityOf(AdventureBoonRank rank) =>
            rank switch
            {
                AdventureBoonRank.Rainbow => ItemQuality.S,
                AdventureBoonRank.Gold => ItemQuality.A,
                _ => ItemQuality.C,
            };

        /// <summary>
        /// The frame of a boon's card, made the first time it is asked for: the way a card's frame
        /// is drawn, a dark dot at the edge and two dots of metal inside, clear within.
        /// </summary>
        public static Texture2D FrameTexture() =>
            frameTexture != null
                ? frameTexture
                : frameTexture = DotTexture(
                    edge =>
                        edge == 0 ? FrameEdge
                        : edge == 1 ? FrameOuter
                        : edge == 2 ? FrameInner
                        : Color.clear
                );

        /// <summary>Where the sparkle shows on a boon's card: all of it within the frame.</summary>
        public static Texture2D FoilTexture() =>
            foilTexture != null
                ? foilTexture
                : foilTexture = DotTexture(edge => edge >= 3 ? FoilArea : Color.clear);

        // 枠の大きさのテクスチャを、縁からのドットの距離ごとの色で塗る。
        private static Texture2D DotTexture(Func<int, Color> colorAt)
        {
            var texture = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color[FrameWidth * FrameHeight];
            for (int y = 0; y < FrameHeight; y++)
            for (int x = 0; x < FrameWidth; x++)
            {
                int edge = Mathf.Min(
                    Mathf.Min(x, FrameWidth - 1 - x),
                    Mathf.Min(y, FrameHeight - 1 - y)
                );
                pixels[y * FrameWidth + x] = colorAt(edge);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void Update()
        {
            if (IsOpen)
                Animate();
        }

        private void Animate()
        {
            float now = Time.unscaledTime;
            for (int column = 0; column < Lanes.Length; column++)
            {
                if (Lanes[column] == null || column >= laneHomes.Length)
                    continue;
                float t = Mathf.Clamp01((now - openedAt - column * RiseStagger) / RiseSeconds);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                Lanes[column].alpha = eased;
                ((RectTransform)Lanes[column].transform).anchoredPosition =
                    laneHomes[column] + Vector2.down * RiseFrom * (1f - eased);
            }
            for (int column = 0; column < Members.Length; column++)
            {
                if (Members[column] == null || column >= memberHomes.Length)
                    continue;
                float lift = 0f;
                if (column < hops.Length && hops[column] >= 0f)
                {
                    float t = (now - hops[column]) / HopSeconds;
                    if (t < 1f)
                        lift = Mathf.Sin(t * Mathf.PI) * HopHeight;
                }
                Members[column].rectTransform.anchoredPosition =
                    memberHomes[column] + Vector2.up * lift;
            }
            if (taking && Picked >= 0 && Picked < offers.Count)
                AnimateTake(now);
        }

        private void Refresh()
        {
            var lit = Picked >= 0 ? ColumnsOf(Picked).ToHashSet() : new HashSet<int>();
            for (int i = 0; i < Selections.Length; i++)
                if (Selections[i] != null)
                    Selections[i].SetActive(i == Picked);
            for (int column = 0; column < Floors.Length; column++)
                if (Floors[column] != null)
                    Floors[column].color = lit.Contains(column) ? FloorPicked : FloorIdle;
            if (Confirm != null)
                Confirm.interactable = Picked >= 0 && !taking;
        }

        private void ShowCard(int index, AdventureBoon boon)
        {
            if (index < Texts.Length && Texts[index] != null)
                Texts[index].text = TextOf(boon);
            var quality = QualityOf(boon.Rank);
            if (index < Frames.Length && Frames[index] != null)
            {
                Frames[index].Image.texture = FrameTexture();
                Frames[index].Show(quality);
            }
            if (index < Foils.Length && Foils[index] != null)
            {
                bool rainbow = boon.Rank == AdventureBoonRank.Rainbow;
                Foils[index].Show(FoilTexture(), quality, rainbow ? RainbowStyle : GoldStyle);
                Foils[index].Fade(rainbow ? RainbowFoil : GoldFoil);
            }
        }

        // 列の仲間のドット絵を、ドット1つを Dot の大きさで描く。member がなければ隠す。
        private void Paint(int column, BattlePartyMember member)
        {
            if (column >= Members.Length || Members[column] == null)
                return;
            var image = Members[column];
            bool shown = member != null && member.Art != null;
            image.gameObject.SetActive(shown);
            if (!shown)
                return;
            image.texture = member.Art;
            image.color = member.Tint;
            image.rectTransform.sizeDelta = new Vector2(member.Art.width, member.Art.height) * Dot;
        }

        // 停止中の表示と展示室の見本の名前（絵は組み立てで列に置いた4人）。
        private static readonly string[] SampleNames = { "トーマ", "ルカ", "アリア", "ミナ" };

        /// <summary>
        /// Opens with sample boons of every step, a boon for everyone and one with a cost among
        /// them, and the four figures the builder put in the columns as the party (the screen's
        /// preview and the showcase). The rainbow one is picked.
        /// </summary>
        public void ShowSample()
        {
            var party = new BattlePartyMember[SampleNames.Length];
            for (int slot = 0; slot < party.Length; slot++)
                party[slot] = new BattlePartyMember
                {
                    Name = SampleNames[slot],
                    Art =
                        slot < Members.Length && Members[slot] != null
                            ? Members[slot].texture
                            : null,
                };
            var physical = Combat.CardStat.PhysicalAttack;
            var magic = Combat.CardStat.MagicAttack;
            Open(
                new[]
                {
                    new AdventureBoon("hp", AdventureBoonRank.White, 0, physical),
                    new AdventureBoon("party-guard", AdventureBoonRank.Gold, -1, physical),
                    new AdventureBoon("attack", AdventureBoonRank.White, 1, physical),
                    new AdventureBoon("seasoned", AdventureBoonRank.Gold, 1, physical),
                    new AdventureBoon("physical-defense", AdventureBoonRank.White, 2, physical),
                    new AdventureBoon("attack", AdventureBoonRank.Rainbow, 2, physical),
                    new AdventureBoon("glass-blade", AdventureBoonRank.Gold, 3, magic),
                    new AdventureBoon("magic-defense", AdventureBoonRank.White, 3, magic),
                },
                party,
                null
            );
            Pick(5);
            openedAt = -10f;
            Animate();
        }

        private BattlePartyMember MemberAt(int slot) =>
            slot >= 0 && slot < members.Count ? members[slot] : null;
    }
}
