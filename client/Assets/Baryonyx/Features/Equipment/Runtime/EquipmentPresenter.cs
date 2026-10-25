using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Inventory;
using Baryonyx.Party;

namespace Baryonyx.Equipment
{
    /// <summary>One item (or an empty slot) as the equipment shows it.</summary>
    public sealed class EquipmentItemState
    {
        // 空いている枠ではnull。
        public string ItemId { get; internal set; }
        public string EquipmentId { get; internal set; }
        public string Name { get; internal set; }

        // 「★★★」。種類ごとのレア度で、強さは表さない。
        public string Stars { get; internal set; }

        // この1つの品質と倍率。空いている枠ではCと×1.00。
        public ItemQuality Quality { get; internal set; } = ItemQuality.C;
        public float Multiplier { get; internal set; } = Inventory.Quality.MinMultiplier;

        // 種類ごとの光り方（EquipmentCatalog.FoilStyleOf）。品質S・Aのとき、アイコンに重ねるキラキラに使う。
        public int FoilStyle { get; internal set; }

        // 効果の短い説明。数字は倍率を掛けた後の値。
        public string Detail { get; internal set; }

        // 右の行の右上に出す、誰が付けているか（「装備中」「アリアが装備中」）。誰も付けていなければ空。
        public string Mark { get; internal set; } = "";

        // 選んでいる人が付けている。
        public bool Worn { get; internal set; }

        // 右の行の装備を選んでいる枠に付けたときの、ステータスの増減（今の装備と比べて、変わる項目を
        // CharacterStats の順に最大3つ）。付けている装備の行、変わらない行、左の枠では空。
        public IReadOnlyList<(int Stat, int Amount)> Changes { get; internal set; } =
            Array.Empty<(int, int)>();
    }

    /// <summary>Everything the equipment shows, recomputed after every input.</summary>
    public sealed class EquipmentViewState
    {
        // 選んでいる人とその人。フリックではパーティの枠の順、続けてパーティにいない仲間を持っている順に替える。
        public string PersonId { get; internal set; }
        public string Name { get; internal set; }
        public bool CanSwitch { get; internal set; }

        // 選んでいる枠と、武器・防具の2つの枠の装備。アクセサリーの枠を選んでいるときは Accessory が0〜2。
        public EquipmentSlot Slot { get; internal set; }
        public int Accessory { get; internal set; } = -1;
        public IReadOnlyList<EquipmentItemState> Slots { get; internal set; }

        // アクセサリーの3つの枠の装備。空いている枠は ItemId がnull。
        public IReadOnlyList<EquipmentItemState> Accessories { get; internal set; } =
            Array.Empty<EquipmentItemState>();

        // 選んでいる枠の名前（右のタブ「武器」「防具」）と、その枠に付けられる持っている装備。
        public string Title { get; internal set; }
        public IReadOnlyList<EquipmentItemState> Choices { get; internal set; }

        // 選んでいる枠に装備があり、外せる。
        public bool CanRemove { get; internal set; }

        // 選んでいる人の、付けている装備で上げたステータスと、装備で上がった分
        // （doc/features/equipment.md#装備の効果）。ステータスを出せないときは Stats がnull。
        public CharacterStats? Stats { get; internal set; }
        public CharacterStats Raise { get; internal set; }
    }

    public interface IEquipmentView
    {
        event Action PrevPressed;
        event Action NextPressed;
        event Action<int> SlotPressed;
        event Action<string> ItemPressed;
        event Action RemovePressed;

        void Render(EquipmentViewState state);

        // 操作を求めない通知（共通の通知の帯）を出す。
        void ShowNotice(string message);
    }

    /// <summary>
    /// The formation's equipment change screen: ◀ ▶ walks the people (the party first, then
    /// the other companions) keeping the chosen slot, choose the weapon, armour or an accessory
    /// slot on the left, then tap an owned item on the right to wear it at once. An item another
    /// person (or another accessory slot) wears moves to the chosen slot; "外す" takes the chosen
    /// slot's item off. An accessory slot opens at its level (<see cref="AccessorySlots"/>). Every change shows at once and is then saved through the source
    /// (the server behind it, or the app's memory without one), and the screen is drawn again
    /// from what the source returns: the saved items, or the ones put back when the save failed
    /// (which <see cref="Networking.SaveQueue"/> tells on the notice band).
    /// </summary>
    public sealed class EquipmentPresenter : IDisposable
    {
        public const string SaveFailedMessage = "装備を保存できませんでした";
        public const string RemoveLabel = "外す";
        public const string AccessoryTitle = "アクセサリー";

        // 右の行に出す、ステータスの増減の数。
        public const int MaxChanges = 3;

        private readonly IEquipmentView view;
        private readonly IReadOnlyList<PartyMember> people;
        private readonly IEquipmentSource source;
        private readonly Action<string> remember;
        private readonly Func<string, int> levelOf;
        private readonly Func<string, CharacterStats?> statsOf;
        private EquipmentState state;
        private int person;
        private EquipmentSlot slot = EquipmentSlot.Weapon;

        // 選んでいるアクセサリーの枠（0〜2）。武器・防具の枠を選んでいるときは-1。
        private int accessory = -1;
        private bool disposed;

        public EquipmentPresenter(
            IEquipmentView view,
            IReadOnlyList<PartyMember> people,
            EquipmentState state,
            IEquipmentSource source,
            string selected = null,
            Action<string> remember = null,
            EquipmentSlot slot = EquipmentSlot.Weapon,
            int accessory = -1,
            Func<string, int> levelOf = null,
            Func<string, CharacterStats?> statsOf = null
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.people = people ?? Array.Empty<PartyMember>();
            this.state = state ?? new EquipmentState(null, null);
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.remember = remember;
            this.levelOf = levelOf;
            this.statsOf = statsOf;
            person = Math.Max(0, IndexOf(selected));
            this.slot = Enum.IsDefined(typeof(EquipmentSlot), slot) ? slot : EquipmentSlot.Weapon;
            this.accessory = accessory >= 0 && accessory < AccessorySlots.Count ? accessory : -1;
            view.PrevPressed += Previous;
            view.NextPressed += Next;
            view.SlotPressed += SelectSlot;
            view.ItemPressed += Choose;
            view.RemovePressed += Remove;
            Refresh();
        }

        public EquipmentViewState State { get; private set; }

        // 直前の付け替えの保存。テストで完了を待つために公開する。
        public Task ChangeTask { get; private set; } = Task.CompletedTask;

        private PartyMember Current => people.Count > 0 ? people[person] : null;

        public void Previous() => Step(-1);

        public void Next() => Step(1);

        // 人を替える。選んでいる枠はそのままにし、同じ枠の装備を人ごとに見比べられるようにする。
        private void Step(int delta)
        {
            if (disposed || people.Count < 2)
                return;
            person = (person + delta + people.Count) % people.Count;
            remember?.Invoke(people[person].Id);
            Refresh();
        }

        /// <summary>
        /// Chooses a slot by its place on the left: 0 the weapon, 1 the armour, 2 to 4 the
        /// accessories. An accessory slot not open yet lists nothing.
        /// </summary>
        public void SelectSlot(int index)
        {
            if (disposed)
                return;
            int chosen = index - 2;
            if (chosen >= 0 && chosen < AccessorySlots.Count)
            {
                if (chosen == accessory)
                    return;
                accessory = chosen;
                Refresh();
                return;
            }
            if (
                !Enum.IsDefined(typeof(EquipmentSlot), index)
                || ((EquipmentSlot)index == slot && accessory < 0)
            )
                return;
            slot = (EquipmentSlot)index;
            accessory = -1;
            Refresh();
        }

        // 選んでいる枠に、押した装備を付ける。
        public void Choose(string itemId) => ChangeTask = ChooseAsync(itemId);

        // 選んでいる枠の装備を外す。
        public void Remove() => ChangeTask = RemoveAsync();

        private async Task ChooseAsync(string itemId)
        {
            var member = Current;
            var item = state.Find(itemId);
            var definition = EquipmentCatalog.Find(item?.EquipmentId);
            var place = Place;
            if (
                disposed
                || member == null
                || definition == null
                || definition.Slot != place.Slot
                || !IsOpen(member)
            )
                return;
            string before = state.ItemIn(member.Id, place);
            if (before == itemId)
                return;
            await Save(
                current => current.WithEquipped(member.Id, place, itemId),
                () => source.EquipAsync(member.Id, place, itemId, CancellationToken.None)
            );
        }

        private async Task RemoveAsync()
        {
            var member = Current;
            var place = Place;
            if (disposed || member == null)
                return;
            string before = state.ItemIn(member.Id, place);
            if (before == null)
                return;
            await Save(
                current => current.WithUnequipped(member.Id, place),
                () => source.UnequipAsync(member.Id, place, CancellationToken.None)
            );
        }

        // 選んでいる枠（武器・防具、またはアクセサリーの枠）。
        private EquipmentPlace Place => accessory >= 0 ? EquipmentPlace.Accessory(accessory) : slot;

        private int LevelOf(PartyMember member) => levelOf?.Invoke(member.Id) ?? member.Level;

        // 選んでいる枠が開いているか。武器と防具はいつも開いている。
        private bool IsOpen(PartyMember member) =>
            accessory < 0 || AccessorySlots.IsOpen(accessory, LevelOf(member));

        // すぐに表示を変えてから保存し、置き場が返した装備（保存したもの、失敗なら戻したもの）で描き直す。
        // 変わったことは枠と一覧を見ればわかるので知らせない。保存は画面を閉じても続ける。
        private async Task Save(
            Func<EquipmentState, EquipmentState> apply,
            Func<Task<EquipmentState>> operation
        )
        {
            var before = state;
            state = apply(state);
            Refresh();
            try
            {
                var saved = await operation();
                if (disposed)
                    return;
                state = saved ?? state;
            }
            catch (Exception)
            {
                // サーバーの置き場は失敗しても戻した装備を返す。投げるのは、それ以外の置き場だけ。
                if (disposed)
                    return;
                state = before;
                view.ShowNotice(SaveFailedMessage);
            }
            Refresh();
        }

        public void Refresh()
        {
            if (disposed)
                return;
            var member = Current;
            if (member == null)
            {
                State = new EquipmentViewState
                {
                    Slot = slot,
                    Accessory = accessory,
                    Slots = Array.Empty<EquipmentItemState>(),
                    Title = TitleOf(),
                    Choices = Array.Empty<EquipmentItemState>(),
                };
                view.Render(State);
                return;
            }
            // 開いていないアクセサリーの枠は、一覧を空にして外せなくする。
            bool open = IsOpen(member);
            // Lvと絆を入れたステータス。装備の上昇分と、右の行の増減はここから求める。
            var stats = statsOf?.Invoke(member.Id);
            var choices = open ? Choices(member.Id, stats) : Array.Empty<EquipmentItemState>();
            string current = open ? state.ItemIn(member.Id, Place) : null;
            State = new EquipmentViewState
            {
                PersonId = member.Id,
                Name = member.Name,
                CanSwitch = people.Count > 1,
                Slot = slot,
                Accessory = accessory,
                Slots = new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor }
                    .Select(entry =>
                        ItemState(member.Id, state.Find(state.ItemIn(member.Id, entry)))
                    )
                    .ToArray(),
                Accessories = Enumerable
                    .Range(0, AccessorySlots.Count)
                    .Select(index =>
                        ItemState(
                            member.Id,
                            state.Find(state.ItemIn(member.Id, EquipmentPlace.Accessory(index)))
                        )
                    )
                    .ToArray(),
                Title = TitleOf(),
                Choices = choices,
                CanRemove = current != null,
                Stats = stats.HasValue ? EquipmentStats.With(stats.Value, state, member.Id) : null,
                Raise = stats.HasValue
                    ? EquipmentStats.Bonus(stats.Value, state, member.Id)
                    : default,
            };
            view.Render(State);
        }

        private string TitleOf() => accessory >= 0 ? AccessoryTitle : EquipmentCatalog.NameOf(slot);

        // 選んでいる枠に付けられる持っている装備。レア度の高い順、同じなら仮データの順、続けて入手の順。
        private IReadOnlyList<EquipmentItemState> Choices(string personId, CharacterStats? stats) =>
            state
                .Items.Select(
                    (item, index) =>
                        (
                            Item: item,
                            Index: index,
                            Definition: EquipmentCatalog.Find(item.EquipmentId)
                        )
                )
                .Where(entry => entry.Definition != null && entry.Definition.Slot == Place.Slot)
                .OrderByDescending(entry => entry.Definition.Rarity)
                .ThenBy(entry => EquipmentCatalog.IndexOf(entry.Item.EquipmentId))
                .ThenByDescending(entry => Inventory.Quality.Percent(entry.Item.Multiplier))
                .ThenBy(entry => entry.Index)
                .Select(entry =>
                {
                    var choice = ItemState(personId, entry.Item);
                    choice.Changes = ChangesOf(personId, entry.Item, stats);
                    return choice;
                })
                .ToArray();

        // 選んでいる枠にその装備を付けたときの、今と比べたステータスの増減。付けている装備では空。
        private IReadOnlyList<(int Stat, int Amount)> ChangesOf(
            string personId,
            EquipmentItem item,
            CharacterStats? stats
        )
        {
            if (!stats.HasValue || state.ItemIn(personId, Place) == item.Id)
                return Array.Empty<(int, int)>();
            var now = EquipmentStats.With(stats.Value, state, personId);
            var after = EquipmentStats.With(
                stats.Value,
                state.WithEquipped(personId, Place, item.Id),
                personId
            );
            var changes = new List<(int, int)>();
            for (int i = 0; i < CharacterStats.Count && changes.Count < MaxChanges; i++)
                if (after[i] != now[i])
                    changes.Add((i, after[i] - now[i]));
            return changes;
        }

        private EquipmentItemState ItemState(string personId, EquipmentItem item)
        {
            var definition = EquipmentCatalog.Find(item?.EquipmentId);
            if (definition == null)
                return new EquipmentItemState
                {
                    Name = "なし",
                    Stars = "",
                    Detail = "",
                };
            string wearer = state.WornBy(item.Id);
            return new EquipmentItemState
            {
                ItemId = item.Id,
                EquipmentId = definition.Id,
                Name = definition.Name,
                Stars = EquipmentCatalog.Stars(definition.Rarity),
                Quality = definition.QualityOf(item.Multiplier),
                Multiplier = item.Multiplier,
                FoilStyle = EquipmentCatalog.FoilStyleOf(definition.Id),
                Detail = definition.DetailAt(item.Multiplier),
                Worn = wearer != null && wearer == personId,
                Mark =
                    wearer == null ? ""
                    : wearer == personId ? "装備中"
                    : $"{NameOf(wearer)}が装備中",
            };
        }

        private string NameOf(string id) =>
            people.FirstOrDefault(entry => entry.Id == id)?.Name ?? "仲間";

        private int IndexOf(string id)
        {
            for (int i = 0; i < people.Count; i++)
                if (people[i].Id == id)
                    return i;
            return -1;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            view.PrevPressed -= Previous;
            view.NextPressed -= Next;
            view.SlotPressed -= SelectSlot;
            view.ItemPressed -= Choose;
            view.RemovePressed -= Remove;
        }
    }
}
