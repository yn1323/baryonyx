using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.UI.GuideMenu
{
    /// <summary>
    /// A feature's own panel on a guide screen (the formation's adventurers, the adventurer's
    /// page, the change screens, the bonus settings): it tells the player on the guide screen's
    /// notice band, and makes the read it needs each time it opens (<see cref="GuidePanelLoad"/>),
    /// showing <see cref="LoadFailedText"/> and telling it when the read fails.
    /// </summary>
    public abstract class GuidePanel : MonoBehaviour
    {
        public const string LoadingText = GuidePanelLoad.LoadingText;
        public const string LoadFailedText = GuidePanelLoad.LoadFailedText;

        // 通知は案内人の画面の通知の帯に出す。
        public GuideMenuView Guide;

        /// <summary>The read the panel makes on opening. Cancel it when the panel closes.</summary>
        protected GuidePanelLoad Load { get; } = new();

        // 実行中または直前の読み込み。テストで完了を待つために公開する。
        public Task LoadTask => Load.Task;

        // 最後に出した通知。テストで確かめるために公開する。
        public string LastNotice { get; private set; } = "";

        public void ShowNotice(string message)
        {
            LastNotice = message ?? "";
            if (Guide != null)
                Guide.ShowToast(message);
        }

        /// <summary>
        /// Starts <paramref name="load"/>. If it fails, logs <paramref name="log"/> (or the
        /// message), calls <paramref name="showFailed"/> and tells <paramref name="failedMessage"/>.
        /// </summary>
        protected void StartLoad(
            Func<CancellationToken, Task> load,
            string failedMessage,
            Action showFailed,
            string log = null
        ) =>
            Load.Start(
                load,
                exception =>
                {
                    Debug.LogWarning((log ?? failedMessage) + "。" + exception.Message, this);
                    showFailed();
                    ShowNotice(failedMessage);
                }
            );
    }
}
