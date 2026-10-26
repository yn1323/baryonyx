using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Party;
using Baryonyx.Tavern;
using Baryonyx.Training;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class TavernFormationTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            PartySession.Reset();
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            PartySession.Reset();
            yield return SceneTests.UnloadAll(nameof(TavernFormationTests));
        }

        // 編成の「冒険者」は、案内人の画面の枠の中に冒険者の一覧を開く。左にパーティの4枚、右にパーティにいない
        // 人と最後に「パーティから外す」、下に選んだ人の帯。パーティの枠と一覧の人を順に押すと入れ替え、
        // 選んだ人は帯の「装備・スキル」で冒険者のタブへ移る。
        [UnityTest]
        public IEnumerator AdventurersSwapThePartyAndOpenTheTabsFromTheBand()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            Assert.That(view.Definition.Title, Is.EqualTo("編成"));
            int item = ItemOf(view, PartySession.GuideItemKey);
            Assert.That(item, Is.GreaterThanOrEqualTo(0));
            Assert.That(view.MenuItems[item].transform.Find("Body/Content/Icon"), Is.Not.Null);
            Assert.That(view.Framed, Is.False);
            view.MenuItems[item].onClick.Invoke();
            var roster = view.PanelFor(item).GetComponent<AdventurerRosterView>();
            Assert.That(roster.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.ListPanel.activeSelf, Is.False);
            Assert.That(view.GuideArt.activeSelf, Is.False);
            // 画面いっぱいの枠：ぼかした背景・ヘッダーの帯と区切り・四隅の飾りを出す。
            Assert.That(view.Framed, Is.True);
            Assert.That(view.FrameParts.All(part => part.activeSelf), Is.True);
            yield return null;

            // カードは「もどる」と重ならず、パーティの4枚は一覧の左にある。どれも指で押せる大きさ。
            var data = roster.Party;
            var party = PartySession.Formation(data);
            foreach (var slot in roster.Slots)
            {
                SceneTests.AssertBelow(slot.Card.transform, view.Back.transform);
                SceneTests.AssertTouchSize(slot.Card.transform);
                // カードは帯と重ならない。
                SceneTests.AssertBelow(roster.Chosen.Detail.transform, slot.Card.transform);
            }
            // 横に長いボタンは、メニューの行と同じく高さを7割まで許す。
            SceneTests.AssertTouchSize(roster.Chosen.Detail.transform, 0.7f);
            Assert.That(
                SceneTests.ScreenRect(roster.Slots[0].Card.transform).xMax,
                Is.LessThanOrEqualTo(SceneTests.ScreenRect(roster.List.transform).xMin)
            );
            // 一覧はパーティにいない人を持っている順に並べ、最後に「パーティから外す」を置く。
            Assert.That(Shown(roster), Is.EqualTo(party.Bench.Select(member => member.Id)));
            Assert.That(roster.Remove.transform.GetSiblingIndex(), Is.EqualTo(LastSibling(roster)));
            foreach (var id in Shown(roster))
                SceneTests.AssertTouchSize(Tile(roster, id).Card.transform);
            SceneTests.AssertTouchSize(roster.Remove.transform);

            // 最初は何も選んでいない。帯は空いていて、「装備・スキル」は押せない。
            // パーティの名前とLvはパーティの順に出す。
            var first = party.Find(party.Member(0));
            Assert.That(roster.Slots.Any(slot => slot.Card.Selected), Is.False);
            Assert.That(roster.Chosen.Name.text, Is.Empty);
            Assert.That(roster.Chosen.Detail.interactable, Is.False);
            Assert.That(roster.Slots[0].Name.text, Is.EqualTo(first.Name));
            Assert.That(roster.Slots[0].Level.text, Is.EqualTo(first.Level.ToString()));
            Assert.That(roster.Slots[0].Figure.gameObject.activeSelf, Is.True);

            // パーティの枠を選んでから一覧の人を押すと入れ替え、何も選んでいない状態に戻る。
            string bench = Shown(roster).First();
            string before = party.Member(1);
            roster.Slots[1].Card.Click();
            Assert.That(roster.Slots[1].Card.Selected, Is.True);
            Assert.That(roster.Chosen.Name.text, Is.EqualTo(party.Name(before)));
            Assert.That(roster.Chosen.Detail.interactable, Is.True);
            Tile(roster, bench).Card.Click();
            Assert.That(party.Member(1), Is.EqualTo(bench));
            Assert.That(roster.Slots[1].Name.text, Is.EqualTo(party.Name(bench)));
            Assert.That(roster.Slots[1].Card.Selected, Is.False);
            Assert.That(Shown(roster), Does.Contain(before));
            // 変わったことは枠を見ればわかるので、通知の帯は出さない。
            Assert.That(roster.LastNotice, Is.Empty);

            // 枠を選んで「パーティから外す」と空きになり、一覧の人を選んでから空いた枠を押すと入れる。
            roster.Slots[1].Card.Click();
            roster.Remove.Click();
            Assert.That(party.Member(1), Is.Null);
            Assert.That(roster.Slots[1].Empty.gameObject.activeSelf, Is.True);
            Assert.That(roster.Slots[1].Figure.gameObject.activeSelf, Is.False);
            // 空いた枠を選んでも、帯には誰も出さない。
            roster.Slots[1].Card.Click();
            Assert.That(roster.Chosen.Detail.interactable, Is.False);
            roster.Slots[1].Card.Click();
            Tile(roster, before).Card.Click();
            Assert.That(Tile(roster, before).Card.Selected, Is.True);
            roster.Slots[1].Card.Click();
            Assert.That(party.Member(1), Is.EqualTo(before));
            Assert.That(roster.LastNotice, Is.Empty);

            // 長押しでは開かない。選んだ人の帯の「装備・スキル」で、その人のステータスのタブを開く。
            // 「もどる」で一覧へ、もう一度でメニューへ戻り、枠を消す。
            roster.Slots[1].Card.Hold();
            Assert.That(roster.gameObject.activeSelf, Is.True);
            roster.Slots[1].Card.Click();
            roster.Chosen.Detail.onClick.Invoke();
            var training = view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
            Assert.That(training.gameObject.activeInHierarchy, Is.True);
            Assert.That(roster.gameObject.activeSelf, Is.False);
            Assert.That(training.Name.text, Is.EqualTo(party.Name(before)));
            view.Back.onClick.Invoke();
            Assert.That(roster.gameObject.activeSelf, Is.True);
            Assert.That(view.Framed, Is.True);
            // 一覧の人も、選んで帯から開く。
            Tile(roster, bench).Card.Click();
            roster.Chosen.Detail.onClick.Invoke();
            Assert.That(training.gameObject.activeInHierarchy, Is.True);
            Assert.That(training.Name.text, Is.EqualTo(party.Name(bench)));
            view.Back.onClick.Invoke();
            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            Assert.That(view.GuideArt.activeSelf, Is.True);
            Assert.That(view.Framed, Is.False);
            Assert.That(view.FrameParts.Any(part => part.activeSelf), Is.False);
        }

        // サーバーがあるときは、開くたびにサーバーの編成を読み、入れ替えを保存してから表示を変える。
        // 読み込むまでは仮データの編成を見せず、保存・読み込みに失敗したら通知の帯で知らせる。
        [UnityTest]
        public IEnumerator AdventurersReadAndSaveThePartyOnTheServer()
        {
            var server = new FakePartySource(0, "anselm", "toma")
                .With("toma", 20, "Fire", "Ice")
                .With("luka", 11, "VitalThrust", "Thunder")
                .With("anselm", 8, "EarthSplitter", "Fire")
                .With("greta", 7, "VitalThrust", "Ice");
            PartySession.Source = server;
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            int item = ItemOf(view, PartySession.GuideItemKey);
            view.MenuItems[item].onClick.Invoke();
            var roster = view.PanelFor(item).GetComponent<AdventurerRosterView>();
            // 読み込む間は、文字の代わりにパーティの枠と一覧の先頭をカードの裏で埋める。
            Assert.That(roster.Status.text, Is.Empty);
            Assert.That(roster.Slots.All(slot => FaceDown(slot.Card)), Is.True);
            Assert.That(Shown(roster), Has.Length.EqualTo(AdventurerRosterView.CoverTiles));
            Assert.That(
                roster
                    .Tiles.Where(tile => tile.Card.gameObject.activeSelf)
                    .All(tile => FaceDown(tile.Card)),
                Is.True
            );
            Assert.That(roster.Remove.gameObject.activeSelf, Is.False);
            Assert.That(roster.Slots[0].Name.text, Is.Empty);
            yield return SceneTests.WaitUntil(
                () => roster.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            // 読み終えたら、伏せたカードをめくって表にする。人のいない場所のカードは消えていく。
            yield return SceneTests.WaitUntil(
                () =>
                    roster.Slots.All(slot => FaceUp(slot.Card))
                    && roster.Tiles.All(tile => FaceUp(tile.Card)),
                message: "The cards did not turn face up."
            );

            // サーバーの持っているキャラ・編成・Lvを出す。
            var data = roster.Party;
            Assert.That(roster.Status.text, Is.Empty);
            Assert.That(roster.Slots[0].Name.text, Is.EqualTo(data.Find("anselm").Name));
            Assert.That(roster.Slots[1].Level.text, Is.EqualTo("20"));
            Assert.That(roster.Slots[2].Empty.gameObject.activeSelf, Is.True);
            Assert.That(Shown(roster), Is.EqualTo(new[] { "luka", "greta" }));

            // 空いた枠を選んでからルカを押すと、すぐに枠に出し、そのあとで保存する。
            roster.Slots[2].Card.Click();
            Tile(roster, "luka").Card.Click();
            Assert.That(roster.Presenter.ChangeTask.IsCompleted, Is.False);
            Assert.That(roster.Slots[2].Name.text, Is.EqualTo(data.Find("luka").Name));
            yield return SceneTests.WaitUntil(() => roster.Presenter.ChangeTask.IsCompleted);
            Assert.That(server.Calls, Is.EqualTo(new[] { "slot 2 luka" }));
            Assert.That(roster.Slots[2].Name.text, Is.EqualTo(data.Find("luka").Name));
            Assert.That(roster.LastNotice, Is.Empty);
            Assert.That(view.ToastMessage, Is.Empty);

            // 保存できなかったときは、出した枠を戻して通知の帯で知らせる。
            server.Fail = true;
            roster.Slots[2].Card.Click();
            Tile(roster, "greta").Card.Click();
            Assert.That(roster.Slots[2].Name.text, Is.EqualTo(data.Find("greta").Name));
            yield return SceneTests.WaitUntil(() => roster.Presenter.ChangeTask.IsCompleted);
            Assert.That(roster.Slots[2].Name.text, Is.EqualTo(data.Find("luka").Name));
            Assert.That(view.ToastMessage, Is.EqualTo(PartyRosterPresenter.SaveFailedMessage));

            // 開き直すと、前に読んだ編成をすぐ出して読み直す。読めなかったときは、その編成を残して知らせる。
            view.Back.onClick.Invoke();
            view.MenuItems[item].onClick.Invoke();
            Assert.That(roster.Slots[2].Name.text, Is.EqualTo(data.Find("luka").Name));
            yield return SceneTests.WaitUntil(() => roster.LoadTask.IsCompleted);
            Assert.That(roster.Status.text, Is.Not.EqualTo(AdventurerRosterView.LoadFailedText));
            Assert.That(roster.LastNotice, Is.EqualTo(AdventurerRosterView.LoadFailedMessage));
            Assert.That(roster.Slots[2].Name.text, Is.EqualTo(data.Find("luka").Name));

            server.Fail = false;
            view.Back.onClick.Invoke();
            view.MenuItems[item].onClick.Invoke();
            yield return SceneTests.WaitUntil(() => roster.LoadTask.IsCompleted);
            Assert.That(server.Loads, Is.EqualTo(2));
            Assert.That(roster.Slots[2].Name.text, Is.EqualTo(data.Find("luka").Name));
            // 開き直したときは何も選んでいない。
            Assert.That(roster.Slots.Any(slot => slot.Card.Selected), Is.False);
        }

        private static int ItemOf(GuideMenuView view, string key) =>
            System.Array.FindIndex(view.Definition.Items, entry => entry.Key == key);

        private static string[] Shown(AdventurerRosterView roster) =>
            roster
                .Tiles.Where(tile => tile.Card.gameObject.activeSelf)
                .OrderBy(tile => tile.Card.transform.GetSiblingIndex())
                .Select(tile => tile.Id)
                .ToArray();

        private static bool FaceDown(GlassCard card) =>
            card.GetComponent<GlassCardBack>() is { FaceDown: true };

        private static bool FaceUp(GlassCard card) =>
            card.GetComponent<GlassCardBack>() is not { } back
            || (!back.FaceDown && !back.Flipping && !back.Leaving);

        private static int LastSibling(AdventurerRosterView roster) =>
            roster.Remove.transform.parent.childCount - 1;

        private static AdventurerTileWidget Tile(AdventurerRosterView roster, string id) =>
            roster.Tiles.Single(tile => tile.Id == id);
    }
}
