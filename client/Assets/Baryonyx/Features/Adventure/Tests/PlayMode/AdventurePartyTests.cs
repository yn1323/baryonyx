using System.Collections;
using System.Linq;
using System.Threading;
using Baryonyx.Adventure;
using Baryonyx.App;
using Baryonyx.Combat.Presentation;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Training;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // Home・探索の地図・冒険の戦闘に、今の編成（doc/features/party.md）が出る。
    // 編成はサーバーの代役（FakePartySource）から読み、3つ目の枠は空けておく。
    public sealed class AdventurePartyTests
    {
        private TestGameServices services;
        private FakePartySource server;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            PartySession.Reset();
            UniqueSkillSession.Reset();
            // アンセルム（Lv 8）・トーマ（Lv 20）・空き・グレタ（Lv 3）。
            server = new FakePartySource(0, "anselm", "toma", null, "greta")
                .With("toma", 20, "Fire", "Ice")
                .With("luka", 11, "VitalThrust", "Thunder")
                .With("anselm", 8, "EarthSplitter", "Fire")
                .With("greta", 3, "VitalThrust", "Ice");
            PartySession.Source = server;
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            services?.Dispose();
            services = null;
            PartySession.Reset();
            UniqueSkillSession.Reset();
            yield return SceneTests.UnloadAll(nameof(AdventurePartyTests));
        }

        private static readonly string[] Slots = { "anselm", "toma", null, "greta" };

        private AdventureState StartAdventure() =>
            AdventureSession.Use(
                services
                    .Adventure.StartAsync(AdventureCatalog.ForestRuins, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
            );

        // 焚き火を囲む4人は、編成の枠の順に、その人の戦闘の絵と色で立つ。空いた枠には誰も立たない。
        [UnityTest]
        public IEnumerator HomeStandsTheFormationAroundTheCampfire()
        {
            var bootstrap = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => bootstrap = value);
            yield return SceneTests.WaitUntil(
                () => bootstrap.PartyTask.IsCompleted,
                message: "The party did not load."
            );
            var view = bootstrap.View;
            var data = bootstrap.Party;
            Assert.That(view.PartyMembers, Has.Length.EqualTo(PartyFormation.Size));
            for (int slot = 0; slot < Slots.Length; slot++)
            {
                var picture = view.PartyMembers[slot];
                var shadow = view.PartyShadows[slot];
                if (Slots[slot] == null)
                {
                    Assert.That(picture.gameObject.activeSelf, Is.False, $"slot {slot}");
                    Assert.That(shadow.gameObject.activeSelf, Is.False, $"slot {slot}");
                    continue;
                }
                var member = data.Find(Slots[slot]);
                Assert.That(picture.gameObject.activeSelf, Is.True, member.Id);
                Assert.That(shadow.gameObject.activeSelf, Is.True, member.Id);
                Assert.That(picture.texture, Is.SameAs(member.Art), member.Id);
                Assert.That(picture.color, Is.EqualTo(member.Tint), member.Id);
            }
            // 右の2人は焚き火を向く。
            Assert.That(view.PartyMembers[1].rectTransform.localScale.x, Is.LessThan(0f));
            Assert.That(view.PartyMembers[0].rectTransform.localScale.x, Is.GreaterThan(0f));
        }

        // 探索の地図のパーティも、隊列の各位置にその枠の人を立たせ、空いた枠の位置は隠す。
        [UnityTest]
        public IEnumerator TheMapWalksTheFormation()
        {
            StartAdventure();
            var bootstrap = default(ExplorationBootstrap);
            yield return SceneTests.Load(
                SceneTests.ExplorationPath,
                (System.Func<ExplorationBootstrap, bool>)(value => value.Flow != null),
                value => bootstrap = value
            );
            yield return SceneTests.WaitUntil(
                () => bootstrap.PartyTask.IsCompleted,
                message: "The party did not load."
            );
            var view = bootstrap.View;
            var data = bootstrap.Party;
            Assert.That(view.PartySlots, Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
            for (int i = 0; i < view.Party.Length; i++)
            {
                string id = Slots[view.PartySlots[i]];
                Assert.That(view.Party[i].gameObject.activeSelf, Is.EqualTo(id != null), $"{i}");
                if (id != null)
                    Assert.That(view.PartySprites[i].texture, Is.SameAs(data.Find(id).Art), id);
            }
        }

        // 冒険の戦闘では、各位置にその枠の人がLvのステータス・満タンのHPで立ち、デッキは4人の
        // 解放した固有スキルとカスタムスキルになる。空いた枠の位置は隠れて、狙えない。
        [UnityTest]
        public IEnumerator TheBattleIsFoughtWithTheFormationAndItsCards()
        {
            var state = StartAdventure();
            AdventureSession.Use(
                services
                    .Adventure.MoveAsync(state.Run.Exits[0], CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
            );
            var bootstrap = default(BattleBootstrap);
            yield return SceneTests.Load(
                SceneTests.BattlePath,
                (System.Func<BattleBootstrap, bool>)(_ => true),
                value => bootstrap = value
            );
            yield return SceneTests.WaitUntil(
                () =>
                    bootstrap.PartyTask.IsCompleted
                    && !bootstrap.Transition.IsPlaying
                    && !bootstrap.Transition.IsCovered
                    && !bootstrap.Battle.Dealing
                    && !bootstrap.Battle.Acting,
                8f,
                "The battle did not get ready."
            );
            var battle = bootstrap.Battle;
            var data = bootstrap.Party;
            var training = bootstrap.Training;
            Assert.That(battle.enabled, Is.True, "The battle runs once the party is read.");
            Assert.That(
                battle.Allies.Select(ally => ally.Slot),
                Is.EquivalentTo(new[] { 0, 1, 2, 3 })
            );

            int expectedCards = 0;
            for (int place = 0; place < battle.Allies.Length; place++)
            {
                var ally = battle.Allies[place];
                string id = Slots[ally.Slot];
                if (id == null)
                {
                    Assert.That(ally.Absent, Is.True);
                    Assert.That(ally.Down, Is.True);
                    Assert.That(ally.Body.gameObject.activeSelf, Is.False);
                    Assert.That(ally.HpBar.gameObject.activeSelf, Is.False);
                    Assert.That(battle.Deck.Any(card => card.Caster == place), Is.False);
                    continue;
                }
                var member = data.Find(id);
                var growth = training.Find(id);
                int level = server.LevelOf(id);
                Assert.That(ally.Absent, Is.False, id);
                Assert.That(ally.Name, Is.EqualTo(member.Name), id);
                Assert.That(ally.Level, Is.EqualTo(level), id);
                Assert.That(ally.MaxHp, Is.EqualTo(TrainingBond.StatsOf(growth, level).Hp), id);
                // パーティより速い敵がいれば戦闘は敵の番から始まるため、もう攻撃を受けていることがある。
                Assert.That(ally.Hp, Is.InRange(0, ally.MaxHp), id);
                Assert.That(ally.Sprite.texture, Is.SameAs(member.Art), id);
                Assert.That(ally.Tint, Is.EqualTo(member.Tint), id);

                // その人のカード：レベルで解放した固有スキル（進化の段階のどれか）と、2枚のカスタムスキル。
                var cards = battle.Deck.Where(card => card.Caster == place).ToArray();
                int unlocked = growth.Uniques.Count(unique => unique.UnlockLevel <= level);
                var uniques = cards.Where(card => UniqueSkillTrees.FindStage(card.Skill) != null);
                Assert.That(uniques.Count(), Is.EqualTo(unlocked), id);
                Assert.That(
                    cards.Select(card => card.Skill).Except(uniques.Select(card => card.Skill)),
                    Is.EquivalentTo(server.CardsOf(id)),
                    id
                );
                Assert.That(cards.All(card => card.Owner == member.Name), Is.True, id);

                // その人のパッシブ：レベルで解放したものだけが戦闘で働く。先に動いた敵に倒されていれば働かない。
                foreach (
                    var passive in ally.Down
                        ? Enumerable.Empty<Combat.CombatPassive>()
                        : Combat.CombatPassives.Of(id)
                )
                    Assert.That(
                        battle.HasPassive(place, passive.Kind),
                        Is.EqualTo(passive.UnlockLevel <= level),
                        $"{id} {passive.Name}"
                    );
                Assert.That(cards.All(card => card.Card != null && card.Art != null), Is.True, id);
                expectedCards += cards.Length;
            }
            // 固有スキルはLv 1・10・30で開く。トーマ（Lv 20）は2つ、アンセルム（Lv 8）とグレタ（Lv 3）は1つ使える。
            Assert.That(battle.Deck, Has.Length.EqualTo(expectedCards));
            Assert.That(expectedCards, Is.EqualTo(4 + 3 + 3));
        }

        // 冒険の戦闘では、固有スキルのカードが部屋の種類と推奨Lvの確率で進化し、
        // 進化はマスの戦闘を終えたときに残る（doc/features/skill-evolution.md）。
        [UnityTest]
        public IEnumerator AUniqueSkillEvolvesInTheAdventuresBattleAndIsKeptWhenTheRoomEnds()
        {
            var state = StartAdventure();
            state = AdventureSession.Use(
                services
                    .Adventure.MoveAsync(state.Run.Exits[0], CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
            );
            var bootstrap = default(BattleBootstrap);
            yield return SceneTests.Load(
                SceneTests.BattlePath,
                (System.Func<BattleBootstrap, bool>)(_ => true),
                value => bootstrap = value
            );
            yield return SceneTests.WaitUntil(
                () =>
                    bootstrap.PartyTask.IsCompleted
                    && !bootstrap.Transition.IsPlaying
                    && !bootstrap.Transition.IsCovered
                    && !bootstrap.Battle.Dealing
                    && !bootstrap.Battle.Acting,
                8f,
                "The battle did not get ready."
            );
            var battle = bootstrap.Battle;

            // The chance is the room's, lowered for a level over the destination's.
            var rules = battle.Evolution;
            Assert.That(rules, Is.Not.Null, "Unique skills may evolve in an adventure's battle.");
            float basePercent = AdventureEvolution.BasePercentOf(state.Run.Room.Kind);
            int recommended = AdventureCatalog.Area(state.Run.DestinationId).Level;
            Assert.That(basePercent, Is.GreaterThan(0f));
            Assert.That(rules.Chance(recommended), Is.EqualTo(basePercent / 100f).Within(1e-6f));
            Assert.That(
                rules.Chance(recommended + 3),
                Is.EqualTo(basePercent / 100f * 0.7f).Within(1e-6f)
            );

            // Toma's first unique skill (its mock progress sets the first branch's first step)
            // evolves when the draw comes under the chance.
            rules.Draw = () => 0f;
            int deck = System.Array.FindIndex(
                battle.Deck,
                card => card.Person == "toma" && card.Unique == 0
            );
            Assert.That(deck, Is.GreaterThanOrEqualTo(0), "Toma's unique skill is in the deck.");
            string from = battle.Deck[deck].Skill;
            var next = UniqueSkillEvolution.NextStages(from);
            Assert.That(next, Is.Not.Empty, from);
            battle.RestartDeal(
                new[] { deck }
                    .Concat(Enumerable.Range(0, battle.Deck.Length).Where(i => i != deck))
                    .ToArray(),
                instant: true
            );
            int card = System.Array.FindIndex(
                battle.Cards,
                value => value.InHand && value.DeckIndex == deck
            );
            var body = battle.Cards[card].Body;
            var press = RectTransformUtility.WorldToScreenPoint(
                null,
                body.TransformPoint(new Vector2(body.rect.center.x, body.rect.yMax - 40f))
            );
            var target = battle.Enemies.First(enemy => enemy.Alive).TargetArea;
            var aim = BattleInspectView.ScreenPointOf(target, target.rect.center);
            battle.PressCard(card, press);
            battle.DragCard(aim);
            battle.ReleaseCard(aim);
            yield return SceneTests.WaitUntil(
                () => battle.Choice.IsOpen,
                6f,
                "The stages to evolve into are offered."
            );
            if (battle.Choice.Count > 1)
                battle.Choice.Select(0);
            battle.Choice.Confirm();
            yield return SceneTests.WaitUntil(
                () => !battle.Evolving && !battle.Acting,
                10f,
                "The evolution and the card end."
            );
            string to = battle.Deck[deck].Skill;
            Assert.That(to, Is.Not.EqualTo(from), "The card is the evolved stage.");
            Assert.That(UniqueSkillEvolution.TryFind(to, out _, out int stage), Is.True);
            Assert.That(
                UniqueSkillSession.ProgressOf("toma", 0, null).SetStage,
                Is.Not.EqualTo(stage),
                "Not kept before the room ends."
            );

            // The room won, the evolution is kept: the stage is acquired and set.
            foreach (var enemy in battle.Enemies)
                enemy.Hp = 0;
            battle.EndTurn();
            yield return SceneTests.WaitUntil(
                () => UniqueSkillSession.ProgressOf("toma", 0, null).SetStage == stage,
                10f,
                "The evolution is kept when the room ends."
            );
            Assert.That(UniqueSkillSession.ProgressOf("toma", 0, null).Has(stage), Is.True);
        }
    }
}
