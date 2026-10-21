using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The battle's turning points: the enemies appearing as the battle opens (a boss last and
    /// heavily), the win, a drop from the last enemy beaten, the party wiped out, and its revive.
    /// The adventure's battle (<see cref="EndsWithOutcome"/>) opens with the enemies appearing
    /// and revives with its moment by itself, and its flow plays the others as the battle ends;
    /// the battle moment lab (<see cref="BattleMomentLab"/>) plays each on demand. Each runs in
    /// the battle's action queue, so <see cref="Acting"/> holds while it plays.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        public const string EncounterText = "魔物が現れた！";
        public const string BossEncounterText = "強敵が現れた！";
        public const string VictoryText = "勝利！";
        public const string WipeoutText = "全滅してしまった…";
        public const string ReviveText = "復活！";

        // How long the band of a turning point stays: longer than a turn's, as each is rarer.
        private const float EncounterHold = 0.7f;
        private const float VictoryHold = 1.1f;
        private const float WipeoutHold = 1.2f;
        private const float ReviveHold = 0.7f;

        // An enemy steps in from the dark this far behind its place, and lights up as it comes.
        private const float EntranceDistance = 128f;
        private const float EntranceTime = 0.32f;
        private const float BossEntranceTime = 0.55f;
        private const float EntranceGap = 0.12f;
        private static readonly Color Silhouette = new(0.08f, 0.08f, 0.13f);

        // The party's hops of joy on a win.
        private const float HopHeight = 6f * Dot;
        private const float HopTime = 0.26f;

        /// <summary>
        /// The adventure's battle opens with the boss coming on last (the deepest room's). Set
        /// before the battle begins, with <see cref="EndsWithOutcome"/>.
        /// </summary>
        [NonSerialized]
        public bool BossEncounter;

        [Header("節目の帯")]
        public Color VictoryBannerLine = new(0.98f, 0.8f, 0.36f);
        public Color VictoryBannerText = new(1f, 0.92f, 0.62f);
        public Color WipeoutBannerLine = new(0.5f, 0.5f, 0.56f);
        public Color WipeoutBannerText = new(0.8f, 0.8f, 0.86f);

        /// <summary>The enemies appear: all at once, or with the boss last when <paramref name="boss"/>.</summary>
        public void PlayEncounter(bool boss) => Enqueue(Encounter(boss));

        /// <summary>The band of the party's turn (or of a turn again), as the battle shows it.</summary>
        public void PlayPartyTurnBanner(bool again) =>
            Enqueue(Band(() => ShowPartyTurnBanner(again)));

        /// <summary>The warning in place of the skill name when the energy cannot pay for a card.</summary>
        public void PlayNoEnergy() => Enqueue(Notice(NoEnergyText));

        /// <summary>The enemies still standing are beaten, and the party wins.</summary>
        public void PlayVictory() => Enqueue(Victory());

        /// <summary>
        /// <paramref name="drop"/> (null for nothing found) falls from the last enemy beaten and
        /// opens on <paramref name="reveal"/>. Enemies still standing are taken away first.
        /// </summary>
        public void PlayDrop(RewardItem drop, RewardReveal reveal) =>
            Enqueue(DropMoment(drop, reveal));

        /// <summary>The win, then the drop.</summary>
        public void PlayVictoryAndDrop(RewardItem drop, RewardReveal reveal)
        {
            Enqueue(Victory());
            Enqueue(DropMoment(drop, reveal));
        }

        /// <summary>Every ally still standing falls, and the party is wiped out.</summary>
        public void PlayWipeout() => Enqueue(Wipeout());

        /// <summary>The fallen party stands up again, as on a revive paid with runes.</summary>
        public void PlayRevive() => Enqueue(ReviveMoment());

        /// <summary>
        /// Puts the battlefield back for a turning point: as <see cref="LabRestore"/>, and every
        /// character back at its place, lit, with no band, dark or effect left.
        /// </summary>
        public void MomentRestore()
        {
            LabRestore();
            for (int i = 0; i < Enemies.Length; i++)
            {
                Enemies[i].Body.anchoredPosition = enemyBase[i];
                Enemies[i].Sprite.color = Color.white;
            }
            for (int i = 0; i < Allies.Length; i++)
            {
                Allies[i].Body.anchoredPosition = allyBase[i];
                RefreshAlly(Allies[i]);
            }
            if (turnBannerShow != null)
                StopCoroutine(turnBannerShow);
            turnBannerShow = null;
            if (TurnBanner != null)
                TurnBanner.gameObject.SetActive(false);
            if (Vfx != null)
                Vfx.Clear();
        }

        /// <summary>The band's whole time on screen, with <paramref name="hold"/> between its fades.</summary>
        private float BandTime(float hold) =>
            Mathf.Max(Settings.TurnBannerFade, hold) + Settings.TurnBannerFade;

        private IEnumerator Band(Action show)
        {
            show();
            yield return Wait(BandTime(Settings.TurnBannerHold));
        }

        private IEnumerator Notice(string text)
        {
            ShowNotice(text);
            yield return Wait(NoticeHold + 0.2f);
        }

        // --- The enemies appear ------------------------------------------------------------------

        /// <summary>
        /// The adventure's battle begins: the enemies appear, then the opening hand is dealt under
        /// the band of the party's turn, as the battle mock begins at once.
        /// </summary>
        private IEnumerator Opening()
        {
            yield return Encounter(BossEncounter, turnBanner: false);
            RestartDeal();
            ShowPartyTurnBanner(again: false);
        }

        private IEnumerator Encounter(bool boss, bool turnBanner = true)
        {
            var standing = new List<int>();
            for (int i = 0; i < Enemies.Length; i++)
                if (Enemies[i].Alive)
                    standing.Add(i);
            // Out of sight behind their places, dark.
            foreach (int i in standing)
            {
                Enemies[i].Group.alpha = 0f;
                Enemies[i].Sprite.color = Silhouette;
                Enemies[i].Body.anchoredPosition = enemyBase[i] + Vector2.right * EntranceDistance;
            }
            yield return Wait(0.25f);

            var first = boss ? standing.FindAll(i => !Enemies[i].Boss) : standing;
            var last = boss ? standing.FindAll(i => Enemies[i].Boss) : new List<int>();
            foreach (int i in first)
            {
                StartCoroutine(Entrance(i, EntranceTime, heavy: false));
                yield return Wait(EntranceGap);
            }
            yield return Wait(EntranceTime);
            if (last.Count > 0)
            {
                // The stage darkens and the boss comes on alone, slower and heavier.
                if (Vfx != null)
                    Vfx.DimTo(0.6f, 0.3f);
                yield return Wait(0.45f);
                foreach (int i in last)
                    StartCoroutine(Entrance(i, BossEntranceTime, heavy: true));
                yield return Wait(BossEntranceTime + 0.35f);
                if (Vfx != null)
                    Vfx.DimTo(0f, 0.6f);
            }
            ShowBand(
                last.Count > 0 ? BossEncounterText : EncounterText,
                EnemyBannerLine,
                EnemyBannerText,
                EncounterHold
            );
            yield return Wait(BandTime(EncounterHold));
            if (!turnBanner)
                yield break;
            ShowPartyTurnBanner(again: false);
            yield return Wait(BandTime(Settings.TurnBannerHold));
        }

        /// <summary>
        /// One enemy steps in from behind its place over <paramref name="seconds"/>, coming out of
        /// the dark into the light, and lands with its dust (heavily, for a boss).
        /// </summary>
        private IEnumerator Entrance(int index, float seconds, bool heavy)
        {
            var enemy = Enemies[index];
            var from = enemyBase[index] + Vector2.right * EntranceDistance * (heavy ? 1.5f : 1f);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float eased = 1f - (1f - k) * (1f - k) * (1f - k);
                enemy.Body.anchoredPosition =
                    enemyBase[index] + ToDots((from - enemyBase[index]) * (1f - eased));
                enemy.Group.alpha = Mathf.Min(1f, k * 3f);
                enemy.Sprite.color = Color.Lerp(
                    Silhouette,
                    Color.white,
                    Mathf.SmoothStep(0f, 1f, k)
                );
                yield return null;
            }
            enemy.Body.anchoredPosition = enemyBase[index];
            enemy.Group.alpha = 1f;
            enemy.Sprite.color = Color.white;
            if (Vfx != null)
                Vfx.Arrive(enemy.TargetArea, heavy);
            if (heavy)
                yield return Shake(enemy.Body, enemyBase[index], 4f, 0.25f, Vector2.down * 8f);
        }

        // --- The win and the drop ----------------------------------------------------------------

        // The enemy a drop falls from: the last one beaten.
        private int lastBeaten = -1;

        private IEnumerator Victory()
        {
            // The enemies still standing fall, one after another, as to a last blow.
            bool any = false;
            for (int i = 0; i < Enemies.Length; i++)
            {
                if (!Enemies[i].Alive)
                    continue;
                any = true;
                lastBeaten = i;
                Enemies[i].Hp = 0;
                RefreshEnemy(Enemies[i]);
                StartCoroutine(HitReaction(i, HitTint));
                yield return Wait(0.18f);
            }
            if (any)
                yield return Wait(Vfx != null ? 0.75f : 0.6f);
            // A breath of quiet before the cheer.
            yield return Wait(0.2f);
            ShowBand(VictoryText, VictoryBannerLine, VictoryBannerText, VictoryHold);
            var cheering = new List<RectTransform>();
            for (int i = 0; i < Allies.Length; i++)
            {
                if (Allies[i].Down)
                    continue;
                cheering.Add(Allies[i].TargetArea);
                StartCoroutine(Hop(i, i * 0.06f));
            }
            if (Vfx != null)
                Vfx.Cheer(cheering);
            yield return Wait(BandTime(VictoryHold));
        }

        /// <summary>An ally hops twice for joy, after <paramref name="delay"/>, in whole dots.</summary>
        private IEnumerator Hop(int index, float delay)
        {
            var ally = Allies[index];
            allyBusy[index]++;
            allyStance[index] = 0f;
            ally.HpBar.anchoredPosition = allyBarBase[index];
            yield return Wait(delay);
            for (int hop = 0; hop < 2; hop++)
            {
                float height = HopHeight * (hop == 0 ? 1f : 0.6f);
                for (float t = 0f; t < HopTime; t += Time.deltaTime)
                {
                    float u = t / HopTime;
                    ally.Body.anchoredPosition =
                        allyBase[index] + ToDots(Vector2.up * (4f * height * u * (1f - u)));
                    yield return null;
                }
                ally.Body.anchoredPosition = allyBase[index];
            }
            allyBusy[index]--;
        }

        private IEnumerator DropMoment(RewardItem drop, RewardReveal reveal)
        {
            if (reveal != null)
                reveal.HideNow();
            // The enemies still standing are taken away at once: the drop comes after the win.
            for (int i = 0; i < Enemies.Length; i++)
            {
                if (!Enemies[i].Alive)
                    continue;
                lastBeaten = i;
                Enemies[i].Hp = 0;
                Enemies[i].Group.alpha = 0f;
                RefreshEnemy(Enemies[i]);
            }
            RefreshTurnOrder();
            void ShowDrop()
            {
                if (reveal != null)
                    reveal.Show(drop);
            }
            int from =
                lastBeaten >= 0 && lastBeaten < Enemies.Length ? lastBeaten : Enemies.Length - 1;
            if (drop == null || Vfx == null || from < 0)
            {
                yield return Wait(0.4f);
                ShowDrop();
                yield return Wait(RewardReveal.OpenTime + 0.6f);
                yield break;
            }
            yield return Vfx.Drop(Enemies[from].TargetArea, drop.Quality, ShowDrop);
        }

        // --- The wipe-out and the revive ---------------------------------------------------------

        private IEnumerator Wipeout()
        {
            for (int i = 0; i < Allies.Length; i++)
            {
                if (Allies[i].Down)
                    continue;
                Allies[i].Hp = 0;
                RefreshAlly(Allies[i]);
                StartCoroutine(AllyHitReaction(i));
                yield return Wait(0.16f);
            }
            yield return Wait(0.5f);
            if (Vfx != null)
                Vfx.DimTo(0.5f, 0.6f);
            ShowBand(WipeoutText, WipeoutBannerLine, WipeoutBannerText, WipeoutHold);
            yield return Wait(BandTime(WipeoutHold));
            if (Vfx != null)
                Vfx.DimTo(0f, 0.5f);
            yield return Wait(0.3f);
        }

        /// <summary>
        /// The adventure's revive: the fallen party stands up again, then its next turn begins and
        /// the cards are dealt under the band of the party's turn.
        /// </summary>
        private IEnumerator Revived()
        {
            yield return ReviveMoment(turnBanner: false);
            BeginPartyTurn(again: false);
        }

        private IEnumerator ReviveMoment(bool turnBanner = true)
        {
            // The party must have fallen first; in the lab it falls at once, unseen.
            foreach (var ally in Allies)
            {
                if (ally.Down)
                    continue;
                ally.Hp = 0;
                RefreshAlly(ally);
            }
            yield return Wait(0.35f);
            if (Vfx != null)
                Vfx.Rise(Array.ConvertAll(Allies, ally => ally.TargetArea));
            yield return Wait(0.18f);
            // The grey lifts as the light rises, then the HP fills.
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                foreach (var ally in Allies)
                    ally.Sprite.color = Color.Lerp(DownTint, Color.white, t / 0.45f);
                yield return null;
            }
            foreach (var ally in Allies)
            {
                ally.Hp = ally.MaxHp;
                RefreshAlly(ally);
                ShowNumber(HealNumber, ally.TargetArea, ally.MaxHp.ToString());
            }
            ShowBand(ReviveText, PartyBannerLine, PartyBannerText, ReviveHold);
            yield return Wait(BandTime(ReviveHold));
            if (!turnBanner)
                yield break;
            ShowPartyTurnBanner(again: false);
            yield return Wait(BandTime(Settings.TurnBannerHold));
        }
    }
}
