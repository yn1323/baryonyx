using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.Combat.Presentation;
using Baryonyx.Combat.Records;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// Runs a battle of the highway on the battle screen
    /// (doc/features/stage-progression-world-map.md#街道のマスと移動2026-10-24): the square's fixed enemies
    /// on its background, fought from full HP. A win clears the square and returns to the map; a
    /// gate boss's defeat opens the way behind it. After the party is wiped out, the square is
    /// not cleared and can be fought again at once or later from the map. The menu returns to the
    /// map. Scene changes are the caller's. The nested world map's battles (<see cref="WorldAtlasBattle"/>)
    /// run the same way with the enemies drawn from the square's settings, and save nothing on a
    /// win, since the square was paid for on the way in.
    /// </summary>
    public sealed class WorldMapBattleFlow : IDisposable
    {
        public const string VictoryTitle = AdventureDialogs.VictoryTitle;
        public const string DefeatTitle = AdventureDialogs.DefeatTitle;
        public const string DefeatBody = "このマスには、何度でも挑み直せる。";
        public const string MapChoice = "地図へ";
        public const string RetryChoice = "もう一度挑む";
        public const string ReturnChoice = "地図へ戻る";
        public const string MenuTitle = "街道のメニュー";
        public const string MenuBody = "地図へ戻ると、このマスには後で挑み直せる。";

        // 入れ子の地図の戦闘は、マスに初めて止まったときにだけ開く。地図へ戻ると、もう戦えない。
        public const string OnceDefeatBody = "地図へ戻ると、このマスの魔物とはもう戦えない。";
        public const string OnceMenuTitle = "戦闘のメニュー";
        public const string OnceMenuBody = "地図へ戻ると、このマスの魔物とはもう戦えない。";
        public const string StayChoice = AdventureTexts.GoOnChoice;

        private readonly BattleInspectView battle;
        private readonly AdventureOverlay overlay;
        private readonly Func<bool> toMap;
        private readonly Func<bool> retry;
        private readonly Func<Task<bool>> clear;
        private readonly Action left;
        private readonly bool fightsAgain;
        private readonly CancellationTokenSource lifetime = new();
        private bool busy;
        private bool disposed;

        /// <summary>A battle of the highway: the square's fixed enemies, clearing it on a win.</summary>
        public WorldMapBattleFlow(
            BattleInspectView battle,
            AdventureOverlay overlay,
            string squareId,
            Func<bool> toMap,
            Func<bool> retry
        )
            : this(
                battle,
                overlay,
                squareId,
                WorldMapEncounters.Of(squareId)
                    ?? throw new ArgumentException("The square has no battle.", nameof(squareId)),
                () => WorldMapSession.MoveAsync(squareId, clear: true),
                toMap,
                retry,
                () => WorldMapSession.Battle = null,
                fightsAgain: true
            ) { }

        /// <summary>
        /// A battle of <paramref name="encounter"/>: on a win, <paramref name="clear"/> saves the
        /// square before going back to the map, and <paramref name="left"/> runs once the map is
        /// on its way. <paramref name="fightsAgain"/> tells whether the square can be fought again
        /// from the map, which the words of the defeat and of the menu follow.
        /// </summary>
        public WorldMapBattleFlow(
            BattleInspectView battle,
            AdventureOverlay overlay,
            string squareId,
            WorldMapEncounter encounter,
            Func<Task<bool>> clear,
            Func<bool> toMap,
            Func<bool> retry,
            Action left,
            bool fightsAgain = false
        )
        {
            this.battle = battle != null ? battle : throw new ArgumentNullException(nameof(battle));
            this.overlay =
                overlay != null ? overlay : throw new ArgumentNullException(nameof(overlay));
            this.toMap = toMap ?? throw new ArgumentNullException(nameof(toMap));
            this.retry = retry ?? throw new ArgumentNullException(nameof(retry));
            this.clear = clear ?? throw new ArgumentNullException(nameof(clear));
            this.left = left;
            this.fightsAgain = fightsAgain;
            Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            SquareId = squareId;
            Gate = Encounter.Enemies.Kinds.Count > 2 && Encounter.Enemies.Kinds[2] != null;

            var enemies = Encounter.Enemies;
            battle.EndsWithOutcome = true;
            battle.BossEncounter = Gate;
            battle.UseEncounter(enemies.Kinds, enemies.Level, enemies.HpScale, enemies.PowerScale);
            // ダンジョンと同じく、開示した弱点を最初から見せ、この戦闘で開示した弱点を記録する。
            battle.UseRevealedWeaknesses(IsRevealed);
            battle.WeaknessRevealed += OnWeaknessRevealed;
            RevealedWeaknessSession.Changed += battle.RefreshRevealedWeaknesses;
            _ = RevealedWeaknessSession.LoadAsync(CancellationToken.None);
            battle.Finished += OnFinished;
            overlay.MenuPressed += OpenMenu;
            overlay.BackPressed += OpenMenu;
            overlay.BattleBack = battle.HandleBack;
            overlay.ShowMenu(true);
        }

        public string SquareId { get; }
        public WorldMapEncounter Encounter { get; }

        // 関所のボス（奥に主が立つ）か。
        public bool Gate { get; }

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
            Work = outcome == BattleOutcome.Victory ? VictoryAsync() : WipeoutAsync();
        }

        // 勝利の演出のあいだにマスのクリアを保存し、保存できたら地図へ戻る。
        private async Task VictoryAsync()
        {
            var saving = clear();
            if (!await Play(battle.PlayVictory))
                return;
            await ShowVictoryAsync(saving);
        }

        private async Task ShowVictoryAsync(Task<bool> saving)
        {
            bool saved = await saving;
            if (disposed)
                return;
            if (!saved)
            {
                Dialog.Show(
                    WorldMapSession.SaveFailedMessage,
                    AdventureDialogs.RetryBody,
                    null,
                    null,
                    new DialogChoice(
                        AdventureDialogs.RetryChoice,
                        () => Work = ShowVictoryAsync(clear())
                    )
                );
                return;
            }
            // 冒険の勝利と同じく、題だけを出し、どこを押しても地図へ戻る（2026-11-03、ユーザー）。
            Dialog.Show(
                VictoryTitle,
                null,
                null,
                null,
                DialogStyle.TapAnywhere,
                new DialogChoice(MapChoice, GoMap)
            );
        }

        // 全滅の演出のあとに、挑み直すか地図へ戻るかを選ばせる。マスはクリアにしない。
        private async Task WipeoutAsync()
        {
            if (!await Play(battle.PlayWipeout))
                return;
            Dialog.Show(
                DefeatTitle,
                DefeatText,
                null,
                null,
                new DialogChoice(RetryChoice, Retry),
                new DialogChoice(ReturnChoice, GoMap)
            );
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

        /// <summary>The words under the defeat's title.</summary>
        public string DefeatText => fightsAgain ? DefeatBody : OnceDefeatBody;

        /// <summary>The title of the battle's menu.</summary>
        public string MenuTitleText => fightsAgain ? MenuTitle : OnceMenuTitle;

        /// <summary>The words of the battle's menu.</summary>
        public string MenuText => fightsAgain ? MenuBody : OnceMenuBody;

        private void OpenMenu()
        {
            if (busy || Leaving || Dialog.IsShown)
                return;
            Dialog.Show(
                MenuTitleText,
                MenuText,
                null,
                Dialog.Hide,
                new DialogChoice(ReturnChoice, GoMap),
                new DialogChoice(StayChoice, Dialog.Hide)
            );
        }

        private void GoMap()
        {
            if (Leaving)
                return;
            Leave(toMap);
            if (Leaving)
                left?.Invoke();
        }

        private void Retry() => Leave(retry);

        private void Leave(Func<bool> go)
        {
            Leaving = go();
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
            overlay.BattleBack = null;
        }
    }
}
