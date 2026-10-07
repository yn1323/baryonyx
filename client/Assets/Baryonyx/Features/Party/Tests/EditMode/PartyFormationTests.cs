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
        public void RosterChoosesThePartysFirstAndListsTheBench()
        {
            var view = new FakeView();
            using var presenter = new PartyRosterPresenter(view, Full());

            Assert.That(view.State.Party, Is.EqualTo(new[] { "toma", "luka", "aria", "mina" }));
            Assert.That(view.State.Bench, Is.EqualTo(new[] { "anselm", "greta" }));
            Assert.That(view.State.OwnedText, Is.EqualTo("所持 6人"));
            // 最初はパーティの先頭を選び、ボタンは「パーティから外す」。
            Assert.That(view.State.Selected, Is.EqualTo("toma"));
            Assert.That(view.State.Action, Is.EqualTo(PartyRosterAction.Leave));
            Assert.That(view.State.ActionLabel, Is.EqualTo(PartyRosterPresenter.LeaveLabel));
            Assert.That(view.State.CanAct, Is.True);
            Assert.That(view.State.CanOpen, Is.True);

            // 押した人を選ぶ。控えの人のボタンは「パーティに入れる」。
            view.PressSlot(2);
            Assert.That(view.State.Selected, Is.EqualTo("aria"));
            view.PressBench("greta");
            Assert.That(view.State.Selected, Is.EqualTo("greta"));
            Assert.That(view.State.ActionLabel, Is.EqualTo(PartyRosterPresenter.JoinLabel));
            Assert.That(view.Notices, Is.Empty);

            // 選んだ人の個別の画面を開く。
            view.PressOpen();
            Assert.That(view.Opened, Is.EqualTo(new[] { "greta" }));
        }

        [Test]
        public void ABenchCharacterSwapsWithThePartySlotChosenNext()
        {
            var view = new FakeView();
            string remembered = null;
            using var presenter = new PartyRosterPresenter(
                view,
                Full(),
                remember: id => remembered = id
            );

            view.PressBench("anselm");
            Assert.That(remembered, Is.EqualTo("anselm"));
            // パーティに空きがないので、入れ替える相手を選び始める。
            view.PressAction();
            Assert.That(view.State.Swapping, Is.True);
            Assert.That(view.State.Action, Is.EqualTo(PartyRosterAction.Cancel));
            Assert.That(view.State.ActionLabel, Is.EqualTo(PartyRosterPresenter.CancelLabel));
            Assert.That(view.State.CanOpen, Is.False);
            view.PressOpen();
            Assert.That(view.Opened, Is.Empty);

            view.PressSlot(1);
            Assert.That(view.State.Swapping, Is.False);
            Assert.That(view.State.Party[1], Is.EqualTo("anselm"));
            Assert.That(view.State.Bench, Is.EqualTo(new[] { "luka", "greta" }));
            Assert.That(view.State.Selected, Is.EqualTo("anselm"));
            Assert.That(view.Notices.Last(), Is.EqualTo("ルカとアンセルムを入れ替えました"));

            // 「やめる」と、控えを押し直したときは、入れ替えをやめる。
            view.PressBench("greta");
            view.PressAction();
            view.PressAction();
            Assert.That(view.State.Swapping, Is.False);
            view.PressAction();
            view.PressBench("luka");
            Assert.That(view.State.Swapping, Is.False);
            Assert.That(view.State.Selected, Is.EqualTo("luka"));
        }

        [Test]
        public void MembersLeaveAndBenchCharactersJoinTheEmptySlot()
        {
            var view = new FakeView();
            using var presenter = new PartyRosterPresenter(view, Full(), "luka");

            view.PressAction();
            Assert.That(view.State.Party[1], Is.Null);
            Assert.That(view.Notices.Last(), Is.EqualTo("ルカを外しました"));
            // 外した人は選んだまま控えに移り、空いた枠へ入れ直せる。
            Assert.That(view.State.Selected, Is.EqualTo("luka"));
            Assert.That(view.State.Action, Is.EqualTo(PartyRosterAction.Join));

            view.PressBench("greta");
            view.PressAction();
            Assert.That(view.State.Party[1], Is.EqualTo("greta"));
            Assert.That(view.Notices.Last(), Is.EqualTo("グレタを編成しました"));

            // 空いた枠を押すと、選んでいる控えの人を入れる。
            view.PressSlot(3);
            view.PressAction();
            Assert.That(view.State.Party[3], Is.Null);
            view.PressSlot(3);
            Assert.That(view.State.Party[3], Is.EqualTo("mina"));
        }

        [Test]
        public void TheLastMemberCannotLeaveTheParty()
        {
            var view = new FakeView();
            int calls = 0;
            using var presenter = new PartyRosterPresenter(
                view,
                new PartyFormation(Roster(), new[] { "toma" }),
                save: (_, _, _) =>
                {
                    calls++;
                    return Task.FromResult<PartyFormation>(null);
                }
            );

            Assert.That(view.State.CanAct, Is.False);
            view.PressAction();
            Assert.That(calls, Is.EqualTo(0));
            Assert.That(view.State.Party[0], Is.EqualTo("toma"));
            Assert.That(view.Notices.Last(), Is.EqualTo(PartyRosterPresenter.KeepOneMessage));
        }

        [Test]
        public void AChangeIsShownAfterTheServerSavesIt()
        {
            var view = new FakeView();
            var saved = new PartyFormation(Roster(), new[] { "toma", "anselm", "aria", "mina" });
            var calls = new List<(int, string)>();
            var pending = new TaskCompletionSource<PartyFormation>();
            using var presenter = new PartyRosterPresenter(
                view,
                Full(),
                "anselm",
                (slot, id, _) =>
                {
                    calls.Add((slot, id));
                    return pending.Task;
                }
            );

            view.PressAction();
            view.PressSlot(1);
            Assert.That(presenter.Saving, Is.True);
            // サーバーが答えるまで枠は変えず、続けて押しても受け付けない。
            Assert.That(view.State.Party[1], Is.EqualTo("luka"));
            Assert.That(view.State.CanAct, Is.False);
            view.PressSlot(2);
            view.PressAction();
            Assert.That(calls, Is.EqualTo(new[] { (1, "anselm") }));

            pending.SetResult(saved);
            Assert.That(presenter.ChangeTask.IsCompleted, Is.True);
            Assert.That(presenter.Saving, Is.False);
            Assert.That(presenter.Formation, Is.SameAs(saved));
            Assert.That(view.State.Party[1], Is.EqualTo("anselm"));
            Assert.That(view.Notices, Is.EqualTo(new[] { "ルカとアンセルムを入れ替えました" }));

            // 外す操作は、キャラのIDの代わりにnullを渡す。
            view.PressAction();
            Assert.That(calls.Last(), Is.EqualTo((1, (string)null)));
        }

        [Test]
        public void AFailedSaveKeepsTheFormationAndSaysSo()
        {
            var view = new FakeView();
            using var presenter = new PartyRosterPresenter(
                view,
                Full(),
                "anselm",
                (_, _, _) => Task.FromException<PartyFormation>(new InvalidOperationException())
            );

            view.PressAction();
            view.PressSlot(1);
            Assert.That(presenter.ChangeTask.IsCompleted, Is.True);
            Assert.That(presenter.Saving, Is.False);
            Assert.That(view.State.Party[1], Is.EqualTo("luka"));
            Assert.That(view.Notices, Is.EqualTo(new[] { PartyRosterPresenter.SaveFailedMessage }));
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
            var presenter = new PartyRosterPresenter(view, Full());
            presenter.Dispose();

            view.PressSlot(2);
            view.PressAction();
            view.PressOpen();
            Assert.That(view.State.Selected, Is.EqualTo("toma"));
            Assert.That(view.Notices, Is.Empty);
            Assert.That(view.Opened, Is.Empty);
        }

        private sealed class FakeView : IPartyRosterView
        {
            public event Action<int> SlotPressed;
            public event Action<string> BenchPressed;
            public event Action ActionPressed;
            public event Action OpenPressed;

            public PartyRosterState State { get; private set; }
            public List<string> Notices { get; } = new();
            public List<string> Opened { get; } = new();

            public void Render(PartyRosterState state) => State = state;

            public void ShowNotice(string message) => Notices.Add(message);

            public void OpenDetail(string id) => Opened.Add(id);

            public void PressSlot(int index) => SlotPressed?.Invoke(index);

            public void PressBench(string id) => BenchPressed?.Invoke(id);

            public void PressAction() => ActionPressed?.Invoke();

            public void PressOpen() => OpenPressed?.Invoke();
        }
    }
}
