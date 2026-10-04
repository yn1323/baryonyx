using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        // 「★★★」。
        public string Stars { get; internal set; }
        public string Detail { get; internal set; }

        // 右の行の右上に出す、誰が付けているか（「装備中」「アリアが装備中」）。誰も付けていなければ空。
        public string Mark { get; internal set; } = "";

        // 選んでいる人が付けている。
        public bool Worn { get; internal set; }
    }

    /// <summary>Everything the equipment shows, recomputed after every input.</summary>
    public sealed class EquipmentViewState
    {
        // 上のタブに並べる人のID。パーティの枠の順、続けてパーティにいない仲間を持っている順。
        public IReadOnlyList<string> People { get; internal set; }
        public int PartyCount { get; internal set; }

        // 選んでいる人（People の位置）とその人。
        public int Person { get; internal set; }
        public string PersonId { get; internal set; }
        public string Name { get; internal set; }

        // 選んでいる枠と、武器・防具の2つの枠の装備。
        public EquipmentSlot Slot { get; internal set; }
        public IReadOnlyList<EquipmentItemState> Slots { get; internal set; }

        // 右の見出し（「武器」「防具」）と、選んでいる枠に付けられる持っている装備。
        public string Title { get; internal set; }
        public IReadOnlyList<EquipmentItemState> Choices { get; internal set; }
        public string CountText { get; internal set; }

        // 選んでいる枠に装備があり、外せる。
        public bool CanRemove { get; internal set; }
    }

    public interface IEquipmentView
    {
        event Action<string> PersonPressed;
        event Action<int> SlotPressed;
        event Action<string> ItemPressed;
        event Action RemovePressed;

        void Render(EquipmentViewState state);

        // 操作を求めない通知（共通の通知の帯）を出す。
        void ShowNotice(string message);
    }

    /// <summary>
    /// The formation's equipment: choose a person on the tabs (the party first, then the other
    /// companions), the weapon or armour slot on the left, then tap an owned item on the right to
    /// wear it at once. An item another person wears moves to the chosen person; "外す" takes the
    /// chosen slot's item off. Every change is saved through the source first (the server, or
    /// the app's memory without one) and then shown, with a notice that asks nothing of the
    /// player. A change that cannot be saved leaves the screen as it was.
    /// </summary>
    public sealed class EquipmentPresenter : IDisposable
    {
        public const string SaveFailedMessage = "装備を保存できませんでした";
        public const string RemoveLabel = "外す";

        private readonly IEquipmentView view;
        private readonly IReadOnlyList<PartyMember> people;
        private readonly int partyCount;
        private readonly IEquipmentSource source;
        private readonly Action<string> remember;
        private readonly CancellationTokenSource lifetime = new();
        private EquipmentState state;
        private int person;
        private EquipmentSlot slot = EquipmentSlot.Weapon;
        private bool saving;
        private bool disposed;

        public EquipmentPresenter(
            IEquipmentView view,
            IReadOnlyList<PartyMember> people,
            int partyCount,
            EquipmentState state,
            IEquipmentSource source,
            string selected = null,
            Action<string> remember = null
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.people = people ?? Array.Empty<PartyMember>();
            this.partyCount = Math.Clamp(partyCount, 0, this.people.Count);
            this.state = state ?? new EquipmentState(null, null);
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.remember = remember;
            person = Math.Max(0, IndexOf(selected));
            view.PersonPressed += SelectPerson;
            view.SlotPressed += SelectSlot;
            view.ItemPressed += Choose;
            view.RemovePressed += Remove;
            Refresh();
        }

        public EquipmentViewState State { get; private set; }

        // 保存を待っている間。重ねて押されても受け付けない。
        public bool Saving => saving;

        // 実行中または直前の付け替え。テストで完了を待つために公開する。
        public Task ChangeTask { get; private set; } = Task.CompletedTask;

        private PartyMember Current => people.Count > 0 ? people[person] : null;

        public void SelectPerson(string id)
        {
            int index = IndexOf(id);
            if (disposed || index < 0 || index == person)
                return;
            person = index;
            slot = EquipmentSlot.Weapon;
            remember?.Invoke(id);
            Refresh();
        }

        public void SelectSlot(int index)
        {
            if (
                disposed
                || !Enum.IsDefined(typeof(EquipmentSlot), index)
                || (EquipmentSlot)index == slot
            )
                return;
            slot = (EquipmentSlot)index;
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
                || saving
                || member == null
                || definition == null
                || definition.Slot != slot
            )
                return;
            string before = state.ItemIn(member.Id, slot);
            if (before == itemId)
                return;
            string beforeName = NameOfItem(before);
            await Save(
                token => source.EquipAsync(member.Id, slot, itemId, token),
                saved =>
                    saved.Change switch
                    {
                        "move" =>
                            $"{NameOf(saved.From)}の「{definition.Name}」を{member.Name}に付け替えました",
                        "replace" =>
                            $"{member.Name}の「{beforeName}」を「{definition.Name}」に替えました",
                        "equip" => $"{member.Name}に「{definition.Name}」を装備しました",
                        _ => null,
                    }
            );
        }

        private async Task RemoveAsync()
        {
            var member = Current;
            if (disposed || saving || member == null)
                return;
            string before = state.ItemIn(member.Id, slot);
            if (before == null)
                return;
            string name = NameOfItem(before);
            await Save(
                token => source.UnequipAsync(member.Id, slot, token),
                saved => saved.Change == "remove" ? $"{member.Name}の「{name}」を外しました" : null
            );
        }

        // 保存してから表示を変える。保存できなければ表示は変えずに知らせる。
        private async Task Save(
            Func<CancellationToken, Task<EquipmentState>> operation,
            Func<EquipmentState, string> message
        )
        {
            saving = true;
            try
            {
                var saved = await operation(lifetime.Token);
                if (disposed)
                    return;
                state = saved ?? state;
                string notice = saved != null ? message(saved) : null;
                if (!string.IsNullOrEmpty(notice))
                    view.ShowNotice(notice);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                if (!disposed)
                    view.ShowNotice(SaveFailedMessage);
            }
            finally
            {
                saving = false;
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
                    People = Array.Empty<string>(),
                    Slot = slot,
                    Slots = Array.Empty<EquipmentItemState>(),
                    Title = EquipmentCatalog.NameOf(slot),
                    Choices = Array.Empty<EquipmentItemState>(),
                    CountText = "",
                };
                view.Render(State);
                return;
            }
            var choices = Choices(member.Id);
            string current = state.ItemIn(member.Id, slot);
            State = new EquipmentViewState
            {
                People = people.Select(entry => entry.Id).ToArray(),
                PartyCount = partyCount,
                Person = person,
                PersonId = member.Id,
                Name = member.Name,
                Slot = slot,
                Slots = new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor }
                    .Select(entry =>
                        ItemState(member.Id, state.Find(state.ItemIn(member.Id, entry)))
                    )
                    .ToArray(),
                Title = EquipmentCatalog.NameOf(slot),
                Choices = choices,
                CountText = $"所持 {choices.Count}",
                CanRemove = current != null,
            };
            view.Render(State);
        }

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
                Detail = definition.Detail,
                Worn = wearer != null && wearer == personId,
                Mark =
                    wearer == null ? ""
                    : wearer == personId ? "装備中"
                    : $"{NameOf(wearer)}が装備中",
            };
        }

        private string NameOfItem(string itemId) =>
            EquipmentCatalog.Find(state.Find(itemId)?.EquipmentId)?.Name ?? "";

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
            lifetime.Cancel();
            lifetime.Dispose();
            view.PersonPressed -= SelectPerson;
            view.SlotPressed -= SelectSlot;
            view.ItemPressed -= Choose;
            view.RemovePressed -= Remove;
        }
    }
}
