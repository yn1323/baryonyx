using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Economy;
using Baryonyx.Home;
using Baryonyx.Library;
using Baryonyx.Temple;
using Baryonyx.TravelOffice;
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
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(GuideScenesTests));
        }

        // Homeの左下の4つのボタンから、それぞれの案内人の画面へ移り、
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
                    (HomeAction.Library, SceneNames.Library),
                }
            )
            {
                Assert.That(SceneNames.GuideFor(action), Is.EqualTo(sceneName));
                // 続けて押しても、画面は1回だけ開く。
                ButtonFor(home.View, action).onClick.Invoke();
                ButtonFor(home.View, action).onClick.Invoke();
                Assert.That(home.Presenter.ScreenOpened, Is.True);

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

        // 上の段：「もどる」の右に画面名から始まるパンくず、右上に×、その左に所持ルーン。
        // ×はリストを開いていても、いちどでHomeへ戻る。
        [UnityTest]
        public IEnumerator HeaderShowsTheTrailAndTheCloseButtonReturnsHome()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Shop, value => guide = value);
            var view = guide.View;
            Assert.That(view.Breadcrumb.text, Is.EqualTo(view.Definition.Title));
            // ×のタブは「もどる」と同じ高さ96で、高さは7割まで許す。
            SceneTests.AssertTouchSize(view.Home.transform, 0.7f);
            yield return SceneTests.WaitForTask(guide.RunesTask);
            Assert.That(RuneBalance.Known, Is.Not.Null);
            Assert.That(view.Runes.text, Is.EqualTo(RuneBalance.Text(RuneBalance.Known)));
            // パンくずと所持ルーンは、「もどる」と×の間に収まる。
            var crumb = SceneTests.ScreenRect(view.Breadcrumb.transform);
            var runes = SceneTests.ScreenRect(view.Runes.transform.parent);
            Assert.That(
                crumb.xMin,
                Is.GreaterThanOrEqualTo(SceneTests.ScreenRect(view.Back.transform).xMax)
            );
            Assert.That(crumb.xMax, Is.LessThanOrEqualTo(runes.xMin));
            Assert.That(
                runes.xMax,
                Is.LessThanOrEqualTo(SceneTests.ScreenRect(view.Home.transform).xMin)
            );

            view.MenuItems[0].onClick.Invoke();
            Assert.That(
                view.Breadcrumb.text,
                Is.EqualTo(
                    view.Definition.Title
                        + GuideMenuView.TrailSeparator
                        + view.Definition.Items[0].Label
                )
            );
            view.Home.onClick.Invoke();
            view.Home.onClick.Invoke();
            Assert.That(guide.Presenter.Left, Is.True);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Home,
                message: "Home did not open."
            );
        }

        [UnityTest]
        public IEnumerator ListScreensOpenAFullListAndConfirmTheChoice()
        {
            foreach (var sceneName in new[] { SceneNames.Pub, SceneNames.Shop })
            {
                var guide = default(GuideSceneBootstrap);
                yield return SceneTests.LoadGuide(sceneName, value => guide = value);
                var view = guide.View;
                var definition = view.Definition;

                Assert.That(view.MenuPanel.activeSelf, Is.True);
                Assert.That(view.ListPanel.activeSelf, Is.False);
                // メニューは案内人とカードだけで、枠（ぼかした背景・ヘッダーの帯と線）を出さない。
                Assert.That(view.Framed, Is.False);
                Assert.That(view.MenuItems.Length, Is.EqualTo(definition.Items.Length));
                for (int i = 0; i < definition.Items.Length; i++)
                {
                    // ほかのパネルから開く項目（編成の冒険者のタブ）は、メニューに行を出さない。
                    Assert.That(view.MenuItems[i] == null, Is.EqualTo(definition.Items[i].Hidden));
                    if (definition.Items[i].Hidden)
                        continue;
                    SceneTests.AssertTouchSize(view.MenuItems[i].transform, 0.7f);
                    // 編成・商会・神殿のメニューは、項目名の前にアイコンを置く。
                    var icon = view.MenuItems[i].transform.Find(CardContent + "Icon");
                    Assert.That(icon != null, Is.EqualTo(definition.Items[i].Icon != null));
                    // 行はアイコンと項目名だけで、何ができるかの説明は添えない。
                    Assert.That(
                        view.MenuItems[i]
                            .GetComponentsInChildren<TMP_Text>(true)
                            .Select(t => t.text),
                        Is.EqualTo(new[] { definition.Items[i].Label })
                    );
                }

                for (int i = 0; i < definition.Items.Length; i++)
                {
                    // 編成の各項目は一覧の代わりに専用のパネルを開く。
                    if (view.PanelFor(i) != null || definition.Items[i].Hidden)
                        continue;
                    view.MenuItems[i].onClick.Invoke();
                    Assert.That(view.MenuPanel.activeSelf, Is.False);
                    Assert.That(view.ListPanel.activeSelf, Is.True);
                    // 一覧は、編成の冒険者と同じ枠の中に出し、案内人は左に残す。
                    Assert.That(view.Framed, Is.True);
                    Assert.That(view.GuideArt.activeSelf, Is.True);
                    var entries = definition.Items[i].Entries;
                    Assert.That(view.Entries.Count, Is.EqualTo(entries.Length));
                    // 行の文字はPrefabに焼き込んであり、定義と一致する。
                    for (int j = 0; j < entries.Length; j++)
                        Assert.That(
                            view.Entries[j]
                                .transform.Find(CardContent + "Name")
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
                    Assert.That(view.Framed, Is.False);
                    Assert.That(guide.Presenter.Left, Is.False);
                }
            }
        }

        // 神殿のメニューの「召喚」は、全画面の召喚の画面を開く。左下の「確率」で確率の窓を開き、
        // 召喚すると、案内人の画面を隠して演出を流し、押して飛ばすと結果の窓を出す。
        [UnityTest]
        public IEnumerator TempleSummonsOverTheWholeScreenWithItsPresentation()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Temple, value => guide = value);
            var view = guide.View;
            Assert.That(view.MenuItems, Has.Length.EqualTo(1));
            view.MenuItems[0].onClick.Invoke();
            var summon = view.PanelFor(0).GetComponent<TempleSummonView>();
            Assert.That(summon.gameObject.activeInHierarchy, Is.True);
            // 全画面のため、案内人も枠も出さない。上の帯は絵の手前に残る。
            Assert.That(view.GuideArt.activeSelf, Is.False);
            Assert.That(view.Framed, Is.False);
            Assert.That(summon.transform.GetSiblingIndex(), Is.Zero);
            Assert.That(
                view.Breadcrumb.text,
                Is.EqualTo(view.Definition.Title + GuideMenuView.TrailSeparator + "召喚")
            );
            SceneTests.AssertTouchSize(summon.Single.transform);
            SceneTests.AssertTouchSize(summon.Ten.transform);
            SceneTests.AssertTouchSize(summon.RatesButton.transform, 0.7f);

            summon.RatesButton.onClick.Invoke();
            Assert.That(summon.Rates.IsOpen, Is.True);
            view.Back.onClick.Invoke();
            Assert.That(summon.Rates.IsOpen, Is.False, "Back closes the rates first.");
            Assert.That(summon.gameObject.activeInHierarchy, Is.True);

            // サーバーのない召喚は、画面に出ている残高から引いて、仮に引いた結果を出す。
            var results = summon.Results;
            var canvas = view.GetComponent<Canvas>();
            RuneBalance.Set(100000);
            summon.Ten.onClick.Invoke();
            yield return SceneTests.WaitUntil(() => summon.Presenting, message: "No presentation.");
            Assert.That(
                canvas.enabled,
                Is.False,
                "The guide screen hides behind the presentation."
            );
            Assert.That(summon.TenShow.gameObject.activeInHierarchy, Is.True);
            // 押さなくても、札が並んだあとひとりでに結果まで進む。
            yield return SceneTests.WaitForTask(summon.SummonTask, 15f);
            Assert.That(canvas.enabled, Is.True);
            Assert.That(summon.TenShow.gameObject.activeSelf, Is.False);
            Assert.That(RuneBalance.Known, Is.EqualTo(100000 - TempleSummon.TenPrice));
            Assert.That(results.IsOpen, Is.True);
            Assert.That(results.Shown.Count, Is.EqualTo(TempleSummon.TenCount));
            Assert.That(
                results.Shown.Any(result =>
                    result.Entry.Kind == LibraryKind.Character
                    || result.Entry.Stars >= TempleSummon.GuaranteedStars
                ),
                Is.True
            );
            view.Back.onClick.Invoke();
            Assert.That(results.IsOpen, Is.False);

            summon.Single.onClick.Invoke();
            yield return SceneTests.WaitUntil(() => summon.Presenting, message: "No presentation.");
            Assert.That(summon.Show.gameObject.activeInHierarchy, Is.True);
            // 演出の間の「もどる」は、押して飛ばすのと同じ。
            view.Back.onClick.Invoke();
            yield return SceneTests.WaitForTask(summon.SummonTask);
            Assert.That(guide.Presenter.Left, Is.False);
            Assert.That(results.Shown.Count, Is.EqualTo(1));
            results.Veil.onClick.Invoke();
            Assert.That(results.IsOpen, Is.False);

            // 残高が足りなければ引かず、通知の帯で知らせる。
            RuneBalance.Set(100);
            summon.Ten.onClick.Invoke();
            Assert.That(summon.Presenting, Is.False);
            Assert.That(view.ToastMessage, Is.EqualTo(TempleSummonSession.ShortMessage));

            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            view.Back.onClick.Invoke();
            Assert.That(guide.Presenter.Left, Is.True);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Home,
                message: "Home did not open."
            );
        }

        // 通知は入力を遮らず、出したあと消える。
        [UnityTest]
        public IEnumerator GuideNoticesUseTheSharedNoticeBand()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Shop, value => guide = value);
            var notice = guide.View.Notice;
            Assert.That(notice, Is.Not.Null);
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
            // 世界地図のダンジョン一覧として開くため、案内人を置かない。
            Assert.That(view.GuideArt, Is.Null);
            Assert.That(guide.BackSceneName, Is.EqualTo(SceneNames.WorldMap));
            // 行き先のリストは、編成の冒険者と同じ枠の中に出す。
            Assert.That(view.Framed, Is.True);
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

            // 冒険の行き先は、どれも出発できる（出発すると冒険が始まるため、ここでは押さない）。
            var areas = Baryonyx.Adventure.AdventureCatalog.Areas;
            var made = destinations.Where(destination => destination.Id != "").ToArray();
            Assert.That(
                made.Select(destination => destination.Id),
                Is.EqualTo(areas.Select(area => area.Id))
            );
            Assert.That(
                made.Select(destination => destination.RecommendedLevel),
                Is.EqualTo(areas.Select(area => area.Level))
            );
            // 中身を作っていないダンジョンは、説明の代わりに街の名前を出す。12の街に54のダンジョンがある。
            Assert.That(destinations.Count(destination => !destination.Locked), Is.EqualTo(54));
            Assert.That(
                destinations.Where(destination => !destination.Locked).All(d => d.Town != ""),
                Is.True
            );

            // 街道で着いた街のダンジョンだけを出す。テストの街道はポルタ・ブリーザ・ピーノまで進んでいる。
            yield return SceneTests.WaitForTask(guide.UnlockTask);
            for (int i = 0; i < destinations.Length; i++)
                Assert.That(
                    view.DestinationRows[i].gameObject.activeSelf,
                    Is.EqualTo(destinations[i].Town is "" or "porta" or "brezza" or "pino"),
                    destinations[i].Name
                );
            int open = System.Array.FindIndex(
                destinations,
                destination => destination.Id == Baryonyx.Adventure.AdventureCatalog.ForestRuins
            );
            view.DestinationRows[open].onClick.Invoke();
            Assert.That(view.Depart.interactable, Is.True);
            Assert.That(
                view.DestinationRows[open].transform.Find("Selected").gameObject.activeSelf,
                Is.True
            );
        }

        // 街道の世界地図の右下のボタンでダンジョン一覧（旅の案内所）を開き、「もどる」で世界地図へ、
        // ×でHomeへ戻る。世界地図の左上のボタンはHomeへ戻る。
        [UnityTest]
        public IEnumerator WorldMapOpensTheDungeonListAndBackReturnsToTheMap()
        {
            var map = default(WorldMapBootstrap);
            yield return SceneTests.LoadWorldMap(value => map = value);
            SceneTests.AssertTouchSize(map.View.Dungeons.transform);
            SceneTests.AssertTouchSize(map.View.Back.transform);

            map.View.Dungeons.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.TravelOffice,
                seconds: 10f,
                message: "The list of dungeons did not open."
            );
            var guide = Object.FindAnyObjectByType<GuideSceneBootstrap>();
            yield return SceneTests.WaitUntil(() => SceneTests.GuideReady(guide));
            guide.Presenter.Back();
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.WorldMap,
                seconds: 10f,
                message: "Back did not return to the world map."
            );

            map = Object.FindAnyObjectByType<WorldMapBootstrap>();
            yield return SceneTests.WaitUntil(() => SceneTests.WorldMapReady(map));
            map.View.Dungeons.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.TravelOffice,
                seconds: 10f,
                message: "The list of dungeons did not open again."
            );
            guide = Object.FindAnyObjectByType<GuideSceneBootstrap>();
            yield return SceneTests.WaitUntil(() => SceneTests.GuideReady(guide));
            guide.View.Home.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Home,
                seconds: 10f,
                message: "The close button did not return to Home."
            );

            yield return SceneTests.LoadWorldMap(value => map = value);
            map.View.Back.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Home,
                seconds: 10f,
                message: "The world map did not return to Home."
            );
        }

        // 行き先の行に見つけたお宝の数を出し、宝箱のボタンで画面の中央にお宝の窓を開く。
        // 見つけたお宝は絵のまま、まだのお宝は影にする。「もどる」では窓だけを閉じる。
        [UnityTest]
        public IEnumerator TravelOfficeShowsEachDestinationsTreasures()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.TravelOffice, value => guide = value);
            var view = guide.View;
            var treasures = view.Overlay.GetComponent<TravelTreasureView>();
            yield return SceneTests.WaitForTask(treasures.LoadTask);
            // テストではサーバーがないため、ライブラリの仮の記録を読む。
            var records = LibraryMemorySource.Mock();

            // 未踏の地と、中身を作っていないダンジョンには、お宝のボタンを出さない。
            var destinations = view
                .Definition.Destinations.Where(d => !d.Locked && d.Id != "")
                .ToArray();
            Assert.That(
                treasures.Rows.Select(row => row.DestinationId),
                Is.EqualTo(destinations.Select(d => d.Id))
            );
            foreach (var row in treasures.Rows)
            {
                Assert.That(
                    row.Count.text,
                    Is.EqualTo(TravelTreasures.CountText(row.DestinationId, records))
                );
                // 街道でまだ着いていない街の行は隠れていて、押せない。
                if (row.Open.gameObject.activeInHierarchy)
                    SceneTests.AssertTouchSize(row.Open.transform);
            }
            Assert.That(treasures.IsOpen, Is.False);

            // 宝箱のボタンは行を選ばずに窓を開く。
            treasures.Rows[0].Open.onClick.Invoke();
            Assert.That(treasures.IsOpen, Is.True);
            Assert.That(guide.Presenter.State.Selected, Is.EqualTo(-1));
            Assert.That(treasures.Title.text, Is.EqualTo(destinations[0].Name + "のお宝"));
            // 見出しに、その行き先をクリアした回数を出す（冒険の記録は、出発の準備で読む）。
            yield return SceneTests.WaitForTask(guide.Departure.Work);
            treasures.Rows[0].Open.onClick.Invoke();
            Assert.That(
                treasures.Clears.text,
                Is.EqualTo(
                    TravelTreasureView.ClearsText(
                        Baryonyx.Adventure.AdventureSession.Current,
                        destinations[0].Id
                    )
                )
            );
            StringAssert.StartsWith("クリア ", treasures.Clears.text);
            Assert.That(
                treasures.Count.text,
                Is.EqualTo(TravelTreasures.CountText(destinations[0].Id, records))
            );

            var list = treasures.Lists[treasures.OpenIndex];
            Assert.That(list.Content.gameObject.activeSelf, Is.True);
            Assert.That(list.Tiles.Any(tile => records.IsFound(tile.Kind, tile.Id)), Is.True);
            Assert.That(list.Tiles.Any(tile => !records.IsFound(tile.Kind, tile.Id)), Is.True);
            foreach (var tile in list.Tiles)
            {
                bool found = records.IsFound(tile.Kind, tile.Id);
                if (tile.Kind == LibraryKind.Skill)
                {
                    // スキルの挿絵は四角いため、まだ見つけていない札は影でなく「？」にする。
                    Assert.That(tile.Art.enabled, Is.EqualTo(found), tile.Id);
                    Assert.That(tile.Unknown.activeSelf, Is.EqualTo(!found), tile.Id);
                }
                else
                {
                    Assert.That(
                        tile.Art.color == LibraryCatalogView.ShadowTint,
                        Is.EqualTo(!found),
                        tile.Id
                    );
                    Assert.That(tile.Unknown == null, Is.True, tile.Id);
                }
                // ☆は、まだ見つけていないお宝にも出す。
                Assert.That(tile.Stars.text, Is.Not.Empty, tile.Id);
            }

            // 押したお宝の名前を下に出す。まだ見つけていないお宝は名前を伏せる。
            var foundTile = list.Tiles.First(tile => records.IsFound(tile.Kind, tile.Id));
            treasures.Select(foundTile.Kind, foundTile.Id);
            var entry = treasures.Data.Entries.Single(e =>
                e.Kind == foundTile.Kind && e.Id == foundTile.Id
            );
            StringAssert.StartsWith(entry.Name, treasures.Info.text);
            // 分類は段の名前でわかるため、名前の行に書かない。
            StringAssert.DoesNotContain(LibraryKinds.Label(foundTile.Kind), treasures.Info.text);
            var hiddenTile = list.Tiles.First(tile => !records.IsFound(tile.Kind, tile.Id));
            treasures.Select(hiddenTile.Kind, hiddenTile.Id);
            StringAssert.StartsWith(TravelTreasureView.HiddenName, treasures.Info.text);
            StringAssert.Contains("★", treasures.Info.text);
            // まだ見つけていないことは伏せた名前でわかるため、説明の欄には何も書かない。
            Assert.That(treasures.InfoBody.text, Is.Empty);

            // 「もどる」は窓を閉じるだけで、画面に残る。
            view.PressBack();
            Assert.That(treasures.IsOpen, Is.False);
            Assert.That(guide.Presenter.Left, Is.False);

            // 窓の外の暗い幕を押しても閉じる。
            treasures.Rows[1].Open.onClick.Invoke();
            Assert.That(treasures.Title.text, Is.EqualTo(destinations[1].Name + "のお宝"));
            treasures.Veil.onClick.Invoke();
            Assert.That(treasures.IsOpen, Is.False);
        }

        private static string RowText(Button row, string label) =>
            row.transform.Find(CardContent + label).GetComponent<TMP_Text>().text;

        // メニューの行・一覧の行・行き先はすりガラスのカードで、文字とアイコンは板と一緒に沈む中身に置く。
        private const string CardContent = "Body/Content/";

        private static Button ButtonFor(HomeView view, HomeAction action) =>
            action switch
            {
                HomeAction.Tavern => view.TavernButton,
                HomeAction.Workshop => view.WorkshopButton,
                HomeAction.Temple => view.TempleButton,
                HomeAction.Library => view.LibraryButton,
                // 旅の案内所は、冒険していないときの右下のカードから開く。
                _ => view.ResumeButton,
            };

        private static bool Selected(Button button) =>
            button.transform.Find("Selected").gameObject.activeSelf;

        // 案内人の上・左・右が画面の外へはみ出さない（下端の腰だけは画面の下端に接してよい）。
        private static void AssertGuideFitsTheScreen(GuideMenuView view)
        {
            Canvas.ForceUpdateCanvases();
            var guide = (RectTransform)view.transform.Find("SafeArea/Core/Guide");
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
