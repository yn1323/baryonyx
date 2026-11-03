using System;
using System.Collections.Generic;
using System.Globalization;
using Baryonyx.Networking;
using Baryonyx.Party;
using Baryonyx.UI;
using Baryonyx.UI.EditModePreview;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static Baryonyx.UI.UiText;

namespace Baryonyx.Home
{
    /// <summary>
    /// Draws a <see cref="HomeViewState"/> into the generated prefab and reports button
    /// presses as <see cref="HomeAction"/> values. It owns no game rules. Without a
    /// listener (the showcase preview), tapping the step panel plays sample feedback.
    /// </summary>
    public sealed class HomeView : MonoBehaviour, IEditModePreview
    {
        public static readonly Color GaugeFilled = new(0.37f, 0.83f, 0.78f);
        public static readonly Color GaugeAchieved = new(0.95f, 0.76f, 0.31f);
        public static readonly Color GaugeEmpty = new(0.95f, 0.91f, 0.82f, 0.14f);
        public static readonly Color TextMain = new(0.95f, 0.91f, 0.82f);

        [Header("歩数")]
        public Button StepButton;
        public TMP_Text DateLabel;
        public GameObject StepDetails;
        public GameObject UnlinkedDetails;
        public TMP_Text StepsLabel;
        public Image[] Segments = Array.Empty<Image>();
        public TMP_Text ClaimLabel;

        // 案内の行（アイコンと文言）。TopのTAP TO STARTと同じ周期と濃さで点滅させる。
        public CanvasGroup ClaimGroup;

        [Min(0.1f)]
        public float ClaimPulseSeconds = 2.4f;

        [Range(0f, 1f)]
        public float ClaimPulseMinimumAlpha = 0.35f;

        [Header("右上")]
        public TMP_Text RunesLabel;
        public Button SettingsButton;

        [Tooltip("設定の下の、デバッグルームの一覧を出すボタン（公開前の確認用）。")]
        public Button DebugRoomsButton;

        [Tooltip("デバッグルームの一覧を出すダイアログ。")]
        public GameDialog Dialog;

        [Tooltip("Homeへ戻るボタンを持たないデバッグルームに、開いたあとで重ねる「ホームへ」。")]
        public DebugRoomReturn RoomReturn;

        [Header("世界")]
        public Button PartyWorldButton;

        [Tooltip(
            "焚き火を囲む4人の絵（編成の枠の順）。右の2人は焚き火を向くよう左右を反転して置く。"
        )]
        public RawImage[] PartyMembers = Array.Empty<RawImage>();

        [Tooltip("4人の足元の影（PartyMembers と同じ順）。枠が空いていれば絵と一緒に隠す。")]
        public Graphic[] PartyShadows = Array.Empty<Graphic>();

        [Header("左下")]
        [FormerlySerializedAs("PartyButton")]
        public Button TavernButton;

        [FormerlySerializedAs("EquipmentButton")]
        public Button WorkshopButton;

        [FormerlySerializedAs("SummonButton")]
        public Button TempleButton;

        public Button LibraryButton;

        [Header("右下")]
        public Button ResumeButton;
        public TMP_Text DestinationNameLabel;
        public TMP_Text DestinationFloorLabel;
        public TMP_Text ResumeLabel;

        // カードの絵。冒険の途中は行き先の絵、そうでなければ世界地図の絵。
        public RawImage DestinationArt;
        public Texture ResumeArt;
        public Texture TravelArt;

        [Header("通知")]
        public CanvasGroup Toast;
        public TMP_Text ToastLabel;

        [Min(0.1f)]
        public float ToastSeconds = 1.6f;

        [Min(0f)]
        public float ToastFadeSeconds = 0.3f;

        // 画面中央に出す結果の表示（「獲得ルーンはありません」）。
        public CanvasGroup Notice;
        public TMP_Text NoticeLabel;

        [Min(0.1f)]
        public float NoticeSeconds = 1.4f;

        [Header("ルーン獲得")]
        // 粒子はACTパネルを押した位置（分からなければ案内の文字）から出て、右上の所持ルーンのアイコンへ飛ぶ。
        public RectTransform RuneOrigin;
        public RuneGainEffect RuneGain;

        private readonly List<(Button button, UnityEngine.Events.UnityAction listener)> bindings =
            new();
        private FadingMessage toastFade;
        private FadingMessage noticeFade;
        private bool claimPulses;

        // 最後に描画した所持ルーン。描画前（Prefabのプレビュー）は焼き込んだ表示を残すためnull。
        private string runesText;
        private bool previewShowsNotice;

        // ACTパネルを押した画面上の位置。ルーンはここから弾ける。取れなければパネルのアイコンから出す。
        private Vector2? tapScreenPoint;

        public event Action<HomeAction> ActionRequested;

        public string CurrentToast => Toast != null && Toast.alpha > 0 ? ToastLabel.text : "";
        public string CurrentNotice => Notice != null && Notice.alpha > 0 ? NoticeLabel.text : "";

        // 演出の間は、所持ルーンを付与前の値から数え上げる。終わると最新の表示に戻す。
        public bool RuneGainPlaying => RuneGain != null && RuneGain.Playing;

        // 直前の演出でルーンが弾けた中心（演出の層の座標）。テストで確かめる。
        internal Vector3 BurstOrigin => RuneGain != null ? RuneGain.BurstOrigin : Vector3.zero;

        private void OnEnable()
        {
            Bind(StepButton, HomeAction.SyncSteps);
            Bind(SettingsButton, HomeAction.Settings);
            Bind(DebugRoomsButton, HomeAction.DebugRooms);
            // The party standing in the world opens the tavern, where the party is formed.
            Bind(PartyWorldButton, HomeAction.Tavern);
            Bind(TavernButton, HomeAction.Tavern);
            Bind(WorkshopButton, HomeAction.Workshop);
            Bind(TempleButton, HomeAction.Temple);
            Bind(LibraryButton, HomeAction.Library);
            Bind(ResumeButton, HomeAction.Resume);
            HideToast();
            HideNotice();
            if (RuneGain != null)
            {
                RuneGain.Finished -= ShowRunes;
                RuneGain.Finished += ShowRunes;
                RuneGain.Finish();
            }
            // 案内人の画面で先に表示した変更を、ホームへ戻ったあとに保存できなかったときも知らせる。
            SaveQueue.Failed += ShowNotice;
        }

        private void OnDisable()
        {
            SaveQueue.Failed -= ShowNotice;
            foreach (var (button, listener) in bindings)
                if (button != null)
                    button.onClick.RemoveListener(listener);
            bindings.Clear();
            if (RuneGain != null)
            {
                RuneGain.Finish();
                RuneGain.Finished -= ShowRunes;
            }
        }

        // 演出が終わったら、最後に描画した所持ルーンに戻す。
        private void ShowRunes()
        {
            if (runesText != null)
                Set(RunesLabel, runesText);
        }

        public void Render(HomeViewState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            Set(DateLabel, state.DateText);
            if (StepDetails != null)
                StepDetails.SetActive(state.ShowSteps);
            if (UnlinkedDetails != null)
                UnlinkedDetails.SetActive(!state.ShowSteps);
            Set(StepsLabel, state.ActText);
            if (StepsLabel != null)
                StepsLabel.color = state.DailyAchieved ? GaugeAchieved : TextMain;
            for (int i = 0; i < Segments.Length; i++)
            {
                if (Segments[i] == null)
                    continue;
                Segments[i].color =
                    i >= state.FilledSegments ? GaugeEmpty
                    : state.DailyAchieved ? GaugeAchieved
                    : GaugeFilled;
            }
            Set(ClaimLabel, state.ClaimText);
            claimPulses = state.ClaimPulses;
            if (!claimPulses && ClaimGroup != null)
                ClaimGroup.alpha = 1f;
            runesText = state.RunesText ?? "";
            if (!RuneGainPlaying)
                Set(RunesLabel, runesText);
            Set(DestinationNameLabel, state.DestinationNameText);
            Set(DestinationFloorLabel, state.DestinationFloorText);
            Set(ResumeLabel, state.ResumeText);
            ShowCardArt(state.AdventureInProgress ? ResumeArt : TravelArt);
        }

        // 絵をカードいっぱいに切り抜く（上から30%の位置を中心に寄せる）。
        private void ShowCardArt(Texture art)
        {
            if (DestinationArt == null || art == null || DestinationArt.texture == art)
                return;
            DestinationArt.texture = art;
            var size = DestinationArt.rectTransform.rect.size;
            if (size.x <= 0f || size.y <= 0f)
                return;
            DestinationArt.uvRect = CoverUv(size, art.width, art.height);
        }

        public static Rect CoverUv(Vector2 card, float width, float height)
        {
            float visible = Mathf.Min(1f, (card.y / card.x) * width / height);
            return new Rect(0, 1 - visible - (1 - visible) * 0.3f, 1, visible);
        }

        private void Update()
        {
            // 端末の戻るキー（Androidの戻るはEscapeで届く）：開いている窓を閉じ、窓がなければ何もしない
            // （doc/rules/ui-design.md#操作の決まり）。
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                PressBack();
            if (claimPulses && ClaimGroup != null)
                ClaimGroup.alpha = PulseAlpha(
                    Time.unscaledTime,
                    ClaimPulseSeconds,
                    ClaimPulseMinimumAlpha
                );
        }

        /// <summary>The device's back key: closes the dialog when one is open, and does nothing otherwise.</summary>
        public void PressBack()
        {
            if (Dialog != null)
                Dialog.HandleBack();
        }

        // 周期の始めと終わりで1、半周期で最小値になる。
        public static float PulseAlpha(float time, float periodSeconds, float minimumAlpha)
        {
            float period = Mathf.Max(0.1f, periodSeconds);
            float wave = (Mathf.Cos(time / period * Mathf.PI * 2f) + 1f) * 0.5f;
            return Mathf.Lerp(Mathf.Clamp01(minimumAlpha), 1f, wave);
        }

        /// <summary>
        /// Stands the formation's members around the campfire, in slot order (null for an empty
        /// slot, whose place is left empty).
        /// </summary>
        public void UseParty(IReadOnlyList<PartyMember> slots)
        {
            if (slots == null)
                return;
            for (int i = 0; i < PartyMembers.Length; i++)
            {
                var member = i < slots.Count ? slots[i] : null;
                var picture = PartyMembers[i];
                if (picture == null)
                    continue;
                picture.gameObject.SetActive(member != null);
                if (i < PartyShadows.Length && PartyShadows[i] != null)
                    PartyShadows[i].gameObject.SetActive(member != null);
                PartyArt.Stand(picture, member);
            }
        }

        public void ShowToast(string message)
        {
            if (!string.IsNullOrEmpty(message))
                ToastFade.Show(Toast, ToastLabel, message, ToastSeconds, ToastFadeSeconds);
        }

        public void HideToast() => ToastFade.Hide(Toast);

        public void ShowNotice(string message)
        {
            if (!string.IsNullOrEmpty(message))
                NoticeFade.Show(Notice, NoticeLabel, message, NoticeSeconds, ToastFadeSeconds);
        }

        public void HideNotice() => NoticeFade.Hide(Notice);

        // 停止中の表示の切り替え：仮データ（HomeMockData の既定値）を変えて、Play中と同じ Render で描く。
        // 通知と中央の表示は、消えずに止まった形で出す。
        public IReadOnlyList<string> PreviewStates =>
            new[]
            {
                "歩数の連携なし",
                "同期中",
                "歩数の取得前",
                "ゲージ満タン",
                "冒険の途中",
                "獲得ルーンなし",
                "通知",
            };

        public void ShowPreviewState(int index)
        {
            var mock = ScriptableObject.CreateInstance<HomeMockData>();
            try
            {
                if (index == 0)
                    mock.StepLink = HomeStepLink.Unlinked;
                if (index == 3)
                    mock.Steps = HomeViewState.GaugeMaxAct;
                mock.AdventureInProgress = index == 4;
                var snapshot = mock.ToSnapshot(DateTime.Today);
                snapshot.StepSyncing = index == 1;
                snapshot.StepsKnown = index != 2;
                snapshot.RunesKnown = index != 2;
                Render(HomeViewState.From(snapshot));
            }
            finally
            {
                DestroyImmediate(mock);
            }
            if (index == 5)
                ShowStill(Notice, NoticeLabel, HomePresenter.NoRunesMessage);
            if (index == 6)
                ShowStill(
                    Toast,
                    ToastLabel,
                    HomeViewState.MessageFor(HomeAction.Settings, HomeStepLink.Linked)
                );
        }

        private static void ShowStill(CanvasGroup group, TMP_Text label, string message)
        {
            if (group == null || label == null)
                return;
            label.text = message;
            group.alpha = 1f;
        }

        private FadingMessage ToastFade => toastFade ??= new FadingMessage(this);
        private FadingMessage NoticeFade => noticeFade ??= new FadingMessage(this);

        // 所持ルーンを from から to まで、粒子が着くたびに増やして見せる。
        public void PlayRuneGain(long from, long to)
        {
            if (RuneGain == null)
                return;
            RuneGain.Finish();
            if (to <= from || !isActiveAndEnabled || RuneOrigin == null)
                return;
            RuneGain.Play(from, to, TapPoint() ?? RuneGain.PointOf(RuneOrigin));
        }

        // 押した位置を覚える。直接呼び出した場合など、ポインターがパネルの外にあれば使わない。
        private void RememberTap(Button button)
        {
            tapScreenPoint = null;
            var pointer = Pointer.current;
            if (pointer == null || button == null)
                return;
            Vector2 point = pointer.position.ReadValue();
            if (
                RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)button.transform,
                    point,
                    EventCamera()
                )
            )
                tapScreenPoint = point;
        }

        // 覚えた位置を演出の層の座標にして返す。1回使ったら忘れる。
        private Vector3? TapPoint()
        {
            var point = tapScreenPoint;
            tapScreenPoint = null;
            return point.HasValue ? RuneGain.LayerPointOf(point.Value) : null;
        }

        // Overlayのキャンバスではカメラを使わず、展示室のカメラ描画ではそのカメラで座標を変換する。
        private Camera EventCamera()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
        }

        // 展示室のプレビューには受け手がいないため、ACTパネルで獲得と獲得なしを交互に見せる。
        private void PlayPreview(HomeAction action)
        {
            if (action != HomeAction.SyncSteps)
                return;
            previewShowsNotice = !previewShowsNotice;
            if (!previewShowsNotice)
            {
                ShowNotice(HomePresenter.NoRunesMessage);
                return;
            }
            long.TryParse(
                runesText ?? (RunesLabel != null ? RunesLabel.text : ""),
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out long from
            );
            long to = from + 1340;
            runesText = HomeViewState.Runes(to);
            PlayRuneGain(from, to);
        }

        private void Bind(Button button, HomeAction action)
        {
            if (button == null)
                return;
            UnityEngine.Events.UnityAction listener = () =>
            {
                if (action == HomeAction.SyncSteps)
                    RememberTap(button);
                if (ActionRequested != null)
                    ActionRequested.Invoke(action);
                else
                    PlayPreview(action);
            };
            button.onClick.AddListener(listener);
            bindings.Add((button, listener));
        }
    }
}
