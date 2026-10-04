using System;
using System.Collections.Generic;
using System.Linq;
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
