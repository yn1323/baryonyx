using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Baryonyx.Party;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class PartyFormationTests
    {
        private static PartyMember[] Roster() =>
            new[]
            {
                new PartyMember { Id = "toma", Name = "トーマ" },
                new PartyMember { Id = "luka", Name = "ルカ" },
                new PartyMember { Id = "aria", Name = "アリア" },
                new PartyMember { Id = "mina", Name = "ミナ" },
                new PartyMember { Id = "anselm", Name = "アンセルム" },
                new PartyMember { Id = "greta", Name = "グレタ" },
            };

        private static PartyFormation Full() =>
            new(Roster(), new[] { "toma", "luka", "aria", "mina" });

        [Test]
        public void SlotsKeepOnlyOwnedCharactersOnce()
        {
            var formation = new PartyFormation(
                Roster(),
                new[] { "toma", "nobody", "toma", null, "luka" }
            );

            Assert.That(formation.SlotCount, Is.EqualTo(PartyFormation.Size));
            Assert.That(formation.Member(0), Is.EqualTo("toma"));
            // 持っていないキャラと、2つ目の枠に入った同じキャラは空きになる。5つ目の枠はない。
            Assert.That(formation.Member(1), Is.Null);
            Assert.That(formation.Member(2), Is.Null);
            Assert.That(formation.Member(3), Is.Null);
            Assert.That(formation.Count, Is.EqualTo(1));
            Assert.That(formation.SlotOf("luka"), Is.EqualTo(-1));
        }

        [Test]
        public void BenchListsOwnedCharactersNotInThePartyInRosterOrder()
        {
            var formation = Full();
            Assert.That(
                formation.Bench.Select(member => member.Id),
                Is.EqualTo(new[] { "anselm", "greta" })
            );
        }

        [Test]
        public void ChoosingACharacterReplacesOrFillsTheSlot()
        {
            var formation = Full();

            Assert.That(formation.Apply(1, "anselm"), Is.EqualTo(PartyChange.Replace));
            Assert.That(formation.Member(1), Is.EqualTo("anselm"));
            Assert.That(formation.Bench.Select(member => member.Id), Does.Contain("luka"));

            Assert.That(formation.Apply(1, null), Is.EqualTo(PartyChange.Leave));
            Assert.That(formation.Member(1), Is.Null);
            Assert.That(formation.Apply(1, "greta"), Is.EqualTo(PartyChange.Join));
            Assert.That(formation.Member(1), Is.EqualTo("greta"));
        }

        [Test]
        public void MembersAndUnknownCharactersAreNotChosenAgain()
        {
            var formation = Full();

            // 右にはパーティにいないキャラだけが並ぶため、パーティ内の並べ替えはしない。
            Assert.That(formation.Apply(0, "luka"), Is.EqualTo(PartyChange.None));
            Assert.That(formation.Apply(0, "toma"), Is.EqualTo(PartyChange.None));
            Assert.That(formation.Apply(0, "nobody"), Is.EqualTo(PartyChange.None));
            Assert.That(formation.Apply(4, "anselm"), Is.EqualTo(PartyChange.None));
            Assert.That(formation.Member(0), Is.EqualTo("toma"));
            Assert.That(formation.Member(1), Is.EqualTo("luka"));
        }

        [Test]
        public void TheLastMemberCannotLeave()
        {
            var formation = new PartyFormation(Roster(), new[] { null, "luka" });

            Assert.That(formation.Apply(0, null), Is.EqualTo(PartyChange.None));
            Assert.That(formation.Apply(1, null), Is.EqualTo(PartyChange.KeepOne));
            Assert.That(formation.Member(1), Is.EqualTo("luka"));
        }

        [Test]
        public void RosterOpensWithNothingChosenAndListsTheBench()
        {
            var view = new FakeView();
            using var presenter = new PartyRosterPresenter(view, Full());

            Assert.That(view.State.Party, Is.EqualTo(new[] { "toma", "luka", "aria", "mina" }));
            Assert.That(view.State.Bench, Is.EqualTo(new[] { "anselm", "greta" }));
            Assert.That(view.State.SelectedSlot, Is.EqualTo(-1));
            Assert.That(view.State.SelectedBench, Is.Null);
            Assert.That(view.State.CanRemove, Is.False);

            // 押したカードを選び、もう一度押すとやめる。別のパーティの枠を押すと選び直す。
            view.PressSlot(2);
            Assert.That(view.State.SelectedSlot, Is.EqualTo(2));
            Assert.That(view.State.CanRemove, Is.True);
            view.PressSlot(0);
            Assert.That(view.State.SelectedSlot, Is.EqualTo(0));
            view.PressSlot(0);
            Assert.That(view.State.SelectedSlot, Is.EqualTo(-1));
            view.PressBench("greta");
            Assert.That(view.State.SelectedBench, Is.EqualTo("greta"));
            view.PressBench("anselm");
            Assert.That(view.State.SelectedBench, Is.EqualTo("anselm"));
            view.PressBench("anselm");
            Assert.That(view.State.SelectedBench, Is.Null);
            Assert.That(view.Notices, Is.Empty);
        }

        [Test]
        public void APartySlotAndABenchCharacterSwapInEitherOrder()
        {
            var view = new FakeView();
            using var presenter = new PartyRosterPresenter(view, Full());

            // パーティの枠を選んでから一覧の人を押す。
            view.PressSlot(1);
            view.PressBench("anselm");
            Assert.That(view.State.Party[1], Is.EqualTo("anselm"));
            Assert.That(view.State.Bench, Is.EqualTo(new[] { "luka", "greta" }));
            // 枠を見ればわかるので知らせない。入れ替えたあとは何も選んでいない。
            Assert.That(view.Notices, Is.Empty);
            Assert.That(view.State.SelectedSlot, Is.EqualTo(-1));
            Assert.That(view.State.SelectedBench, Is.Null);

            // 一覧の人を選んでからパーティの枠を押す。
            view.PressBench("greta");
            view.PressSlot(3);
            Assert.That(view.State.Party[3], Is.EqualTo("greta"));
            Assert.That(view.Notices, Is.Empty);
            Assert.That(view.State.SelectedBench, Is.Null);
        }

        [Test]
        public void RemoveEmptiesTheChosenSlotWhichABenchCharacterThenFills()
        {
            var view = new FakeView();
            using var presenter = new PartyRosterPresenter(view, Full());

            // 枠を選んでいないと外せない。
            view.PressRemove();
            Assert.That(view.Notices.Last(), Is.EqualTo(PartyRosterPresenter.ChooseLeaverMessage));
            Assert.That(view.State.Party[1], Is.EqualTo("luka"));

            view.PressSlot(1);
            view.PressRemove();
            Assert.That(view.State.Party[1], Is.Null);
            Assert.That(view.State.Bench, Does.Contain("luka"));
            Assert.That(view.Notices.Last(), Is.EqualTo(PartyRosterPresenter.ChooseLeaverMessage));

            // 空いた枠も選べ、一覧の人を入れられる。空いた枠は外せない。
            view.PressSlot(1);
            Assert.That(view.State.CanRemove, Is.False);
            view.PressRemove();
            Assert.That(view.Notices.Last(), Is.EqualTo(PartyRosterPresenter.ChooseLeaverMessage));
            view.PressBench("greta");
            Assert.That(view.State.Party[1], Is.EqualTo("greta"));
            Assert.That(view.Notices.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheBandOpensTheTabsOfThePersonChosen()
        {
            var view = new FakeView();
            using var presenter = new PartyRosterPresenter(view, Full());

            // 選んでいなければ、帯は空いていて開かない。
            Assert.That(view.State.Chosen, Is.Null);
            view.PressDetail();
            Assert.That(view.Opened, Is.Empty);

            // パーティの枠の人も、一覧の人も、選ぶと帯に出て、帯のボタンでその人を開く。
            view.PressSlot(2);
            Assert.That(view.State.Chosen, Is.EqualTo("aria"));
            view.PressDetail();
            view.PressSlot(2);
            view.PressBench("greta");
            Assert.That(view.State.Chosen, Is.EqualTo("greta"));
            view.PressDetail();
            Assert.That(view.Opened, Is.EqualTo(new[] { "aria", "greta" }));
            // 開いても選び方は変えない。
            Assert.That(view.State.SelectedBench, Is.EqualTo("greta"));
        }

        [Test]
        public void TheLastMemberCannotLeaveTheParty()
        {
            var view = new FakeView();
            int calls = 0;
            using var presenter = new PartyRosterPresenter(
                view,
                new PartyFormation(Roster(), new[] { "toma" }),
                save: (_, _) =>
                {
                    calls++;
                    return Task.FromResult<PartyFormation>(null);
                }
            );

            view.PressSlot(0);
            Assert.That(view.State.CanRemove, Is.False);
            view.PressRemove();
            Assert.That(calls, Is.EqualTo(0));
            Assert.That(view.State.Party[0], Is.EqualTo("toma"));
            Assert.That(view.Notices.Last(), Is.EqualTo(PartyRosterPresenter.KeepOneMessage));
        }

        [Test]
        public void AChangeShowsAtOnceAndTheSavedFormationAfter()
        {
            var view = new FakeView();
            var saved = new PartyFormation(Roster(), new[] { "toma", "anselm", "greta", "mina" });
            var calls = new List<(int, string)>();
            var pending = new TaskCompletionSource<PartyFormation>();
            using var presenter = new PartyRosterPresenter(
                view,
                Full(),
                (slot, id) =>
                {
                    calls.Add((slot, id));
                    return pending.Task;
                }
            );

            view.PressBench("anselm");
            view.PressSlot(1);
            // 保存を待たずに枠を変え、続けて押しても受け付ける。
            Assert.That(view.State.Party[1], Is.EqualTo("anselm"));
            Assert.That(view.State.SelectedBench, Is.Null);
            Assert.That(presenter.ChangeTask.IsCompleted, Is.False);
            view.PressSlot(2);
            view.PressBench("greta");
            Assert.That(view.State.Party[2], Is.EqualTo("greta"));
            view.PressSlot(0);
            view.PressDetail();
            Assert.That(calls, Is.EqualTo(new[] { (1, "anselm"), (2, "greta") }));
            Assert.That(view.Opened, Is.EqualTo(new[] { "toma" }));

            // 保存が済んだら、保存した編成で描き直す。
            pending.SetResult(saved);
            Assert.That(presenter.ChangeTask.IsCompleted, Is.True);
            Assert.That(presenter.Formation, Is.SameAs(saved));
            Assert.That(view.State.Party, Is.EqualTo(new[] { "toma", "anselm", "greta", "mina" }));
            Assert.That(view.Notices, Is.Empty);

            // 外す操作は、キャラのIDの代わりにnullを渡す。
            view.PressSlot(1);
            view.PressRemove();
            Assert.That(calls.Last(), Is.EqualTo((1, (string)null)));
        }

        [Test]
        public void AFailedSaveShowsTheFormationPutBack()
        {
            var view = new FakeView();
            // 保存できなかったときは、戻した編成が返る（知らせるのは SaveQueue）。
            var back = Full();
            using var presenter = new PartyRosterPresenter(
                view,
                Full(),
                (_, _) => Task.FromResult(back)
            );

            view.PressBench("anselm");
            view.PressSlot(1);
            Assert.That(presenter.ChangeTask.IsCompleted, Is.True);
            Assert.That(presenter.Formation, Is.SameAs(back));
            Assert.That(view.State.Party[1], Is.EqualTo("luka"));
            Assert.That(view.Notices, Is.Empty);
        }

        [Test]
        public void TheServersPartyUsesTheMockDataForNamesAndArt()
        {
            var data = UnityEngine.ScriptableObject.CreateInstance<PartyMockData>();
            try
            {
                data.Members = Roster();
                var state = new PartyState(
                    new[]
                    {
                        new PartyCharacterState("greta", 7, new[] { "Ice" }),
                        new PartyCharacterState("nobody", 3, Array.Empty<string>()),
                        new PartyCharacterState("toma", 12, new[] { "Fire" }),
                    },
                    new[] { null, "toma", null, null },
                    500,
                    30,
                    100
                );
                var formation = PartyFormation.From(data, state);

                // サーバーの持っている順に並べ、仮データにないキャラは外す。
                Assert.That(
                    formation.Roster.Select(member => member.Id),
                    Is.EqualTo(new[] { "greta", "toma" })
                );
                Assert.That(formation.Member(1), Is.EqualTo("toma"));
                Assert.That(formation.Name("toma"), Is.EqualTo("トーマ"));
                Assert.That(
                    formation.Bench.Select(member => member.Id),
                    Is.EqualTo(new[] { "greta" })
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void TheSessionReadsTheServersLevelsAndCardsOnceUsed()
        {
            var data = UnityEngine.ScriptableObject.CreateInstance<PartyMockData>();
            try
            {
                data.Members = new[]
                {
                    new PartyMember
                    {
                        Id = "toma",
                        Level = 12,
                        Cards = new[] { new PartyCard { Skill = "Fire" } },
                    },
                };
                PartySession.Reset();
                PartySession.SetLevel("toma", 20);
                Assert.That(PartySession.LevelOf(data, "toma"), Is.EqualTo(20));
                // 仮データそのものは書き換えない。
                Assert.That(data.Members[0].Level, Is.EqualTo(12));

                PartySession.Use(
                    data,
                    new PartyState(
                        new[] { new PartyCharacterState("toma", 14, new[] { "Ice", null }) },
                        new[] { "toma", null, null, null },
                        0,
                        30,
                        100
                    )
                );
                Assert.That(PartySession.LevelOf(data, "toma"), Is.EqualTo(14));
                Assert.That(PartySession.CardsOf(data, "toma"), Is.EqualTo(new[] { "Ice", null }));
                Assert.That(PartySession.LevelOf(data, "nobody"), Is.EqualTo(1));
                Assert.That(PartySession.Formation(data).Member(0), Is.EqualTo("toma"));

                PartySession.Reset();
                Assert.That(PartySession.State, Is.Null);
                Assert.That(PartySession.LevelOf(data, "toma"), Is.EqualTo(12));
            }
            finally
            {
                PartySession.Reset();
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void DisposedPresenterIgnoresTheView()
        {
            var view = new FakeView();
            var presenter = new PartyRosterPresenter(view, Full());
            presenter.Dispose();

            view.PressSlot(2);
            view.PressBench("greta");
            view.PressRemove();
            view.PressDetail();
            Assert.That(view.State.SelectedSlot, Is.EqualTo(-1));
            Assert.That(view.Notices, Is.Empty);
            Assert.That(view.Opened, Is.Empty);
        }

        private sealed class FakeView : IPartyRosterView
        {
            public event Action<int> SlotPressed;
            public event Action<string> BenchPressed;
            public event Action RemovePressed;
            public event Action DetailPressed;

            public PartyRosterState State { get; private set; }
            public List<string> Notices { get; } = new();
            public List<string> Opened { get; } = new();

            public void Render(PartyRosterState state) => State = state;

            public void ShowNotice(string message) => Notices.Add(message);

            public void OpenDetail(string id) => Opened.Add(id);

            public void PressSlot(int index) => SlotPressed?.Invoke(index);

            public void PressBench(string id) => BenchPressed?.Invoke(id);

            public void PressRemove() => RemovePressed?.Invoke();

            public void PressDetail() => DetailPressed?.Invoke();
        }
    }
}
