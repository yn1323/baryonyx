using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Baryonyx.App;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Tavern;
using Baryonyx.Training;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class TavernTrainingTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            PartySession.Reset();
            TrainingSession.Reset();
            CardLoadoutSession.Reset();
            UniqueSkillSession.Reset();
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            PartySession.Reset();
            TrainingSession.Reset();
            CardLoadoutSession.Reset();
            UniqueSkillSession.Reset();
            yield return SceneTests.UnloadAll(nameof(TavernTrainingTests));
        }

        // 冒険者の一覧の「装備・スキル・育成」は、その人の個別の画面を開く。左にイラストと右下のドット絵、
        // 右にステータスとパッシブ、デッキの4枚（固有スキル2・カスタムスキル2）、装備5枠。左右のフリックで
        // 人を替え、「レベルアップ」で重ねて開いた画面で上げる数を選んでから上げる。戻る操作は重ねた画面を
        // 先に閉じ、次に一覧へ戻る。
        [UnityTest]
        public IEnumerator TheAdventurersPageShowsThemAndRaisesTheLevel()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            var training = OpenPage(view);
            Assert.That(training.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.ListPanel.activeSelf, Is.False);
            Assert.That(view.GuideArt.activeSelf, Is.False);
            Assert.That(training.Dialog.activeSelf, Is.False);
            // 個別の画面はメニューに出さない。
            int item = ItemOf(view, TrainingSession.GuideItemKey);
            Assert.That(view.Definition.Items[item].Hidden, Is.True);
            Assert.That(view.MenuItems[item], Is.Null);

            // 左のイラストと右の枠は「もどる」より下で、重ならない。ドット絵はイラストの右下に立つ。
            var portrait = training.transform.Find("Layout/Portrait");
            var detail = training.transform.Find("Layout/Detail");
            SceneTests.AssertBelow(portrait, view.Back.transform);
            SceneTests.AssertBelow(detail, view.Back.transform);
            SceneTests.AssertBelow(detail, view.Breadcrumb.transform);
            Assert.That(
                SceneTests.ScreenRect(portrait).xMax,
                Is.LessThanOrEqualTo(SceneTests.ScreenRect(detail).xMin)
            );
            var picture = SceneTests.ScreenRect(portrait);
            var figure = SceneTests.ScreenRect(training.Figure.transform);
            Assert.That(figure.center.x, Is.GreaterThan(picture.center.x));
            Assert.That(figure.center.y, Is.LessThan(picture.center.y));
            Assert.That(training.Illustration.enabled, Is.True);
            Assert.That(training.Illustration.texture, Is.Not.Null);
            // 横に長いボタンは、メニューの行と同じく高さを7割まで許す。
            SceneTests.AssertTouchSize(training.LevelUp.transform, 0.7f);
            foreach (var gear in training.Gear)
                SceneTests.AssertTouchSize(gear.Button.transform);
            foreach (var card in training.CardSlots)
                SceneTests.AssertTouchSize(card.Button.transform);

            // 最初はパーティの1人目。ステータス・スキル・装備・カードは今のレベルの値。
            var party = PartySession.Formation(training.Party);
            var first = party.Find(party.Member(0));
            var growth = training.Data.Find(first.Id);
            Assert.That(training.Name.text, Is.EqualTo(first.Name));
            Assert.That(training.Level.text, Is.EqualTo(first.Level.ToString()));
            // 上の段のパンくずは「編成 › 冒険者 › 名前」、所持ルーンはこの画面で読んだ残高。
            Assert.That(view.Breadcrumb.text, Is.EqualTo(Trail("編成", "冒険者", first.Name)));
            Assert.That(
                view.Runes.text,
                Is.EqualTo(Baryonyx.Economy.RuneBalance.Text(training.Presenter.State.Runes))
            );
            Assert.That(training.Illustration.texture, Is.EqualTo(first.Illustration));
            Assert.That(
                training.Stats.Select(label => label.text),
                Is.EqualTo(Values(TrainingBond.StatsOf(growth, first.Level)))
            );
            Assert.That(training.Passives[0].Name.text, Is.EqualTo(growth.Passives[0].Name));
            Assert.That(training.Passives[0].Description.text, Is.Not.Empty);
            // 2つ目のパッシブは、今のレベルで解放していなければ鍵と解放するレベルを出す。
            bool locked = first.Level < growth.Passives[1].UnlockLevel;
            Assert.That(training.Passives[1].Lock.activeSelf, Is.EqualTo(locked));
            Assert.That(
                training.Passives[1].When.text,
                Is.EqualTo(locked ? $"Lv {growth.Passives[1].UnlockLevel}で解放" : "")
            );
            Assert.That(training.Passives[0].Icon.texture, Is.Not.Null);
            Assert.That(training.Passives[0].Icon.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)));
            // 固有スキルは押せる小さなカードで、セットしている進化の段階を出す。未解放のものは暗くして
            // 解放するレベルを重ねる。効果の文は出さず、カードを押して開く付け替えの画面で読む。
            Assert.That(
                training.Uniques[0].Name.text,
                Is.EqualTo(
                    new CardLoadoutSessionStore(training.Party, training.Data)
                        .UniqueCardOf(first.Id, 0)
                        .Name
                )
            );
            Assert.That(training.Uniques[0].Description, Is.Null);
            // 1つ目はLv1、2つ目はLv10で開き、Lv12の先頭の人は3つ目（Lv30）だけがまだ開いていない。
            Assert.That(training.Uniques[0].Lock.activeSelf, Is.False);
            Assert.That(training.Uniques[1].Lock.activeSelf, Is.False);
            Assert.That(training.Uniques[2].Lock.activeSelf, Is.True);
            Assert.That(
                training.Uniques[2].When.text,
                Is.EqualTo($"Lv {growth.Uniques[2].UnlockLevel}で解放")
            );
            foreach (var card in training.Uniques)
                SceneTests.AssertTouchSize(card.Button.transform);
            // カスタムスキルは3枠で、枠は属性の枠。仮データの人は2枚を付け、3枚目の枠は空き。
            Assert.That(training.CardSlots.Length, Is.EqualTo(TrainingPresenter.CustomSlots));
            Assert.That(training.CardSlots.All(card => card.Name.text != ""), Is.True);
            Assert.That(training.CardSlots.Take(2).All(card => card.Art.enabled), Is.True);
            Assert.That(training.CardSlots.All(card => card.Frame.texture != null), Is.True);
            Assert.That(training.CardSlots[2].Name.text, Is.EqualTo(TrainingPresenter.EmptyName));
            Assert.That(training.CardSlots[2].Art.enabled, Is.False);
            Assert.That(training.Gear[0].Slot.text, Is.EqualTo("武器"));
            Assert.That(training.Gear[0].Name.text, Is.Not.Empty);
            Assert.That(training.Gear[0].Detail.text, Is.Not.Empty);
            Assert.That(training.Gear[0].Icon.enabled, Is.True);
            // アクセサリーの1つ目はLv1から開き、2つ目と3つ目はLvで開くまで錠前と解放するLvを出す。
            // 押すと付け替えの画面を開ける。
            Assert.That(training.Gear[4].Slot.text, Is.EqualTo("アクセサリー 3"));
            Assert.That(training.Gear[2].Lock.activeSelf, Is.False);
            Assert.That(training.Gear.Skip(3).All(gear => gear.Lock.activeSelf), Is.True);
            Assert.That(
                training.Gear.Skip(3).Select(gear => gear.When.text),
                Is.EqualTo(new[] { AccessorySlots.UnlockText(1), AccessorySlots.UnlockText(2) })
            );
            Assert.That(training.Gear.All(gear => gear.Button.interactable), Is.True);

            // 左へのフリックで次の人、右へのフリックで前の人。枠の上から動かしてもフリックとして届く。
            Assert.That(
                ExecuteEvents.GetEventHandler<IEndDragHandler>(
                    training.CardSlots[0].Button.gameObject
                ),
                Is.SameAs(training.Swipe.gameObject)
            );
            Flick(training, -1);
            Assert.That(training.Name.text, Is.EqualTo(party.Name(party.Member(1))));
            Assert.That(
                view.Breadcrumb.text,
                Is.EqualTo(Trail("編成", "冒険者", party.Name(party.Member(1))))
            );
            Flick(training, 1);
            Assert.That(training.Name.text, Is.EqualTo(first.Name));

            // レベルアップを重ねて開く。暗幕が背面の「もどる」を押せなくする。
            training.LevelUp.onClick.Invoke();
            Assert.That(training.Dialog.activeSelf, Is.True);
            // 開いたばかりの暗幕は、1度描かれるまでタップの判定に入らない。
            yield return null;
            foreach (
                var button in new Component[]
                {
                    training.Less,
                    training.More,
                    training.Max,
                    training.Cancel,
                    training.Confirm,
                }
            )
                SceneTests.AssertTouchSize(button.transform, 0.7f);
            // 上の段は、左にLv、右に「－」「＋」「最大」を同じ高さで並べる。
            var to = SceneTests.ScreenRect(training.To.transform);
            var less = SceneTests.ScreenRect(training.Less.transform);
            Assert.That(to.xMax, Is.LessThan(less.xMin));
            Assert.That(to.yMin, Is.LessThan(less.yMax));
            Assert.That(to.yMax, Is.GreaterThan(less.yMin));
            Assert.That(training.To.fontSize, Is.EqualTo(training.From.fontSize));
            // 使うルーンは「レベルアップ」の中に添える。
            Assert.That(training.Cost.transform.IsChildOf(training.Confirm.transform), Is.True);
            Assert.That(TopHit(view.Back.transform).name, Is.EqualTo("Dim"));
            // 端末の戻るキーと同じ経路（「もどる」）は、重ねた画面だけを閉じる。
            view.Back.onClick.Invoke();
            Assert.That(training.Dialog.activeSelf, Is.False);
            Assert.That(training.gameObject.activeSelf, Is.True);

            training.LevelUp.onClick.Invoke();
            training.More.onClick.Invoke();
            training.More.onClick.Invoke();
            int target = first.Level + 3;
            Assert.That(training.To.text, Is.EqualTo(target.ToString()));
            Assert.That(
                training.Diffs[0].After.text,
                Is.EqualTo(growth.StatsAt(target, TrainingBond.StageOf(growth)).Hp.ToString())
            );
            long cost = TrainingRules.CostBetween(first.Level, target, training.Data.CostPerLevel);
            Assert.That(
                training.Cost.text,
                Is.EqualTo(cost.ToString("#,0", CultureInfo.InvariantCulture))
            );
            training.Confirm.onClick.Invoke();
            Assert.That(training.Dialog.activeSelf, Is.False);
            Assert.That(PartySession.LevelOf(training.Party, first.Id), Is.EqualTo(target));
            Assert.That(training.Level.text, Is.EqualTo(target.ToString()));
            Assert.That(view.ToastMessage, Does.StartWith($"{first.Name}が Lv {target} になり"));

            // カスタムスキルの枠は、その人とその枠を選んだスキルの付け替えの画面へ移り、戻ると個別の画面に帰る。
            Flick(training, -1);
            string second = party.Member(1);
            training.CardSlots[1].Button.onClick.Invoke();
            var cards = view.PanelFor(ItemOf(view, CardLoadoutSession.GuideItemKey));
            Assert.That(cards.activeInHierarchy, Is.True);
            Assert.That(training.gameObject.activeSelf, Is.False);
            Assert.That(CardLoadoutSession.Selected, Is.EqualTo(second));
            Assert.That(
                cards
                    .GetComponent<CardLoadoutView>()
                    .Tiles[CardLoadoutPresenter.UniqueCount + 1]
                    .Selected.activeSelf,
                Is.True
            );
            view.Back.onClick.Invoke();
            Assert.That(cards.activeSelf, Is.False);
            Assert.That(training.gameObject.activeSelf, Is.True);
            Assert.That(training.Name.text, Is.EqualTo(party.Name(second)));

            // 固有スキルは、その固有スキルの進化ツリーを出したスキルの付け替えの画面へ移る。
            training.Uniques[1].Button.onClick.Invoke();
            var loadout = cards.GetComponent<CardLoadoutView>();
            Assert.That(cards.activeInHierarchy, Is.True);
            Assert.That(loadout.Tiles[1].Selected.activeSelf, Is.True);
            Assert.That(loadout.Tiles.Count(tile => tile.Selected.activeSelf), Is.EqualTo(1));
            Assert.That(loadout.Tree.Root.activeSelf, Is.True);
            view.Back.onClick.Invoke();
            Assert.That(training.gameObject.activeSelf, Is.True);

            // 「もどる」で冒険者の一覧へ戻る。パーティの先頭を長押しして開き直すと、アプリを動かしている間はレベルが残る。
            view.Back.onClick.Invoke();
            var roster = view.PanelFor(ItemOf(view, PartySession.GuideItemKey))
                .GetComponent<AdventurerRosterView>();
            Assert.That(roster.gameObject.activeSelf, Is.True);
            Assert.That(training.gameObject.activeSelf, Is.False);
            roster.Slots[0].Card.Hold();
            Assert.That(training.Name.text, Is.EqualTo(first.Name));
            Assert.That(training.Level.text, Is.EqualTo(target.ToString()));
            yield return null;
        }

        // サーバーがあるときは、サーバーのLvと所持ルーンを読み、サーバーでレベルを上げてルーンを使う。
        // 別の端末で上げていて断られたときは、読み直してから失敗を知らせる。
        [UnityTest]
        public IEnumerator TrainingRaisesTheLevelOnTheServer()
        {
            var server = new FakePartySource(5000, "toma", "luka")
                .With("toma", 12, "Fire", "Ice")
                .With("luka", 11, "VitalThrust", "Thunder");
            PartySession.Source = server;
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            var roster = view.PanelFor(ItemOf(view, PartySession.GuideItemKey))
                .GetComponent<AdventurerRosterView>();
            view.MenuItems[ItemOf(view, PartySession.GuideItemKey)].onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => roster.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            roster.Slots[0].Card.Hold();
            var training = view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
            Assert.That(training.Name.text, Is.EqualTo(TrainingView.LoadingText));
            Assert.That(training.LevelUp.interactable, Is.False);
            yield return SceneTests.WaitUntil(
                () => training.LoadTask.IsCompleted,
                message: "The party did not load."
            );

            var data = training.Party;
            Assert.That(training.Name.text, Is.EqualTo(data.Find("toma").Name));
            Assert.That(training.Level.text, Is.EqualTo("12"));
            Assert.That(training.LevelUpCostLabel.text, Is.EqualTo("1,200"));

            // Lv 12 → 14 は 1,200 + 1,300 = 2,500ルーン。保存を待たずに重ねた画面を閉じ、上げたLvを出す。
            training.LevelUp.onClick.Invoke();
            training.More.onClick.Invoke();
            training.Confirm.onClick.Invoke();
            Assert.That(training.Presenter.ConfirmTask.IsCompleted, Is.False);
            Assert.That(training.Dialog.activeSelf, Is.False);
            Assert.That(training.Level.text, Is.EqualTo("14"));
            yield return SceneTests.WaitUntil(() => training.Presenter.ConfirmTask.IsCompleted);
            Assert.That(server.Calls, Is.EqualTo(new[] { "level toma 12 14" }));
            Assert.That(server.LevelOf("toma"), Is.EqualTo(14));
            Assert.That(training.Dialog.activeSelf, Is.False);
            Assert.That(training.Level.text, Is.EqualTo("14"));
            training.LevelUp.onClick.Invoke();
            Assert.That(training.Presenter.State.Runes, Is.EqualTo(2500));
            training.Cancel.onClick.Invoke();
            Assert.That(view.ToastMessage, Does.StartWith($"{data.Find("toma").Name}が Lv 14"));

            // 別の端末でLv 16まで上げていると断られ、通知の帯で知らせて、読み直したLvを出す。
            server.SetLevel("toma", 16);
            training.LevelUp.onClick.Invoke();
            training.Confirm.onClick.Invoke();
            Assert.That(training.Level.text, Is.EqualTo("15"));
            yield return SceneTests.WaitUntil(() => training.Presenter.ConfirmTask.IsCompleted);
            Assert.That(server.LevelOf("toma"), Is.EqualTo(16));
            Assert.That(server.Runes, Is.EqualTo(2500));
            Assert.That(training.Level.text, Is.EqualTo("16"));
            Assert.That(view.ToastMessage, Is.EqualTo(TrainingPresenter.LevelUpFailedMessage));
        }

        // 右上の「絆」は、灯った段を6つのしるしで見せ、押すと次の段で上がるステータスを重ねた窓に出す。
        // 「絆を深める」で絆のしるしを使って1段上げ、灯った段の演出を出す。戻る操作は演出を先に閉じる。
        [UnityTest]
        public IEnumerator TheBondDeepensFromThePageAndLightsUp()
        {
            TrainingBond.Reset();
            try
            {
                var guide = default(GuideSceneBootstrap);
                yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
                var view = guide.View;
                var training = OpenPage(view);
                var party = PartySession.Formation(training.Party);
                var first = party.Find(party.Member(0));
                var growth = training.Data.Find(first.Id);
                int stage = TrainingBond.StageOf(growth);
                Assert.That(stage, Is.LessThan(TrainingBond.Max - 1));
                Assert.That(TrainingBond.TokensOf(growth), Is.EqualTo(1));

                // 「絆」は名前の行と「レベルアップ」の間に並び、重ならない。
                SceneTests.AssertTouchSize(training.Bond.transform, 0.7f);
                var bond = SceneTests.ScreenRect(training.Bond.transform);
                Assert.That(
                    bond.xMax,
                    Is.LessThanOrEqualTo(SceneTests.ScreenRect(training.LevelUp.transform).xMin)
                );
                foreach (var icon in training.Elements.Where(icon => icon.gameObject.activeSelf))
                    Assert.That(
                        SceneTests.ScreenRect(icon.transform).xMax,
                        Is.LessThanOrEqualTo(bond.xMin)
                    );
                Assert.That(Lit(training, training.BondPips), Is.EqualTo(stage));

                training.Bond.onClick.Invoke();
                Assert.That(training.BondDialog.activeSelf, Is.True);
                Assert.That(training.BondFrom.text, Is.EqualTo(stage.ToString()));
                Assert.That(training.BondTo.text, Is.EqualTo((stage + 1).ToString()));
                Assert.That(Lit(training, training.BondStones), Is.EqualTo(stage));
                Assert.That(
                    training.BondDiffs[0].After.text,
                    Is.EqualTo(growth.StatsAt(first.Level, stage + 1).Hp.ToString())
                );
                Assert.That(training.BondTokens.text, Is.EqualTo("1 / 1"));
                Assert.That(training.BondConfirm.interactable, Is.True);
                // 重ねた窓が配置されてから、「絆を深める」が背面より手前で押せることを確かめる。
                yield return null;
                Assert.That(
                    TopHit(training.BondConfirm.transform)
                        .transform.IsChildOf(training.BondConfirm.transform),
                    Is.True
                );

                training.BondConfirm.onClick.Invoke();
                Assert.That(training.BondDialog.activeSelf, Is.False);
                Assert.That(training.Celebration.gameObject.activeSelf, Is.True);
                Assert.That(TrainingBond.StageOf(growth), Is.EqualTo(stage + 1));
                Assert.That(
                    training.Stats.Select(label => label.text),
                    Is.EqualTo(Values(growth.StatsAt(first.Level, stage + 1)))
                );
                Assert.That(Lit(training, training.BondPips), Is.EqualTo(stage + 1));
                var celebration = training.Celebration;
                Assert.That(celebration.Name.text, Is.EqualTo(first.Name));
                Assert.That(celebration.Stage.text, Is.EqualTo((stage + 1).ToString()));
                Assert.That(
                    celebration.Effect.text,
                    Is.EqualTo($"全ステータス +{TrainingBond.PercentAt(stage + 1)}%")
                );
                yield return SceneTests.WaitUntil(
                    () => celebration.Card.alpha >= 1f,
                    message: "The bond's moment did not finish."
                );
                Assert.That(celebration.Stones[stage].sprite, Is.EqualTo(training.BondLit));
                Assert.That(celebration.Stones[stage + 1].sprite, Is.EqualTo(training.BondDark));

                // 戻る操作は演出だけを閉じる。しるしを使い切ると、深められない。
                view.Back.onClick.Invoke();
                Assert.That(celebration.gameObject.activeSelf, Is.False);
                Assert.That(training.gameObject.activeSelf, Is.True);
                training.Bond.onClick.Invoke();
                Assert.That(training.BondTokens.text, Is.EqualTo("0 / 1"));
                Assert.That(training.BondConfirm.interactable, Is.False);
                view.Back.onClick.Invoke();
                Assert.That(training.BondDialog.activeSelf, Is.False);
                Assert.That(training.gameObject.activeSelf, Is.True);
            }
            finally
            {
                TrainingBond.Reset();
            }
        }

        // サーバーがあるときは、サーバーの絆としるしを読み、保存を待たずに演出を出してサーバーで深める。
        // 保存できなければ、演出を閉じて元の段に戻し、通知の帯で知らせる。
        [UnityTest]
        public IEnumerator TrainingDeepensTheBondOnTheServer()
        {
            var server = new FakePartySource(5000, "toma", "luka")
                .With("toma", 12, "Fire", "Ice")
                .With("luka", 11, "VitalThrust", "Thunder")
                .WithBond("toma", 2, 2);
            PartySession.Source = server;
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            var roster = view.PanelFor(ItemOf(view, PartySession.GuideItemKey))
                .GetComponent<AdventurerRosterView>();
            view.MenuItems[ItemOf(view, PartySession.GuideItemKey)].onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => roster.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            roster.Slots[0].Card.Hold();
            var training = view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
            yield return SceneTests.WaitUntil(
                () => training.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            Assert.That(Lit(training, training.BondPips), Is.EqualTo(2));

            training.Bond.onClick.Invoke();
            Assert.That(training.BondTokens.text, Is.EqualTo("2 / 1"));
            training.BondConfirm.onClick.Invoke();
            Assert.That(training.Celebration.gameObject.activeSelf, Is.True);
            Assert.That(training.Celebration.Stage.text, Is.EqualTo("3"));
            Assert.That(training.Presenter.DeepenTask.IsCompleted, Is.False);
            yield return SceneTests.WaitUntil(() => training.Presenter.DeepenTask.IsCompleted);
            Assert.That(server.Calls, Is.EqualTo(new[] { "bond toma 3" }));
            Assert.That(server.BondOf("toma"), Is.EqualTo((3, 1)));
            Assert.That(training.Celebration.gameObject.activeSelf, Is.True);
            Assert.That(Lit(training, training.BondPips), Is.EqualTo(3));
            view.Back.onClick.Invoke();

            // 保存できなければ、演出を閉じて段としるしを戻す。
            server.Fail = true;
            training.Bond.onClick.Invoke();
            training.BondConfirm.onClick.Invoke();
            Assert.That(training.Celebration.gameObject.activeSelf, Is.True);
            yield return SceneTests.WaitUntil(() => training.Presenter.DeepenTask.IsCompleted);
            Assert.That(training.Celebration.gameObject.activeSelf, Is.False);
            Assert.That(Lit(training, training.BondPips), Is.EqualTo(3));
            Assert.That(view.ToastMessage, Is.EqualTo(TrainingPresenter.DeepenFailedMessage));
        }

        private static int Lit(TrainingView training, IEnumerable<UnityEngine.UI.Image> stones) =>
            stones.Count(stone => stone.sprite == training.BondLit);

        private static int ItemOf(Baryonyx.UI.GuideMenu.GuideMenuView view, string key) =>
            System.Array.FindIndex(view.Definition.Items, entry => entry.Key == key);

        // メニューの「冒険者」から一覧を開き、選んでいる人の個別の画面を開く。
        private static string Trail(params string[] parts) =>
            string.Join(Baryonyx.UI.GuideMenu.GuideMenuView.TrailSeparator, parts);

        private static TrainingView OpenPage(Baryonyx.UI.GuideMenu.GuideMenuView view)
        {
            int item = ItemOf(view, PartySession.GuideItemKey);
            view.MenuItems[item].onClick.Invoke();
            view.PanelFor(item).GetComponent<AdventurerRosterView>().Slots[0].Card.Hold();
            return view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
        }

        // 指を横へ400（設計座標）動かして離したことにする。-1は左へ（次の人）、1は右へ（前の人）。
        private static void Flick(TrainingView training, int direction)
        {
            var canvas = training.Swipe.GetComponentInParent<Canvas>().rootCanvas;
            var start = new Vector2(Screen.width / 2f, Screen.height / 2f);
            training.Swipe.OnEndDrag(
                new PointerEventData(EventSystem.current)
                {
                    pressPosition = start,
                    position = start + new Vector2(direction * 400f * canvas.scaleFactor, 0f),
                }
            );
        }

        private static IEnumerable<string> Values(CharacterStats stats) =>
            Enumerable.Range(0, CharacterStats.Count).Select(i => stats[i].ToString());

        // 画面のその位置を押したとき、いちばん手前で受け取るもの。
        private static GameObject TopHit(Transform target)
        {
            var hits = new List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = SceneTests.ScreenRect(target).center,
            };
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            return hits[0].gameObject;
        }
    }
}
