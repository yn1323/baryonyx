using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Party;
using Baryonyx.Tavern;
using Baryonyx.Training;
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

        // 編成の「冒険者」は一覧の代わりに冒険者の一覧を開く。右はパーティの4枠、その下に控え。押した人の
        // 見本を左に出し、「装備・スキル・育成」でその人の個別の画面へ移る。下のボタンで外す・入れる・入れ替える。
        [UnityTest]
        public IEnumerator AdventurersShowTheChosenAndChangeTheParty()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            Assert.That(view.Definition.Title, Is.EqualTo("編成"));
            int item = ItemOf(view, PartySession.GuideItemKey);
            Assert.That(item, Is.GreaterThanOrEqualTo(0));
            Assert.That(view.MenuItems[item].transform.Find("Icon"), Is.Not.Null);
            view.MenuItems[item].onClick.Invoke();
            var roster = view.PanelFor(item).GetComponent<AdventurerRosterView>();
            Assert.That(roster.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.ListPanel.activeSelf, Is.False);
            Assert.That(view.GuideArt.activeSelf, Is.False);
            yield return null;

            // 左の見本と右の一覧は「もどる」と重ならない。ボタンとタイルは指で押せる大きさ。
            var chosen = roster.transform.Find("Layout/Chosen");
            var list = roster.transform.Find("Layout/Roster");
            SceneTests.AssertBelow(chosen, view.Back.transform);
            SceneTests.AssertBelow(list, view.Back.transform);
            Assert.That(
                SceneTests.ScreenRect(chosen).xMax,
                Is.LessThanOrEqualTo(SceneTests.ScreenRect(list).xMin)
            );
            SceneTests.AssertTouchSize(roster.Open.transform, 0.7f);
            SceneTests.AssertTouchSize(roster.Action.transform, 0.7f);
            foreach (var slot in roster.Slots)
                SceneTests.AssertTouchSize(slot.Button.transform);

            // 最初はパーティの先頭を選び、見本にその人を出す。
            var data = roster.Party;
            var party = PartySession.Formation(data);
            var first = party.Find(party.Member(0));
            Assert.That(roster.Slots[0].Selected.activeSelf, Is.True);
            Assert.That(roster.Name.text, Is.EqualTo(first.Name));
            Assert.That(roster.Level.text, Is.EqualTo(first.Level.ToString()));
            Assert.That(roster.Figure.enabled, Is.True);
            Assert.That(roster.Stats.All(stat => stat.text != ""), Is.True);
            Assert.That(roster.Cards.All(card => card.enabled), Is.True);
            Assert.That(roster.ActionLabel.text, Is.EqualTo(PartyRosterPresenter.LeaveLabel));
            // 控えは持っている順に、パーティにいない人だけを並べる。
            Assert.That(Shown(roster), Is.EqualTo(party.Bench.Select(member => member.Id)));
            foreach (var id in Shown(roster))
                SceneTests.AssertTouchSize(Tile(roster, id).Button.transform);

            // 控えの人を選ぶと見本が替わり、ボタンは「パーティに入れる」。
            string bench = Shown(roster).First();
            Tile(roster, bench).Button.onClick.Invoke();
            Assert.That(roster.Name.text, Is.EqualTo(party.Name(bench)));
            Assert.That(Tile(roster, bench).Selected.activeSelf, Is.True);
            Assert.That(roster.Slots[0].Selected.activeSelf, Is.False);
            Assert.That(roster.ActionLabel.text, Is.EqualTo(PartyRosterPresenter.JoinLabel));

            // パーティに空きがないので、押すとパーティの4枠が入れ替える相手になる。
            roster.Action.onClick.Invoke();
            Assert.That(roster.Slots.All(slot => slot.Swap.activeSelf), Is.True);
            Assert.That(roster.ActionLabel.text, Is.EqualTo(PartyRosterPresenter.CancelLabel));
            Assert.That(roster.Open.interactable, Is.False);
            string before = party.Member(1);
            roster.Slots[1].Button.onClick.Invoke();
            Assert.That(party.Member(1), Is.EqualTo(bench));
            Assert.That(roster.Slots.Any(slot => slot.Swap.activeSelf), Is.False);
            Assert.That(roster.Slots[1].Name.text, Is.EqualTo(party.Name(bench)));
            Assert.That(Shown(roster), Does.Contain(before));
            Assert.That(
                roster.LastNotice,
                Is.EqualTo($"{party.Name(before)}と{party.Name(bench)}を入れ替えました")
            );
            Assert.That(view.ToastMessage, Is.EqualTo(roster.LastNotice));

            // パーティの人を外すと空きになり、空いた枠を押すと選んだ控えを入れる。
            roster.Action.onClick.Invoke();
            Assert.That(party.Member(1), Is.Null);
            Assert.That(roster.Slots[1].Empty.gameObject.activeSelf, Is.True);
            Assert.That(roster.Slots[1].Figure.gameObject.activeSelf, Is.False);
            Assert.That(roster.LastNotice, Does.EndWith("を外しました"));
            Tile(roster, before).Button.onClick.Invoke();
            roster.Slots[1].Button.onClick.Invoke();
            Assert.That(party.Member(1), Is.EqualTo(before));
            Assert.That(roster.LastNotice, Does.EndWith("を編成しました"));

            // 「装備・スキル・育成」は、選んだ人の個別の画面を開く。「もどる」で一覧へ、もう一度でメニューへ。
            roster.Open.onClick.Invoke();
            var training = view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
            Assert.That(training.gameObject.activeInHierarchy, Is.True);
            Assert.That(roster.gameObject.activeSelf, Is.False);
            Assert.That(training.Name.text, Is.EqualTo(party.Name(before)));
            view.Back.onClick.Invoke();
            Assert.That(roster.gameObject.activeSelf, Is.True);
            Assert.That(roster.Name.text, Is.EqualTo(party.Name(before)));
            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            Assert.That(view.GuideArt.activeSelf, Is.True);
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
            Assert.That(roster.Owned.text, Is.EqualTo(AdventurerRosterView.LoadingText));
            Assert.That(Shown(roster), Is.Empty);
            Assert.That(roster.Slots[0].Name.text, Is.Empty);
            yield return SceneTests.WaitUntil(
                () => roster.LoadTask.IsCompleted,
                message: "The party did not load."
            );

            // サーバーの持っているキャラ・編成・Lvを出す。
            var data = roster.Party;
            Assert.That(roster.Owned.text, Is.EqualTo("所持 4人"));
            Assert.That(roster.Slots[0].Name.text, Is.EqualTo(data.Find("anselm").Name));
            Assert.That(roster.Slots[1].Level.text, Is.EqualTo("Lv 20"));
            Assert.That(roster.Slots[2].Empty.gameObject.activeSelf, Is.True);
            Assert.That(Shown(roster), Is.EqualTo(new[] { "luka", "greta" }));

            // 空きがあるので、ルカを選んで「パーティに入れる」と空いた枠へ入れる。保存してから枠に出す。
            Tile(roster, "luka").Button.onClick.Invoke();
            roster.Action.onClick.Invoke();
            Assert.That(roster.Presenter.Saving, Is.True);
            Assert.That(roster.Slots[2].Empty.gameObject.activeSelf, Is.True);
            yield return SceneTests.WaitUntil(() => roster.Presenter.ChangeTask.IsCompleted);
            Assert.That(server.Calls, Is.EqualTo(new[] { "slot 2 luka" }));
            Assert.That(roster.Slots[2].Name.text, Is.EqualTo(data.Find("luka").Name));
            Assert.That(roster.LastNotice, Does.EndWith("を編成しました"));

            // 保存できなかったときは枠を変えずに知らせる。
            server.Fail = true;
            roster.Action.onClick.Invoke();
            yield return SceneTests.WaitUntil(() => roster.Presenter.ChangeTask.IsCompleted);
            Assert.That(roster.Slots[2].Name.text, Is.EqualTo(data.Find("luka").Name));
            Assert.That(roster.LastNotice, Is.EqualTo(PartyRosterPresenter.SaveFailedMessage));

            // 開き直すと読み直す。読めなかったときは仮データを見せずに知らせる。
            view.Back.onClick.Invoke();
            view.MenuItems[item].onClick.Invoke();
            yield return SceneTests.WaitUntil(() => roster.LoadTask.IsCompleted);
            Assert.That(roster.Owned.text, Is.EqualTo(AdventurerRosterView.LoadFailedText));
            Assert.That(roster.LastNotice, Is.EqualTo(AdventurerRosterView.LoadFailedMessage));
            Assert.That(Shown(roster), Is.Empty);

            server.Fail = false;
            view.Back.onClick.Invoke();
            view.MenuItems[item].onClick.Invoke();
            yield return SceneTests.WaitUntil(() => roster.LoadTask.IsCompleted);
            Assert.That(server.Loads, Is.EqualTo(2));
            Assert.That(roster.Slots[2].Name.text, Is.EqualTo(data.Find("luka").Name));
            // 最後に選んでいたルカを選んだまま開く。
            Assert.That(roster.Name.text, Is.EqualTo(data.Find("luka").Name));
        }

        private static int ItemOf(GuideMenuView view, string key) =>
            System.Array.FindIndex(view.Definition.Items, entry => entry.Key == key);

        private static string[] Shown(AdventurerRosterView roster) =>
            roster
                .Tiles.Where(tile => tile.Button.gameObject.activeSelf)
                .OrderBy(tile => tile.Button.transform.GetSiblingIndex())
                .Select(tile => tile.Id)
                .ToArray();

        private static AdventurerTileWidget Tile(AdventurerRosterView roster, string id) =>
            roster.Tiles.Single(tile => tile.Id == id);
    }
}
