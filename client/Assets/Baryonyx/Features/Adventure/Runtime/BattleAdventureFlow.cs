using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat.Presentation;
using Baryonyx.Combat.Records;
using Baryonyx.Networking;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// Runs a battle of the adventure on the battle screen (doc/features/combat.md): the room's
    /// encounter on the destination's stage, opening with the enemies appearing (the boss last,
    /// in the deepest room), and, when the battle ends, the win, each drop of the room falling
    /// from the last enemy beaten and the way on to the next room, or, after the party is wiped
    /// out, the revive paid with runes or the return with what was found (both shown alike, with
    /// no countdown). The boss's defeat ends the adventure with its result. The menu suspends the
    /// adventure (the battle begins again on resuming) or ends it. Scene changes are the caller's.
    /// </summary>
    public sealed class BattleAdventureFlow : IDisposable
    {
        public const float DefeatDelaySeconds = 1.0f;

        // ドロップの札を開いてから次のドロップ（最後ならダイアログ）までの、読む間。
        public const float DropReadSeconds = 0.6f;
        public const string VictoryTitle = AdventureDialogs.VictoryTitle;
        public const string DefeatTitle = AdventureDialogs.DefeatTitle;

        private readonly BattleInspectView battle;
        private readonly AdventureOverlay overlay;
        private readonly IAdventureSource source;
        private readonly Func<bool> toExploration;
        private readonly Func<bool> toHome;
        private readonly CancellationTokenSource lifetime = new();

        // この戦闘に出た敵（Enemies の番号）。終えたときに、種類ごとの倒した数をライブラリへ送る。
        private readonly IReadOnlyCollection<int> standing;

        // この戦闘での固有スキルの進化。部屋を終えたときに残し、サーバーへ送る。
        private readonly AdventureEvolution evolution = new(
            step => Baryonyx.Party.PartyUniqueSkills.Evolve(step.Person, step.Unique, step.Stage),
            awakening =>
                Baryonyx.Party.PartySkillAwakenings.Awaken(awakening.Card, awakening.Branch)
        );
        private AdventureState state;
        private readonly AdventureTrial trial;
        private readonly int revivesAtStart;
        private AdventureClearOptions clearOptions = AdventureClearOptions.None;
        private bool busy;
        private bool disposed;

        public BattleAdventureFlow(
            BattleInspectView battle,
            AdventureOverlay overlay,
            IAdventureSource source,
            AdventureState state,
            Func<bool> toExploration,
            Func<bool> toHome
        )
        {
            this.battle = battle != null ? battle : throw new ArgumentNullException(nameof(battle));
            this.overlay =
                overlay != null ? overlay : throw new ArgumentNullException(nameof(overlay));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.toExploration =
                toExploration ?? throw new ArgumentNullException(nameof(toExploration));
            this.toHome = toHome ?? throw new ArgumentNullException(nameof(toHome));
            if (state.Run == null || !state.Run.InBattle)
                throw new ArgumentException("The adventure has no battle to fight.", nameof(state));

            var room = state.Run.Room;
            var encounter = AdventureCatalog.Encounter(room.Encounter);
            battle.EndsWithOutcome = true;
            battle.BossEncounter = room.Kind == AdventureRoomKind.Boss;
            battle.UseEncounter(
                encounter.Kinds,
                encounter.Level,
                encounter.HpScale,
                encounter.PowerScale
            );
            standing = encounter.Enemies;
            // 固有スキルは、部屋の種類と行き先の推奨Lvで決まる確率で進化する（doc/features/skill-evolution.md）。
            battle.Evolution = evolution.Rules(
                room.Kind,
                AdventureCatalog.Area(state.Run.DestinationId)?.Level ?? encounter.Level
            );
            // 前の戦闘までに開示した弱点を最初から見せ、この戦闘で開示した弱点を記録する。
            battle.UseRevealedWeaknesses(IsRevealed);
            battle.WeaknessRevealed += OnWeaknessRevealed;
            RevealedWeaknessSession.Changed += battle.RefreshRevealedWeaknesses;
            _ = RevealedWeaknessSession.LoadAsync(CancellationToken.None);
            battle.Finished += OnFinished;
            // 試練の条件は、戦闘の始めに知らせる。条件の判定に、この戦闘での復活の回数を使う。
            trial = state.Run.Contents.Trial;
            revivesAtStart = state.Run.Revives;
            if (trial != AdventureTrial.None)
                overlay.ShowNotice(AdventureTexts.TrialNotice(trial));
            overlay.MenuPressed += OpenMenu;
            overlay.BackPressed += OpenMenu;
            overlay.ShowMenu(true);
        }

        public AdventureState State => state;
        public string RoomId => state.Run?.RoomId ?? "";
        public bool Leaving { get; private set; }

        // 実行中または直前の保存。テストで完了を待つために公開する。
        public Task Work { get; private set; } = Task.CompletedTask;

        private GameDialog Dialog => overlay.Dialog;

        private static bool IsRevealed(string enemyId, BattleInspectElement element) =>
            RevealedWeaknessSession.IsRevealed(enemyId, BattleCardText.ElementOf(element));

        private static void OnWeaknessRevealed(string enemyId, BattleInspectElement element) =>
            RevealedWeaknessSession.Record(enemyId, BattleCardText.ElementOf(element));

        private void OnFinished(BattleOutcome outcome)
        {
            if (disposed || Leaving)
                return;
            busy = true;
            // 部屋を終えた戦闘の進化を残す。負けても残す（復活して続けた分は、次に終えたときに残す）。
            evolution.Keep();
            Work = outcome == BattleOutcome.Victory ? VictoryAsync() : WipeoutAsync();
        }

        /// <summary>
        /// Plays a turning point of the battle (<paramref name="moment"/> queues it) and waits
        /// for it to end. False when the flow was disposed meanwhile.
        /// </summary>
        private async Task<bool> Play(Action moment)
        {
            moment();
            try
            {
                while (!disposed && battle != null && battle.Acting)
                    await Awaitable.NextFrameAsync(lifetime.Token);
                return !disposed && battle != null;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Task<bool> Pause(float seconds)
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(seconds, lifetime.Token);
                return !disposed;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        // 勝利の演出のあいだに勝った部屋を保存し、ドロップを1つずつ見せてから次の部屋へ。
        // 最奥のボスなら冒険の結果を見せてホームへ。
        private async Task VictoryAsync()
        {
            // 勝ったときのHPを、次のマスへ持ち越す。
            var shares = battle.HpSharesBySlot();
            AdventureVitals.Keep(state.Run?.Id, shares);
            // 試練は、勝ったときに条件を満たしたかを判定する。倒れた味方がいるか、この戦闘で復活したら
            // 誰かが倒れたとみなす。
            if (trial != AdventureTrial.None)
            {
                var present = battle.Allies.Where(ally => !ally.Absent).ToArray();
                bool fell =
                    present.Any(ally => ally.Down) || (state.Run?.Revives ?? 0) > revivesAtStart;
                bool met = AdventureTrials.Met(
                    trial,
                    battle.Turn,
                    fell,
                    present.Select(ally => ally.MaxHp > 0 ? ally.Hp / (float)ally.MaxHp : 1f)
                );
                clearOptions = new AdventureClearOptions { TrialMet = met };
                overlay.ShowNotice(met ? AdventureTexts.TrialMet : AdventureTexts.TrialMissed);
            }
            var clearing = ClearAsync();
            if (!await Play(battle.PlayVictory))
                return;
            await RewardAsync(clearing);
        }

        private Task<AdventureState> ClearAsync() =>
            Begin(() => source.ClearAsync(RoomId, lifetime.Token, Tally(), clearOptions));

        private Task<AdventureState> LoadAsync() => Begin(() => source.LoadAsync(lifetime.Token));

        // 保存・取得を演出と並べて始める。すぐに投げた例外も、待つところで受け取る。
        private static Task<AdventureState> Begin(Func<Task<AdventureState>> start)
        {
            try
            {
                return start();
            }
            catch (Exception exception)
            {
                return Task.FromException<AdventureState>(exception);
            }
        }

        private async Task RewardAsync(Task<AdventureState> clearing)
        {
            AdventureState cleared;
            try
            {
                cleared = await clearing;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("勝利を保存できませんでした。" + exception.Message);
                if (disposed)
                    return;
                AdventureDialogs.SaveFailed(Dialog, () => Work = RewardAsync(ClearAsync()));
                return;
            }
            if (disposed)
                return;
            state = AdventureSession.Use(cleared);
            if (!await ShowDrops(cleared.Drops))
                return;
            var result = cleared.Result;
            AdventureDialogs.Victory(
                Dialog,
                result != null,
                result != null
                    ? () => ExplorationFlow.ShowResult(Dialog, result, GoHome)
                    : GoExplore
            );
        }

        // ドロップを1つずつ、最後に倒した敵から落として札で見せる。何もなければ「何も見つからなかった」。
        private async Task<bool> ShowDrops(IReadOnlyList<AdventureDrop> drops)
        {
            var reveal = overlay.Reveal;
            if (drops == null || drops.Count == 0)
            {
                if (!await Play(() => battle.PlayDrop(null, reveal)))
                    return false;
            }
            else
            {
                foreach (var drop in drops)
                {
                    var item = AdventureDropItems.From(drop, overlay.ItemArt);
                    if (!await Play(() => battle.PlayDrop(item, reveal)))
                        return false;
                    if (!await Pause(DropReadSeconds))
                        return false;
                }
            }
            if (reveal != null)
                reveal.Hide();
            return true;
        }

        // 全滅の演出のあいだに最新の冒険を取り、復活と帰還を選ばせる。
        private async Task WipeoutAsync()
        {
            var loading = LoadAsync();
            if (!await Play(battle.PlayWipeout))
                return;
            await ChooseAfterDefeatAsync(loading);
        }

        // 復活を断られたときは、間を置いて最新の値で選び直す。
        private async Task DefeatAsync()
        {
            if (!await Pause(DefeatDelaySeconds))
                return;
            await ChooseAfterDefeatAsync(LoadAsync());
        }

        // 復活と帰還を同じ重みで並べる。どちらを選んでも手に入れた物は残る。
        private async Task ChooseAfterDefeatAsync(Task<AdventureState> loading)
        {
            try
            {
                // 所持ルーンと次の復活の費用を、最新の値で見せる。
                state = AdventureSession.Use(await loading);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("冒険を取得できませんでした。" + exception.Message);
            }
            if (disposed)
                return;
            ShowRevive();
        }

        private void ShowRevive()
        {
            var run = state.Run;
            int cost = run?.ReviveCost ?? 0;
            bool canRevive = run != null && run.InBattle && state.Runes >= cost;
            AdventureDialogs.Revive(
                Dialog,
                cost,
                state.Runes,
                canRevive,
                () => Work = ReviveAsync(),
                () => Work = EndAsync(AdventureEndReason.Defeat)
            );
        }

        private async Task ReviveAsync()
        {
            AdventureState revived;
            try
            {
                revived = await source.ReviveAsync(RoomId, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("復活できませんでした。" + exception.Message);
                if (disposed)
                    return;
                overlay.ShowNotice(
                    exception is ServerApiException { StatusCode: 409 }
                        ? "ルーンが足りないか、冒険の状態が変わりました"
                        : "復活できませんでした"
                );
                // 断られたら最新の残高と費用で選び直す。
                Work = DefeatAsync();
                return;
            }
            if (disposed)
                return;
            state = AdventureSession.Use(revived);
            Dialog.Hide();
            busy = false;
            battle.Revive();
            overlay.ShowNotice($"ルーン {revived.RevivedFor ?? 0:N0} を使って復活した");
        }

        private void OpenMenu()
        {
            if (busy || Leaving || Dialog.IsShown)
                return;
            AdventureDialogs.Menu(
                Dialog,
                state.Run,
                AdventureTexts.SuspendBody,
                AdventureTexts.GoOnChoice,
                Dialog.Hide,
                GoHome,
                ConfirmQuit
            );
        }

        private void ConfirmQuit() =>
            AdventureDialogs.ConfirmQuit(
                Dialog,
                OpenMenu,
                () => Work = EndAsync(AdventureEndReason.Retreat),
                Dialog.Hide
            );

        private async Task EndAsync(AdventureEndReason reason)
        {
            busy = true;
            AdventureState ended;
            try
            {
                ended = await source.EndAsync(
                    reason,
                    lifetime.Token,
                    reason == AdventureEndReason.Defeat ? Tally() : null
                );
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
                overlay.ShowNotice(AdventureTexts.SaveFailed);
                Dialog.Resume();
                return;
            }
            if (disposed)
                return;
            state = AdventureSession.Use(ended);
            ExplorationFlow.ShowResult(Dialog, ended.Result, GoHome);
        }

        /// <summary>The enemies of this battle by kind, with how many the party has beaten.</summary>
        public IReadOnlyList<AdventureEnemyTally> Tally() =>
            AdventureEnemyTally.Count(
                standing
                    .Where(index => index >= 0 && index < battle.Enemies.Length)
                    .Select(index => (battle.Enemies[index].Id, !battle.Enemies[index].Alive))
            );

        private void GoExplore()
        {
            if (Leaving)
                return;
            Leaving = toExploration();
            if (!Leaving)
                Dialog.Resume();
        }

        private void GoHome()
        {
            if (Leaving)
                return;
            Leaving = toHome();
            if (!Leaving)
                Dialog.Resume();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            lifetime.Cancel();
            battle.Finished -= OnFinished;
            battle.WeaknessRevealed -= OnWeaknessRevealed;
            RevealedWeaknessSession.Changed -= battle.RefreshRevealedWeaknesses;
            overlay.MenuPressed -= OpenMenu;
            overlay.BackPressed -= OpenMenu;
        }
    }
}
