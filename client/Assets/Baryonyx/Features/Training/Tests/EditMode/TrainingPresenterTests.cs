using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Baryonyx.Party;
using Baryonyx.Training;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class TrainingPresenterTests
    {
        private sealed class FakeView : ITrainingView
        {
            public event Action PrevPressed;
            public event Action NextPressed;
            public event Action LevelUpPressed;
            public event Action LessPressed;
            public event Action MorePressed;
            public event Action MaxPressed;
            public event Action CancelPressed;
            public event Action ConfirmPressed;
            public event Action CardsPressed;

            public TrainingState Last;
            public readonly List<string> Notices = new();
            public readonly List<string> CardsOpened = new();
            public bool CardsReady;

            public void Render(TrainingState state) => Last = state;

            public void ShowNotice(string message) => Notices.Add(message);

            public bool OpenCards(string id)
            {
                CardsOpened.Add(id);
                return CardsReady;
            }

            public void Prev() => PrevPressed?.Invoke();

            public void Next() => NextPressed?.Invoke();

            public void LevelUp() => LevelUpPressed?.Invoke();

            public void Less() => LessPressed?.Invoke();

            public void More() => MorePressed?.Invoke();

            public void Max() => MaxPressed?.Invoke();

            public void Cancel() => CancelPressed?.Invoke();

            public void Confirm() => ConfirmPressed?.Invoke();

            public void Cards() => CardsPressed?.Invoke();
        }

        private sealed class FakeStore : ITrainingStore
        {
            public readonly Dictionary<string, int> Levels = new();

            public int LevelOf(string id) => Levels.TryGetValue(id, out int level) ? level : 1;

            public void SetLevel(string id, int level) => Levels[id] = level;

            public readonly Dictionary<string, string[]> Cards = new();

            public IReadOnlyList<string> CardsOf(string id) =>
                Cards.TryGetValue(id, out var cards) ? cards : Array.Empty<string>();

            public long Runes { get; set; }

            public void Spend(long runes) => Runes -= runes;

            public int MaxLevel { get; set; } = 30;
            public int CostPerLevel { get; set; } = 100;

            public string Selected { get; set; }
        }

        private TrainingMockData data;
        private FakeView view;
        private FakeStore store;
        private TrainingPresenter presenter;

        [SetUp]
        public void CreateData()
        {
            data = ScriptableObject.CreateInstance<TrainingMockData>();
            data.CostPerLevel = 100;
            data.MaxLevel = 30;
            data.Characters = new[] { Growth("toma"), Growth("luka"), Growth("aria") };
            view = new FakeView();
            store = new FakeStore
            {
                Runes = 8450,
                Levels =
                {
                    ["toma"] = 12,
                    ["luka"] = 11,
                    ["aria"] = 10,
                },
                Cards = { ["toma"] = new[] { "Fire", "Meteor", "Ice", "Blizzard" } },
            };
        }

        [TearDown]
        public void Destroy()
        {
            presenter?.Dispose();
            UnityEngine.Object.DestroyImmediate(data);
        }

        private static TrainingCharacter Growth(string id) =>
            new()
            {
                Id = id,
                Base = new TrainingStats(102, 57, 120, 55, 50),
                Growth = new TrainingStats(18, 3, 18, 5, 2),
                Passives = new[] { Skill("魔力の泉", 1, 0), Skill("炎の心得", 15, 0) },
                Uniques = new[] { Skill("マナバースト", 5, 3), Skill("星降り", 20, 5) },
            };

        private static TrainingSkill Skill(string name, int level, int energy) =>
            new()
            {
                Name = name,
                Description = name + "の説明",
                UnlockLevel = level,
                Energy = energy,
            };

        private static PartyMember Member(string id, string name) => new() { Id = id, Name = name };

        private IReadOnlyList<TrainingMember> Roster() =>
            new[]
            {
                new TrainingMember(Member("toma", "トーマ"), data.Find("toma")),
                new TrainingMember(Member("luka", "ルカ"), data.Find("luka")),
                new TrainingMember(Member("aria", "アリア"), data.Find("aria")),
            };

        private TrainingPresenter Open() =>
            presenter = new TrainingPresenter(view, Roster(), store);

        [Test]
        public void CostsGrowWithTheLevel()
        {
            Assert.That(TrainingRules.CostToNext(12, 100), Is.EqualTo(1200));
            Assert.That(TrainingRules.CostBetween(12, 15, 100), Is.EqualTo(1200 + 1300 + 1400));
            Assert.That(TrainingRules.CostBetween(12, 12, 100), Is.EqualTo(0));
            Assert.That(TrainingRules.Affordable(12, 3900, 30, 100), Is.EqualTo(3));
            Assert.That(TrainingRules.Affordable(12, 3899, 30, 100), Is.EqualTo(2));
            Assert.That(TrainingRules.Affordable(12, 1199, 30, 100), Is.EqualTo(0));
            // 上限より上へは上げない。
            Assert.That(TrainingRules.Affordable(29, 1_000_000, 30, 100), Is.EqualTo(1));
        }

        [Test]
        public void RosterPutsThePartyFirstAndSkipsCharactersWithoutGrowth()
        {
            var formation = new PartyFormation(
                new[]
                {
                    Member("aria", "アリア"),
                    Member("nobody", "名無し"),
                    Member("luka", "ルカ"),
                    Member("toma", "トーマ"),
                },
                new[] { "toma", null, "luka" }
            );

            var roster = TrainingRoster.From(formation, data);

            Assert.That(
                roster.Select(member => member.Id),
                Is.EqualTo(new[] { "toma", "luka", "aria" })
            );
        }

        [Test]
        public void ShowsTheCharacterAtTheirLevel()
        {
            Open();
            var state = view.Last;

            Assert.That(state.Name, Is.EqualTo("トーマ"));
            Assert.That(state.Level, Is.EqualTo(12));
            Assert.That(state.Stats, Is.EqualTo(new TrainingStats(300, 90, 318, 110, 72)));
            Assert.That(state.Runes, Is.EqualTo(8450));
            Assert.That(state.NextCost, Is.EqualTo(1200));
            Assert.That(state.DialogOpen, Is.False);
            // パッシブ2つ、続けて固有スキル2つ。レベルが届いていないものは未解放。
            Assert.That(
                state.Skills.Select(skill => skill.Name),
                Is.EqualTo(new[] { "魔力の泉", "炎の心得", "マナバースト", "星降り" })
            );
            Assert.That(
                state.Skills.Select(skill => skill.Unlocked),
                Is.EqualTo(new[] { true, false, true, false })
            );
            Assert.That(state.Skills[2].Type, Is.EqualTo("固有スキル"));
            Assert.That(state.Skills[2].Energy, Is.EqualTo(3));
            // 付けているカードの名前とコストは、スキルの定義から引く。
            Assert.That(
                state.Cards.Select(card => card.Name),
                Is.EqualTo(new[] { "ファイア", "メテオ", "アイスランス", "ブリザード" })
            );
            Assert.That(state.Cards.Select(card => card.Cost), Is.EqualTo(new[] { 2, 9, 2, 5 }));
        }

        [Test]
        public void ArrowsWalkTheRosterAndRememberTheCharacter()
        {
            store.Selected = "luka";
            Open();
            Assert.That(view.Last.Name, Is.EqualTo("ルカ"));

            view.Next();
            Assert.That(view.Last.Name, Is.EqualTo("アリア"));
            Assert.That(store.Selected, Is.EqualTo("aria"));
            // 端から反対の端へ回る。
            view.Next();
            Assert.That(view.Last.Name, Is.EqualTo("トーマ"));
            view.Prev();
            Assert.That(view.Last.Name, Is.EqualTo("アリア"));
            Assert.That(view.Last.CanSwitch, Is.True);
        }

        [Test]
        public void TheDialogShowsWhatTheLevelsChangeBeforeSpending()
        {
            Open();
            view.LevelUp();
            var state = view.Last;

            Assert.That(state.DialogOpen, Is.True);
            Assert.That(state.CanSwitch, Is.False);
            Assert.That(state.Count, Is.EqualTo(1));
            Assert.That(state.Target, Is.EqualTo(13));
            Assert.That(state.Cost, Is.EqualTo(1200));
            Assert.That(state.Remaining, Is.EqualTo(7250));
            Assert.That(
                state.TargetStats - state.Stats,
                Is.EqualTo(new TrainingStats(18, 3, 18, 5, 2))
            );
            Assert.That(state.Learned, Is.Empty);
            Assert.That(state.NextUnlock.Name, Is.EqualTo("炎の心得"));
            Assert.That(state.CanLess, Is.False);
            Assert.That(state.CanConfirm, Is.True);
            Assert.That(store.Runes, Is.EqualTo(8450), "Opening the dialog spends nothing.");

            view.More();
            view.More();
            state = view.Last;
            Assert.That(state.Target, Is.EqualTo(15));
            Assert.That(state.Cost, Is.EqualTo(3900));
            Assert.That(
                state.Learned.Select(skill => skill.Name),
                Is.EqualTo(new[] { "炎の心得" })
            );
            Assert.That(state.NextUnlock.Name, Is.EqualTo("星降り"));
            view.Less();
            Assert.That(view.Last.Target, Is.EqualTo(14));
        }

        [Test]
        public void MaxBuysAsManyLevelsAsTheRunesAllow()
        {
            Open();
            view.LevelUp();
            view.Max();

            // 8,450ルーンでは 1,200＋1,300＋1,400＋1,500＋1,600 = 7,000（5レベル）まで。
            Assert.That(view.Last.Count, Is.EqualTo(5));
            Assert.That(view.Last.Target, Is.EqualTo(17));
            Assert.That(view.Last.CanConfirm, Is.True);

            view.More();
            Assert.That(view.Last.Count, Is.EqualTo(6));
            Assert.That(view.Last.CanAfford, Is.False);
            Assert.That(view.Last.CanConfirm, Is.False);
            Assert.That(view.Last.Remaining, Is.EqualTo(8450 - 8700));
        }

        [Test]
        public void ConfirmSpendsRunesRaisesTheLevelAndTellsWhatWasLearned()
        {
            Open();
            view.LevelUp();
            view.More();
            view.More();
            view.Confirm();

            Assert.That(store.Levels["toma"], Is.EqualTo(15));
            Assert.That(store.Runes, Is.EqualTo(8450 - 3900));
            Assert.That(view.Last.DialogOpen, Is.False);
            Assert.That(view.Last.Level, Is.EqualTo(15));
            Assert.That(view.Last.Skills[1].Unlocked, Is.True);
            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { "トーマが Lv 15 になり、「炎の心得」を覚えました" })
            );

            view.LevelUp();
            view.Confirm();
            Assert.That(view.Notices.Last(), Is.EqualTo("トーマが Lv 16 になりました"));
        }

        [Test]
        public void TheServerRaisesTheLevelBeforeTheDetailShowsIt()
        {
            var calls = new List<(string, int, int)>();
            var pending = new TaskCompletionSource<bool>();
            presenter = new TrainingPresenter(
                view,
                Roster(),
                store,
                levelUp: async (id, from, to, _) =>
                {
                    calls.Add((id, from, to));
                    await pending.Task;
                    // サーバーが上げたレベルと残高を、store が読み直す。
                    store.Levels[id] = to;
                    store.Runes = 8450 - 3900;
                }
            );
            view.LevelUp();
            view.More();
            view.More();
            view.Confirm();

            // サーバーが答えるまでは、重ねた画面を開いたまま、押し直しや数の変更を受け付けない。
            Assert.That(presenter.Saving, Is.True);
            Assert.That(view.Last.DialogOpen, Is.True);
            Assert.That(view.Last.CanConfirm, Is.False);
            Assert.That(view.Last.CanLess, Is.False);
            view.Confirm();
            view.Less();
            Assert.That(calls, Is.EqualTo(new[] { ("toma", 12, 15) }));
            Assert.That(view.Last.Count, Is.EqualTo(3));

            pending.SetResult(true);
            Assert.That(presenter.ConfirmTask.IsCompleted, Is.True);
            Assert.That(presenter.Saving, Is.False);
            Assert.That(view.Last.DialogOpen, Is.False);
            Assert.That(view.Last.Level, Is.EqualTo(15));
            Assert.That(view.Last.Runes, Is.EqualTo(8450 - 3900));
            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { "トーマが Lv 15 になり、「炎の心得」を覚えました" })
            );
        }

        [Test]
        public void AFailedLevelUpKeepsTheDialogAndSaysSo()
        {
            presenter = new TrainingPresenter(
                view,
                Roster(),
                store,
                levelUp: (_, _, _, _) => Task.FromException(new InvalidOperationException())
            );
            view.LevelUp();
            view.Confirm();

            Assert.That(presenter.ConfirmTask.IsCompleted, Is.True);
            Assert.That(presenter.Saving, Is.False);
            Assert.That(store.Levels["toma"], Is.EqualTo(12));
            Assert.That(store.Runes, Is.EqualTo(8450));
            Assert.That(view.Last.DialogOpen, Is.True);
            Assert.That(view.Last.CanConfirm, Is.True);
            Assert.That(view.Notices, Is.EqualTo(new[] { TrainingPresenter.LevelUpFailedMessage }));
        }

        [Test]
        public void TheStoresRulesSetTheCapAndTheCost()
        {
            store.MaxLevel = 14;
            store.CostPerLevel = 10;
            Open();
            Assert.That(view.Last.NextCost, Is.EqualTo(120));
            view.LevelUp();
            view.Max();
            Assert.That(view.Last.Target, Is.EqualTo(14));
            Assert.That(view.Last.Cost, Is.EqualTo(120 + 130));
        }

        [Test]
        public void ConfirmDoesNothingWithoutEnoughRunes()
        {
            store.Runes = 1199;
            Open();
            view.LevelUp();
            Assert.That(view.Last.CanConfirm, Is.False);
            view.Confirm();

            Assert.That(store.Levels["toma"], Is.EqualTo(12));
            Assert.That(store.Runes, Is.EqualTo(1199));
            Assert.That(view.Notices, Is.Empty);
            Assert.That(view.Last.DialogOpen, Is.True);
            // 1レベルも上げられないときも、最大は1のまま足りない量を見せる。
            view.Max();
            Assert.That(view.Last.Count, Is.EqualTo(1));
        }

        [Test]
        public void BackClosesTheDialogFirst()
        {
            Open();
            Assert.That(presenter.Back(), Is.False, "The detail leaves back to the guide.");

            view.LevelUp();
            Assert.That(presenter.Back(), Is.True);
            Assert.That(view.Last.DialogOpen, Is.False);
            Assert.That(presenter.Back(), Is.False);

            view.LevelUp();
            view.Cancel();
            Assert.That(view.Last.DialogOpen, Is.False);
            Assert.That(store.Runes, Is.EqualTo(8450));
        }

        [Test]
        public void TheLastLevelCannotBeRaised()
        {
            store.Levels["toma"] = 29;
            Open();
            view.LevelUp();
            view.More();
            Assert.That(view.Last.Count, Is.EqualTo(1), "Only one level is left.");
            view.Confirm();

            Assert.That(view.Last.Level, Is.EqualTo(30));
            Assert.That(view.Last.Maxed, Is.True);
            Assert.That(view.Last.NextCost, Is.EqualTo(0));
            view.LevelUp();
            Assert.That(view.Last.DialogOpen, Is.False);
        }

        [Test]
        public void CardsOpenTheCardScreenOrSayItIsComing()
        {
            Open();
            view.Cards();
            Assert.That(view.CardsOpened, Is.EqualTo(new[] { "toma" }));
            Assert.That(view.Notices, Is.EqualTo(new[] { TrainingPresenter.CardsComingSoon }));

            view.CardsReady = true;
            view.Cards();
            Assert.That(view.Notices.Count, Is.EqualTo(1));
        }

        [Test]
        public void PartyLevelsLastWhileTheAppRunsWithoutTouchingTheMockData()
        {
            var party = ScriptableObject.CreateInstance<PartyMockData>();
            try
            {
                party.Members = new[]
                {
                    new PartyMember { Id = "toma", Level = 12 },
                };
                PartySession.Reset();

                Assert.That(PartySession.LevelOf(party, "toma"), Is.EqualTo(12));
                PartySession.SetLevel("toma", 13);
                Assert.That(PartySession.LevelOf(party, "toma"), Is.EqualTo(13));
                Assert.That(party.Members[0].Level, Is.EqualTo(12));
                Assert.That(PartySession.LevelOf(party, "nobody"), Is.EqualTo(1));

                PartySession.Reset();
                Assert.That(PartySession.LevelOf(party, "toma"), Is.EqualTo(12));
            }
            finally
            {
                PartySession.Reset();
                UnityEngine.Object.DestroyImmediate(party);
            }
        }
    }
}
