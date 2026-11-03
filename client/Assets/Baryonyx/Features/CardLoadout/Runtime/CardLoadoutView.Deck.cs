using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Party;
using Baryonyx.Training;
using Baryonyx.UI;
using TMPro;
using UnityEngine;

namespace Baryonyx.CardLoadout
{
    // スキルのタブの6枚（戦闘のカード）と、押すと開く選ぶ画面（doc/features/screens/loadout.md#スキル）。
    // 覚醒のタブは左の6行（UniqueRows・SlotRows）を使い、ここの部品は空にしている。
    public sealed partial class CardLoadoutView
    {
        // 見出し「固有スキル」の下の3枚と、見出し「カスタムスキル」の下の3枚。
        public CardLoadoutCard[] UniqueCards = Array.Empty<CardLoadoutCard>();
        public CardLoadoutCard[] SlotCards = Array.Empty<CardLoadoutCard>();
        public CardLoadoutPicker Picker;

        // 左の縦の欄のステータス8つの数字（CharacterStats の順）。カードの効果はステータスの割合で書くので、
        // 見比べるときに実際の数字を読めるようにする（2026-11-03、ユーザーが4案から選んだ）。
        public TMP_Text[] Stats = Array.Empty<TMP_Text>();

        // 戦闘のカードの属性ごとの枠と、右上の属性の絵（CardElement の順）。
        public Texture[] CardFrames = Array.Empty<Texture>();
        public Texture[] ElementArts = Array.Empty<Texture>();

        // 停止中の表示の切り替えで、選ぶ画面を出すときの名前。
        public const string ChoicesPreviewState = "スキルを選ぶとき";
        public const string StagesPreviewState = "固有スキルを選ぶとき";

        public const string EmptyText = "空き";

        private readonly List<(CardLoadoutCard Card, Action Action)> cardBindings = new();

        // ステータスに足す、付けている装備。開くたびに読み、読むまでは前に読んだ物（なければ装備なし）で出す。
        private EquipmentState worn;
        private CancellationTokenSource wearing;
        private PartyFormation shownFormation;

        private bool HasCards => UniqueCards.Length > 0 || SlotCards.Length > 0;

        private void BindCards()
        {
            for (int i = 0; i < UniqueCards.Length; i++)
            {
                int index = i;
                BindCard(UniqueCards[i], () => OpenUnique(index));
            }
            for (int i = 0; i < SlotCards.Length; i++)
            {
                int index = i;
                BindCard(SlotCards[i], () => OpenSlot(index));
            }
            if (Picker != null)
            {
                Picker.CardPressed += ChooseInPicker;
                Picker.StagePressed += ChooseStageInPicker;
                Picker.Close();
            }
            if (Stats.Length > 0)
            {
                worn = EquipmentSession.Known;
                wearing = new CancellationTokenSource();
                _ = ReadWornAsync(wearing.Token);
            }
        }

        private async Task ReadWornAsync(CancellationToken token)
        {
            try
            {
                var state = await EquipmentSession.SourceOrLocal.LoadAsync(token);
                if (token.IsCancellationRequested || this == null)
                    return;
                worn = state;
                ShowStats(presenter?.State?.PersonId);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                // 装備を読めなくても、スキルは付け替えられる。ステータスは装備なしで出したままにする。
                Debug.LogWarning("[CardLoadout] 装備を読めませんでした: " + error.Message);
            }
        }

        // 選んでいる人のステータス：レベルと突破の段階の値に、付けている装備の分を足す（ステータスのタブと同じ）。
        private void ShowStats(string personId)
        {
            if (Stats.Length == 0)
                return;
            var formation =
                shownFormation ?? (Party != null ? PartySession.Formation(Party) : null);
            var member =
                personId != null && formation != null && Training != null
                    ? TrainingRoster
                        .From(formation, Training)
                        .FirstOrDefault(entry => entry.Id == personId)
                    : null;
            if (member == null)
            {
                StatGrid.Show(Stats, null);
                return;
            }
            var store = new TrainingSessionStore(Party, Training);
            var stats = member.Growth.StatsAt(store.LevelOf(member.Id), store.BondOf(member.Id));
            if (worn != null)
                stats = EquipmentStats.With(stats, worn, member.Id);
            StatGrid.Show(Stats, stats);
        }

        private void BindCard(CardLoadoutCard card, Action action)
        {
            if (card == null || card.Glass == null)
                return;
            card.Glass.Clicked += action;
            cardBindings.Add((card, action));
        }

        private void UnbindCards()
        {
            foreach (var (card, action) in cardBindings)
                if (card != null && card.Glass != null)
                    card.Glass.Clicked -= action;
            cardBindings.Clear();
            wearing?.Cancel();
            wearing = null;
            if (Picker != null)
            {
                Picker.CardPressed -= ChooseInPicker;
                Picker.StagePressed -= ChooseStageInPicker;
                Picker.Close();
            }
        }

        // カスタムスキルの枠を押すと、その枠を選んで候補を開く。
        private void OpenSlot(int index)
        {
            if (presenter == null || Picker == null)
                return;
            SlotPressed?.Invoke(index);
            Picker.OpenFor(false);
            ShowPicker(presenter.State);
        }

        // 固有スキルを押すと、その覚醒ツリーの段階を開く。
        private void OpenUnique(int index)
        {
            if (presenter == null || Picker == null)
                return;
            UniquePressed?.Invoke(index);
            Picker.OpenFor(true);
            ShowPicker(presenter.State);
        }

        // 選んだら、幕を閉じてから付け替える。6枚の画面で変わったカードが回る。
        private void ChooseInPicker(string key)
        {
            Picker.Close();
            CardPressed?.Invoke(key);
        }

        private void ChooseStageInPicker(int stage)
        {
            Picker.Close();
            StagePressed?.Invoke(stage);
        }

        private void ShowPicker(CardLoadoutState state)
        {
            if (Picker != null && Picker.Open && state != null)
                Picker.Show(state, DrawCard);
        }

        // 6枚。付け替えた枠のカードは、描き替える前に縦に回す。
        private void ShowCards(CardLoadoutState state, GlassCardTurns turning)
        {
            for (int i = 0; i < UniqueCards.Length; i++)
            {
                var unique =
                    state.Uniques != null && i < state.Uniques.Count ? state.Uniques[i] : null;
                var card = unique?.Card;
                bool locked = unique != null && unique.Locked;
                turning.Show(
                    i,
                    UniqueCards[i].Glass,
                    TurnKey(card, card != null, locked),
                    card != null ? ArtOf(card) : null
                );
                ShowDeckCard(UniqueCards[i], card, locked, unique?.UnlockLevel ?? 0);
            }
            for (int i = 0; i < SlotCards.Length; i++)
            {
                var card = state.Slots != null && i < state.Slots.Count ? state.Slots[i] : null;
                bool filled = card != null && card.Id != null;
                turning.Show(
                    UniqueCards.Length + i,
                    SlotCards[i].Glass,
                    TurnKey(card, filled, false),
                    filled ? ArtOf(card) : null
                );
                ShowDeckCard(SlotCards[i], filled ? card : null, false, 0);
            }
            ShowStats(state.PersonId);
            ShowPicker(state);
        }

        // 6枚の1枚。未解放の固有スキルは暗くして効果の代わりに「Lv 30で解放」、空いた枠は点線のカード。
        private void ShowDeckCard(
            CardLoadoutCard widget,
            CardLoadoutCardState card,
            bool locked,
            int unlockLevel
        )
        {
            if (widget == null)
                return;
            widget.Id = card?.Id ?? "";
            if (card == null || card.Id == null)
                widget.ShowEmpty();
            else
                DrawCard(widget, card, !locked, locked ? UnlockText(unlockLevel) : null);
            widget.Glass.Selected = false;
            if (card == null || card.Id == null)
                widget.SetMark(null, false);
        }

        private void DrawCard(
            CardLoadoutCard widget,
            CardLoadoutCardState card,
            bool playable,
            string note
        )
        {
            widget.Show(
                card,
                ArtOf(card),
                FrameOf(card.ItemId != null ? CardElement.None : card.Element),
                card.ItemId != null ? null : ElementArtOf(card.Element),
                playable,
                note
            );
            widget.SetMark(null, false);
        }

        private Texture FrameOf(CardElement element) => At(CardFrames, (int)element);

        private Texture ElementArtOf(CardElement element) =>
            element == CardElement.None ? null : At(ElementArts, (int)element);

        private static T At<T>(T[] items, int index)
            where T : class => index >= 0 && index < items.Length ? items[index] : null;

        // 読み込むまで、6枚を伏せて仮データを見せない。
        private void ShowCardsLoading()
        {
            StatGrid.Show(Stats, null);
            foreach (var card in UniqueCards.Concat(SlotCards))
                if (card != null)
                    card.ShowBack(false);
            if (Picker != null)
                Picker.Close();
        }

        // 戻るキーと「もどる」は、選ぶ画面を開いていれば閉じるだけにする。
        private bool ClosePicker()
        {
            if (Picker == null || !Picker.Open)
                return false;
            Picker.Close();
            return true;
        }

        private static readonly string[] CardPreviewStates =
        {
            ChoicesPreviewState,
            StagesPreviewState,
        };

        /// <summary>
        /// Opens the picker on <paramref name="state"/> and closes it again, leaving its cards
        /// drawn: the generator bakes the first custom slot's choices and the first unique
        /// skill's tree, so the stopped editor's preview reads them.
        /// </summary>
        public void BakePicker(CardLoadoutState state, bool tree)
        {
            if (Picker == null || state == null)
                return;
            Picker.OpenFor(tree);
            Picker.Show(state, DrawCard);
            Picker.gameObject.SetActive(false);
        }

        // 停止中：生成時に作り込んだ候補（1つ目のカスタムスキルの枠）か、1つ目の固有スキルのツリーを開く。
        private void ShowPickerPreview(bool tree)
        {
            if (Picker == null)
                return;
            Picker.gameObject.SetActive(true);
            if (Picker.ChoicesRoot != null)
                Picker.ChoicesRoot.SetActive(!tree);
            if (Picker.TreeRoot != null)
                Picker.TreeRoot.SetActive(tree);
        }
    }
}
