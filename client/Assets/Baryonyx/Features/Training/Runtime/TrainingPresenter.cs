using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.Party;
using Baryonyx.UI.Cards;
using UnityEngine;

namespace Baryonyx.Training
{
    /// <summary>One passive or unique skill as the training shows it.</summary>
    public sealed class TrainingSkillState
    {
        // 「パッシブ」または「固有スキル」。
        public string Type { get; internal set; }
        public string Name { get; internal set; }
        public string Description { get; internal set; }

        // 固有スキルのエネルギー。パッシブは0。
        public int Energy { get; internal set; }
        public int UnlockLevel { get; internal set; }
        public bool Unlocked { get; internal set; }
        public Texture2D Icon { get; internal set; }
    }

    /// <summary>
    /// A card as the battle draws it, for the screens that lay deck cards out whole (the shop):
    /// a unique skill (fixed, unlocked by level) or a custom skill (a skill the player sets).
    /// </summary>
    public sealed class TrainingCardState
    {
        // スキルのID。固有スキルと、空いているカスタムスキルの枠ではnull。
        public string Id { get; internal set; }

        // カードの上の行。「固有スキル」または「スキル」。
        public string Type { get; internal set; } = "";
        public string Name { get; internal set; } = "";

        // 種類と対象の行（「攻撃・敵単体」、色付き）と、数字に色を付けた説明。
        public string Kind { get; internal set; } = "";
        public string Description { get; internal set; } = "";

        // コスト（固有スキルはエネルギー）。
        public int Cost { get; internal set; }

        /// <summary>The card's quality (S, A, B or C): how much it sparkles.</summary>
        public ItemQuality Quality { get; internal set; } = ItemQuality.C;

        /// <summary>How the card sparkles: its kind's style.</summary>
        public int FoilStyle { get; internal set; }

        // カードの枠の色を決める属性と、属性のアイコン。属性のないカードはアイコンがnull。
        public CardElement Element { get; internal set; }
        public Sprite Icon { get; internal set; }

        // 挿絵（64×58のドット絵）。
        public Texture2D Art { get; internal set; }

        // まだ解放していない固有スキル。
        public bool Locked { get; internal set; }
        public int UnlockLevel { get; internal set; }

        public bool Filled => Id != null || Type == UniqueType;

        /// <summary>
        /// A card for another screen that shows deck cards the same way (the card skills change
        /// screen): <paramref name="id"/> null with <see cref="SkillType"/> is an empty slot.
        /// </summary>
        public static TrainingCardState Of(
            string id,
            string type,
            string name,
            string kind,
            string description,
            int cost,
            ItemQuality quality,
            int foilStyle,
            CardElement element,
            Sprite icon,
            Texture2D art,
            bool locked = false,
            int unlockLevel = 0
        ) =>
            new()
            {
                Id = id,
                Type = type ?? "",
                Name = name ?? "",
                Kind = kind ?? "",
                Description = description ?? "",
                Cost = cost,
                Quality = quality,
                FoilStyle = foilStyle,
                Element = element,
                Icon = icon,
                Art = art,
                Locked = locked,
                UnlockLevel = unlockLevel,
            };

        public const string UniqueType = "固有スキル";
        public const string SkillType = "スキル";
    }

    /// <summary>Everything the training shows, recomputed after every input.</summary>
    public sealed class TrainingState
    {
        // --- 詳細 ---
        public PartyMember Member { get; internal set; }
        public string Name { get; internal set; }
        public int Level { get; internal set; }
        public bool Maxed { get; internal set; }
        public bool CanSwitch { get; internal set; }

        // 使えるカードの属性のアイコン（重ねずに、カードの順）。
        public IReadOnlyList<Sprite> Elements { get; internal set; }
        public CharacterStats Stats { get; internal set; }

        // パッシブ2つ、続けて固有スキル2つ。
        public IReadOnlyList<TrainingSkillState> Skills { get; internal set; }
        public IReadOnlyList<TrainingSkillState> Passives { get; internal set; }

        public long Runes { get; internal set; }

        // 1レベル上げるのに要るルーン。上限なら0。
        public long NextCost { get; internal set; }

        // --- レベルアップ（詳細の上に重ねて開く） ---
        public bool DialogOpen { get; internal set; }
        public int Count { get; internal set; }
        public int Target { get; internal set; }
        public CharacterStats TargetStats { get; internal set; }
        public long Cost { get; internal set; }
        public bool CanAfford { get; internal set; }
        public bool CanLess { get; internal set; }
        public bool CanMore { get; internal set; }
        public bool CanConfirm { get; internal set; }

        // 目標のレベルまでに覚えるスキル。上げたあとの知らせに出す。
        public IReadOnlyList<TrainingSkillState> Learned { get; internal set; }

        // --- 限界突破（突破のタブ。コードと保存では bond と呼ぶ） ---
        public int Bond { get; internal set; }
        public int BondTokens { get; internal set; }
        public bool BondMaxed { get; internal set; }

        // 次の段へ突破できる（しるしがあり、上限でなく、重ねた画面や演出を出していない）。
        public bool CanDeepen { get; internal set; }

        // 深めた直後の演出で灯す段。0なら出さない。
        public int Celebrated { get; internal set; }
    }

    /// <summary>
    /// The adventurer's stats tab of the formation: one character at a time (a flick walks the
    /// party, then the others, and the people's column picks one), with their stats (raised by
    /// what they wear) and passives. "レベルアップ" opens a dialog
    /// over the page that shows what the chosen levels change before any rune is spent; back
    /// closes the dialog first. A level-up shows a notice that asks nothing. With a
    /// <c>levelUp</c> function the level and runes change in the store at once and the server
    /// raises the level behind it (put back when it failed, which
    /// <see cref="Networking.SaveQueue"/> tells on the notice band); otherwise (the showcase) the
    /// store keeps them in memory. The breakthrough tab lists the bond's stages; "限界突破" spends
    /// a token to raise it one stage at once and shows the stage lighting up over the page until
    /// it is closed (doc/features/monetization.md).
    /// </summary>
    public sealed class TrainingPresenter : IDisposable
    {
        public const string LevelUpFailedMessage = "レベルアップできませんでした";
        public const string DeepenFailedMessage = "限界突破できませんでした";
        public const string PassiveType = "パッシブ";
        public const string EmptyName = "空き";

        private readonly ITrainingView view;
        private readonly IReadOnlyList<TrainingMember> roster;
        private readonly ITrainingStore store;

        // スキルのIDから属性のアイコンを引く。属性のないカードはnull。
        private readonly Func<string, Sprite> iconOf;

        // キャラのID・今のLv・上げたあとのLvでレベルアップを保存する。すぐに store へ当て、
        // 保存が済む（失敗なら戻す）と終わる。表示は store から読み直す。
        private readonly Func<string, int, int, Task> levelUp;

        // キャラのIDと深めたあとの段で、絆を深めて保存する。すぐに store へ当て、保存が済む（失敗なら戻す）と終わる。
        // なければ store だけで深める（サーバーがないとき）。
        private readonly Func<string, int, Task> deepenBond;

        // キャラのIDとステータスから、付けている装備で上げたステータスを出す（doc/features/equipment.md#装備の効果）。
        // なければ装備なし。
        private readonly Func<string, CharacterStats, CharacterStats> wear;
        private int index;
        private bool dialog;
        private int celebrated;
        private int count = 1;
        private bool disposed;

        public TrainingPresenter(
            ITrainingView view,
            IReadOnlyList<TrainingMember> roster,
            ITrainingStore store,
            Func<string, Sprite> iconOf = null,
            Func<string, int, int, Task> levelUp = null,
            Func<string, int, Task> deepenBond = null,
            Func<string, CharacterStats, CharacterStats> wear = null
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.roster = roster ?? Array.Empty<TrainingMember>();
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.iconOf = iconOf ?? (_ => null);
            this.levelUp = levelUp;
            this.deepenBond = deepenBond;
            this.wear = wear ?? ((_, stats) => stats);
            index = Math.Max(0, IndexOf(store.Selected));
            view.PrevPressed += Previous;
            view.NextPressed += Next;
            view.LevelUpPressed += OpenDialog;
            view.LessPressed += Less;
            view.MorePressed += More;
            view.MaxPressed += Max;
            view.CancelPressed += CloseDialog;
            view.ConfirmPressed += Confirm;
            view.BreakThroughPressed += Deepen;
            view.CelebrationClosed += CloseCelebration;
            Refresh();
        }

        public TrainingState State { get; private set; }

        // 直前のレベルアップの保存。テストで完了を待つために公開する。
        public Task ConfirmTask { get; private set; } = Task.CompletedTask;

        // 直前に絆を深めた保存。テストで完了を待つために公開する。
        public Task DeepenTask { get; private set; } = Task.CompletedTask;

        private TrainingMember Current => roster.Count > 0 ? roster[index] : null;

        // 重ねた画面（レベルアップ）か、突破した演出を出している。
        private bool Busy => dialog || celebrated > 0;

        public void Previous() => Step(-1);

        public void Next() => Step(1);

        private void Step(int delta)
        {
            if (disposed || Busy || roster.Count < 2)
                return;
            index = (index + delta + roster.Count) % roster.Count;
            store.Selected = Current.Id;
            Refresh();
        }

        /// <summary>Shows the character <paramref name="id"/> (pressed in the people's column).</summary>
        public void Select(string id)
        {
            int at = IndexOf(id);
            if (disposed || Busy || at < 0 || at == index)
                return;
            index = at;
            store.Selected = Current.Id;
            Refresh();
        }

        public void OpenDialog()
        {
            if (disposed || Busy || Current == null || State.Maxed)
                return;
            dialog = true;
            count = 1;
            Refresh();
        }

        public void CloseDialog()
        {
            if (disposed || !dialog)
                return;
            dialog = false;
            Refresh();
        }

        /// <summary>
        /// Back closes the breakthrough's lighting up, or the dialog over the page; true when it
        /// did, so the guide screen stays.
        /// </summary>
        public bool Back()
        {
            if (disposed || !Busy)
                return false;
            if (celebrated > 0)
                CloseCelebration();
            else
                CloseDialog();
            return true;
        }

        // 突破のタブの「限界突破」。しるしを使ってその場で1段上げ、灯った段の演出を出す。
        // サーバーがあれば、保存を待たずに演出を出し、保存できなかったら演出を閉じて戻した段を出す。
        public void Deepen()
        {
            if (disposed || State == null || !State.CanDeepen)
                return;
            string id = Current.Id;
            int target = State.Bond + 1;
            if (deepenBond == null)
            {
                if (!store.DeepenBond(id))
                    return;
                celebrated = store.BondOf(id);
                Refresh();
                return;
            }
            var saved = deepenBond(id, target);
            celebrated = target;
            Refresh();
            DeepenTask = AfterDeepenAsync(saved, id, target);
        }

        private async Task AfterDeepenAsync(Task saved, string id, int target)
        {
            await saved;
            if (disposed)
                return;
            if (celebrated == target && store.BondOf(id) < target)
                celebrated = 0;
            Refresh();
        }

        public void CloseCelebration()
        {
            if (disposed || celebrated == 0)
                return;
            celebrated = 0;
            Refresh();
        }

        public void Less()
        {
            if (disposed || !dialog || count <= 1)
                return;
            count--;
            Refresh();
        }

        public void More()
        {
            if (disposed || !dialog || count >= LevelsLeft)
                return;
            count++;
            Refresh();
        }

        // 所持ルーンで上げられるだけ上げる数にする。1つも上げられなければ1のまま（足りない量を見せる）。
        public void Max()
        {
            if (disposed || !dialog)
                return;
            int level = store.LevelOf(Current.Id);
            int affordable = TrainingRules.Affordable(
                level,
                store.Runes,
                store.MaxLevel,
                store.CostPerLevel
            );
            count = Mathf.Clamp(affordable, 1, Math.Max(1, LevelsLeft));
            Refresh();
        }

        public void Confirm() => ConfirmTask = ConfirmAsync();

        // すぐにレベルを上げてルーンを使い、サーバーがあればそのあとで保存する。
        private async Task ConfirmAsync()
        {
            if (disposed || !dialog || !State.CanConfirm)
                return;
            var member = Current;
            int level = State.Level;
            int target = State.Target;
            var learned = State.Learned;
            string message = LevelUpMessage(member.Member.Name, target, learned);
            if (levelUp == null)
            {
                store.Spend(State.Cost);
                store.SetLevel(member.Id, target);
                dialog = false;
                view.ShowNotice(message);
                Refresh();
                return;
            }

            // levelUp はすぐに store へ当てるので、保存を待たずにダイアログを閉じて知らせる。
            var saved = levelUp(member.Id, level, target);
            dialog = false;
            view.ShowNotice(message);
            Refresh();
            await saved;
            if (disposed)
                return;
            // 保存したレベル（失敗なら戻したレベルとルーン）で描き直す。
            Refresh();
        }

        public static string LevelUpMessage(
            string name,
            int level,
            IReadOnlyList<TrainingSkillState> learned
        ) =>
            learned == null || learned.Count == 0
                ? $"{name}が Lv {level} になりました"
                : $"{name}が Lv {level} になり、"
                    + string.Join("と", learned.Select(skill => $"「{skill.Name}」"))
                    + "を覚えました";

        private int LevelsLeft =>
            Current != null ? Math.Max(0, store.MaxLevel - store.LevelOf(Current.Id)) : 0;

        private int IndexOf(string id)
        {
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].Id == id)
                    return i;
            return -1;
        }

        public void Refresh()
        {
            if (disposed)
                return;
            State = Compute();
            view.Render(State);
        }

        private TrainingState Compute()
        {
            var current = Current;
            if (current == null)
                return new TrainingState
                {
                    Name = "",
                    Elements = Array.Empty<Sprite>(),
                    Skills = Array.Empty<TrainingSkillState>(),
                    Passives = Array.Empty<TrainingSkillState>(),
                    Learned = Array.Empty<TrainingSkillState>(),
                    Maxed = true,
                };

            int maxLevel = store.MaxLevel;
            int costPerLevel = store.CostPerLevel;
            int level = Mathf.Clamp(store.LevelOf(current.Id), 1, maxLevel);
            bool maxed = level >= maxLevel;
            int levelsLeft = maxLevel - level;
            count = Mathf.Clamp(count, 1, Math.Max(1, levelsLeft));
            int target = maxed ? level : level + count;
            long runes = store.Runes;
            long cost = maxed ? 0 : TrainingRules.CostBetween(level, target, costPerLevel);
            var cards = store.CardsOf(current.Id);
            var skills = Skills(current.Growth, level);
            var targetSkills = Skills(current.Growth, target);
            bool open = dialog && !maxed;
            int stage = store.BondOf(current.Id);
            int tokens = store.BondTokensOf(current.Id);
            bool bondMaxed = stage >= TrainingBond.Max;
            return new TrainingState
            {
                Member = current.Member,
                Name = current.Member.Name,
                Level = level,
                Maxed = maxed,
                CanSwitch = roster.Count > 1 && !open && celebrated == 0,
                Elements = cards.Select(iconOf).Where(icon => icon != null).Distinct().ToArray(),
                Stats = wear(current.Id, current.Growth.StatsAt(level, stage)),
                Skills = skills,
                Passives = skills.Where(skill => skill.Type == PassiveType).ToArray(),
                Runes = runes,
                NextCost = maxed ? 0 : TrainingRules.CostToNext(level, costPerLevel),
                DialogOpen = open,
                Count = count,
                Target = target,
                TargetStats = wear(current.Id, current.Growth.StatsAt(target, stage)),
                Cost = cost,
                CanAfford = cost <= runes,
                CanLess = open && count > 1,
                CanMore = open && count < levelsLeft,
                CanConfirm = open && cost <= runes,
                Learned = targetSkills
                    .Where(skill => skill.UnlockLevel > level && skill.UnlockLevel <= target)
                    .OrderBy(skill => skill.UnlockLevel)
                    .ToArray(),
                Bond = stage,
                BondTokens = tokens,
                BondMaxed = bondMaxed,
                CanDeepen =
                    !open && celebrated == 0 && !bondMaxed && tokens >= TrainingBond.TokensPerStage,
                Celebrated = celebrated,
            };
        }

        private static TrainingSkillState[] Skills(TrainingCharacter growth, int level) =>
            growth
                .Passives.Select(skill => Skill(skill, PassiveType, level))
                .Concat(
                    growth.Uniques.Select(skill =>
                        Skill(skill, TrainingCardState.UniqueType, level)
                    )
                )
                .ToArray();

        private static TrainingSkillState Skill(TrainingSkill skill, string type, int level) =>
            new()
            {
                Type = type,
                Name = skill.Name,
                Description = skill.Description,
                Energy = skill.Energy,
                UnlockLevel = skill.UnlockLevel,
                Unlocked = skill.UnlockLevel <= level,
                Icon = skill.Icon,
            };

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            view.PrevPressed -= Previous;
            view.NextPressed -= Next;
            view.LevelUpPressed -= OpenDialog;
            view.LessPressed -= Less;
            view.MorePressed -= More;
            view.MaxPressed -= Max;
            view.CancelPressed -= CloseDialog;
            view.ConfirmPressed -= Confirm;
            view.BreakThroughPressed -= Deepen;
            view.CelebrationClosed -= CloseCelebration;
        }
    }

    public interface ITrainingView
    {
        event Action PrevPressed;
        event Action NextPressed;
        event Action LevelUpPressed;
        event Action LessPressed;
        event Action MorePressed;
        event Action MaxPressed;
        event Action CancelPressed;
        event Action ConfirmPressed;

        // 突破のタブの「限界突破」を押した。
        event Action BreakThroughPressed;

        // 深めた演出を閉じた（画面を押した）。
        event Action CelebrationClosed;

        void Render(TrainingState state);

        // 操作を求めない通知（共通の通知の帯）を出す。
        void ShowNotice(string message);
    }
}
