using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Equipment;
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
            public event Action<int> GearPressed;
            public event Action<int> CardPressed;
            public event Action<int> UniquePressed;

            public TrainingState Last;
            public readonly List<string> Notices = new();
            public readonly List<string> Opened = new();

            public void Render(TrainingState state) => Last = state;

            public void ShowNotice(string message) => Notices.Add(message);

            public void OpenGear(string id, int slot) => Opened.Add($"gear {id} {slot}");

            public void OpenCards(string id, int slot) => Opened.Add($"cards {id} {slot}");

            public void OpenUnique(string id, int index) => Opened.Add($"unique {id} {index}");

            public void Prev() => PrevPressed?.Invoke();

            public void Next() => NextPressed?.Invoke();

            public void LevelUp() => LevelUpPressed?.Invoke();

            public void Less() => LessPressed?.Invoke();

            public void More() => MorePressed?.Invoke();

            public void Max() => MaxPressed?.Invoke();

            public void Cancel() => CancelPressed?.Invoke();

            public void Confirm() => ConfirmPressed?.Invoke();

            public void Gear(int slot) => GearPressed?.Invoke(slot);

            public void Card(int slot) => CardPressed?.Invoke(slot);

            public void Unique(int index) => UniquePressed?.Invoke(index);
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
                Cards = { ["toma"] = new[] { "Fire", "Ice" } },
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
                Level100 = new CharacterStats(2300, 700, 850, 2450, 1700, 560, 300, 500),
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

        // スキルの説明は、キャラのIDとスキルのIDを並べた文にして、どのキャラの数字で作ったかを確かめる。
        private TrainingPresenter Open() =>
            presenter = new TrainingPresenter(
                view,
                Roster(),
                store,
                describe: (id, card) => $"{id}:{card.Id}"
            );

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
            // Lv 100 の値に、Lv 12 の成長率（約12.9%）を掛けて四捨五入した値。
            Assert.That(
                state.Stats,
                Is.EqualTo(new CharacterStats(297, 90, 110, 316, 220, 72, 39, 65))
            );
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
            Assert.That(
                state.Passives.Select(skill => skill.Name),
                Is.EqualTo(new[] { "魔力の泉", "炎の心得" })
            );

            // デッキの4枚：固有スキル2枚（エネルギーをコストに、未解放は解放するLv）と、カスタムスキル2枚。
            Assert.That(
                state.Uniques.Select(card => card.Name),
                Is.EqualTo(new[] { "マナバースト", "星降り" })
            );
            Assert.That(state.Uniques.Select(card => card.Cost), Is.EqualTo(new[] { 3, 5 }));
            Assert.That(
                state.Uniques.Select(card => card.Locked),
                Is.EqualTo(new[] { false, true })
            );
            Assert.That(state.Uniques[1].UnlockLevel, Is.EqualTo(20));
            Assert.That(state.Uniques[0].Type, Is.EqualTo(TrainingCardState.UniqueType));
            Assert.That(state.Uniques[0].Element, Is.EqualTo(CardElement.None));
            // カスタムスキルの名前・コスト・属性は、スキルの定義から引き、説明はそのキャラで作る。
            Assert.That(
                state.Cards.Select(card => card.Name),
                Is.EqualTo(new[] { "ファイア", "アイスランス" })
            );
            Assert.That(state.Cards.Select(card => card.Cost), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(
                state.Cards.Select(card => card.Element),
                Is.EqualTo(new[] { CardElement.Fire, CardElement.Ice })
            );
            Assert.That(state.Cards[0].Type, Is.EqualTo(TrainingCardState.SkillType));
            Assert.That(state.Cards[0].Description, Is.EqualTo("toma:Fire"));
            Assert.That(state.Cards[0].Kind, Is.Not.Empty);
        }

        [Test]
        public void AnEmptyCustomSlotShowsAsEmpty()
        {
            store.Cards["toma"] = new[] { "Fire" };
            Open();

            // カスタムスキルはいつも2枠。足りない枠は「空き」。
            Assert.That(view.Last.Cards.Count, Is.EqualTo(TrainingPresenter.CustomSlots));
            Assert.That(view.Last.Cards[1].Filled, Is.False);
            Assert.That(view.Last.Cards[1].Name, Is.EqualTo(TrainingPresenter.EmptyName));
        }

        [Test]
        public void FlicksWalkTheRosterAndRememberTheCharacter()
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
            Assert.That(
                state.TargetStats - state.Stats,
                Is.EqualTo(new CharacterStats(7, 3, 2, 8, 5, 2, 1, 1))
            );
            Assert.That(state.Learned, Is.Empty);
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
            Assert.That(view.Last.Cost, Is.EqualTo(8700));
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
        public void ALevelUpShowsAtOnceAndTheServersLevelAfter()
        {
            var calls = new List<(string, int, int)>();
            var pending = new TaskCompletionSource<bool>();
            presenter = new TrainingPresenter(
                view,
                Roster(),
                store,
                levelUp: (id, from, to) =>
                {
                    calls.Add((id, from, to));
                    // レベルアップする関数は、すぐに store へ当てる（PartySession と同じ）。
                    store.Levels[id] = to;
                    store.Runes = 8450 - 3900;
                    return SavedAsync(id);
                }
            );

            async Task SavedAsync(string id)
            {
                await pending.Task;
                // サーバーが上げたレベルと残高を、store が読み直す。
                store.Levels[id] = 15;
                store.Runes = 8450 - 3900;
            }

            view.LevelUp();
            view.More();
            view.More();
            view.Confirm();

            // 保存を待たずに重ねた画面を閉じ、上げたレベルと残りのルーンを出して知らせる。
            Assert.That(calls, Is.EqualTo(new[] { ("toma", 12, 15) }));
            Assert.That(presenter.ConfirmTask.IsCompleted, Is.False);
            Assert.That(view.Last.DialogOpen, Is.False);
            Assert.That(view.Last.Level, Is.EqualTo(15));
            Assert.That(view.Last.Runes, Is.EqualTo(8450 - 3900));
            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { "トーマが Lv 15 になり、「炎の心得」を覚えました" })
            );
            // 保存を待つ間も、続けてレベルアップできる。
            view.LevelUp();
            Assert.That(view.Last.DialogOpen, Is.True);
            Assert.That(view.Last.Level, Is.EqualTo(15));

            pending.SetResult(true);
            Assert.That(presenter.ConfirmTask.IsCompleted, Is.True);
            Assert.That(view.Last.Level, Is.EqualTo(15));
        }

        [Test]
        public void AFailedLevelUpShowsTheLevelPutBack()
        {
            presenter = new TrainingPresenter(
                view,
                Roster(),
                store,
                // すぐに当てたあと、保存できずに戻す（知らせるのは SaveQueue）。
                levelUp: (_, _, _) => Task.CompletedTask
            );
            view.LevelUp();
            view.Confirm();

            Assert.That(presenter.ConfirmTask.IsCompleted, Is.True);
            Assert.That(store.Levels["toma"], Is.EqualTo(12));
            Assert.That(store.Runes, Is.EqualTo(8450));
            Assert.That(view.Last.Level, Is.EqualTo(12));
            Assert.That(view.Last.Runes, Is.EqualTo(8450));
            Assert.That(view.Notices, Is.EqualTo(new[] { "トーマが Lv 13 になりました" }));
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
        public void SlotsOpenTheirChangeScreensForTheCharacterOnScreen()
        {
            Open();
            view.Gear(1);
            view.Card(1);
            view.Next();
            view.Gear(0);
            // 開いていないアクセサリーの枠も、付け替えの画面を開く。
            view.Gear(3);
            // 固有スキルは、未解放のものも、進化ツリーを出した付け替えの画面を開く。
            view.Unique(1);
            Assert.That(
                view.Opened,
                Is.EqualTo(
                    new[]
                    {
                        "gear toma 1",
                        "cards toma 1",
                        "gear luka 0",
                        "gear luka 3",
                        "unique luka 1",
                    }
                )
            );

            // 枠の外は開かない。重ねた画面を開いている間も開かない。
            view.Gear(5);
            view.Gear(-1);
            view.Unique(2);
            view.LevelUp();
            view.Gear(0);
            view.Card(0);
            view.Unique(0);
            Assert.That(view.Opened.Count, Is.EqualTo(5));
            Assert.That(view.Notices, Is.Empty);
        }

        [Test]
        public void ShowsTheEquipmentAndTheAccessorySlotsThatAreNotReady()
        {
            presenter = new TrainingPresenter(
                view,
                Roster(),
                store,
                gearOf: id =>
                    id == "toma"
                        ? new[]
                        {
                            TrainingGearState.Worn("武器", "樫の杖", "★"),
                            TrainingGearState.Worn("防具", null, null),
                        }
                        : Array.Empty<TrainingGearState>()
            );

            var gear = view.Last.Gear;
            Assert.That(gear.Count, Is.EqualTo(TrainingPresenter.GearSlots));
            Assert.That(
                gear.Select(slot => slot.Slot),
                Is.EqualTo(new[] { "武器", "防具", "アクセサリー", "アクセサリー", "アクセサリー" })
            );
            Assert.That(gear[0].Filled, Is.True);
            Assert.That(gear[0].Name, Is.EqualTo("樫の杖"));
            Assert.That(gear[1].Filled, Is.False);
            // アクセサリーは、Lvで開くまで解放するLvを出す。
            Assert.That(
                gear.Select(slot => slot.Locked),
                Is.EqualTo(new[] { false, false, true, true, true })
            );
            Assert.That(gear[2].UnlockText, Is.EqualTo(AccessorySlots.UnlockText(0)));
            Assert.That(gear[0].UnlockText, Is.Empty);

            // 装備がわからない人は、武器と防具を空きとして出す。
            view.Next();
            Assert.That(
                view.Last.Gear.Take(2).Select(slot => slot.Filled),
                Is.EqualTo(new[] { false, false })
            );
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
