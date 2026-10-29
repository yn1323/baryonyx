using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    /// <summary>The party's passive skills working in the battle (doc/features/passives.md).</summary>
    public sealed class BattlePassivesSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/BattleLab.unity";
        private Scene loadedScene;

        private FixedRandom random;

        // 手札を配る順、狙う相手、敵の行動を、いつも同じ乱数で決める。
        [SetUp]
        public void FixRandom() => random = FixedRandom.Use();

        [UnityTest]
        public IEnumerator HeadStartDealsOneMoreCardToOpenWith()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            UseParty(view, 0, new[] { CombatPassiveKind.HeadStart });

            view.RestartDeal(instant: true);
            Assert.That(view.HandCards, Has.Count.EqualTo(view.Settings.OpeningDraw + 1));
            Assert.That(Popups(view), Does.Contain("早駆け"));
        }

        [UnityTest]
        public IEnumerator StarChartCarriesAnEnergyLeftIntoTheNextTurn()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            UseParty(view, 0, new[] { CombatPassiveKind.StarChart });
            view.RestartDeal(instant: true);
            Assert.That(view.Energy, Is.EqualTo(3));

            view.EndTurn();
            Assert.That(view.CarriedEnergy, Is.EqualTo(1));
            yield return WaitEnemyTurn(view);
            Assert.That(view.MaxEnergy, Is.EqualTo(3));
            Assert.That(view.Energy, Is.EqualTo(4), "The turn's 3 and the 1 carried over.");
            Assert.That(view.CarriedEnergy, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator CoverTakesTheBlowAimedAtALowAlly()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int aria = 2;
            UseParty(view, aria, new[] { CombatPassiveKind.Cover });
            view.RestartDeal(instant: true);
            // One enemy attacks; only a low ally and Aria stand, so the blow aims at either.
            for (int i = 1; i < view.Enemies.Length; i++)
                view.Enemies[i].Hp = 0;
            int low = 0;
            for (int i = 0; i < view.Allies.Length; i++)
                if (i != low && i != aria)
                    view.Allies[i].Hp = 0;
            view.Allies[low].Hp = view.Allies[low].MaxHp / 5;
            int lowHp = view.Allies[low].Hp;

            view.EndTurn();
            yield return WaitEnemyTurn(view);
            Assert.That(view.Allies[low].Hp, Is.EqualTo(lowHp), "Aria took the blow.");
            Assert.That(view.Allies[aria].Hp, Is.LessThan(view.Allies[aria].MaxHp));
        }

        [UnityTest]
        public IEnumerator ChainedArrowsLandsARepeatedBlowOnceMore()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            // A card that only strikes: not one that discards the hand first (千刃乱舞).
            var repeated = CardSkills.All.First(skill =>
                skill.Cost <= BattleInspectView.EnergyPerTurn
                && skill.Actions.All(action => action.Kind != CardActionKind.Discard)
                && skill.Actions.Any(action =>
                    action.Kind == CardActionKind.Damage
                    && action.Hits > 1
                    && action.Target == CardTarget.RandomEnemies
                )
            );
            int hits = repeated.Actions.First(action => action.Hits > 1).Hits;
            UseParty(view, 1, new[] { CombatPassiveKind.ChainedArrows }, repeated);
            int card = DealInto(view, 1, repeated.Id);
            // The enemies outlast the blows, so every one shows its number.
            foreach (var enemy in view.Enemies)
                enemy.Hp = enemy.MaxHp = 999999;

            var seen = new Seen();
            Play(view, card, view.Enemies[0].TargetArea);
            yield return WaitActions(view, seen);
            Assert.That(seen.Numbers, Has.Count.EqualTo(hits + 1));
            Assert.That(seen.Texts, Does.Contain("連ね矢"));
        }

        // 連続攻撃の途中で倒れた敵は、最後の一撃まで残ってから崩れる。ランダムの残りの撃は、
        // 倒れた敵を狙わず、立っている敵へ向かう（doc/features/combat.md#スキルの仮設定）。
        [UnityTest]
        public IEnumerator AnEnemyBeatenMidwayStaysUntilTheLastBlowAndTheRestGoElsewhere()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var repeated = CardSkills.All.First(skill =>
                skill.Cost <= BattleInspectView.EnergyPerTurn
                && skill.Actions.All(action => action.Kind != CardActionKind.Discard)
                && skill.Actions.Any(action =>
                    action.Kind == CardActionKind.Damage
                    && action.Hits > 1
                    && action.Target == CardTarget.RandomEnemies
                )
            );
            int hits = repeated.Actions.First(action => action.Hits > 1).Hits;
            UseParty(view, 1, new CombatPassiveKind[0], repeated);
            int card = DealInto(view, 1, repeated.Id);
            // Every blow is rolled onto the last enemy standing, which the first blow beats.
            view.Dice = new BattleDice(count => count - 1);
            var standing = view.Enemies[0];
            var beaten = view.Enemies[^1];
            for (int i = 1; i < view.Enemies.Length - 1; i++)
                view.Enemies[i].Hp = 0;
            standing.Hp = standing.MaxHp = 999999;
            beaten.Hp = 1;

            var seen = new Seen();
            float shownAtLastBlow = -1f;
            Play(view, card, standing.TargetArea);
            for (float t = 0f; view.Acting && t < 10f; t += Time.deltaTime)
            {
                yield return null;
                seen.Look(view);
                if (shownAtLastBlow < 0f && seen.Numbers.Count >= hits)
                    shownAtLastBlow = beaten.Group.alpha;
            }
            Assert.That(view.Acting, Is.False, "The actions end.");
            Assert.That(beaten.Alive, Is.False);
            Assert.That(seen.Numbers, Has.Count.EqualTo(hits), "No blow is lost on the beaten.");
            Assert.That(standing.Hp, Is.LessThan(standing.MaxHp), "The rest go to the one standing.");
            Assert.That(shownAtLastBlow, Is.EqualTo(1f), "The beaten one is there at the last blow.");
            for (float t = 0f; beaten.Group.alpha > 0f && t < 2f; t += Time.deltaTime)
                yield return null;
            Assert.That(beaten.Group.alpha, Is.EqualTo(0f), "Then it crumbles away.");
        }

        // 装備の「攻撃スキルが2回発動」（doc/features/equipment.md#装備の効果）も、ここで確かめる。
        // 確率が100%なら、攻撃のカードの効果が同じ敵にもう一度起きる。
        [UnityTest]
        public IEnumerator AnEquipmentsChanceToPlayTwiceLandsTheAttackOnceMore()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var single = CardSkills.All.First(skill =>
                skill.Cost <= 5
                && skill.Actions.Count == 1
                && skill.Actions[0].Kind == CardActionKind.Damage
                && skill.Actions[0].Hits <= 1
                && skill.Actions[0].Target == CardTarget.OneEnemy
            );
            UseParty(view, 0, new CombatPassiveKind[0], single, twice: 100);
            int card = DealInto(view, 0, single.Id);
            foreach (var enemy in view.Enemies)
                enemy.Hp = enemy.MaxHp = 999999;

            var seen = new Seen();
            Play(view, card, view.Enemies[0].TargetArea);
            seen.Look(view);
            for (float t = 0f; view.Acting && t < 10f; t += Time.deltaTime)
            {
                yield return null;
                seen.Look(view);
            }
            Assert.That(view.Acting, Is.False, "The actions end.");
            Assert.That(seen.Numbers, Has.Count.EqualTo(2));
            Assert.That(seen.Texts, Does.Contain(BattleInspectView.TwiceText));
            for (int i = 1; i < view.Enemies.Length; i++)
                Assert.That(
                    view.Enemies[i].Hp,
                    Is.EqualTo(999999),
                    "Only the enemy aimed at is hit."
                );
        }

        [UnityTest]
        public IEnumerator OverflowingPrayerTurnsHealingPastTheMaxIntoBlock()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var heal = CardSkills.All.First(skill =>
                skill.Cost <= 5
                && skill.Target == CardTarget.OneAlly
                && skill.Actions.Any(action => action.Kind == CardActionKind.Heal)
            );
            int mina = 3;
            UseParty(view, mina, new[] { CombatPassiveKind.OverflowingPrayer }, heal);
            int card = DealInto(view, mina, heal.Id);
            Assert.That(view.BlockOf(0), Is.EqualTo(0));

            Play(view, card, view.Allies[0].TargetArea);
            yield return WaitActions(view);
            Assert.That(view.Allies[0].Hp, Is.EqualTo(view.Allies[0].MaxHp));
            Assert.That(view.BlockOf(0), Is.GreaterThan(0), "All of the heal went past the max.");
        }

        [UnityTest]
        public IEnumerator ChargedShieldStrikesAnEnemyWhenLutzGetsBlock()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var guard = CardSkills.All.First(skill =>
                skill.Cost <= 5
                && skill.Kind != CardKind.Attack
                && skill.Actions.Any(action =>
                    action.Kind == CardActionKind.Block && action.Target == CardTarget.AllAllies
                )
            );
            int lutz = 1;
            UseParty(view, lutz, new[] { CombatPassiveKind.ChargedShield }, guard);
            int card = DealInto(view, lutz, guard.Id);
            int before = view.Enemies.Sum(enemy => enemy.Hp);

            Play(view, card, view.Allies[lutz].TargetArea);
            yield return WaitActions(view);
            // The bolt's effect lands after the card's.
            for (
                float t = 0f;
                view.Enemies.Sum(enemy => enemy.Hp) == before && t < 3f;
                t += Time.deltaTime
            )
                yield return null;
            Assert.That(view.BlockOf(lutz), Is.GreaterThan(0));
            Assert.That(view.Enemies.Sum(enemy => enemy.Hp), Is.LessThan(before));
        }

        [UnityTest]
        public IEnumerator CopycatWatchesTheOthersIceAndThunder()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var ice = CardSkills.All.First(skill =>
                skill.Cost <= 5
                && skill.Element == CardElement.Ice
                && skill.Target == CardTarget.OneEnemy
            );
            UseParty(
                view,
                3,
                new[] { CombatPassiveKind.Copycat, CombatPassiveKind.StandingAlone },
                ice,
                cardsOf: 0
            );
            int card = DealInto(view, 0, ice.Id);

            var seen = new Seen();
            Play(view, card, view.Enemies[0].TargetArea);
            yield return WaitActions(view, seen);
            Assert.That(view.CopycatSeen, Is.EqualTo(1));
            Assert.That(seen.Texts, Does.Contain("見よう見まね +10%"));
        }

        [UnityTest]
        public IEnumerator AFallenHoldersPassiveStopsWorking()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            UseParty(view, 0, new[] { CombatPassiveKind.HeadStart });
            Assert.That(view.HasPassive(0, CombatPassiveKind.HeadStart), Is.True);
            Assert.That(view.HasPassive(1, CombatPassiveKind.HeadStart), Is.False);

            view.Allies[0].Hp = 0;
            Assert.That(view.HasPassive(0, CombatPassiveKind.HeadStart), Is.False);
            Assert.That(view.PassiveHolder(CombatPassiveKind.HeadStart), Is.EqualTo(-1));
        }

        /// <summary>
        /// Puts the battle's own party back with its stats, giving the ally at
        /// <paramref name="holder"/> the passives, and <paramref name="card"/> to the ally at
        /// <paramref name="cardsOf"/> (the holder when -1) on top of each ally's two cards.
        /// </summary>
        private static void UseParty(
            BattleInspectView view,
            int holder,
            IReadOnlyList<CombatPassiveKind> passives,
            CardSkill card = null,
            int cardsOf = -1,
            int twice = 0
        )
        {
            if (cardsOf < 0)
                cardsOf = holder;
            var slots = new BattlePartyMember[view.Allies.Length];
            for (int place = 0; place < view.Allies.Length; place++)
            {
                var ally = view.Allies[place];
                var cards = view
                    .Deck.Where(data => data.Caster == place)
                    .Take(2)
                    .Select(data => BattleInspectView.SkillOf(data))
                    .Where(skill => skill != null)
                    .ToList();
                if (card != null && place == cardsOf)
                    cards.Add(card);
                slots[ally.Slot] = new BattlePartyMember
                {
                    Name = ally.Name,
                    Art = ally.Sprite.texture,
                    Level = ally.Level,
                    Stats = ally.Stats,
                    Cards = cards.Select(skill => new BattlePartyCard { Skill = skill }).ToArray(),
                    Passives = place == holder ? passives : new CombatPassiveKind[0],
                    TwiceChance = place == holder ? twice : 0,
                };
            }
            view.UseParty(slots);
        }

        /// <summary>Deals <paramref name="skill"/> of the ally at <paramref name="caster"/> into the opening hand, and returns its card.</summary>
        private static int DealInto(BattleInspectView view, int caster, string skill)
        {
            int deck = System.Array.FindIndex(
                view.Deck,
                data => data.Caster == caster && data.Skill == skill
            );
            Assert.That(deck, Is.GreaterThanOrEqualTo(0), skill);
            view.RestartDeal(
                new[] { deck }
                    .Concat(Enumerable.Range(0, view.Deck.Length).Where(i => i != deck))
                    .ToArray(),
                instant: true
            );
            return System.Array.FindIndex(
                view.Cards,
                card => card.InHand && card.DeckIndex == deck
            );
        }

        private static void Play(BattleInspectView view, int card, RectTransform target)
        {
            view.PressCard(card, CardPoint(view.Cards[card]));
            view.DragCard(Center(target));
            view.ReleaseCard(Center(target));
        }

        private static Vector2 CardPoint(BattleInspectCard card)
        {
            var rect = card.Body.rect;
            return RectTransformUtility.WorldToScreenPoint(
                null,
                card.Body.TransformPoint(new Vector2(rect.center.x, rect.yMax - 40f))
            );
        }

        private static Vector2 Center(RectTransform rect) =>
            BattleInspectView.ScreenPointOf(rect, rect.rect.center);

        /// <summary>The popups now rising.</summary>
        private static string[] Popups(BattleInspectView view) =>
            Clones(view.PopupTemplate).Select(text => text.text).ToArray();

        /// <summary>The numbers and popups that have risen while the actions played (they fade on the way).</summary>
        private sealed class Seen
        {
            public readonly HashSet<TMP_Text> Numbers = new();
            public readonly List<string> Texts = new();
            private readonly HashSet<TMP_Text> popups = new();

            public void Look(BattleInspectView view)
            {
                foreach (var template in new[] { view.DamageNumber, view.WeakNumber })
                    Numbers.UnionWith(Clones(template));
                // Read as it shows: a popup is destroyed once it fades.
                foreach (var popup in Clones(view.PopupTemplate))
                    if (popups.Add(popup))
                        Texts.Add(popup.text);
            }
        }

        private static IEnumerable<TMP_Text> Clones(TMP_Text template) =>
            template
                .transform.parent.GetComponentsInChildren<TMP_Text>()
                .Where(text => text != template && text.name == template.name + "(Clone)");

        private static IEnumerator WaitActions(BattleInspectView view, Seen seen = null)
        {
            seen?.Look(view);
            for (float t = 0f; view.Acting && t < 5f; t += Time.deltaTime)
            {
                yield return null;
                seen?.Look(view);
            }
            Assert.That(view.Acting, Is.False, "The actions end.");
        }

        private static IEnumerator WaitEnemyTurn(BattleInspectView view)
        {
            for (float t = 0f; view.EnemyTurn && t < 10f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.EnemyTurn, Is.False, "The enemy turn ends.");
        }

        private IEnumerator Load(System.Action<BattleInspectView> found)
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            yield return null;
            found(
                loadedScene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<BattleInspectView>(true))
                    .Single()
            );
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Time.timeScale = 1f;
            random?.Dispose();
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
