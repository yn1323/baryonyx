using System.Collections.Generic;
using Baryonyx.UI.EditModePreview;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The battle screen's states while the editor is stopped (doc/rules/ui-design.md#停止中の表示の切り替え):
    /// the turn bands and the skill name's band held still, and a card of the opening hand raised.
    /// They use the bands' and cards' own drawing, on the prefab's baked hand, without Awake.
    /// </summary>
    public sealed partial class BattleInspectView : IEditModePreview
    {
        public IReadOnlyList<string> PreviewStates =>
            new[]
            {
                "ターンの帯（味方）",
                "ターンの帯（連続）",
                "ターンの帯（敵）",
                "技名の帯",
                "エネルギー不足",
                "カードを選んだ（タップ）",
                "カードを敵へ向けた（スワイプ）",
                "キーワードの説明札",
                "予告（倒せる敵）",
                "予告（回復）",
                "予告（ブロック）",
                "状態の一覧",
                "覚醒の予兆（光るカード）",
                "キーワードの説明札（味方向け）",
                "山札の一覧",
            };

        public void ShowPreviewState(int index)
        {
            switch (index)
            {
                case 0:
                    StillTurnBanner(PartyTurnText(StartTurn, again: false), enemies: false);
                    break;
                case 1:
                    StillTurnBanner(PartyTurnText(StartTurn + 1, again: true), enemies: false);
                    break;
                case 2:
                    StillTurnBanner(EnemyTurnText, enemies: true);
                    break;
                case 3:
                    int card = PreviewCard();
                    if (card >= 0 && SkillBanner != null && SkillBanner.Label != null)
                        ShowBanner(Deck[Cards[card].DeckIndex].Name, SkillBanner.Label.color);
                    break;
                case 4:
                    ShowBanner(NoEnergyText, BattleInspectCardView.ShortCost);
                    break;
                case 5:
                    RaisePreviewCard(swiped: false);
                    break;
                case 6:
                    RaisePreviewCard(swiped: true);
                    break;
                case 7:
                    RaisePreviewCard(swiped: false, withKeywords: true);
                    break;
                case 8:
                    RaisePreviewCard(swiped: false, enemyHpPercent: 12);
                    break;
                case 9:
                    RaisePreviewCard(PreviewCardWith(CardActionKind.Heal), allyHpPercent: 45);
                    break;
                case 10:
                    RaisePreviewCard(PreviewCardWith(CardActionKind.Block));
                    break;
                case 11:
                    ListPreviewStatuses();
                    break;
                case 12:
                    GlintPreviewCard();
                    break;
                case 13:
                    RaisePreviewCard(PreviewPartyCardWithKeywords(), withKeywords: true);
                    break;
                case 14:
                    ListPreviewDeck();
                    break;
            }
        }

        // 手札に限らない最初のカードで、敵に使わず、説明文にキーワードがあるもの（説明札は右に出る）。
        private int PreviewPartyCardWithKeywords()
        {
            for (int i = 0; i < Cards.Length; i++)
                if (
                    !Cards[i].TargetsEnemies
                    && Cards[i].DeckIndex < Deck.Length
                    && KeywordsOf(Cards[i].DeckIndex).Count > 0
                )
                    return i;
            return -1;
        }

        // 初めの手札の1枚を、使えば覚醒するカードとして光らせた1コマ（光り始めの瞬きが収まったあと）。
        private void GlintPreviewCard()
        {
            int index = PreviewCard();
            var glint = index >= 0 ? MakeGlint(index) : null;
            if (glint != null)
                glint.ShowAt(1f);
        }

        // 初めの手札に配られていない山札のカードを、引く順を隠したコスト順で並べた一覧。
        private void ListPreviewDeck()
        {
            if (PileList == null || Settings == null)
                return;
            int dealt = Mathf.Min(Settings.OpeningDraw, Settings.HandLimit, Cards.Length);
            var inHand = new HashSet<int>();
            for (int i = 0; i < dealt; i++)
                inHand.Add(Cards[i].DeckIndex);
            var rest = new List<int>();
            for (int i = 0; i < Deck.Length; i++)
                if (!inHand.Contains(i) && !Deck[i].Generated)
                    rest.Add(i);
            ShowPile(BattlePile.Draw, ByCost(rest));
        }

        // 2体目の敵（狼）に見本の状態を付け、HPを減らして、その一覧を開く。
        private void ListPreviewStatuses()
        {
            int index = Mathf.Min(1, Enemies.Length - 1);
            if (index < 0)
                return;
            var enemy = Enemies[index];
            enemy.Hp = Mathf.Max(1, enemy.MaxHp * 6 / 10);
            enemy.Gauge.SetHp(enemy.Hp, enemy.MaxHp);
            var list = StatusesOf(false, index);
            list.Clear();
            list.Add(new ActiveStatus { Status = CardStatus.Chill, Turns = 2 });
            list.Add(new ActiveStatus { Status = CardStatus.Vulnerable, Turns = 2 });
            list.Add(new ActiveStatus { Status = CardStatus.Poison, Amount = 3 });
            ShowStatusList(false, index);
        }

        // 帯が開ききって止まった1コマ。
        private void StillTurnBanner(string text, bool enemies)
        {
            if (TurnBanner == null)
                return;
            PaintTurnBanner(text, enemies);
            SetTurnBanner(1f, 1f);
        }

        // 初めの手札のうち、初めのエネルギーで出せる、敵1体に使うカード（なければ出せるカード）。
        // withKeywords のときは、文にキーワードがあるカードに限る。
        private int PreviewCard(bool withKeywords = false)
        {
            if (Settings == null)
                return -1;
            int count = Mathf.Min(Settings.OpeningDraw, Settings.HandLimit, Cards.Length);
            int playable = -1;
            for (int i = 0; i < count; i++)
            {
                var card = Cards[i];
                if (card.Cost > MaxEnergyAt(StartTurn) || card.DeckIndex >= Deck.Length)
                    continue;
                if (withKeywords && KeywordsOf(card.DeckIndex).Count == 0)
                    continue;
                if (card.Effect == BattleInspectCardEffect.DamageOne)
                    return i;
                if (playable < 0)
                    playable = i;
            }
            return playable;
        }

        // 予告を見せる、手札に限らない最初のカードで、その種類の効果を持つもの。
        private int PreviewCardWith(CardActionKind kind)
        {
            for (int i = 0; i < Cards.Length; i++)
            {
                var skill =
                    Cards[i].DeckIndex < Deck.Length ? SkillOf(Deck[Cards[i].DeckIndex]) : null;
                if (skill == null)
                    continue;
                foreach (var action in skill.Actions)
                    if (action.Kind == kind)
                        return i;
            }
            return -1;
        }

        private void RaisePreviewCard(
            bool swiped,
            bool withKeywords = false,
            int enemyHpPercent = 100
        ) => RaisePreviewCard(PreviewCard(withKeywords), swiped, withKeywords, enemyHpPercent);

        /// <summary>
        /// Raises a card as a press does: up in the middle with its user stepped forward, its
        /// ring and name, the cursors over its targets and its forecast on their bars; tapped,
        /// the tap area waits for the target; swiped, the aim runs from the card to the first
        /// enemy it can hit. With <paramref name="withKeywords"/>, a card whose text names
        /// keywords, with their notes. The enemies start from the part of their max HP given, and
        /// the allies from it too, or from their HP at the start when none is given.
        /// </summary>
        private void RaisePreviewCard(
            int index,
            bool swiped = false,
            bool withKeywords = false,
            int enemyHpPercent = 100,
            int allyHpPercent = -1
        )
        {
            if (index < 0)
                return;
            // 停止中は Awake が動かず、HPが入っていない（倒れた扱いになる）。
            foreach (var enemy in Enemies)
            {
                enemy.Hp = Mathf.Max(1, enemy.MaxHp * enemyHpPercent / 100);
                enemy.Gauge.SetHp(enemy.Hp, enemy.MaxHp);
            }
            foreach (var ally in Allies)
            {
                ally.Hp =
                    allyHpPercent < 0
                        ? ally.StartHp
                        : Mathf.Max(1, ally.MaxHp * allyHpPercent / 100);
                ally.Gauge.SetHp(ally.Hp, ally.MaxHp);
            }
            var card = Cards[index];
            card.Body.SetAsLastSibling();
            PlaceCard(card.Body, RaisedPose(index), 1f);

            if (card.Caster >= 0 && card.Caster < Allies.Length)
            {
                var ally = Allies[card.Caster];
                var step = StepOffset(Vector2.right);
                ally.Body.anchoredPosition += step;
                ally.HpBar.anchoredPosition += step;
                SetShown(ally.CasterRing, true);
                SetShown(ally.NameTag, true);
            }
            ShowMarkers(card);
            if (TapArea != null)
                TapArea.SetActive(!swiped);
            if (withKeywords && KeywordTips != null)
                ShowKeywordTips(index);

            if (Aim == null)
                return;
            int target = -1;
            if (swiped && card.TargetsEnemies && !card.TargetsWholeSide)
                for (int i = 0; i < Enemies.Length && target < 0; i++)
                    if (CanTarget(card, i))
                        target = i;
            Aim.gameObject.SetActive(target >= 0);
            if (target < 0)
                return;
            var area = Enemies[target].TargetArea;
            Vector2 start = Aim.InverseTransformPoint(
                card.Body.TransformPoint(card.Body.rect.center)
            );
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Aim,
                ScreenPointOf(area, area.rect.center),
                EventCamera(),
                out var end
            );
            LayoutAim(start, end, onTarget: true);
        }
    }
}
