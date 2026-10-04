using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.CardLoadout;
using Baryonyx.Networking;
using Baryonyx.Party;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Training
{
    /// <summary>One passive or unique skill on the detail, baked by the generator.</summary>
    [Serializable]
    public sealed class TrainingSkillWidget
    {
        public Image Frame;
        public RawImage Icon;

        // 未解放のときだけ出す錠前と、解放するレベル（Lockの中）。
        public GameObject Lock;
        public TMP_Text Type;
        public TMP_Text Name;
        public TMP_Text Description;
        public TMP_Text When;
    }

    /// <summary>One card skill on the detail: cost, name and element icon.</summary>
    [Serializable]
    public sealed class TrainingCardWidget
    {
        public TMP_Text Cost;
        public TMP_Text Name;
        public Image Element;

        // 属性のないカードに出す「無」。
        public GameObject None;
    }

    /// <summary>One stat's row in the level-up dialog: before, after and the gain.</summary>
    [Serializable]
    public sealed class TrainingDiffWidget
    {
        public TMP_Text Before;
        public TMP_Text After;
        public TMP_Text Gain;
    }

    /// <summary>
    /// The tavern's training over the guide screen: the character on the left with the level-up
    /// and card buttons, their stats, skills and cards on the right, and the level-up dialog
    /// over them. TrainingAssets bakes every widget into the tavern prefab, so the text reads
    /// in the editor; this view wires them up and drives its own presenter. When the app has a
    /// server (<see cref="PartySession.Source"/>), it reads the party and runes on opening and
    /// raises levels there; otherwise it uses the mock data. Back (the button and the device
    /// key) closes the dialog before the guide screen goes back.
    /// </summary>
    public sealed class TrainingView : MonoBehaviour, ITrainingView, IGuideBackHandler
    {
        public const string LoadingText = "読み込み中…";
        public const string LoadFailedText = "取得できませんでした";
        public const string LoadFailedMessage = "キャラを取得できませんでした";

        // 案内人の画面の文字の色（GuideMenuAssets と同じ）と、足りないルーン・未解放のスキルの色。
        private static readonly Color Main = new(0.953f, 0.914f, 0.824f);
        private static readonly Color Sub = new(0.788f, 0.749f, 0.659f);
        private static readonly Color Short = new(1f, 0.45f, 0.4f);
        private static readonly Color LockedIcon = new(0.25f, 0.25f, 0.3f, 1f);
        private static readonly Color LockedFrame = new(0.62f, 0.62f, 0.68f, 1f);
        private static readonly Color LockedText = new(0.55f, 0.58f, 0.66f, 1f);

        public PartyMockData Party;
        public TrainingMockData Data;

        // 通知は案内人の画面の通知の帯に出す。
        public GuideMenuView Guide;

        [Header("Detail")]
        public Button Prev;
        public Button Next;
        public TMP_Text Name;
        public TMP_Text Level;
        public Image[] Elements = Array.Empty<Image>();
        public RawImage Figure;
        public TMP_Text Runes;
        public Button LevelUp;
        public TMP_Text LevelUpLabel;
        public GameObject LevelUpCost;
        public TMP_Text LevelUpCostLabel;
        public Button Cards;
        public TMP_Text[] Stats = Array.Empty<TMP_Text>();
        public TrainingSkillWidget[] Skills = Array.Empty<TrainingSkillWidget>();
        public TrainingCardWidget[] CardSlots = Array.Empty<TrainingCardWidget>();

        [Header("Level-up dialog")]
        public GameObject Dialog;
        public TMP_Text DialogName;
        public TMP_Text From;
        public TMP_Text To;
        public TMP_Text Count;
        public Button Less;
        public Button More;
        public Button Max;
        public Button Cancel;
        public Button Confirm;
        public TrainingDiffWidget[] Diffs = Array.Empty<TrainingDiffWidget>();
        public TMP_Text Learned;
        public GameObject NextBox;
        public RawImage NextIcon;
        public TMP_Text NextLevel;
        public TMP_Text NextName;
        public TMP_Text NextRemain;
        public TMP_Text Cost;
        public TMP_Text Balance;

        private TrainingPresenter presenter;
        private CancellationTokenSource loading;
        private readonly List<(Button Button, UnityEngine.Events.UnityAction Action)> bindings =
            new();

        public event Action PrevPressed;
        public event Action NextPressed;
        public event Action LevelUpPressed;
        public event Action LessPressed;
        public event Action MorePressed;
        public event Action MaxPressed;
        public event Action CancelPressed;
        public event Action ConfirmPressed;
        public event Action CardsPressed;

        public TrainingPresenter Presenter => presenter;
        public string LastNotice { get; private set; } = "";

        // 実行中または直前の読み込み。テストで完了を待つために公開する。
        public Task LoadTask { get; private set; } = Task.CompletedTask;

        private void OnEnable()
        {
            Bind(Prev, () => PrevPressed?.Invoke());
            Bind(Next, () => NextPressed?.Invoke());
            Bind(LevelUp, () => LevelUpPressed?.Invoke());
            Bind(Less, () => LessPressed?.Invoke());
            Bind(More, () => MorePressed?.Invoke());
            Bind(Max, () => MaxPressed?.Invoke());
            Bind(Cancel, () => CancelPressed?.Invoke());
            Bind(Confirm, () => ConfirmPressed?.Invoke());
            Bind(Cards, () => CardsPressed?.Invoke());

            // 開くたびに、サーバーのレベル・所持ルーン・編成（なければアプリを動かしている間のもの）で描き直す。
            presenter?.Dispose();
            presenter = null;
            if (Party == null || Data == null)
                return;
            var source = PartySession.Source;
            if (source == null)
            {
                presenter = Build(this, Party, Data);
                return;
            }
            ShowLoading();
            loading = new CancellationTokenSource();
            LoadTask = LoadAsync(source, loading.Token);
        }

        private async Task LoadAsync(IPartySource source, CancellationToken token)
        {
            try
            {
                var state = await source.LoadAsync(token);
                if (token.IsCancellationRequested)
                    return;
                PartySession.Use(Party, state);
                presenter = Build(
                    this,
                    Party,
                    Data,
                    (id, from, to, cancel) => LevelUpAsync(source, id, from, to, cancel)
                );
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (token.IsCancellationRequested)
                    return;
                Debug.LogWarning("育成のキャラを取得できませんでした。" + exception.Message, this);
                Set(Name, LoadFailedText);
                ShowNotice(LoadFailedMessage);
            }
        }

        // サーバーでレベルを上げる。断られたときは手元の表示がずれているので、読み直してから失敗を伝える。
        private async Task LevelUpAsync(
            IPartySource source,
            string id,
            int from,
            int to,
            CancellationToken token
        )
        {
            try
            {
                PartySession.Use(Party, await source.LevelUpAsync(id, from, to, token));
            }
            catch (ServerApiException exception) when (exception.StatusCode == 409)
            {
                try
                {
                    PartySession.Use(Party, await source.LoadAsync(token));
                }
                catch (Exception reload) when (reload is not OperationCanceledException)
                {
                    Debug.LogWarning("育成のキャラを読み直せませんでした。" + reload.Message, this);
                }
                throw;
            }
        }

        // 読み込むまで、仮データのキャラを本当のキャラとして見せない。
        private void ShowLoading()
        {
            Set(Name, LoadingText);
            Set(Level, "");
            Set(Runes, "--");
            foreach (var icon in Elements)
                if (icon != null)
                    icon.gameObject.SetActive(false);
            if (Figure != null)
                Figure.enabled = false;
            foreach (var button in new[] { Prev, Next, LevelUp, Cards })
                if (button != null)
                    button.interactable = false;
            if (LevelUpCost != null)
                LevelUpCost.SetActive(false);
            foreach (var stat in Stats)
                Set(stat, "");
            foreach (var skill in Skills)
                ShowSkill(skill, null);
            foreach (var card in CardSlots)
                ShowCard(card, null);
            if (Dialog != null)
                Dialog.SetActive(false);
        }

        private void OnDisable()
        {
            loading?.Cancel();
            loading?.Dispose();
            loading = null;
            foreach (var (button, action) in bindings)
                if (button != null)
                    button.onClick.RemoveListener(action);
            bindings.Clear();
            presenter?.Dispose();
            presenter = null;
        }

        /// <summary>
        /// A presenter over the running app's party and runes, raising levels with
        /// <paramref name="levelUp"/> when the app has a server. The generator uses it too, so
        /// the prefab shows the first character in the editor.
        /// </summary>
        public static TrainingPresenter Build(
            ITrainingView view,
            PartyMockData party,
            TrainingMockData data,
            Func<string, int, int, CancellationToken, Task> levelUp = null
        ) =>
            new(
                view,
                TrainingRoster.From(PartySession.Formation(party), data),
                new TrainingSessionStore(party, data),
                party.IconOf,
                levelUp
            );

        public bool HandleBack() => presenter != null && presenter.Back();

        public void Render(TrainingState state)
        {
            RenderDetail(state);
            RenderDialog(state);
        }

        private void RenderDetail(TrainingState state)
        {
            Set(Name, state.Name);
            Set(Level, state.Level.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < Elements.Length; i++)
            {
                var icon = i < state.Elements.Count ? state.Elements[i] : null;
                Elements[i].sprite = icon;
                Elements[i].gameObject.SetActive(icon != null);
            }
            if (Figure != null)
            {
                Figure.enabled = state.Member != null;
                if (state.Member != null)
                    PartyFormationView.Paint(Figure, state.Member, trim: false);
            }
            Set(Runes, Number(state.Runes));
            if (Prev != null)
                Prev.interactable = state.CanSwitch;
            if (Next != null)
                Next.interactable = state.CanSwitch;

            if (LevelUp != null)
                LevelUp.interactable = !state.Maxed;
            if (Cards != null)
                Cards.interactable = state.Member != null;
            Set(LevelUpLabel, state.Maxed ? "レベル上限" : "レベルアップ");
            if (LevelUpCost != null)
                LevelUpCost.SetActive(!state.Maxed);
            Set(LevelUpCostLabel, Number(state.NextCost));

            for (int i = 0; i < Stats.Length && i < TrainingStats.Count; i++)
                Set(Stats[i], state.Stats[i].ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < Skills.Length; i++)
                ShowSkill(Skills[i], i < state.Skills.Count ? state.Skills[i] : null);
            for (int i = 0; i < CardSlots.Length; i++)
                ShowCard(CardSlots[i], i < state.Cards.Count ? state.Cards[i] : null);
        }

        private void RenderDialog(TrainingState state)
        {
            if (Dialog != null)
                Dialog.SetActive(state.DialogOpen);
            Set(DialogName, state.Name);
            Set(From, state.Level.ToString(CultureInfo.InvariantCulture));
            Set(To, state.Target.ToString(CultureInfo.InvariantCulture));
            Set(Count, state.Count.ToString(CultureInfo.InvariantCulture));
            if (Less != null)
                Less.interactable = state.CanLess;
            if (More != null)
                More.interactable = state.CanMore;
            if (Confirm != null)
                Confirm.interactable = state.CanConfirm;
            for (int i = 0; i < Diffs.Length && i < TrainingStats.Count; i++)
            {
                int before = state.Stats[i];
                int after = state.TargetStats[i];
                Set(Diffs[i].Before, before.ToString(CultureInfo.InvariantCulture));
                Set(Diffs[i].After, after.ToString(CultureInfo.InvariantCulture));
                Set(Diffs[i].Gain, $"+{after - before}");
            }
            Set(
                Learned,
                state.Learned.Count == 0
                    ? "なし"
                    : string.Join("　", state.Learned.Select(skill => $"「{skill.Name}」"))
            );
            var next = state.NextUnlock;
            if (NextBox != null)
                NextBox.SetActive(next != null);
            if (next != null)
            {
                if (NextIcon != null)
                {
                    NextIcon.texture = next.Icon;
                    NextIcon.enabled = next.Icon != null;
                }
                Set(NextLevel, $"Lv {next.UnlockLevel}・{next.Type}");
                Set(NextName, next.Name);
                Set(NextRemain, $"あと{next.UnlockLevel - state.Target}レベル");
            }
            Set(Cost, Number(state.Cost));
            if (Cost != null)
                Cost.color = state.CanAfford ? Main : Short;
            Set(
                Balance,
                state.CanAfford
                    ? $"所持 {Number(state.Runes)} → のこり {Number(state.Remaining)}"
                    : $"ルーンが {Number(-state.Remaining)} 足りません"
            );
            if (Balance != null)
                Balance.color = state.CanAfford ? Sub : Short;
        }

        private static void ShowSkill(TrainingSkillWidget widget, TrainingSkillState skill)
        {
            bool open = skill == null || skill.Unlocked;
            if (widget.Icon != null)
            {
                widget.Icon.texture = skill?.Icon;
                widget.Icon.enabled = skill?.Icon != null;
                widget.Icon.color = open ? Color.white : LockedIcon;
            }
            if (widget.Frame != null)
                widget.Frame.color = open ? Color.white : LockedFrame;
            if (widget.Lock != null)
                widget.Lock.SetActive(!open);
            Set(
                widget.Type,
                skill == null ? ""
                    : skill.Energy > 0 ? $"{skill.Type}　エネルギー {skill.Energy}"
                    : skill.Type
            );
            Set(widget.Name, skill?.Name);
            Set(widget.Description, skill?.Description);
            Set(widget.When, open ? "" : $"Lv {skill.UnlockLevel}");
            if (widget.Name != null)
                widget.Name.color = open ? Main : LockedText;
        }

        private static void ShowCard(TrainingCardWidget widget, TrainingCardState card)
        {
            Set(widget.Cost, card != null ? card.Cost.ToString(CultureInfo.InvariantCulture) : "");
            Set(widget.Name, card?.Name);
            if (widget.Element != null)
            {
                widget.Element.sprite = card?.Icon;
                widget.Element.gameObject.SetActive(card?.Icon != null);
            }
            if (widget.None != null)
                widget.None.SetActive(card != null && card.Icon == null);
        }

        public void ShowNotice(string message)
        {
            LastNotice = message ?? "";
            if (Guide != null)
                Guide.ShowToast(message);
        }

        /// <summary>
        /// Moves to the tavern's card skills with this character chosen. Their back comes back
        /// here, where the same character is still on screen.
        /// </summary>
        public bool OpenCards(string id)
        {
            if (Guide == null || !Guide.HasItem(CardLoadoutSession.GuideItemKey))
                return false;
            var guide = Guide;
            CardLoadoutSession.Open(id, () => guide.OpenItem(TrainingSession.GuideItemKey));
            return guide.OpenItem(CardLoadoutSession.GuideItemKey);
        }

        private static string Number(long value) =>
            value.ToString("#,0", CultureInfo.InvariantCulture);

        private void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;
            button.onClick.AddListener(action);
            bindings.Add((button, action));
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }
    }
}
