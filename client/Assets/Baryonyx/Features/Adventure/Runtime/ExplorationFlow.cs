using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Inventory;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// Runs the exploration of the adventure in progress (doc/features/stage-progression.md):
    /// reads where the party is, shows the map from its room, walks the road to the room tapped
    /// with the camera following and saves the room chosen before showing the map from it, opens
    /// a treasure room's chest for its reward, and hands a room with a battle to the battle scene.
    /// The monsters found and the chest opened play the screen's moments (<see cref="ExplorationMoments"/>)
    /// as the exploration moment lab shows them.
    /// The menu suspends the adventure to Home (it goes on from the room the party is in), or ends
    /// it and shows what it brought back. Scene changes are the caller's (<c>toHome</c>,
    /// <c>toBattle</c>); the next room inside the scene is shown without leaving the map.
    /// </summary>
    public sealed class ExplorationFlow : IDisposable
    {
        // 出現の演出がない画面で、戦闘へ移るまで案内を見せる間。
        public const float BattleDelaySeconds = 0.9f;

        // 宝箱の2つ目からの中身の札を開いてから、次（最後ならダイアログ）までの読む間。
        public const float ItemReadSeconds = 1.1f;

        private readonly ExplorationView view;
        private readonly IAdventureSource source;
        private readonly Func<bool> toHome;
        private readonly Func<bool> toBattle;
        private readonly Action<AdventureRun> entered;
        private readonly CancellationTokenSource lifetime = new();
        private ExplorationRoom room;
        private bool busy;
        private bool disposed;

        public ExplorationFlow(
            ExplorationView view,
            IAdventureSource source,
            Func<bool> toHome,
            Func<bool> toBattle,
            Action<AdventureRun> entered = null,
            Func<IReadOnlyList<AdventureAltar.Choice>> altarChoices = null,
            Action<AdventureAltar.Choice, int> evolve = null
        )
        {
            this.altarChoices = altarChoices;
            this.evolve = evolve;
            this.view = view != null ? view : throw new ArgumentNullException(nameof(view));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.toHome = toHome ?? throw new ArgumentNullException(nameof(toHome));
            this.toBattle = toBattle ?? throw new ArgumentNullException(nameof(toBattle));
            this.entered = entered;
            view.RoomPressed += OnRoom;
            view.ChestPressed += OnChest;
            view.MenuPressed += OpenMenu;
            view.BackPressed += OpenMenu;
        }

        // 進化の祭壇で進められる固有スキルと、進化させる処理（編成と育成の仮データを持つシーンが渡す）。
        private readonly Func<IReadOnlyList<AdventureAltar.Choice>> altarChoices;
        private readonly Action<AdventureAltar.Choice, int> evolve;

        public AdventureState State { get; private set; }
        public ExplorationRoom Room => room;

        // シーンを離れる操作を受け付けたあと。以後の操作は受け付けない。
        public bool Leaving { get; private set; }
        public bool Busy => busy;

        // 実行中または直前の読み込み・保存。テストで完了を待つために公開する。
        public Task Work { get; private set; } = Task.CompletedTask;

        public void Start() => Work = LoadAsync();

        private async Task LoadAsync()
        {
            busy = true;
            if (view.Location != null)
                view.Location.text = "読み込み中…";
            if (view.Prompt != null)
                view.Prompt.text = "";
            AdventureState state;
            try
            {
                state = await source.LoadAsync(lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("冒険を取得できませんでした。" + exception.Message);
                if (disposed)
                    return;
                AdventureDialogs.LoadFailed(view.Dialog, () => Work = LoadAsync(), GoHome);
                return;
            }
            if (disposed)
                return;
            view.Dialog.Hide();
            if (!state.InProgress)
            {
                // 冒険がなければ（終えた・別の端末でやめたなど）、ホームへ戻る。
                AdventureSession.Use(state);
                GoHome();
                return;
            }
            Enter(state);
        }

        // 部屋を見せる。戦闘が残っていれば、少し見せてから戦闘へ移る。
        private void Enter(AdventureState state)
        {
            State = AdventureSession.Use(state);
            room = ExplorationRoom.From(state.Run);
            entered?.Invoke(state.Run);
            view.Render(room);
            busy = false;
            if (room.StartsBattle)
                Work = BattleAsync();
            else if (room.PassesBy)
                Work = PassAsync();
            else if (room.Shops)
                OpenMerchant();
        }

        // 休憩は、着いたら全員のHPを回復して終える。怪しい宝箱を通り過ぎるときも、ここで終える。
        private async Task PassAsync(AdventureClearOptions options = null)
        {
            busy = true;
            var kind = State.Run.Room?.Kind ?? AdventureRoomKind.Rest;
            AdventureState state;
            try
            {
                state = await source.ClearAsync(State.Run.RoomId, lifetime.Token, null, options);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("マスを終えられませんでした。" + exception.Message);
                if (disposed)
                    return;
                view.ShowNotice(AdventureTexts.SaveFailed);
                Work = LoadAsync();
                return;
            }
            if (disposed)
                return;
            // 終えたと保存できてから回復し、読み直しても二度は回復しない。
            if (kind == AdventureRoomKind.Rest)
                AdventureVitals.Rest(state.Run?.Id ?? State.Run.Id);
            Enter(state);
            view.ShowNotice(
                kind == AdventureRoomKind.Rest ? AdventureTexts.Rested
                : kind == AdventureRoomKind.ShadyChest ? AdventureTexts.PassedChest
                : AdventureTexts.KindName(kind)
            );
        }

        // --- Altar ---------------------------------------------------------------------------

        private IReadOnlyList<AdventureAltar.Choice> AltarChoices() =>
            altarChoices?.Invoke() ?? Array.Empty<AdventureAltar.Choice>();

        // 祭壇：仲間を選び、その固有スキルを選び、次の段階を選んで進化させる。進められる固有スキルが
        // なければ、宝箱と同じに開ける（OnChest）。
        private void OpenAltar()
        {
            var choices = AltarChoices();
            var people = choices.Select(choice => choice.Person).Distinct().ToArray();
            AdventureDialogs.Altar(
                view.Dialog,
                AdventureDialogs.AltarBody,
                people
                    .Select(person =>
                    {
                        var mine = choices.Where(choice => choice.Person == person).ToArray();
                        return (mine[0].Name, (Action)(() => OpenAltarUniques(mine)));
                    })
                    .ToArray(),
                AdventureTexts.CloseChoice,
                view.Dialog.Hide
            );
        }

        private void OpenAltarUniques(IReadOnlyList<AdventureAltar.Choice> mine)
        {
            if (mine.Count == 1)
            {
                OpenAltarStages(mine[0], () => OpenAltar());
                return;
            }
            AdventureDialogs.Altar(
                view.Dialog,
                mine[0].Name + "のどの固有スキルを覚醒させる？",
                mine.Select(choice =>
                        (
                            choice.CardName(choice.From),
                            (Action)(() => OpenAltarStages(choice, () => OpenAltarUniques(mine)))
                        )
                    )
                    .ToArray(),
                AdventureDialogs.BackChoice,
                OpenAltar
            );
        }

        private void OpenAltarStages(AdventureAltar.Choice choice, Action back) =>
            AdventureDialogs.Altar(
                view.Dialog,
                $"{choice.Name}の「{choice.CardName(choice.From)}」の覚醒先を選ぶ",
                choice
                    .Next.Select(stage =>
                        (choice.CardName(stage), (Action)(() => ConfirmEvolve(choice, stage, back)))
                    )
                    .ToArray(),
                AdventureDialogs.BackChoice,
                back
            );

        private void ConfirmEvolve(AdventureAltar.Choice choice, int stage, Action back) =>
            AdventureDialogs.Altar(
                view.Dialog,
                $"「{choice.CardName(choice.From)}」を「{choice.CardName(stage)}」へ覚醒させますか？",
                new (string, Action)[]
                {
                    (AdventureDialogs.EvolveChoice, () => Work = EvolveAsync(choice, stage)),
                },
                AdventureDialogs.BackChoice,
                () => OpenAltarStages(choice, back)
            );

        // 祭壇のマスを終えてから進化させ、終えたことを保存できなければ進化させない。
        private async Task EvolveAsync(AdventureAltar.Choice choice, int stage)
        {
            busy = true;
            view.Dialog.Hide();
            AdventureState state;
            try
            {
                state = await source.ClearAsync(
                    State.Run.RoomId,
                    lifetime.Token,
                    null,
                    new AdventureClearOptions { Evolved = true }
                );
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("祭壇のマスを終えられませんでした。" + exception.Message);
                if (disposed)
                    return;
                view.ShowNotice(AdventureTexts.SaveFailed);
                Work = LoadAsync();
                return;
            }
            if (disposed)
                return;
            evolve?.Invoke(choice, stage);
            Enter(state);
            view.ShowNotice(
                $"「{choice.CardName(choice.From)}」が「{choice.CardName(stage)}」に覚醒した！"
            );
        }

        // --- Merchant ------------------------------------------------------------------------

        // 行商人の品物を並べる。買うと確かめてから買い、一覧へ戻る。立ち去ると、行商人のマスを終える。
        private void OpenMerchant()
        {
            if (State?.Run == null)
                return;
            AdventureDialogs.Merchant(
                view.Dialog,
                view.Bonuses,
                State.Run.Contents.Wares,
                State.Runes,
                ConfirmBuy,
                () =>
                {
                    view.Dialog.Hide();
                    Work = PassAsync();
                }
            );
        }

        private void ConfirmBuy(AdventureWare ware) =>
            AdventureDialogs.ConfirmBuy(
                view.Dialog,
                view.Bonuses,
                ware,
                State.Runes,
                () => Work = BuyAsync(ware),
                OpenMerchant
            );

        private async Task BuyAsync(AdventureWare ware)
        {
            busy = true;
            view.Dialog.Hide();
            AdventureState state;
            try
            {
                state = await source.BuyAsync(State.Run.RoomId, ware.Index, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("品物を買えませんでした。" + exception.Message);
                if (disposed)
                    return;
                busy = false;
                view.ShowNotice(AdventureTexts.BuyFailed);
                OpenMerchant();
                return;
            }
            if (disposed)
                return;
            // 買った物を札で見せてから、一覧へ戻る（Enter が開き直す）。
            if (view.Moments != null && view.Moments.Reveal != null)
            {
                var item = AdventureDropItems.From(
                    view.Bonuses,
                    ware.AsDrop(state.Run.RoomId),
                    view.ItemArt
                );
                if (item != null)
                {
                    view.Moments.Reveal.Show(item);
                    try
                    {
                        await Awaitable.WaitForSecondsAsync(ItemReadSeconds, lifetime.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    view.Moments.Reveal.Hide();
                }
            }
            Enter(state);
        }

        private async Task BattleAsync()
        {
            busy = true;
            if (State.Run.Contents.Trial != AdventureTrial.None)
                view.ShowNotice(AdventureTexts.TrialNotice(State.Run.Contents.Trial));
            if (!await EncounterAsync(State.Run.Room?.Kind ?? AdventureRoomKind.Battle))
                return;
            if (disposed || Leaving)
                return;
            Leaving = toBattle();
            if (!Leaving)
            {
                busy = false;
                if (view.Moments != null)
                    view.Moments.Clear();
                view.ShowNotice("戦闘を始められませんでした");
            }
        }

        // 魔物の出現の演出を、戦闘へ移る幕が閉じきるまで見せる。演出の部品がない画面では、下の案内を
        // 少し見せる。
        private async Task<bool> EncounterAsync(AdventureRoomKind kind)
        {
            var moments = view.Moments;
            try
            {
                if (moments == null)
                {
                    await Awaitable.WaitForSecondsAsync(BattleDelaySeconds, lifetime.Token);
                    return !disposed;
                }
                moments.PlayEncounter(kind, intoBattle: true);
                return await WhileBusy(moments);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        // 演出が終わるまで待つ。待つ間に流れを捨てたら false。
        private async Task<bool> WhileBusy(ExplorationMoments moments)
        {
            while (!disposed && moments != null && moments.Busy)
                await Awaitable.NextFrameAsync(lifetime.Token);
            return !disposed && moments != null;
        }

        private void OnRoom(string roomId)
        {
            if (busy || Leaving || room == null)
                return;
            foreach (var exit in room.Exits)
                if (exit.Id == roomId)
                {
                    Work = MoveAsync(exit);
                    return;
                }
        }

        // 道を歩きながら選んだ部屋を保存し、両方が済んでから、着いた部屋から見た地図を見せる。
        private async Task MoveAsync(ExplorationExit exit)
        {
            busy = true;
            var walked = new TaskCompletionSource<bool>();
            view.WalkTo(exit.Id, () => walked.TrySetResult(true));
            AdventureState state;
            try
            {
                state = await source.MoveAsync(exit.Room, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("マスを選べませんでした。" + exception.Message);
                if (disposed)
                    return;
                view.ShowNotice(AdventureTexts.SaveFailed);
                Work = LoadAsync();
                return;
            }
            await walked.Task;
            if (disposed)
                return;
            Enter(state);
        }

        private void OnChest()
        {
            if (busy || Leaving || room == null || room.Chest != ExplorationChest.Closed)
                return;
            if (room.Run?.Room?.Kind == AdventureRoomKind.Altar && AltarChoices().Count > 0)
                OpenAltar();
            else if (room.Run?.Room?.Kind == AdventureRoomKind.ShadyChest)
                AdventureDialogs.ShadyChest(
                    view.Dialog,
                    () =>
                    {
                        view.Dialog.Hide();
                        Work = OpenChestAsync(open: true);
                    },
                    () =>
                    {
                        view.Dialog.Hide();
                        Work = PassAsync(new AdventureClearOptions { Pass = true });
                    },
                    view.Dialog.Hide
                );
            else
                Work = OpenChestAsync();
        }

        // 宝箱・歩数の扉・進化の祭壇は終えて中身を得る。怪しい宝箱（open）は開け、ミミックなら戦闘へ移る。
        private async Task OpenChestAsync(bool open = false)
        {
            busy = true;
            var run = State.Run;
            AdventureState state;
            try
            {
                state = open
                    ? await source.OpenAsync(run.RoomId, lifetime.Token)
                    : await source.ClearAsync(run.RoomId, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("宝箱を開けられませんでした。" + exception.Message);
                if (disposed)
                    return;
                view.ShowNotice(AdventureTexts.SaveFailed);
                Work = LoadAsync();
                return;
            }
            if (disposed)
                return;
            if (state.Run != null && state.Run.InBattle)
            {
                // ミミックだった。宝箱を閉じたまま、魔物の出現から戦闘へ移る。
                Enter(state);
                return;
            }
            if (state.StepDoor != null)
                view.ShowNotice(AdventureTexts.StepDoorNotice(state.StepDoor));
            bool shown = view.Moments != null;
            if (shown && !await ChestAsync(view.Moments, state.Drops))
                return;
            Enter(state);
            busy = true;
            // 手に入れた物を演出の札で見せたあとは、ダイアログの本文に書かない。
            if (shown)
                AdventureDialogs.TreasureOpened(view.Dialog, CloseReward);
            else
                AdventureDialogs.Treasure(
                    view.Dialog,
                    view.Bonuses,
                    state.Drops,
                    CloseReward,
                    view.ItemArt
                );
        }

        // 宝箱が震えて開き、中身を札で1つずつ見せる。宝箱は一番良い物の品質の色で光り、その札から開く。
        private async Task<bool> ChestAsync(
            ExplorationMoments moments,
            IReadOnlyList<AdventureDrop> drops
        )
        {
            var items = (drops ?? Array.Empty<AdventureDrop>())
                .Select(drop => AdventureDropItems.From(view.Bonuses, drop, view.ItemArt))
                .Where(item => item != null)
                .OrderByDescending(item => item.Graded ? Quality.Grade(item.Quality) : -1)
                .ToList();
            try
            {
                moments.PlayChest(items.Count > 0 ? items[0] : null);
                if (!await WhileBusy(moments))
                    return false;
                var reveal = moments.Reveal;
                for (int i = 1; i < items.Count && reveal != null; i++)
                {
                    reveal.Show(items[i]);
                    await Awaitable.WaitForSecondsAsync(ItemReadSeconds, lifetime.Token);
                }
                if (reveal != null)
                    reveal.Hide();
                return !disposed;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private void CloseReward()
        {
            view.Dialog.Hide();
            busy = false;
        }

        private void OpenMenu()
        {
            if (busy || Leaving || State?.Run == null)
                return;
            AdventureDialogs.Menu(
                view.Dialog,
                State.Run,
                null,
                AdventureTexts.CloseChoice,
                view.Dialog.Hide,
                GoHome,
                ConfirmQuit
            );
        }

        private void ConfirmQuit() =>
            AdventureDialogs.ConfirmQuit(
                view.Dialog,
                OpenMenu,
                () => Work = QuitAsync(),
                view.Dialog.Hide
            );

        private async Task QuitAsync()
        {
            busy = true;
            AdventureState state;
            try
            {
                state = await source.EndAsync(AdventureEndReason.Retreat, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("冒険を終えられませんでした。" + exception.Message);
                if (disposed)
                    return;
                busy = false;
                view.Dialog.Hide();
                view.ShowNotice(AdventureTexts.SaveFailed);
                return;
            }
            if (disposed)
                return;
            State = AdventureSession.Use(state);
            ShowResult(view.Dialog, view.Bonuses, state.Result, GoHome);
        }

        /// <summary>The end of an adventure: how it ended and what it brought back, then Home.</summary>
        public static void ShowResult(
            GameDialog dialog,
            StepBonus.StepBonusMockData bonuses,
            AdventureResult result,
            Action home
        )
        {
            if (result == null)
            {
                home();
                return;
            }
            AdventureDialogs.Result(dialog, bonuses, result, home);
        }

        private void GoHome()
        {
            if (Leaving)
                return;
            Leaving = toHome();
            if (!Leaving)
            {
                view.Dialog.Resume();
                view.ShowNotice("ホームへ戻れませんでした");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            // 待っている処理が後からトークンを読んでも例外にならないよう、取り消すだけにする。
            lifetime.Cancel();
            view.RoomPressed -= OnRoom;
            view.ChestPressed -= OnChest;
            view.MenuPressed -= OpenMenu;
            view.BackPressed -= OpenMenu;
        }
    }
}
