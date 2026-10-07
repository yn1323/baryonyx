using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Home;
using Baryonyx.StepBonus;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class GuideScenesTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            StepBonusSession.Reset();
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(GuideScenesTests));
        }

        // Homeの左下の3つのボタンと、冒険していないときの右下のカードから、それぞれの案内人の画面へ移り、
        // 「もどる」でHomeへ戻る。
        [UnityTest]
        public IEnumerator EveryHomeButtonOpensItsGuideSceneAndBackReturnsHome()
        {
            var home = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => home = value);
            foreach (
                var (action, sceneName) in new[]
                {
                    (HomeAction.Tavern, SceneNames.Pub),
                    (HomeAction.Workshop, SceneNames.Shop),
                    (HomeAction.Temple, SceneNames.Temple),
                    (HomeAction.TravelOffice, SceneNames.TravelOffice),
                }
            )
            {
                Assert.That(SceneNames.GuideFor(action), Is.EqualTo(sceneName));
                // 右下のカードは、冒険の状態を読んでから旅の案内所を開く。
                yield return SceneTests.WaitForTask(home.AdventureTask);
                // 続けて押しても、画面は1回だけ開く。
                ButtonFor(home.View, action).onClick.Invoke();
                ButtonFor(home.View, action).onClick.Invoke();
                Assert.That(
                    action == HomeAction.TravelOffice
                        ? home.Presenter.AdventureStarted
                        : home.Presenter.ScreenOpened,
                    Is.True
                );

                yield return SceneTests.WaitUntil(
                    () => SceneManager.GetActiveScene().name == sceneName,
                    message: sceneName + " did not open."
                );
                var guide = Object.FindAnyObjectByType<GuideSceneBootstrap>();
                Assert.That(guide, Is.Not.Null);
                yield return SceneTests.WaitUntil(() => SceneTests.GuideReady(guide));
                AssertGuideFitsTheScreen(guide.View);
                SceneTests.AssertTouchSize(guide.View.Back.transform, 0.7f);

                guide.Presenter.Back();
                yield return SceneTests.WaitUntil(
                    () => SceneManager.GetActiveScene().name == SceneNames.Home,
                    message: "Home did not open again."
                );
                home = Object.FindAnyObjectByType<HomeBootstrap>();
                yield return SceneTests.WaitUntil(() => SceneTests.HomeReady(home));
            }
        }

        [UnityTest]
        public IEnumerator ListScreensOpenAFullListAndConfirmTheChoice()
        {
            foreach (var sceneName in new[] { SceneNames.Pub, SceneNames.Shop, SceneNames.Temple })
            {
                var guide = default(GuideSceneBootstrap);
                yield return SceneTests.LoadGuide(sceneName, value => guide = value);
                var view = guide.View;
                var definition = view.Definition;

                Assert.That(view.MenuPanel.activeSelf, Is.True);
                Assert.That(view.ListPanel.activeSelf, Is.False);
                Assert.That(view.MenuItems.Length, Is.EqualTo(definition.Items.Length));
                for (int i = 0; i < definition.Items.Length; i++)
                {
                    // ほかのパネルから開く項目（編成の個別・付け替えの画面）は、メニューに行を出さない。
                    Assert.That(view.MenuItems[i] == null, Is.EqualTo(definition.Items[i].Hidden));
                    if (definition.Items[i].Hidden)
                        continue;
                    SceneTests.AssertTouchSize(view.MenuItems[i].transform, 0.7f);
                    // 編成と商会のメニューは、項目名の前にドット絵のアイコンを置く。
                    var icon = view.MenuItems[i].transform.Find("Icon");
                    Assert.That(icon != null, Is.EqualTo(definition.Items[i].Icon != null));
                }

                for (int i = 0; i < definition.Items.Length; i++)
                {
                    // 編成の各項目は一覧の代わりに専用のパネルを開く（ボーナスは下の別のテストで確かめる）。
                    if (view.PanelFor(i) != null || definition.Items[i].Hidden)
                        continue;
                    view.MenuItems[i].onClick.Invoke();
                    Assert.That(view.MenuPanel.activeSelf, Is.False);
                    Assert.That(view.ListPanel.activeSelf, Is.True);
                    var entries = definition.Items[i].Entries;
                    Assert.That(view.Entries.Count, Is.EqualTo(entries.Length));
                    // 行の文字はPrefabに焼き込んであり、定義と一致する。
                    for (int j = 0; j < entries.Length; j++)
                        Assert.That(
                            view.Entries[j]
                                .transform.Find("Name")
                                .GetComponent<TMPro.TMP_Text>()
                                .text,
                            Is.EqualTo(entries[j].Name)
                        );
                    Assert.That(view.Confirm.interactable, Is.False);

                    view.Entries[0].onClick.Invoke();
                    Assert.That(view.Confirm.interactable, Is.True);
                    Assert.That(Selected(view.Entries[0]), Is.True);
                    view.Confirm.onClick.Invoke();
                    Assert.That(
                        view.ToastMessage,
                        Is.EqualTo(GuideMenuPresenter.ComingSoon(definition.Items[i].ConfirmLabel))
                    );

                    view.Back.onClick.Invoke();
                    Assert.That(view.MenuPanel.activeSelf, Is.True);
                    Assert.That(guide.Presenter.Left, Is.False);
                }
            }
        }

        // 左で枠を選び、右のボーナスを押すと、その場でセットか入れ替えをして共通の通知の帯で知らせる。
        [UnityTest]
        public IEnumerator TavernBonusSettingsSetAndSwapBonuses()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            int item = System.Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == StepBonusSession.GuideItemKey
            );
            Assert.That(item, Is.GreaterThanOrEqualTo(0));
            Assert.That(view.MenuItems[item].transform.Find("Icon"), Is.Not.Null);
            view.MenuItems[item].onClick.Invoke();
            var settings = BonusSettings(view);
            Assert.That(settings.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.ListPanel.activeSelf, Is.False);
            // 左右の区画をまとめた大きな枠は「もどる」と重ならず、案内人は隠れる。
            Assert.That(settings.transform.Find("Panel/Effects"), Is.Not.Null);
            Assert.That(settings.transform.Find("Panel/Bonuses"), Is.Not.Null);
            SceneTests.AssertBelow(settings.transform.Find("Panel"), view.Back.transform);
            Assert.That(view.GuideArt.activeSelf, Is.False);

            var loadout = StepBonusSession.Loadout(settings.Data);
            Assert.That(settings.Slots.Length, Is.EqualTo(loadout.SlotCount));
            Assert.That(settings.Rows.Length, Is.EqualTo(loadout.Owned.Count));
            foreach (var slot in settings.Slots)
            {
                SceneTests.AssertTouchSize(slot.Button.transform);
                // アイコンは枠の縁の内側に収まる。
                AssertInside(slot.Icon.transform, slot.Frame.transform, 16f);
            }
            foreach (var row in settings.Rows)
                Assert.That(
                    row.SetMark.activeSelf,
                    Is.EqualTo(loadout.SlotOf(row.Id) >= 0),
                    row.Id
                );
            // 単体で開くと仮データの今日のACT（3,240）を使い、届いていない枠は暗い。
            Assert.That(settings.Slots[2].Icon.color, Is.EqualTo(Color.white));
            Assert.That(settings.Slots[3].Icon.color, Is.EqualTo(StepBonusSettingsView.Closed));

            // 空いているボーナスを5,000の枠へ。
            string free = loadout.Owned.First(roll => loadout.SlotOf(roll.Id) < 0).Id;
            settings.Slots[3].Button.onClick.Invoke();
            Row(settings, free).Button.onClick.Invoke();
            Assert.That(loadout.Bonus(3), Is.EqualTo(free));
            Assert.That(settings.LastNotice, Does.EndWith("をセットしました"));
            Assert.That(view.ToastMessage, Is.EqualTo(settings.LastNotice));
            Assert.That(view.Notice.Message, Is.EqualTo(settings.LastNotice));
            Assert.That(settings.Slots[3].Icon.sprite, Is.EqualTo(loadout.Definition(free).Icon));
            Assert.That(Row(settings, free).SetMark.activeSelf, Is.True);

            // 2,000の枠のボーナスを3,000の枠で押すと、2つの枠を入れ替える。
            string second = loadout.Bonus(1);
            string third = loadout.Bonus(2);
            settings.Slots[2].Button.onClick.Invoke();
            Row(settings, second).Button.onClick.Invoke();
            Assert.That(loadout.Bonus(2), Is.EqualTo(second));
            Assert.That(loadout.Bonus(1), Is.EqualTo(third));
            Assert.That(settings.LastNotice, Does.EndWith("を入れ替えました"));

            // タブで1つのカテゴリだけを並べる。
            settings.Tabs[2].onClick.Invoke();
            foreach (var row in settings.Rows)
                Assert.That(
                    row.Button.gameObject.activeSelf,
                    Is.EqualTo(loadout.Definition(row.Id).Category == StepBonusCategory.Drop),
                    row.Id
                );

            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            Assert.That(settings.gameObject.activeSelf, Is.False);
            Assert.That(view.GuideArt.activeSelf, Is.True);
        }

        // サーバーがあるときは、プレイヤーごとの持ち物と枠を読み、変更をサーバーに保存してから見せる。
        [UnityTest]
        public IEnumerator TavernBonusSettingsReadAndSaveThroughTheServer()
        {
            var server = new FakeStepBonusSource(
                new[]
                {
                    new StepBonusRoll { Id = "luck", Rank = StepBonusRank.E },
                    new StepBonusRoll { Id = "guard", Rank = StepBonusRank.A },
                },
                new[] { "luck", null, null, null, null }
            );
            StepBonusSession.Source = server;
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            Assert.That(guide.View.PanelFor(BonusItem(guide.View)), Is.Not.Null);
            guide.View.MenuItems[BonusItem(guide.View)].onClick.Invoke();
            var settings = BonusSettings(guide.View);
            yield return SceneTests.WaitUntil(
                () => settings.LoadTask.IsCompleted,
                message: "The bonuses did not load."
            );

            // サーバーの持ち物だけを、ランクの高い順に、サーバーのランクで並べる。
            var shown = settings
                .Rows.Where(row => row.Button.gameObject.activeSelf)
                .OrderBy(row => row.Button.transform.GetSiblingIndex())
                .Select(row => row.Id);
            Assert.That(shown, Is.EqualTo(new[] { "guard", "luck" }));
            Assert.That(Row(settings, "luck").Rank.text, Is.EqualTo("E"));
            Assert.That(Row(settings, "guard").Rank.text, Is.EqualTo("A"));
            Assert.That(settings.Slots[0].Name.text, Is.EqualTo("幸運"));
            Assert.That(settings.Slots[1].Name.text, Is.EqualTo("空き"));

            settings.Slots[1].Button.onClick.Invoke();
            Row(settings, "guard").Button.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => settings.Presenter.ChooseTask.IsCompleted,
                message: "The bonus was not saved."
            );
            Assert.That(server.Calls, Is.EqualTo(new[] { (1, "guard") }));
            Assert.That(settings.Slots[1].Name.text, Is.EqualTo("守り"));
            Assert.That(settings.LastNotice, Does.EndWith("をセットしました"));

            // 保存に失敗したら、枠を変えずに知らせる。
            server.Fail = true;
            settings.Slots[2].Button.onClick.Invoke();
            Row(settings, "luck").Button.onClick.Invoke();
            yield return SceneTests.WaitUntil(() => settings.Presenter.ChooseTask.IsCompleted);
            Assert.That(settings.Slots[0].Name.text, Is.EqualTo("幸運"));
            Assert.That(
                settings.LastNotice,
                Is.EqualTo(StepBonusSettingsPresenter.SaveFailedMessage)
            );
        }

        // 通知はどの案内人の画面でも、技名と同じ暗い帯の共通の部品で出す。
        [UnityTest]
        public IEnumerator GuideNoticesUseTheSharedNoticeBand()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Shop, value => guide = value);
            var notice = guide.View.Notice;
            Assert.That(notice, Is.Not.Null);
            Assert.That(notice.GetComponent<TranslucentTextPanel>(), Is.Not.Null);
            Assert.That(notice.Group.blocksRaycasts, Is.False);
            Assert.That(notice.Message, Is.Empty);

            guide.View.ShowToast("付け替え（準備中）");
            Assert.That(notice.Message, Is.EqualTo("付け替え（準備中）"));
            yield return SceneTests.WaitUntil(
                () => notice.Message == "",
                message: "The notice did not fade."
            );
        }

        // 旅の案内所は、メニューを通さずに行き先のリストを出す。未踏の地は名前を伏せ、選べない。
        [UnityTest]
        public IEnumerator TravelOfficeChoosesDestinationsFromTheList()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.TravelOffice, value => guide = value);
            var view = guide.View;
            var destinations = view.Definition.Destinations;

            Assert.That(view.DestinationPanel.activeSelf, Is.True);
            // 見ればわかるため、リストに題名を付けない。
            Assert.That(view.DestinationPanel.transform.Find("ListTitle"), Is.Null);
            // 行き先の画面には、メニューと項目のリストを作らない。
            Assert.That(view.MenuPanel, Is.Null);
            Assert.That(view.ListPanel, Is.Null);
            Assert.That(view.DestinationRows.Length, Is.EqualTo(destinations.Length));
            Assert.That(view.Depart.interactable, Is.False);

            // どの行き先にも推奨Lvを書く。
            for (int i = 0; i < destinations.Length; i++)
                Assert.That(
                    RowText(view.DestinationRows[i], "Badge"),
                    Is.EqualTo($"推奨Lv{destinations[i].RecommendedLevel}")
                );

            // 未踏の地は地名を伏せ、説明を出さない。
            int locked = System.Array.FindIndex(destinations, destination => destination.Locked);
            Assert.That(view.DestinationRows[locked].interactable, Is.False);
            Assert.That(RowText(view.DestinationRows[locked], "Name"), Is.EqualTo("？？？"));
            Assert.That(RowText(view.DestinationRows[locked], "Detail"), Is.Empty);

            int open = System.Array.FindIndex(
                destinations,
                destination => !destination.Locked && destination.Id == ""
            );
            view.DestinationRows[open].onClick.Invoke();
            Assert.That(view.Depart.interactable, Is.True);
            Assert.That(
                view.DestinationRows[open].transform.Find("Selected").gameObject.activeSelf,
                Is.True
            );
            view.Depart.onClick.Invoke();
            Assert.That(view.ToastMessage, Is.EqualTo("出発（準備中）"));
        }

        private static string RowText(Button row, string label) =>
            row.transform.Find(label).GetComponent<TMP_Text>().text;

        private static Button ButtonFor(HomeView view, HomeAction action) =>
            action switch
            {
                HomeAction.Tavern => view.TavernButton,
                HomeAction.Workshop => view.WorkshopButton,
                HomeAction.Temple => view.TempleButton,
                // 旅の案内所は、冒険していないときの右下のカードから開く。
                _ => view.ResumeButton,
            };

        private static StepBonusSettingsView BonusSettings(GuideMenuView view) =>
            view
                .ItemPanels.Where(panel => panel != null)
                .Select(panel => panel.GetComponent<StepBonusSettingsView>())
                .Single(settings => settings != null);

        private static int BonusItem(GuideMenuView view) =>
            System.Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == StepBonusSession.GuideItemKey
            );

        // 持ち物と枠を覚え、枠の変更を入れ替えも含めて反映するだけのサーバー。
        private sealed class FakeStepBonusSource : IStepBonusSource
        {
            private readonly StepBonusRoll[] owned;
            private readonly string[] slots;

            public FakeStepBonusSource(StepBonusRoll[] owned, string[] slots)
            {
                this.owned = owned;
                this.slots = slots;
            }

            public bool Fail { get; set; }
            public System.Collections.Generic.List<(int, string)> Calls { get; } = new();

            public System.Threading.Tasks.Task<StepBonusState> LoadAsync(
                System.Threading.CancellationToken token
            ) => System.Threading.Tasks.Task.FromResult(new StepBonusState(owned, slots.ToArray()));

            public async System.Threading.Tasks.Task<StepBonusState> SetSlotAsync(
                int slot,
                string bonusId,
                System.Threading.CancellationToken token
            )
            {
                await System.Threading.Tasks.Task.Yield();
                if (Fail)
                    throw new System.InvalidOperationException("offline");
                Calls.Add((slot, bonusId));
                int from = System.Array.IndexOf(slots, bonusId);
                if (from >= 0)
                    slots[from] = slots[slot];
                slots[slot] = bonusId;
                return new StepBonusState(owned, slots.ToArray());
            }
        }

        private static StepBonusRowWidget Row(StepBonusSettingsView settings, string id) =>
            settings.Rows.Single(row => row.Id == id);

        // inner が outer の内側に、縁の幅（設計座標）だけ空けて収まる。
        private static void AssertInside(Transform inner, Transform outer, float border)
        {
            var a = SceneTests.ScreenRect(inner);
            var b = SceneTests.ScreenRect(outer);
            float scale = b.width / ((RectTransform)outer).rect.width;
            float edge = border * scale - 0.5f;
            Assert.That(a.xMin - b.xMin, Is.GreaterThanOrEqualTo(edge), inner.name);
            Assert.That(b.xMax - a.xMax, Is.GreaterThanOrEqualTo(edge), inner.name);
            Assert.That(a.yMin - b.yMin, Is.GreaterThanOrEqualTo(edge), inner.name);
            Assert.That(b.yMax - a.yMax, Is.GreaterThanOrEqualTo(edge), inner.name);
        }

        private static bool Selected(Button button) =>
            button.transform.Find("Selected").gameObject.activeSelf;

        // 案内人の上・左・右が画面の外へはみ出さない（下端の腰だけは画面の下端に接してよい）。
        private static void AssertGuideFitsTheScreen(GuideMenuView view)
        {
            Canvas.ForceUpdateCanvases();
            var guide = (RectTransform)view.transform.Find("SafeArea/Guide");
            var screen = (RectTransform)view.transform;
            var corners = new Vector3[4];
            guide.GetWorldCorners(corners);
            var min = screen.InverseTransformPoint(corners[0]);
            var max = screen.InverseTransformPoint(corners[2]);
            var rect = screen.rect;
            Assert.That(min.x, Is.GreaterThanOrEqualTo(rect.xMin), "left");
            Assert.That(max.x, Is.LessThanOrEqualTo(rect.xMax), "right");
            Assert.That(max.y, Is.LessThanOrEqualTo(rect.yMax), "top");
        }
    }
}
