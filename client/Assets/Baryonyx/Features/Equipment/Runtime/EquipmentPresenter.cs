using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        // 効果の短い説明。数字は倍率を掛けた後の値。
        public string Detail { get; internal set; }

        // 右の行の右上に出す、誰が付けているか（「装備中」「アリアが装備中」）。誰も付けていなければ空。
        public string Mark { get; internal set; } = "";

        // 選んでいる人が付けている。
        public bool Worn { get; internal set; }
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

        // 選んでいる枠の名前（右のタブ「武器」「防具」）と、その枠に付けられる持っている装備。
        public string Title { get; internal set; }
        public IReadOnlyList<EquipmentItemState> Choices { get; internal set; }

        // 選んでいる枠に装備があり、外せる。
        public bool CanRemove { get; internal set; }
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
    /// the other companions) keeping the chosen slot, choose the weapon or armour slot on the
    /// left, then tap an owned item on the right to wear it at once. An item another person wears moves to the chosen person; "外す" takes the
    /// chosen slot's item off. Every change shows at once and is then saved through the source
    /// (the server behind it, or the app's memory without one), and the screen is drawn again
    /// from what the source returns: the saved items, or the ones put back when the save failed
    /// (which <see cref="Networking.SaveQueue"/> tells on the notice band).
    /// </summary>
    public sealed class EquipmentPresenter : IDisposable
    {
        public const string SaveFailedMessage = "装備を保存できませんでした";
        public const string RemoveLabel = "外す";
        public const string AccessoryTitle = "アクセサリー";

        private readonly IEquipmentView view;
        private readonly IReadOnlyList<PartyMember> people;
        private readonly IEquipmentSource source;
        private readonly Action<string> remember;
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
            int accessory = -1
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.people = people ?? Array.Empty<PartyMember>();
            this.state = state ?? new EquipmentState(null, null);
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.remember = remember;
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
        /// accessories. Accessories have no items yet, so their list stays empty.
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
            if (
                disposed
                || accessory >= 0
                || member == null
                || definition == null
                || definition.Slot != slot
            )
                return;
            string before = state.ItemIn(member.Id, slot);
            if (before == itemId)
                return;
            await Save(
                current => current.WithEquipped(member.Id, slot, itemId),
                () => source.EquipAsync(member.Id, slot, itemId, CancellationToken.None)
            );
        }

        private async Task RemoveAsync()
        {
            var member = Current;
            if (disposed || accessory >= 0 || member == null)
                return;
            string before = state.ItemIn(member.Id, slot);
            if (before == null)
                return;
            await Save(
                current => current.WithUnequipped(member.Id, slot),
                () => source.UnequipAsync(member.Id, slot, CancellationToken.None)
            );
        }

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
            // アクセサリーはまだ持てないので、一覧は空にして外せなくする。
            var choices = accessory >= 0 ? Array.Empty<EquipmentItemState>() : Choices(member.Id);
            string current = accessory >= 0 ? null : state.ItemIn(member.Id, slot);
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
                Title = TitleOf(),
                Choices = choices,
                CanRemove = current != null,
            };
            view.Render(State);
        }

        private string TitleOf() => accessory >= 0 ? AccessoryTitle : EquipmentCatalog.NameOf(slot);

        // 選んでいる枠に付けられる持っている装備。レア度の高い順、同じなら仮データの順、続けて入手の順。
        private IReadOnlyList<EquipmentItemState> Choices(string personId) =>
            state
                .Items.Select(
                    (item, index) =>
                        (
                            Item: item,
                            Index: index,
                            Definition: EquipmentCatalog.Find(item.EquipmentId)
                        )
                )
                .Where(entry => entry.Definition != null && entry.Definition.Slot == slot)
                .OrderByDescending(entry => entry.Definition.Rarity)
                .ThenBy(entry => EquipmentCatalog.IndexOf(entry.Item.EquipmentId))
                .ThenByDescending(entry => Inventory.Quality.Percent(entry.Item.Multiplier))
                .ThenBy(entry => entry.Index)
                .Select(entry => ItemState(personId, entry.Item))
                .ToArray();

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
