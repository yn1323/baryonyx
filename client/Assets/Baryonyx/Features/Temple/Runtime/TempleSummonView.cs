using System;
using System.Threading.Tasks;
using Baryonyx.Economy;
using Baryonyx.Networking;
using Baryonyx.UI.GuideMenu;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Temple
{
    /// <summary>
    /// The temple's summoning, over the whole screen (doc/features/screens/temple.md): the cathedral's
    /// key art, the buttons that summon once or ten times, and a small button at the bottom left
    /// that opens the rates and what can come out (<see cref="TempleSummonRates"/>). A press
    /// summons on the server (<see cref="TempleSummonSession"/>), which spends the runes and keeps
    /// what came out; then the presentation plays (<see cref="TempleSummonShow"/> once,
    /// <see cref="TempleSummonTenShow"/> ten times), drawn by the scene's camera with Bloom while
    /// the guide screen is hidden, and the results window follows (<see cref="TempleSummonResults"/>).
    /// Without the presentations in the scene (the showcase), the results show at once.
    /// </summary>
    public sealed class TempleSummonView : GuidePanel, IGuideBackHandler
    {
        public Button Single;
        public Button Ten;
        public Button RatesButton;
        public TempleSummonResults Results;
        public TempleSummonRates Rates;

        // シーンに置いた演出。空なら、開いたときにシーンから探す。
        public TempleSummonShow Show;
        public TempleSummonTenShow TenShow;

        private readonly System.Random random = new();
        private int lastCount = 1;
        private bool busy;
        private Canvas hidden;
        private TaskCompletionSource<bool> presenting;

        // 実行中または直前の召喚。テストで完了を待つために公開する。
        public Task SummonTask { get; private set; } = Task.CompletedTask;

        /// <summary>Whether a presentation is on screen.</summary>
        public bool Presenting => presenting != null;

        private void OnEnable()
        {
            if (Single != null)
                Single.onClick.AddListener(SummonOnce);
            if (Ten != null)
                Ten.onClick.AddListener(SummonTen);
            if (RatesButton != null)
                RatesButton.onClick.AddListener(OpenRates);
            if (Results != null)
                Results.AgainPressed += SummonAgain;
            if (Show == null)
                Show = FindAnyObjectByType<TempleSummonShow>(FindObjectsInactive.Include);
            if (TenShow == null)
                TenShow = FindAnyObjectByType<TempleSummonTenShow>(FindObjectsInactive.Include);
        }

        private void OnDisable()
        {
            if (Single != null)
                Single.onClick.RemoveListener(SummonOnce);
            if (Ten != null)
                Ten.onClick.RemoveListener(SummonTen);
            if (RatesButton != null)
                RatesButton.onClick.RemoveListener(OpenRates);
            if (Results != null)
            {
                Results.AgainPressed -= SummonAgain;
                Results.Hide();
            }
            if (Rates != null)
                Rates.Hide();
            EndPresentation();
        }

        private void SummonOnce() => Summon(1);

        private void SummonTen() => Summon(TempleSummon.TenCount);

        private void SummonAgain() => Summon(lastCount);

        private void OpenRates()
        {
            if (Rates != null && !busy)
                Rates.Open();
        }

        // 演出の間の「もどる」と端末の戻るキーは、押して飛ばすのと同じにする。
        public bool HandleBack()
        {
            if (!Presenting)
                return false;
            TapPresentation();
            return true;
        }

        /// <summary>
        /// Summons <paramref name="count"/> times, plays the presentation and shows what came
        /// out. Without enough runes it only tells so; one summoning at a time.
        /// </summary>
        public void Summon(int count)
        {
            if (Results == null || busy)
                return;
            lastCount = Math.Max(1, count);
            if (RuneBalance.Known is { } balance && balance < TempleSummon.PriceOf(lastCount))
            {
                ShowNotice(TempleSummonSession.ShortMessage);
                return;
            }
            SummonTask = SummonAsync(lastCount);
        }

        private async Task SummonAsync(int count)
        {
            busy = true;
            try
            {
                var pull = await TempleSummonSession.SummonAsync(
                    count,
                    TempleSummonResults.PoolOf(Results.Data)
                );
                if (this == null || !isActiveAndEnabled)
                    return;
                Results.Hide();
                await PresentAsync(pull);
                if (this != null && isActiveAndEnabled)
                    Results.Show(pull.Results);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogWarning(TempleSummonSession.FailedMessage + "。" + exception.Message);
                ShowNotice(
                    exception is ServerApiException { StatusCode: 409 }
                        ? TempleSummonSession.ShortMessage
                        : TempleSummonSession.FailedMessage
                );
            }
            finally
            {
                busy = false;
            }
        }

        // 演出を、案内人の画面を隠して最後まで（または押して飛ばすまで）流す。
        private async Task PresentAsync(TempleSummonPull pull)
        {
            var results = pull.Results;
            bool ten = results.Count > 1;
            if (results.Count == 0 || (ten ? TenShow == null : Show == null))
                return;
            presenting = new TaskCompletionSource<bool>();
            var canvas = GetComponentInParent<Canvas>();
            hidden = canvas != null ? canvas.rootCanvas : null;
            if (hidden != null)
                hidden.enabled = false;
            if (ten)
            {
                TenShow.gameObject.SetActive(true);
                TenShow.Finished += EndPresentation;
                TenShow.Setup(results, random.Next(1, 100000));
                TenShow.Play();
            }
            else
            {
                Show.gameObject.SetActive(true);
                Show.Finished += EndPresentation;
                var tier = TempleSummonFx.TierOf(results[0]);
                // 金と虹は半分の確率で、1つ下の光で灯してから上げる（昇格）。結果より上の光では灯さない。
                var start =
                    tier != TempleSummonTier.White && random.NextDouble() < 0.5
                        ? (TempleSummonTier)((int)tier - 1)
                        : tier;
                Show.Setup(results[0], start, random.Next(1, 100000));
                Show.Play();
            }
            await presenting.Task;
        }

        /// <summary>A tap during the presentation: skips ahead (ten times: to the cards, then turns them).</summary>
        public void TapPresentation()
        {
            if (TenShow != null && TenShow.isActiveAndEnabled)
                TenShow.Tap();
            else if (Show != null && Show.isActiveAndEnabled)
                Show.Tap();
        }

        private void EndPresentation()
        {
            if (Show != null)
            {
                Show.Finished -= EndPresentation;
                Show.gameObject.SetActive(false);
            }
            if (TenShow != null)
            {
                TenShow.Finished -= EndPresentation;
                TenShow.gameObject.SetActive(false);
            }
            if (hidden != null)
                hidden.enabled = true;
            hidden = null;
            var done = presenting;
            presenting = null;
            done?.TrySetResult(true);
        }
    }
}
