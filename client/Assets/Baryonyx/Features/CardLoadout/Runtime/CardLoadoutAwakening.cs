using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using Baryonyx.Economy;
using Baryonyx.Enhancement;
using Baryonyx.Inventory;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.UI;
using Baryonyx.UI.Buttons;
using Baryonyx.UI.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.CardLoadout
{
    /// <summary>
    /// One stage of an owned skill card's awakening on the 「覚醒」 tab: the card before it
    /// awakens, or one of its two branches.
    /// </summary>
    public sealed class CardBranch
    {
        // 0 が覚醒する前の1枚、1・2 が枝。
        public int Branch { get; set; }
        public CardSkill Card { get; set; }

        // 持っている（この1枚がその枝へ覚醒した、別の1枚がその枝を取った）段階だけ、中身を見せる。
        public bool Held { get; set; }

        // この1枚の今の段階（覚醒していなければ覚醒する前の1枚、していればその枝）。
        public bool Set { get; set; }

        // 覚醒先に選べる（まだ覚醒していない1枚の枝）。
        public bool Open { get; set; }
    }

    /// <summary>
    /// The adventurer's 「覚醒」 tab (doc/features/screens/loadout.md#覚醒) over the card skills
    /// tab's six rows and tree (<see cref="CardLoadoutView"/> with this set): a unique skill shows
    /// its tree, and a custom skill its card and its two branches. Only the stages held show
    /// their card; the others stay sealed. The stages it can awaken into (the next of an
    /// acquired stage; either branch of a card not yet awakened) take a tap, the one tapped is
    /// chosen, and the shard and the runes it takes show at the bottom left. 「覚醒」 awakens it
    /// with the trading house's effect: the card rises from the row on the left, cracks, and the
    /// stage appears (doc/features/skill-evolution/effects.md#覚醒のエフェクト).
    /// </summary>
    public sealed class CardLoadoutAwakening : MonoBehaviour
    {
        public const string FailedMessage = "覚醒できませんでした";
        public const string AwakenLabel = "覚醒";

        // 覚醒した段階を大きく見せておく秒数と、見せる大きさ（商会のスキル覚醒と同じ）。
        public const float RevealSeconds = 1.6f;
        public const float RevealScale = 2.5f;

        public CardLoadoutView View;

        // カスタムスキルを選んだときの、覚醒する前の1枚と2つの枝（その順）。固有スキルは View.Tree。
        public CardLoadoutTreeWidget Branches = new();

        // 右下：払う物の札（覚醒の欠片の「使う数/持っている数」とルーン）と「覚醒」。
        public GameObject CostCard;
        public TMP_Text ShardCount;
        public TMP_Text RuneCost;
        public Button Confirm;

        // 覚醒の演出：画面を覆う幕、戦闘と同じ覚醒の演出と、行から浮き上がるカード。
        public GameObject Scene;
        public BattleSkillVfx Vfx;
        public BattleInspectCardView Origin;

        // 浮き上がるカードの、属性ごとの枠と属性の絵（CardElement の順）。
        public Texture[] CardFrames = Array.Empty<Texture>();
        public Texture[] ElementArts = Array.Empty<Texture>();

        private readonly ButtonBindings bindings = new();
        private CancellationTokenSource reading;
        private CardLoadoutState state;

        // 今出しているツリーの段階ごとの覚醒先（覚醒できない段階はnull）。
        private AwakeningOption[] options = Array.Empty<AwakeningOption>();
        private bool branches;
        private int picked = -1;
        private bool busy;

        // 素材を読んだか。読むまでは、足りない数を赤くしない。
        private bool materialsRead;

        /// <summary>Whether an awakening runs (the effect plays, or the server is awaited).</summary>
        public bool Busy => busy;

        /// <summary>The stage chosen to awaken into, or null.</summary>
        public AwakeningOption Picked =>
            picked >= 0 && picked < options.Length ? options[picked] : null;

        /// <summary>The stages of the tree shown that can be awakened into (null where not).</summary>
        public IReadOnlyList<AwakeningOption> Options => options;

        // 直前の覚醒。テストで完了を待つために公開する。
        public Task Work { get; private set; } = Task.CompletedTask;

        private void OnEnable()
        {
            for (int i = 0; i < View.Tree.Stages.Length; i++)
            {
                int stage = i;
                bindings.Bind(View.Tree.Stages[i].Card.Button, () => Pick(stage, false));
            }
            for (int i = 0; i < Branches.Stages.Length; i++)
            {
                int stage = i;
                bindings.Bind(Branches.Stages[i].Card.Button, () => Pick(stage, true));
            }
            bindings.Bind(Confirm, Awaken);
            RuneBalance.Changed += RefreshCost;
            if (Scene != null)
                Scene.SetActive(false);
            busy = false;
            materialsRead = false;
            ReadMaterials();
        }

        private void OnDisable()
        {
            bindings.Clear();
            RuneBalance.Changed -= RefreshCost;
            reading?.Cancel();
            reading = null;
            StopAllCoroutines();
            busy = false;
            if (Vfx != null && Application.isPlaying)
                Vfx.Clear();
        }

        // 持っている覚醒の欠片を読む。読めなくても、払うときにサーバーが確かめる。
        private async void ReadMaterials()
        {
            reading?.Cancel();
            reading = new CancellationTokenSource();
            var token = reading.Token;
            try
            {
                await EnhancementSession.LoadAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("覚醒の欠片を取得できませんでした。" + exception.Message, this);
                return;
            }
            if (this == null || token.IsCancellationRequested)
                return;
            materialsRead = true;
            RefreshCost();
        }

        /// <summary>
        /// The opening choice of the six: what the skill or awakening tab chose when moved from
        /// it, otherwise the first unique skill.
        /// </summary>
        public static (int Slot, int Unique) Opened()
        {
            string left = PartySession.LeftTab;
            bool carried =
                left == CardLoadoutSession.GuideItemKey
                && (CardLoadoutSession.LastUnique >= 0 || CardLoadoutSession.LastSlot >= 0);
            return carried ? (CardLoadoutSession.LastSlot, CardLoadoutSession.LastUnique) : (-1, 0);
        }

        /// <summary>Hides the tree, the cost and the button while the party is read.</summary>
        public void Hide()
        {
            state = null;
            options = Array.Empty<AwakeningOption>();
            picked = -1;
            ShowTrees(false, false);
            RefreshCost();
        }

        /// <summary>
        /// Shows the awakening of the card chosen on the left: the unique skill's tree, or the
        /// custom skill's card and branches, keeping the stage chosen when it can still be
        /// awakened into, otherwise the first that can.
        /// </summary>
        public void Show(CardLoadoutState next)
        {
            var keep =
                state != null
                && next != null
                && state.PersonId == next.PersonId
                && state.Unique == next.Unique
                && state.Slot == next.Slot
                    ? Picked
                    : null;
            state = next;
            options = Array.Empty<AwakeningOption>();
            picked = -1;
            if (next == null)
            {
                Hide();
                return;
            }
            if (next.Unique >= 0 && next.Tree != null)
                ShowUnique(next);
            else if (next.Slot >= 0 && next.Slot < (next.Slots?.Count ?? 0))
                ShowCard(next);
            else
                ShowTrees(false, false);
            int kept = keep != null ? Array.FindIndex(options, option => Same(option, keep)) : -1;
            picked = kept >= 0 ? kept : Array.FindIndex(options, option => option != null);
            PaintPicked();
            RefreshCost();
        }

        private static bool Same(AwakeningOption a, AwakeningOption b) =>
            a != null
            && b != null
            && a.CharacterId == b.CharacterId
            && a.Index == b.Index
            && a.OwnedCard == b.OwnedCard
            && a.Stage == b.Stage;

        // 固有スキル：presenter のツリーに、覚醒できる段階を足して出す。
        private void ShowUnique(CardLoadoutState shown)
        {
            ShowTrees(true, false);
            var tree = shown.Tree;
            var store = new CardLoadoutSessionStore(View.Party, View.Training);
            var uniques = store.UniquesOf(shown.PersonId);
            var made =
                uniques != null && shown.Unique < uniques.Count
                    ? uniques[shown.Unique]?.Tree
                    : null;
            branches = false;
            options = new AwakeningOption[View.Tree.Stages.Length];
            CardLoadoutView.ShowTreeTitle(View.Tree, tree.Name);
            for (int i = 0; i < View.Tree.Stages.Length; i++)
            {
                var stage = i < tree.Stages.Count ? tree.Stages[i] : null;
                var widget = View.Tree.Stages[i];
                CardLoadoutView.ShowStage(
                    widget,
                    stage,
                    tree,
                    View.ArtOf(stage?.Card?.Id),
                    View.IconOf(stage?.Card?.Element ?? CardElement.None)
                );
                bool open = CanAwakenTo(tree, made, i);
                if (open)
                    options[i] = new AwakeningOption
                    {
                        CharacterId = shown.PersonId,
                        CharacterName = shown.Name,
                        Index = shown.Unique,
                        Stage = i,
                        Tree = made,
                    };
                ShowOpen(widget, stage != null && stage.Acquired, open);
            }
        }

        // カスタムスキル：持っているスキルの1枚なら、覚醒する前の1枚と2つの枝を出し、まだ覚醒していなければ選べる。
        // 持ち物を読んでいない仮データのスキル（展示室など）は、まだ覚醒していない1枚として形だけ出す（選べない）。
        // アイテムと空いた枠は、覚醒させられないので何も出さない。
        private void ShowCard(CardLoadoutState shown)
        {
            var slot = shown.Slots[shown.Slot];
            var owned = PartySession.State?.SkillCardOf(slot?.Id);
            if (owned != null)
            {
                ShowBranches(
                    owned,
                    PartySession.State.SkillCards,
                    true,
                    shown.PersonId,
                    shown.Name
                );
                return;
            }
            bool mock =
                slot?.Id != null
                && slot.ItemId == null
                && !PartyCardKey.IsCard(slot.Id)
                && CardSkills.Find(slot.Id) != null
                && !CardSkillAwakenings.IsAwakened(slot.Id);
            if (!mock)
            {
                ShowTrees(false, false);
                return;
            }
            var card = new PartySkillCard("mock", slot.Id, Quality.MinMultiplier);
            ShowBranches(card, null, false, shown.PersonId, shown.Name);
        }

        // 覚醒する前の1枚と2つの枝。awakens なら、覚醒できる枝を覚醒先にする。
        private void ShowBranches(
            PartySkillCard owned,
            IEnumerable<PartySkillCard> held,
            bool awakens,
            string person,
            string name
        )
        {
            ShowTrees(false, true);
            branches = true;
            var stages = BranchesOf(owned, held);
            var root = stages.FirstOrDefault()?.Card;
            CardLoadoutView.ShowTreeTitle(Branches, root?.Name ?? "");
            var tree = new CardLoadoutTreeState { Name = root?.Name ?? "" };
            options = new AwakeningOption[Branches.Stages.Length];
            for (int i = 0; i < Branches.Stages.Length; i++)
            {
                var branch = i < stages.Count ? stages[i] : null;
                var widget = Branches.Stages[i];
                widget.Card.Button.gameObject.SetActive(branch != null);
                if (branch == null)
                    continue;
                var card =
                    branch.Held && branch.Card != null
                        ? CardLoadoutPresenter.StateOf(branch.Card)
                        : null;
                CardLoadoutView.ShowStage(
                    widget,
                    new CardLoadoutStageState
                    {
                        Stage = i,
                        Parent = i == 0 ? -1 : 0,
                        Acquired = card != null,
                        Set = branch.Set,
                        Card = card,
                    },
                    tree,
                    View.ArtOf(card?.Id),
                    View.IconOf(card?.Element ?? CardElement.None)
                );
                bool open = awakens && branch.Open;
                if (open)
                    options[i] = new AwakeningOption
                    {
                        CharacterId = person,
                        CharacterName = name,
                        OwnedCard = owned.Id,
                        SkillId = owned.SkillId,
                        Stage = branch.Branch,
                        Multiplier = owned.Multiplier,
                        MaxMultiplier = CardSkillQuality.LimitOf(CardSkills.Find(owned.SkillId)),
                    };
                ShowOpen(widget, card != null, open);
            }
        }

        /// <summary>
        /// Whether the unique skill's tree can awaken into <paramref name="stage"/>: unlocked, the
        /// stage made, not acquired, and the stage before it acquired.
        /// </summary>
        public static bool CanAwakenTo(CardLoadoutTreeState tree, UniqueSkillTree made, int stage)
        {
            if (tree == null || tree.Locked || made == null || stage <= 0)
                return false;
            if (stage >= tree.Stages.Count || made.StageAt(stage) == null)
                return false;
            int parent = UniqueSkillTreeShape.Parent(stage);
            return !tree.Stages[stage].Acquired
                && parent >= 0
                && parent < tree.Stages.Count
                && tree.Stages[parent].Acquired;
        }

        /// <summary>
        /// The owned skill <paramref name="card"/> before it awakens and its two branches. A
        /// branch shows its card when held: this card awakened into it, or another of
        /// <paramref name="owned"/> of the same skill did. A card not yet awakened can take
        /// either branch.
        /// </summary>
        public static IReadOnlyList<CardBranch> BranchesOf(
            PartySkillCard card,
            IEnumerable<PartySkillCard> owned
        )
        {
            if (card == null)
                return Array.Empty<CardBranch>();
            var taken = new HashSet<int>(
                (owned ?? Array.Empty<PartySkillCard>())
                    .Where(other => other.SkillId == card.SkillId && other.AwakenedBranch > 0)
                    .Select(other => other.AwakenedBranch)
            );
            if (card.AwakenedBranch > 0)
                taken.Add(card.AwakenedBranch);
            var list = new List<CardBranch>
            {
                new()
                {
                    Branch = 0,
                    Card = CardSkills.Find(card.SkillId)?.WithMultiplier(card.Multiplier),
                    Held = true,
                    Set = card.AwakenedBranch == 0,
                },
            };
            var next = CardSkillAwakenings.NextStages(card.SkillId);
            for (int i = 0; i < next.Count; i++)
            {
                int branch = i + 1;
                list.Add(
                    new CardBranch
                    {
                        Branch = branch,
                        Card = next[i]?.WithMultiplier(card.Multiplier),
                        Held = taken.Contains(branch),
                        Set = card.AwakenedBranch == branch,
                        Open = card.AwakenedBranch == 0,
                    }
                );
            }
            return list;
        }

        // 覚醒できる段階は押せる。取得していない段階のうち覚醒できない物は、封じたまま薄くする。
        private static void ShowOpen(CardLoadoutStageWidget widget, bool held, bool open)
        {
            var button = widget.Card.Button;
            if (button == null)
                return;
            button.interactable = held || open;
            if (button.TryGetComponent(out GlassCard glass))
                glass.Dimmed = !held && !open;
        }

        private void ShowTrees(bool unique, bool card)
        {
            if (View.Tree.Root != null)
                View.Tree.Root.SetActive(unique);
            if (Branches.Root != null)
                Branches.Root.SetActive(card);
        }

        // 選んでいる覚醒先を金の枠で囲む。ほかの段階の枠は消す（セットしている段階は「セット中」でわかる）。
        private void PaintPicked()
        {
            var stages = branches ? Branches.Stages : View.Tree.Stages;
            for (int i = 0; i < stages.Length; i++)
                if (stages[i].Card.Selected != null)
                    stages[i].Card.Selected.SetActive(i == picked);
        }

        /// <summary>Chooses the stage at <paramref name="index"/> of the tree shown, when it can be awakened into.</summary>
        public void Pick(int index, bool branch)
        {
            if (busy || branch != branches || index < 0 || index >= options.Length)
                return;
            if (options[index] == null || index == picked)
                return;
            picked = index;
            PaintPicked();
            RefreshCost();
        }

        private static int Shards => EnhancementSession.CountOf(EnhancementMaterials.Shard);

        private bool CanAwaken =>
            !busy
            && Picked is { } option
            && Shards >= EnhancementMaterials.ShardsPerStage
            && (RuneBalance.Known is not { } balance || balance >= option.Runes);

        private void RefreshCost()
        {
            var option = Picked;
            if (CostCard != null)
                CostCard.SetActive(option != null);
            if (Confirm != null)
                Confirm.interactable = CanAwaken;
            if (option == null)
                return;
            bool shards = !materialsRead || Shards >= EnhancementMaterials.ShardsPerStage;
            bool runes = RuneBalance.Known is not { } balance || balance >= option.Runes;
            Set(ShardCount, $"{EnhancementMaterials.ShardsPerStage}/{Shards}");
            if (ShardCount != null)
                ShardCount.color = shards ? UiPalette.TextMain : Short;
            Set(RuneCost, $"{option.Runes:N0}");
            if (RuneCost != null)
                RuneCost.color = runes ? UiPalette.TextMain : Short;
        }

        // 足りない数の色（商会の品質強化・スキル覚醒と同じ赤）。
        private static readonly Color Short = new(1f, 0.54f, 0.48f);

        /// <summary>Awakens into the stage chosen, with the effect, when the shard and the runes are enough.</summary>
        public void Awaken()
        {
            if (!CanAwaken || state == null)
                return;
            var option = Picked;
            busy = true;
            var done = new TaskCompletionSource<bool>();
            Work = done.Task;
            RefreshCost();
            StartCoroutine(Run(option, done));
        }

        private IEnumerator Run(AwakeningOption option, TaskCompletionSource<bool> done)
        {
            var from = FromOf(option);
            var caster = CasterOf();
            bool deep = !option.IsSkillCard && UniqueSkillTreeShape.Step(option.Stage) == 2;
            bool effect = Vfx != null && Origin != null && Scene != null;
            if (effect)
            {
                Scene.SetActive(true);
                var card = (RectTransform)Origin.transform;
                card.SetParent(Vfx.EvolutionLayer, false);
                // カードは左の行から浮き上がるため、戦闘のようにキャラの足元に魔法陣や光を置かず、画面の中央で割る。
                Vfx.GroundUnderCaster = false;
                Vfx.EvolveAtCenter = true;
                Vfx.RevealSeconds = RevealSeconds;
                Vfx.RevealScale = RevealScale;
                ShowOrigin(from);
                yield return Vfx.EvolutionOmen(card, caster, deep, ArtColor(from));
                yield return Vfx.EvolutionPresent();
            }
            // カードが浮いたまま、サーバーの答えを待つ。
            var request = EnhancementSession.AwakenAsync(option);
            while (!request.IsCompleted)
                yield return null;
            if (request.IsFaulted || request.IsCanceled)
            {
                var error = request.Exception?.GetBaseException();
                Debug.LogWarning(FailedMessage + "。" + error?.Message, this);
                View.ShowNotice(FailedMessage);
                Finish();
                // 覚醒できなかったら、編成と素材を読み直す。
                View.Reload();
                ReadMaterials();
                done.TrySetResult(false);
                yield break;
            }
            var next = option.IsSkillCard
                ? option.Card?.WithMultiplier(option.Multiplier)
                : option.Card;
            if (effect)
                // 割れて覚醒した段階が大きく現れ、読める間だけ見せてから、左の行へ飛び込む。
                yield return Vfx.EvolutionBurst(ArtColor(next), deep, () => ShowOrigin(next));
            if (next != null)
                View.ShowNotice(AwakenedText(next.Name));
            Finish();
            View.Refresh();
            done.TrySetResult(true);
        }

        private void Finish()
        {
            busy = false;
            if (Vfx != null && Application.isPlaying)
                Vfx.Clear();
            if (Scene != null)
                Scene.SetActive(false);
            RefreshCost();
        }

        public static string AwakenedText(string card) => $"{card}に覚醒した";

        // 覚醒する前のカード：固有スキルは今セットしている段階、スキルは持っている1枚。
        private CardSkill FromOf(AwakeningOption option) =>
            option.IsSkillCard
                ? CardSkills.Find(option.SkillId)?.WithMultiplier(option.Multiplier)
                : option.From;

        // カードが浮き上がる所：左で選んでいる行の挿絵。
        private RectTransform CasterOf()
        {
            var rows = state.Unique >= 0 ? View.UniqueRows : View.SlotRows;
            int index = state.Unique >= 0 ? state.Unique : state.Slot;
            var art = index >= 0 && index < rows.Length ? rows[index].Card.Art : null;
            return art != null ? art.rectTransform : (RectTransform)View.transform;
        }

        private void ShowOrigin(CardSkill card)
        {
            if (Origin == null || card == null)
                return;
            var owner = View.Party != null ? View.Party.Find(state?.PersonId) : null;
            Origin.Show(
                card.Name,
                owner?.Name ?? "",
                CardText.KindLine(card),
                CardText.Description(card),
                View.ArtOf(card.Id),
                At(CardFrames, (int)card.Element),
                At(ElementArts, (int)card.Element),
                card.Cost,
                card.Quality,
                card.FoilStyle
            );
            Origin.SetPlayable(true, 0f);
        }

        // 演出の光の色：カードの挿絵の色（挿絵がなければスキルごとの色）。
        private Color ArtColor(CardSkill card) =>
            CardArtColor.Of(View.ArtOf(card?.Id), BattleSkillVfx.ColorOf(card?.Id ?? ""));

        private static T At<T>(T[] items, int index)
            where T : class =>
            items != null && index >= 0 && index < items.Length ? items[index] : null;

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }

        // --- Previews in the editor ---------------------------------------------------------

        // 停止中の表示の切り替え：最初の表示は、先頭の人の1つ目の固有スキルのツリー。
        public const string CardPreviewState = "スキルを選んだとき";

        /// <summary>
        /// The stopped editor's view of the first person's first custom skill: the card and its
        /// two sealed branches, the first chosen, as if the card were owned and not awakened.
        /// </summary>
        public void ShowCardPreview()
        {
            var member = PartyFormation.From(View.Party).TabOrder().People.FirstOrDefault();
            var skill = member?.Cards.FirstOrDefault()?.Skill;
            if (skill == null)
                return;
            ShowBranches(
                new PartySkillCard("preview", skill, Quality.MinMultiplier),
                null,
                true,
                member.Id,
                member.Name
            );
            picked = Array.FindIndex(options, option => option != null);
            PaintPicked();
            RefreshCost();
        }
    }
}
