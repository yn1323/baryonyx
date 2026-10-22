using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;

namespace Baryonyx.Equipment
{
    /// <summary>
    /// The server's equipment, changed at once and saved behind it (doc/rules/data-storage.md).
    /// A change returns what to show once it is saved, or put back when it failed (told on the
    /// notice band). A read while changes wait to be saved returns the shown items, since the
    /// server would answer with the items before them.
    /// </summary>
    public sealed class EquipmentSaves : IEquipmentSource
    {
        private OptimisticState<EquipmentState> saves;

        // 付け替えを始めた回数。読み直している間に付け替えたら、その前の読み込みで手元の装備を戻さない。
        private int changes;

        public EquipmentSaves(IEquipmentSource server)
        {
            Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        public IEquipmentSource Server { get; }

        // サーバーが確かめた装備に、保存を待っている付け替えを当てたもの。まだ読んでいなければnull。
        public EquipmentState Current => saves?.Current;

        // 最後にサーバーから読んだ装備の指紋。読み直して変わったかを比べる（変わらなければ描き直さない）。
        public string ReadVersion => saves?.Confirmed.Fingerprint;

        public async Task<EquipmentState> LoadAsync(CancellationToken token)
        {
            if (saves is { Pending: true })
                return saves.Current;
            int before = changes;
            var state = await Server.LoadAsync(token);
            // 読んでいる間に付け替えたら、読み込みは付け替えの前の装備なので使わない。
            if (saves != null && changes != before)
                return saves.Current;
            if (saves == null)
                saves = new OptimisticState<EquipmentState>(state, Server.LoadAsync);
            else
                saves.Confirm(state);
            return saves.Current;
        }

        public Task<EquipmentState> EquipAsync(
            string characterId,
            EquipmentSlot slot,
            string itemId,
            CancellationToken token
        ) =>
            Save(
                state => state.WithEquipped(characterId, slot, itemId),
                cancel => Server.EquipAsync(characterId, slot, itemId, cancel)
            );

        public Task<EquipmentState> UnequipAsync(
            string characterId,
            EquipmentSlot slot,
            CancellationToken token
        ) =>
            Save(
                state => state.WithUnequipped(characterId, slot),
                cancel => Server.UnequipAsync(characterId, slot, cancel)
            );

        // 画面を閉じても保存は続けるので、画面の token は使わない。
        private async Task<EquipmentState> Save(
            Func<EquipmentState, EquipmentState> apply,
            Func<CancellationToken, Task<EquipmentState>> send
        )
        {
            if (saves == null)
                throw new InvalidOperationException("The equipment is saved after it is read.");
            changes++;
            await saves.ChangeAsync(apply, send, _ => EquipmentPresenter.SaveFailedMessage);
            return saves.Current;
        }
    }
}
