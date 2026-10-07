using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
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
    /// One of the four cards the character brings to the deck, as the battle draws it: a unique
    /// skill (fixed, unlocked by level) or a custom skill (a skill the player sets).
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

        // カードの枠の色を決める属性と、属性のアイコン。属性のないカードはアイコンがnull。
        public CardElement Element { get; internal set; }
        public Sprite Icon { get; internal set; }

        // 挿絵（64×58のドット絵）。
        public Texture2D Art { get; internal set; }

        // まだ解放していない固有スキル。
        public bool Locked { get; internal set; }
        public int UnlockLevel { get; internal set; }

        public bool Filled => Id != null || Type == UniqueType;

        public const string UniqueType = "固有スキル";
        public const string SkillType = "スキル";
    }

    /// <summary>One equipment slot on the adventurer's page.</summary>
    public sealed class TrainingGearState
    {
        // 「武器」「防具」「アクセサリー」。
        public string Slot { get; internal set; }

        // 付けている装備の名前と「★★」と効果。空いていれば名前は「なし」。
        public string Name { get; internal set; } = "";
        public string Stars { get; internal set; } = "";
        public string Detail { get; internal set; } = "";
        public bool Filled { get; internal set; }

        // まだ付けられない枠（アクセサリーは準備中）。押しても開かない。
        public bool Locked { get; internal set; }

        public static TrainingGearState Worn(
            string slot,
            string name,
            string stars,
            string detail = null
        ) =>
            new()
            {
                Slot = slot,
                Name = name ?? "",
                Stars = stars ?? "",
                Detail = detail ?? "",
                Filled = !string.IsNullOrEmpty(name),
            };
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

        // デッキに入る4枚：固有スキル2枚と、カスタムスキル2枚。
        public IReadOnlyList<TrainingCardState> Uniques { get; internal set; }
        public IReadOnlyList<TrainingCardState> Cards { get; internal set; }

        // 武器・防具、続けてアクセサリー3つ。
        public IReadOnlyList<TrainingGearState> Gear { get; internal set; }
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

        // 目標のレベルまでに覚えるスキルと、その次に覚えるスキル（なければnull）。
        public IReadOnlyList<TrainingSkillState> Learned { get; internal set; }
        public TrainingSkillState NextUnlock { get; internal set; }

        public long Remaining => Runes - Cost;
    }

    /// <summary>
    /// The adventurer's page of the formation: one character at a time (a flick walks the party,
    /// then the others), with their stats and passives, the four cards they bring to the deck
    /// (two unique skills and two custom skills) and their equipment. A custom skill or a weapon
    /// or armour slot opens its change screen for the character. "レベルアップ" opens a dialog
    /// over the page that shows what the chosen levels change before any rune is spent; back
    /// closes the dialog first. A level-up shows a notice that asks nothing. With a
    /// <c>levelUp</c> function the server raises the level and spends its runes first, and the
    /// store then reads them; otherwise (the showcase) the store keeps them in memory.
    /// </summary>
    public sealed class TrainingPresenter : IDisposable
    {
        public const string LevelUpFailedMessage = "レベルアップできませんでした";
        public const string PassiveType = "パッシブ";
        public const string EmptyName = "空き";

        // カスタムスキルの枠。固有スキル2枚と合わせて、1人4枚がデッキに入る（doc/features/party.md）。
        public const int CustomSlots = CardLoadoutRules.Size;

        // 武器・防具と、まだ付けられないアクセサリー3つ。
        public const int GearSlots = 5;
        public const int OpenGearSlots = 2;
        public static readonly string[] GearLabels = { "武器", "防具", "アクセサリー" };

        private readonly ITrainingView view;
        private readonly IReadOnlyList<TrainingMember> roster;
        private readonly ITrainingStore store;

        // スキルのIDから属性のアイコンを引く。属性のないカードはnull。
        private readonly Func<string, Sprite> iconOf;

        // キャラのID・今のLv・上げたあとのLvでレベルアップを保存する。保存した結果は store から読み直す。
        private readonly Func<string, int, int, CancellationToken, Task> levelUp;

        // キャラが付けている武器と防具（枠の順）。なければ空き。
        private readonly Func<string, IReadOnlyList<TrainingGearState>> gearOf;

        // スキルのIDから挿絵を引く。
        private readonly Func<string, Texture2D> artOf;

        // キャラのIDとスキルから、そのキャラのステータスで数字を入れた説明を作る。
        private readonly Func<string, CardSkill, string> describe;
        private readonly CancellationTokenSource lifetime = new();
        private int index;
        private bool dialog;
        private int count = 1;
        private bool saving;
        private bool disposed;

        public TrainingPresenter(
            ITrainingView view,
            IReadOnlyList<TrainingMember> roster,
            ITrainingStore store,
            Func<string, Sprite> iconOf = null,
            Func<string, int, int, CancellationToken, Task> levelUp = null,
            Func<string, IReadOnlyList<TrainingGearState>> gearOf = null,
            Func<string, Texture2D> artOf = null,
            Func<string, CardSkill, string> describe = null
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.roster = roster ?? Array.Empty<TrainingMember>();
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.iconOf = iconOf ?? (_ => null);
            this.levelUp = levelUp;
            this.gearOf = gearOf ?? (_ => Array.Empty<TrainingGearState>());
            this.artOf = artOf ?? (_ => null);
            this.describe = describe ?? ((_, card) => card.Text);
            index = Math.Max(0, IndexOf(store.Selected));
            view.PrevPressed += Previous;
            view.NextPressed += Next;
            view.LevelUpPressed += OpenDialog;
            view.LessPressed += Less;
            view.MorePressed += More;
            view.MaxPressed += Max;
            view.CancelPressed += CloseDialog;
            view.ConfirmPressed += Confirm;
            view.GearPressed += OpenGear;
            view.CardPressed += OpenCard;
            Refresh();
        }

        public TrainingState State { get; private set; }

        // レベルアップの保存を待っている間。重ねて押されても受け付けない。
        public bool Saving => saving;

        // 実行中または直前のレベルアップ。テストで完了を待つために公開する。
        public Task ConfirmTask { get; private set; } = Task.CompletedTask;

        private TrainingMember Current => roster.Count > 0 ? roster[index] : null;

        public void Previous() => Step(-1);

        public void Next() => Step(1);

        private void Step(int delta)
        {
            if (disposed || dialog || roster.Count < 2)
                return;
            index = (index + delta + roster.Count) % roster.Count;
            store.Selected = Current.Id;
            Refresh();
        }

        public void OpenDialog()
        {
            if (disposed || dialog || Current == null || State.Maxed)
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

        /// <summary>Back closes the dialog; true when it did, so the guide screen stays.</summary>
        public bool Back()
        {
            if (disposed || !dialog)
                return false;
            CloseDialog();
            return true;
        }

        public void Less()
        {
            if (disposed || saving || !dialog || count <= 1)
                return;
            count--;
            Refresh();
        }

        public void More()
        {
            if (disposed || saving || !dialog || count >= LevelsLeft)
                return;
            count++;
            Refresh();
        }

        // 所持ルーンで上げられるだけ上げる数にする。1つも上げられなければ1のまま（足りない量を見せる）。
        public void Max()
        {
            if (disposed || saving || !dialog)
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

        // サーバーがあれば、サーバーでレベルを上げてルーンを使ってから表示を変える。
        private async Task ConfirmAsync()
        {
            if (disposed || saving || !dialog || !State.CanConfirm)
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

            saving = true;
            Refresh();
            try
            {
                await levelUp(member.Id, level, target, lifetime.Token);
                if (disposed)
                    return;
                dialog = false;
                view.ShowNotice(message);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                if (!disposed)
                    view.ShowNotice(LevelUpFailedMessage);
            }
            finally
            {
                saving = false;
            }
            Refresh();
        }

        // 装備の枠を押す。武器・防具はその枠を選んだ装備の付け替えの画面を開く。
        public void OpenGear(int slot)
        {
            if (disposed || dialog || Current == null || slot < 0 || slot >= OpenGearSlots)
                return;
            view.OpenGear(Current.Id, slot);
        }

        // カスタムスキルの枠を押す。その枠を選んだスキルの付け替えの画面を開く。
        public void OpenCard(int slot)
        {
            if (disposed || dialog || Current == null || slot < 0 || slot >= State.Cards.Count)
                return;
            view.OpenCards(Current.Id, slot);
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
                    Uniques = Array.Empty<TrainingCardState>(),
                    Cards = Array.Empty<TrainingCardState>(),
                    Gear = Array.Empty<TrainingGearState>(),
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
            return new TrainingState
            {
                Member = current.Member,
                Name = current.Member.Name,
                Level = level,
                Maxed = maxed,
                CanSwitch = roster.Count > 1 && !open,
                Elements = cards.Select(iconOf).Where(icon => icon != null).Distinct().ToArray(),
                Stats = current.Growth.StatsAt(level),
                Skills = skills,
                Passives = skills.Where(skill => skill.Type == PassiveType).ToArray(),
                Uniques = skills
                    .Where(skill => skill.Type == TrainingCardState.UniqueType)
                    .Select(Unique)
                    .ToArray(),
                Cards = Enumerable
                    .Range(0, CustomSlots)
                    .Select(i => Card(current.Id, i < cards.Count ? cards[i] : null))
                    .ToArray(),
                Gear = Gear(current.Id),
                Runes = runes,
                NextCost = maxed ? 0 : TrainingRules.CostToNext(level, costPerLevel),
                DialogOpen = open,
                Count = count,
                Target = target,
                TargetStats = current.Growth.StatsAt(target),
                Cost = cost,
                CanAfford = cost <= runes,
                CanLess = open && count > 1 && !saving,
                CanMore = open && count < levelsLeft && !saving,
                CanConfirm = open && cost <= runes && !saving,
                Learned = targetSkills
                    .Where(skill => skill.UnlockLevel > level && skill.UnlockLevel <= target)
                    .OrderBy(skill => skill.UnlockLevel)
                    .ToArray(),
                NextUnlock = targetSkills
                    .Where(skill => !skill.Unlocked)
                    .OrderBy(skill => skill.UnlockLevel)
                    .FirstOrDefault(),
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

        // 固有スキルを、カードの見た目で出すための形にする（属性はまだないので枠は属性なし）。
        private static TrainingCardState Unique(TrainingSkillState skill) =>
            new()
            {
                Type = TrainingCardState.UniqueType,
                Name = skill.Name,
                Description = skill.Description,
                Cost = skill.Energy,
                Art = skill.Icon,
                Locked = !skill.Unlocked,
                UnlockLevel = skill.UnlockLevel,
            };

        private TrainingCardState Card(string person, string id)
        {
            var card = CardSkills.Find(id);
            if (card == null)
                return new TrainingCardState { Name = EmptyName };
            return new TrainingCardState
            {
                Id = card.Id,
                Type = TrainingCardState.SkillType,
                Name = card.Name,
                Kind = CardText.KindLine(card),
                Description = describe(person, card),
                Cost = card.Cost,
                Element = card.Element,
                Icon = iconOf(id),
                Art = artOf(id),
            };
        }

        // 武器・防具は付けているもの、続けてまだ付けられないアクセサリー3つ。
        private TrainingGearState[] Gear(string id)
        {
            var worn = gearOf(id) ?? Array.Empty<TrainingGearState>();
            return Enumerable
                .Range(0, GearSlots)
                .Select(i =>
                    i < OpenGearSlots
                        ? i < worn.Count && worn[i] != null
                            ? worn[i]
                            : TrainingGearState.Worn(GearLabels[i], null, null)
                        : new TrainingGearState { Slot = GearLabels[2], Locked = true }
                )
                .ToArray();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            lifetime.Cancel();
            lifetime.Dispose();
            view.PrevPressed -= Previous;
            view.NextPressed -= Next;
            view.LevelUpPressed -= OpenDialog;
            view.LessPressed -= Less;
            view.MorePressed -= More;
            view.MaxPressed -= Max;
            view.CancelPressed -= CloseDialog;
            view.ConfirmPressed -= Confirm;
            view.GearPressed -= OpenGear;
            view.CardPressed -= OpenCard;
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
        event Action<int> GearPressed;
        event Action<int> CardPressed;

        void Render(TrainingState state);

        // 操作を求めない通知（共通の通知の帯）を出す。
        void ShowNotice(string message);

        // キャラの装備の付け替えの画面を、押した枠（0が武器、1が防具）を選んで開く。
        void OpenGear(string id, int slot);

        // キャラのスキルの付け替えの画面を、押した枠を選んで開く。
        void OpenCards(string id, int slot);
    }
}
