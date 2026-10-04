using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Party;
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

        // 酒場の「編成」は一覧の代わりに編成を開く。左で枠を選んで右の仲間を押すとその場で入れ替え、
        // 「外す」で空きにする。変えたことは共通の通知の帯で知らせる。
        [UnityTest]
        public IEnumerator FormationSwapsAndTakesOutMembers()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            int item = System.Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == PartySession.GuideItemKey
            );
            Assert.That(item, Is.GreaterThanOrEqualTo(0));
            Assert.That(view.MenuItems[item].transform.Find("Icon"), Is.Not.Null);
            view.MenuItems[item].onClick.Invoke();
            var formation = view.PanelFor(item).GetComponent<PartyFormationView>();
            Assert.That(formation.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.ListPanel.activeSelf, Is.False);
            // 左右の区画をまとめた大きな枠は「もどる」と重ならず、案内人は隠れる。
            Assert.That(view.GuideArt.activeSelf, Is.False);
            AssertBelow(formation.transform.Find("Panel"), view.Back.transform);

            var party = PartySession.Formation(formation.Data);
            Assert.That(formation.Slots.Length, Is.EqualTo(PartyFormation.Size));
            foreach (var slot in formation.Slots)
                SceneTests.AssertTouchSize(slot.Button.transform);
            // 左は2行2列。開いたときは1つ目の枠を選んでいる。
            var first = ScreenRect(formation.Slots[0].Button.transform);
            Assert.That(
                ScreenRect(formation.Slots[1].Button.transform).xMin,
                Is.GreaterThanOrEqualTo(first.xMax)
            );
            Assert.That(
                ScreenRect(formation.Slots[2].Button.transform).yMax,
                Is.LessThanOrEqualTo(first.yMin)
            );
            Assert.That(formation.Slots[0].Selected.activeSelf, Is.True);
            Assert.That(formation.Slots[1].Selected.activeSelf, Is.False);
            Assert.That(formation.Slots[0].Name.text, Is.EqualTo(party.Name(party.Member(0))));
            // カード4枚の属性。属性のないカードは「無」と書く。
            for (int i = 0; i < formation.Slots.Length; i++)
            {
                var member = party.Find(party.Member(i));
                for (int j = 0; j < member.Cards.Length; j++)
                {
                    bool none = formation.Data.IconOf(member.Cards[j].Skill) == null;
                    Assert.That(formation.Slots[i].Cards[j].Icon.enabled, Is.EqualTo(!none));
                    Assert.That(formation.Slots[i].Cards[j].None.activeSelf, Is.EqualTo(none));
                }
            }

            // 右はパーティにいない仲間だけを持っている順に並べ、最後に「外す」を置く。
            Assert.That(Shown(formation), Is.EqualTo(party.Bench.Select(member => member.Id)));
            var leave = formation.Leave.transform;
            Assert.That(leave.GetSiblingIndex(), Is.EqualTo(leave.parent.childCount - 1));
            SceneTests.AssertTouchSize(leave);
            foreach (var id in Shown(formation))
            {
                var tile = Tile(formation, id).Button.transform;
                SceneTests.AssertTouchSize(tile);
                // 仲間は全身の立ち姿で、タイルからはみ出さず、名前と重ならない。
                var box = ScreenRect(tile);
                var figure = ScreenRect(tile.Find("Figure"));
                Assert.That(figure.xMin, Is.GreaterThanOrEqualTo(box.xMin - 0.5f), id);
                Assert.That(figure.xMax, Is.LessThanOrEqualTo(box.xMax + 0.5f), id);
                Assert.That(figure.yMax, Is.LessThanOrEqualTo(box.yMax + 0.5f), id);
                Assert.That(
                    figure.yMin,
                    Is.GreaterThanOrEqualTo(ScreenRect(tile.Find("Name")).yMax - 0.5f),
                    id
                );
                Assert.That(
                    figure.height / figure.width,
                    Is.EqualTo(
                            PartyFormationView.Figure.height
                                / (float)PartyFormationView.Figure.width
                        )
                        .Within(0.02f),
                    id
                );
            }

            // 2つ目の枠のキャラを、右の仲間と入れ替える。
            string before = party.Member(1);
            string bench = Shown(formation).First();
            formation.Slots[1].Button.onClick.Invoke();
            Tile(formation, bench).Button.onClick.Invoke();
            Assert.That(party.Member(1), Is.EqualTo(bench));
            Assert.That(formation.Slots[1].Name.text, Is.EqualTo(party.Name(bench)));
            Assert.That(Tile(formation, bench).Button.gameObject.activeSelf, Is.False);
            Assert.That(Tile(formation, before).Button.gameObject.activeSelf, Is.True);
            Assert.That(formation.LastNotice, Does.EndWith("を入れ替えました"));
            Assert.That(view.ToastMessage, Is.EqualTo(formation.LastNotice));

            // 外すと空きになり、「外す」は押せなくなる。空いた枠には右の仲間を入れられる。
            formation.Leave.onClick.Invoke();
            Assert.That(party.Member(1), Is.Null);
            Assert.That(formation.Slots[1].Empty.gameObject.activeSelf, Is.True);
            Assert.That(formation.Slots[1].Sprite.gameObject.activeSelf, Is.False);
            Assert.That(formation.Leave.interactable, Is.False);
            Assert.That(formation.LastNotice, Does.EndWith("を外しました"));
            Tile(formation, before).Button.onClick.Invoke();
            Assert.That(party.Member(1), Is.EqualTo(before));
            Assert.That(formation.Slots[1].Sprite.gameObject.activeSelf, Is.True);
            Assert.That(formation.LastNotice, Does.EndWith("を編成しました"));

            // 「もどる」でメニューへ戻る。開き直しても、アプリを動かしている間は編成が残る。
            formation.Slots[1].Button.onClick.Invoke();
            Tile(formation, bench).Button.onClick.Invoke();
            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            Assert.That(formation.gameObject.activeSelf, Is.False);
            Assert.That(view.GuideArt.activeSelf, Is.True);
            view.MenuItems[item].onClick.Invoke();
            Assert.That(formation.Slots[1].Name.text, Is.EqualTo(party.Name(bench)));
            Assert.That(Tile(formation, bench).Button.gameObject.activeSelf, Is.False);
        }

        // 育成で上げたLvと、酒場のスキルの画面で付け替えたカードを、編成を開くたびに出す。
        [UnityTest]
        public IEnumerator FormationShowsRaisedLevelsAndChangedCards()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            int item = System.Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == PartySession.GuideItemKey
            );
            view.MenuItems[item].onClick.Invoke();
            var formation = view.PanelFor(item).GetComponent<PartyFormationView>();
            var data = formation.Data;
            var party = PartySession.Formation(data);
            string member = party.Member(1);
            string bench = Shown(formation).First();
            Assert.That(
                formation.Slots[1].Level.text,
                Is.EqualTo(PartyFormationView.LevelText(data.Find(member).Level))
            );

            // 編成を閉じている間に、ほかの画面でLvを上げ、カードを付け替える。
            view.Back.onClick.Invoke();
            PartySession.SetLevel(member, 25);
            PartySession.SetLevel(bench, 9);
            var cards = new[] { "Slash", "Heal", "Fire", "Thunder" };
            PartySession.SetCards(member, cards);
            view.MenuItems[item].onClick.Invoke();

            Assert.That(formation.Slots[1].Level.text, Is.EqualTo("Lv 25"));
            Assert.That(Tile(formation, bench).Level.text, Is.EqualTo("Lv 9"));
            for (int j = 0; j < cards.Length; j++)
            {
                var widget = formation.Slots[1].Cards[j];
                var icon = data.IconOf(cards[j]);
                Assert.That(widget.Icon.sprite, Is.EqualTo(icon), cards[j]);
                Assert.That(widget.None.activeSelf, Is.EqualTo(icon == null), cards[j]);
            }
            // 回復は属性がなく「無」、ほかは属性のアイコンを出す。
            Assert.That(formation.Slots[1].Cards[1].None.activeSelf, Is.True);
            Assert.That(formation.Slots[1].Cards[0].Icon.sprite, Is.Not.Null);

            // 枠に入れた仲間も、上げたLvで出す。
            formation.Slots[1].Button.onClick.Invoke();
            Tile(formation, bench).Button.onClick.Invoke();
            Assert.That(formation.Slots[1].Level.text, Is.EqualTo("Lv 9"));
            Assert.That(Tile(formation, member).Level.text, Is.EqualTo("Lv 25"));
        }

        // サーバーがあるときは、開くたびにサーバーの編成を読み、入れ替えを保存してから表示を変える。
        // 読み込むまでは仮データの編成を見せず、保存・読み込みに失敗したら通知の帯で知らせる。
        [UnityTest]
        public IEnumerator FormationReadsAndSavesThePartyOnTheServer()
        {
            var server = new FakePartySource(0, "anselm", "toma")
                .With("toma", 20, "Fire", "Meteor", "Ice", "Blizzard")
                .With("luka", 11, "VitalThrust", "ArrowRain", "Thunder", "LightningBolt")
                .With("anselm", 8, "EarthSplitter", "HolyHammer", "Fire", "Embers")
                .With("greta", 7, "VitalThrust", "PoisonNeedle", "Ice", "Icicles");
            PartySession.Source = server;
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            int item = System.Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == PartySession.GuideItemKey
            );
            view.MenuItems[item].onClick.Invoke();
            var formation = view.PanelFor(item).GetComponent<PartyFormationView>();
            Assert.That(formation.Owned.text, Is.EqualTo(PartyFormationView.LoadingText));
            Assert.That(Shown(formation), Is.Empty);
            Assert.That(formation.Slots[0].Name.text, Is.Empty);
            yield return SceneTests.WaitUntil(
                () => formation.LoadTask.IsCompleted,
                message: "The party did not load."
            );

            // サーバーの持っているキャラ・編成・Lvを出す。
            var data = formation.Data;
            Assert.That(formation.Owned.text, Is.EqualTo("所持 4人"));
            Assert.That(formation.Slots[0].Name.text, Is.EqualTo(data.Find("anselm").Name));
            Assert.That(formation.Slots[1].Level.text, Is.EqualTo("Lv 20"));
            Assert.That(formation.Slots[2].Empty.gameObject.activeSelf, Is.True);
            Assert.That(Shown(formation), Is.EqualTo(new[] { "luka", "greta" }));

            // 空いた枠にルカを入れると、サーバーに保存してから枠に出す。
            formation.Slots[2].Button.onClick.Invoke();
            Tile(formation, "luka").Button.onClick.Invoke();
            Assert.That(formation.Presenter.Saving, Is.True);
            Assert.That(formation.Slots[2].Empty.gameObject.activeSelf, Is.True);
            yield return SceneTests.WaitUntil(() => formation.Presenter.ChangeTask.IsCompleted);
            Assert.That(server.Calls, Is.EqualTo(new[] { "slot 2 luka" }));
            Assert.That(formation.Slots[2].Name.text, Is.EqualTo(data.Find("luka").Name));
            Assert.That(formation.LastNotice, Does.EndWith("を編成しました"));

            // 保存できなかったときは枠を変えずに知らせる。
            server.Fail = true;
            formation.Leave.onClick.Invoke();
            yield return SceneTests.WaitUntil(() => formation.Presenter.ChangeTask.IsCompleted);
            Assert.That(formation.Slots[2].Name.text, Is.EqualTo(data.Find("luka").Name));
            Assert.That(
                formation.LastNotice,
                Is.EqualTo(PartyFormationPresenter.SaveFailedMessage)
            );

            // 開き直すと読み直す。読めなかったときは仮データを見せずに知らせる。
            view.Back.onClick.Invoke();
            view.MenuItems[item].onClick.Invoke();
            yield return SceneTests.WaitUntil(() => formation.LoadTask.IsCompleted);
            Assert.That(formation.Owned.text, Is.EqualTo(PartyFormationView.LoadFailedText));
            Assert.That(formation.LastNotice, Is.EqualTo(PartyFormationView.LoadFailedMessage));
            Assert.That(Shown(formation), Is.Empty);

            server.Fail = false;
            view.Back.onClick.Invoke();
            view.MenuItems[item].onClick.Invoke();
            yield return SceneTests.WaitUntil(() => formation.LoadTask.IsCompleted);
            Assert.That(server.Loads, Is.EqualTo(2));
            Assert.That(formation.Slots[2].Name.text, Is.EqualTo(data.Find("luka").Name));
        }

        private static string[] Shown(PartyFormationView formation) =>
            formation
                .Members.Where(tile => tile.Button.gameObject.activeSelf)
                .OrderBy(tile => tile.Button.transform.GetSiblingIndex())
                .Select(tile => tile.Id)
                .ToArray();

        private static PartyMemberWidget Tile(PartyFormationView formation, string id) =>
            formation.Members.Single(tile => tile.Id == id);

        private static Rect ScreenRect(Transform target)
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void AssertBelow(Transform lower, Transform upper) =>
            Assert.That(
                ScreenRect(lower).yMax,
                Is.LessThanOrEqualTo(ScreenRect(upper).yMin + 0.5f)
            );
    }
}
