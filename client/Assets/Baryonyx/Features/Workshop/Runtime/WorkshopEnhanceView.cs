using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Economy;
using Baryonyx.Enhancement;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.Library;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Training;
using Baryonyx.UI;
using Baryonyx.UI.Cards;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// The trading house's "enhance" (doc/features/enhancement.md#強化の画面), filling the screen.
    /// The tabs on the left list the owned skills, the equipment and the unlocked unique skills
    /// with their quality, and the stages the unique skills could awaken into. Choose a row, and
    /// the right shows it: the quality and multiplier now and after, the effect's numbers that
    /// change, the quality gauge with the part the stones add, and the polishing stones and how
    /// many (or the shard to awaken). Press the button: the result shows when the server
    /// answers, since the next enhancement starts from it.
    /// </summary>
    public sealed class WorkshopEnhanceView : GuidePanel, IEditModePreview
    {
        public const string LoadFailedMessage = "強化できる物を取得できませんでした";
        public const string FailedMessage = "強化できませんでした";
        public const string PolishLabel = "強化";
        public const string AwakenLabel = "覚醒";

        public enum Tab
        {
            Skills,
            Equipment,
            Uniques,
            Awakening,
        }

        public static readonly string[] TabLabels = { "スキル", "装備", "固有スキル", "覚醒" };

        // 品質強化のタブの順。スキル覚醒は別の項目（AwakeningItemKey）で、覚醒だけのパネルを開く。
        public static readonly Tab[] PolishTabs = { Tab.Skills, Tab.Equipment, Tab.Uniques };
        public static readonly Tab[] AwakenTabs = { Tab.Awakening };

        /// <summary>The tabs this panel shows, in order; <see cref="Tabs"/> has a button for each (none when one).</summary>
        public Tab[] TabOrder = PolishTabs;

        public LibraryData Data;
        public PartyMockData Party;
        public TrainingMockData Training;

        // 絵を置く正方形の一辺（設計座標）。ドット絵は、収まるいちばん大きい整数倍で出す。
        public float ArtBox = 96f;
        public GuideTab[] Tabs = Array.Empty<GuideTab>();
        public ScrollRect List;
        public RectTransform Content;
        public WorkshopGoodRow Template;
        public List<WorkshopGoodRow> Rows = new();
        public TMP_Text Status;

        // 一覧の下：磨き石4色と覚醒の欠片（素材の順）、使う数を決める「－」「＋」。
        public WorkshopMaterialChip[] Chips = Array.Empty<WorkshopMaterialChip>();
        public GameObject Stepper;
        public Button Fewer;
        public Button More;
        public TMP_Text CountLabel;

        public TMP_Text Info;
        public Button Confirm;
        public TMP_Text ConfirmLabel;

        // 右の面：選んだ物の名前・分類と持ち主、強化する前と後のカード（覚醒は覚醒する前と後の段階）。
        public TMP_Text DetailName;
        public TMP_Text DetailSub;

        // スキル・固有スキルは戦闘と同じデッキのカード、装備は品質の色の縁の札で見せる。
        public TrainingCardWidget BeforeCard = new();
        public TrainingCardWidget AfterCard = new();
        public WorkshopGearCard BeforeGear = new();
        public WorkshopGearCard AfterGear = new();
        public GameObject Arrow;

        // カードの下の品質と倍率（「品質C（×1.00）」）。
        public TMP_Text BeforeQuality;
        public TMP_Text AfterQuality;

        // 属性ごとのカードの枠と属性のアイコン（CardElement の順）、装備の札の絵の枠の一辺。
        public Texture[] CardFrames = Array.Empty<Texture>();
        public Sprite[] ElementIcons = Array.Empty<Sprite>();
        public float GearArtBox = 128f;

        // 品質のゲージ：今の位置（白）と、磨き石で上がる所（水色）。境目の印は生成で描いてある。
        public GameObject Gauge;
        public RectTransform GaugeNow;
        public RectTransform GaugeAfter;

        private readonly HashSet<Component> bound = new();
        private IReadOnlyList<EnhancementTarget> skills = Array.Empty<EnhancementTarget>();
        private IReadOnlyList<EnhancementTarget> equipment = Array.Empty<EnhancementTarget>();
        private IReadOnlyList<EnhancementTarget> uniques = Array.Empty<EnhancementTarget>();
        private IReadOnlyList<AwakeningOption> awakenings = Array.Empty<AwakeningOption>();
        private List<PartySkillCard> ownedCards = new();
        private Dictionary<string, string> owners = new();
        private IReadOnlyList<PartyMember> roster = Array.Empty<PartyMember>();
        private Tab tab;
        private int selected = -1;
        private int stone;
        private int count = 1;
        private bool busy;

        public Tab Current => tab;
        public int Selected => selected;
        public int Count => count;
        public EnhancementMaterial Stone => EnhancementMaterials.Polishes[stone];

        // 直前の強化。テストで完了を待つために公開する。
        public Task Work { get; private set; } = Task.CompletedTask;

        /// <summary>The rows of the tab shown: the targets, or the awakening options on the last tab.</summary>
        public IReadOnlyList<EnhancementTarget> Targets =>
            tab switch
            {
                Tab.Skills => skills,
                Tab.Equipment => equipment,
                Tab.Uniques => uniques,
                _ => Array.Empty<EnhancementTarget>(),
            };

        public IReadOnlyList<AwakeningOption> Awakenings => awakenings;

        private int RowCount => tab == Tab.Awakening ? awakenings.Count : Targets.Count;

        private EnhancementTarget SelectedTarget =>
            tab != Tab.Awakening && selected >= 0 && selected < Targets.Count
                ? Targets[selected]
                : null;

        private AwakeningOption SelectedOption =>
            tab == Tab.Awakening && selected >= 0 && selected < awakenings.Count
                ? awakenings[selected]
                : null;

        private void OnEnable()
        {
            BindControls();
            RuneBalance.Changed += RefreshInfo;
            ShowLoading();
            StartLoad(LoadAsync, LoadFailedMessage, ShowLoadFailed);
        }

        private void OnDisable()
        {
            Load.Cancel();
            RuneBalance.Changed -= RefreshInfo;
        }

        private void BindControls()
        {
            for (int i = 0; i < Tabs.Length; i++)
            {
                var item = Tabs[i];
                if (item == null || item.Button == null || !bound.Add(item))
                    continue;
                if (i >= TabOrder.Length)
                    continue;
                var next = TabOrder[i];
                item.Button.onClick.AddListener(() => ShowTab(next));
            }
            for (int i = 0; i < Chips.Length; i++)
            {
                var chip = Chips[i];
                if (chip == null || chip.Card == null || !bound.Add(chip))
                    continue;
                int index = i;
                chip.Card.Clicked += () => ChooseStone(index);
            }
            if (Fewer != null && bound.Add(Fewer))
                Fewer.onClick.AddListener(() => ChangeCount(-1));
            if (More != null && bound.Add(More))
                More.onClick.AddListener(() => ChangeCount(1));
            if (Confirm != null && bound.Add(Confirm))
                Confirm.onClick.AddListener(Enhance);
        }

        private async Task LoadAsync(CancellationToken token)
        {
            // 編成・装備・素材は互いに依存しないため、同時に読む。ログインは AccountSessionRunner が1回にまとめる。
            var reading = PartySession.LoadAsync(Party, PartySession.Source, token);
            var wearing = EquipmentSession.SourceOrLocal.LoadAsync(token);
            var materials = EnhancementSession.LoadAsync(token);
            await Task.WhenAll(reading, wearing, materials);
            if (token.IsCancellationRequested)
                return;
            Use(await reading, PartySession.State, await wearing);
            ShowTab(Array.IndexOf(TabOrder, tab) >= 0 ? tab : TabOrder[0]);
        }

        /// <summary>Takes the party and the equipment as the lists to show.</summary>
        public void Use(PartyFormation formation, PartyState party, EquipmentState gear)
        {
            roster = formation?.Roster ?? Array.Empty<PartyMember>();
            ownedCards = (party?.SkillCards ?? MockCards(roster)).ToList();
            skills = EnhancementTargets.Skills(ownedCards);
            equipment = EnhancementTargets.Equipment(gear);
            owners = OwnersOf(roster, party, gear);
            RefreshUniques();
        }

        // サーバーがないときは、仮データの4人のカードを×1.00で持っている扱いにする。
        private static IEnumerable<PartySkillCard> MockCards(IEnumerable<PartyMember> roster) =>
            roster.SelectMany(member =>
                member.Cards.Select(
                    (card, i) => new PartySkillCard($"mock:{member.Id}:{i}", card.Skill, 1f)
                )
            );

        // 持ち物のIDから、付けている人の名前。
        private static Dictionary<string, string> OwnersOf(
            IReadOnlyList<PartyMember> roster,
            PartyState party,
            EquipmentState gear
        )
        {
            var names = new Dictionary<string, string>();
            foreach (var member in roster)
            {
                foreach (string key in party?.Find(member.Id)?.Cards ?? Array.Empty<string>())
                    if (PartyCardKey.IsCard(key))
                        names[PartyCardKey.IdOf(key)] = member.Name;
            }
            foreach (var item in gear?.Items ?? Array.Empty<EquipmentItem>())
            {
                string wearer = gear.WornBy(item.Id);
                var member = roster.FirstOrDefault(entry => entry.Id == wearer);
                if (member != null)
                    names[item.Id] = member.Name;
            }
            return names;
        }

        private UniqueSkillProgress ProgressOf(string id, int index) =>
            UniqueSkillSession.ProgressOf(
                id,
                index,
                Training != null ? Training.Find(id)?.MockProgressOf(index) : null
            );

        private void RefreshUniques()
        {
            uniques = EnhancementTargets.Uniques(roster, Training, ProgressOf);
            // 固有スキルの段階のあとに、持っているスキルの1枚ずつの2つの枝（doc/features/skill-awakening.md）。
            awakenings = EnhancementTargets
                .Awakenings(roster, Training, ProgressOf)
                .Concat(EnhancementTargets.SkillAwakenings(ownedCards, owners))
                .ToArray();
        }

        /// <summary>Shows the tab's rows, the first chosen.</summary>
        public void ShowTab(Tab next)
        {
            tab = next;
            selected = -1;
            count = 1;
            for (int i = 0; i < Tabs.Length; i++)
                if (Tabs[i] != null)
                    Tabs[i].Chosen = i < TabOrder.Length && TabOrder[i] == tab;
            EnsureRows(RowCount);
            for (int i = 0; i < Rows.Count; i++)
            {
                bool shown = i < RowCount;
                Rows[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                PaintRow(i);
                if (Rows[i].Card != null)
                    Rows[i].Card.Selected = false;
            }
            Set(Status, RowCount == 0 ? EmptyText(tab) : "");
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            Set(ConfirmLabel, tab == Tab.Awakening ? AwakenLabel : PolishLabel);
            if (tab != Tab.Awakening && EnhancementSession.CountOf(Stone.Id) == 0)
                stone = FirstHeldStone();
            // 右の面が空にならないよう、先頭の行を選んでおく。
            if (RowCount > 0)
            {
                selected = 0;
                if (Rows.Count > 0 && Rows[0].Card != null)
                    Rows[0].Card.Selected = true;
            }
            RefreshMaterials();
            RefreshInfo();
        }

        public static string EmptyText(Tab tab) =>
            tab switch
            {
                Tab.Skills => "スキルを持っていません",
                Tab.Equipment => "装備を持っていません",
                Tab.Uniques => "解放した固有スキルがありません",
                _ => "覚醒できる固有スキルがありません",
            };

        public void Select(int index)
        {
            if (busy || index < 0 || index >= RowCount)
                return;
            selected = index;
            count = 1;
            for (int i = 0; i < RowCount && i < Rows.Count; i++)
                if (Rows[i].Card != null)
                    Rows[i].Card.Selected = i == index;
            RefreshMaterials();
            RefreshInfo();
        }

        public void ChooseStone(int index)
        {
            if (
                busy
                || tab == Tab.Awakening
                || index < 0
                || index >= EnhancementMaterials.Polishes.Count
            )
                return;
            stone = index;
            count = 1;
            RefreshMaterials();
            RefreshInfo();
        }

        public void ChangeCount(int delta)
        {
            if (busy)
                return;
            count = Mathf.Clamp(count + delta, 1, Math.Max(1, MaxCount()));
            RefreshMaterials();
            RefreshInfo();
        }

        // 使える数：持っている数までで、最後の1つが何かを上げる数まで。
        private int MaxCount()
        {
            var target = SelectedTarget;
            if (target == null)
                return 1;
            int useful = EnhancementMaterials.UsefulCount(
                target.Multiplier,
                Stone,
                target.MaxMultiplier
            );
            return Math.Min(useful, EnhancementSession.CountOf(Stone.Id));
        }

        private int FirstHeldStone()
        {
            for (int i = 0; i < EnhancementMaterials.Polishes.Count; i++)
                if (EnhancementSession.CountOf(EnhancementMaterials.Polishes[i].Id) > 0)
                    return i;
            return 0;
        }

        /// <summary>
        /// Makes the chosen enhancement, and shows it when the server answers: the row's new
        /// quality, or the awakened stage gone from the list. A failure is told and the lists are
        /// read again.
        /// </summary>
        public void Enhance()
        {
            if (!CanEnhance())
                return;
            Work = tab == Tab.Awakening ? AwakenAsync(SelectedOption) : PolishAsync(SelectedTarget);
        }

        private async Task PolishAsync(EnhancementTarget target)
        {
            var used = Stone;
            int uses = count;
            busy = true;
            RefreshInfo();
            try
            {
                var outcome = await EnhancementSession.PolishAsync(target, used, uses);
                var after = target.WithMultiplier(outcome.Multiplier);
                Replace(target, after);
                ShowNotice(PolishedText(after));
                if (selected >= 0 && selected < Rows.Count && after.Quality > target.Quality)
                {
                    StartCoroutine(Punch(Rows[selected].Price));
                    StartCoroutine(Punch(BeforeQuality));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Failed(exception);
            }
            finally
            {
                busy = false;
                count = 1;
                if (this != null && isActiveAndEnabled)
                {
                    RefreshMaterials();
                    RefreshInfo();
                }
            }
        }

        private async Task AwakenAsync(AwakeningOption option)
        {
            busy = true;
            RefreshInfo();
            try
            {
                var outcome = await EnhancementSession.AwakenAsync(option);
                ShowNotice(AwakenedText(option));
                // 覚醒したスキルの1枚は、もう一覧に出さない。
                if (option.IsSkillCard)
                    ownedCards = ownedCards
                        .Select(card =>
                            card.Id == option.OwnedCard ? card.Awakened(outcome.Stage) : card
                        )
                        .ToList();
                RefreshUniques();
                if (this != null && isActiveAndEnabled)
                    ShowTab(Tab.Awakening);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Failed(exception);
            }
            finally
            {
                busy = false;
                if (this != null && isActiveAndEnabled)
                    RefreshInfo();
            }
        }

        private void Failed(Exception exception)
        {
            Debug.LogWarning(FailedMessage + "。" + exception.Message, this);
            ShowNotice(FailedMessage);
            if (this != null && isActiveAndEnabled)
                StartLoad(LoadAsync, LoadFailedMessage, ShowLoadFailed);
        }

        private void Replace(EnhancementTarget before, EnhancementTarget after)
        {
            IReadOnlyList<EnhancementTarget> Swap(IReadOnlyList<EnhancementTarget> list) =>
                list.Select(entry => ReferenceEquals(entry, before) ? after : entry).ToArray();
            skills = Swap(skills);
            equipment = Swap(equipment);
            uniques = Swap(uniques);
            if (this != null && selected >= 0 && selected < Rows.Count)
                PaintRow(selected);
        }

        private bool CanEnhance()
        {
            if (busy)
                return false;
            if (tab == Tab.Awakening)
            {
                var option = SelectedOption;
                return option != null
                    && EnhancementSession.CountOf(EnhancementMaterials.Shard)
                        >= EnhancementMaterials.ShardsPerStage
                    && Affordable(option.Runes);
            }
            var target = SelectedTarget;
            return target != null
                && !target.AtMax
                && count <= MaxCount()
                && Affordable(Stone.Runes * count);
        }

        // 残高を読めていないときは、サーバーに任せる。
        private static bool Affordable(int runes) =>
            RuneBalance.Known is not { } balance || balance >= runes;

        private void RefreshMaterials()
        {
            bool awakening = tab == Tab.Awakening;
            for (int i = 0; i < Chips.Length; i++)
            {
                var chip = Chips[i];
                if (chip == null)
                    continue;
                var material =
                    i < EnhancementMaterials.All.Count ? EnhancementMaterials.All[i] : null;
                bool shown = material != null && material.IsPolish != awakening;
                chip.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                Set(chip.Count, CountText(EnhancementSession.CountOf(material.Id)));
                if (chip.Card != null)
                    chip.Card.Selected = !awakening && i == stone;
            }
            if (Stepper != null)
                Stepper.SetActive(!awakening);
            Set(CountLabel, count.ToString());
            int max = MaxCount();
            if (Fewer != null)
                Fewer.interactable = !busy && count > 1;
            if (More != null)
                More.interactable = !busy && count < max;
        }

        private void RefreshInfo()
        {
            if (Confirm != null)
                Confirm.interactable = CanEnhance();
            Set(Info, InfoText());
            ShowDetail();
        }

        private void ShowDetail()
        {
            var option = SelectedOption;
            var target = SelectedTarget;
            string name = "",
                sub = "",
                before = "",
                after = "";
            float now = 0f,
                next = 0f;
            bool gauge = false;
            CardSkill beforeCard = null,
                afterCard = null;
            EquipmentDefinition gear = null;
            float beforeMultiplier = 1f,
                afterMultiplier = 1f,
                max = 1f;
            if (option != null)
            {
                // 覚醒は、覚醒する前と後の段階を、今の倍率のカードで並べる。
                var progress = option.IsSkillCard
                    ? null
                    : uniques.FirstOrDefault(entry =>
                        entry.CharacterId == option.CharacterId && entry.Index == option.Index
                    );
                beforeMultiplier = afterMultiplier =
                    progress?.Multiplier ?? (option.IsSkillCard ? option.Multiplier : 1f);
                max = progress?.MaxMultiplier ?? (option.IsSkillCard ? option.MaxMultiplier : 1f);
                beforeCard = option.From;
                afterCard = option.Card;
                name = option.Card?.Name ?? "";
                sub = AwakeningDetail(option);
                before = after = QualityText(
                    Inventory.Quality.Of(beforeMultiplier, max),
                    beforeMultiplier
                );
            }
            else if (target != null)
            {
                name = target.Name;
                sub = DetailOf(target);
                max = target.MaxMultiplier;
                beforeMultiplier = target.Multiplier;
                afterMultiplier = target.AtMax
                    ? target.Multiplier
                    : EnhancementMaterials.Polished(target.Multiplier, Stone, count, max);
                if (target.Category is EnhancementCategory.Skill or EnhancementCategory.Unique)
                    beforeCard = afterCard = CardOf(target);
                else
                    gear = EquipmentCatalog.Find(target.KindId);
                before = QualityText(target.Quality, beforeMultiplier);
                after = target.AtMax
                    ? "上限"
                    : QualityText(Inventory.Quality.Of(afterMultiplier, max), afterMultiplier);
                now = Inventory.Quality.Place(beforeMultiplier, max);
                next = Inventory.Quality.Place(afterMultiplier, max);
                gauge = true;
            }
            bool cards = beforeCard != null;
            bool unique = option != null || target?.Category == EnhancementCategory.Unique;
            ShowCard(BeforeCard, cards ? beforeCard : null, beforeMultiplier, max, unique);
            ShowCard(AfterCard, cards ? afterCard : null, afterMultiplier, max, unique);
            var entry = target != null && gear != null ? EntryOf(target) : null;
            ShowGear(BeforeGear, gear, entry, beforeMultiplier, null);
            ShowGear(
                AfterGear,
                gear,
                entry,
                afterMultiplier,
                gear != null ? gear.DetailAt(beforeMultiplier) : null
            );
            if (Arrow != null)
                Arrow.SetActive(cards || gear != null);
            Set(DetailName, name);
            Set(DetailSub, sub);
            Set(BeforeQuality, before);
            Set(AfterQuality, after);
            if (Gauge != null)
                Gauge.SetActive(gauge);
            Fill(GaugeNow, now);
            Fill(GaugeAfter, next);
        }

        // スキル・固有スキルのカードを、倍率を当てて戦闘と同じ形で出す。null なら隠す。
        private void ShowCard(
            TrainingCardWidget widget,
            CardSkill card,
            float multiplier,
            float max,
            bool unique
        ) =>
            WorkshopSkillCard.Show(
                widget,
                card,
                multiplier,
                max,
                unique,
                Party,
                CardFrames,
                ElementIcons
            );

        // 装備の札：縁を品質の色にし、絵に品質のキラキラを重ね、効果をその倍率の数字で書く。
        // 強化した後の札は、強化する前から変わる数字を「前→後」で書く。
        private void ShowGear(
            WorkshopGearCard card,
            EquipmentDefinition gear,
            LibraryEntryData entry,
            float multiplier,
            string before
        )
        {
            if (card?.Root == null)
                return;
            card.Root.SetActive(gear != null);
            if (gear == null)
                return;
            var quality = gear.QualityOf(multiplier);
            if (card.Rim != null)
                card.Rim.color = QualityPalette.Of(quality);
            LibraryCatalogView.PaintArt(card.Art, entry, true, GearArtBox);
            if (card.Foil != null)
                card.Foil.Show(
                    card.Art != null ? card.Art.texture : null,
                    quality,
                    EquipmentCatalog.FoilStyleOf(gear.Id)
                );
            Set(card.Name, gear.Name);
            Set(card.Quality, QualityText(quality, multiplier));
            string effect = gear.DetailAt(multiplier);
            Set(card.Effect, before != null ? EffectChange(before, effect) : effect);
        }

        private static CardSkill CardOf(EnhancementTarget target) =>
            target.Category == EnhancementCategory.Unique
                ? UniqueSkillTrees.FindStage(target.KindId)?.Card
                : CardSkills.Find(target.KindId);

        private static void Fill(RectTransform bar, float place)
        {
            if (bar != null)
                bar.anchorMax = new Vector2(Mathf.Clamp01(place), bar.anchorMax.y);
        }

        /// <summary>The quality and multiplier now and after the stones, or now at the limit.</summary>
        public static string QualityChange(EnhancementTarget target, float after)
        {
            string now = QualityText(target.Quality, target.Multiplier);
            if (target.AtMax)
                return $"{now}　上限";
            var next = Inventory.Quality.Of(after, target.MaxMultiplier);
            return $"{now} → {QualityText(next, after)}";
        }

        private static readonly System.Text.RegularExpressions.Regex Numbers = new("[0-9]+");

        /// <summary>
        /// The effect after, with each number that changes written as "now→after" in the light
        /// blue of the gain. The texts differ only in their numbers, so they pair up in order.
        /// </summary>
        public static string EffectChange(string now, string after)
        {
            if (string.IsNullOrEmpty(after) || now == after)
                return after ?? "";
            var before = Numbers.Matches(now ?? "");
            int index = 0;
            string changed = Numbers.Replace(
                after,
                match =>
                {
                    string old = index < before.Count ? before[index].Value : match.Value;
                    index++;
                    return old == match.Value
                        ? match.Value
                        : $"{old}→<color=#{GainHex}>{match.Value}</color>";
                }
            );
            return index == before.Count ? changed : after;
        }

        private const string GainHex = "7fe3ff";

        private string InfoText()
        {
            if (tab == Tab.Awakening)
                return SelectedOption is { } option ? AwakeningInfo(option, RuneBalance.Known) : "";
            return SelectedTarget is { } target
                ? PolishInfo(
                    target,
                    Stone,
                    count,
                    EnhancementSession.CountOf(Stone.Id),
                    RuneBalance.Known
                )
                : "";
        }

        /// <summary>
        /// The stones and their runes beside the button, with what is missing.
        /// </summary>
        public static string PolishInfo(
            EnhancementTarget target,
            EnhancementMaterial stone,
            int count,
            int held,
            long? runes
        )
        {
            if (target.AtMax)
                return "上限に届いています";
            int cost = stone.Runes * count;
            string missing =
                held < count ? Missing($"{stone.Name}が足りません")
                : runes is { } balance && balance < cost
                    ? Missing($"ルーンが {cost - balance:N0} 足りません")
                : "";
            return $"{stone.Name}×{count}・{cost:N0} ルーン{missing}";
        }

        /// <summary>The shard and the runes beside the button, with what is missing.</summary>
        public static string AwakeningInfo(AwakeningOption option, long? runes)
        {
            int shards = EnhancementSession.CountOf(EnhancementMaterials.Shard);
            string missing =
                shards < EnhancementMaterials.ShardsPerStage
                    ? Missing($"{EnhancementMaterials.AwakeningShard.Name}が足りません")
                : runes is { } balance && balance < option.Runes
                    ? Missing($"ルーンが {option.Runes - balance:N0} 足りません")
                : "";
            return $"{EnhancementMaterials.AwakeningShard.Name}×{EnhancementMaterials.ShardsPerStage}・{option.Runes:N0} ルーン{missing}";
        }

        private static string Missing(string text) => $"　<color=#ff8a7a>{text}</color>";

        public static string QualityText(ItemQuality quality, float multiplier) =>
            $"<color=#{QualityPalette.Hex(quality)}>品質{Inventory.Quality.Letter(quality)}</color>（{Inventory.Quality.Times(multiplier)}）";

        public static string CountText(int count) => $"×{count}";

        public static string PolishedText(EnhancementTarget target) =>
            $"{target.Name}が品質{Inventory.Quality.Letter(target.Quality)}（{Inventory.Quality.Times(target.Multiplier)}）になった";

        public static string AwakenedText(AwakeningOption option) =>
            $"「{option.From?.Name}」が「{option.Card?.Name}」に覚醒した";

        private void PaintRow(int index)
        {
            var row = Rows[index];
            if (tab == Tab.Awakening)
            {
                var option = awakenings[index];
                Set(row.Name, option.Card?.Name ?? "");
                Set(row.Price, $"{option.Runes:N0} ルーン");
                row.Price.color = UiPalette.Gold;
                Set(row.Detail, AwakeningDetail(option));
                PaintTexture(row.Art, Party != null ? Party.ArtOf(option.Card?.Id) : null, ArtBox);
                return;
            }
            var target = Targets[index];
            PaintTarget(row, target, DetailOf(target), EntryOf(target), ArtBox);
            if (target.Category == EnhancementCategory.Unique)
                PaintTexture(row.Art, Party != null ? Party.ArtOf(target.KindId) : null, ArtBox);
        }

        // ドット絵の挿絵を、枠に収まるいちばん大きい整数倍で出す。
        private static void PaintTexture(RawImage image, Texture2D art, float box)
        {
            if (image == null)
                return;
            image.texture = art;
            image.enabled = art != null;
            if (art == null)
                return;
            image.uvRect = new Rect(0f, 0f, 1f, 1f);
            image.color = Color.white;
            float dot = Mathf.Max(1f, Mathf.Floor(box / Mathf.Max(art.width, art.height)));
            image.rectTransform.sizeDelta = new Vector2(art.width, art.height) * dot;
        }

        /// <summary>Writes a target on a row. The generator bakes the mock targets with it too.</summary>
        public static void PaintTarget(
            WorkshopGoodRow row,
            EnhancementTarget target,
            string detail,
            LibraryEntryData entry,
            float artBox
        )
        {
            Set(row.Name, target.Name);
            Set(row.Price, QualityText(target.Quality, target.Multiplier));
            if (row.Price != null)
            {
                row.Price.richText = true;
                row.Price.color = UiPalette.TextMain;
            }
            Set(row.Detail, detail);
            LibraryCatalogView.PaintArt(row.Art, entry, true, artBox);
        }

        private static string StepText(int stage) =>
            UniqueSkillTreeShape.Step(stage) == 1 ? "1段目へ" : "2段目へ";

        // 覚醒の行と右の面の2行目：誰の何から、どこへ。スキルの1枚は元に戻せない。
        private static string AwakeningDetail(AwakeningOption option) =>
            option.IsSkillCard
                ? $"{option.CharacterName}｜{option.From?.Name}から覚醒（元に戻せない）"
                : $"{option.CharacterName}｜{option.From?.Name}から{StepText(option.Stage)}";

        private string DetailOf(EnhancementTarget target)
        {
            string owner = owners.TryGetValue(target.ItemId ?? "", out string name) ? name : null;
            return target.Category switch
            {
                EnhancementCategory.Unique =>
                    $"{roster.FirstOrDefault(member => member.Id == target.CharacterId)?.Name}の固有スキル",
                EnhancementCategory.Skill => Join(
                    EntryOf(LibraryKind.Skill, target.KindId)?.KindLine,
                    owner != null ? owner + "が装備中" : null
                ),
                _ => Join(
                    EquipmentCatalog.Find(target.KindId) is { } gear
                        ? EquipmentCatalog.NameOf(gear.Slot)
                        : "",
                    owner != null ? owner + "が装備中" : null
                ),
            };
        }

        private LibraryEntryData EntryOf(EnhancementTarget target) =>
            target.Category switch
            {
                EnhancementCategory.Unique => null,
                EnhancementCategory.Skill => EntryOf(LibraryKind.Skill, target.KindId),
                EnhancementCategory.Weapon => EntryOf(LibraryKind.Weapon, target.KindId),
                EnhancementCategory.Armor => EntryOf(LibraryKind.Armor, target.KindId),
                _ => EntryOf(LibraryKind.Accessory, target.KindId),
            };

        private LibraryEntryData EntryOf(LibraryKind kind, string id) =>
            Data != null
                ? Data.Entries.FirstOrDefault(entry =>
                    entry != null && entry.Kind == kind && entry.Id == id
                )
                : null;

        private static string Join(string first, string second) =>
            string.Join("｜", new[] { first, second }.Where(text => !string.IsNullOrEmpty(text)));

        private void EnsureRows(int rows)
        {
            Rows.RemoveAll(row => row == null);
            while (Rows.Count < rows && Template != null && Content != null)
                Rows.Add(Instantiate(Template, Content));
            for (int i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                row.name = "Row" + i;
                if (row.Card == null || !bound.Add(row))
                    continue;
                int index = i;
                row.Card.Clicked += () => Select(index);
            }
        }

        // 品質が上がった瞬間：品質の文字を大きくして戻す。
        private static IEnumerator Punch(TMP_Text label)
        {
            if (label == null)
                yield break;
            var rect = label.rectTransform;
            const float duration = 0.45f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                rect.localScale = Vector3.one * (1f + 0.45f * (1f - k) * (1f - k));
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        private void ShowLoading()
        {
            selected = -1;
            foreach (var row in Rows)
                if (row != null)
                    row.gameObject.SetActive(false);
            Set(Status, LoadingText);
            RefreshMaterials();
            RefreshInfo();
        }

        private void ShowLoadFailed() => Set(Status, LoadFailedText);

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }

        // 停止中の表示の切り替え：焼いた仮のスキル、サーバーから読み込む間、読めなかったとき。
        public IReadOnlyList<string> PreviewStates =>
            new[] { "一覧" }.Concat(GuidePanelLoad.PreviewStates).ToArray();

        public void ShowPreviewState(int index)
        {
            if (index == 0)
            {
                foreach (var row in Rows)
                    if (row != null)
                        row.gameObject.SetActive(true);
                Set(Status, "");
                return;
            }
            foreach (var row in Rows)
                if (row != null)
                    row.gameObject.SetActive(false);
            Set(Status, index == 2 ? LoadFailedText : LoadingText);
        }
    }
}
