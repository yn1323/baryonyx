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
        public void PresenterChangesTheChosenSlotAndTellsWhatChanged()
        {
            var view = new FakeView();
            using var presenter = new PartyFormationPresenter(view, Full());

            // 最初は1つ目の枠を選んでいる。
            Assert.That(view.State.SelectedSlot, Is.EqualTo(0));
            Assert.That(view.State.Slots[0].Selected, Is.True);
            Assert.That(view.State.Bench, Is.EqualTo(new[] { "anselm", "greta" }));
            Assert.That(view.State.OwnedText, Is.EqualTo("所持 6人"));
            Assert.That(view.State.CanLeave, Is.True);

            view.PressSlot(1);
            Assert.That(view.State.Slots[1].Selected, Is.True);
            Assert.That(view.State.Slots[0].Selected, Is.False);

            view.PressMember("anselm");
            Assert.That(view.State.Slots[1].Member, Is.EqualTo("anselm"));
            Assert.That(view.State.Bench, Is.EqualTo(new[] { "luka", "greta" }));
            Assert.That(view.Notices.Last(), Is.EqualTo("ルカとアンセルムを入れ替えました"));

            view.PressLeave();
            Assert.That(view.State.Slots[1].Member, Is.Null);
            Assert.That(view.State.CanLeave, Is.False);
            Assert.That(view.Notices.Last(), Is.EqualTo("アンセルムを外しました"));

            view.PressMember("greta");
            Assert.That(view.State.Slots[1].Member, Is.EqualTo("greta"));
            Assert.That(view.Notices.Last(), Is.EqualTo("グレタを編成しました"));

            // 変わらない操作は、通知も描き直しもしない。
            int renders = view.Renders;
            int notices = view.Notices.Count;
            view.PressMember("toma");
            Assert.That(view.Renders, Is.EqualTo(renders));
            Assert.That(view.Notices.Count, Is.EqualTo(notices));
        }

        [Test]
        public void PresenterKeepsTheLastMember()
        {
            var view = new FakeView();
            using var presenter = new PartyFormationPresenter(
                view,
                new PartyFormation(Roster(), new[] { "toma" })
            );

            view.PressLeave();
            Assert.That(view.State.Slots[0].Member, Is.EqualTo("toma"));
            Assert.That(view.Notices.Last(), Is.EqualTo(PartyFormationPresenter.KeepOneMessage));
        }

        [Test]
        public void AChangeIsShownAfterTheServerSavesIt()
        {
            var view = new FakeView();
            var saved = new PartyFormation(Roster(), new[] { "toma", "anselm", "aria", "mina" });
            var calls = new List<(int, string)>();
            var pending = new TaskCompletionSource<PartyFormation>();
            using var presenter = new PartyFormationPresenter(
                view,
                Full(),
                (slot, id, _) =>
                {
                    calls.Add((slot, id));
                    return pending.Task;
                }
            );

            view.PressSlot(1);
            view.PressMember("anselm");
            Assert.That(presenter.Saving, Is.True);
            // サーバーが答えるまで枠は変えず、続けて押しても受け付けない。
            Assert.That(view.State.Slots[1].Member, Is.EqualTo("luka"));
            view.PressMember("greta");
            view.PressLeave();
            Assert.That(calls, Is.EqualTo(new[] { (1, "anselm") }));

            pending.SetResult(saved);
            Assert.That(presenter.ChangeTask.IsCompleted, Is.True);
            Assert.That(presenter.Saving, Is.False);
            Assert.That(presenter.Formation, Is.SameAs(saved));
            Assert.That(view.State.Slots[1].Member, Is.EqualTo("anselm"));
            Assert.That(view.Notices, Is.EqualTo(new[] { "ルカとアンセルムを入れ替えました" }));

            // 外す操作は、キャラのIDの代わりにnullを渡す。
            presenter.Leave();
            Assert.That(calls.Last(), Is.EqualTo((1, (string)null)));
        }

        [Test]
        public void AFailedSaveKeepsTheFormationAndSaysSo()
        {
            var view = new FakeView();
            using var presenter = new PartyFormationPresenter(
                view,
                Full(),
                (_, _, _) => Task.FromException<PartyFormation>(new InvalidOperationException())
            );

            view.PressSlot(1);
            view.PressMember("anselm");
            Assert.That(presenter.ChangeTask.IsCompleted, Is.True);
            Assert.That(presenter.Saving, Is.False);
            Assert.That(view.State.Slots[1].Member, Is.EqualTo("luka"));
            Assert.That(
                view.Notices,
                Is.EqualTo(new[] { PartyFormationPresenter.SaveFailedMessage })
            );
        }

        [Test]
        public void TheLastMemberStaysWithoutAskingTheServer()
        {
            var view = new FakeView();
            int calls = 0;
            using var presenter = new PartyFormationPresenter(
                view,
                new PartyFormation(Roster(), new[] { "toma" }),
                (_, _, _) =>
                {
                    calls++;
                    return Task.FromResult<PartyFormation>(null);
                }
            );

            view.PressLeave();
            Assert.That(calls, Is.EqualTo(0));
            Assert.That(view.Notices.Last(), Is.EqualTo(PartyFormationPresenter.KeepOneMessage));
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
            var presenter = new PartyFormationPresenter(view, Full());
            presenter.Dispose();

            view.PressSlot(2);
            view.PressMember("anselm");
            Assert.That(view.State.SelectedSlot, Is.EqualTo(0));
            Assert.That(view.Notices, Is.Empty);
        }

        private sealed class FakeView : IPartyFormationView
        {
            public event Action<int> SlotPressed;
            public event Action<string> MemberPressed;
            public event Action LeavePressed;

            public PartyFormationState State { get; private set; }
            public int Renders { get; private set; }
            public List<string> Notices { get; } = new();

            public void Render(PartyFormationState state)
            {
                State = state;
                Renders++;
            }

            public void ShowNotice(string message) => Notices.Add(message);

            public void PressSlot(int index) => SlotPressed?.Invoke(index);

            public void PressMember(string id) => MemberPressed?.Invoke(id);

            public void PressLeave() => LeavePressed?.Invoke();
        }
    }
}
