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
    /// with their quality. Choose a row, and
    /// the right shows it: the quality and multiplier now and after, the effect's numbers that
    /// change, the quality gauge with the part the stones add, and the polishing stones and how
    /// many. Press the button: the result shows when the server
    /// answers, since the next enhancement starts from it.
    /// </summary>
    public sealed class WorkshopEnhanceView : GuidePanel, IEditModePreview
    {
        public const string LoadFailedMessage = "強化できる物を取得できませんでした";
        public const string FailedMessage = "強化できませんでした";
        public const string PolishLabel = "強化";

        // 読み込む間に伏せて並べる行の数（一覧の見える所を埋める）。
        public const int CoverRows = 7;

        public enum Tab
        {
            Skills,
            Equipment,
            Uniques,
        }

        public static readonly string[] TabLabels = { "スキル", "装備", "固有スキル" };

        // 品質強化のタブの順。スキル覚醒は別の項目（AwakeningItemKey）で、WorkshopAwakenView を開く。
        public static readonly Tab[] PolishTabs = { Tab.Skills, Tab.Equipment, Tab.Uniques };

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

        // 一覧の下：磨き石4色。札を押すたびにその色を使う数が増え、右上の×でやめる。
        public WorkshopMaterialChip[] Chips = Array.Empty<WorkshopMaterialChip>();

        public TMP_Text Info;
        public Button Confirm;
        public TMP_Text ConfirmLabel;

        // 右の面：選んだ物の名前・分類と持ち主、強化する前と後のカード。
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

        // 選んだ色の上限：そこから先を暗く覆い（Blocked）、上限に石の色の壁の線（Wall）を立てる。金は出さない。
        public RectTransform GaugeBlocked;
        public Image GaugeWall;

        // 選んだ色が足りないが下の色から作れるとき、「強化」で開く確かめの画面。
        public WorkshopCombinePrompt Prompt;

        private readonly HashSet<Component> bound = new();
        private readonly GlassCardCover cover = new();
        private IReadOnlyList<EnhancementTarget> skills = Array.Empty<EnhancementTarget>();
        private IReadOnlyList<EnhancementTarget> equipment = Array.Empty<EnhancementTarget>();
        private IReadOnlyList<EnhancementTarget> uniques = Array.Empty<EnhancementTarget>();
        private List<PartySkillCard> ownedCards = new();
        private Dictionary<string, string> owners = new();
        private IReadOnlyList<PartyMember> roster = Array.Empty<PartyMember>();
        private Tab tab;
        private int selected = -1;
        private int stone;

        // 色ごとの使う数（白・青・紫・金の順）。札を押すたびに1つ増え、低い色から順に使う。
        private readonly int[] uses = new int[EnhancementMaterials.Polishes.Count];
        private bool busy;

        public Tab Current => tab;
        public int Selected => selected;

        /// <summary>The colour last chosen: the one whose ceiling the gauge shows.</summary>
        public EnhancementMaterial Stone => EnhancementMaterials.Polishes[stone];

        /// <summary>How many of the chosen colour are used.</summary>
        public int Count => uses[stone];

        /// <summary>How many of each colour are used, white to gold.</summary>
        public IReadOnlyList<int> Uses => uses;

        // 直前の強化。テストで完了を待つために公開する。
        public Task Work { get; private set; } = Task.CompletedTask;

        /// <summary>The rows of the tab shown.</summary>
        public IReadOnlyList<EnhancementTarget> Targets =>
            tab switch
            {
                Tab.Skills => skills,
                Tab.Equipment => equipment,
                Tab.Uniques => uniques,
                _ => Array.Empty<EnhancementTarget>(),
            };

        private int RowCount => Targets.Count;

        private EnhancementTarget SelectedTarget =>
            selected >= 0 && selected < Targets.Count ? Targets[selected] : null;

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
                if (chip.Cancel != null)
                    chip.Cancel.onClick.AddListener(() => CancelStone(index));
            }
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
        }

        /// <summary>Shows the tab's rows, the first chosen.</summary>
        public void ShowTab(Tab next)
        {
            tab = next;
            selected = -1;
            ClearUses();
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
            // 強化できる物のない場所の伏せた行も、めくってから静かに消す。
            cover.Reveal(
                Rows.Take(RowCount).Select(row => row.gameObject),
                Rows.Skip(RowCount).Take(CoverRows - RowCount).Select(row => row.gameObject)
            );
            if (List != null)
                List.verticalNormalizedPosition = 1f;
            Set(ConfirmLabel, PolishLabel);
            // 右の面が空にならないよう、先頭の行を選んでおく。
            if (RowCount > 0)
            {
                selected = 0;
                if (Rows.Count > 0 && Rows[0].Card != null)
                    Rows[0].Card.Selected = true;
            }
            stone = PreferredStone();
            RefreshMaterials();
            RefreshInfo();
        }

        public static string EmptyText(Tab tab) =>
            tab switch
            {
                Tab.Skills => "スキルを持っていません",
                Tab.Equipment => "装備を持っていません",
                _ => "解放した固有スキルがありません",
            };

        public void Select(int index)
        {
            if (busy || index < 0 || index >= RowCount)
                return;
            selected = index;
            ClearUses();
            for (int i = 0; i < RowCount && i < Rows.Count; i++)
                if (Rows[i].Card != null)
                    Rows[i].Card.Selected = i == index;
            stone = PreferredStone();
            RefreshMaterials();
            RefreshInfo();
        }

        /// <summary>Chooses a colour and uses one more of it, when one more still raises the multiplier and can be had.</summary>
        public void ChooseStone(int index)
        {
            if (busy || index < 0 || index >= EnhancementMaterials.Polishes.Count)
                return;
            stone = index;
            if (CanAdd(index))
                uses[index]++;
            RefreshMaterials();
            RefreshInfo();
        }

        /// <summary>Stops using a colour: its count goes back to none.</summary>
        public void CancelStone(int index)
        {
            if (busy || index < 0 || index >= uses.Length)
                return;
            uses[index] = 0;
            RefreshMaterials();
            RefreshInfo();
        }

        private void ClearUses() => Array.Clear(uses, 0, uses.Length);

        private int UsedCount() => uses.Sum();

        // 1つ増やせるか：増やしても、どの石も何かを上げ（色の上限で無駄にならず）、足りない分を下の色から作れる。
        private bool CanAdd(int index)
        {
            var target = SelectedTarget;
            if (target == null || target.AtMax)
                return false;
            var next = (int[])uses.Clone();
            next[index]++;
            EnhancementMaterials.PolishedAll(
                target.Multiplier,
                next,
                target.MaxMultiplier,
                out bool allRaise
            );
            return allRaise && PlanFor(next) != null;
        }

        // 使う数をそろえるのに足りない分を、下の色から作る段取り（足りていれば空、作れなければ null）。
        private static IReadOnlyList<CombineStep> PlanFor(IReadOnlyList<int> counts) =>
            EnhancementMaterials.CombinePlan(counts, EnhancementSession.CountOf);

        // 使う石と、足りない分を作る変換に払うルーン。
        private int RunesFor(IReadOnlyList<CombineStep> plan)
        {
            int runes = EnhancementMaterials.CombineRunesOf(plan);
            for (int i = 0; i < uses.Length; i++)
                runes += EnhancementMaterials.Polishes[i].Runes * uses[i];
            return runes;
        }

        // 押したときに増える色の初め：今の色を持っていて1つ増やせればそのまま。増やせなければ、持っていて増やせる
        // いちばん低い色、下の色から作って増やせるいちばん低い色、上限に届いていないいちばん低い色の順に替える。
        private int PreferredStone()
        {
            var target = SelectedTarget;
            bool Held(int i) => EnhancementSession.CountOf(EnhancementMaterials.Polishes[i].Id) > 0;
            bool Raises(int i) =>
                target == null
                || EnhancementMaterials.CanRaise(
                    target.Multiplier,
                    EnhancementMaterials.Polishes[i],
                    target.MaxMultiplier
                );
            if (Held(stone) && CanAdd(stone))
                return stone;
            int colors = EnhancementMaterials.Polishes.Count;
            foreach (
                var pick in new Func<int, bool>[] { i => Held(i) && CanAdd(i), CanAdd, Raises }
            )
                for (int i = 0; i < colors; i++)
                    if (pick(i))
                        return i;
            return stone;
        }

        /// <summary>
        /// Makes the chosen enhancement, and shows it when the server answers: the row's new
        /// quality. A failure is told and the lists are read again.
        /// </summary>
        public void Enhance()
        {
            if (!CanEnhance())
                return;
            var target = SelectedTarget;
            var counts = (int[])uses.Clone();
            var plan = PlanFor(counts);
            // 足りない色は、確かめの画面で下の色から作ってから使う。
            if (plan is { Count: > 0 } && Prompt != null)
            {
                Prompt.Open(plan, RunesFor(plan), () => Work = PolishAsync(target, counts, plan));
                return;
            }
            Work = PolishAsync(target, counts, null);
        }

        private async Task PolishAsync(
            EnhancementTarget target,
            IReadOnlyList<int> counts,
            IReadOnlyList<CombineStep> plan
        )
        {
            busy = true;
            RefreshInfo();
            try
            {
                // 足りない色を、下の色から順に作る。作った石は、強化に失敗しても残る。
                foreach (var step in plan ?? Array.Empty<CombineStep>())
                    await EnhancementSession.CombineAsync(step.Made, step.Count);
                // 選んだ石を、低い色から1色ずつ使う。
                var after = target;
                for (int i = 0; i < counts.Count; i++)
                {
                    if (counts[i] <= 0)
                        continue;
                    var outcome = await EnhancementSession.PolishAsync(
                        after,
                        EnhancementMaterials.Polishes[i],
                        counts[i]
                    );
                    after = after.WithMultiplier(outcome.Multiplier);
                }
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
                ClearUses();
                if (this != null && isActiveAndEnabled)
                {
                    RefreshMaterials();
                    RefreshInfo();
                }
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
            var target = SelectedTarget;
            if (target == null || target.AtMax || UsedCount() == 0)
                return false;
            EnhancementMaterials.PolishedAll(
                target.Multiplier,
                uses,
                target.MaxMultiplier,
                out bool allRaise
            );
            var plan = PlanFor(uses);
            return allRaise && plan != null && Affordable(RunesFor(plan));
        }

        // 残高を読めていないときは、サーバーに任せる。
        private static bool Affordable(int runes) =>
            RuneBalance.Known is not { } balance || balance >= runes;

        private void RefreshMaterials()
        {
            var target = SelectedTarget;
            for (int i = 0; i < Chips.Length; i++)
            {
                var chip = Chips[i];
                if (chip == null)
                    continue;
                var material =
                    i < EnhancementMaterials.Polishes.Count
                        ? EnhancementMaterials.Polishes[i]
                        : null;
                bool shown = material != null;
                chip.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                int used = material.IsPolish && i < uses.Length ? uses[i] : 0;
                Set(chip.Count, UseText(used, EnhancementSession.CountOf(material.Id)));
                if (chip.Card != null)
                {
                    // 使う色を選んだ印にし、もう1つも増やせない色（上限に届いた・作れない）は暗くする。
                    chip.Card.Selected = used > 0;
                    chip.Card.Dimmed = target != null && !target.AtMax && used == 0 && !CanAdd(i);
                }
                if (chip.Cancel != null)
                    chip.Cancel.gameObject.SetActive(!busy && used > 0);
            }
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
            var target = SelectedTarget;
            string name = "",
                sub = "",
                before = "",
                after = "";
            float now = 0f,
                next = 0f,
                ceiling = 1f;
            bool gauge = false;
            CardSkill beforeCard = null,
                afterCard = null;
            EquipmentDefinition gear = null;
            float beforeMultiplier = 1f,
                afterMultiplier = 1f,
                max = 1f;
            if (target != null)
            {
                name = target.Name;
                sub = DetailOf(target);
                max = target.MaxMultiplier;
                beforeMultiplier = target.Multiplier;
                afterMultiplier = target.AtMax
                    ? target.Multiplier
                    : EnhancementMaterials.PolishedAll(target.Multiplier, uses, max, out _);
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
                ceiling = Inventory.Quality.Place(
                    EnhancementMaterials.CeilingPercent(Stone, max) / 100f,
                    max
                );
                gauge = true;
            }
            bool cards = beforeCard != null;
            bool unique = target?.Category == EnhancementCategory.Unique;
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
            ShowCeiling(gauge && ceiling < 1f ? ceiling : 1f, now, StoneColor(Stone));
        }

        // 選んだ色の上限の位置に壁の線を立て、そこから先（今の位置より先）を赤みで覆う。上限が種類の上限（1）なら隠す。
        private void ShowCeiling(float place, float now, Color color)
        {
            bool shown = place < 1f;
            if (GaugeBlocked != null)
            {
                GaugeBlocked.gameObject.SetActive(shown);
                GaugeBlocked.anchorMin = new Vector2(Mathf.Clamp01(Mathf.Max(place, now)), 0f);
                GaugeBlocked.anchorMax = Vector2.one;
            }
            if (GaugeWall != null)
            {
                GaugeWall.gameObject.SetActive(shown);
                GaugeWall.color = color;
                var rect = GaugeWall.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(Mathf.Clamp01(place), 0.5f);
            }
        }

        /// <summary>The colour of a stone, for the wall of its ceiling on the gauge.</summary>
        public static Color StoneColor(EnhancementMaterial stone) =>
            stone?.Id switch
            {
                EnhancementMaterials.Blue => new Color(0.42f, 0.71f, 1f),
                EnhancementMaterials.Purple => new Color(0.78f, 0.55f, 1f),
                EnhancementMaterials.Gold => new Color(1f, 0.84f, 0.4f),
                _ => new Color(0.93f, 0.95f, 0.98f),
            };

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
            return SelectedTarget is { } target
                ? PolishInfo(
                    target,
                    Stone,
                    uses,
                    EnhancementSession.CountOf,
                    RuneBalance.Known,
                    PlanFor(uses)
                )
                : "";
        }

        /// <summary>
        /// The stones and their runes beside the button ("白い磨き石×3・青い磨き石×2・700 ルーン"),
        /// how many are made from the colours below, and what is missing. With none chosen yet, why
        /// the chosen colour cannot be used, if it cannot.
        /// </summary>
        public static string PolishInfo(
            EnhancementTarget target,
            EnhancementMaterial chosen,
            IReadOnlyList<int> uses,
            Func<string, int> held,
            long? runes,
            IReadOnlyList<CombineStep> plan
        )
        {
            if (target.AtMax)
                return "上限に届いています";
            var parts = new List<string>();
            int cost = EnhancementMaterials.CombineRunesOf(plan);
            int made = 0;
            for (int i = 0; i < uses.Count && i < EnhancementMaterials.Polishes.Count; i++)
            {
                if (uses[i] <= 0)
                    continue;
                var stone = EnhancementMaterials.Polishes[i];
                parts.Add($"{stone.Name}×{uses[i]}");
                cost += stone.Runes * uses[i];
                made += Math.Max(0, uses[i] - held(stone.Id));
            }
            if (parts.Count == 0)
            {
                if (!EnhancementMaterials.CanRaise(target.Multiplier, chosen, target.MaxMultiplier))
                    return CeilingText(chosen);
                return
                    held(chosen.Id) == 0
                    && EnhancementMaterials.CombinePlan(chosen, 1, held) == null
                    ? Missing($"{chosen.Name}が足りません").TrimStart('　')
                    : "";
            }
            string combined = made > 0 && plan != null ? $"（{made}個は合成）" : "";
            string missing =
                runes is { } balance && balance < cost
                    ? Missing($"ルーンが {cost - balance:N0} 足りません")
                    : "";
            return $"{string.Join("・", parts)}{combined}・{cost:N0} ルーン{missing}";
        }

        /// <summary>What the button's line says when the chosen colour cannot raise the thing past its ceiling.</summary>
        public static string CeilingText(EnhancementMaterial stone) =>
            stone.Ceiling is { } ceiling
                ? $"{stone.Name}で上げられるのは品質{Inventory.Quality.Letter(ceiling)}まで"
                : "上限に届いています";

        private static string Missing(string text) => $"　<color=#ff8a7a>{text}</color>";

        public static string QualityText(ItemQuality quality, float multiplier) =>
            $"<color=#{QualityPalette.Hex(quality)}>品質{Inventory.Quality.Letter(quality)}</color>（{Inventory.Quality.Times(multiplier)}）";

        public static string CountText(int count) => $"×{count}";

        /// <summary>A stone's chip: how many are held ("×8"), and how many are used when any ("3/8", the used in the gain's light blue).</summary>
        public static string UseText(int used, int held) =>
            used > 0 ? $"<color=#{GainHex}>{used}</color>/{held}" : CountText(held);

        public static string PolishedText(EnhancementTarget target) =>
            $"{target.Name}が品質{Inventory.Quality.Letter(target.Quality)}（{Inventory.Quality.Times(target.Multiplier)}）になった";

        private void PaintRow(int index)
        {
            var row = Rows[index];
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

        // 読み込むまで、一覧の見える所をカードの裏で埋める。
        private void ShowLoading()
        {
            selected = -1;
            EnsureRows(CoverRows);
            for (int i = 0; i < Rows.Count; i++)
                Rows[i].gameObject.SetActive(i < CoverRows);
            cover.Cover(Rows.Take(CoverRows).Select(row => row.gameObject));
            Set(Status, "");
            RefreshMaterials();
            RefreshInfo();
        }

        private void ShowLoadFailed()
        {
            cover.Clear();
            foreach (var row in Rows)
                if (row != null)
                    row.gameObject.SetActive(false);
            Set(Status, LoadFailedText);
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }

        // 停止中の表示の切り替え：焼いた仮のスキル、サーバーから読み込む間、読めなかったとき。
        public IReadOnlyList<string> PreviewStates =>
            new[] { "一覧", "色の上限", "足りない石を作る" }
                .Concat(GuidePanelLoad.PreviewStates)
                .ToArray();

        public void ShowPreviewState(int index)
        {
            if (index == 0)
            {
                cover.Clear();
                foreach (var row in Rows)
                    if (row != null)
                        row.gameObject.SetActive(true);
                Set(Status, "");
                return;
            }
            if (Prompt != null)
                Prompt.Close();
            if (index == 1)
            {
                ShowCeilingState();
                return;
            }
            if (index == 2)
            {
                ShowCombineState();
                return;
            }
            ShowLoading();
            if (index == 4)
                ShowLoadFailed();
        }

        // 仮の素材（金1個・紫2個・青4個）で、品質Cの先頭のスキルの金の札を2回押して「強化」を押す。
        // 足りない金1個を紫3個から、足りない紫1個を青3個から作る確かめの画面が開く。
        private void ShowCombineState()
        {
            if (EnhancementSession.Source == null)
                _ = EnhancementSession.LoadAsync(CancellationToken.None);
            cover.Clear();
            Use(PartyFormation.From(Party), null, null);
            ShowTab(Tab.Skills);
            if (skills.Count == 0)
                return;
            Select(0);
            ChooseStone(EnhancementMaterials.Polishes.Count - 1);
            ChooseStone(EnhancementMaterials.Polishes.Count - 1);
            Enhance();
        }

        // 仮の編成のスキルの先頭を品質B（ゲージの55%）にして選び、白い磨き石を選ぶ。白の札が暗くなり、
        // ゲージのBの入口に白い壁の線が立って、その先が暗く覆われる。
        private void ShowCeilingState()
        {
            // 停止中はサーバーがなく、仮の素材がその場で読める。
            if (EnhancementSession.Source == null)
                _ = EnhancementSession.LoadAsync(CancellationToken.None);
            cover.Clear();
            Use(PartyFormation.From(Party), null, null);
            ShowTab(Tab.Skills);
            if (skills.Count == 0)
                return;
            var first = skills[0];
            float place = 0.55f;
            float multiplier = (float)
                Math.Round(
                    Inventory.Quality.MinMultiplier
                        + (first.MaxMultiplier - Inventory.Quality.MinMultiplier) * place,
                    2
                );
            Replace(first, first.WithMultiplier(multiplier));
            Select(0);
            ChooseStone(0);
        }
    }
}
